namespace WebDownload.Server.Models
{
    // Model to receive download requests
    public class DownloadRequest
    {
        public required string Url { get; set; }
        public bool AudioOnly { get; set; }
        public bool SubTitle { get; set; }
        public string Options { get; set; } = string.Empty;
        public required string DownloadId { get; set; }
        public required string OutputFolder { get; set; }
    }
    public class DownloadTitleRequest
    {
        public required string Url { get; set; }
        public required string DownloadId { get; set; }
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
    }
}

