using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EdgeTtsSharp;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// srt/vtt -> Edge TTS clip per cue -> sample-accurate timeline -> one MP3,
/// then optionally the MP3 is added to the video as a second audio track.
///
/// Differences from the WPF version, on purpose:
///  - The timeline is raw PCM written in C#, so every cue starts at its exact sample and
///    nothing drifts. The WPF flow concatenated hundreds of separately encoded MP3 files,
///    and each encode adds a few tens of ms of padding that accumulates.
///  - The MP3 is encoded once, at the end.
///  - Every ffmpeg run has its exit code checked and can be cancelled.
///  - The embed step never uses -shortest, so a short voice track can't truncate the video.
/// </summary>
public sealed class VoiceoverService
{
    private static readonly string[] DefaultIgnoreWords =
    {
        "[music]", "(music)", "[applause]", "(applause)", "[noise]", "(noise)",
        "[laughter]", "(laughter)", "[sighs]", "[clears throat]"
    };

    private static readonly Regex LangSuffix =
        new(@"\.[a-z]{2,3}(-[a-z]{2,4})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly VoiceoverSettings _s;
    private readonly FfmpegRunner _ffmpeg;
    private readonly ILogger<VoiceoverService> _logger;

    public VoiceoverService(
        IOptions<VoiceoverSettings> settings,
        FfmpegRunner ffmpeg,
        ILogger<VoiceoverService> logger)
    {
        _s = settings.Value;
        _ffmpeg = ffmpeg;
        _logger = logger;
    }

    public async Task<VoiceoverResult> RunAsync(VoiceoverJob job, Func<JobUpdate, Task> report, CancellationToken ct)
    {
        Task ReportState(string text, int percent) =>
            report(new JobUpdate(job.JobId, "state", State: text, Percent: percent));
        Task ReportLog(string text) =>
            report(new JobUpdate(job.JobId, "log", Message: text));

        Directory.CreateDirectory(job.WorkDir);

        // 1. Read and prepare the subtitle ------------------------------------------------
        await ReportState("Reading subtitles", 1);
        var raw = await File.ReadAllTextAsync(job.SrtPath, Encoding.UTF8, ct);

        if (_s.CleanSubtitles && string.Equals(Path.GetExtension(job.SrtPath), ".srt", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var cleaned = SubtitleCleanerService.CleanText(raw, out var kept);
                if (kept > 0)
                {
                    raw = cleaned;
                    await ReportLog($"Cleaned subtitle: {kept} cues kept.");
                }
            }
            catch (Exception ex)
            {
                await ReportLog($"Subtitle cleanup skipped: {ex.Message}");
            }
        }

        var cues = SubtitleCueParser.Parse(raw, BuildIgnoreRegex());
        if (cues.Count == 0)
            throw new InvalidOperationException("No usable subtitle cues were found in this file.");
        await ReportLog($"{cues.Count} cues to voice.");

        // 2. Voice every cue and lay it on the timeline ------------------------------------
        var voice = await EdgeTts.GetVoice(job.Voice);
        var sampleRate = _s.SampleRate;
        var pcmPath = Path.Combine(job.WorkDir, "timeline.pcm");
        var cuePath = Path.Combine(job.WorkDir, "cue.mp3");
        long writtenSamples = 0;
        var overruns = 0;

        async Task SynthesizeAsync(string text)
        {
            for (var attempt = 1; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(cuePath)) File.Delete(cuePath);
                    await voice.SaveAudioToFile(text, cuePath);
                    if (File.Exists(cuePath) && new FileInfo(cuePath).Length > 0) return;
                    throw new IOException("The voice service returned no audio.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < _s.MaxSynthesisRetries)
                {
                    await ReportLog($"Retrying a cue (attempt {attempt}): {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
                }
            }
        }

        await using (var pcm = new FileStream(pcmPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, useAsync: true))
        {
            var lastPercent = -1;
            for (var i = 0; i < cues.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var cue = cues[i];

                await SynthesizeAsync(cue.Text);

                using var clip = new MemoryStream();
                await _ffmpeg.RunAsync(DecodeArgs(cuePath, null), ct, clip);

                var natural = clip.Length / 2.0 / sampleRate;
                var target = cue.TargetSeconds;
                if (target > 0 && natural > target * 1.02)
                {
                    var ratio = Math.Min(natural / target, _s.MaxSpeedUp);
                    clip.SetLength(0);
                    await _ffmpeg.RunAsync(DecodeArgs(cuePath, ratio), ct, clip);
                    if (clip.Length / 2.0 / sampleRate > target + 0.1) overruns++;
                }

                // Pad with silence up to this cue's start. If the previous clip overran,
                // the cue simply starts as soon as the timeline is free.
                var startSample = (long)Math.Round(cue.Start.TotalSeconds * sampleRate);
                if (startSample > writtenSamples)
                {
                    await WriteSilenceAsync(pcm, startSample - writtenSamples, ct);
                    writtenSamples = startSample;
                }

                clip.Position = 0;
                await clip.CopyToAsync(pcm, ct);
                writtenSamples += clip.Length / 2;

                var percent = 5 + (int)(85.0 * (i + 1) / cues.Count);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    await ReportState($"Voicing cue {i + 1} of {cues.Count}", percent);
                }
            }
        }

        if (overruns > 0)
            await ReportLog($"{overruns} cue(s) were still longer than their time slot at the maximum speed-up and run into the gap after them.");

        // 3. Encode the MP3 once ------------------------------------------------------------
        await ReportState("Encoding MP3", 92);
        var baseName = BaseNameFor(job);
        var mp3Final = UniquePath(Path.Combine(_s.OutputFolder, "mp3"), baseName + "_Voice", ".mp3");
        Directory.CreateDirectory(Path.GetDirectoryName(mp3Final)!);
        var mp3Part = Path.ChangeExtension(mp3Final, ".part.mp3");

        try
        {
            await _ffmpeg.RunAsync(new[]
            {
                "-y", "-f", "s16le", "-ar", sampleRate.ToString(CultureInfo.InvariantCulture), "-ac", "1", "-i", pcmPath,
                "-c:a", "libmp3lame", "-b:a", _s.Mp3Bitrate,
                "-metadata", $"title={baseName}",
                mp3Part
            }, ct);
            File.Move(mp3Part, mp3Final);
        }
        catch
        {
            TryDelete(mp3Part);
            throw;
        }

        if (job.Mode == VoiceoverMode.Mp3)
            return new VoiceoverResult(mp3Final, null);

        // 4. Add the MP3 to the video as an extra audio track -------------------------------
        await ReportState("Reading video", 94);
        var probe = await _ffmpeg.ProbeAsync(job.VideoPath!, ct);
        var ext = Path.GetExtension(job.VideoPath!).ToLowerInvariant();
        var videoFinal = UniquePath(
            Path.Combine(_s.OutputFolder, "video"),
            $"{baseName}_{Sanitize(_s.VoiceTrackTitle)}Voiceover",
            ext);
        Directory.CreateDirectory(Path.GetDirectoryName(videoFinal)!);
        var videoPart = Path.ChangeExtension(videoFinal, ".part" + ext);

        await ReportState("Embedding audio into video", 96);
        await ReportLog(probe.AudioStreams == 0
            ? "The video has no audio track; the voice track will be its only audio."
            : $"Keeping the video's {probe.AudioStreams} original audio track(s) and adding the {_s.VoiceTrackTitle} track.");

        try
        {
            await _ffmpeg.RunAsync(BuildEmbedArgs(job.VideoPath!, mp3Final, videoPart, ext, probe), ct);
            File.Move(videoPart, videoFinal);
        }
        catch (FfmpegException ex)
        {
            TryDelete(videoPart);
            throw new FfmpegException($"The MP3 was saved as '{Path.GetFileName(mp3Final)}', but adding it to the video failed. {ex.Message}");
        }
        catch
        {
            TryDelete(videoPart);
            throw;
        }

        return new VoiceoverResult(mp3Final, videoFinal);
    }

    // ffmpeg decodes a TTS clip to raw mono 16-bit PCM on stdout, optionally sped up.
    private IEnumerable<string> DecodeArgs(string input, double? tempo)
    {
        var args = new List<string> { "-i", input };
        if (tempo is > 1.0)
        {
            args.Add("-filter:a");
            args.Add(TempoFilter(tempo.Value));
        }
        args.AddRange(new[] { "-f", "s16le", "-ar", _s.SampleRate.ToString(CultureInfo.InvariantCulture), "-ac", "1", "-" });
        return args;
    }

    // Chained so ratios above 2.0 work on older ffmpeg builds too.
    private static string TempoFilter(double ratio)
    {
        string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
        return ratio <= 2.0 ? $"atempo={F(ratio)}" : $"atempo=2.0,atempo={F(ratio / 2.0)}";
    }

    private IEnumerable<string> BuildEmbedArgs(string video, string mp3, string output, string ext, MediaProbe probe)
    {
        var n = probe.AudioStreams;           // original audio tracks, kept as-is
        var args = new List<string> { "-y", "-i", video, "-i", mp3, "-map", "0:v:0" };

        if (n > 0) args.AddRange(new[] { "-map", "0:a" });
        args.AddRange(new[] { "-map", "1:a:0", "-c:v", "copy" });

        for (var k = 0; k < n; k++) args.AddRange(new[] { $"-c:a:{k}", "copy" });
        args.AddRange(new[] { $"-c:a:{n}", "aac", $"-b:a:{n}", _s.EmbeddedAudioBitrate });

        if (n == 1) args.AddRange(new[] { "-metadata:s:a:0", $"title={_s.OriginalTrackTitle}" });
        args.AddRange(new[]
        {
            $"-metadata:s:a:{n}", $"title={_s.VoiceTrackTitle}",
            $"-metadata:s:a:{n}", $"language={_s.VoiceLanguageCode}"
        });

        if (_s.MakeVoiceTrackDefault && n > 0)
        {
            for (var k = 0; k < n; k++) args.AddRange(new[] { $"-disposition:a:{k}", "0" });
            args.AddRange(new[] { $"-disposition:a:{n}", "default" });
        }

        // Cap the output at the video's length instead of using -shortest: a voice track that
        // ends early leaves the video intact, and one that overruns is trimmed.
        if (probe.Duration is { } d)
            args.AddRange(new[] { "-t", d.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture) });

        if (ext is ".mp4" or ".m4v" or ".mov")
            args.AddRange(new[] { "-movflags", "+faststart" });

        args.Add(output);
        return args;
    }

