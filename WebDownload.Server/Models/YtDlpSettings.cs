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

        // Used by the "Embed Subtitle" checkbox to mux the closecaption file
        // into the downloaded video after translation. ffmpeg is a separate
        // executable from yt-dlp.exe, typically installed alongside it.
        public string FfmpegExecutablePath { get; set; } = "ffmpeg.exe";

        // {0}=video path, {1}=subtitle path, {2}=subtitle codec, {3}=output path,
        // {4}=3-letter subtitle language code (see LanguageCodeMap below).
        // Explicit -c:v/-c:a copy (rather than a blanket -c copy) avoids ffmpeg's
        // "multiple codec options for stream N" warning when -c:s overrides it.
        // -disposition:s:0 default marks the embedded subtitle track as the
        // default one, so players show it automatically instead of requiring
        // the user to manually enable it from a subtitle menu.
        // -metadata:s:s:0 language={4} labels the track properly (e.g. "Khmer")
        // instead of players showing it as "und" (undefined).
        // -movflags +faststart moves the moov atom (the file's index) to the
        // front of the file. Without it, ffmpeg writes moov at the end by
        // default, which native players can still play (they seek freely) but
        // browsers streaming the file progressively over HTTP cannot - they
        // read sequentially and hit audio/video data before the index that
        // describes how to decode it, which typically shows up as audio
        // playing fine while video stays black.
        public string EmbedSubtitleArgsTemplate { get; set; } =
            "-y -i \"{0}\" -i \"{1}\" -map 0 -map 1 -c:v copy -c:a copy -c:s {2} -disposition:s:0 default -metadata:s:s:0 language={4} -movflags +faststart \"{3}\"";

        // Languages whose script needs libass-style shaping (stacked marks,
        // glyph joining, reordering) that the mov_text soft-subtitle format
        // can't render correctly - Khmer, Thai, and similar complex scripts
        // come out visually garbled as mov_text even though the underlying
        // UTF-8 text is correct. Languages in this set get burned in as
        // hardsubs via HardsubEmbedArgsTemplate instead of soft-muxed via
        // EmbedSubtitleArgsTemplate.
        public HashSet<string> ComplexScriptLanguages { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        // Font libass should use per language when hardsub-burning subtitles -
        // a font with no glyphs for a script renders empty boxes instead of
        // text. Falls back to HardsubFontDefault if a language isn't listed.
        // "Leelawadee UI" ships with Windows by default (no file needed).
        // "Moulpali" does not ship with Windows - it must exist as a .ttf file
        // in FontsDirectory below, or libass will fail to find it.
        public Dictionary<string, string> HardsubFontByLanguage { get; set; } = new();
        public string HardsubFontDefault { get; set; } = "Arial";

        // Folder libass should search for font files, in addition to the OS's
        // installed fonts - needed for a script like Khmer whose font
        // (Moulpali.ttf) isn't installed system-wide on the server. Resolved
        // in DownloadHub relative to AppContext.BaseDirectory if not already
        // an absolute/rooted path, so "." means "wherever the app itself is
        // running from" (which, per this project's layout, is also where
        // yt-dlp.exe and ffmpeg.exe live).
        public string FontsDirectory { get; set; } = ".";

        // {0}=video path, {1}=escaped .ass path, {2}=escaped fonts directory,
        // {3}=output path. Uses the `ass` filter (not `subtitles`+force_style)
        // because the generated .ass file (see ConvertSubtitleToAss in
        // DownloadHub) already carries its own explicit style block, matching
        // the proven-working native app's approach exactly. Explicit libx264
        // re-encode - can't -c:v copy while burning pixels, so this is slower
        // than the soft-mux path and there's no user-facing toggle to turn
        // subtitles off afterward, by design.

        public string HardsubEmbedArgsTemplate { get; set; } =
            "-y -i \"{0}\" -vf \"ass='{1}':fontsdir='{2}'\" -c:v libx264 -crf 22 -c:a aac -b:a 192k -movflags +faststart \"{3}\"";
       
        // Maps the 2-letter language codes used by the Google Translate API
        // (request.TranslateTo, e.g. "km"/"en") to the 3-letter ISO 639-2 codes
        // that MKV/MP4 container metadata expects, so players show a proper
        // language name instead of "und". Add more pairs here as more target
        // languages are used. Only consulted on the soft-mux path - the
        // hardsub path has no separate subtitle track to label.
        public Dictionary<string, string> LanguageCodeMap { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "eng",
            ["km"] = "khm",
            ["th"] = "tha",
        };

        // Which subtitle codec ffmpeg needs for a given output container - mp4
        // containers require "mov_text" for soft subtitles, mkv can carry the
        // original text-based subtitle codec directly, and webm (Matroska-based)
        // only supports WebVTT - ffmpeg will transcode srt/vtt input to whichever
        // codec is specified here, so no separate file conversion is needed.
        // Only consulted on the soft-mux path.
        public Dictionary<string, string> EmbedSubtitleCodecByExtension { get; set; } = new()
        {
            [".mp4"] = "mov_text",
            [".m4v"] = "mov_text",
            [".mov"] = "mov_text",
            [".mkv"] = "srt",
            [".webm"] = "webvtt",
        };

        // Used for any output container not listed in EmbedSubtitleCodecByExtension.
        public string EmbedSubtitleDefaultCodec { get; set; } = "srt";

        // Subtitle files DownloadHub moves / translates after a download.
        // List-valued, so null by default and read through the Get*() methods: configuration binding
        // appends to a list that already has items.
        public List<string>? SubtitleFileExtensions { get; set; }

        // Video containers DownloadHub looks for when it embeds subtitles.
        public List<string>? VideoFileExtensions { get; set; }

        public IReadOnlyList<string> GetSubtitleFileExtensions() =>
            SubtitleFileExtensions is { Count: > 0 } ? SubtitleFileExtensions : new[] { ".srt", ".vtt", ".ass", ".ssa", ".sbv", ".ttml" };

        public IReadOnlyList<string> GetVideoFileExtensions() =>
            VideoFileExtensions is { Count: > 0 } ? VideoFileExtensions : new[] { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".flv", ".m4v" };
    }
}