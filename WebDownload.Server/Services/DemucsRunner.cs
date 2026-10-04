using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

public sealed class DemucsException : Exception
{
    public DemucsException(string message) : base(message) { }
}

public static class ToolPaths
{
    // A rooted path is used as given. A bare name is looked up next to the app first, then left for PATH.
    public static string Resolve(string? configured)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? "demucs.exe" : configured.Trim();
        if (Path.IsPathRooted(value)) return value;
        var local = Path.Combine(AppContext.BaseDirectory, value);
        return File.Exists(local) ? local : value;
    }
}

/// <summary>
/// Runs Demucs as an outside program (like yt-dlp.exe and ffmpeg.exe), turns its progress bar into
/// percentages, and kills it when the token is cancelled.
/// </summary>
public sealed class DemucsRunner
{
    // tqdm prints e.g. "100%|██████| 380.25/380.25 [04:38<00:00, ...]" with carriage returns, not newlines.
    private static readonly Regex Percent = new(@"(\d{1,3})%\|", RegexOptions.Compiled);
    private static readonly Regex Bag = new(@"bag of (\d+) models", RegexOptions.Compiled);

    private readonly SplitterSettings _s;

    public DemucsRunner(IOptions<SplitterSettings> settings) => _s = settings.Value;

    // Python must print UTF-8 even though its output is redirected: with the default Windows code page
    // it crashes on Khmer characters and on the progress bar's block characters.
    public static void ApplyEnvironment(ProcessStartInfo psi, SplitterSettings s)
    {
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        if (!string.IsNullOrWhiteSpace(s.ModelCacheFolder))
        {
            psi.Environment["HF_HOME"] = s.ModelCacheFolder;
            psi.Environment["TORCH_HOME"] = s.ModelCacheFolder;
        }
    }

    /// <summary>
    /// Separates inputWav into "vocals" and "no_vocals". Output lands in
    /// outDir\&lt;model&gt;\vocals.wav and outDir\&lt;model&gt;\no_vocals.wav.
    /// </summary>
    /// <param name="onPercent">Overall progress 0-100, including the several passes of a multi-model bag.</param>
    public async Task RunAsync(
        string inputWav, string outDir, string model, string device,
        Func<int, Task> onPercent, Func<string, Task> onLog, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = ToolPaths.Resolve(_s.DemucsExecutablePath),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false)
        };
        foreach (var a in _s.GetLeadingArgs()) psi.ArgumentList.Add(a);
        foreach (var a in new[]
        {
            "-n", model,
            "--two-stems", "vocals",
            "-d", device,
            "--filename", "{stem}.{ext}",   // flat output: <model>\vocals.wav, no per-track folder
            "-o", outDir,
            inputWav
        }) psi.ArgumentList.Add(a);
        ApplyEnvironment(psi, _s);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new DemucsException($"Could not start Demucs ('{psi.FileName}'): {ex.Message}");
        }
        using var killOnCancel = ct.Register(() => TryKill(process));

        var bag = 1;
        var completed = 0;
        var lastPct = 0;
        var lastOverall = -1;
        var logged = 0;
        var tail = new Queue<string>();

        async Task HandleAsync(string segment)
        {
            segment = segment.Trim();
            if (segment.Length == 0) return;

            var pm = Percent.Match(segment);
            if (pm.Success)
            {
                var pct = Math.Clamp(int.Parse(pm.Groups[1].Value), 0, 100);
                if (pct + 40 < lastPct) completed++;      // a new bar started: a bag of several models
                lastPct = pct;
                var overall = (int)Math.Min(100, (completed * 100.0 + pct) / bag);
                if (overall != lastOverall)
                {
                    lastOverall = overall;
                    await onPercent(overall);
                }
                return;
            }

            var bm = Bag.Match(segment);
            if (bm.Success) bag = Math.Max(1, int.Parse(bm.Groups[1].Value));

            tail.Enqueue(segment);
            if (tail.Count > 12) tail.Dequeue();

            if (segment.Contains("HF Hub", StringComparison.OrdinalIgnoreCase)) return; // harmless token warning
            if (logged++ < 8) await onLog(segment);
        }

        async Task ReadProgressAsync(StreamReader reader)
        {
            var sb = new StringBuilder();
            var buffer = new char[1024];
            int n;
            while ((n = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < n; i++)
                {
                    var c = buffer[i];
                    if (c is '\r' or '\n')
                    {
                        if (sb.Length > 0) { await HandleAsync(sb.ToString()); sb.Clear(); }
                    }
                    else sb.Append(c);
                }
            }
            if (sb.Length > 0) await HandleAsync(sb.ToString());
        }

        // Killing the process closes its pipes, which lets both reads finish.
        await Task.WhenAll(ReadProgressAsync(process.StandardError), process.StandardOutput.ReadToEndAsync());
        await process.WaitForExitAsync();
        ct.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
            throw new DemucsException($"Demucs failed (exit code {process.ExitCode}): {string.Join(" | ", tail)}");
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* already gone */ }
    }
}
