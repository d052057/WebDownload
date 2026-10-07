namespace WebDownload.Server.Services;

public sealed record StemResult(string VocalsWav, string MusicWav);

/// <summary>
/// Step 1 of the voice swap: media file -> WAV -> Demucs -> vocals.wav and no_vocals.wav.
/// It uses the same pieces as the Splitter page (FfmpegRunner, DemucsRunner, DeviceDetector and the
/// "Splitter" settings) but keeps the two stems as WAV files for the next step, which the Splitter's own
/// pipeline does not (it encodes MP3s and deletes the work folder).
/// </summary>
public sealed class StemSeparator
{
    private readonly FfmpegRunner _ffmpeg;
    private readonly DemucsRunner _demucs;
    private readonly DeviceDetector _device;

    public StemSeparator(FfmpegRunner ffmpeg, DemucsRunner demucs, DeviceDetector device)
    {
        _ffmpeg = ffmpeg;
        _demucs = demucs;
        _device = device;
    }

    /// <param name="onState">Text and a percent from 0 to 100 for this step only.</param>
    public async Task<StemResult> SeparateAsync(
        string mediaPath, string workDir, string demucsModel,
        Func<string, int, Task> onState, Func<string, Task> onLog, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);

        await onState("Reading the media", 0);
        var probe = await _ffmpeg.ProbeAsync(mediaPath, ct);
        if (probe.AudioStreams == 0)
            throw new InvalidOperationException("This file has no audio track to separate.");

        // A plain WAV with an ASCII name: Demucs never sees the original file name (Khmer characters in a
        // path can trip Python on Windows) or an unusual container.
        await onState("Extracting the audio", 3);
        var inputWav = Path.Combine(workDir, "input.wav");
        await _ffmpeg.RunAsync(new[]
        {
            "-y", "-i", mediaPath, "-vn", "-map", "0:a:0",
            "-ac", "2", "-ar", "44100", "-c:a", "pcm_s16le", inputWav
        }, ct);

        // Same GPU-then-CPU behaviour as the Splitter page.
        var device = await _device.GetAsync(refresh: false, ct);
        var outDir = Path.Combine(workDir, "demucs");

        async Task RunAsync(string dev)
        {
            var where = dev == "cuda" ? "GPU" : "CPU";
            await onState($"Separating voice and music ({where})", 5);
            await _demucs.RunAsync(
                inputWav, outDir, demucsModel, dev,
                percent => onState($"Separating voice and music ({where})", 5 + (int)(95 * percent / 100.0)),
                onLog, ct);
        }

        try
        {
            await RunAsync(device.Device);
        }
        catch (DemucsException ex) when (device.Device == "cuda")
        {
            await onLog($"The GPU run failed ({ex.Message}). Trying again on the CPU.");
            TryDeleteDirectory(outDir);
            await RunAsync("cpu");
        }

        string? Find(string name) =>
            Directory.Exists(outDir)
                ? Directory.EnumerateFiles(outDir, name, SearchOption.AllDirectories).FirstOrDefault()
                : null;

        var vocals = Find("vocals.wav") ?? throw new DemucsException("Demucs finished but did not produce vocals.wav.");
        var music = Find("no_vocals.wav") ?? throw new DemucsException("Demucs finished but did not produce no_vocals.wav.");

        try { File.Delete(inputWav); } catch { /* the longest file in the work folder; best effort */ }
        return new StemResult(vocals, music);
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }
}
