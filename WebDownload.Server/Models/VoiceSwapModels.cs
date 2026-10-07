namespace WebDownload.Server.Models;

// Quality: "standard" | "high" (the Splitter's Demucs models). ModelId is a VoiceSwap:Models id.
// Pitch is in semitones; null = use the voice's DefaultPitch. MediaPath is relative to
// ApplicationSettings:MediaDrive, as returned by the tree.
public record VoiceSwapStartRequest(string JobId, string ModelId, string Quality, string MediaPath, int? Pitch);

public record VoiceSwapJob(
    string JobId, ResolvedVoiceModel Voice, int Pitch, string DemucsModel, string MediaPath, string WorkDir);

public record VoiceSwapFile(string Label, string Name, string? Url);

// Sent to the job's SignalR group as "ReceiveVoiceSwapUpdate".
// Kind: "state" | "log" | "done" | "error" | "cancelled".
// Step is 1 (split), 2 (convert voice) or 3 (mix), so the page can show which step is running.
public record VoiceSwapUpdate(
    string JobId,
    string Kind,
    string? State = null,
    int? Percent = null,
    int? Step = null,
    string? Message = null,
    IReadOnlyList<VoiceSwapFile>? Files = null);

public record VoiceModelDto(string Id, string Name, int DefaultPitch);

public record VoiceSwapConfigDto(
    IReadOnlyList<WebDownload.Server.Services.MenuItemDto> Menus,
    IReadOnlyList<SplitterQualityDto> Qualities,
    IReadOnlyList<VoiceModelDto> Models,
    string OutputFolder,
    string RpmFolder,
    string RpmMenu,
    string HubPath,
    // Problems with the setup (Applio or a model file missing), shown on the page before a job is started.
    IReadOnlyList<string> Problems);
