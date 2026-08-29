using Windows.UI;

namespace WeatherWizard.Services;

public static class SeasonalOutlookColors
{
    public static Color BackgroundFor(string? seasonCode) =>
        seasonCode?.Trim().ToUpperInvariant() switch
        {
            "DJF" => Color.FromArgb(255, 255, 255, 255),   // winter — white
            "SON" => Color.FromArgb(255, 255, 140, 0),     // fall — orange
            "MAM" => Color.FromArgb(255, 180, 230, 180),   // spring — light green
            "JJA" => Color.FromArgb(255, 0, 100, 0),       // summer — dark green
            _ => Color.FromArgb(255, 240, 240, 240),
        };

    public static Color ForegroundFor(string? seasonCode) =>
        seasonCode?.Trim().ToUpperInvariant() switch
        {
            "JJA" => Color.FromArgb(255, 255, 255, 255),   // white on dark green
            _ => Color.FromArgb(255, 0, 0, 0),
        };
}
