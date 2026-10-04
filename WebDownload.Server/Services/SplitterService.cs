using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// media file -> audio extracted to WAV -> Demucs ("vocals" and "no_vocals") -> optional voice cleanup
/// -> MP3 files, plus (for files with a picture) a video with the new soundtrack.
///
/// The picture is copied, never re-encoded, so only the audio steps take time. Original subtitles and
/// extra audio tracks are not carried into the new video.
/// </summary>
public sealed class SplitterService
{
    private readonly SplitterSettings _s;
    private readonly FfmpegRunner _ffmpeg;
    private readonly DemucsRunner _demucs;
    private readonly DeviceDetector _device;
    private readonly MediaPathResolver _paths;

    public SplitterService(
        IOptions<SplitterSettings> settings,
        FfmpegRunner ffmpeg,
        DemucsRunner demucs,
        DeviceDetector device,
        MediaPathResolver paths)
    {
        _s = settings.Value;
        _ffmpeg = ffmpeg;
        _demucs = demucs;
        _device = device;
        _paths = paths;
    }

    public async Task<IReadOnlyList<SplitterFile>> RunAsync(
        SplitterJob job, Func<SplitterUpdate, Task> report, CancellationToken ct)
    {
        Task ReportState(string text, int percent) =>
            report(new SplitterUpdate(job.JobId, "state", State: text, Percent: percent));
        Task ReportLog(string text) =>
            report(new SplitterUpdate(job.JobId, "log", Message: text));

        Directory.CreateDirectory(job.WorkDir);
        var ext = Path.GetExtension(job.MediaPath).ToLowerInvariant();
        var hasPicture = _s.GetVideoExtensions().Contains(ext, StringComparer.OrdinalIgnoreCase);

        // 1. Pull the audio out as a plain WAV with an ASCII name. Demucs then never sees the original
        //    file name (Khmer characters in a path can trip Python on Windows) or an odd container.
        await ReportState("Reading the media", 1);
        var probe = await _ffmpeg.ProbeAsync(job.MediaPath, ct);
        if (probe.AudioStreams == 0)
            throw new InvalidOperationException("This file has no audio track to separate.");

        await ReportState("Extracting the audio", 2);
        var inputWav = Path.Combine(job.WorkDir, "input.wav");
        await _ffmpeg.RunAsync(new[]
        {
            "-y", "-i", job.MediaPath, "-vn", "-map", "0:a:0",
            "-ac", "2", "-ar", "44100", "-c:a", "pcm_s16le", inputWav
        }, ct);

        // 2. Separate. Use the GPU when the hardware check says PyTorch can; if a GPU run fails
        //    (out of memory, driver problems), say so and run again on the CPU.
        var device = await _device.GetAsync(refresh: false, ct);
        var outDir = Path.Combine(job.WorkDir, "demucs");

        async Task SeparateAsync(string dev)
        {
            var where = dev == "cuda" ? "GPU" : "CPU";
            await ReportState($"Separating voice and music ({where})", 5);
            await _demucs.RunAsync(
                inputWav, outDir, job.Model, dev,
                percent => ReportState($"Separating voice and music ({where})", 5 + (int)(80 * percent / 100.0)),
                ReportLog, ct);
        }

        try
        {
            await SeparateAsync(device.Device);
        }
        catch (DemucsException ex) when (device.Device == "cuda")
        {
            await ReportLog($"The GPU run failed ({ex.Message}). Trying again on the CPU.");
            TryDeleteDirectory(outDir);
            await SeparateAsync("cpu");
        }

        string? FindStem(string fileName) =>
            Directory.Exists(outDir)
                ? Directory.EnumerateFiles(outDir, fileName, SearchOption.AllDirectories).FirstOrDefault()
                : null;

        var vocals = FindStem("vocals.wav")
            ?? throw new DemucsException("Demucs finished but did not produce vocals.wav.");
        var music = FindStem("no_vocals.wav")
            ?? throw new DemucsException("Demucs finished but did not produce no_vocals.wav.");
        TryDelete(inputWav); // the longest file in the work folder; no longer needed

        // 3. Voice cleanup works on the separated voice.
        if (job.Mode == SplitterMode.VoiceCleanup)
        {
            await ReportState("Cleaning the voice", 87);
            var clean = Path.Combine(job.WorkDir, "voice_clean.wav");
            if (string.IsNullOrWhiteSpace(_s.CleanupFilter))
            {
                File.Copy(vocals, clean, overwrite: true);
            }
            else
            {
                // loudnorm resamples internally, so the rate is set explicitly.
                await _ffmpeg.RunAsync(new[]
                {
                    "-y", "-i", vocals, "-af", _s.CleanupFilter, "-ar", "44100", "-c:a", "pcm_s16le", clean
                }, ct);
            }
            vocals = clean;
        }

        // 4. What to produce for each button.
        var baseName = Sanitize(Path.GetFileNameWithoutExtension(job.MediaPath));
        var voiceTitle = _s.VoiceTrackTitle;
        var musicTitle = _s.MusicTrackTitle;

        // audio files: label, wav source, name tag
        var audio = new List<(string Label, string Wav, string Tag)>();
        // video: label, name tag, audio tracks (first one is the default)
        string videoLabel;
        string videoTag;
        var tracks = new List<(string Wav, string Title)>();

        switch (job.Mode)
        {
            case SplitterMode.KeepVoice:
                audio.Add(("Voice (MP3)", vocals, "[voice]"));
                videoLabel = "Video with only the voice"; videoTag = "[voice]";
                tracks.Add((vocals, voiceTitle));
                break;
            case SplitterMode.KeepMusic:
                audio.Add(("Music (MP3)", music, "[music]"));
                videoLabel = "Video with only the music"; videoTag = "[music]";
                tracks.Add((music, musicTitle));
                break;
            case SplitterMode.KeepBoth:
                audio.Add(("Music (MP3)", music, "[music]"));
                audio.Add(("Voice (MP3)", vocals, "[voice]"));
                videoLabel = $"Video with {musicTitle} and {voiceTitle} audio tracks"; videoTag = "[music+voice]";
                tracks.Add((music, musicTitle));
                tracks.Add((vocals, voiceTitle));
                break;
            default: // VoiceCleanup
                audio.Add(("Cleaned voice (MP3)", vocals, "[voice clean]"));
                videoLabel = "Video with the cleaned voice"; videoTag = "[voice clean]";
                tracks.Add((vocals, $"{voiceTitle} (cleaned)"));
                break;
        }

        var files = new List<SplitterFile>();
        var audioFolder = Path.Combine(_s.OutputFolder, "audio");
        var videoFolder = Path.Combine(_s.OutputFolder, "video");

        var step = 0;
        foreach (var (label, wav, tag) in audio)
        {
            await ReportState($"Encoding {label}", 90 + step++ * 2);
            var path = await EncodeMp3Async(wav, audioFolder, $"{baseName} {tag}", $"{baseName} {tag}", ct);
            files.Add(new SplitterFile(label, Path.GetFileName(path), _paths.ToMediaUrl(path)));
        }

        if (hasPicture)
        {
            await ReportState("Building the video", 96);
            var path = await BuildVideoAsync(job.MediaPath, ext, tracks, videoFolder, $"{baseName} {videoTag}", ct);
            files.Add(new SplitterFile(videoLabel, Path.GetFileName(path), _paths.ToMediaUrl(path)));
        }

        return files;
    }

