namespace WeatherWizard.Models;

using WeatherWizard.Services;

/// <summary>Two-column rows for the "now" panel (left | right per row), plus a weather glyph for the graphic row.</summary>
public sealed record CurrentConditionsPanel(
    string Row1Left,
    string Row1Right,
    string Row2Left,
    string Row2Right,
    string Row3Left,
    string Row3Right,
    string Row4Left,
    string Row4Right,
    string Row5Left,
    string Row5Right,
    string ConditionEmoji,
    int WeatherCode,
    string PressureArrow = "",
    PressureTrendKind PressureTrend = PressureTrendKind.Unknown,
    DateTimeOffset? ObservationTime = null,
    DateTimeOffset? SunriseToday = null,
    DateTimeOffset? SunsetToday = null)
{
    public bool IsNighttime =>
        SolarTimeHelper.IsNight(ObservationTime ?? DateTimeOffset.Now, SunriseToday, SunsetToday);
    public static CurrentConditionsPanel Loading { get; } = new(
        "Loading…", "",
        "", "",
        "", "",
        "", "",
        "", "",
        "🌤️",
        -1);

    public static CurrentConditionsPanel Blank { get; } = new(
        "—", "—",
        "—", "—",
        "—", "—",
        "—", "—",
        "—", "—",
        "🌤️",
        -1);
}
