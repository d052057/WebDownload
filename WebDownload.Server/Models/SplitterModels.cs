namespace WebDownload.Server.Models;

public enum SplitterMode
{
    KeepVoice,
    KeepMusic,
    KeepBoth,
    VoiceCleanup
}

// Mode: "voice" | "music" | "both" | "cleanup". Quality: "standard" | "high".
// MediaPath is relative to ApplicationSettings:MediaDrive, as returned by the media list.
public record SplitterStartRequest(string JobId, string Mode, string Quality, string MediaPath);

public record SplitterJob(string JobId, SplitterMode Mode, string Model, string MediaPath, string WorkDir);

public record SplitterFile(string Label, string Name, string? Url);

// Sent to the job's SignalR group as "ReceiveSplitterUpdate".
// Kind: "state" | "log" | "done" | "error" | "cancelled".
public record SplitterUpdate(
    string JobId,
    string Kind,
    string? State = null,
    int? Percent = null,
    string? Message = null,
    IReadOnlyList<SplitterFile>? Files = null);

// Device: "cuda" or "cpu". Name is what the page shows after "Running on:".
public record DeviceInfo(string Device, string Name, string? Note);

public record SplitterQualityDto(string Id, string Label);

public record SplitterConfigDto(
    IReadOnlyList<string> Menus,
    IReadOnlyList<SplitterQualityDto> Qualities,
    string OutputFolder);
