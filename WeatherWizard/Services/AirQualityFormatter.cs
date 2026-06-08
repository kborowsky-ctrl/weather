namespace WeatherWizard.Services;

public static class AirQualityFormatter
{
    public static string FormatUsAqi(int? usAqi)
    {
        if (usAqi is null)
            return "—";

        var aqi = Math.Clamp(usAqi.Value, 0, 500);
        return $"AQI {aqi} {ShortLabel(aqi)}";
    }

    private static string ShortLabel(int aqi) => aqi switch
    {
        <= 50 => "Good",
        <= 100 => "Moderate",
        <= 150 => "Unhealthy SG",
        <= 200 => "Unhealthy",
        <= 300 => "Very Unhlthy",
        _ => "Hazardous",
    };
}
