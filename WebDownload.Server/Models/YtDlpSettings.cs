namespace WebDownload.Server.Models
{
    // One entry in the "drag an item onto the box" reference list shown in
    // the UI next to the free-form Options textbox.
    public class YtDlpDragDropArg
    {
        public string Label { get; set; } = string.Empty;
        public string Arg { get; set; } = string.Empty;
    }

    /// <summary>
    /// Bind this to the "YtDlp" section in appsettings.json / appsettings.Local.json.
    /// Every yt-dlp.exe argument that used to be hardcoded in DownloadService lives
    /// here instead, so they can be added/changed/removed by editing appsettings.json
    /// and restarting the app - no recompile needed. Static, no-substitution flags are
    /// plain string lists (just add/remove JSON array entries); flags that need a
    /// runtime value (a path, a format, a language list) are "{0}"-style templates
    /// applied with string.Format.
    /// </summary>
    public class YtDlpSettings
    {
        public string ExecutablePath { get; set; } = "yt-dlp.exe";
        public string ConfigLocation { get; set; } = "yt-dlp.conf";
        public string OutputFileTemplate { get; set; } = "%(title)s [%(id)s].%(ext)s";

        // Used only by StartDownloadTitleAsync (the "just look up the title" call).
        public List<string> TitleLookupArgs { get; set; } = new();

        // Used only by GetAvailableSubtitlesAsync (--list-subs).
        public List<string> ListSubtitlesArgs { get; set; } = new();

        // Appended to every real download (StartDownloadAsync), right before the URL.
        public List<string> CommonDownloadArgs { get; set; } = new();

        // Applied when the "Audio only" checkbox is checked.
        public List<string> AudioOnlyArgs { get; set; } = new();
        public List<string> AudioChapterArgs { get; set; } = new();
        // {0} = the chosen audio format, e.g. "flac".
        public string AudioFormatArgsTemplate { get; set; } = "-x --audio-format {0}";

        // {0} = comma-joined subtitle language codes, e.g. "en,km".
        public string SubtitleArgsTemplate { get; set; } =
            "--sub-langs \"{0}\" --write-subs --write-auto-subs --convert-subs srt";

        // The drag-and-drop reference chips shown in the ytdlp page UI.
        public List<YtDlpDragDropArg> DragDropArgs { get; set; } = new();
    }
}
