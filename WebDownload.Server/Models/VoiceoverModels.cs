namespace WebDownload.Server.Models;

public enum VoiceoverMode
{
    Mp3,
    Mp3AndEmbed
}

public record SrtFileItem(string Name, string Type, long SizeBytes, DateTime ModifiedUtc);

// RelativePath is relative to ApplicationSettings:MediaDrive, e.g. "movies/Some Show/ep1.mp4".
public record VideoFileItem(Guid Id, string FileName, string Folder, string RelativePath);

public record VoiceoverConfigDto(
    IReadOnlyList<VoiceoverVoice> Voices,
    string DefaultVoice,
    IReadOnlyList<string> Menus,
    string SrtFolder,
    string OutputFolder,
    IReadOnlyList<string> VideoExtensions);

// Everything the background job needs. Paths are already resolved and checked by the controller.
public record VoiceoverJob(
    string JobId,
    VoiceoverMode Mode,
    string SrtPath,
    string? VideoPath,
    string Voice,
    string WorkDir,
    bool MatchVoice = false,
    int RatePercent = 0,    // -25..25, speeds up or slows down every clip
    int PitchPercent = 0);  // -25..25

public record VoiceoverResult(string Mp3Path, string? VideoPath);

// Sent to the job's SignalR group as "ReceiveJobUpdate".
// Kind: "state" | "log" | "done" | "error" | "cancelled".
public record JobUpdate(
    string JobId,
    string Kind,
    string? State = null,
    int? Percent = null,
    string? Message = null,
    string? Mp3Name = null,
    string? Mp3Url = null,
    string? VideoName = null,
    string? VideoUrl = null);
