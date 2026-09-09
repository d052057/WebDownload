namespace WebDownload.Server.Models
{
    // Model to receive download requests
    public class DownloadRequest
    {
        public required string Url { get; set; }
        public bool AudioOnly { get; set; }
        public string AudioFormat { get; set; } = string.Empty;
        public bool AudioChapter { get; set; }
        public bool VideoOnly { get; set; }

        // Language codes the user checked in the dynamically-populated subtitle
        // checkbox list (e.g. ["en", "en-orig"]). Empty/null means "no subtitles".
        public List<string> SubtitleLangs { get; set; } = new();

        // Empty/null = don't translate. Otherwise the target language code
        // to translate the downloaded subtitle into, e.g. "km" or "en".
        public string? TranslateTo { get; set; }

        public string Options { get; set; } = string.Empty;
        public required string DownloadId { get; set; }
        public required string OutputFolder { get; set; }
    }

    public class DownloadTitleRequest
    {
        public required string Url { get; set; }
        public required string DownloadId { get; set; }
    }

    // One row in the "Available subtitles" list returned by yt-dlp --list-subs.
    public class SubtitleTrack
    {
        public required string Code { get; set; }      // e.g. "en", "en-orig", "km"
        public required string Name { get; set; }       // e.g. "English", "English (auto)"
        public bool IsAutomatic { get; set; }            // true = auto-generated captions
    }

    public class DownloadInfo
    {
        public string? Speed { get; set; }
        public string? Eta { get; set; }
        public string? Size { get; set; }
        public string? Frag { get; set; }
        public string? State { get; set; }
        public string Output { get; set; } = string.Empty;
        public string? Progress { get; set; }
        public string? FileName { get; set; }
        public string? Chapter { get; set;}
        public string? FinishOutput { get; set; }
        public string? Error { get; set; }
        public string? Command { get; set; }
        public List<SubtitleTrack>? SubtitleTracks { get; set; }
        public string? TranslatedFile { get; set; }
    }

    // Server-side record of where a translation job stands, so the client
    // can ask "is it done yet?" on demand (page reload, tab reopened later,
    // or just double-checking) instead of relying only on the live push.
    public class TranslationJobStatus
    {
        public required string State { get; set; } // "Running", "Completed", "Failed"
        public int CurrentLine { get; set; }
        public int TotalLines { get; set; }
        public string? TranslatedFile { get; set; }
        public string? Error { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}

