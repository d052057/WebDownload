using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Text.RegularExpressions;
using WebDownload.Server.Models;
using WebDownload.Server.Services;
namespace WebDownload.Server.Hubs
{
    public class DownloadHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly IDownloadService _downloadService;
        private readonly ITranslationService _translationService;
        private readonly ITranslationJobTracker _jobTracker;
        private readonly Regex rgxFilePostProc = new Regex(@"\[download\] Destination:\s+(?<downloadFileName>.+)");
        private readonly Regex rgxExtractAudio = new Regex(@"\[ExtractAudio\] Destination:\s+(?<downloadFileName>.+)");
        private readonly Regex rgxChapterAudio = new Regex(@"\[SplitChapters\] Chapter 0*\d{1,3};\s+Destination:\s+(?<ChapterFileName>.+)");
        private readonly Regex regex = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+~?\s*(?<totalSize>[\d.\w]+) at\s+(?<speed>[\d.\w/]+)\s+ETA\s+(?<eta>[\w\d:]+)(\s\(frag (?<fragNumber>\d{1,3}/\d{1,3})\))?");
        private readonly Regex rgxHlsnative = new Regex(@"\[hlsnative\] Total fragments:\s(?<TotalFragment>[\d]+)");
        private readonly Regex rgxLast = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+(?<totalSize>[\d.\w]+) in\s+(?<eta>[\w\d:]+) at\s+(?<speed>[\d.\w/]+)");


        private readonly IOptions<ApplicationSettings> _appSettings;
        public DownloadHub(
            IHttpClientFactory httpClientFactory,
            IDownloadService downloadService,
            ITranslationService translationService,
            ITranslationJobTracker jobTracker,
            IOptions<ApplicationSettings> appSettings
            )
        {
            _downloadService = downloadService;
            _translationService = translationService;
            _jobTracker = jobTracker;
            //_httpClientFactory = httpClientFactory;
            _appSettings = appSettings;
        }

        public string GetConnectionId() => Context.ConnectionId;

