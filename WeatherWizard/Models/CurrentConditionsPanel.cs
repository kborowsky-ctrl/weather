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
    string ConditionEmoji)
{
    public static CurrentConditionsPanel Loading { get; } = new(
        "Loading…", "",
        "", "",
        "", "",
        "", "",
        "", "",
        "🌤️");

    public static CurrentConditionsPanel Blank { get; } = new(
        "—", "—",
        "—", "—",
        "—", "—",
        "—", "—",
        "—", "—",
        "🌤️");

    /// <summary>Sunrise / sunset line for compact layout.</summary>
    public string Row5SunLine
    {
        get
        {
            var L = (Row5Left ?? "").Trim();
            var R = (Row5Right ?? "").Trim();
            if (L.Length == 0 && R.Length == 0)
                return "—";
            if (R.Length == 0)
                return L;
            if (L.Length == 0)
                return R;
            return $"{L}   {R}";
        }
    }
}
