using Microsoft.AspNetCore.SignalR;
using WebDownload.Server.Services;

namespace WebDownload.Server.Hubs;

/// <summary>
/// Progress channel for Voiceover jobs. The browser picks a job id, joins that group, THEN
/// starts the job over HTTP, so it can't miss early messages. The server pushes
/// "ReceiveJobUpdate" to the group (see VoiceoverJobRunner). Starting the work itself is
/// not a hub call: a long conversion must not be tied to one hub invocation.
/// </summary>
public class ConvertHub : Hub
{
    private readonly VoiceoverJobRegistry _jobs;

    public ConvertHub(VoiceoverJobRegistry jobs) => _jobs = jobs;

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
