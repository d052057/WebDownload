using System.Diagnostics;
using System.Text;
using WebDownload.Server.Models;
namespace WebDownload.Server.Services
{

    public interface IDownloadService
    {
        Task StartDownloadAsync(DownloadRequest request, Func<DownloadInfo, Task> callback);
        Task StartDownloadTitleAsync(DownloadTitleRequest request, Func<DownloadInfo, Task> callback);
    };

    public class DownloadService : IDownloadService
    {
        private string OutputFileTemplate = @"%(title)s [%(id)s].%(ext)s";
        private readonly string configPath = @"yt-dlp.conf";
        private string ytDlpPath = @"yt-dlp.exe";
        private StringBuilder sb = new StringBuilder();
        public async Task StartDownloadTitleAsync(DownloadTitleRequest request, Func<DownloadInfo, Task> callback)
        {
            sb.Clear();
            sb.AppendFormat(" {0} {1}", "--config-location", configPath);
            sb.AppendFormat(" --progress -o \"{0}\" --restrict-filenames", OutputFileTemplate);
            sb.AppendFormat(" --no-warnings --print filename --skip-download");
            sb.AppendFormat(" \"{0}\"", request.Url);
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = sb.ToString(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
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
            sb.AppendFormat(" --config-location \"{0}\"", configPath);

            // Output folder and template
            sb.AppendFormat(" -P \"{0}\"", request.OutputFolder);
            sb.AppendFormat(" --progress -o \"{0}\"", OutputFileTemplate);
            if (request.AudioOnly)
            {
                sb.AppendFormat(" -f {0} ", "bestaudio");
                if (request.AudioFormat.Length > 0)
                {
                    sb.AppendFormat(" -x {0} {1}", "--audio-format", request.AudioFormat);
                }
                if (request.AudioChapter)
                {
                    sb.AppendFormat(" {0} ", "--split-chapters");
                }
               
            }
            else
            {
                if (request.SubTitle)
                {
                    sb.AppendFormat(" --sub-langs \"{0}\" --write-subs --write-auto-subs",
                        request.SubTitleLang ?? "en");
                }
            }
            ;
            sb.Append(" --no-warnings");
            sb.AppendFormat(" \"{0}\"", request.Url);

            Console.WriteLine($"yt-dlp command: {sb}");
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ytDlpPath,
                        Arguments = sb.ToString(),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
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

    }
}