    private async Task<string> EncodeMp3Async(string wav, string folder, string name, string title, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var final = UniquePath(folder, name, ".mp3");
        var part = Path.ChangeExtension(final, ".part.mp3");
        try
        {
            await _ffmpeg.RunAsync(new[]
            {
                "-y", "-i", wav, "-vn", "-c:a", "libmp3lame", "-b:a", _s.Mp3Bitrate,
                "-metadata", $"title={title}", part
            }, ct);
            File.Move(part, final);
        }
        catch
        {
            TryDelete(part);
            throw;
        }
        return final;
    }

    // Copies the picture and replaces the soundtrack. WebM only allows Opus or Vorbis audio, so a .webm
    // gets Opus; everything else gets AAC. The first track is marked as the default one.
    private async Task<string> BuildVideoAsync(
        string media, string ext, IReadOnlyList<(string Wav, string Title)> tracks,
        string folder, string name, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var final = UniquePath(folder, name, ext);
        var part = Path.ChangeExtension(final, ".part" + ext);

        var args = new List<string> { "-y", "-i", media };
        foreach (var t in tracks) { args.Add("-i"); args.Add(t.Wav); }

        args.AddRange(new[] { "-map", "0:v:0" });
        for (var k = 0; k < tracks.Count; k++) { args.Add("-map"); args.Add($"{k + 1}:a:0"); }

        var codec = ext == ".webm" ? "libopus" : "aac";
        args.AddRange(new[] { "-c:v", "copy" });
        for (var k = 0; k < tracks.Count; k++)
        {
            args.AddRange(new[]
            {
                $"-c:a:{k}", codec,
                $"-b:a:{k}", _s.VideoAudioBitrate,
                $"-metadata:s:a:{k}", $"title={tracks[k].Title}",
                $"-disposition:a:{k}", k == 0 ? "default" : "0"
            });
        }
        if (ext is ".mp4" or ".m4v" or ".mov")
            args.AddRange(new[] { "-movflags", "+faststart" });
        args.Add(part);

        try
        {
            await _ffmpeg.RunAsync(args, ct);
            File.Move(part, final);
        }
        catch
        {
            TryDelete(part);
            throw;
        }
        return final;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        if (name.Length > 150) name = name[..150];
        return name.Length == 0 ? "media" : name;
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

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }
}
