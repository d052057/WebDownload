using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// Builds the folder tree the Splitter page shows, from SQL:
///  - any menu in the MediaMenu table (movies, videos, ...): the MediaFolder / MediaTrack tables
///    (nested folders, only media files).
///  - the special Splitter:RpmMenu ("rpm"): the Rpm / RpmTrack tables. Each Rpm row is one album folder, and each RpmTrack row holds the
///    track's FILE NAME in Title (that is what the rpm scan stores), so the file is
///    MediaDrive\RpmFolder\&lt;Rpm.Title&gt;\&lt;RpmTrack.Title&gt;.
/// Only names and relative paths are returned; the file itself is read from disk when a job runs.
/// </summary>
public sealed class MediaTreeService
{
    // "01. ", "2 - ", "03) " in front of a title. A bare "01 " is deliberately left alone.
    private static readonly Regex LeadingNumber = new(@"^\s*\d+\s*[.\-_)\]]+\s*", RegexOptions.Compiled);

    private readonly DBWebDownload _db;
    private readonly SplitterSettings _s;
    private readonly MediaPathResolver _paths;
    private readonly MenuService _menus;

    public MediaTreeService(DBWebDownload db, IOptions<SplitterSettings> settings, MediaPathResolver paths, MenuService menus)
    {
        _db = db;
        _s = settings.Value;
        _paths = paths;
        _menus = menus;
    }

