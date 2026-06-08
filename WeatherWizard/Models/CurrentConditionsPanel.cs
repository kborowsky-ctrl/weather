namespace WeatherWizard.Models;

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
    PressureTrendKind PressureTrend = PressureTrendKind.Unknown)
{
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
