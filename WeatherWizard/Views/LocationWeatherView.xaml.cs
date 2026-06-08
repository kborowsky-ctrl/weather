using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WeatherWizard.Models;
using WeatherWizard.Services;
using WeatherWizard.ViewModels;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace WeatherWizard.Views;

public sealed partial class LocationWeatherView : UserControl
{
    private LocationWeatherViewModel? _vm;
    private IRandomAccessStream? _heldRadarStream;
    private DispatcherTimer? _pressureBlinkTimer;
    private DispatcherTimer? _radarFlipTimer;
    private bool _radarAvailable;
    private List<RadarFrame> _radarFrames = [];
    private int _radarFrameIndex;
    private string? _loadedRadarUrl;
    private bool _appSettingsHandlerWired;

    private const double RadarZoomScale = 2.6;

    private enum RadarFrameKind { NwsRegional, NwsZoomed, Custom }

    private sealed record RadarFrame(RadarFrameKind Kind, string ImageUrl, string Hint);

    public string VersionDisplay => AppVersion.Display;

    public LocationWeatherView()
    {
        InitializeComponent();
    }

    private void RefreshLink_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        global::WeatherWizard.App.Current.RequestMainRefresh();
    }

    private void RadarImageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not FrameworkElement host)
            return;

        var w = host.ActualWidth;
        if (w <= 0 || double.IsNaN(w))
            return;

        UpdateRadarHostHeight();
        UpdateRadarClip();
        ApplyCurrentRadarFramePresentation();
    }

    public void Attach(LocationWeatherViewModel vm)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            StopPressureBlink();
            StopRadarFlipTimer();
        }

        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;

        if (!_appSettingsHandlerWired)
        {
            App.Current.Locations.Changed += OnAppSettingsChanged;
            _appSettingsHandlerWired = true;
        }

        SyncAlertLinkVisibility();
        SyncErrorInfo();
        SyncPressureArrow();
        _ = NavigateMapAsync();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocationWeatherViewModel.CurrentConditions))
            SyncPressureArrow();

        if (e.PropertyName is nameof(LocationWeatherViewModel.AlertLink) or nameof(LocationWeatherViewModel.HasAlertLink))
            SyncAlertLinkVisibility();

        if (e.PropertyName is nameof(LocationWeatherViewModel.ErrorBanner) or nameof(LocationWeatherViewModel.HasError))
            SyncErrorInfo();

        if (e.PropertyName is nameof(LocationWeatherViewModel.RadarStamp))
            _ = RestartRadarCarouselAsync();
    }

    private void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => _ = RestartRadarCarouselAsync());
    }

    private void SyncAlertLinkVisibility()
    {
        if (_vm is null)
            return;

        AlertDetailsLink.Visibility = _vm.HasAlertLink ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (_vm.AlertLink is not null)
            AlertDetailsLink.NavigateUri = _vm.AlertLink;
    }

    private void SyncErrorInfo()
    {
        if (_vm is null)
            return;

        var has = _vm.HasError;
        ErrorInfo.IsOpen = has;
        ErrorInfo.Message = has ? _vm.ErrorBanner : string.Empty;
    }

    private void SyncPressureArrow()
    {
        if (_vm is null)
            return;

        var cc = _vm.CurrentConditions;
        var arrow = cc.PressureArrow ?? "";
        PressureArrowBlock.Text = arrow;
        PressureArrowBlock.Visibility = string.IsNullOrEmpty(arrow)
            ? Visibility.Collapsed
            : Visibility.Visible;

        StopPressureBlink();

        var bodyBrush = Application.Current.Resources["AppBodyTextBrush"] as Brush;
        var redBrush = Application.Current.Resources["AppAlertRedBrush"] as Brush;
        PressureArrowBlock.Foreground = bodyBrush;
        PressureArrowBlock.Opacity = 1;

        switch (cc.PressureTrend)
        {
            case PressureTrendKind.FallingMild:
                StartPressureBlink(TimeSpan.FromMilliseconds(900));
                break;
            case PressureTrendKind.FallingModerate:
                StartPressureBlink(TimeSpan.FromMilliseconds(450));
                break;
            case PressureTrendKind.FallingSevere:
                if (redBrush is not null)
                    PressureArrowBlock.Foreground = redBrush;
                StartPressureBlink(TimeSpan.FromMilliseconds(450));
                break;
        }
    }

    private void StartPressureBlink(TimeSpan interval)
    {
        _pressureBlinkTimer = new DispatcherTimer { Interval = interval };
        var visible = true;
        _pressureBlinkTimer.Tick += (_, _) =>
        {
            visible = !visible;
            PressureArrowBlock.Opacity = visible ? 1.0 : 0.12;
        };
        _pressureBlinkTimer.Start();
    }

    private void StopPressureBlink()
    {
        if (_pressureBlinkTimer is null)
            return;

        _pressureBlinkTimer.Stop();
        _pressureBlinkTimer = null;
        PressureArrowBlock.Opacity = 1.0;
    }

    private Task RestartRadarCarouselAsync() => NavigateMapAsync();

    private async Task NavigateMapAsync()
    {
        if (_vm is null)
            return;

        try
        {
            await NavigateMapContentAsync().ConfigureAwait(true);
        }
        catch
        {
            await Task.Delay(250).ConfigureAwait(true);
            try
            {
                await NavigateMapContentAsync().ConfigureAwait(true);
            }
            catch
            {
                // ignored
            }
        }
    }

    private async Task NavigateMapContentAsync()
    {
        if (_vm is null)
            return;

        _heldRadarStream?.Dispose();
        _heldRadarStream = null;
        MapRasterImage.Source = null;
        _loadedRadarUrl = null;

        _radarFrames = BuildRadarFrames();
        if (_radarFrames.Count == 0)
        {
            _radarAvailable = false;
            StopRadarFlipTimer();
            MapRasterImage.Visibility = Visibility.Collapsed;
            RadarPlaceholder.Visibility = Visibility.Visible;
            RadarViewHint.Text = string.Empty;
            return;
        }

        _radarAvailable = true;
        RadarPlaceholder.Visibility = Visibility.Collapsed;
        MapRasterImage.Visibility = Visibility.Visible;

        _radarFrameIndex = 0;
        await ShowRadarFrameAsync(_radarFrames[0]).ConfigureAwait(true);

        if (_radarFrames.Count > 1)
            StartRadarFlipTimer();
        else
            StopRadarFlipTimer();
    }

    private List<RadarFrame> BuildRadarFrames()
    {
        var frames = new List<RadarFrame>();
        if (_vm is null)
            return frames;

        var nws = MapUrlBuilder.TryBuildNwsRadarUri(_vm.Location);
        if (nws is not null)
        {
            var url = nws.AbsoluteUri;
            frames.Add(new RadarFrame(RadarFrameKind.NwsRegional, url, "Regional view"));
            frames.Add(new RadarFrame(RadarFrameKind.NwsZoomed, url, "Local zoom"));
        }

        var n = 1;
        foreach (var url in App.Current.Locations.Settings.CustomRadarImageUrls)
        {
            frames.Add(new RadarFrame(RadarFrameKind.Custom, url, $"Custom {n}"));
            n++;
        }

        return frames;
    }

    private void StartRadarFlipTimer()
    {
        if (!_radarAvailable || _radarFrames.Count <= 1)
            return;

        StopRadarFlipTimer();

        _radarFlipTimer = new DispatcherTimer
        {
            Interval = App.Current.Locations.Settings.RadarFlipInterval,
        };
        _radarFlipTimer.Tick += OnRadarFlipTick;
        _radarFlipTimer.Start();
    }

    private void OnRadarFlipTick(object? sender, object e)
    {
        if (_radarFrames.Count <= 1)
            return;

        _radarFrameIndex = (_radarFrameIndex + 1) % _radarFrames.Count;
        _ = ShowRadarFrameAsync(_radarFrames[_radarFrameIndex]);
    }

    private void StopRadarFlipTimer()
    {
        if (_radarFlipTimer is null)
            return;

        _radarFlipTimer.Tick -= OnRadarFlipTick;
        _radarFlipTimer.Stop();
        _radarFlipTimer = null;
    }

    private async Task ShowRadarFrameAsync(RadarFrame frame)
    {
        if (!_radarAvailable)
            return;

        try
        {
            if (!string.Equals(_loadedRadarUrl, frame.ImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                var url = AppendCacheBuster(new Uri(frame.ImageUrl));
                await LoadRadarImageAsync(url).ConfigureAwait(true);
                _loadedRadarUrl = frame.ImageUrl;
            }

            ApplyRadarFramePresentation(frame);
        }
        catch
        {
            MapRasterImage.Visibility = Visibility.Collapsed;
            RadarPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void ApplyCurrentRadarFramePresentation()
    {
        if (_radarFrameIndex < 0 || _radarFrameIndex >= _radarFrames.Count)
            return;

        ApplyRadarFramePresentation(_radarFrames[_radarFrameIndex]);
    }

    private void ApplyRadarFramePresentation(RadarFrame frame)
    {
        if (!_radarAvailable || MapRasterImage.Source is null)
            return;

        if (frame.Kind == RadarFrameKind.NwsZoomed)
        {
            MapRasterImage.HorizontalAlignment = HorizontalAlignment.Stretch;
            MapRasterImage.VerticalAlignment = VerticalAlignment.Stretch;

            var w = MapRasterImage.ActualWidth;
            var h = MapRasterImage.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                w = RadarImageHost.ActualWidth;
                h = RadarImageHost.ActualHeight;
            }

            var cx = w > 0 ? w / 2 : 200;
            var cy = h > 0 ? h / 2 : 200;
            MapRasterImage.RenderTransform = new ScaleTransform
            {
                ScaleX = RadarZoomScale,
                ScaleY = RadarZoomScale,
                CenterX = cx,
                CenterY = cy,
            };
            MapRasterImage.Stretch = Stretch.Uniform;
        }
        else if (frame.Kind == RadarFrameKind.Custom)
        {
            MapRasterImage.RenderTransform = null;
            MapRasterImage.HorizontalAlignment = HorizontalAlignment.Center;
            MapRasterImage.VerticalAlignment = VerticalAlignment.Center;
            MapRasterImage.Stretch = Stretch.Uniform;
            UpdateRadarHostHeight();
        }
        else
        {
            MapRasterImage.RenderTransform = null;
            MapRasterImage.HorizontalAlignment = HorizontalAlignment.Stretch;
            MapRasterImage.VerticalAlignment = VerticalAlignment.Stretch;
            MapRasterImage.Stretch = Stretch.UniformToFill;
            UpdateRadarHostHeight();
        }

        RadarViewHint.Text = frame.Hint;
    }

    private void UpdateRadarHostHeight()
    {
        var w = RadarImageHost.ActualWidth;
        if (w <= 0 || double.IsNaN(w))
            return;

        double target;
        if (_radarFrameIndex >= 0
            && _radarFrameIndex < _radarFrames.Count
            && _radarFrames[_radarFrameIndex].Kind == RadarFrameKind.Custom
            && MapRasterImage.Source is BitmapImage bmp
            && bmp.PixelWidth > 0
            && bmp.PixelHeight > 0)
        {
            var aspect = (double)bmp.PixelHeight / bmp.PixelWidth;
            target = Math.Round(Math.Clamp(w * aspect, 120, 460));
        }
        else
        {
            // NWS GIF aspect; UniformToFill may crop edges for a filled card.
            target = Math.Round(Math.Clamp(w / 1.08, 120, 460));
        }

        if (double.IsNaN(RadarImageHost.Height) || Math.Abs(RadarImageHost.Height - target) > 0.5)
            RadarImageHost.Height = target;
    }

    private void UpdateRadarClip()
    {
        var w = RadarImageHost.ActualWidth;
        var h = RadarImageHost.ActualHeight;
        if (w <= 0 || h <= 0)
            return;

        RadarImageHost.Clip = new RectangleGeometry { Rect = new Rect(0, 0, w, h) };
    }

    private static string AppendCacheBuster(Uri uri)
    {
        var url = uri.AbsoluteUri;
        return url + (uri.Query.Length > 0 ? "&" : "?") + "cb=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private async Task LoadRadarImageAsync(string url)
    {
        _heldRadarStream?.Dispose();
        _heldRadarStream = null;
        MapRasterImage.Source = null;

        var bytes = await App.Current.Http.Client.GetByteArrayAsync(url).ConfigureAwait(true);
        if (bytes.Length == 0)
            throw new InvalidOperationException("Empty radar image.");

        var ras = new InMemoryRandomAccessStream();
        using (var output = ras.GetOutputStreamAt(0))
        {
            using var writer = new DataWriter(output);
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            await output.FlushAsync();
        }

        ras.Seek(0);
        _heldRadarStream = ras;

        var bmp = new BitmapImage();
        bmp.SetSource(ras);
        MapRasterImage.Source = bmp;
        bmp.ImageOpened += (_, _) =>
        {
            UpdateRadarHostHeight();
            ApplyCurrentRadarFramePresentation();
            UpdateRadarClip();
        };
    }
}
