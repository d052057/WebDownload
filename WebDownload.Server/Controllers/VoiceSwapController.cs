using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;
using WebDownload.Server.Services;

namespace WebDownload.Server.Controllers;

/// <summary>
/// The voice swap page's API. The song picker is the Splitter's (same menus, extensions and rpm folder),
/// so those come from the "Splitter" settings; everything about the voice conversion is in "VoiceSwap".
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VoiceSwapController : ControllerBase
{
    private readonly VoiceSwapSettings _s;
    private readonly SplitterSettings _splitter;
    private readonly ApplicationSettings _app;
    private readonly MenuService _menus;
    private readonly MediaTreeService _tree;
    private readonly MediaPathResolver _paths;
    private readonly ApplioRunner _applio;
    private readonly VoiceoverJobRegistry _registry;
    private readonly VoiceSwapJobRunner _runner;
    private readonly ILogger<VoiceSwapController> _logger;

    public VoiceSwapController(
        IOptions<VoiceSwapSettings> settings,
        IOptions<SplitterSettings> splitter,
        IOptions<ApplicationSettings> app,
        MenuService menus,
        MediaTreeService tree,
        MediaPathResolver paths,
        ApplioRunner applio,
        VoiceoverJobRegistry registry,
        VoiceSwapJobRunner runner,
        ILogger<VoiceSwapController> logger)
    {
        _s = settings.Value;
        _splitter = splitter.Value;
        _app = app.Value;
        _menus = menus;
        _tree = tree;
        _paths = paths;
        _applio = applio;
        _registry = registry;
        _runner = runner;
        _logger = logger;
    }

    // GET api/voiceswap/config
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(new VoiceSwapConfigDto(
        await _menus.GetForFilesAsync(_splitter.GetMediaExtensions(), _splitter.Menus, extra: _splitter.RpmMenu, ct),
        new[]
        {
            new SplitterQualityDto("standard", _splitter.StandardQualityLabel),
            new SplitterQualityDto("high", _splitter.HighQualityLabel)
        },
        _s.GetModels().Select(m => new VoiceModelDto(m.Id, m.Name, m.DefaultPitch)).ToList(),
        _s.OutputFolder.Replace('\\', '/'),
        (_splitter.RpmFolder ?? "").Replace('\\', '/').Trim('/'),
        _splitter.RpmMenu ?? "",
        _app.VoiceSwapHubPath,
        _applio.CheckSetup()));

    // GET api/voiceswap/tree?menu=musics
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

    // POST api/voiceswap/jobs  { jobId, modelId, quality, mediaPath, pitch? }
    // Validates, then starts the job in the background and returns 202. Progress arrives over SignalR
    // (VoiceSwapHub) in the group named by jobId.
    [HttpPost("jobs")]
    public IActionResult StartJob([FromBody] VoiceSwapStartRequest request)
    {
        if (request is null || !VoiceoverJobRegistry.IsValidId(request.JobId))
            return BadRequest("Invalid job id.");

        var voice = _s.GetModels().FirstOrDefault(m => m.Id == request.ModelId);
        if (voice is null) return BadRequest("Unknown voice.");

        string demucsModel;
        switch (request.Quality)
        {
            case "standard": demucsModel = _splitter.StandardModel; break;
            case "high": demucsModel = _splitter.HighQualityModel; break;
            default: return BadRequest("Quality must be 'standard' or 'high'.");
        }

        var pitch = request.Pitch ?? voice.DefaultPitch;
        if (pitch is < -24 or > 24) return BadRequest("Pitch must be between -24 and 24 semitones.");

        var problems = _applio.CheckSetup();
        if (problems.Count > 0) return StatusCode(StatusCodes.Status503ServiceUnavailable, string.Join(" ", problems));

        string mediaPath;
        try
        {
            mediaPath = _paths.ResolveUnderMedia(request.MediaPath);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        if (!_splitter.GetMediaExtensions().Contains(Path.GetExtension(mediaPath), StringComparer.OrdinalIgnoreCase))
            return BadRequest("That file type is not supported.");
        if (!System.IO.File.Exists(mediaPath))
            return NotFound("That file was not found on the server.");

        if (!_registry.TryRegister(request.JobId, out var token))
            return Conflict("That job id is already in use.");

        _logger.LogInformation("Voice swap job {JobId}: voice {Voice}, pitch {Pitch}, model {Model}, {File}",
            request.JobId, voice.Name, pitch, demucsModel, Path.GetFileName(mediaPath));

        var workDir = Path.Combine(_s.GetTempRoot(), request.JobId);
        _runner.Start(new VoiceSwapJob(request.JobId, voice, pitch, demucsModel, mediaPath, workDir), token);
        return Accepted(new { jobId = request.JobId });
    }

    // POST api/voiceswap/jobs/{jobId}/cancel  (the hub's CancelJob does the same thing)
    [HttpPost("jobs/{jobId}/cancel")]
    public IActionResult CancelJob(string jobId) =>
        VoiceoverJobRegistry.IsValidId(jobId) && _registry.Cancel(jobId) ? Ok() : NotFound();
}
