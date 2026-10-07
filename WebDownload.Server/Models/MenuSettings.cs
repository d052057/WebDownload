namespace WebDownload.Server.Models;

/// <summary>
/// Bind to the "MediaMenu" section of appsettings.json. The menus themselves live in the MediaMenu
/// table; this only controls how they are presented.
/// </summary>
public class MenuSettings
{
    // Menu name (case-insensitive) -> title shown on the pages. A menu with no entry is shown with
    // its first letter capitalised ("movies" -> "Movies"). Example: { "rpm": "RPM" }.
    public Dictionary<string, string>? Titles { get; set; }

    // Menus are listed in this order first (names, case-insensitive); the rest follow by creation date.
    public List<string>? Order { get; set; }

    // The MediaMenu table and the "which menus hold videos" check are cached for this long.
    public int CacheSeconds { get; set; } = 60;
}
