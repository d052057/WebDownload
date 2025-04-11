using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebDownload.Server.Services;
using WebDownload.Server.Models;
namespace WebDownload.Server.Hubs
{
    public class DownloadHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly IDownloadService _downloadService;
        private readonly Regex rgxFilePostProc = new Regex(@"\[download\] Destination:\s+(?<downloadFileName>.+)");
        private readonly Regex regex = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of ~\s+(?<totalSize>[\d.\w]+) at\s+(?<speed>[\d.\w/]+)\s+ETA\s+(?<eta>[\w\d:]+)");
        private readonly Regex rgxHlsnative = new Regex(@"\[hlsnative\] Total fragments:\s(?<TotalFragment>[\d]+)");

        //[download] 100% of    7.63MiB in 00:00:04 at 1.88MiB/s       
        private readonly Regex rgxLast = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+(?<totalSize>[\d.\w]+) in\s+(?<eta>[\w\d:]+) at\s+(?<speed>[\d.\w/]+)");
        //private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<ApplicationSettings> _appSettings;
        public DownloadHub(
            IHttpClientFactory httpClientFactory,
            IDownloadService downloadService,           
            IOptions<ApplicationSettings> appSettings
            )
        {
            _downloadService = downloadService;
            //_httpClientFactory = httpClientFactory;
            _appSettings = appSettings;
        }

        public string GetConnectionId() => Context.ConnectionId;


        public async Task HubGetTitleServiceAsync(DownloadTitleRequest request)
        {
            string conn = request.DownloadId;
            try
            {
                Func<string, Task> callback = async p =>
                {
                    await Clients.Client(conn).SendAsync("ReceiveFileName", p);
                };
                await _downloadService.StartDownloadTitleAsync(request, callback);
            }
            catch (UriFormatException)
            {
                await Clients.Client(conn).SendAsync("ReceiveError", "Hub The URL format is invalid.");
            }
            catch (IOException)
            {
                await Clients.Client(conn).SendAsync("ReceiveError", "Hub An error occurred while accessing the file system.");
            }
            catch (Exception ex)
            {
                await Clients.Client(conn).SendAsync("ReceiveError", $"Hub Error during download: {ex.Message}");
            }
        }
        public async Task HubStartDownloadServiceAsync(DownloadRequest request)
        {
            request.OutputFolder = _appSettings.Value.MediaDrive + @"\" + request.OutputFolder;
            string conn = request.DownloadId;
            string state = "Pre Processing";
            await Clients.Client(conn).SendAsync("ReceiveState", state);
            try
            {
                Func<string, Task> callback = async p =>
                {
                    var matchFileName = rgxFilePostProc.Match(p);
                    if (matchFileName.Success)
                    {
                        await Clients.Client(conn).SendAsync("ReceiveFileName", matchFileName.Groups["downloadFileName"].Value);
                        state = "download";
                    }

                    if (p.Contains("[hlsnative] Total fragments:"))
                    {
                        var match = rgxHlsnative.Match(p);
                        if (match.Success)
                        {
                            await Clients.Client(conn).SendAsync("ReceiveTotalFragment", match.Groups["TotalFragment"].Value);

                        }
                    }
                    if (state == "download")
                    {

                        if (p.Contains("[download]"))
                        {

                            var match = regex.Match(p);
                            if (match.Success)
                            {

                                var progressPercentage = match.Groups["progress"].Value; // Extract "50.2"
                                progressPercentage = progressPercentage.TrimEnd('%');
                                if (float.TryParse(progressPercentage, out float progress))
                                {
                                    var normalizedProgress = progress / 100.0f; // Convert percentage to 0.000-1.000
                                    await Clients.Client(conn).SendAsync("ReceiveProgress", progress);
                                }

                                await Clients.Client(conn).SendAsync("ReceiveSpeed", match.Groups["speed"].Value);
                                await Clients.Client(conn).SendAsync("ReceiveETA", match.Groups["eta"].Value);
                                await Clients.Client(conn).SendAsync("ReceiveTotalSize", match.Groups["totalSize"].Value);
                                await Clients.Client(conn).SendAsync("ReceiveState", "Downloading");

                            }
                            var matchLast = rgxLast.Match(p);
                            if (matchLast.Success)
                            {

                                var progressPercentage = matchLast.Groups["progress"].Value; // Extract "50.2"
                                progressPercentage = progressPercentage.TrimEnd('%');
                                if (float.TryParse(progressPercentage, out float progress))
                                {
                                    var normalizedProgress = progress / 100.0f; // Convert percentage to 0.000-1.000
                                    await Clients.Client(conn).SendAsync("ReceiveProgress", progress);
                                }
                                await Clients.Client(conn).SendAsync("ReceiveState", "Success");
                                await Clients.Client(conn).SendAsync("ReceiveSpeed", matchLast.Groups["speed"].Value);
                                await Clients.Client(conn).SendAsync("ReceiveETA", matchLast.Groups["eta"].Value);
                                await Clients.Client(conn).SendAsync("ReceiveTotalSize", matchLast.Groups["totalSize"].Value);

                            }

                            if (p.IndexOf("[Merger] Merging formats into") > -1 || p.IndexOf("Deleting original file") > -1)
                            {
                                await Clients.Client(conn).SendAsync("ReceiveState", "Post Processing");

                            }

                        }
                    }
                    await Clients.Client(conn).SendAsync("ReceiveOutput", p + "\n");
                };
                await _downloadService.StartDownloadAsync(request, callback);
                await Clients.Client(conn).SendAsync("DownloadFinished", $"Hub Download complete! Files saved to {request.OutputFolder}.");
            }
            catch (UriFormatException)
            {
                await Clients.Client(conn).SendAsync("ReceiveError", "Hub The URL format is invalid.");
            }
            catch (IOException)
            {
                await Clients.Client(conn).SendAsync("ReceiveError", "Hub An error occurred while accessing the file system.");
            }
            catch (Exception ex)
            {
                await Clients.Client(conn).SendAsync("ReceiveError", $"Hub Error during download: {ex.Message}");
            }
        }
    }
}
