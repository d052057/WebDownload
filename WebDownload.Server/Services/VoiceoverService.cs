using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EdgeTtsSharp;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// srt/vtt -> Edge TTS clip per cue -> sample-accurate timeline -> one MP3,
/// then optionally the MP3 is added to the video as an extra audio track.
///
/// Differences from the WPF version, on purpose:
///  - The timeline is raw PCM written in C#, so every cue starts at its exact sample. The WPF
///    flow concatenated hundreds of separately encoded MP3 files, and each encode adds a few
///    tens of ms of padding that accumulates.
///  - Each clip is fitted to the time that is actually left for it. If an earlier clip ran late,
///    the next one is sped up to catch up, so lateness can't pile up over a long file.
///  - A pitch change also changes speed (asetrate); that is cancelled out so the clip keeps the
///    length it was fitted to. The WPF filter did not, so pitched clips came out the wrong length.
///  - The MP3 is encoded once, at the end.
///  - Every ffmpeg run has its exit code checked and can be cancelled.
///  - The embed step never uses -shortest, so a short voice track can't truncate the video.
/// </summary>
public sealed class VoiceoverService
{
    private static readonly Regex LangSuffix =
        new(@"\.[a-z]{2,3}(-[a-z]{2,4})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly VoiceoverSettings _s;
    private readonly FfmpegRunner _ffmpeg;
    private readonly VoiceAnalysisService _analysis;
    private readonly ILogger<VoiceoverService> _logger;

    public VoiceoverService(
        IOptions<VoiceoverSettings> settings,
        FfmpegRunner ffmpeg,
        VoiceAnalysisService analysis,
        ILogger<VoiceoverService> logger)
    {
        _s = settings.Value;
        _ffmpeg = ffmpeg;
        _analysis = analysis;
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

        // 2. Choose the voice, speed and pitch -------------------------------------------------
        // Either as set in the UI, or detected from the video's speaker (like "Analyze MP4" in the WPF app).
        var voiceId = job.Voice;
        var pitchPercent = job.PitchPercent;
        var ratePercent = job.RatePercent;
        SpeakerMap? speakers = null;

        // Always shown in Details, so it is clear whether voice matching actually reached the server.
        await ReportLog(
            $"Options: {job.Mode}, voice matching {(job.MatchVoice ? "on" : "off")}, " +
            $"video {(job.VideoPath is null ? "none" : Path.GetFileName(job.VideoPath))}, voice {job.Voice}, " +
            $"speed {job.RatePercent:+#;-#;0}%, pitch {job.PitchPercent:+#;-#;0}%.");
        if (job.MatchVoice && job.VideoPath is null)
            await ReportLog("Voice matching was requested but no video reached the server, so it is skipped.");

        if (job.MatchVoice && job.VideoPath is not null)
        {
            await ReportState("Analyzing the video's speakers", 3);
            try
            {
                // Per cue: a man and a woman in the video get a male and a female Khmer voice.
                var lastReported = -1;
                speakers = await _analysis.AnalyzeCuesAsync(job.VideoPath, cues, job.WorkDir, async (done, total) =>
                {
                    var percent = 3 + (int)(6.0 * done / total);
                    if (percent == lastReported) return;
                    lastReported = percent;
                    await ReportState($"Analyzing speakers (cue {done} of {total})", percent);
                }, ct);

                if (speakers is not null)
                {
                    ratePercent = 0;
                    await ReportLog(
                        $"Matched voices cue by cue: {speakers.MaleCues} male and {speakers.FemaleCues} female" +
                        (speakers.UnclearCues > 0 ? $" ({speakers.UnclearCues} cues had no clear voice and kept the previous speaker)" : "") + ". " +
                        (speakers.MalePitchHz is { } mh ? $"Male speech averages {mh:F0} Hz (pitch {speakers.MalePitchPercent:+#;-#;0}%). " : "") +
                        (speakers.FemalePitchHz is { } fh ? $"Female speech averages {fh:F0} Hz (pitch {speakers.FemalePitchPercent:+#;-#;0}%)." : ""));
                }
                else
                {
                    // No cue had a clear voice: fall back to one voice for the whole video, like the WPF app.
                    var found = await _analysis.AnalyzeAsync(job.VideoPath, ct);
                    if (found is null)
                    {
                        await ReportLog("No clear speech was found in the video, so the selected voice and sliders are used.");
                    }
                    else
                    {
                        voiceId = found.IsMale ? _s.MaleVoice : _s.FemaleVoice;
                        pitchPercent = found.PitchPercent;
                        ratePercent = 0;
                        await ReportLog(
                            $"Could not match cue by cue. Detected a {(found.IsMale ? "male" : "female")} voice overall (average pitch {found.AveragePitchHz:F0} Hz): " +
                            $"using {voiceId} with pitch {found.PitchPercent:+#;-#;0}%.");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                speakers = null;
                await ReportLog($"Voice analysis skipped: {ex.Message}");
            }
        }

        var rateFactor = 1.0 + ratePercent / 100.0;
        var pitchFactor = 1.0 + pitchPercent / 100.0;

        // 3. Voice every cue and lay it on the timeline ------------------------------------
        // Edge TTS voices. With speaker matching there is one male and one female voice, used per cue.
        var defaultVoice = await EdgeTts.GetVoice(voiceId);
        Func<string, string, Task> saveDefault = async (text, path) => await defaultVoice.SaveAudioToFile(text, path);
        var saveMale = saveDefault;
        var saveFemale = saveDefault;
        if (speakers is not null)
        {
            var maleVoice = await EdgeTts.GetVoice(_s.MaleVoice);
            var femaleVoice = await EdgeTts.GetVoice(_s.FemaleVoice);
            saveMale = async (text, path) => await maleVoice.SaveAudioToFile(text, path);
            saveFemale = async (text, path) => await femaleVoice.SaveAudioToFile(text, path);
        }
        var malePitch = 1.0 + (speakers?.MalePitchPercent ?? 0) / 100.0;
        var femalePitch = 1.0 + (speakers?.FemalePitchPercent ?? 0) / 100.0;
        var sampleRate = _s.SampleRate;
        var pcmPath = Path.Combine(job.WorkDir, "timeline.pcm");
        var cuePath = Path.Combine(job.WorkDir, "cue.mp3");
        long writtenSamples = 0;
        var overruns = 0;

        async Task SynthesizeAsync(Func<string, string, Task> save, string text)
        {
            for (var attempt = 1; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(cuePath)) File.Delete(cuePath);
                    await save(text, cuePath);
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

                var save = speakers is null ? saveDefault : speakers.IsMale[i] ? saveMale : saveFemale;
                var cuePitch = speakers is null ? pitchFactor : speakers.IsMale[i] ? malePitch : femalePitch;

                await SynthesizeAsync(save, cue.Text);

                using var clip = new MemoryStream();
                await _ffmpeg.RunAsync(DecodeArgs(cuePath), ct, clip);
                var natural = clip.Length / 2.0 / sampleRate;

                // The clip starts when its cue starts, or as soon as the timeline is free if an
                // earlier clip ran late. It should end by its cue's end, plus up to MaxBorrowSeconds
                // of the silent gap before the next cue.
                var from = Math.Max(cue.Start.TotalSeconds, writtenSamples / (double)sampleRate);
                var deadline = cue.End.TotalSeconds;
                if (i + 1 < cues.Count)
                    deadline = Math.Max(deadline, Math.Min(cues[i + 1].Start.TotalSeconds, deadline + _s.MaxBorrowSeconds));
                var slot = Math.Max(deadline - from, 0.3);

                var afterRate = natural / rateFactor;
                var fit = afterRate > slot * 1.02 ? afterRate / slot : 1.0;
                var tempo = Math.Clamp(rateFactor * fit, _s.MinClipTempo, _s.MaxSpeedUp);

                if (Math.Abs(tempo - 1.0) > 0.005 || Math.Abs(cuePitch - 1.0) > 0.001)
                {
                    clip.SetLength(0);
                    await _ffmpeg.RunAsync(DecodeArgs(cuePath, tempo, cuePitch), ct, clip);
                }
                if (clip.Length / 2.0 / sampleRate > slot + 0.1) overruns++;

                // Pad with silence up to this cue's start (nothing to pad if it is already late).
                var startSample = (long)Math.Round(cue.Start.TotalSeconds * sampleRate);
                if (startSample > writtenSamples)
                {
                    await WriteSilenceAsync(pcm, startSample - writtenSamples, ct);
                    writtenSamples = startSample;
                }

                clip.Position = 0;
                await clip.CopyToAsync(pcm, ct);
                writtenSamples += clip.Length / 2;

                var percent = 10 + (int)(80.0 * (i + 1) / cues.Count);
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    await ReportState($"Voicing cue {i + 1} of {cues.Count}", percent);
                }
            }
        }

        var audioSeconds = writtenSamples / (double)sampleRate;
        await ReportLog($"Voice track is {Fmt(audioSeconds)} long; the subtitles end at {Fmt(cues[^1].End.TotalSeconds)}.");
        if (overruns > 0)
            await ReportLog($"{overruns} cue(s) are still longer than their time slot at the maximum speed-up ({_s.MaxSpeedUp:0.#}x), so they run into the gap after them. Shorter translations or a higher MaxSpeedUp would tighten this.");

        // 4. Encode the MP3 once ------------------------------------------------------------
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

        // 5. Add the MP3 to the video as an extra audio track -------------------------------
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
        if (probe.Duration is { } videoLength && audioSeconds > videoLength.TotalSeconds + 1)
            await ReportLog($"The voice track is {Fmt(audioSeconds - videoLength.TotalSeconds)} longer than the video, so its end is trimmed to the video's length.");

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

    // ffmpeg decodes a TTS clip to raw mono 16-bit PCM on stdout, with optional speed and pitch changes.
    private IEnumerable<string> DecodeArgs(string input, double tempo = 1.0, double pitch = 1.0)
    {
        var args = new List<string> { "-i", input };
        var filter = BuildFilter(tempo, pitch);
        if (filter is not null)
        {
            args.Add("-filter:a");
            args.Add(filter);
        }
        args.AddRange(new[] { "-f", "s16le", "-ar", _s.SampleRate.ToString(CultureInfo.InvariantCulture), "-ac", "1", "-" });
        return args;
    }

    private string? BuildFilter(double tempo, double pitch)
    {
        string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
        var parts = new List<string>();

        if (Math.Abs(pitch - 1.0) > 0.001)
        {
            // Relabelling the sample rate shifts pitch, but speeds the clip up by the same factor.
            parts.Add($"aresample={_s.TtsSampleRate}");
            parts.Add($"asetrate={F(_s.TtsSampleRate * pitch)}");
            parts.Add($"aresample={_s.SampleRate}");
            tempo /= pitch; // take that side effect back out, so the clip keeps its fitted length
        }

        tempo = Math.Clamp(tempo, _s.MinFilterTempo, _s.MaxFilterTempo);
        if (Math.Abs(tempo - 1.0) > 0.005)
            parts.Add(TempoFilter(tempo));

        return parts.Count == 0 ? null : string.Join(",", parts);
    }

    // atempo accepts 0.5 to 2.0 per stage on older ffmpeg builds, so wider ratios are chained.
    private static string TempoFilter(double ratio)
    {
        string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
        if (ratio > 2.0) return $"atempo=2.0,atempo={F(ratio / 2.0)}";
        if (ratio < 0.5) return $"atempo=0.5,atempo={F(ratio / 0.5)}";
        return $"atempo={F(ratio)}";
    }

    private IEnumerable<string> BuildEmbedArgs(string video, string mp3, string output, string ext, MediaProbe probe)
    {
        var n = probe.AudioStreams;           // original audio tracks, kept as-is
        var args = new List<string> { "-y", "-i", video, "-i", mp3, "-map", "0:v:0" };

        if (n > 0) args.AddRange(new[] { "-map", "0:a" });
        args.AddRange(new[] { "-map", "1:a:0", "-c:v", "copy" });

        for (var k = 0; k < n; k++) args.AddRange(new[] { $"-c:a:{k}", "copy" });

        // WebM only allows Opus or Vorbis audio, so a .webm video gets an Opus voice track;
        // every other supported container takes AAC.
        var voiceCodec = ext == ".webm" ? "libopus" : "aac";
        args.AddRange(new[] { $"-c:a:{n}", voiceCodec, $"-b:a:{n}", _s.EmbeddedAudioBitrate });

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
        var words = _s.GetIgnoreWords();
        if (words.Count == 0) return null;
        // Longest first, so "[clears throat]" wins over any shorter overlapping entry.
        var pattern = string.Join("|", words.OrderByDescending(w => w.Length).Select(Regex.Escape));
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    // MP3-only jobs are named after the subtitle; embed jobs after the video, so the pair stays together.
    private static string BaseNameFor(VoiceoverJob job)
    {
        if (job.Mode == VoiceoverMode.Mp3AndEmbed && job.VideoPath is not null)
            return Sanitize(Path.GetFileNameWithoutExtension(job.VideoPath));

        var name = Path.GetFileNameWithoutExtension(job.SrtPath);
        return Sanitize(LangSuffix.Replace(name, ""));
    }

    private static string Fmt(double seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"h\:mm\:ss");

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
