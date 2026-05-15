namespace WeatherWizard.Services;

/// <summary>Short labels for tabs and lists (e.g. <c>Austin, TX</c> — no trailing country for U.S.).</summary>
public static class LocationDisplayFormatter
{
    public static string PrimaryCity(string? locality, string displayNameFallback) =>
        !string.IsNullOrWhiteSpace(locality)
            ? locality.Trim()
            : FirstCommaSegment(displayNameFallback);

    public static string TabShort(string? locality, string displayNameFallback, string? admin1, string countryCode)
    {
        var city = PrimaryCity(locality, displayNameFallback);
        if (string.IsNullOrWhiteSpace(city))
            return "—";

        if (string.Equals(countryCode, "US", StringComparison.OrdinalIgnoreCase))
        {
            var abbr = UsStateAbbreviations.TryAbbreviateFromAdmin1(admin1);
            if (abbr is not null)
                return $"{city}, {abbr}";
            if (!string.IsNullOrWhiteSpace(admin1))
                return $"{city}, {admin1}";
            return city;
        }

        if (!string.IsNullOrWhiteSpace(admin1))
            return $"{city}, {admin1}";

        if (!string.IsNullOrWhiteSpace(countryCode))
            return $"{city}, {countryCode.ToUpperInvariant()}";

        return city;
    }

    private static string FirstCommaSegment(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        var i = s.IndexOf(',');
        return i > 0 ? s[..i].Trim() : s.Trim();
    }
}
