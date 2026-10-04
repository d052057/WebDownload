using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// Finds out whether Demucs can really use a GPU, by asking PyTorch (the library Demucs runs on) in the
/// Python that sits next to the Demucs launcher. A card being installed is not enough: with a CPU-only
/// PyTorch, or a card too old for it, Demucs would silently fall back to the CPU. The answer is cached;
/// ask with refresh = true to check again after the hardware or the PyTorch install changed.
/// </summary>
public sealed class DeviceDetector
{
    // Runs a tiny GPU operation, because is_available() can be true on a card whose kernels PyTorch
    // doesn't include, which only fails later.
    private const string Script = """
import torch
ok = False
name = ""
if torch.cuda.is_available():
    try:
        x = torch.zeros(1, device="cuda") + 1
        torch.cuda.synchronize()
        ok = True
        name = torch.cuda.get_device_name(0)
    except Exception:
        ok = False
print("cuda" if ok else "cpu")
print(name)
""";

    private readonly SplitterSettings _s;
    private readonly ILogger<DeviceDetector> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DeviceInfo? _cached;

    public DeviceDetector(IOptions<SplitterSettings> settings, ILogger<DeviceDetector> logger)
    {
        _s = settings.Value;
        _logger = logger;
    }

    public async Task<DeviceInfo> GetAsync(bool refresh, CancellationToken ct)
    {
        if (!refresh && _cached is not null) return _cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (!refresh && _cached is not null) return _cached;
            _cached = await DetectAsync(ct);
            _logger.LogInformation("Splitter hardware check: {Device} ({Name})", _cached.Device, _cached.Name);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DeviceInfo> DetectAsync(CancellationToken ct)
    {
        var python = FindPython();
        if (python is null)
            return new DeviceInfo("cpu", "CPU",
                "The Python that runs Demucs was not found next to the Demucs program, so the graphics card was not checked.");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"splitter_hw_{Guid.NewGuid():N}.py");
        try
        {
            await File.WriteAllTextAsync(scriptPath, Script, new UTF8Encoding(false), ct);

            var psi = new ProcessStartInfo
            {
                FileName = python,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(scriptPath);
            DemucsRunner.ApplyEnvironment(psi, _s);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("The hardware check could not be started.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _s.HardwareCheckTimeoutSeconds)));
            using var killOnTimeout = timeout.Token.Register(() => TryKill(process));

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdout, stderr);
            await process.WaitForExitAsync();
            ct.ThrowIfCancellationRequested();

            if (timeout.IsCancellationRequested)
                return new DeviceInfo("cpu", "CPU", "The hardware check took too long, so the CPU is used.");
            if (process.ExitCode != 0)
                return new DeviceInfo("cpu", "CPU", $"The hardware check failed, so the CPU is used. {Tail(stderr.Result)}");

            var lines = stdout.Result.Split('\n', StringSplitOptions.TrimEntries);
            if (lines.Length > 0 && lines[0] == "cuda")
            {
                var name = lines.Length > 1 && lines[1].Length > 0 ? lines[1] : "NVIDIA GPU";
                return new DeviceInfo("cuda", name, null);
            }
            return new DeviceInfo("cpu", "CPU", "No usable graphics card was found, so Demucs runs on the CPU (slower).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Splitter hardware check failed.");
            return new DeviceInfo("cpu", "CPU", $"The hardware check failed, so the CPU is used. {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { /* temp file */ }
        }
    }

    // The launcher (demucs.exe) lives in the Python environment's Scripts folder, next to python.exe.
    private string? FindPython()
    {
        var exe = ToolPaths.Resolve(_s.DemucsExecutablePath);
        var name = Path.GetFileNameWithoutExtension(exe);
        if (name.StartsWith("python", StringComparison.OrdinalIgnoreCase)) return exe;

        var dir = Path.GetDirectoryName(exe);
        if (!string.IsNullOrEmpty(dir))
        {
            var sibling = Path.Combine(dir, "python.exe");
            if (File.Exists(sibling)) return sibling;
        }
        return null;
    }

    private static void TryKill(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* already gone */ }
    }

    private static string Tail(string text)
    {
        text = text.Trim();
        return text.Length <= 300 ? text : "..." + text[^300..];
    }
}
