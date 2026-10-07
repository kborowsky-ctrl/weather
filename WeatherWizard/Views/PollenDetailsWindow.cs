using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WeatherWizard.Models;
using Windows.Graphics;

namespace WeatherWizard.Views;

/// <summary>Shows the pollen.com index (yesterday / today / tomorrow) and leading allergens.</summary>
public sealed class PollenDetailsWindow : Window
{
    public PollenDetailsWindow(PollenReport report, string? locationLabel = null)
    {
        Title = string.IsNullOrWhiteSpace(locationLabel) ? "Pollen" : $"Pollen — {locationLabel}";

        var sb = new StringBuilder();
        sb.AppendLine(report.DisplayLocation is { Length: > 0 } place
            ? $"Pollen index for {place} (ZIP {report.Zip})"
            : $"Pollen index for ZIP {report.Zip}");
        sb.AppendLine();

        foreach (var day in report.Days)
        {
            sb.AppendLine($"{day.Label}: {day.Index:0.0} of 12 ({day.Category})");
            if (day.Triggers.Count > 0)
                sb.AppendLine($"    Top allergens: {string.Join(", ", day.Triggers)}");
            sb.AppendLine();
        }

        sb.AppendLine("Scale: Low 0-2.4, Low-Medium 2.5-4.8, Medium 4.9-7.2, Medium-High 7.3-9.6, High 9.7-12.");
        sb.Append("Source: pollen.com.");

        var text = new TextBlock
        {
            Text = sb.ToString(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.WrapWholeWords,
            FontSize = 13,
            LineHeight = 20,
        };

        var scroll = new ScrollViewer
        {
            Padding = new Thickness(16),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = text,
        };

        Content = new Border { Child = scroll };

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
            AppWindow.Resize(new SizeInt32(460, 420));
        }
        catch
        {
            // Ignore if AppWindow not ready.
        }
    }
}
