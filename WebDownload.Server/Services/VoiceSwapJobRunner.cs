using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WebDownload.Server.Hubs;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// Runs voice swap jobs in the background (the web request returns at once) and streams progress to the
/// job's SignalR group. A semaphore keeps jobs to MaxConcurrentJobs at a time; the rest wait in a queue.
/// It shares VoiceoverJobRegistry (a plain job-id to cancellation-token map) with the other pages.
/// </summary>
public sealed class VoiceSwapJobRunner
{
    private readonly VoiceSwapService _service;
    private readonly VoiceoverJobRegistry _registry;
    private readonly IHubContext<VoiceSwapHub> _hub;
    private readonly ILogger<VoiceSwapJobRunner> _logger;
    private readonly SemaphoreSlim _gate;

    public VoiceSwapJobRunner(
        VoiceSwapService service,
        VoiceoverJobRegistry registry,
        IHubContext<VoiceSwapHub> hub,
        IOptions<VoiceSwapSettings> settings,
        ILogger<VoiceSwapJobRunner> logger)
    {
        _service = service;
        _registry = registry;
        _hub = hub;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, settings.Value.MaxConcurrentJobs));
    }

    public void Start(VoiceSwapJob job, CancellationToken token) =>
        _ = Task.Run(() => RunAsync(job, token));

    private async Task RunAsync(VoiceSwapJob job, CancellationToken ct)
    {
        try
        {
            if (_gate.CurrentCount == 0)
                await SendAsync(new VoiceSwapUpdate(job.JobId, "state", State: "Waiting for another voice swap to finish", Percent: 0));

            await _gate.WaitAsync(ct);
            try
            {
                var files = await _service.RunAsync(job, SendAsync, ct);
                await SendAsync(new VoiceSwapUpdate(job.JobId, "done", State: "Done", Percent: 100, Files: files));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await SendAsync(new VoiceSwapUpdate(job.JobId, "cancelled", State: "Cancelled", Message: "Job cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Voice swap job {JobId} failed.", job.JobId);
            await SendAsync(new VoiceSwapUpdate(job.JobId, "error", State: "Failed", Message: ex.Message));
        }
        finally
        {
            _registry.Remove(job.JobId);
            try { if (Directory.Exists(job.WorkDir)) Directory.Delete(job.WorkDir, recursive: true); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete work folder {Dir}.", job.WorkDir); }
        }
    }

    private async Task SendAsync(VoiceSwapUpdate update)
    {
        try
        {
            await _hub.Clients.Group(update.JobId).SendAsync("ReceiveVoiceSwapUpdate", update);
        }
        catch (Exception ex)
        {
            // A dropped browser connection must never abort the job itself.
            _logger.LogDebug(ex, "Could not push a progress update for job {JobId}.", update.JobId);
        }
    }
}
