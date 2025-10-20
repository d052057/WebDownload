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
        private readonly Regex rgxExtractAudio = new Regex(@"\[ExtractAudio\] Destination:\s+(?<downloadFileName>.+)");
        private readonly Regex rgxChapterAudio = new Regex(@"\[SplitChapters\] Chapter 0*\d{1,3};\s+Destination:\s+(?<ChapterFileName>.+)");
        private readonly Regex regex = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of ~\s+(?<totalSize>[\d.\w]+) at\s+(?<speed>[\d.\w/]+)\s+ETA\s+(?<eta>[\w\d:]+)\s\(frag (?<fragNumber>\d{1,3}/\d{1,3})\)");
        private readonly Regex rgxHlsnative = new Regex(@"\[hlsnative\] Total fragments:\s(?<TotalFragment>[\d]+)");
        private readonly Regex rgxLast = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+(?<totalSize>[\d.\w]+) in\s+(?<eta>[\w\d:]+) at\s+(?<speed>[\d.\w/]+)");
      

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
                Func<DownloadInfo, Task> callback = async p      =>
                {
                    DownloadInfo info = new()
                    {
                        FileName = p.Output
                    };
                    await Clients.Client(conn).SendAsync("ReceiveFileName", info);
                };
                await _downloadService.StartDownloadTitleAsync(request, callback);
            }
            catch (UriFormatException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub The URL format is invalid."
                };
                await Clients.Client(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (IOException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub An error occurred while accessing the file system."
                };
                await Clients.Client(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (Exception ex)
            {
                DownloadInfo Errinfo = new() {
                    Error = $"Hub Error during download: {ex.Message}"
                };
                await Clients.Client(conn).SendAsync("ReceiveError", Errinfo);
            }
        }
        public async Task HubStartDownloadServiceAsync(DownloadRequest request)
        {
            request.OutputFolder = _appSettings.Value.MediaDrive + @"\" + request.OutputFolder;
            string conn = request.DownloadId;
            string state = "Pre Processing";
            DownloadInfo info = new()
            { 
                State = state 
            };  
            await Clients.Client(conn).SendAsync("ReceiveState", info);
            try
            {
                Func<DownloadInfo, Task> callback = async p =>
                {
                    var matchFileName = rgxFilePostProc.Match(p.Output);
                    if (matchFileName.Success)
                    {
                        DownloadInfo    Finfo = new()
                        {
                            FileName = matchFileName.Groups["downloadFileName"].Value
                        };
                        await Clients.Client(conn).SendAsync("ReceiveFileName", Finfo);
                        state = "download";
                    }

                    if (p.Output.Contains("[hlsnative] Total fragments:"))
                    {
                        var match = rgxHlsnative.Match(p.Output);
                        if (match.Success)
                        {
                            DownloadInfo tinfo = new()
                            {
                                Frag = match.Groups["TotalFragment"].Value
                            };
                            
                            await Clients.Client(conn).SendAsync("ReceiveTotalFragment", tinfo);

                        }
                    }
                    if (state == "download")
                    {

                        if (p.Output.Contains("[download]"))
                        {

                            var match = regex.Match(p.Output);
                            if (match.Success)
                            {
                                var progressPercentage = match.Groups["progress"].Value; // Extract "50.2"
                                progressPercentage = progressPercentage.TrimEnd('%');
                                DownloadInfo dinfo = new()
                                {
                                    Progress = progressPercentage,
                                    Speed = match.Groups["speed"].Value,
                                    Eta = match.Groups["eta"].Value,
                                    Size = match.Groups["totalSize"].Value,
                                    Frag = match.Groups["fragNumber"].Value,
                                    State = "Downloading"
                                };
                                await Clients.Client(conn).SendAsync("ReceiveDownloadInfo", dinfo);
                            }
                            var matchLast = rgxLast.Match(p.Output);
                            if (matchLast.Success)
                            {
                                var progressPercentage = matchLast.Groups["progress"].Value; 
                                progressPercentage = progressPercentage.TrimEnd('%');
                                DownloadInfo xinfo = new()
                                {
                                    Progress = progressPercentage,
                                    Speed = matchLast.Groups["speed"].Value,
                                    Eta = matchLast.Groups["eta"].Value,
                                    Size = matchLast.Groups["totalSize"].Value,
                                    State = "Success"
                                };
                                await Clients.Client(conn).SendAsync("ReceiveLastDownloadInfo", xinfo);
                            }

                            if (p.Output.IndexOf("[Merger] Merging formats into") > -1 || p.Output.IndexOf("Deleting original file") > -1)
                            {
                                DownloadInfo minfo = new()
                                {
                                    State = "Post Processing"
                                };
                                await Clients.Client(conn).SendAsync("ReceiveState", minfo);

                            }

                        };
                        if (p.Output.Contains("[ExtractAudio]"))
                        {
                            var matchExtractFile = rgxExtractAudio.Match(p.Output);
                            if (matchExtractFile.Success)
                            {
                                DownloadInfo einfo = new()
                                {
                                    FileName = matchExtractFile.Groups["downloadFileName"].Value
                                };
                                
                                await Clients.Client(conn).SendAsync("ReceiveFileName", einfo);
                            }
                        };
                        if (p.Output.Contains("[SplitChapters]"))
                        {
                            var matchChapterFile = rgxChapterAudio.Match(p.Output);
                            if (matchChapterFile.Success)
                            {
                                DownloadInfo sinfo = new()
                                {
                                    Chapter = matchChapterFile.Groups["ChapterFileName"].Value
                                };
                                await Clients.Client(conn).SendAsync("ReceiveChapterFileName", sinfo);
                            }
                        }
                    }
                    DownloadInfo info = new()
                    {
                        Output = p.Output
                    };
                    await Clients.Client(conn).SendAsync("ReceiveOutput", info);
                };
                await _downloadService.StartDownloadAsync(request, callback);
                DownloadInfo Finfo = new()
                {
                    FinishOutput = $"Files saved to {request.OutputFolder}."
                };
                await Clients.Client(conn).SendAsync("ReceiveDownloadFinished", Finfo);
            }
            catch (UriFormatException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub The URL format is invalid."
                };
                await Clients.Client(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (IOException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub An error occurred while accessing the file system."
                };
                await Clients.Client(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (Exception ex)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = $"Hub Error during download: {ex.Message}"
                };
                await Clients.Client(conn).SendAsync("ReceiveError", Errinfo);
            }
        }
    }
}
