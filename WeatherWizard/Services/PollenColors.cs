using WeatherWizard.Models;
using Windows.UI;

namespace WeatherWizard.Services;

public static class PollenColors
{
    public static Color BackgroundFor(PollenReport report) => report.IsHigh
        ? Color.FromArgb(255, 0xF9, 0xA8, 0x25)   // amber
        : Color.FromArgb(255, 0xFF, 0xEB, 0x3B);  // yellow

    public static Color ForegroundFor(PollenReport report) => Color.FromArgb(255, 0, 0, 0);
}
