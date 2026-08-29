using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WeatherWizard.Services;
using Windows.Graphics;

namespace WeatherWizard.Views;

/// <summary>Shows CPC seasonal temp/precip summary and regional outlook map crops.</summary>
public sealed class SeasonalOutlookDetailsWindow : Window
{
    public SeasonalOutlookDetailsWindow(SeasonalOutlookSnapshot snapshot, string? locationLabel = null)
    {
        var title = string.IsNullOrWhiteSpace(locationLabel)
            ? $"{snapshot.Target.DisplayName} outlook"
            : $"{snapshot.Target.DisplayName} outlook — {locationLabel}";
        Title = title;

        var summary = new TextBlock
        {
            Text = snapshot.SummaryText,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.WrapWholeWords,
            FontSize = 13,
            LineHeight = 20,
            Margin = new Thickness(0, 0, 0, 12),
        };

        var tempHeader = new TextBlock
        {
            Text = "Temperature outlook (regional)",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        var tempImage = CreateMapImage(snapshot.TemperatureMapUri);

        var precipHeader = new TextBlock
        {
            Text = "Precipitation outlook (regional)",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 6),
        };
        var precipImage = CreateMapImage(snapshot.PrecipitationMapUri);

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(summary);
        stack.Children.Add(tempHeader);
        stack.Children.Add(tempImage);
        stack.Children.Add(precipHeader);
        stack.Children.Add(precipImage);

        var scroll = new ScrollViewer
        {
            Padding = new Thickness(16),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ZoomMode = ZoomMode.Disabled,
            Content = stack,
        };

        Content = new Border { Child = scroll };

        Activated += (_, _) =>
        {
            try
            {
                if (Application.Current.Resources.TryGetValue("AppPageBackgroundBrush", out var bg) && bg is Brush bgBrush)
                    ((Border)Content).Background = bgBrush;
                if (Application.Current.Resources.TryGetValue("AppBodyTextBrush", out var fg) && fg is Brush fgBrush)
                {
                    summary.Foreground = fgBrush;
                    tempHeader.Foreground = fgBrush;
                    precipHeader.Foreground = fgBrush;
                }
            }
            catch
            {
                // Fall back to system defaults.
            }
        };

        try
        {
            AppWindow.Resize(new SizeInt32(600, 720));
        }
        catch
        {
            // Ignore if AppWindow not ready.
        }
    }

    private static Image CreateMapImage(Uri uri)
    {
        var bmp = new BitmapImage();
        bmp.UriSource = uri;
        return new Image
        {
            Source = bmp,
            Stretch = Stretch.Uniform,
            MaxHeight = 320,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }
}
