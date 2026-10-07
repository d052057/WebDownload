using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>Feeds the two select lists: subtitle files from disk, video files from the media database.</summary>
public sealed class MediaBrowseService
{
    private readonly DBWebDownload _db;
    private readonly VoiceoverSettings _s;
    private readonly MenuService _menus;
    private readonly HashSet<string> _subtitleExtensions;

    public MediaBrowseService(DBWebDownload db, IOptions<VoiceoverSettings> settings, MenuService menus)
    {
        _db = db;
        _s = settings.Value;
        _menus = menus;
        _subtitleExtensions = new HashSet<string>(_s.GetSubtitleExtensions(), StringComparer.OrdinalIgnoreCase);
    }

    public List<SrtFileItem> GetSrtFiles()
    {
        if (string.IsNullOrWhiteSpace(_s.SrtFolder) || !Directory.Exists(_s.SrtFolder))
            return new List<SrtFileItem>();

        return new DirectoryInfo(_s.SrtFolder)
            .EnumerateFiles()
            .Where(f => _subtitleExtensions.Contains(f.Extension))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new SrtFileItem(f.Name, f.Extension.TrimStart('.').ToLowerInvariant(), f.Length, f.LastWriteTimeUtc))
            .ToList();
    }

    /// <summary>
    /// Every video track in one MediaMenu menu, with its path relative to the media
    /// drive. Folder paths are rebuilt the same way MediaFolderTreeService does it: a top-level
    /// folder starts at its own RootPath/Name and nested folders extend their parent.
    /// </summary>
    public Task<List<VideoFileItem>> GetVideosAsync(string menu, CancellationToken ct) =>
        GetVideosAsync(menu, _s.Menus, _s.GetVideoExtensions(), ct);

    /// <summary>The same listing with the optional menu allow-list and the file extensions supplied by the caller.</summary>
    public async Task<List<VideoFileItem>> GetVideosAsync(
        string menu, IReadOnlyList<string>? allowList, IReadOnlyList<string> allowedExtensions, CancellationToken ct)
    {
        var canonical = await _menus.ResolveAsync(menu, allowList, ct)
            ?? throw new ArgumentException($"'{menu}' is not a supported menu.");

        var menuId = await _db.MediaMenus
            .Where(m => m.Menu == canonical)
            .Select(m => (Guid?)m.RecordId)
            .FirstOrDefaultAsync(ct);
        if (menuId is null) return new List<VideoFileItem>();

        // Two flat queries filtered by a server-side join; the paths are assembled in memory.
        var folders = await _db.MediaFolders
            .Where(f => f.MenuId == menuId)
            .Select(f => new { f.RecordId, f.ParentFolderId, f.Name, f.RootPath })
            .ToListAsync(ct);
        var tracks = await _db.MediaTracks
            .Where(t => t.Folder.MenuId == menuId)
            .Select(t => new { t.RecordId, t.FolderId, t.FileName })
            .ToListAsync(ct);

        var byId = folders.ToDictionary(f => f.RecordId);
        var pathCache = new Dictionary<Guid, string?>();

        string? FolderPath(Guid id)
        {
            if (pathCache.TryGetValue(id, out var cached)) return cached;

            var names = new List<string>();
            var seen = new HashSet<Guid>();
            string? root = null;
            Guid? current = id;
            string? result;

            while (current is { } g)
            {
                if (!byId.TryGetValue(g, out var folder) || !seen.Add(g))
                {
                    pathCache[id] = null; // orphaned or cyclic parent chain
                    return null;
                }
                names.Add(folder.Name);
                if (folder.ParentFolderId is null) root = folder.RootPath;
                current = folder.ParentFolderId;
            }

            names.Reverse();
            var parts = string.IsNullOrWhiteSpace(root) ? names : names.Prepend(root.Trim('/', '\\'));
            result = string.Join('/', parts);
            pathCache[id] = result;
            return result;
        }

        var extensions = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
        var items = new List<VideoFileItem>();
        foreach (var t in tracks)
        {
            if (!extensions.Contains(Path.GetExtension(t.FileName))) continue;
            var folderPath = FolderPath(t.FolderId);
            if (folderPath is null) continue;
            items.Add(new VideoFileItem(t.RecordId, t.FileName, folderPath, $"{folderPath}/{t.FileName}"));
        }

        return items
            .OrderBy(i => i.Folder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
