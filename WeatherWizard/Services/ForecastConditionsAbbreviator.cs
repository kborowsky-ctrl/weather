using System.Text.RegularExpressions;

namespace WeatherWizard.Services;

/// <summary>Progressively shorter forecast labels for tight grid columns (NWS shortForecast and Open-Meteo text).</summary>
public static class ForecastConditionsAbbreviator
{
    private static readonly (string Pattern, string Replacement, int MinLevel)[] Rules =
    [
        (@"\bthen\b", "/", 1),
        (@"\band\b", "&", 1),
        (@"\bwith\b", "w/", 1),
        (@"\bchance\b", "Chc", 1),
        (@"\bshowers\b", "Shwrs", 1),
        (@"\bthunderstorms\b", "T-Storms", 1),
        (@"\bthunderstorm\b", "T-Storm", 1),
        (@"\bpartly\b", "Pty", 1),
        (@"\bmostly\b", "Mstly", 1),
        (@"\bcloudy\b", "Cldy", 1),
        (@"\bsunny\b", "Sun", 1),
        (@"\bclear\b", "Clr", 1),
        (@"\bslight\b", "Slt", 1),
        (@"\blight\b", "Lt", 1),
        (@"\bheavy\b", "Hvy", 1),
        (@"\bscattered\b", "Sct", 1),
        (@"\bisolated\b", "Iso", 1),
        (@"\bpatchy\b", "Ptchy", 1),
        (@"\bfreezing\b", "Frz", 1),
        (@"\blikely\b", "Lkly", 1),
        (@"\bpossible\b", "Poss", 1),
        (@"\bwindy\b", "Wndy", 1),
        (@"\bblustery\b", "Blust", 1),
        (@"\bovercast\b", "Ovcst", 1),
        (@"\bdrizzle\b", "Dzl", 1),
        (@"\bflurries\b", "Flur", 1),
        (@"\bblizzard\b", "Bliz", 1),
        (@"\bmainly\b", "Mstly", 1),
        (@"\bshowers\b", "Shwr", 2),
        (@"\bthunderstorms\b", "TStrm", 2),
        (@"\bthunderstorm\b", "TStrm", 2),
        (@"\bpartly\b", "Pt", 2),
        (@"\bmostly\b", "Mst", 2),
        (@"\bcloudy\b", "Cld", 2),
        (@"\bovercast\b", "Ovc", 2),
        (@"\bmainly\b", "Mn", 2),
        (@"\bshowers\b", "Shw", 3),
        (@"\bthunderstorms\b", "TS", 3),
        (@"\bthunderstorm\b", "TS", 3),
        (@"\bchance\b", "Ch", 3),
        (@"\bpartly\b", "P", 3),
        (@"\bmostly\b", "M", 3),
        (@"\bcloudy\b", "C", 3),
        (@"\bsunny\b", "Sn", 3),
        (@"\bclear\b", "Cl", 3),
    ];

    public static IEnumerable<string> GetCandidates(string fullText, int weatherCode)
    {
        if (string.IsNullOrWhiteSpace(fullText))
        {
            yield return "—";
            yield break;
        }

        var full = CollapseWhitespace(fullText.Trim());
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        seen.Add(full);
        yield return full;

        for (var level = 1; level <= 3; level++)
        {
            var abbreviated = ApplyLevel(full, level);
            if (seen.Add(abbreviated))
                yield return abbreviated;
        }

        if (weatherCode >= 0)
        {
            var wxShort = WeatherCodeInterpreter.Short(weatherCode);
            if (seen.Add(wxShort))
                yield return wxShort;
        }
    }

    public static string ApplyLevel(string text, int level)
    {
        var result = text;
        foreach (var (pattern, replacement, minLevel) in Rules)
        {
            if (minLevel > level)
                continue;

            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return CollapseWhitespace(result);
    }

    private static string CollapseWhitespace(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
}