    private static async Task WriteSilenceAsync(Stream s, long samples, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var remaining = samples * 2; // 16-bit mono
        while (remaining > 0)
        {
            var n = (int)Math.Min(buffer.Length, remaining);
            await s.WriteAsync(buffer.AsMemory(0, n), ct);
            remaining -= n;
        }
    }

    private Regex? BuildIgnoreRegex()
    {
        var words = LoadIgnoreWords();
        if (words.Count == 0) return null;
        // Longest first, so "[clears throat]" wins over any shorter overlapping entry.
        var pattern = string.Join("|", words.OrderByDescending(w => w.Length).Select(Regex.Escape));
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private List<string> LoadIgnoreWords()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, _s.IgnoreWordsFile);
            if (!File.Exists(path))
                File.WriteAllLines(path, DefaultIgnoreWords, new UTF8Encoding(false));

            return File.ReadAllLines(path, Encoding.UTF8)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read {File}; using the built-in ignore list.", _s.IgnoreWordsFile);
            return DefaultIgnoreWords.ToList();
        }
    }

    // MP3-only jobs are named after the subtitle; embed jobs after the video, so the pair stays together.
    private static string BaseNameFor(VoiceoverJob job)
    {
        if (job.Mode == VoiceoverMode.Mp3AndEmbed && job.VideoPath is not null)
            return Sanitize(Path.GetFileNameWithoutExtension(job.VideoPath));

        var name = Path.GetFileNameWithoutExtension(job.SrtPath);
        return Sanitize(LangSuffix.Replace(name, ""));
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        if (name.Length > 150) name = name[..150];
        return name.Length == 0 ? "voiceover" : name;
    }

    private static string UniquePath(string dir, string name, string ext)
    {
        var path = Path.Combine(dir, name + ext);
        for (var n = 2; File.Exists(path); n++)
            path = Path.Combine(dir, $"{name} ({n}){ext}");
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
