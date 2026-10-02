using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

public sealed class FfmpegException : Exception
{
    public FfmpegException(string message) : base(message) { }
}

public sealed record MediaProbe(TimeSpan? Duration, int AudioStreams);

/// <summary>
/// Runs ffmpeg with an argument LIST (no hand-quoted command strings), checks the exit
/// code, keeps stderr for error messages, and kills the process when the token is cancelled.
/// Reuses YtDlp:FfmpegExecutablePath; a bare name is looked up next to the app first, then on PATH.
/// </summary>
public sealed class FfmpegRunner
{
    private static readonly Regex DurationRx =
        new(@"Duration:\s*(?<h>\d+):(?<m>\d+):(?<s>\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex AudioStreamRx =
        new(@"Stream #\d+:\d+.*?: Audio:", RegexOptions.Compiled);

    private readonly string _exe;

    public FfmpegRunner(IOptions<YtDlpSettings> yt)
    {
        var configured = string.IsNullOrWhiteSpace(yt.Value.FfmpegExecutablePath)
            ? "ffmpeg.exe"
            : yt.Value.FfmpegExecutablePath;
        var local = Path.Combine(AppContext.BaseDirectory, configured);
        _exe = !Path.IsPathRooted(configured) && File.Exists(local) ? local : configured;
    }

    /// <param name="stdoutTo">When set, ffmpeg's stdout (use "-" as the output) is copied here.</param>
    /// <param name="allowNonZeroExit">For probing: "ffmpeg -i file" exits 1 by design.</param>
    /// <param name="verbose">Probing needs the info-level stream listing; normal runs only want errors.</param>
    /// <returns>ffmpeg's stderr text.</returns>
    public async Task<string> RunAsync(
        IEnumerable<string> args,
        CancellationToken ct,
        Stream? stdoutTo = null,
        bool allowNonZeroExit = false,
        bool verbose = false)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = _exe,
            RedirectStandardError = true,
            RedirectStandardOutput = stdoutTo is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-nostdin");
        psi.ArgumentList.Add("-hide_banner");
        if (!verbose)
        {
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
        }
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new FfmpegException($"Could not start ffmpeg ('{_exe}'): {ex.Message}");
        }

        // Killing the process closes its pipes, which lets the reads below finish.
        using var killOnCancel = ct.Register(() => TryKill(process));

        var stderrTask = process.StandardError.ReadToEndAsync();
        var stdoutTask = stdoutTo is null
            ? Task.CompletedTask
            : process.StandardOutput.BaseStream.CopyToAsync(stdoutTo);

        await Task.WhenAll(stderrTask, stdoutTask);
        await process.WaitForExitAsync();
        ct.ThrowIfCancellationRequested();

        var stderr = stderrTask.Result;
        if (!allowNonZeroExit && process.ExitCode != 0)
            throw new FfmpegException($"ffmpeg failed (exit code {process.ExitCode}): {Tail(stderr)}");
        return stderr;
    }

    public async Task<MediaProbe> ProbeAsync(string path, CancellationToken ct)
    {
        var text = await RunAsync(new[] { "-i", path }, ct, allowNonZeroExit: true, verbose: true);

        TimeSpan? duration = null;
        var m = DurationRx.Match(text);
        if (m.Success)
        {
            var seconds = int.Parse(m.Groups["h"].Value) * 3600
                        + int.Parse(m.Groups["m"].Value) * 60
                        + double.Parse(m.Groups["s"].Value, CultureInfo.InvariantCulture);
            duration = TimeSpan.FromSeconds(seconds);
        }
        return new MediaProbe(duration, AudioStreamRx.Matches(text).Count);
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* already gone */ }
    }

    private static string Tail(string text)
    {
        text = text.Trim();
        return text.Length <= 800 ? text : "..." + text[^800..];
    }
}
