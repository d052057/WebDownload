using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>One entry of a menu list: Name is the MediaMenu.Menu value, Title is what the page shows.</summary>
public sealed record MenuItemDto(string Name, string Title);

/// <summary>
/// The single source of menu names (movies, videos, musics, photos, books, ...): the MediaMenu table.
/// Nothing else in the app should hold a list of them. Results are cached for MediaMenu:CacheSeconds.
/// </summary>
public sealed class MenuService
{
    private readonly DBWebDownload _db;
    private readonly IMemoryCache _cache;
    private readonly MenuSettings _s;
    private readonly Dictionary<string, string> _titles;

    public MenuService(DBWebDownload db, IMemoryCache cache, IOptions<MenuSettings> settings)
    {
        _db = db;
        _cache = cache;
        _s = settings.Value;
        _titles = new Dictionary<string, string>(_s.Titles ?? new(), StringComparer.OrdinalIgnoreCase);
    }

    public string TitleOf(string name) =>
        _titles.TryGetValue(name, out var t) && !string.IsNullOrWhiteSpace(t)
            ? t
            : name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    /// <summary>Every row of MediaMenu, in the configured order.</summary>
    public async Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken ct)
    {
        var rows = await _cache.GetOrCreateAsync("menus:all", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(0, _s.CacheSeconds));
            return await _db.MediaMenus
                .OrderBy(m => m.Datetime).ThenBy(m => m.Menu)
                .Select(m => m.Menu)
                .ToListAsync(ct);
        }) ?? new List<string>();

        var order = (_s.Order ?? new List<string>())
            .Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

        return rows
            .Select((name, i) => (name, i))
            .OrderBy(x => order.TryGetValue(x.name, out var o) ? o : int.MaxValue)
            .ThenBy(x => x.i)
            .Select(x => new MenuItemDto(x.name, TitleOf(x.name)))
            .ToList();
    }

    /// <summary>
    /// The menus a page offers: those that hold at least one file with one of <paramref name="extensions"/>
    /// (and, when <paramref name="allowList"/> is not empty, are named in it), then <paramref name="extra"/>
    /// (a special menu that is not in MediaMenu, such as "rpm") when it is given.
    /// </summary>
    public async Task<IReadOnlyList<MenuItemDto>> GetForFilesAsync(
        IReadOnlyList<string> extensions, IReadOnlyList<string>? allowList, string? extra, CancellationToken ct)
    {
        var withFiles = await MenusWithFilesAsync(extensions, ct);
        var result = (await GetAllAsync(ct))
            .Where(m => withFiles.Contains(m.Name))
            .Where(m => allowList is not { Count: > 0 } || allowList.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(extra) && !result.Any(m => string.Equals(m.Name, extra, StringComparison.OrdinalIgnoreCase)))
            result.Add(new MenuItemDto(extra, TitleOf(extra)));
        return result;
    }

    /// <summary>
    /// The exact stored name of a menu the caller may use, or null. <paramref name="menu"/> is matched
    /// case-insensitively against the MediaMenu table, then against the allow-list (when it is not empty).
    /// </summary>
    public async Task<string?> ResolveAsync(string menu, IReadOnlyList<string>? allowList, CancellationToken ct)
    {
        var found = (await GetAllAsync(ct))
            .FirstOrDefault(m => string.Equals(m.Name, menu, StringComparison.OrdinalIgnoreCase))?.Name;
        if (found is null) return null;
        if (allowList is { Count: > 0 } && !allowList.Contains(found, StringComparer.OrdinalIgnoreCase)) return null;
        return found;
    }

    // One small query per extension (EndsWith becomes a LIKE), unioned and cached.
    private async Task<HashSet<string>> MenusWithFilesAsync(IReadOnlyList<string> extensions, CancellationToken ct)
    {
        var key = "menus:files:" + string.Join('|', extensions.Select(e => e.ToLowerInvariant()).OrderBy(e => e));
        var cached = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(0, _s.CacheSeconds));
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in extensions.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var e = ext.StartsWith('.') ? ext : "." + ext;
                var menus = await _db.MediaTracks
                    .Where(t => t.FileName.EndsWith(e))
                    .Select(t => t.Folder.Menu.Menu)
                    .Distinct()
                    .ToListAsync(ct);
                names.UnionWith(menus);
            }
            return names;
        });
        return cached ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
