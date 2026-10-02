using Microsoft.Extensions.Options;
using NWaves.Features;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>Whole-video result (the WPF "Analyze MP4" step): one voice and pitch for everything.</summary>
public sealed record VoiceAnalysis(bool IsMale, double AveragePitchHz, int PitchPercent);

/// <summary>Per-cue result: which voice speaks each subtitle cue, and the pitch offset for each voice.</summary>
public sealed record SpeakerMap(
    IReadOnlyList<bool> IsMale,     // one entry per cue
    int MaleCues,
    int FemaleCues,
    int UnclearCues,                // no clear voice in the original audio; kept the previous cue's speaker
    double? MalePitchHz,
    double? FemalePitchHz,
    int MalePitchPercent,
    int FemalePitchPercent);

/// <summary>
/// Reads the pitch of the ORIGINAL speech to choose a male or female Khmer voice.
///
/// AnalyzeCuesAsync: for every subtitle cue, takes the matching stretch of the original audio,
/// finds its median pitch, and calls it male below GenderThresholdHz, female above. So a video
/// with a man and a woman gets a male and a female Khmer voice, cue by cue.
///
/// AnalyzeAsync: the WPF behaviour, one answer from the first AnalysisSeconds of audio. It is the
/// fallback when per-cue analysis finds nothing.
///
/// Limits: this is pitch only, not speaker recognition. Background music, overlapping speakers,
/// whispering, or a very deep female / high male voice can be misjudged.
/// The WPF code never measured speaking rate (it always reset the speed slider to 0), and neither does this.
/// </summary>
public sealed class VoiceAnalysisService
{
    private const int SampleRate = 16000;
    private const int Window = SampleRate / 5;      // 0.2 s per pitch frame
    private const int MinHop = SampleRate / 10;     // frames at least 0.1 s apart
    private const int MaxFramesPerCue = 12;         // bounds the work for long cues
    private const int MinVoicedFrames = 2;
    private const int MaxCueSeconds = 30;

    private readonly FfmpegRunner _ffmpeg;
    private readonly VoiceoverSettings _s;

    public VoiceAnalysisService(FfmpegRunner ffmpeg, IOptions<VoiceoverSettings> settings)
    {
        _ffmpeg = ffmpeg;
        _s = settings.Value;
    }

    public async Task<SpeakerMap?> AnalyzeCuesAsync(
        string videoPath,
        IReadOnlyList<SubtitleCue> cues,
        string workDir,
        Func<int, int, Task>? onProgress,
        CancellationToken ct)
    {
        // The whole soundtrack once, as 16 kHz mono PCM on disk (about 115 MB per hour).
        var pcmPath = Path.Combine(workDir, "original.pcm");
        try
        {
            await using (var output = new FileStream(pcmPath, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16, useAsync: true))
            {
                await _ffmpeg.RunAsync(new[]
                {
                    "-i", videoPath, "-vn", "-ac", "1", "-ar", SampleRate.ToString(), "-f", "s16le", "-"
                }, ct, output);
            }

            var cuePitch = new double?[cues.Count];
            await using (var input = new FileStream(pcmPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true))
            {
                var totalSamples = input.Length / 2;

                for (var i = 0; i < cues.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var start = (long)(cues[i].Start.TotalSeconds * SampleRate);
                    var length = Math.Min((long)(cues[i].TargetSeconds * SampleRate), (long)MaxCueSeconds * SampleRate);
                    if (start < totalSamples) length = Math.Min(length, totalSamples - start);

                    if (start < totalSamples && length > Window)
                    {
                        var bytes = new byte[length * 2];
                        input.Seek(start * 2, SeekOrigin.Begin);
                        var read = await ReadFullyAsync(input, bytes, ct);
                        var count = read / 2;
                        if (count > Window)
                            cuePitch[i] = await Task.Run(() => MedianPitch(bytes, count, ct), ct);
                    }

                    if (onProgress is not null && (i % 10 == 0 || i == cues.Count - 1))
                        await onProgress(i + 1, cues.Count);
                }
            }

            return BuildSpeakerMap(cuePitch);
        }
        finally
        {
            try { if (File.Exists(pcmPath)) File.Delete(pcmPath); } catch { /* removed with the work folder anyway */ }
        }
    }

