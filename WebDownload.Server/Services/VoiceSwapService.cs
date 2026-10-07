using System.Globalization;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// The voice swap pipeline, one job:
///   1. split the song into vocals and music (Demucs, through StemSeparator),
///   2. convert the vocals to the chosen voice (Applio / RVC, through ApplioRunner),
///   3. mix the converted voice with the music (ffmpeg) and write an MP3, plus a video when the source has a picture.
///
/// The three steps run one after the other, each in its own process, so the graphics card is free again
/// before the next step starts. That matters on a 6 GB card.
/// </summary>
public sealed class VoiceSwapService
{
    // Share of the progress bar for each step.
    private const int Step1End = 40;
    private const int Step2End = 85;

    private readonly VoiceSwapSettings _s;
    private readonly SplitterSettings _splitter;
    private readonly StemSeparator _stems;
    private readonly ApplioRunner _applio;
    private readonly FfmpegRunner _ffmpeg;
    private readonly MediaPathResolver _paths;

    public VoiceSwapService(
        IOptions<VoiceSwapSettings> settings,
        IOptions<SplitterSettings> splitter,
        StemSeparator stems,
        ApplioRunner applio,
        FfmpegRunner ffmpeg,
        MediaPathResolver paths)
    {
        _s = settings.Value;
        _splitter = splitter.Value;
        _stems = stems;
        _applio = applio;
        _ffmpeg = ffmpeg;
        _paths = paths;
    }

    public async Task<IReadOnlyList<VoiceSwapFile>> RunAsync(
        VoiceSwapJob job, Func<VoiceSwapUpdate, Task> report, CancellationToken ct)
    {
        Task State(int step, string text, int percent) =>
            report(new VoiceSwapUpdate(job.JobId, "state", State: text, Percent: Math.Clamp(percent, 0, 100), Step: step));
        Task Log(string text) =>
            report(new VoiceSwapUpdate(job.JobId, "log", Message: text));

        var problems = _applio.CheckSetup();
        if (problems.Count > 0) throw new InvalidOperationException(string.Join(" ", problems));

        Directory.CreateDirectory(job.WorkDir);
        var ext = Path.GetExtension(job.MediaPath).ToLowerInvariant();
        var hasPicture = _splitter.GetVideoExtensions().Contains(ext, StringComparer.OrdinalIgnoreCase);

        // ---- Step 1: vocals and music ---------------------------------------------------------------------------
        var stems = await _stems.SeparateAsync(
            job.MediaPath, Path.Combine(job.WorkDir, "split"), job.DemucsModel,
            (text, pct) => State(1, $"Step 1 of 3: {text}", pct * Step1End / 100),
            Log, ct);

        // ---- Step 2: convert the vocals to the chosen voice -----------------------------------------------
        var index = ApplioRunner.PrepareIndex(job.Voice.IndexPath, job.WorkDir, out var indexNote);
        if (indexNote is not null) await Log(indexNote);

        var converted = Path.Combine(job.WorkDir, "converted.wav");

        async Task ConvertAsync(bool useGpu)
        {
            var where = useGpu ? "GPU if available" : "CPU";
            await State(2, $"Step 2 of 3: Converting the voice to {job.Voice.Name} ({where})", Step1End);
            await _applio.ConvertAsync(
                stems.VocalsWav, converted, job.Voice.PthPath, index, job.Pitch, useGpu,
                pct => State(2, $"Step 2 of 3: Converting the voice to {job.Voice.Name} ({where})",
                    Step1End + pct * (Step2End - Step1End) / 100),
                Log, ct);
        }

        try
        {
            await ConvertAsync(useGpu: true);
        }
        catch (ApplioException ex) when (_s.FallbackToCpu && LooksLikeGpuProblem(ex.Message))
        {
            await Log($"The GPU run failed ({ex.Message}). Trying again on the CPU.");
            await ConvertAsync(useGpu: false);
        }

        // ---- Step 3: mix with the music ---------------------------------------------------------------------
        await State(3, "Step 3 of 3: Mixing the voice with the music", Step2End);
        var mixWav = Path.Combine(job.WorkDir, "mix.wav");
        var graph = string.Format(CultureInfo.InvariantCulture, _s.MixFilterTemplate, _s.MusicGainDb, _s.VocalGainDb);
        await _ffmpeg.RunAsync(new[]
        {
            "-y", "-i", stems.MusicWav, "-i", converted,
            "-filter_complex", graph, "-map", $"[{_s.MixOutputLabel}]",
            "-c:a", "pcm_s16le", mixWav
        }, ct);

        var baseName = Sanitize(Path.GetFileNameWithoutExtension(job.MediaPath));
        var tag = $"[voice swap - {Sanitize(job.Voice.Name)}]";
        var files = new List<VoiceSwapFile>();
        var audioFolder = Path.Combine(_s.OutputFolder, "audio");
        var videoFolder = Path.Combine(_s.OutputFolder, "video");

        await State(3, "Step 3 of 3: Encoding the MP3", 90);
        var mix = await EncodeMp3Async(mixWav, audioFolder, $"{baseName} {tag}", ct);
        files.Add(new VoiceSwapFile("Song with the new voice (MP3)", Path.GetFileName(mix), _paths.ToMediaUrl(mix)));

        if (_s.KeepConvertedVoice)
        {
            await State(3, "Step 3 of 3: Encoding the converted voice", 94);
            var voice = await EncodeMp3Async(converted, audioFolder, $"{baseName} [voice only - {Sanitize(job.Voice.Name)}]", ct);
            files.Add(new VoiceSwapFile("Converted voice alone (MP3)", Path.GetFileName(voice), _paths.ToMediaUrl(voice)));
        }

        if (hasPicture)
        {
            await State(3, "Step 3 of 3: Building the video", 97);
            var video = await BuildVideoAsync(job.MediaPath, ext, mixWav, videoFolder, $"{baseName} {tag}", ct);
            files.Add(new VoiceSwapFile("Video with the new voice", Path.GetFileName(video), _paths.ToMediaUrl(video)));
        }

        return files;
    }

