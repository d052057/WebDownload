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
    
}

