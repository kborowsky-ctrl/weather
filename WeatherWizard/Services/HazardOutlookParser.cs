using System.Text;
using System.Text.RegularExpressions;

namespace WeatherWizard.Services;

/// <summary>Extracts a zone's "Days two through seven" paragraph from an NWS Hazardous Weather Outlook.</summary>
public static partial class HazardOutlookParser
{
    private const string Boilerplate =
        "Please listen to NOAA Weather Radio or go to weather.gov on the Internet for more information about the following hazards.";

    public static string? DaysTwoThroughSeven(string productText, string? forecastZone, string? countyZone)
    {
        var text = productText.Replace("\r\n", "\n");
        foreach (var segment in text.Split("$$"))
        {
            var lines = segment.Split('\n');
            var zones = ReadUgcZones(lines);
            if (zones.Count == 0)
                continue;
            if (!(forecastZone is not null && zones.Contains(forecastZone))
                && !(countyZone is not null && zones.Contains(countyZone)))
                continue;

            return ExtractDaysTwoThroughSeven(lines);
        }

        return null;
    }

    /// <summary>
    /// Reads the UGC header (e.g. "CTZ005>012-NJZ002-004-NYZ067>074-250645-"), which can wrap
    /// across lines and ends with a 6-digit expiration time. Bare numbers reuse the last prefix.
    /// </summary>
    public static HashSet<string> ReadUgcZones(IEnumerable<string> lines)
    {
        var header = new StringBuilder();
        var started = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!started)
            {
                if (!UgcStartRegex().IsMatch(line))
                    continue;
                started = true;
            }

            header.Append(line);
            if (UgcEndRegex().IsMatch(line))
                break;
        }

        var zones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!started)
            return zones;

        var prefix = "";
        foreach (var token in header.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = token.Trim();
            var m = UgcTokenRegex().Match(t);
            if (!m.Success)
                continue;

            if (m.Groups["prefix"].Success && m.Groups["prefix"].Value.Length > 0)
                prefix = m.Groups["prefix"].Value.ToUpperInvariant();
            else if (m.Groups["from"].Value.Length == 6)
                continue; // expiration time, e.g. 250645

            if (prefix.Length == 0)
                continue;

            var from = int.Parse(m.Groups["from"].Value);
            var to = m.Groups["to"].Success ? int.Parse(m.Groups["to"].Value) : from;
            for (var n = from; n <= to && n - from < 1000; n++)
                zones.Add($"{prefix}{n:000}");
        }

        return zones;
    }

    private static string? ExtractDaysTwoThroughSeven(string[] lines)
    {
        var body = new StringBuilder();
        string? heading = null;
        var inSection = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!inSection)
            {
                if (line.StartsWith(".DAYS TWO THROUGH SEVEN", StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    heading = line.TrimStart('.').Replace("...", ": ");
                }

                continue;
            }

            if ((line.StartsWith('.') && !line.StartsWith("...")) || line.StartsWith("&&"))
                break;

            body.Append(line.Length == 0 ? "\n" : line + " ");
        }

        if (!inSection)
            return null;

        var paragraphs = body.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => WhitespaceRegex().Replace(p, " ").Trim())
            .Where(p => p.Length > 0 && !p.Equals(Boilerplate, StringComparison.OrdinalIgnoreCase));

        var result = string.Join("\n", paragraphs);
        if (!string.IsNullOrWhiteSpace(heading))
            result = $"{heading}\n{result}";
        return result.Trim();
    }

    [GeneratedRegex(@"^[A-Z]{2}[CZ]\d{3}")]
    private static partial Regex UgcStartRegex();

    [GeneratedRegex(@"-\d{6}-\s*$")]
    private static partial Regex UgcEndRegex();

    [GeneratedRegex(@"^(?<prefix>[A-Z]{2}[CZ])?(?<from>\d{3}|\d{6})(?:>(?<to>\d{3}))?$")]
    private static partial Regex UgcTokenRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
