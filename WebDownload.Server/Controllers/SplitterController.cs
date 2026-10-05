using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;
using WebDownload.Server.Services;

namespace WebDownload.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SplitterController : ControllerBase
{
    private readonly SplitterSettings _s;
    private readonly MediaTreeService _tree;
    private readonly MediaPathResolver _paths;
    private readonly DeviceDetector _device;
    private readonly VoiceoverJobRegistry _registry;
    private readonly SplitterJobRunner _runner;
    private readonly ILogger<SplitterController> _logger;

    public SplitterController(
        IOptions<SplitterSettings> settings,
        MediaTreeService tree,
        MediaPathResolver paths,
        DeviceDetector device,
        VoiceoverJobRegistry registry,
        SplitterJobRunner runner,
        ILogger<SplitterController> logger)
    {
        _s = settings.Value;
        _tree = tree;
        _paths = paths;
        _device = device;
        _registry = registry;
        _runner = runner;
        _logger = logger;
    }

    // GET api/splitter/config
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(new SplitterConfigDto(
        _s.GetMenus(),
        new[]
        {
            new SplitterQualityDto("standard", "Standard (faster)"),
            new SplitterQualityDto("high", "High (about 4 times slower)")
        },
        _s.OutputFolder.Replace('\\', '/'),
        (_s.RpmFolder ?? "").Replace('\\', '/').Trim('/')));

    // GET api/splitter/hardware?refresh=true
    // The first call can take a few seconds (PyTorch is loaded to ask it); the answer is then cached.
    [HttpGet("hardware")]
    public async Task<IActionResult> GetHardware([FromQuery] bool refresh, CancellationToken ct) =>
        Ok(await _device.GetAsync(refresh, ct));

    // GET api/splitter/tree?menu=movies   (menu: movies | videos | rpm)
    // Folder names and file lists only, shaped for the folder-node component.
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] string menu, CancellationToken ct)
    {
        try
        {
            return Ok(await _tree.GetTreeAsync(menu, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // POST api/splitter/jobs  { jobId, mode, quality, mediaPath }
    // Validates, then starts the job in the background and returns 202. Progress arrives over SignalR
    // (SplitterHub) in the group named by jobId. mediaPath is the track's url from the tree.
    [HttpPost("jobs")]
    public IActionResult StartJob([FromBody] SplitterStartRequest request)
    {
        if (request is null || !VoiceoverJobRegistry.IsValidId(request.JobId))
            return BadRequest("Invalid job id.");

        SplitterMode mode;
        switch (request.Mode)
        {
            case "voice": mode = SplitterMode.KeepVoice; break;
            case "music": mode = SplitterMode.KeepMusic; break;
            case "both": mode = SplitterMode.KeepBoth; break;
            case "cleanup": mode = SplitterMode.VoiceCleanup; break;
            default: return BadRequest("Mode must be 'voice', 'music', 'both' or 'cleanup'.");
        }

        string model;
        switch (request.Quality)
        {
            case "standard": model = _s.StandardModel; break;
            case "high": model = _s.HighQualityModel; break;
            default: return BadRequest("Quality must be 'standard' or 'high'.");
        }

        string mediaPath;
        try
        {
            mediaPath = _paths.ResolveUnderMedia(request.MediaPath);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        if (!_s.GetMediaExtensions().Contains(Path.GetExtension(mediaPath), StringComparer.OrdinalIgnoreCase))
            return BadRequest("That file type is not supported.");
        if (!System.IO.File.Exists(mediaPath))
            return NotFound("That file was not found on the server.");

        if (!_registry.TryRegister(request.JobId, out var token))
            return Conflict("That job id is already in use.");

        _logger.LogInformation("Splitter job {JobId}: {Mode}, model {Model}, {File}",
            request.JobId, mode, model, Path.GetFileName(mediaPath));

        var workDir = Path.Combine(_s.GetTempRoot(), request.JobId);
        _runner.Start(new SplitterJob(request.JobId, mode, model, mediaPath, workDir), token);
        return Accepted(new { jobId = request.JobId });
    }

    // POST api/splitter/jobs/{jobId}/cancel  (the hub's CancelJob does the same thing)
    [HttpPost("jobs/{jobId}/cancel")]
    public IActionResult CancelJob(string jobId) =>
        VoiceoverJobRegistry.IsValidId(jobId) && _registry.Cancel(jobId) ? Ok() : NotFound();
}
