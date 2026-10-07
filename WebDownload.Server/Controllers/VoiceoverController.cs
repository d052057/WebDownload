using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;
using WebDownload.Server.Services;

namespace WebDownload.Server.Controllers;

public class VoiceoverConvertForm
{
    public string JobId { get; set; } = "";
    public string Mode { get; set; } = "";          // "mp3" | "mp3-embed"
    public string? Voice { get; set; }

    // MatchVoice: detect male/female and pitch from the video instead of using Voice and the sliders.
    public bool MatchVoice { get; set; }
    public int RatePercent { get; set; }    // -MaxAdjustPercent..MaxAdjustPercent
    public int PitchPercent { get; set; }   // -MaxAdjustPercent..MaxAdjustPercent

    // Exactly one subtitle source: a file from the folder list OR an upload.
    public string? SrtServerName { get; set; }
    public IFormFile? SrtFile { get; set; }

    // Needed for "mp3-embed" and whenever MatchVoice is on: exactly one video source.
    // VideoServerPath is relative to the media drive.
    public string? VideoServerPath { get; set; }
    public IFormFile? VideoFile { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class VoiceoverController : ControllerBase
{
    private readonly HashSet<string> SubtitleExtensions;

    private readonly VoiceoverSettings _s;
    private readonly MenuService _menus;
    private readonly MediaBrowseService _browse;
    private readonly MediaTreeService _tree;
    private readonly MediaPathResolver _paths;
    private readonly VoiceoverJobRegistry _registry;
    private readonly VoiceoverJobRunner _runner;
    private readonly ILogger<VoiceoverController> _logger;

    public VoiceoverController(
        IOptions<VoiceoverSettings> settings,
        MenuService menus,
        MediaBrowseService browse,
        MediaTreeService tree,
        MediaPathResolver paths,
        VoiceoverJobRegistry registry,
        VoiceoverJobRunner runner,
        ILogger<VoiceoverController> logger)
    {
        _s = settings.Value;
        _menus = menus;
        SubtitleExtensions = new HashSet<string>(_s.GetSubtitleExtensions(), StringComparer.OrdinalIgnoreCase);
        _browse = browse;
        _tree = tree;
        _paths = paths;
        _registry = registry;
        _runner = runner;
        _logger = logger;
    }

    // GET api/voiceover/config
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(new VoiceoverConfigDto(
        _s.GetVoices(),
        _s.DefaultVoice,
        await _menus.GetForFilesAsync(_s.GetVideoExtensions(), _s.Menus, extra: null, ct),
        _s.SrtFolder.Replace('\\', '/'),
        _s.OutputFolder.Replace('\\', '/'),
        _s.GetVideoExtensions(),
        _s.GetSubtitleExtensions(),
        _s.MaxAdjustPercent,
        _s.MatchVoiceByDefault));

    // GET api/voiceover/srt
    [HttpGet("srt")]
    public IActionResult GetSrtFiles()
    {
        try
        {
            return Ok(_browse.GetSrtFiles());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to list subtitle files in {Folder}", _s.SrtFolder);
            return StatusCode(StatusCodes.Status500InternalServerError, "Unable to read the subtitle folder.");
        }
    }

    // GET api/voiceover/mp4?menu=movies
    [HttpGet("mp4")]
    public async Task<IActionResult> GetVideoFiles([FromQuery] string menu, CancellationToken ct)
    {
        try
        {
            return Ok(await _browse.GetVideosAsync(menu, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // GET api/voiceover/tree?menu=movies
    // The same videos as /mp4, as a folder tree for the folder-node component (folder names and file lists).
    // mediaPath-style relative paths come back in each track's url.
    [HttpGet("tree")]
    public async Task<IActionResult> GetVideoTree([FromQuery] string menu, CancellationToken ct)
    {
        try
        {
            return Ok(await _tree.GetMediaTreeAsync(menu, _s.Menus, _s.GetVideoExtensions(), ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // POST api/voiceover/convert  (multipart/form-data)
    // Validates and stores everything the job needs, then starts it in the background and
    // returns 202. Progress arrives over SignalR (ConvertHub) in the group named by jobId.
    [HttpPost("convert")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> StartConversion([FromForm] VoiceoverConvertForm form, CancellationToken ct)
    {
        if (!VoiceoverJobRegistry.IsValidId(form.JobId))
            return BadRequest("Invalid job id.");

        VoiceoverMode mode;
        switch (form.Mode)
        {
            case "mp3": mode = VoiceoverMode.Mp3; break;
            case "mp3-embed": mode = VoiceoverMode.Mp3AndEmbed; break;
            default: return BadRequest("Mode must be 'mp3' or 'mp3-embed'.");
        }

        var voice = string.IsNullOrWhiteSpace(form.Voice) ? _s.DefaultVoice : form.Voice;
        if (!_s.GetVoices().Any(v => v.Id == voice))
            return BadRequest("That voice is not available.");

        var hasSrtUpload = form.SrtFile is { Length: > 0 };
        var hasSrtServer = !string.IsNullOrWhiteSpace(form.SrtServerName);
        if (hasSrtUpload == hasSrtServer)
            return BadRequest("Choose one subtitle source: a file from the list, or an uploaded file.");

        var needVideo = mode == VoiceoverMode.Mp3AndEmbed || form.MatchVoice;
        var hasVideoUpload = form.VideoFile is { Length: > 0 };
        var hasVideoServer = !string.IsNullOrWhiteSpace(form.VideoServerPath);
        if (needVideo && hasVideoUpload == hasVideoServer)
            return BadRequest(mode == VoiceoverMode.Mp3AndEmbed
                ? "Choose one video source: a file from the list, or an uploaded file."
                : "Voice matching needs a video: choose one, or turn voice matching off.");

        var videoExtensions = new HashSet<string>(_s.GetVideoExtensions(), StringComparer.OrdinalIgnoreCase);

        // Files already on the server: resolve and check them before touching any upload.
        string? srtPath = null;
        string? videoPath = null;
        try
        {
            if (hasSrtServer) srtPath = ResolveServerSubtitle(form.SrtServerName!);
            if (needVideo && hasVideoServer) videoPath = ResolveServerVideo(form.VideoServerPath!, videoExtensions);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (FileNotFoundException ex) { return NotFound(ex.Message); }

        // Uploads: IFormFile streams die with the request, so they are copied to the job's work
        // folder now; the background job deletes that folder when it finishes.
        var workDir = Path.Combine(_s.GetTempRoot(), form.JobId);
        try
        {
            Directory.CreateDirectory(workDir);
            if (hasSrtUpload)
                srtPath = await SaveUploadAsync(form.SrtFile!, workDir, SubtitleExtensions, ct);
            if (needVideo && hasVideoUpload)
                videoPath = await SaveUploadAsync(form.VideoFile!, workDir, videoExtensions, ct);
        }
        catch (ArgumentException ex)
        {
            DeleteWorkDir(workDir);
            return BadRequest(ex.Message);
        }
        catch (OperationCanceledException)
        {
            DeleteWorkDir(workDir);
            return StatusCode(499); // the browser aborted the upload
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Could not store an upload for job {JobId}", form.JobId);
            DeleteWorkDir(workDir);
            return StatusCode(StatusCodes.Status500InternalServerError, "Could not store the uploaded file.");
        }

        if (!_registry.TryRegister(form.JobId, out var token))
        {
            DeleteWorkDir(workDir);
            return Conflict("That job id is already in use.");
        }

        _logger.LogInformation(
            "Voiceover job {JobId}: mode {Mode}, match voice {MatchVoice}, video {Video}",
            form.JobId, mode, form.MatchVoice, videoPath is null ? "none" : Path.GetFileName(videoPath));

        _runner.Start(new VoiceoverJob(
            form.JobId, mode, srtPath!, videoPath, voice, workDir,
            form.MatchVoice,
            Math.Clamp(form.RatePercent, -_s.MaxAdjustPercent, _s.MaxAdjustPercent),
            Math.Clamp(form.PitchPercent, -_s.MaxAdjustPercent, _s.MaxAdjustPercent)), token);
        return Accepted(new { jobId = form.JobId });
    }

    // POST api/voiceover/jobs/{jobId}/cancel  (the hub's CancelJob does the same thing)
    [HttpPost("jobs/{jobId}/cancel")]
    public IActionResult CancelJob(string jobId) =>
        VoiceoverJobRegistry.IsValidId(jobId) && _registry.Cancel(jobId) ? Ok() : NotFound();

    private string ResolveServerSubtitle(string name)
    {
        // The subtitle folder is flat, so only a bare file name is accepted.
        var safe = Path.GetFileName(name);
        if (!SubtitleExtensions.Contains(Path.GetExtension(safe)))
            throw new ArgumentException("Only " + string.Join(", ", SubtitleExtensions) + " files are supported.");
        var full = _paths.ResolveUnder(_s.SrtFolder, safe);
        if (!System.IO.File.Exists(full))
            throw new FileNotFoundException($"'{safe}' was not found in the subtitle folder.");
        return full;
    }

    private string ResolveServerVideo(string relativePath, HashSet<string> allowedExtensions)
    {
        if (!allowedExtensions.Contains(Path.GetExtension(relativePath)))
            throw new ArgumentException("That is not a supported video file type.");

        var full = _paths.ResolveUnderMedia(relativePath);
        if (!System.IO.File.Exists(full))
            throw new FileNotFoundException("That video file was not found on the server.");
        return full;
    }

    private static async Task<string> SaveUploadAsync(
        IFormFile file, string workDir, HashSet<string> allowedExtensions, CancellationToken ct)
    {
        var name = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(name);
        if (string.IsNullOrWhiteSpace(name) || !allowedExtensions.Contains(ext))
            throw new ArgumentException($"Unsupported file type '{ext}'.");

        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');

        var dest = Path.Combine(workDir, name);
        await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        await file.CopyToAsync(fs, ct);
        return dest;
    }

    private static void DeleteWorkDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }
}
