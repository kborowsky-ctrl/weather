using System.Drawing;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WeatherWizard.Services;
using Windows.Storage.Streams;
using UiImage = Microsoft.UI.Xaml.Controls.Image;

namespace WeatherWizard.Views;

/// <summary>
/// Condition glyph: emoji by day; light-grey vector moon phase at night for clear/partly-clear codes.
/// </summary>
public sealed class ConditionWeatherIcon : UserControl
{
    private readonly TextBlock _emoji;
    private readonly UiImage _moonImage;
    private Bitmap? _moonBitmap;

    public static readonly DependencyProperty WeatherCodeProperty =
        DependencyProperty.Register(nameof(WeatherCode), typeof(int), typeof(ConditionWeatherIcon),
            new PropertyMetadata(-1, OnVisualInputChanged));

    public static readonly DependencyProperty IsNighttimeProperty =
        DependencyProperty.Register(nameof(IsNighttime), typeof(bool), typeof(ConditionWeatherIcon),
            new PropertyMetadata(false, OnVisualInputChanged));

    public static readonly DependencyProperty PhaseTimeProperty =
        DependencyProperty.Register(nameof(PhaseTime), typeof(DateTimeOffset?), typeof(ConditionWeatherIcon),
            new PropertyMetadata(null, OnVisualInputChanged));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(ConditionWeatherIcon),
            new PropertyMetadata(34d, OnVisualInputChanged));

    public int WeatherCode
    {
        get => (int)GetValue(WeatherCodeProperty);
        set => SetValue(WeatherCodeProperty, value);
    }

    public bool IsNighttime
    {
        get => (bool)GetValue(IsNighttimeProperty);
        set => SetValue(IsNighttimeProperty, value);
    }

    public DateTimeOffset? PhaseTime
    {
        get => (DateTimeOffset?)GetValue(PhaseTimeProperty);
        set => SetValue(PhaseTimeProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public ConditionWeatherIcon()
    {
        _emoji = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _moonImage = new UiImage
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Stretch = Stretch.Uniform,
        };

        var host = new Grid();
        host.Children.Add(_emoji);
        host.Children.Add(_moonImage);
        Content = host;

        Loaded += (_, _) => UpdateVisual();
        SizeChanged += (_, _) => UpdateVisual();
    }

    private static void OnVisualInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConditionWeatherIcon self)
            self.UpdateVisual();
    }

    private void UpdateVisual()
    {
        var size = IconSize;
        if (size <= 0)
            size = 34;

        Width = size;
        Height = size;
        _emoji.FontSize = size * 0.92;
        _moonImage.Width = size;
        _moonImage.Height = size;

        if (IsNighttime && WeatherCodeInterpreter.UsesSunDisc(WeatherCode))
        {
            _emoji.Visibility = Visibility.Collapsed;
            _moonImage.Visibility = Visibility.Visible;

            var at = PhaseTime ?? DateTimeOffset.Now;
            var px = (int)Math.Clamp(Math.Round(size), 16, 128);
            _moonBitmap?.Dispose();
            _moonBitmap = MoonPhaseIconRenderer.CreateBitmap(
                px, at, MoonPhaseIconRenderer.UiPalette, litPortionOnly: true, weatherCode: WeatherCode);
            _moonImage.Source = BitmapToImageSource(_moonBitmap);
            return;
        }

        _moonImage.Visibility = Visibility.Collapsed;
        _emoji.Visibility = Visibility.Visible;
        _emoji.Text = WeatherCode >= 0
            ? WeatherCodeInterpreter.Emoji(WeatherCode)
            : WeatherCodeInterpreter.Emoji(2);
    }

    private static ImageSource BitmapToImageSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;

        var ras = new InMemoryRandomAccessStream();
        using (var output = ras.GetOutputStreamAt(0))
        {
            using var writer = new DataWriter(output);
            writer.WriteBytes(stream.ToArray());
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.FlushAsync().AsTask().GetAwaiter().GetResult();
        }

        ras.Seek(0);
        var image = new BitmapImage();
        image.SetSource(ras);
        return image;
    }
}
