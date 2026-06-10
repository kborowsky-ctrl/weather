using System.Text.RegularExpressions;

namespace WeatherWizard.Services;

/// <summary>
/// Cleans NWS alert headlines for UI: drops timezone tokens (CDT, PDT, …) and removes "by NWS" onward.
/// </summary>
public static partial class NwsAlertHeadlineFormatter
{
    /// <summary>Removes <c> by NWS …</c> (case-insensitive) and the rest of the line.</summary>
    [GeneratedRegex(@"\s+by\s+NWS\b.*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ByNwsSuffixRegex();

    /// <summary>Common NWS / US zone abbreviations in alert text.</summary>
    [GeneratedRegex(
        @"\b(?:AKST|AKDT|AST|ADT|ChST|CST|CDT|EST|EDT|GMT|UTC|HST|HDT|MDT|MST|PDT|PST|SST)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeZoneTokenRegex();

    [GeneratedRegex(@"\s{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex MultiSpaceRegex();

    /// <summary>Strips NWS timing tails (issued / in effect / until …) from headline text.</summary>
    [GeneratedRegex(
        @"\s+(?:issued|remains\s+in\s+effect|now\s+in\s+effect|in\s+effect(?:\s+from|\s+until)?)\b.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimingTailRegex();

    public static string Format(string? headline)
    {
        if (string.IsNullOrWhiteSpace(headline))
            return headline?.Trim() ?? "";

        var s = headline.Trim();
        var original = s;
        s = ByNwsSuffixRegex().Replace(s, "");
        s = TimeZoneTokenRegex().Replace(s, "");
        s = TimingTailRegex().Replace(s, "");
        s = MultiSpaceRegex().Replace(s, " ").Trim();
        return string.IsNullOrEmpty(s) ? original : s;
    }

    /// <summary>Short label for UI: event name, or headline with dates/times removed.</summary>
    public static string ToSummary(string? eventName, string? headline)
    {
        if (!string.IsNullOrWhiteSpace(eventName))
            return eventName.Trim();

        if (string.IsNullOrWhiteSpace(headline))
            return "";

        var s = Format(headline);
        s = TimingTailRegex().Replace(s, "");
        s = MultiSpaceRegex().Replace(s, " ").Trim();
        return string.IsNullOrEmpty(s) ? headline.Trim() : s;
    }
}
