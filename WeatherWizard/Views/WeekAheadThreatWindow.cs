using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using WeatherWizard.Models;
using WeatherWizard.Services;
using Windows.Graphics;

namespace WeatherWizard.Views;

/// <summary>Shows week-ahead threat potential: flagged hazards, the local Hazardous Weather Outlook, and regional maps.</summary>
public sealed class WeekAheadThreatWindow : Window
{
    private const double MapWidth = 560;
    private const double MapHeight = 420;

    private readonly List<TextBlock> _themedText = [];

    public WeekAheadThreatWindow(WeekAheadThreatSnapshot snapshot, string? locationLabel = null)
    {
        Title = string.IsNullOrWhiteSpace(locationLabel)
            ? "Week-ahead threats"
            : $"Week-ahead threats — {locationLabel}";

        var stack = new StackPanel { Spacing = 0 };

        foreach (var threat in snapshot.Threats)
            stack.Children.Add(CreateThreatRow(threat));

        stack.Children.Add(Themed(new TextBlock
        {
            Text = "These are potential threats from longer-range outlooks, not forecasts or warnings. "
                + "Timing and location often shift as the event gets closer; check the forecast and alerts for updates.",
            FontSize = 11,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.8,
            Margin = new Thickness(0, 4, 0, 12),
        }));

        if (!string.IsNullOrWhiteSpace(snapshot.HazardOutlookText))
        {
            var header = snapshot.HazardOutlookOffice is { Length: > 0 } office
                ? $"Hazardous Weather Outlook (NWS {office})"
                : "Hazardous Weather Outlook";
            if (snapshot.HazardOutlookIssued is { } issued)
                header += $" — issued {issued.ToLocalTime():ddd MMM d h:mm tt}";
            stack.Children.Add(SectionHeader(header, topMargin: 4));
            stack.Children.Add(Themed(new TextBlock
            {
                Text = snapshot.HazardOutlookText,
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.WrapWholeWords,
                FontSize = 13,
                LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 12),
            }));
        }

        foreach (var map in snapshot.Maps)
        {
            stack.Children.Add(SectionHeader(map.Title, topMargin: 8));
            stack.Children.Add(CreateMap(map));
        }

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
                    foreach (var tb in _themedText)
                        tb.Foreground = fgBrush;
                }
            }
            catch
            {
                // Fall back to system defaults.
            }
        };

        try
        {
            AppWindow.Resize(new SizeInt32(620, 760));
        }
        catch
        {
            // Ignore if AppWindow not ready.
        }
    }

    private TextBlock Themed(TextBlock tb)
    {
        _themedText.Add(tb);
        return tb;
    }

    private TextBlock SectionHeader(string text, double topMargin) => Themed(new TextBlock
    {
        Text = text,
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextWrapping = TextWrapping.WrapWholeWords,
        Margin = new Thickness(0, topMargin, 0, 6),
    });

    private UIElement CreateThreatRow(WeekAheadThreat threat)
    {
        var chip = new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(WeekAheadThreatColors.BackgroundFor(threat.Kind)),
            Child = new TextBlock
            {
                Text = threat.Title,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(WeekAheadThreatColors.ForegroundFor(threat.Kind)),
            },
        };

        var detail = Themed(new TextBlock
        {
            Text = threat.Sources.Count > 0
                ? $"{threat.DateRange}  ·  {string.Join("; ", threat.Sources)}"
                : threat.DateRange,
            FontSize = 12,
            TextWrapping = TextWrapping.WrapWholeWords,
            IsTextSelectionEnabled = true,
        });

        var row = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 10) };
        row.Children.Add(chip);
        row.Children.Add(detail);
        return row;
    }

    private static UIElement CreateMap(ThreatMapPanel map)
    {
        var grid = new Grid { Width = MapWidth, Height = MapHeight };
        grid.Children.Add(CreateImage(map.BaseMapUri));
        grid.Children.Add(CreateImage(map.OverlayUri, opacity: 0.75));
        grid.Children.Add(new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(Colors.Black),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return new Viewbox
        {
            Stretch = Stretch.Uniform,
            MaxHeight = 360,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = grid,
        };
    }

    private static Image CreateImage(Uri uri, double opacity = 1.0) => new()
    {
        Source = new BitmapImage { UriSource = uri },
        Stretch = Stretch.Fill,
        Opacity = opacity,
    };
}
