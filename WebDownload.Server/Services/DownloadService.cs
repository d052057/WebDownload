using WebDownload.Server.Models;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
namespace WebDownload.Server.Services
{
    public interface IDownloadService
    {
        Task StartDownloadAsync(DownloadRequest request, Func<string, Task> callback);
    };
    public class DownloadService : IDownloadService
    {
        private string OutputFileTemplate = @"%(title)s [%(id)s].%(ext)s";

        private string ytDlpPath = @"yt-dlp.exe";
        private StringBuilder sb = new StringBuilder();

        public async Task StartDownloadAsync(DownloadRequest request, Func<string, Task> callback)
        {
            //--list-subs  --skip-download --get-title
            sb.Clear();
            sb.AppendFormat(" -P {0}", request.OutputFolder); // output to a folder
            sb.AppendFormat(" {0} -o \"{1}\"", "--progress", OutputFileTemplate);

            // inlude subtitles
            if (request.SubTitle)
            {
                //sb.Append(" --sub-langs \"en,km\" --write-subs --write-auto-subs");
                sb.AppendFormat(" --sub-langs \"{0}\" --write-subs --write-auto-subs", "en.*,km");
            };
            sb.AppendFormat(" {0}", "--no-warnings");
            sb.AppendFormat(" {0}", request.Url);
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
                        Console.WriteLine($"ProgressLine: {progressLine}");
                        if (!string.IsNullOrWhiteSpace(progressLine))
                        {
                            await callback(progressLine);
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
                    await callback(error);
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
