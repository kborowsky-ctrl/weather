using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WeatherWizard.Models;
using Windows.UI;

namespace WeatherWizard.Services;

public static class AppTheme
{
    public static void Apply(AppSettings settings)
    {
        var dark = IsDark(settings);

        if (App.Current.ThemeScope is FrameworkElement root)
        {
            root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
            root.InvalidateMeasure();
            root.InvalidateArrange();
        }

        // Mutate existing brush instances so controls that already resolved ThemeResource update.
        TrySetBrushColor(
            "AppPageBackgroundBrush",
            dark ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255));
        TrySetBrushColor(
            "AppSectionBorderBrush",
            dark ? Color.FromArgb(255, 90, 90, 90) : Color.FromArgb(255, 189, 189, 189));
        TrySetBrushColor(
            "AppBodyTextBrush",
            dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0));
        TrySetBrushColor(
            "AppMutedTextBrush",
            dark ? Color.FromArgb(255, 207, 207, 207) : Color.FromArgb(255, 92, 92, 92));

        if (App.Current.MainWindow is { } w)
        {
            try
            {
                var fg = dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
                var tb = w.AppWindow.TitleBar;
                tb.ForegroundColor = fg;
                tb.ButtonForegroundColor = fg;
                tb.ButtonHoverForegroundColor = fg;
                tb.ButtonPressedForegroundColor = fg;
                tb.ButtonInactiveForegroundColor = dark ? Microsoft.UI.Colors.LightGray : Microsoft.UI.Colors.DimGray;
                tb.InactiveForegroundColor = dark ? Microsoft.UI.Colors.DarkGray : Microsoft.UI.Colors.Gray;
            }
            catch
            {
                // TitleBar color APIs can vary by OS/SDK; ignore if unsupported.
            }
        }
    }

    private static void TrySetBrushColor(string key, Color color)
    {
        var rd = Application.Current.Resources;
        if (rd.ContainsKey(key) && rd[key] is SolidColorBrush sb)
        {
            try
            {
                sb.Color = color;
                return;
            }
            catch
            {
                // Brush may be immutable in some cases; replace.
            }
        }

        rd[key] = new SolidColorBrush(color);
    }

    public static bool IsDark(AppSettings settings) =>
        string.Equals(settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase);
}
