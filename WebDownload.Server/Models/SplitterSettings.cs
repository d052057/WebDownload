namespace WebDownload.Server.Models;

/// <summary>
/// Bind to the "Splitter" section of appsettings.json. Every property has a default, so the section
/// is optional, but DemucsExecutablePath almost always needs setting to your real path.
///
/// List-valued settings are null by default and read through the Get*() methods: .NET configuration
/// binding APPENDS to a list that already has items, so in-code defaults would be duplicated as soon
/// as appsettings.json set the same list.
/// </summary>
public class SplitterSettings
{
    // The Demucs launcher, for example C:\Tools\demucs314\Scripts\demucs.exe. A bare name is looked
    // up next to the app first, then on PATH.
    public string DemucsExecutablePath { get; set; } = "demucs.exe";

    // Only for running Demucs through Python instead of a launcher: set DemucsExecutablePath to
    // python.exe and this to [ "-m", "demucs" ].
    public List<string>? DemucsLeadingArgs { get; set; }

    // Demucs downloads its model on first use into a per-Windows-user cache. Set this to a fixed folder
    // so the model is found no matter which account runs the app. Empty = leave the defaults alone.
    public string? ModelCacheFolder { get; set; }

    // Results go to <OutputFolder>\audio and <OutputFolder>\video. Keep it inside
    // ApplicationSettings:MediaDrive so /medias can serve the files back as links.
    public string OutputFolder { get; set; } = @"d:\medias\separated";

    // Scratch space for the extracted audio and Demucs's output. Empty = system temp folder.
    public string TempFolder { get; set; } = "";

    // MediaMenu.Menu values shown in the list. Add "rpm" later when that source is wired up.
    public List<string>? Menus { get; set; }
    public List<string>? VideoExtensions { get; set; }   // files that have a picture
    public List<string>? AudioExtensions { get; set; }   // audio-only files (no video is built for these)

    public string StandardModel { get; set; } = "htdemucs";
    public string HighQualityModel { get; set; } = "htdemucs_ft";   // about 4 times slower

    public string Mp3Bitrate { get; set; } = "320k";
    public string VideoAudioBitrate { get; set; } = "256k";

    // Applied to the separated voice for "Voice cleanup": remove rumble, reduce steady noise, quiet the
    // gaps between speech (where leaked music is easiest to hear), then even out the volume.
    public string CleanupFilter { get; set; } =
        "highpass=f=80,afftdn=nr=12:nf=-35,agate=threshold=0.02:ratio=2:attack=10:release=300,loudnorm=I=-16:TP=-1.5:LRA=11";

    public string MusicTrackTitle { get; set; } = "Music";
    public string VoiceTrackTitle { get; set; } = "Voice";

    public int MaxConcurrentJobs { get; set; } = 1;
    public int HardwareCheckTimeoutSeconds { get; set; } = 60;

    private static readonly string[] DefaultMenus = { "movies", "videos" };
    private static readonly string[] DefaultVideoExtensions = { ".mp4", ".m4v", ".mov", ".mkv", ".webm" };
    private static readonly string[] DefaultAudioExtensions = { ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma" };

    public IReadOnlyList<string> GetMenus() => Menus is { Count: > 0 } ? Menus : DefaultMenus;
    public IReadOnlyList<string> GetVideoExtensions() => VideoExtensions is { Count: > 0 } ? VideoExtensions : DefaultVideoExtensions;
    public IReadOnlyList<string> GetAudioExtensions() => AudioExtensions is { Count: > 0 } ? AudioExtensions : DefaultAudioExtensions;

    public IReadOnlyList<string> GetMediaExtensions() =>
        GetVideoExtensions().Concat(GetAudioExtensions()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> GetLeadingArgs() =>
        (DemucsLeadingArgs ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();

    public string GetTempRoot() =>
        string.IsNullOrWhiteSpace(TempFolder) ? Path.Combine(Path.GetTempPath(), "Splitter") : TempFolder;
}