    private SpeakerMap? BuildSpeakerMap(double?[] cuePitch)
    {
        var threshold = _s.GenderThresholdHz;
        var firstClear = Array.FindIndex(cuePitch, p => p is not null);
        if (firstClear < 0) return null;

        // A cue with no clear voice keeps the previous cue's speaker; leading ones take the first clear cue's.
        var isMale = new bool[cuePitch.Length];
        var carry = cuePitch[firstClear]!.Value < threshold;
        var unclear = 0;
        for (var i = 0; i < cuePitch.Length; i++)
        {
            if (cuePitch[i] is { } hz) carry = hz < threshold;
            else unclear++;
            isMale[i] = carry;
        }

        // Pitch offset per voice, relative to 120 Hz (male) or 210 Hz (female): the WPF formula.
        var maleHz = Average(cuePitch, isMale, wantMale: true);
        var femaleHz = Average(cuePitch, isMale, wantMale: false);

        return new SpeakerMap(
            isMale,
            isMale.Count(m => m),
            isMale.Count(m => !m),
            unclear,
            maleHz,
            femaleHz,
            maleHz is { } m ? OffsetPercent(m, 120) : 0,
            femaleHz is { } f ? OffsetPercent(f, 210) : 0);
    }

    private static double? Average(double?[] cuePitch, bool[] isMale, bool wantMale)
    {
        var values = new List<double>();
        for (var i = 0; i < cuePitch.Length; i++)
            if (cuePitch[i] is { } hz && isMale[i] == wantMale) values.Add(hz);
        return values.Count == 0 ? null : values.Average();
    }

    private static int OffsetPercent(double hz, double baseHz) =>
        Math.Clamp((int)((hz - baseHz) / baseHz * 100), -25, 25);

    private static double? MedianPitch(byte[] bytes, int count, CancellationToken ct)
    {
        var samples = new float[count];
        for (var i = 0; i < count; i++)
            samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;

        var hop = Math.Max(MinHop, (count - Window) / (MaxFramesPerCue - 1));
        var pitches = new List<float>();
        for (var pos = 0; pos + Window < count; pos += hop)
        {
            ct.ThrowIfCancellationRequested();
            var pitch = Pitch.FromYin(samples, SampleRate, pos, pos + Window, 50, 400);
            if (pitch > 60 && pitch < 350) pitches.Add(pitch);
        }

        if (pitches.Count < MinVoicedFrames) return null;
        pitches.Sort();
        return pitches[pitches.Count / 2];
    }

    private static async Task<int> ReadFullyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(total), ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    /// <returns>null when no clear human voice was found in the sampled audio.</returns>
    public async Task<VoiceAnalysis?> AnalyzeAsync(string videoPath, CancellationToken ct)
    {
        using var pcm = new MemoryStream();
        await _ffmpeg.RunAsync(new[]
        {
            "-i", videoPath, "-vn", "-t", Math.Max(5, _s.AnalysisSeconds).ToString(),
            "-ac", "1", "-ar", SampleRate.ToString(), "-f", "s16le", "-"
        }, ct, pcm);

        var count = (int)(pcm.Length / 2);
        if (count < SampleRate) return null; // under one second of audio

        var bytes = pcm.GetBuffer();
        var samples = new float[count];
        for (var i = 0; i < count; i++)
            samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;

        var pitches = await Task.Run(() =>
        {
            var found = new List<float>();
            for (var pos = 0; pos + Window < count; pos += MinHop)
            {
                ct.ThrowIfCancellationRequested();
                var pitch = Pitch.FromYin(samples, SampleRate, pos, pos + Window, 50, 400);
                if (pitch > 60 && pitch < 350) found.Add(pitch);
            }
            return found;
        }, ct);

        if (pitches.Count == 0) return null;

        var average = pitches.Average();
        var male = average < 165;
        var offsetPercent = male ? (average - 120) / 120 * 100 : (average - 210) / 210 * 100;
        return new VoiceAnalysis(male, average, Math.Clamp((int)offsetPercent, -25, 25));
    }
}
