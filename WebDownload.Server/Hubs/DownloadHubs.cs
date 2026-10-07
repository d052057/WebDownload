using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        private readonly HashSet<string> SubtitleFileExtensions;

        private readonly Regex rgxMerger = new Regex(@"\[Merger\] Merging formats into ""(?<downloadFileName>.+)""");
        private readonly Regex regex = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+~?\s*(?<totalSize>[\d.\w]+) at\s+(?<speed>[\d.\w/]+)\s+ETA\s+(?<eta>[\w\d:]+)(\s\(frag (?<fragNumber>\d{1,3}/\d{1,3})\))?");
        private readonly Regex rgxHlsnative = new Regex(@"\[hlsnative\] Total fragments:\s(?<TotalFragment>[\d]+)");
        private readonly Regex rgxLast = new Regex(@"\[download\]\s+(?<progress>[\d.]+%) of\s+(?<totalSize>[\d.\w]+) in\s+(?<eta>[\w\d:]+) at\s+(?<speed>[\d.\w/]+)");


        private readonly IOptions<ApplicationSettings> _appSettings;
        private readonly YtDlpSettings _ytDlpSettings;

        private readonly HashSet<string> VideoFileExtensions;

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
            _appSettings = appSettings;
            _ytDlpSettings = ytDlpSettings.Value;
            SubtitleFileExtensions = new HashSet<string>(_ytDlpSettings.GetSubtitleFileExtensions(), StringComparer.OrdinalIgnoreCase);
            VideoFileExtensions = new HashSet<string>(_ytDlpSettings.GetVideoFileExtensions(), StringComparer.OrdinalIgnoreCase);
        }

        public string GetConnectionId() => Context.ConnectionId;

        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
        }

        public Task<TranslationJobStatus?> GetTranslationStatus(string groupId)
        {
            return Task.FromResult(_jobTracker.GetStatus(groupId));
        }

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
            string? downloadedBaseName = null;
            string? translatedPath = null;
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
                        var destFile = matchFileName.Groups["downloadFileName"].Value.Trim();
                        if (!SubtitleFileExtensions.Contains(System.IO.Path.GetExtension(destFile)))
                        {
                            downloadedBaseName ??= System.IO.Path.GetFileNameWithoutExtension(destFile);
                        }
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
                                var progressPercentage = match.Groups["progress"].Value;
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

                if (downloadedBaseName != null)
                {
                    await MoveDownloadedSubtitlesToClosecaptionAsync(conn, request, downloadedBaseName);
                }

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

        // Checked "Translate File Folder" -> shared Subtitle:StoragePath folder for
        // every download. Unchecked -> a "closecaption" subfolder inside this
        // download's own OutputFolder, so movies\9 gets movies\9\closecaption.
        private string GetSubtitleStorageDir(DownloadRequest request)
        {
            var dir = request.UseSharedClosecaptionFolder
                ? _subtitleSettings.Value.StoragePath
                : Path.Combine(request.OutputFolder, "closecaption");
            return Path.GetFullPath(dir);
        }

        // Same split for translated output, with an extra "translate" subfolder in
        // both cases so originals and translations never collide in the same folder.
        private string GetTranslatedOutputDir(DownloadRequest request)
        {
            var dir = request.UseSharedClosecaptionFolder
                ? _subtitleSettings.Value.OutputPath
                : Path.Combine(request.OutputFolder, "closecaption", "translate");
            return Path.GetFullPath(dir);
        }

        // Moves the .srt/.vtt file(s) yt-dlp just wrote (into request.OutputFolder,
        // alongside the video) into the shared subtitle storage folder, so every
        // original subtitle ends up there rather than staying next to the video.
        // Always overwrites on a name collision - the storage folder is shared
        // with another app using the same naming convention by design, not by
        // accident, so last-write-wins is the intended behavior here.
        private async Task MoveDownloadedSubtitlesToClosecaptionAsync(string conn, DownloadRequest request, string downloadedBaseName)
        {
            try
            {
                var subtitleFiles = Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}*.srt")
                    .Concat(Directory.GetFiles(request.OutputFolder, $"{downloadedBaseName}*.vtt"))
                    .ToArray();

                if (subtitleFiles.Length == 0) return;

                var storageDir = GetSubtitleStorageDir(request);
                Directory.CreateDirectory(storageDir);

                var movedCount = 0;
                foreach (var file in subtitleFiles)
                {
                    var dest = Path.Combine(storageDir, Path.GetFileName(file));
                    if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // already there - nothing to move
                    }
                    CopyOrCleanSubtitle(file, dest);
                    File.Delete(file);
                    movedCount++;
                }

                if (movedCount > 0)
                {
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                    {
                        Output = $"[Subtitles] Moved {movedCount} downloaded subtitle file(s) to '{storageDir}'."
                    });
                }
            }
            catch (Exception ex)
            {
                await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                {
                    Output = $"[Subtitles] Failed to move downloaded subtitle files to closecaption folder: {ex.Message}"
                });
            }
        }

        private async Task<string?> TranslateDownloadedSubtitlesAsync(string conn, DownloadRequest request, string downloadedBaseName)
        {
            try
            {
                var storageDir = GetSubtitleStorageDir(request);

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Searching '{storageDir}' for '{downloadedBaseName}*.srt' ..." });

                var srtFiles = Directory.GetFiles(storageDir, $"{downloadedBaseName}*.srt")
                    .Concat(Directory.GetFiles(storageDir, $"{downloadedBaseName}*.vtt"))
                    .ToArray();
                if (srtFiles.Length == 0)
                {
                    var msg = $"No .srt or .vtt file matching '{downloadedBaseName}*' was found in '{storageDir}'. " +
                               "Check that a subtitle language was actually selected before downloading.";
                    _jobTracker.SetStatus(conn, new TranslationJobStatus { State = "Failed", Error = msg });
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo { Output = $"[Translate] {msg}" });
                    return null;
                }

                await Clients.Group(conn).SendAsync("ReceiveOutput",
                    new DownloadInfo { Output = $"[Translate] Found {srtFiles.Length} .srt file(s): {string.Join(", ", srtFiles.Select(Path.GetFileName))}" });

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

                //SubtitleTranslationResult result;
                //await using (var stream = File.OpenRead(sourceFile))
                //{
                //    result = await _subtitleTranslationService.TranslateSubtitleAsync(stream, request.TranslateTo!);
                //}
                SubtitleTranslationResult result;
                await using (var stream = await OpenCleanedSubtitleStreamAsync(conn, sourceFile))
                {
                    result = await _subtitleTranslationService.TranslateSubtitleAsync(stream, request.TranslateTo!);
                }
                var outputDir = GetTranslatedOutputDir(request);
                Directory.CreateDirectory(outputDir);

                var fileNameNoExt = Path.GetFileNameWithoutExtension(sourceFile);
                fileNameNoExt = Regex.Replace(fileNameNoExt, @"\.[a-zA-Z-]{2,8}$", string.Empty);
                var sourceExt = Path.GetExtension(sourceFile);
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
        // Cleans YouTube rolling auto-captions in memory and returns a stream of the result.
        // The original file in the (shared) closecaption folder is never modified.
        private async Task<Stream> OpenCleanedSubtitleStreamAsync(string conn, string sourceFile)
        {
            // yt-dlp converts subtitles to .srt, and the cleaner writes SRT, so a .vtt
            // source is passed through unchanged.
            if (string.Equals(Path.GetExtension(sourceFile), ".srt", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var raw = await File.ReadAllTextAsync(sourceFile, Encoding.UTF8);
                    var cleaned = SubtitleCleanerService.CleanText(raw, out var cueCount);
                    if (cueCount > 0)
                    {
                        await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                        {
                            Output = $"[Subtitles] Cleaned '{Path.GetFileName(sourceFile)}' before translation: {cueCount} cue(s) kept."
                        });
                        return new MemoryStream(new UTF8Encoding(false).GetBytes(cleaned));
                    }
                }
                catch (Exception ex)
                {
                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                    {
                        Output = $"[Subtitles] Cleanup skipped, translating the original file: {ex.Message}"
                    });
                }
            }
            return File.OpenRead(sourceFile);
        }
        // "Embed Subtitle" checkbox handler: mux the closecaption file into the
        // downloaded video as a subtitle track using ffmpeg. Runs after
        // translation (if any) so it can use the just-produced translatedPath;
        // if no translation happened this run, falls back to searching the
        // shared translated-output folder, then the shared subtitle-storage
        // folder, for an already-existing closecaption file.
        //
        // Complex-script languages (Khmer, Thai, ...) take the hardsub path:
        // converted to a proper ASS file matching the proven-working native
        // app's style block exactly (fixed PlayResX 1080 / PlayResY 1920 /
        // Fontsize 54), then burned in via libass, since soft mov_text
        // subtitles render those scripts' shaping incorrectly. This path
        // always outputs .mp4 regardless of the source container, since the
        // libx264 re-encode it requires cannot be muxed into WebM. Everything
        // else uses the normal soft-mux path.
        private async Task EmbedSubtitleIntoVideoAsync(string conn, DownloadRequest request, string downloadedBaseName, string? translatedPath)
        {
            string? tempAssPath = null;
            try
            {
                var subtitlePath = translatedPath;
                if (subtitlePath == null || !File.Exists(subtitlePath))
                {
                    var translatedDir = GetTranslatedOutputDir(request);
                    subtitlePath = Directory.Exists(translatedDir)
                        ? Directory.GetFiles(translatedDir, $"{downloadedBaseName}*.srt")
                            .Concat(Directory.GetFiles(translatedDir, $"{downloadedBaseName}*.vtt"))
                            .FirstOrDefault()
                        : null;

                    if (subtitlePath == null)
                    {
                        var storageDir = GetSubtitleStorageDir(request);
                        subtitlePath = Directory.Exists(storageDir)
                            ? Directory.GetFiles(storageDir, $"{downloadedBaseName}*.srt")
                                .Concat(Directory.GetFiles(storageDir, $"{downloadedBaseName}*.vtt"))
                                .FirstOrDefault()
                            : null;
                    }
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

                var ext = Path.GetExtension(videoPath).ToLowerInvariant();
                var subtitleCodec = _ytDlpSettings.EmbedSubtitleCodecByExtension.TryGetValue(ext, out var codec)
                    ? codec
                    : _ytDlpSettings.EmbedSubtitleDefaultCodec;

                var langKey = request.TranslateTo?.ToLowerInvariant() ?? string.Empty;
                var subtitleLanguage = _ytDlpSettings.LanguageCodeMap.TryGetValue(langKey, out var mappedLang)
                    ? mappedLang
                    : "und";

                bool needsHardsub = _ytDlpSettings.ComplexScriptLanguages.Contains(langKey);

                var outputExtension = needsHardsub ? ".mp4" : Path.GetExtension(videoPath);
                var outputPath = Path.Combine(
                    Path.GetDirectoryName(videoPath) ?? request.OutputFolder,
                    $"{Path.GetFileNameWithoutExtension(videoPath)}.embedded{outputExtension}");

                string args;
                if (needsHardsub)
                {
                    var fontName = _ytDlpSettings.HardsubFontByLanguage.TryGetValue(langKey, out var font)
                        ? font
                        : _ytDlpSettings.HardsubFontDefault;

                    var fontsDir = Path.IsPathRooted(_ytDlpSettings.FontsDirectory)
                        ? _ytDlpSettings.FontsDirectory
                        : Path.Combine(AppContext.BaseDirectory, _ytDlpSettings.FontsDirectory);

                    tempAssPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ass");
                    ConvertSubtitleToAss(subtitlePath, tempAssPath, fontName);

                    var escapedAssPath = EscapeForSubtitlesFilter(tempAssPath);
                    var escapedFontsDir = EscapeForSubtitlesFilter(fontsDir);

                    args = string.Format(_ytDlpSettings.HardsubEmbedArgsTemplate,
                        videoPath, escapedAssPath, escapedFontsDir, outputPath);

                    await Clients.Group(conn).SendAsync("ReceiveOutput", new DownloadInfo
                    {
                        Output = $"[Embed] '{langKey}' needs shaped-script rendering - converted to ASS and burning in with font '{fontName}' from '{fontsDir}' (this re-encodes video and will take longer)."
                    });
                }
                else
                {
                    args = string.Format(_ytDlpSettings.EmbedSubtitleArgsTemplate, videoPath, subtitlePath, subtitleCodec, outputPath, subtitleLanguage);
                }

                var fullEmbedCommand = $"{_ytDlpSettings.FfmpegExecutablePath} {args}";
                await Clients.Group(conn).SendAsync("ReceiveEmbedCommand", new DownloadInfo { Command = fullEmbedCommand });

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
            finally
            {
                if (tempAssPath != null && File.Exists(tempAssPath))
                {
                    try { File.Delete(tempAssPath); } catch { /* best-effort cleanup */ }
                }
            }
        }
        // Writes the subtitle to dest. .srt files are cleaned of YouTube rolling duplicates and
        // filler cues on the way; anything else (or anything the cleaner can't read) is copied as-is.
        private static void CopyOrCleanSubtitle(string source, string dest)
        {
            if (string.Equals(Path.GetExtension(source), ".srt", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var cleaned = SubtitleCleanerService.CleanText(File.ReadAllText(source, Encoding.UTF8), out var cueCount);
                    if (cueCount > 0)
                    {
                        File.WriteAllText(dest, cleaned, new UTF8Encoding(false));
                        return;
                    }
                }
                catch
                {
                    // fall through to a plain copy
                }
            }
            File.Copy(source, dest, overwrite: true);
        }
        private static void ConvertSubtitleToAss(string sourcePath, string assPath, string fontName)
        {
            var assContent = new StringBuilder();
            assContent.AppendLine("[Script Info]");
            assContent.AppendLine("ScriptType: v4.00+");
            assContent.AppendLine("PlayResX: 1080");
            assContent.AppendLine("PlayResY: 1920");
            assContent.AppendLine("ScaledBorderAndShadow: yes");
            assContent.AppendLine();
            assContent.AppendLine("[V4+ Styles]");
            assContent.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            assContent.AppendLine($"Style: Default,{fontName},54,&Hffffff,&Hffffff,&H000000,&H0,0,0,0,0,100,100,0,0,1,4,0,2,50,50,120,1");
            assContent.AppendLine();
            assContent.AppendLine("[Events]");
            assContent.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            var lines = File.ReadAllLines(sourcePath, Encoding.UTF8);
            string startTime = "", endTime = "", textCollector = "";

            foreach (var rawLine in lines)
            {
                var trimmed = rawLine.Trim();
                if (trimmed.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase)) continue;

                if (string.IsNullOrEmpty(trimmed))
                {
                    if (!string.IsNullOrEmpty(startTime) && !string.IsNullOrEmpty(textCollector))
                    {
                        assContent.AppendLine($"Dialogue: 0,{startTime},{endTime},Default,,0,0,0,,{textCollector.Trim()}");
                    }
                    startTime = ""; endTime = ""; textCollector = "";
                    continue;
                }

                if (trimmed.Contains("-->"))
                {
                    var parts = trimmed.Split(new[] { "-->" }, StringSplitOptions.None);
                    startTime = FormatSubtitleTimeToAss(parts[0].Trim());
                    endTime = FormatSubtitleTimeToAss(parts[1].Trim().Split(' ')[0]);
                }
                else if (!int.TryParse(trimmed, out _))
                {
                    textCollector += (string.IsNullOrEmpty(textCollector) ? "" : "\\N") + trimmed;
                }
            }

            if (!string.IsNullOrEmpty(startTime) && !string.IsNullOrEmpty(textCollector))
            {
                assContent.AppendLine($"Dialogue: 0,{startTime},{endTime},Default,,0,0,0,,{textCollector.Trim()}");
            }

            File.WriteAllText(assPath, assContent.ToString(), new UTF8Encoding(false));
        }

        private static string FormatSubtitleTimeToAss(string time)
        {
            var formatted = time.Replace(',', '.');
            if (formatted.StartsWith("00:")) formatted = "0:" + formatted.Substring(3);
            if (formatted.Length > 10) formatted = formatted.Substring(0, 10);
            return formatted;
        }

        private static string EscapeForSubtitlesFilter(string path)
        {
            return path
                .Replace(@"\", @"\\")
                .Replace(":", @"\:")
                .Replace("'", @"\'");
        }
    }
}