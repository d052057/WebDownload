using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WebDownload.Server.Hubs;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>One CancellationTokenSource per running job, so the Cancel button can stop it.</summary>
public sealed class VoiceoverJobRegistry
{
    private static readonly Regex ValidId = new(@"^[A-Za-z0-9-]{8,64}$", RegexOptions.Compiled);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobs = new();

    public static bool IsValidId(string? id) => id is not null && ValidId.IsMatch(id);

    public bool TryRegister(string jobId, out CancellationToken token)
    {
        var cts = new CancellationTokenSource();
        if (_jobs.TryAdd(jobId, cts))
        {
            token = cts.Token;
            return true;
        }
        cts.Dispose();
        token = default;
        return false;
    }

    // Returns false when there is no running job with that id (for example, nothing to cancel yet).
    public bool Cancel(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var cts)) return false;
        try { cts.Cancel(); } catch (ObjectDisposedException) { return false; }
        return true;
    }

    public void Remove(string jobId)
    {
        if (_jobs.TryRemove(jobId, out var cts)) cts.Dispose();
    }
}

/// <summary>
/// Runs jobs in the background and streams their progress to the job's SignalR group.
/// A semaphore keeps text-to-speech plus ffmpeg from running more than MaxConcurrentJobs at once.
/// </summary>
public sealed class VoiceoverJobRunner
{
    private readonly VoiceoverService _service;
    private readonly VoiceoverJobRegistry _registry;
    private readonly IHubContext<ConvertHub> _hub;
    private readonly MediaPathResolver _paths;
    private readonly ILogger<VoiceoverJobRunner> _logger;
    private readonly SemaphoreSlim _gate;

    public VoiceoverJobRunner(
        VoiceoverService service,
        VoiceoverJobRegistry registry,
        IHubContext<ConvertHub> hub,
        MediaPathResolver paths,
        IOptions<VoiceoverSettings> settings,
        ILogger<VoiceoverJobRunner> logger)
    {
        _service = service;
        _registry = registry;
        _hub = hub;
        _paths = paths;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, settings.Value.MaxConcurrentJobs));
    }

    public void Start(VoiceoverJob job, CancellationToken token) =>
        _ = Task.Run(() => RunAsync(job, token));

    private async Task RunAsync(VoiceoverJob job, CancellationToken ct)
    {
        try
        {
            if (_gate.CurrentCount == 0)
                await SendAsync(new JobUpdate(job.JobId, "state", State: "Waiting for another conversion to finish", Percent: 0));

            await _gate.WaitAsync(ct);
            try
            {
                var result = await _service.RunAsync(job, SendAsync, ct);

                await SendAsync(new JobUpdate(
                    job.JobId, "done",
                    State: "Done", Percent: 100,
                    Mp3Name: Path.GetFileName(result.Mp3Path),
                    Mp3Url: _paths.ToMediaUrl(result.Mp3Path),
                    VideoName: result.VideoPath is null ? null : Path.GetFileName(result.VideoPath),
                    VideoUrl: result.VideoPath is null ? null : _paths.ToMediaUrl(result.VideoPath)));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await SendAsync(new JobUpdate(job.JobId, "cancelled", State: "Cancelled", Message: "Conversion cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voiceover job {JobId} failed.", job.JobId);
            await SendAsync(new JobUpdate(job.JobId, "error", State: "Failed", Message: ex.Message));
        }
        finally
        {
            _registry.Remove(job.JobId);
            try { if (Directory.Exists(job.WorkDir)) Directory.Delete(job.WorkDir, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete work folder {Dir}.", job.WorkDir); }
        }
    }

    private async Task SendAsync(JobUpdate update)
    {
        try
        {
            await _hub.Clients.Group(update.JobId).SendAsync("ReceiveJobUpdate", update);
        }
        catch (Exception ex)
        {
            // A dropped browser connection must never abort the conversion itself.
            _logger.LogDebug(ex, "Could not push a progress update for job {JobId}.", update.JobId);
        }
    }
}
