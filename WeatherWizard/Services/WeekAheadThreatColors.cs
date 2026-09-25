using WeatherWizard.Models;
using Windows.UI;

namespace WeatherWizard.Services;

public static class WeekAheadThreatColors
{
    public static Color BackgroundFor(ThreatKind kind) => kind switch
    {
        ThreatKind.Severe => Color.FromArgb(255, 0xFF, 0x57, 0x22),                            // red-orange
        ThreatKind.WinterStorm or ThreatKind.Ice => Color.FromArgb(255, 0xB3, 0xE5, 0xFC),     // light blue
        ThreatKind.Flooding or ThreatKind.HeavyRain => Color.FromArgb(255, 0x00, 0x89, 0x7B),  // teal
        ThreatKind.HighWind => Color.FromArgb(255, 0x9E, 0x9E, 0x9E),                          // gray
        ThreatKind.Heat => Color.FromArgb(255, 0xD3, 0x2F, 0x2F),                              // red
        ThreatKind.Cold or ThreatKind.Frost => Color.FromArgb(255, 0x7E, 0x57, 0xC2),          // purple
        _ => Color.FromArgb(255, 0xF0, 0xF0, 0xF0),
    };

    public static Color ForegroundFor(ThreatKind kind) => kind switch
    {
        ThreatKind.WinterStorm or ThreatKind.Ice or ThreatKind.HighWind => Color.FromArgb(255, 0, 0, 0),
        _ => Color.FromArgb(255, 255, 255, 255),
    };
}
