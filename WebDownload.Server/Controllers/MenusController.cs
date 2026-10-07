using Microsoft.AspNetCore.Mvc;
using WebDownload.Server.Services;

namespace WebDownload.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MenusController : ControllerBase
{
    private readonly MenuService _menus;

    public MenusController(MenuService menus) => _menus = menus;

    // GET api/menus  ->  [ { "name": "movies", "title": "Movies" }, ... ]  (every row of MediaMenu)
    [HttpGet]
    public async Task<IActionResult> GetMenus(CancellationToken ct) => Ok(await _menus.GetAllAsync(ct));
}
