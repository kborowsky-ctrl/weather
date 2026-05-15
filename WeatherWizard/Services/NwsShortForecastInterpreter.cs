namespace WeatherWizard.Services;

/// <summary>Maps NWS <c>shortForecast</c> text to approximate WMO-style codes for emoji/icons.</summary>
public static class NwsShortForecastInterpreter
{
    public static int ApproximateWmoCode(string shortForecast)
    {
        if (string.IsNullOrWhiteSpace(shortForecast))
            return 0;

        var t = shortForecast.ToLowerInvariant();

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
        if (t.Contains("fog"))
            return 45;

        if (t.Contains("mostly sunny"))
            return 1;
        if (t.Contains("partly sunny") || t.Contains("partly cloudy"))
            return 2;
        if (t.Contains("mostly cloudy"))
            return 3;

        if (t.Contains("sunny") || (t.Contains("clear") && !t.Contains("cloud")))
            return 0;

        if (t.Contains("overcast") || t.Contains("cloudy"))
            return 3;

        return 2;
    }
}
