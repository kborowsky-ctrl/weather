namespace WeatherWizard.Services;

/// <summary>Maps common U.S. state/territory abbreviations to Open-Meteo <c>admin1</c> names.</summary>
public static class UsStateAbbreviations
{
    private static readonly Dictionary<string, string> CodeToAdmin1 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
            ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
            ["DC"] = "District of Columbia", ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii",
            ["ID"] = "Idaho", ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa",
            ["KS"] = "Kansas", ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine",
            ["MD"] = "Maryland", ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota",
            ["MS"] = "Mississippi", ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska",
            ["NV"] = "Nevada", ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico",
            ["NY"] = "New York", ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio",
            ["OK"] = "Oklahoma", ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island",
            ["SC"] = "South Carolina", ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas",
            ["UT"] = "Utah", ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington",
            ["WV"] = "West Virginia", ["WI"] = "Wisconsin", ["WY"] = "Wyoming",
            ["AS"] = "American Samoa", ["GU"] = "Guam", ["MP"] = "Northern Mariana Islands",
            ["PR"] = "Puerto Rico", ["VI"] = "U.S. Virgin Islands",
        };

    private static readonly Lazy<Dictionary<string, string>> Admin1ToCode = new(() =>
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in CodeToAdmin1)
            d[kv.Value] = kv.Key;
        return d;
    });

    /// <summary>Returns a 2-letter code when <paramref name="admin1"/> matches a U.S. state/territory name.</summary>
    public static string? TryAbbreviateFromAdmin1(string? admin1)
    {
        if (string.IsNullOrWhiteSpace(admin1))
            return null;

        return Admin1ToCode.Value.TryGetValue(admin1.Trim(), out var code) ? code : null;
    }

    /// <summary>Returns the GeoNames-style admin1 name if <paramref name="twoLetter"/> is a known U.S. code.</summary>
    public static string? TryExpandToAdmin1(string twoLetter)
    {
        if (string.IsNullOrWhiteSpace(twoLetter))
            return null;

        var key = twoLetter.Trim();
        if (key.Length != 2)
            return null;

        return CodeToAdmin1.TryGetValue(key.ToUpperInvariant(), out var full) ? full : null;
    }

    public static bool IsProbablyUsStateCode(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;

        var t = s.Trim();
        if (t.Length != 2)
            return false;

        return IsAsciiLetter(t[0]) && IsAsciiLetter(t[1]);
    }

    private static bool IsAsciiLetter(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
