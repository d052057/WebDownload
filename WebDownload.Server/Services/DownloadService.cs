using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;
namespace WebDownload.Server.Services
{

    public interface IDownloadService
    {
        Task StartDownloadAsync(DownloadRequest request, Func<DownloadInfo, Task> callback);
        Task StartDownloadTitleAsync(DownloadTitleRequest request, Func<DownloadInfo, Task> callback);
        Task<List<SubtitleTrack>> GetAvailableSubtitlesAsync(string url);
    };

    public class DownloadService : IDownloadService
    {
        private readonly YtDlpSettings _settings;
        private StringBuilder sb = new StringBuilder();

        public DownloadService(IOptions<YtDlpSettings> ytDlpSettings)
        {
            _settings = ytDlpSettings.Value;
        }

        public async Task StartDownloadTitleAsync(DownloadTitleRequest request, Func<DownloadInfo, Task> callback)
        {
            sb.Clear();
            sb.AppendFormat(" {0} {1}", "--config-location", _settings.ConfigLocation);
            sb.AppendFormat(" --progress -o \"{0}\" --restrict-filenames", _settings.OutputFileTemplate);
            foreach (var arg in _settings.TitleLookupArgs)
            {
                sb.Append(' ').Append(arg);
            }
            sb.AppendFormat(" \"{0}\"", request.Url);
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo    
                    {
                        FileName = _settings.ExecutablePath,
                        Arguments = sb.ToString(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                        EnvironmentVariables = { ["PYTHONIOENCODING"] = "utf-8" }
                    }
                };

                process.Start();
                // Stream progress updates asynchronously
                try
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var progressLine = await process.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(progressLine))
                        {
                            DownloadInfo info = new() { Output = progressLine };
                            await callback(info);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading process output: {ex.Message}");
                }
                var error = await process.StandardError.ReadToEndAsync();
                while (!process.StandardError.EndOfStream)
                {
                    var errorLine = await process.StandardError.ReadLineAsync();
                    Console.WriteLine($"ErrorLine: {errorLine}");
                    DownloadInfo info = new() { Error = errorLine };
                    await callback(info);
                }
                await process.WaitForExitAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Download: {ex.Message}");
                throw;
            }
        }
        public async Task StartDownloadAsync(DownloadRequest request, Func<DownloadInfo, Task> callback)
        {
            //--list-subs  --skip-download --get-title
            sb.Clear();
            // Config location
            sb.AppendFormat(" --config-location \"{0}\"", _settings.ConfigLocation);

            // Output folder and template
            sb.AppendFormat(" -P \"{0}\"", request.OutputFolder);
            sb.AppendFormat(" --progress -o \"{0}\" --restrict-filenames", _settings.OutputFileTemplate);
            if (request.AudioOnly)
            {
                foreach (var arg in _settings.AudioOnlyArgs)
                {
                    sb.Append(' ').Append(arg);
                }
                if (request.AudioFormat.Length > 0)
                {
                    sb.Append(' ').AppendFormat(_settings.AudioFormatArgsTemplate, request.AudioFormat);
                }
                if (request.AudioChapter)
                {
                    foreach (var arg in _settings.AudioChapterArgs)
                    {
                        sb.Append(' ').Append(arg);
                    }
                }
               
            }
            else
            {
                if (request.SubtitleLangs is { Count: > 0 })
                {
                    var langs = string.Join(",", request.SubtitleLangs);
                    sb.Append(' ').AppendFormat(_settings.SubtitleArgsTemplate, langs);
                }
            }
            ;
            if (!string.IsNullOrWhiteSpace(request.Options))
            {
                // Options is entered as multi-line text in the UI; collapse newlines to spaces
                // since the process argument string must be a single line.
                var customOptions = request.Options.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
                sb.AppendFormat(" {0}", customOptions);
            }
            foreach (var arg in _settings.CommonDownloadArgs)
            {
                sb.Append(' ').Append(arg);
            }
            sb.AppendFormat(" \"{0}\"", request.Url);

            Console.WriteLine($"yt-dlp command: {sb}");
            await callback(new DownloadInfo { Command = sb.ToString() });
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _settings.ExecutablePath,
                        Arguments = sb.ToString(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                        EnvironmentVariables = { ["PYTHONIOENCODING"] = "utf-8" }
                    }
                };

                process.Start();
                // Stream progress updates asynchronously
                try
                {
                    while (!process.StandardOutput.EndOfStream)
                    {
                        var progressLine = await process.StandardOutput.ReadLineAsync();
                        
                        if (!string.IsNullOrWhiteSpace(progressLine))
                        {
                            DownloadInfo info = new() { Output = progressLine };
                            await callback(info);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading process output: {ex.Message}");
                }
                var error = await process.StandardError.ReadToEndAsync();
                while (!process.StandardError.EndOfStream)
                {
                    var errorLine = await process.StandardError.ReadLineAsync();
                    Console.WriteLine($"ErrorLine: {errorLine}");
                    DownloadInfo info = new() { Error = errorLine };
                    await callback(info);
                }
                await process.WaitForExitAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Download: {ex.Message}");
                throw;
            }
        }

        // Runs `yt-dlp --list-subs` and parses both the "Available subtitles"
        // (manually authored) and "Available automatic captions" sections into
        // a flat list the UI can render as checkboxes.
        public async Task<List<SubtitleTrack>> GetAvailableSubtitlesAsync(string url)
        {
            var tracks = new List<SubtitleTrack>();
            var args = new StringBuilder();
            args.AppendFormat(" --config-location \"{0}\"", _settings.ConfigLocation);
            foreach (var arg in _settings.ListSubtitlesArgs)
            {
                args.Append(' ').Append(arg);
            }
            args.AppendFormat(" \"{0}\"", url);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _settings.ExecutablePath,
                    Arguments = args.ToString(),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    EnvironmentVariables = { ["PYTHONIOENCODING"] = "utf-8" }
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.StandardError.ReadToEndAsync(); // drain, ignore for now
            await process.WaitForExitAsync();

            bool isAutomaticSection = false;
            bool inSubtitleTable = false;
            var seen = new HashSet<string>();

            foreach (var rawLine in stdout.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');

                if (line.Contains("Available automatic captions for", StringComparison.OrdinalIgnoreCase))
                {
                    isAutomaticSection = true;
                    inSubtitleTable = true;
                    continue;
                }
                if (line.Contains("Available subtitles for", StringComparison.OrdinalIgnoreCase))
                {
                    isAutomaticSection = false;
                    inSubtitleTable = true;
                    continue;
                }
                if (!inSubtitleTable)
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    inSubtitleTable = false;
                    continue;
                }
                // Header row: "Language Name    Formats"
                if (line.TrimStart().StartsWith("Language", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Columns are aligned with runs of 2+ spaces:
                // "en       English                                     vtt, ttml, ..."
                var columns = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s{2,}");
                if (columns.Length < 1)
                {
                    continue;
                }

                var code = columns[0].Trim();
                // Real rows are either 3 columns (code, name, formats) or, for a
                // handful of "no separate name" auto-caption rows, just 2 columns
                // (code, formats) with no name at all - in that case don't treat
                // the Formats string (e.g. "vtt") as if it were the Name.
                var name = columns.Length >= 3 ? columns[1].Trim() : code;

                // YouTube lets yt-dlp report an on-demand auto-translated caption
                // for essentially every language once a video has ASR captions at
                // all (yt-dlp names these rows e.g. "Albanian from Arabic", "Zulu
                // from English", ...). These aren't real files on YouTube and this
                // app does its own translation step after download, so skip them -
                // only keep genuine subtitle/caption tracks.
                bool isTranslatedPseudoTrack = name.Contains(" from ", StringComparison.OrdinalIgnoreCase);
                if (isTranslatedPseudoTrack)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(code) || !seen.Add(code + "|" + isAutomaticSection))
                {
                    continue;
                }

                tracks.Add(new SubtitleTrack
                {
                    Code = code,
                    Name = isAutomaticSection ? $"{name} (auto)" : name,
                    IsAutomatic = isAutomaticSection
                });
            }

            return tracks;
        }
    }
}
