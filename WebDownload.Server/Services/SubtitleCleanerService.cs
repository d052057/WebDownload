using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WebDownload.Server.Services;

/// <summary>
/// Cleans YouTube auto-generated ("rolling") captions so each line appears once.
/// Works for any language, including YouTube's auto-translated tracks.
/// Accepts .srt or .vtt text; always produces SRT text.
/// Manual (non-rolling) subtitles keep their lines intact.
/// </summary>
public static class SubtitleCleanerService
{
    private static readonly Regex TimeRx = new(
        @"((?:\d{1,2}:)?\d{2}:\d{2}[.,]\d{3})\s*-->\s*((?:\d{1,2}:)?\d{2}:\d{2}[.,]\d{3})",
        RegexOptions.Compiled);

    private static readonly Regex TagRx = new(@"<[^>]+>", RegexOptions.Compiled);   // <c>, <00:00:01.000>, <i> ...
    private static readonly Regex SpaceRx = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex DigitsRx = new(@"^\s*\d+\s*$", RegexOptions.Compiled);

    // Cues shorter than this are YouTube's rolling "filler" cues (10-26 ms in practice).
    private const double MinCueMs = 100;

    // Identical neighbouring cues are merged only if the gap between them is this small,
    // so a genuine repeat ("yeah ... yeah") after a pause is not stretched across the silence.
    private const double MergeGapMs = 250;

    // A file is treated as rolling captions when most of its two-line cues repeat the
    // previous cue's last line as their first line.
    private const int MinMultiLineCuesForRolling = 2;
    private const double RollingRatio = 0.6;

    private sealed class RawCue
    {
        public TimeSpan Start { get; init; }
        public TimeSpan End { get; init; }
        public List<string> Lines { get; } = new();
    }

    private sealed record Cue(TimeSpan Start, TimeSpan End, string Text);

    /// <summary>File-to-file version. Returns the number of cues written.</summary>
    public static int Clean(string inputPath, string outputPath)
    {
        var raw = File.ReadAllText(inputPath, Encoding.UTF8);
        var srt = CleanText(raw, out var cueCount);
        File.WriteAllText(outputPath, srt, new UTF8Encoding(false));
        return cueCount;
    }

    /// <summary>
    /// Text version: takes the content of an .srt/.vtt file and returns cleaned SRT text.
    /// cueCount is 0 when no cues could be read (the caller should then keep the original).
    /// </summary>
    public static string CleanText(string raw, out int cueCount)
    {
        var rawCues = ParseCues(raw);
        bool rolling = LooksLikeRollingCaptions(rawCues);

        var cues = new List<Cue>();
        foreach (var rc in rawCues)
        {
            if ((rc.End - rc.Start).TotalMilliseconds < MinCueMs) continue;

            // Rolling captions: line 1 = previous text, last line = NEW text.
            var text = rolling ? rc.Lines[^1] : string.Join("\n", rc.Lines);
            cues.Add(new Cue(rc.Start, rc.End, text));
        }

        // Merge consecutive identical cues by extending the end time.
        var merged = new List<Cue>();
        foreach (var c in cues)
        {
            if (merged.Count > 0
                && merged[^1].Text == c.Text
                && (c.Start - merged[^1].End).TotalMilliseconds <= MergeGapMs)
            {
                var end = c.End > merged[^1].End ? c.End : merged[^1].End;
                merged[^1] = merged[^1] with { End = end };
            }
            else
            {
                merged.Add(c);
            }
        }

        // Prevent overlaps: a cue must not run past the start of the next one.
        for (int i = 0; i < merged.Count - 1; i++)
        {
            if (merged[i].End > merged[i + 1].Start && merged[i + 1].Start > merged[i].Start)
                merged[i] = merged[i] with { End = merged[i + 1].Start };
        }

        var sb = new StringBuilder();
        for (int i = 0; i < merged.Count; i++)
        {
            sb.Append(i + 1).Append('\n');
            sb.Append(Fmt(merged[i].Start)).Append(" --> ").Append(Fmt(merged[i].End)).Append('\n');
            sb.Append(merged[i].Text).Append("\n\n");
        }

        cueCount = merged.Count;
        return sb.ToString();
    }

    // Line-by-line reader that works for both SRT and VTT. Blank lines BEFORE a cue's text
    // are skipped (YouTube's rolling captions often have one); the cue ends at the first
    // blank line AFTER its text. SRT cue numbers (a digits-only line right above a time
    // line) are skipped.
    private static List<RawCue> ParseCues(string raw)
    {
        var text = raw.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
        var result = new List<RawCue>();
        RawCue? cur = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            var m = TimeRx.Match(line);
            if (m.Success)
            {
                Flush();
                cur = new RawCue { Start = ParseTime(m.Groups[1].Value), End = ParseTime(m.Groups[2].Value) };
                continue;
            }

            if (cur == null) continue;                  // WEBVTT header, NOTE blocks, etc.

            if (i + 1 < lines.Length && DigitsRx.IsMatch(line) && TimeRx.IsMatch(lines[i + 1]))
                continue;                               // SRT cue number

            if (string.IsNullOrWhiteSpace(line))
            {
                if (cur.Lines.Count > 0) Flush();       // blank line after text = end of cue
                continue;                               // blank line before the text: keep waiting
            }

            var clean = CleanLine(line);
            if (clean.Length > 0) cur.Lines.Add(clean);
        }

        Flush();
        return result;

        void Flush()
        {
            if (cur != null && cur.Lines.Count > 0) result.Add(cur);
            cur = null;
        }
    }

    private static bool LooksLikeRollingCaptions(List<RawCue> cues)
    {
        int multiLine = 0, rolled = 0;
        string? prevLast = null;

        foreach (var c in cues)
        {
            if (c.Lines.Count > 1)
            {
                multiLine++;
                if (c.Lines[0] == prevLast) rolled++;
            }
            prevLast = c.Lines[^1];
        }

        return multiLine >= MinMultiLineCuesForRolling && rolled >= multiLine * RollingRatio;
    }

    private static string CleanLine(string line)
    {
        var s = TagRx.Replace(line, "");
        s = WebUtility.HtmlDecode(s);
        return SpaceRx.Replace(s, " ").Trim();
    }

    // Reads "[hh:]mm:ss,mmm" with integer math, so there is no floating-point rounding.
    private static TimeSpan ParseTime(string s)
    {
        var p = s.Split(':', '.', ',');
        int h = p.Length == 4 ? int.Parse(p[0], CultureInfo.InvariantCulture) : 0;
        int m = int.Parse(p[^3], CultureInfo.InvariantCulture);
        int sec = int.Parse(p[^2], CultureInfo.InvariantCulture);
        int ms = int.Parse(p[^1], CultureInfo.InvariantCulture);
        return new TimeSpan(0, h, m, sec, ms);
    }

    private static string Fmt(TimeSpan t) =>
        $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
}
