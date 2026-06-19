namespace WeatherWizard.Services;

/// <summary>WMO weather interpretation codes (Open-Meteo).</summary>
public static class WeatherCodeInterpreter
{
    public static string Describe(int code) => code switch
    {
        0 => "Clear",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Depositing rime fog",
        51 => "Light drizzle",
        53 => "Drizzle",
        55 => "Dense drizzle",
        56 => "Freezing drizzle",
        57 => "Freezing drizzle",
        61 => "Slight rain",
        63 => "Rain",
        65 => "Heavy rain",
        66 => "Freezing rain",
        67 => "Freezing rain",
        71 => "Slight snow",
        73 => "Snow",
        75 => "Heavy snow",
        77 => "Snow grains",
        80 => "Rain showers",
        81 => "Rain showers",
        82 => "Violent rain showers",
        85 => "Snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 => "Thunderstorm w/ hail",
        99 => "Thunderstorm w/ heavy hail",
        _ => "Conditions",
    };

    /// <summary>Short label for compact one-line forecast.</summary>
    public static string Short(int code) => code switch
    {
        0 => "Sunny",
        1 => "Mostly sunny",
        2 => "Partly cloudy",
        3 => "Cloudy",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        56 or 57 => "Ice mix",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Ice rain",
        71 or 73 or 75 or 77 => "Snow",
        80 or 81 or 82 => "Showers",
        85 or 86 => "Snow shwrs",
        95 or 96 or 99 => "Storm",
        _ => "Mix",
    };

    /// <summary>Single emoji for a compact “now” graphic (Open-Meteo WMO codes).</summary>
    public static string Emoji(int code) => EmojiCore(code);

    /// <summary>Day/night aware emoji; clear/partly-clear codes use moon phase after sunset.</summary>
    public static string Emoji(int code, bool isNight, DateTimeOffset at)
    {
        if (isNight && UsesSunDisc(code))
            return MoonPhaseCalculator.Emoji(at);

        return EmojiCore(code);
    }

    public static bool UsesSunDisc(int code) => code is 0 or 1 or 2;

    private static string EmojiCore(int code) => code switch
    {
        0 => "☀️",
        1 => "🌤️",
        2 => "⛅",
        3 => "☁️",
        45 or 48 => "🌫️",
        51 or 53 or 55 => "🌦️",
        56 or 57 => "🌧️",
        61 or 63 or 65 => "🌧️",
        66 or 67 => "🌨️",
        71 or 73 or 75 or 77 => "❄️",
        80 or 81 or 82 => "🌦️",
        85 or 86 => "🌨️",
        95 or 96 or 99 => "⛈️",
        _ => "🌤️",
    };
}