    public async Task<SplitterTreeDto> GetTreeAsync(string menu, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_s.RpmMenu) && string.Equals(menu, _s.RpmMenu, StringComparison.OrdinalIgnoreCase))
            return await GetRpmTreeAsync(_s.RpmMenu, ct);

        var canonical = await _menus.ResolveAsync(menu, _s.Menus, ct)
            ?? throw new ArgumentException($"'{menu}' is not a supported menu.");
        return await GetMediaTreeAsync(canonical, _s.GetMediaExtensions(), ct);
    }

    /// <summary>
    /// The tree of one MediaMenu menu with the caller's own optional menu allow-list and file extensions.
    /// Voiceover uses this: it has no rpm menu and only lists video files.
    /// </summary>
    public async Task<SplitterTreeDto> GetMediaTreeAsync(
        string menu, IReadOnlyList<string>? allowList, IReadOnlyList<string> allowedExtensions, CancellationToken ct)
    {
        var canonical = await _menus.ResolveAsync(menu, allowList, ct)
            ?? throw new ArgumentException($"'{menu}' is not a supported menu.");
        return await GetMediaTreeAsync(canonical, allowedExtensions, ct);
    }

    // ---- MediaMenu menus (movies, videos, ...) ----------------------------------------------------------------------

    private async Task<SplitterTreeDto> GetMediaTreeAsync(string menu, IReadOnlyList<string> allowedExtensions, CancellationToken ct)
    {
        var menuId = await _db.MediaMenus
            .Where(m => m.Menu == menu)
            .Select(m => (Guid?)m.RecordId)
            .FirstOrDefaultAsync(ct);
        if (menuId is null) return new SplitterTreeDto(menu, 0, new(), new());

        // Two flat queries filtered by a server-side join; the nesting is rebuilt in memory.
        var folderRows = await _db.MediaFolders
            .Where(f => f.MenuId == menuId)
            .Select(f => new { f.RecordId, f.ParentFolderId, f.Name, f.RootPath })
            .ToListAsync(ct);
        var trackRows = await _db.MediaTracks
            .Where(t => t.Folder.MenuId == menuId)
            .Select(t => new { t.RecordId, t.FolderId, t.FileName })
            .ToListAsync(ct);

        var extensions = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
        var foldersByParent = folderRows.ToLookup(f => f.ParentFolderId);
        var tracksByFolder = trackRows
            .Where(t => extensions.Contains(Path.GetExtension(t.FileName)))
            .ToLookup(t => t.FolderId);

        // A top-level folder starts at its own RootPath/Name; nested folders extend their parent
        // (the same rule MediaFolderTreeService uses).
        List<SplitterFolderDto> Build(Guid? parentId, string? parentPath, HashSet<Guid> ancestors)
        {
            var result = new List<SplitterFolderDto>();
            foreach (var f in foldersByParent[parentId].OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (ancestors.Contains(f.RecordId)) continue; // a corrupted parent chain must not recurse forever

                var path = parentId is null
                    ? (string.IsNullOrWhiteSpace(f.RootPath) ? f.Name : $"{f.RootPath.Trim('/', '\\')}/{f.Name}")
                    : $"{parentPath}/{f.Name}";

                var children = Build(f.RecordId, path, new HashSet<Guid>(ancestors) { f.RecordId });
                var files = tracksByFolder[f.RecordId]
                    .OrderBy(t => t.FileName, StringComparer.OrdinalIgnoreCase)
                    .Select(t => new SplitterTrackDto
                    {
                        Id = t.RecordId,
                        FileName = t.FileName,
                        DisplayTitle = t.FileName,
                        Album = f.Name,
                        Url = $"{path}/{t.FileName}"
                    })
                    .ToList();

                if (children.Count == 0 && files.Count == 0) continue; // nothing usable below this folder
                result.Add(new SplitterFolderDto { Id = f.RecordId, Name = f.Name, Folders = children, Tracks = files });
            }
            return result;
        }

        var top = Build(null, null, new HashSet<Guid>());
        return new SplitterTreeDto(menu, CountFiles(top, new List<SplitterTrackDto>()), top, new List<SplitterTrackDto>());
    }

    // ---- rpm (Splitter:RpmMenu) --------------------------------------------------------------------------------------

    private async Task<SplitterTreeDto> GetRpmTreeAsync(string menu, CancellationToken ct)
    {
        var albums = await _db.Rpms
            .Select(r => new { r.RecordId, r.Title, r.Artist })
            .ToListAsync(ct);
        var tracks = await _db.RpmTracks
            .Select(t => new { t.RecordId, t.RpmId, t.Title, t.Artist, t.TrackNumber, t.DurationSeconds })
            .ToListAsync(ct);

        var tracksByAlbum = tracks.ToLookup(t => t.RpmId);
        var rpmRoot = (_s.RpmFolder ?? "").Trim().Trim('/', '\\').Replace('\\', '/');
        var audio = new HashSet<string>(_s.GetAudioExtensions(), StringComparer.OrdinalIgnoreCase);

        var folders = new List<SplitterFolderDto>();
        foreach (var album in albums.OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase))
        {
            var albumPath = string.IsNullOrEmpty(rpmRoot) ? album.Title : $"{rpmRoot}/{album.Title}";
            Dictionary<string, string>? onDisk = null;
            var files = new List<SplitterTrackDto>();

            foreach (var t in tracksByAlbum[album.RecordId]
                         .OrderBy(t => t.TrackNumber ?? int.MaxValue)
                         .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = t.Title;

                // The scan stores the full file name. Older rows may hold only a cleaned title with no audio
                // extension; for those, find the real file in the album folder (looked up once per album).
                if (!audio.Contains(Path.GetExtension(fileName)))
                {
                    onDisk ??= IndexAlbumFolder(albumPath, audio);
                    if (!onDisk.TryGetValue(NormalizeName(fileName, audio), out var real)) continue;
                    fileName = real;
                }

                files.Add(new SplitterTrackDto
                {
                    Id = t.RecordId,
                    FileName = fileName,
                    DisplayTitle = CleanTitle(fileName, audio),
                    Artist = t.Artist ?? album.Artist,
                    Album = album.Title,
                    TrackNumber = t.TrackNumber,
                    Duration = FormatDuration(t.DurationSeconds),
                    Url = $"{albumPath}/{fileName}"
                });
            }

            if (files.Count > 0)
                folders.Add(new SplitterFolderDto { Id = album.RecordId, Name = album.Title, Tracks = files });
        }

        return new SplitterTreeDto(menu, CountFiles(folders, new List<SplitterTrackDto>()), folders, new List<SplitterTrackDto>());
    }

    private Dictionary<string, string> IndexAlbumFolder(string albumPath, HashSet<string> audio)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(_paths.MediaRoot, albumPath);
        if (!Directory.Exists(dir)) return map;

        foreach (var file in Directory.EnumerateFiles(dir))
        {
            if (!audio.Contains(Path.GetExtension(file))) continue;
            var name = Path.GetFileName(file);
            map.TryAdd(NormalizeName(name, audio), name);
        }
        return map;
    }

    // Only a KNOWN audio extension is removed: Path.GetExtension("01. Title") would treat ". Title" as an
    // extension and leave just "01".
    private static string StripAudioExtension(string name, HashSet<string> audio)
    {
        var ext = Path.GetExtension(name);
        return ext.Length > 0 && audio.Contains(ext) ? name[..^ext.Length] : name;
    }

    private static string NormalizeName(string name, HashSet<string> audio) =>
        LeadingNumber.Replace(StripAudioExtension(name, audio), "").Trim();

    private static string CleanTitle(string fileName, HashSet<string> audio)
    {
        var stem = StripAudioExtension(fileName, audio).Trim();
        var cleaned = LeadingNumber.Replace(stem, "").Trim();
        return cleaned.Length > 0 ? cleaned : stem;
    }

    private static string? FormatDuration(int? seconds) =>
        seconds is null
            ? null
            : TimeSpan.FromSeconds(seconds.Value).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

    private static int CountFiles(List<SplitterFolderDto> folders, List<SplitterTrackDto> tracks) =>
        tracks.Count + folders.Sum(f => CountFiles(f.Folders, f.Tracks));
}
