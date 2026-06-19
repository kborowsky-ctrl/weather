namespace WeatherWizard.Services;

/// <summary>Approximate synodic moon phase for emoji and tray glyphs.</summary>
public static class MoonPhaseCalculator
{
    // Known new moon (UTC) for phase reference.
    private static readonly DateTime EpochNewMoonUtc = new(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);
    private const double SynodicDays = 29.530588853;

    /// <summary>Phase in [0, 1): 0 new, 0.25 first quarter, 0.5 full, 0.75 last quarter.</summary>
    public static double Phase01(DateTimeOffset at)
    {
        var days = (at.ToUniversalTime() - EpochNewMoonUtc).TotalDays;
        var phase = days / SynodicDays;
        phase -= Math.Floor(phase);
        if (phase < 0)
            phase += 1;
        return phase;
    }

    public static string Emoji(DateTimeOffset at)
    {
        var phase = Phase01(at);
        var idx = (int)Math.Floor(phase * 8 + 0.5) % 8;
        return idx switch
        {
            0 => "🌑",
            1 => "🌒",
            2 => "🌓",
            3 => "🌔",
            4 => "🌕",
            5 => "🌖",
            6 => "🌗",
            7 => "🌘",
            _ => "🌙",
        };
    }
}
