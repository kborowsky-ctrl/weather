using System.Globalization;

namespace WeatherWizard.Services;

public static class ForecastDisplayFormat
{
    public static string OrdinalDate(DateOnly date)
    {
        var m = date.ToString("MMMM", CultureInfo.CurrentCulture);
        var d = date.Day;
        var suffix = d is 11 or 12 or 13
            ? "th"
            : (d % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        return $"{m} {d}{suffix}";
    }
}