        // Called by the client right after connecting, and again after every
        // automatic reconnect. groupId is the client's stable downloadGroupId
        // (a GUID the client generates once and reuses for the life of a
        // download job, independent of the underlying SignalR ConnectionId,
        // which changes on every reconnect). Adding the *current* connection
        // into that group lets the server keep sending progress updates to
        // "whichever connection currently represents this browser tab" via
        // Clients.Group(groupId), even after a reconnect swaps the
        // ConnectionId out from under it.
        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        }

        // On-demand check for "is my translation done yet?" - independent of
        // the live SignalR push, so it still works after a page reload or if
        // a message got missed during a reconnect. groupId is the client's
        // stable downloadGroupId (same value it uses as DownloadId).
        public Task<TranslationJobStatus?> GetTranslationStatus(string groupId)
        {
            return Task.FromResult(_jobTracker.GetStatus(groupId));
        }

        // Called when the URL field changes (or on demand) to populate the
        // subtitle checkbox list from what YouTube actually has available.
        public async Task HubGetSubtitlesAsync(DownloadTitleRequest request)
        {
            string conn = request.DownloadId;
            try
            {
                var tracks = await _downloadService.GetAvailableSubtitlesAsync(request.Url);
                DownloadInfo info = new() { SubtitleTracks = tracks };
                await Clients.Group(conn).SendAsync("ReceiveSubtitleList", info);
            }
            catch (Exception ex)
            {
                DownloadInfo errInfo = new() { Error = $"Hub Error listing subtitles: {ex.Message}" };
                await Clients.Group(conn).SendAsync("ReceiveError", errInfo);
            }
        }


        public async Task HubGetTitleServiceAsync(DownloadTitleRequest request)
        {
            string conn = request.DownloadId;
            try
            {
                Func<DownloadInfo, Task> callback = async p =>
                {
                    DownloadInfo info = new()
                    {
                        FileName = p.Output
                    };
                    await Clients.Group(conn).SendAsync("ReceiveFileName", info);
                };
                await _downloadService.StartDownloadTitleAsync(request, callback);
            }
            catch (UriFormatException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub The URL format is invalid."
                };
                await Clients.Group(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (IOException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub An error occurred while accessing the file system."
                };
                await Clients.Group(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (Exception ex)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = $"Hub Error during download: {ex.Message}"
                };
                await Clients.Group(conn).SendAsync("ReceiveError", Errinfo);
            }
        }
        public async Task HubStartDownloadServiceAsync(DownloadRequest request)
        {
            request.OutputFolder = _appSettings.Value.MediaDrive + @"\" + request.OutputFolder;
            string conn = request.DownloadId;
            string state = "Pre Processing";
            string? downloadedBaseName = null; // e.g. "My Video [abc123]" (no extension)
            DownloadInfo info = new()
            {
                State = state
            };
            await Clients.Group(conn).SendAsync("ReceiveState", info);
            try
            {
                Func<DownloadInfo, Task> callback = async p =>
                {
                    if (!string.IsNullOrEmpty(p.Command))
                    {
                        DownloadInfo cinfo = new()
                        {
                            Command = p.Command
                        };
                        await Clients.Group(conn).SendAsync("ReceiveCommand", cinfo);
                    }

                    var matchFileName = rgxFilePostProc.Match(p.Output);
                    if (matchFileName.Success)
                    {
                        var destFile = matchFileName.Groups["downloadFileName"].Value;
                        downloadedBaseName ??= System.IO.Path.GetFileNameWithoutExtension(destFile);
                        DownloadInfo Finfo = new()
                        {
                            FileName = destFile
                        };
                        await Clients.Group(conn).SendAsync("ReceiveFileName", Finfo);
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

                            await Clients.Group(conn).SendAsync("ReceiveTotalFragment", tinfo);

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
                                await Clients.Group(conn).SendAsync("ReceiveDownloadInfo", dinfo);
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
                                await Clients.Group(conn).SendAsync("ReceiveLastDownloadInfo", xinfo);
                            }

                        }
                        ;
                        if (p.Output.IndexOf("[Merger] Merging formats into") > -1 || p.Output.IndexOf("Deleting original file") > -1)
                        {
                            DownloadInfo minfo = new()
                            {
                                State = "Post Processing"
                            };
                            await Clients.Group(conn).SendAsync("ReceiveState", minfo);

                        }
                        if (p.Output.Contains("[ExtractAudio]"))
                        {
                            var matchExtractFile = rgxExtractAudio.Match(p.Output);
                            if (matchExtractFile.Success)
                            {
                                DownloadInfo einfo = new()
                                {
                                    FileName = matchExtractFile.Groups["downloadFileName"].Value
                                };

                                await Clients.Group(conn).SendAsync("ReceiveFileName", einfo);
                            }
                        }
                        ;
                        if (p.Output.Contains("[SplitChapters]"))
                        {
                            var matchChapterFile = rgxChapterAudio.Match(p.Output);
                            if (matchChapterFile.Success)
                            {
                                DownloadInfo sinfo = new()
                                {
                                    Chapter = matchChapterFile.Groups["ChapterFileName"].Value
                                };
                                await Clients.Group(conn).SendAsync("ReceiveChapterFileName", sinfo);
                            }
                        }
                    }
                    DownloadInfo info = new()
                    {
                        Output = p.Output
                    };
                    await Clients.Group(conn).SendAsync("ReceiveOutput", info);
                };
                await _downloadService.StartDownloadAsync(request, callback);

                if (!string.IsNullOrWhiteSpace(request.TranslateTo) && downloadedBaseName != null)
                {
                    await TranslateDownloadedSubtitlesAsync(conn, request, downloadedBaseName);
                }

                DownloadInfo Finfo = new()
                {
                    FinishOutput = $"Files saved to {request.OutputFolder}."
                };
                await Clients.Group(conn).SendAsync("ReceiveDownloadFinished", Finfo);
            }
            catch (UriFormatException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub The URL format is invalid."
                };
                await Clients.Group(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (IOException)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = "Hub An error occurred while accessing the file system."
                };
                await Clients.Group(conn).SendAsync("ReceiveError", Errinfo);
            }
            catch (Exception ex)
            {
                DownloadInfo Errinfo = new()
                {
                    Error = $"Hub Error during download: {ex.Message}"
                };
                await Clients.Group(conn).SendAsync("ReceiveError", Errinfo);
            }
        }

        // Finds the .srt file(s) yt-dlp just wrote for this video and translates
        // one of them (preferring "en") into request.TranslateTo.
        private async Task TranslateDownloadedSubtitlesAsync(string conn, DownloadRequest request, string downloadedBaseName)
        {
            try
            {
                var srtFiles = Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}*.srt");
                if (srtFiles.Length == 0)
                {
                    return; // nothing to translate, e.g. no subtitles were requested
                }

                // Prefer an English source track if one was downloaded; otherwise
                // just take the first subtitle file that isn't already the target language.
                var sourceFile = srtFiles.FirstOrDefault(f => f.Contains(".en.", StringComparison.OrdinalIgnoreCase))
                                  ?? srtFiles.FirstOrDefault(f => !f.Contains($".{request.TranslateTo}.", StringComparison.OrdinalIgnoreCase));

                if (sourceFile == null)
                {
                    return; // only a same-language file exists already; nothing to do
                }

                var sourceLangMatch = System.Text.RegularExpressions.Regex.Match(sourceFile, @"\.([a-zA-Z-]{2,8})\.srt$");
                var sourceLang = sourceLangMatch.Success ? sourceLangMatch.Groups[1].Value : null;

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Starting: {Path.GetFileName(sourceFile)} -> {request.TranslateTo} ..." });
                await Clients.Group(conn).SendAsync("ReceiveState",
                    new DownloadInfo { State = $"Translating subtitles to {request.TranslateTo}..." });
                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Running", CurrentLine = 0, TotalLines = 0 });

                var lastReportedPercent = -1;
                async Task OnProgress(int current, int total)
                {
                    _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Running", CurrentLine = current, TotalLines = total });

                    var percent = total > 0 ? (current * 100) / total : 100;
                    // Only push a log line every ~5% (or every line for short files) so
                    // we don't flood the log for a 300+ line subtitle file.
                    if (percent != lastReportedPercent && (percent - lastReportedPercent >= 5 || total <= 20))
                    {
                        lastReportedPercent = percent;
                        await Clients.Group(conn).SendAsync("ReceiveOutput",
                            new DownloadInfo { Output = $"[Translate] {current}/{total} ({percent}%)" });
                    }
                }

                var translatedPath = await _translationService.TranslateSrtFileAsync(sourceFile, request.TranslateTo!, sourceLang, OnProgress);

                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Completed", TranslatedFile = translatedPath });

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Done -> {Path.GetFileName(translatedPath)}" });

                DownloadInfo info = new() { TranslatedFile = translatedPath };
                await Clients.Group(conn).SendAsync("ReceiveTranslatedFile", info);
            }
            catch (Exception ex)
            {
                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Failed", Error = ex.Message });
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Failed: {ex.Message}" });
                DownloadInfo errInfo = new() { Error = $"Hub Error translating subtitles: {ex.Message}" };
                await Clients.Group(conn).SendAsync("ReceiveError", errInfo);
            }
        }
    }
}
