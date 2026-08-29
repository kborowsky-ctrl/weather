using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WeatherWizard.Models;
using WeatherWizard.Services;
using Windows.Graphics;

namespace WeatherWizard.Views;

/// <summary>Shows NWS alert description/instruction text in-app (no browser).</summary>
public sealed class AlertDetailsWindow : Window
{
    public AlertDetailsWindow(IReadOnlyList<WeatherAlertItem> alerts, string? locationLabel = null)
    {
        var title = string.IsNullOrWhiteSpace(locationLabel)
            ? "Weather alert details"
            : $"Weather alert — {locationLabel}";
        Title = title;

        var body = NwsAlertDetailFormatter.FormatAll(alerts);

        var scroll = new ScrollViewer
        {
            Padding = new Thickness(16),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ZoomMode = ZoomMode.Disabled,
        };

        var text = new TextBlock
        {
            Text = body,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.WrapWholeWords,
            FontSize = 13,
            FontFamily = new FontFamily("Consolas, Cascadia Mono, Courier New"),
            LineHeight = 20,
        };

        // Theme brushes may not apply on a new Window until resources resolve; bind after load.
        scroll.Content = text;
        Content = new Border
        {
            Child = scroll,
        };

        Activated += (_, _) =>
        {
            try
            {
                if (Application.Current.Resources.TryGetValue("AppPageBackgroundBrush", out var bg) && bg is Brush bgBrush)
                    ((Border)Content).Background = bgBrush;
                if (Application.Current.Resources.TryGetValue("AppBodyTextBrush", out var fg) && fg is Brush fgBrush)
                    text.Foreground = fgBrush;
            }
            catch
            {
                // Fall back to system defaults.
            }
        };

        try
        {
            AppWindow.Resize(new SizeInt32(560, 640));
        }
        catch
        {
            // Ignore if AppWindow not ready.
        }
    }
}
