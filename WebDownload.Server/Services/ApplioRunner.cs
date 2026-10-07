using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

public sealed class ApplioException : Exception
{
    public ApplioException(string message) : base(message) { }
}

/// <summary>
/// Runs Applio's command line ("core.py infer") as an outside process, the way DemucsRunner runs Demucs:
/// the AI never runs inside the web app, so IIS only has to start a program and read its output.
///
/// Written against Applio's click command line (infer --input-path ... --pth-path ... --index-path ...).
/// Progress comes from the two lines Applio prints when --split-audio is on:
///   "Audio split into N chunks for processing."  and  "Converted audio chunk K".
/// If a later Applio version words them differently the job still works; the percentage just stays at the
/// start of its range until the file is finished.
/// </summary>
public sealed class ApplioRunner
{
    private static readonly Regex Total = new(@"Audio split into (\d+) chunks", RegexOptions.Compiled);
    private static readonly Regex Done = new(@"Converted audio chunk (\d+)", RegexOptions.Compiled);
    private const int MaxLoggedLines = 25;

    private readonly VoiceSwapSettings _s;

    public ApplioRunner(IOptions<VoiceSwapSettings> settings) => _s = settings.Value;

    /// <summary>What is wrong with the setup, in plain words. Empty = ready.</summary>
    public IReadOnlyList<string> CheckSetup()
    {
        var problems = new List<string>();

        if (!File.Exists(_s.GetPython()))
            problems.Add($"Applio's Python was not found: {_s.GetPython()} (VoiceSwap:ApplioFolder / PythonExecutablePath).");
        if (!File.Exists(Path.Combine(_s.ApplioFolder, _s.ScriptName)))
            problems.Add($"Applio's {_s.ScriptName} was not found in {_s.ApplioFolder} (VoiceSwap:ApplioFolder).");

        var models = _s.GetModels();
        if (models.Count == 0)
            problems.Add("No voice is configured (VoiceSwap:Models needs at least one PthPath).");
        foreach (var m in models)
        {
            if (!File.Exists(m.PthPath)) problems.Add($"Voice '{m.Name}': the .pth file was not found: {m.PthPath}");
            if (string.IsNullOrWhiteSpace(m.IndexPath)) problems.Add($"Voice '{m.Name}': IndexPath is empty.");
            else if (!File.Exists(m.IndexPath)) problems.Add($"Voice '{m.Name}': the .index file was not found: {m.IndexPath}");
        }
        return problems;
    }

    // Applio rewrites "trained" to "added" ANYWHERE in the index path (it expects its own file names),
    // so an index in a folder or file with that word in its name would silently not be found and the
    // voice would be converted without the index. Such a file is copied to a safe name first.
    public static string PrepareIndex(string indexPath, string workDir, out string? note)
    {
        note = null;
        if (!indexPath.Contains("trained", StringComparison.Ordinal)) return indexPath;

        Directory.CreateDirectory(workDir);
        var copy = Path.Combine(workDir, "voice.index");
        File.Copy(indexPath, copy, overwrite: true);
        note = "The index path contains the word \"trained\", which Applio rewrites, so a copy is used.";
        return copy;
    }

    public IReadOnlyList<string> BuildArguments(
        string inputWav, string outputWav, string pthPath, string indexPath, int pitch)
    {
        string N(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        var args = new List<string>
        {
            _s.ScriptName, "infer",
            "--input-path", inputWav,
            "--output-path", outputWav,
            "--pth-path", pthPath,
            "--index-path", indexPath,
            "--pitch", pitch.ToString(CultureInfo.InvariantCulture),
            "--index-rate", N(_s.IndexRate),
            "--volume-envelope", N(_s.VolumeEnvelope),
            "--protect", N(_s.Protect),
            "--f0-method", _s.F0Method,
            "--embedder-model", _s.EmbedderModel,
            "--sid", _s.SpeakerId.ToString(CultureInfo.InvariantCulture),
            "--export-format", "WAV"
        };
        if (_s.SplitAudio) args.Add("--split-audio");
        if (_s.CleanAudio) { args.Add("--clean-audio"); args.Add("--clean-strength"); args.Add(N(_s.CleanStrength)); }
        if (_s.F0Autotune) { args.Add("--f0-autotune"); args.Add("--f0-autotune-strength"); args.Add(N(_s.F0AutotuneStrength)); }
        args.AddRange(_s.GetExtraArgs());
        return args;
    }

    /// <summary>Converts inputWav to the voice of the model; the result is written to outputWav.</summary>
    /// <param name="useGpu">false hides the graphics card from the process, so it runs on the CPU.</param>
    /// <param name="onPercent">0-100 for this step only.</param>
    public async Task ConvertAsync(
        string inputWav, string outputWav, string pthPath, string indexPath, int pitch, bool useGpu,
        Func<int, Task> onPercent, Func<string, Task> onLog, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (File.Exists(outputWav)) File.Delete(outputWav);

        var psi = new ProcessStartInfo
        {
            FileName = _s.GetPython(),
            WorkingDirectory = _s.ApplioFolder,   // core.py reads files by relative path
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false)
        };
        foreach (var a in BuildArguments(inputWav, outputWav, pthPath, indexPath, pitch)) psi.ArgumentList.Add(a);

        // UNBUFFERED matters: Python buffers redirected output in blocks, which would hold every progress
        // line back until the end. UTF-8 stops it crashing on non-ASCII names in its own messages.
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONUNBUFFERED"] = "1";
        if (!useGpu) psi.Environment["CUDA_VISIBLE_DEVICES"] = "-1";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_s.InferenceTimeoutMinutes > 0) timeout.CancelAfter(TimeSpan.FromMinutes(_s.InferenceTimeoutMinutes));

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new ApplioException($"Could not start Applio ('{psi.FileName}'): {ex.Message}");
        }
        using var killOnCancel = timeout.Token.Register(() => TryKill(process));

        var total = 0;
        var logged = 0;
        var lastPct = -1;
        var tail = new Queue<string>();

        async Task HandleAsync(string line)
        {
            line = line.Trim();
            if (line.Length == 0) return;

            tail.Enqueue(line);
            if (tail.Count > 12) tail.Dequeue();

            var tm = Total.Match(line);
            if (tm.Success) total = Math.Max(1, int.Parse(tm.Groups[1].Value));

            var dm = Done.Match(line);
            if (dm.Success && total > 0)
            {
                var pct = Math.Clamp((int)(int.Parse(dm.Groups[1].Value) * 100.0 / total), 0, 100);
                if (pct != lastPct) { lastPct = pct; await onPercent(pct); }
                return;
            }
            if (logged++ < MaxLoggedLines) await onLog(line);
        }

        async Task ReadLinesAsync(StreamReader reader)
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
        await Task.WhenAll(ReadLinesAsync(process.StandardOutput), ReadLinesAsync(process.StandardError));
        await process.WaitForExitAsync();

        ct.ThrowIfCancellationRequested();   // the user pressed Cancel
        if (timeout.IsCancellationRequested)
            throw new ApplioException($"Applio was stopped after {_s.InferenceTimeoutMinutes} minutes (VoiceSwap:InferenceTimeoutMinutes).");

        // Applio can also end normally without a result (for example when it cannot load the model),
        // so the output file is the real proof of success.
        if (process.ExitCode != 0 || !File.Exists(outputWav))
            throw new ApplioException(
                $"Applio failed (exit code {process.ExitCode}): {string.Join(" | ", tail)}");
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* already gone */ }
    }
}