    // Only a graphics card problem is worth a second, slower try on the CPU; anything else would fail again.
    private static bool LooksLikeGpuProblem(string message) =>
        message.Contains("CUDA", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("out of memory", StringComparison.OrdinalIgnoreCase);

    private async Task<string> EncodeMp3Async(string wav, string folder, string name, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var final = UniquePath(folder, name, ".mp3");
        var part = Path.ChangeExtension(final, ".part.mp3");
        try
        {
            await _ffmpeg.RunAsync(new[]
            {
                "-y", "-i", wav, "-vn", "-c:a", "libmp3lame", "-b:a", _s.Mp3Bitrate,
                "-metadata", $"title={name}", part
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

    // Copies the picture and replaces the soundtrack with the mix. WebM only allows Opus or Vorbis audio,
    // so a .webm gets Opus; everything else gets AAC. Original subtitles and extra audio tracks are not carried over.
    private async Task<string> BuildVideoAsync(
        string media, string ext, string mixWav, string folder, string name, CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var final = UniquePath(folder, name, ext);
        var part = Path.ChangeExtension(final, ".part" + ext);

        var args = new List<string>
        {
            "-y", "-i", media, "-i", mixWav,
            "-map", "0:v:0", "-map", "1:a:0",
            "-c:v", "copy",
            "-c:a", ext == ".webm" ? "libopus" : "aac",
            "-b:a", _s.VideoAudioBitrate,
            "-metadata:s:a:0", $"title={_s.MixTrackTitle}",
            "-disposition:a:0", "default"
        };
        if (ext is ".mp4" or ".m4v" or ".mov") args.AddRange(new[] { "-movflags", "+faststart" });
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
}
