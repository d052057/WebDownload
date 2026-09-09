using System.Collections.Concurrent;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services
{
    public interface ITranslationJobTracker
    {
        void SetStatus(string groupId, TranslationJobStatus status);
        TranslationJobStatus? GetStatus(string groupId);
    }

    // Registered as a singleton (see Program.cs) so status survives across
    // hub method calls and even across a page reload within the same app
    // run - as long as the client still knows its downloadGroupId, it can
    // ask "is my translation done yet?" at any time via GetTranslationStatus,
    // independent of whether the live SignalR push actually reached it.
    //
    // NOTE: this is in-memory only - it resets if the server process
    // restarts (deploy, crash, IIS recycle). Good enough for a single-server
    // personal tool; a real multi-instance deployment would need this in a
    // shared store (e.g. a database or distributed cache) instead.
    public class TranslationJobTracker : ITranslationJobTracker
    {
        private readonly ConcurrentDictionary<string, TranslationJobStatus> _jobs = new();

        public void SetStatus(string groupId, TranslationJobStatus status)
        {
            _jobs[groupId] = status;
        }

        public TranslationJobStatus? GetStatus(string groupId)
        {
            return _jobs.TryGetValue(groupId, out var status) ? status : null;
        }
    }
}
