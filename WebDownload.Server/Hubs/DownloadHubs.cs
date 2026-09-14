using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Diagnostics;
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
        private readonly YtDlpSettings _ytDlpSettings;

        // Container extensions this app recognizes as a downloaded video file
        // when hunting for "the file yt-dlp just produced" to embed a subtitle into.
        private static readonly HashSet<string> VideoFileExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".flv", ".m4v" };

        public DownloadHub(
            IHttpClientFactory httpClientFactory,
            IDownloadService downloadService,
            ISubtitleTranslationService subtitleTranslationService,
            ITranslationJobTracker jobTracker,
            IOptions<SubtitleSettings> subtitleSettings,
            IOptions<ApplicationSettings> appSettings,
            IOptions<YtDlpSettings> ytDlpSettings
            )
        {
            _downloadService = downloadService;
            _subtitleTranslationService = subtitleTranslationService;
            _jobTracker = jobTracker;
            _subtitleSettings = subtitleSettings;
            //_httpClientFactory = httpClientFactory;
            _appSettings = appSettings;
            _ytDlpSettings = ytDlpSettings.Value;
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
            string? translatedPath = null; // set if TranslateTo produced a file, used by the embed step below
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
                    translatedPath = await TranslateDownloadedSubtitlesAsync(conn, request, downloadedBaseName);
                }
                else if (!string.IsNullOrWhiteSpace(request.TranslateTo))
                {
                    _jobTracker.SetStatus(conn, new TranslationJobStatus
                    {
                        State = "Failed",
                        Error = "Could not determine the downloaded file's base name - the yt-dlp 'Destination:' line was never matched. See the output log above."
                    });
                }

                if (request.EmbedSubtitle && downloadedBaseName != null)
                {
                    await EmbedSubtitleIntoVideoAsync(conn, request, downloadedBaseName, translatedPath);
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
        // Resolves where translated/embedded closecaption files should live:
        // the "Translate File Folder" per-movie folder if that checkbox was
        // checked on the client, otherwise the shared Subtitle:OutputPath
        // folder used by default.
        private string ResolveClosecaptionDir(DownloadRequest request, string fallbackDir)
        {
            if (!string.IsNullOrWhiteSpace(request.TranslateOutputFolder))
            {
                return Path.Combine(_appSettings.Value.MediaDrive, request.TranslateOutputFolder);
            }
            var outputDir = _subtitleSettings.Value.OutputPath;
            return string.IsNullOrWhiteSpace(outputDir) ? fallbackDir : outputDir;
        }

        private async Task<string?> TranslateDownloadedSubtitlesAsync(string conn, DownloadRequest request, string downloadedBaseName)
        {
            try
            {
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Searching '{request.OutputFolder}' for '{downloadedBaseName}*.srt' ..." });

                // Look for either .srt or .vtt - yt-dlp can write either depending on
                // --sub-format/--convert-subs, and the translated output should match.
                var srtFiles = Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}*.srt")
                    .Concat(Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}*.vtt"))
                    .ToArray();
                if (srtFiles.Length == 0)
                {
                    var msg = $"No .srt or .vtt file matching '{downloadedBaseName}*' was found in '{request.OutputFolder}'. " +
                               "Check that a subtitle language was actually selected before downloading.";
                    _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Failed", Error = msg });
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo { Output = $"[Translate] {msg}" });
                    return null;
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
                    return null;
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

                var outputDir = ResolveClosecaptionDir(request, Path.GetDirectoryName(sourceFile) ?? ".");
                Directory.CreateDirectory(outputDir);

                var fileNameNoExt = Path.GetFileNameWithoutExtension(sourceFile);
                // Strip a trailing ".en" / ".th" etc. language suffix if present so we
                // don't end up with "video.en.km.srt" style names.
                fileNameNoExt = Regex.Replace(fileNameNoExt, @"\.[a-zA-Z-]{2,8}$", string.Empty);
                var sourceExt = Path.GetExtension(sourceFile); // preserve .srt vs .vtt
                var translatedPath = Path.Combine(outputDir, $"{fileNameNoExt}.{request.TranslateTo}{sourceExt}");
                await File.WriteAllTextAsync(translatedPath, result.Content, new System.Text.UTF8Encoding(false));

                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Completed", TranslatedFile = translatedPath });
                await Clients.Group(conn).SendAsync("ReceiveState",
                    new DownloadInfo { State = $"Translating subtitles to {request.TranslateTo} completed..." });

                var detectedNote = result.DetectedSourceLanguage != null
                    ? $" (detected source language: {result.DetectedSourceLanguage})"
                    : string.Empty;
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Done -> {translatedPath}{detectedNote}" });

                DownloadInfo info = new() { TranslatedFile = translatedPath };
                await Clients.Group(conn).SendAsync("ReceiveTranslatedFile", info);
                return translatedPath;
            }
            catch (Exception ex)
            {
                _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Failed", Error = ex.Message });
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Failed: {ex.Message}" });
                DownloadInfo errInfo = new() { Error = $"Hub Error translating subtitles: {ex.Message}" };
                await Clients.Group(conn).SendAsync("ReceiveError", errInfo);
                return null;
            }
        }

        // "Embed Subtitle" checkbox handler: mux the closecaption file into the
        // downloaded video as a subtitle track using ffmpeg. Runs after
        // translation (if any) so it can use the just-produced translatedPath;
        // if no translation happened this run, falls back to looking for an
        // already-existing closecaption file in the same folder translation
        // would have used (Translate File Folder if checked, otherwise the
        // shared Subtitle:OutputPath folder).
        private async Task EmbedSubtitleIntoVideoAsync(string conn, DownloadRequest request, string downloadedBaseName, string? translatedPath)
        {
            try
            {
                var subtitlePath = translatedPath;
                if (subtitlePath == null || !File.Exists(subtitlePath))
                {
                    var closecaptionDir = ResolveClosecaptionDir(request, request.OutputFolder);
                    subtitlePath = Directory.Exists(closecaptionDir)
                        ? Directory.GetFiles(closecaptionDir, $"{downloadedBaseName}*.srt")
                            .Concat(Directory.GetFiles(closecaptionDir, $"{downloadedBaseName}*.vtt"))
                            .FirstOrDefault()
                        : null;
                }

                if (subtitlePath == null)
                {
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                    {
                        Output = $"[Embed] No closecaption file found for '{downloadedBaseName}' to embed. " +
                                  "Translate a subtitle first, or make sure one already exists in the closecaption folder."
                    });
                    return;
                }

                var videoPath = Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}.*")
                    .FirstOrDefault(f => VideoFileExtensions.Contains(Path.GetExtension(f)));
                if (videoPath == null)
                {
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                    {
                        Output = $"[Embed] No downloaded video file found matching '{downloadedBaseName}' in '{request.OutputFolder}'."
                    });
                    return;
                }

                await Clients.Group(conn).SendAsync("ReceiveState",
                    new DownloadInfo { State = "Embedding subtitle into video..." });
                await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                {
                    Output = $"[Embed] Merging '{Path.GetFileName(subtitlePath)}' into '{Path.GetFileName(videoPath)}' ..."
                });

                var ext = Path.GetExtension(videoPath);
                var subtitleCodec = _ytDlpSettings.EmbedSubtitleCodecByExtension.TryGetValue(ext, out var codec)
                    ? codec
                    : _ytDlpSettings.EmbedSubtitleDefaultCodec;

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(videoPath) ?? request.OutputFolder,
                    $"{Path.GetFileNameWithoutExtension(videoPath)}.embedded{Path.GetExtension(videoPath)}");

                var args = string.Format(_ytDlpSettings.EmbedSubtitleArgsTemplate, videoPath, subtitlePath, subtitleCodec, outputPath);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ytDlpSettings.FfmpegExecutablePath,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    }
                };
                process.Start();
                // ffmpeg writes its progress/log to stderr, not stdout.
                var stderrTask = process.StandardError.ReadToEndAsync();
                await process.StandardOutput.ReadToEndAsync();
                var ffmpegLog = await stderrTask;
                await process.WaitForExitAsync();

                if (process.ExitCode != 0 || !File.Exists(outputPath))
                {
                    await Clients.Group(conn).SendAsync("ReceiveOutput",
                        new DownloadInfo { Output = $"[Embed] ffmpeg failed (exit code {process.ExitCode}):\n{ffmpegLog}" });
                    await Clients.Group(conn).SendAsync("ReceiveError",
                        new DownloadInfo { Error = "Embedding subtitle into the video failed. See the output log above for the ffmpeg error." });
                    return;
                }

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Embed] Done -> {outputPath}" });
                await Clients.Group(conn).SendAsync("ReceiveEmbeddedFile", new DownloadInfo { EmbeddedFile = outputPath });
            }
            catch (Exception ex)
            {
                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Embed] Failed: {ex.Message}" });
                await Clients.Group(conn).SendAsync("ReceiveError",
                    new DownloadInfo { Error = $"Hub Error embedding subtitle: {ex.Message}" });
            }
        }
    }
}
