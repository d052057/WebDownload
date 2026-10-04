using Microsoft.AspNetCore.SignalR;
using WebDownload.Server.Services;

namespace WebDownload.Server.Hubs;

/// <summary>
/// Progress channel for Splitter jobs. The page picks a job id, joins that group, THEN starts the job
/// over HTTP, so it can't miss early messages. The server pushes "ReceiveSplitterUpdate" to the group.
/// </summary>
public class SplitterHub : Hub
{
    private readonly VoiceoverJobRegistry _jobs;

    public SplitterHub(VoiceoverJobRegistry jobs) => _jobs = jobs;

    public Task JoinGroup(string jobId)
    {
        Require(jobId);
        return Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    }

    public Task LeaveGroup(string jobId)
    {
        Require(jobId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
    }

    // True when a running job was found and told to stop.
    public bool CancelJob(string jobId)
    {
        Require(jobId);
        return _jobs.Cancel(jobId);
    }

    private static void Require(string jobId)
    {
        if (!VoiceoverJobRegistry.IsValidId(jobId))
            throw new HubException("Invalid job id.");
    }
}
