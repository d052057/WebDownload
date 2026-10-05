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
    string OutputFolder,
    string RpmFolder);

// The folder tree the page shows. These serialize to exactly the shape of the Angular
// MediaFolderTreeDto / MediaTrackDto (media-folder-tree.model.ts), so the existing
// folder-node component can render them as they are.
public sealed class SplitterTrackDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = "";
    public string DisplayTitle { get; init; } = "";
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public int? TrackNumber { get; init; }
    public string? Duration { get; init; }
    public string? Type { get; init; }
    // Relative to the media drive; this is also what the start request sends back as mediaPath.
    public string Url { get; init; } = "";
    public List<object> Subtitles { get; init; } = new();
}

public sealed class SplitterFolderDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? CoverImagePath { get; init; }
    public List<SplitterFolderDto> Folders { get; init; } = new();
    public List<SplitterTrackDto> Tracks { get; init; } = new();
}

public record SplitterTreeDto(
    string Menu,
    int FileCount,
    List<SplitterFolderDto> Folders,
    List<SplitterTrackDto> Tracks);
