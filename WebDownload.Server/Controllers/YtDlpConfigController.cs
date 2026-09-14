using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Controllers
{
    // Lets the Angular ytdlp page load its "drag an item onto the box"
    // reference list from appsettings.json (YtDlp:DragDropArgs) at runtime,
    // instead of it being hardcoded into the compiled JS bundle - so items
    // can be added/edited/removed by editing config and restarting the app,
    // with no rebuild of either the server or the client needed.
    [ApiController]
    [Route("api/[controller]")]
    public class YtDlpConfigController : ControllerBase
    {
        private readonly YtDlpSettings _settings;

        public YtDlpConfigController(IOptions<YtDlpSettings> settings)
        {
            _settings = settings.Value;
        }

        // GET: api/YtDlpConfig/drag-drop-args
        [HttpGet("drag-drop-args")]
        public ActionResult<List<YtDlpDragDropArg>> GetDragDropArgs()
        {
            return Ok(_settings.DragDropArgs);
        }
    }
}
