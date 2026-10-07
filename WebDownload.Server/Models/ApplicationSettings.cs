namespace WebDownload.Server.Models;

public class ApplicationSettings
{
    // Root folder of the media library. Everything under it is served at MediaRequestPath.
    public string MediaDrive { get; set; } = @"d:\medias";

    // URL prefix the media drive is served under (and that download links use).
    public string MediaRequestPath { get; set; } = "/medias";

    // Virtual directory the app is hosted under (IIS sub-application). "" = hosted at the site root.
    public string PathBase { get; set; } = "/webdownload";

    // Endpoints of the SignalR hubs.
    public string DownloadHubPath { get; set; } = "/downloadHub";
    public string SplitterHubPath { get; set; } = "/splitterHub";
    public string VoiceSwapHubPath { get; set; } = "/voiceSwapHub";
    public string ConvertHubPath { get; set; } = "/convertHub";
}
