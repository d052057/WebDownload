using Microsoft.Extensions.Options;
using WebDownload.Server.Models;

namespace WebDownload.Server.Services;

/// <summary>
/// Turns client-supplied relative paths into real paths and refuses anything that escapes
/// the allowed root. The check compares against "root + separator", so a sibling folder like
/// d:\medias2 can't pass for d:\medias.
/// </summary>
public sealed class MediaPathResolver
{
    public string MediaRoot { get; }
    public string RequestPath { get; }

    public MediaPathResolver(IOptions<ApplicationSettings> app)
    {
        RequestPath = "/" + (app.Value.MediaRequestPath ?? "/medias").Trim('/');
        var drive = app.Value.MediaDrive;
        if (string.IsNullOrWhiteSpace(drive))
            throw new InvalidOperationException("ApplicationSettings:MediaDrive is not configured.");
        MediaRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(drive));
    }

    public string ResolveUnder(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            throw new ArgumentException("A file path is required.");

        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var cleaned = relative
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(rootFull, cleaned));

        if (!IsUnder(rootFull, combined))
            throw new ArgumentException("That path is outside the allowed folder.");
        return combined;
    }

    public string ResolveUnderMedia(string relative) => ResolveUnder(MediaRoot, relative);

    public static bool IsUnder(string root, string path)
    {
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    // "<MediaRequestPath>/..." URL (before the app's path base) for a file under the media drive, or null.
    public string? ToMediaUrl(string fullPath)
    {
        if (!IsUnder(MediaRoot, fullPath)) return null;
        var relative = Path.GetRelativePath(MediaRoot, fullPath).Replace('\\', '/');
        return RequestPath + "/" + string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));
    }
}
