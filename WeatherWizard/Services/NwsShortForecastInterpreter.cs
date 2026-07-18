namespace WeatherWizard.Services;

/// <summary>Maps NWS <c>shortForecast</c> / detailed text to approximate WMO-style codes for emoji/icons.</summary>
public static class NwsShortForecastInterpreter
{
    public static int ApproximateWmoCode(string shortForecast, string? detailedForecast = null)
    {
        // NWS often puts air-quality headlines (e.g. "Smoke") in shortForecast while precip
        // details live only in detailedForecast — search both for icon selection.
        var t = Combine(shortForecast, detailedForecast);

        if (string.IsNullOrWhiteSpace(t))
            return 0;

        if (t.Contains("thunder"))
            return 95;
        if (t.Contains("blizzard") || t.Contains("heavy snow"))
            return 75;
        if (t.Contains("snow") || t.Contains("flurries"))
            return 73;
        if (t.Contains("sleet") || t.Contains("freezing rain") || t.Contains("ice"))
            return 66;
        if (t.Contains("rain") || t.Contains("drizzle") || (t.Contains("shower") && !t.Contains("snow")))
            return 65;
        if (t.Contains("fog") || t.Contains("smoke") || t.Contains("haze") || t.Contains("ash"))
            return 45;

        if (t.Contains("mostly sunny") || t.Contains("mostly clear"))
            return 1;
        if (t.Contains("partly sunny") || t.Contains("partly cloudy") || t.Contains("partly clear"))
            return 2;
        if (t.Contains("mostly cloudy"))
            return 3;

        if (t.Contains("sunny") || (t.Contains("clear") && !t.Contains("cloud")))
            return 0;

        if (t.Contains("overcast") || t.Contains("cloudy"))
            return 3;

        return 2;
    }

    /// <summary>
    /// Prefer a conditions label that includes precip when NWS shortForecast is smoke/haze-only
    /// but detailed text or PoP indicates rain/storms.
    /// </summary>
    public static string PreferConditionsDisplay(string shortForecast, string? detailedForecast, int? precipPercent)
    {
        var shortTrim = string.IsNullOrWhiteSpace(shortForecast) ? "" : shortForecast.Trim();
        if (shortTrim.Length == 0)
            return "—";

        var shortLower = shortTrim.ToLowerInvariant();
        if (HasPrecipKeywords(shortLower))
            return shortTrim;

        if (!IsSmokeHazeOrFog(shortLower))
            return shortTrim;

        var detailed = detailedForecast ?? "";
        var detailedLower = detailed.ToLowerInvariant();
        if (HasPrecipKeywords(detailedLower))
        {
            if (detailedLower.Contains("thunder"))
                return $"{shortTrim}, storms";
            if (detailedLower.Contains("shower"))
                return $"{shortTrim}, showers";
            if (detailedLower.Contains("rain") || detailedLower.Contains("drizzle"))
                return $"{shortTrim}, rain";
            return $"{shortTrim}, precip";
        }

        if (precipPercent is >= 50)
            return $"{shortTrim}, precip likely";

        return shortTrim;
    }

    private static string Combine(string? shortForecast, string? detailedForecast)
    {
        var s = shortForecast?.Trim() ?? "";
        var d = detailedForecast?.Trim() ?? "";
        if (s.Length == 0)
            return d.ToLowerInvariant();
        if (d.Length == 0)
            return s.ToLowerInvariant();
        return $"{s} {d}".ToLowerInvariant();
    }

    private static bool HasPrecipKeywords(string lower) =>
        lower.Contains("thunder")
        || lower.Contains("rain")
        || lower.Contains("drizzle")
        || lower.Contains("shower")
        || lower.Contains("snow")
        || lower.Contains("flurries")
        || lower.Contains("sleet")
        || lower.Contains("freezing rain");

    private static bool IsSmokeHazeOrFog(string lower) =>
        lower.Contains("smoke")
        || lower.Contains("haze")
        || lower.Contains("ash")
        || lower.Contains("fog");
}
