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
        private readonly ISubtitleTranslationService _subtitleTranslationService;
        private readonly ITranslationJobTracker _jobTracker;
        private readonly IOptions<SubtitleSettings> _subtitleSettings;
        private readonly Regex rgxFilePostProc = new Regex(@"\[download\] Destination:\s+(?<downloadFileName>.+)");
        private readonly Regex rgxExtractAudio = new Regex(@"\[ExtractAudio\] Destination:\s+(?<downloadFileName>.+)");
        private readonly Regex rgxChapterAudio = new Regex(@"\[SplitChapters\] Chapter 0*\d{1,3};\s+Destination:\s+(?<ChapterFileName>.+)");
        // yt-dlp logs a separate "[download] Destination:" line for EACH stream
        // it downloads before merging (e.g. "...f137.mp4" video-only, then
        // "...f140.m4a" audio-only) - those format-coded names never match the
        // final merged file or its subtitle. This line has the true final name.
        private readonly Regex rgxMerger = new Regex(@"\[Merger\] Merging formats into ""(?<downloadFileName>.+)""");
        private readonly Regex regex = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+~?\s*(?<totalSize>[\d.\w]+) at\s+(?<speed>[\d.\w/]+)\s+ETA\s+(?<eta>[\w\d:]+)(\s\(frag (?<fragNumber>\d{1,3}/\d{1,3})\))?");
        private readonly Regex rgxHlsnative = new Regex(@"\[hlsnative\] Total fragments:\s(?<TotalFragment>[\d]+)");
        private readonly Regex rgxLast = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+(?<totalSize>[\d.\w]+) in\s+(?<eta>[\w\d:]+) at\s+(?<speed>[\d.\w/]+)");


        private readonly IOptions<ApplicationSettings> _appSettings;
        public DownloadHub(
            IHttpClientFactory httpClientFactory,
            IDownloadService downloadService,
            ISubtitleTranslationService subtitleTranslationService,
            ITranslationJobTracker jobTracker,
            IOptions<SubtitleSettings> subtitleSettings,
            IOptions<ApplicationSettings> appSettings
            )
        {
            _downloadService = downloadService;
            _subtitleTranslationService = subtitleTranslationService;
            _jobTracker = jobTracker;
            _subtitleSettings = subtitleSettings;
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

                            var matchMerger = rgxMerger.Match(p.Output);
                            if (matchMerger.Success)
                            {
                                // This is the authoritative final filename - overrides
                                // whatever format-coded stream name we grabbed earlier.
                                var mergedFile = matchMerger.Groups["downloadFileName"].Value;
                                downloadedBaseName = System.IO.Path.GetFileNameWithoutExtension(mergedFile);
                                await Clients.Group(conn).SendAsync("ReceiveOutput",
                                    new DownloadInfo { Output = $"[Merger] Final filename base corrected to: {downloadedBaseName}" });
                            }
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

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Post-download check: TranslateTo='{request.TranslateTo}', downloadedBaseName='{downloadedBaseName ?? "(null - destination line was never matched)"}', OutputFolder='{request.OutputFolder}'" });

                if (!string.IsNullOrWhiteSpace(request.TranslateTo) && downloadedBaseName != null)
                {
                    await TranslateDownloadedSubtitlesAsync(conn, request, downloadedBaseName);
                }
                else if (!string.IsNullOrWhiteSpace(request.TranslateTo))
                {
                    _jobTracker.SetStatus(conn, new TranslationJobStatus
                    {
                        State = "Failed",
                        Error = "Could not determine the downloaded file's base name - the yt-dlp 'Destination:' line was never matched. See the output log above."
                    });
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
        // one of them (preferring "en") into request.TranslateTo, writing the
        // result into the shared Subtitle.OutputPath "translate" folder rather
        // than next to the source file. Uses the same ISubtitleTranslationService
        // the subtitle-dashboard page uses (batched official Google Cloud
        // Translation API calls).
        private async Task TranslateDownloadedSubtitlesAsync(string conn, DownloadRequest request, string downloadedBaseName)
        {
            try
            {
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Searching '{request.OutputFolder}' for '{downloadedBaseName}*.srt' ..." });

                var srtFiles = Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}*.srt");
                if (srtFiles.Length == 0)
                {
                    var msg = $"No .srt file matching '{downloadedBaseName}*.srt' was found in '{request.OutputFolder}'. " +
                               "Check that a subtitle language was actually selected before downloading.";
                    _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Failed", Error = msg });
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo { Output = $"[Translate] {msg}" });
                    return;
                }

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Found {srtFiles.Length} .srt file(s): {string.Join(", ", srtFiles.Select(Path.GetFileName))}" });

                // Prefer an English source track if one was downloaded; otherwise
                // just take the first subtitle file that isn't already the target language.
                var sourceFile = srtFiles.FirstOrDefault(f => f.Contains(".en.", StringComparison.OrdinalIgnoreCase))
                                  ?? srtFiles.FirstOrDefault(f => !f.Contains($".{request.TranslateTo}.", StringComparison.OrdinalIgnoreCase));

                if (sourceFile == null)
                {
                    var msg = $"Found subtitle file(s) but none usable as a source (they're all already '{request.TranslateTo}').";
                    _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Failed", Error = msg });
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo { Output = $"[Translate] {msg}" });
                    return;
                }

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Starting: {Path.GetFileName(sourceFile)} -> {request.TranslateTo} ..." });
                await Clients.Group(conn).SendAsync("ReceiveState",
                    new DownloadInfo { State = $"Translating subtitles to {request.TranslateTo}..." });
                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Running", CurrentLine = 0, TotalLines = 0 });

                SubtitleTranslationResult result;
                await using (var stream = File.OpenRead(sourceFile))
                {
                    result = await _subtitleTranslationService.TranslateSubtitleAsync(stream, request.TranslateTo!);
                }

                var outputDir = _subtitleSettings.Value.OutputPath;
                if (string.IsNullOrWhiteSpace(outputDir))
                {
                    // Fall back to writing next to the source if OutputPath isn't
                    // configured, rather than failing outright.
                    outputDir = Path.GetDirectoryName(sourceFile) ?? ".";
                }
                Directory.CreateDirectory(outputDir);

                var fileNameNoExt = Path.GetFileNameWithoutExtension(sourceFile);
                // Strip a trailing ".en" / ".th" etc. language suffix if present so we
                // don't end up with "video.en.km.srt" style names.
                fileNameNoExt = Regex.Replace(fileNameNoExt, @"\.[a-zA-Z-]{2,8}$", string.Empty);
                var translatedPath = Path.Combine(outputDir, $"{fileNameNoExt}.{request.TranslateTo}.srt");
                await File.WriteAllTextAsync(translatedPath, result.Content, new System.Text.UTF8Encoding(false));

                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Completed", TranslatedFile = translatedPath });

                var detectedNote = result.DetectedSourceLanguage != null
                    ? $" (detected source language: {result.DetectedSourceLanguage})"
                    : string.Empty;
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Done -> {translatedPath}{detectedNote}" });

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
