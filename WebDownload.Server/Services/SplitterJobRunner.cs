using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WebDownload.Server.Hubs;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// Runs Splitter jobs in the background and streams progress to the job's SignalR group. Demucs is heavy,
/// so a semaphore keeps jobs to MaxConcurrentJobs at a time; the rest wait in a queue. It shares
/// VoiceoverJobRegistry (a plain job-id to cancellation-token map) with the Voiceover page.
/// </summary>
public sealed class SplitterJobRunner
{
    private readonly SplitterService _service;
    private readonly VoiceoverJobRegistry _registry;
    private readonly IHubContext<SplitterHub> _hub;
    private readonly ILogger<SplitterJobRunner> _logger;
    private readonly SemaphoreSlim _gate;

    public SplitterJobRunner(
        SplitterService service,
        VoiceoverJobRegistry registry,
        IHubContext<SplitterHub> hub,
        IOptions<SplitterSettings> settings,
        ILogger<SplitterJobRunner> logger)
    {
        _service = service;
        _registry = registry;
        _hub = hub;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, settings.Value.MaxConcurrentJobs));
    }

    public void Start(SplitterJob job, CancellationToken token) =>
        _ = Task.Run(() => RunAsync(job, token));

    private async Task RunAsync(SplitterJob job, CancellationToken ct)
    {
        try
        {
            if (_gate.CurrentCount == 0)
                await SendAsync(new SplitterUpdate(job.JobId, "state", State: "Waiting for another job to finish", Percent: 0));

            await _gate.WaitAsync(ct);
            try
            {
                var files = await _service.RunAsync(job, SendAsync, ct);
                await SendAsync(new SplitterUpdate(job.JobId, "done", State: "Done", Percent: 100, Files: files));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await SendAsync(new SplitterUpdate(job.JobId, "cancelled", State: "Cancelled", Message: "Job cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Splitter job {JobId} failed.", job.JobId);
            await SendAsync(new SplitterUpdate(job.JobId, "error", State: "Failed", Message: ex.Message));
        }
        finally
        {
            _registry.Remove(job.JobId);
            try { if (Directory.Exists(job.WorkDir)) Directory.Delete(job.WorkDir, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete work folder {Dir}.", job.WorkDir); }
        }
    }

    private async Task SendAsync(SplitterUpdate update)
    {
        try
        {
            await _hub.Clients.Group(update.JobId).SendAsync("ReceiveSplitterUpdate", update);
        }
        catch (Exception ex)
        {
            // A dropped browser connection must never abort the job itself.
            _logger.LogDebug(ex, "Could not push a progress update for job {JobId}.", update.JobId);
        }
    }
}
