namespace WebDownload.Server.Models;

public class VoiceoverVoice
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>
/// Bind to the "Voiceover" section of appsettings.json. Every property has a default,
/// so the section is optional.
///
/// List-valued settings are null by default and resolved through the Get*() methods.
/// .NET configuration binding APPENDS to a list that already has items, so giving these
/// lists in-code defaults would duplicate them as soon as appsettings.json set them too.
/// </summary>
public class VoiceoverSettings
{
    // Where the srt/vtt select list reads from (translated subtitles).
    public string SrtFolder { get; set; } = @"d:\medias\closecaption\translate";

    // Finished files go to <OutputFolder>\mp3 and <OutputFolder>\video. Keep this inside
    // ApplicationSettings:MediaDrive so /medias can serve the files back as download links.
    public string OutputFolder { get; set; } = @"d:\medias\converted";

    // Scratch space for uploads and the in-progress audio. Empty = system temp folder.
    public string TempFolder { get; set; } = "";

    // OPTIONAL allow-list of MediaMenu.Menu names. Leave empty and the page offers every menu in the
    // MediaMenu table that actually holds files with one of VideoExtensions.
    public List<string>? Menus { get; set; }
    public List<string>? VideoExtensions { get; set; }
    public List<VoiceoverVoice>? Voices { get; set; }
    public string DefaultVoice { get; set; } = "km-KH-PisethNeural";

    // "Match the voice to the video" picks one of these from the speaker's detected pitch.
    public string MaleVoice { get; set; } = "km-KH-PisethNeural";
    public string FemaleVoice { get; set; } = "km-KH-SreymomNeural";
    // How much of the video's audio (from the start) is sampled when no per-cue result is available.
    public int AnalysisSeconds { get; set; } = 30;

    // Original speech with a median pitch below this is treated as a male speaker, above it female.
    // If women are being given the male voice, lower it (for example 150); if men are being
    // given the female voice, raise it (for example 180).
    public int GenderThresholdHz { get; set; } = 165;

    // Text to strip from every cue before it is spoken, such as "[music]". Case-insensitive.
    // The list lives only in appsettings.json ("Voiceover:IgnoreWords"); nothing is built in,
    // so an empty or missing list means nothing is removed. Changes need an app restart.
    public List<string>? IgnoreWords { get; set; }

    public bool CleanSubtitles { get; set; } = true;

    // Timeline is built as raw PCM at this rate, then encoded to MP3 once at the end.
    public int SampleRate { get; set; } = 24000;
    public string Mp3Bitrate { get; set; } = "96k";
    public string EmbeddedAudioBitrate { get; set; } = "128k";

    // Longest speed-up applied to squeeze a clip into its cue. Beyond this the clip
    // overruns into the gap after it rather than turning unintelligible.
    public double MaxSpeedUp { get; set; } = 2.0;

    // A clip may also use up to this much of the silent gap after its cue before the next cue starts.
    public double MaxBorrowSeconds { get; set; } = 1.0;
    public int MaxSynthesisRetries { get; set; } = 3;
    public int MaxConcurrentJobs { get; set; } = 1;

    // Subtitle files the srt list and the upload box accept.
    public List<string>? SubtitleExtensions { get; set; }

    // Rate / pitch sliders and the automatic voice matching are all limited to +/- this many percent.
    public int MaxAdjustPercent { get; set; } = 25;

    // The sample rate Edge TTS returns. Only used when pitch is shifted by relabelling the rate.
    public int TtsSampleRate { get; set; } = 24000;

    // Voice matching: the pitch (Hz) each detected voice is measured against to work out its pitch offset.
    public int MaleBaselineHz { get; set; } = 120;
    public int FemaleBaselineHz { get; set; } = 210;

    // Starting state of the "Match the voice to the video" checkbox.
    public bool MatchVoiceByDefault { get; set; } = true;

    // Limits for the tempo (speed) factor applied to a single clip.
    public double MinClipTempo { get; set; } = 0.5;
    public double MaxFilterTempo { get; set; } = 4.0;
    public double MinFilterTempo { get; set; } = 0.25;

    public string VoiceTrackTitle { get; set; } = "Khmer";
    public string OriginalTrackTitle { get; set; } = "Original";
    public string VoiceLanguageCode { get; set; } = "khm";
    public bool MakeVoiceTrackDefault { get; set; } = true;

    private static readonly string[] DefaultSubtitleExtensions = { ".srt", ".vtt" };
    private static readonly string[] DefaultVideoExtensions = { ".mp4", ".m4v", ".mov", ".mkv", ".webm" };
    private static readonly VoiceoverVoice[] DefaultVoices =
    {
        new() { Id = "km-KH-PisethNeural", Label = "Piseth (male)" },
        new() { Id = "km-KH-SreymomNeural", Label = "Sreymom (female)" },
    };

    public IReadOnlyList<string> GetIgnoreWords() =>
        (IgnoreWords ?? new List<string>())
            .Select(w => w?.Trim() ?? "")
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<string> GetSubtitleExtensions() => SubtitleExtensions is { Count: > 0 } ? SubtitleExtensions : DefaultSubtitleExtensions;
    public IReadOnlyList<string> GetVideoExtensions() => VideoExtensions is { Count: > 0 } ? VideoExtensions : DefaultVideoExtensions;
    public IReadOnlyList<VoiceoverVoice> GetVoices() => Voices is { Count: > 0 } ? Voices : DefaultVoices;

    public string GetTempRoot() =>
        string.IsNullOrWhiteSpace(TempFolder) ? Path.Combine(Path.GetTempPath(), "Voiceover") : TempFolder;
}
