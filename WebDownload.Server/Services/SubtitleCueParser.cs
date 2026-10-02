using System.Globalization;
using System.Text.RegularExpressions;

namespace WebDownload.Server.Services;

public sealed record SubtitleCue(TimeSpan Start, TimeSpan End, string Text)
{
    public double TargetSeconds => (End - Start).TotalSeconds;
}

/// <summary>Parses .srt and .vtt into voiceable cues.</summary>
public static class SubtitleCueParser
{
    private static readonly Regex TimeLine = new(
        @"(?<s>(?:\d{1,2}:)?\d{1,2}:\d{2}[.,]\d{1,3})\s*-->\s*(?<e>(?:\d{1,2}:)?\d{1,2}:\d{2}[.,]\d{1,3})",
        RegexOptions.Compiled);

    // <i>, <c.colorE5E5E5>, {\an8} and similar styling that must never be read aloud.
    private static readonly Regex Markup = new(@"<[^>]*>|\{\\[^}]*\}", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static List<SubtitleCue> Parse(string raw, Regex? ignore)
    {
        var cues = new List<SubtitleCue>();
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        TimeSpan start = default, end = default;
        var inCue = false;
        var text = new List<string>();
        var lastText = "";
        var lastEnd = TimeSpan.MinValue;

        void Flush()
        {
            if (inCue && text.Count > 0)
            {
                var joined = Clean(string.Join(" ", text), ignore);
                // A repeated line that overlaps the previous cue is a rolling-caption duplicate.
                var duplicate = joined.Equals(lastText, StringComparison.OrdinalIgnoreCase) && start <= lastEnd;
                if (joined.Length > 0 && !duplicate)
                {
                    cues.Add(new SubtitleCue(start, end, joined));
                    lastText = joined;
                    lastEnd = end;
                }
            }
            text.Clear();
            inCue = false;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            var m = TimeLine.Match(line);
            if (m.Success)
            {
                Flush();
                start = ParseTime(m.Groups["s"].Value);
                end = ParseTime(m.Groups["e"].Value);
                inCue = true;
                continue;
            }

            if (line.Length == 0) { Flush(); continue; }
            if (!inCue) continue; // WEBVTT header, NOTE blocks, cue index lines

            // A bare number right before a timecode is the next cue's index, not text.
            if (IsDigits(line) && i + 1 < lines.Length && TimeLine.IsMatch(lines[i + 1]))
            {
                Flush();
                continue;
            }

            text.Add(line);
        }
        Flush();
        return cues;
    }

    private static string Clean(string s, Regex? ignore)
    {
        s = Markup.Replace(s, " ");
        if (ignore is not null) s = ignore.Replace(s, " ");
        // Quotes, ampersands and angle brackets can break the SSML the TTS client builds
        // (same clean-up the WPF app did), and music notes would be read out.
        s = s.Replace("\"", "").Replace("'", "").Replace("♪", " ")
             .Replace("&", " ").Replace("<", " ").Replace(">", " ");
        return Spaces.Replace(s, " ").Trim();
    }

    private static bool IsDigits(string s) => s.All(char.IsDigit);

    private static TimeSpan ParseTime(string t)
    {
        var parts = t.Replace(',', '.').Split(':');
        double hours = 0, minutes, seconds;
        if (parts.Length == 3)
        {
            hours = double.Parse(parts[0], CultureInfo.InvariantCulture);
            minutes = double.Parse(parts[1], CultureInfo.InvariantCulture);
            seconds = double.Parse(parts[2], CultureInfo.InvariantCulture);
        }
        else
        {
            minutes = double.Parse(parts[0], CultureInfo.InvariantCulture);
            seconds = double.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        return TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
    }
}
