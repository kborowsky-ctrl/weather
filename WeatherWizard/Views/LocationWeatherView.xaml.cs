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
    private int _radarPixelWidth;
    private int _radarPixelHeight;
    private double? _radarSiteLat;
    private double? _radarSiteLon;
    private string? _radarSiteIdLoaded;

    private const double RadarZoomScale = 2.6;

    private enum RadarFrameKind { NwsRegional, NwsZoomed, Custom }

    private sealed record RadarFrame(RadarFrameKind Kind, string ImageUrl, string Hint);

    public string VersionDisplay => AppVersion.Display;

    public event EventHandler? ContentLayoutChanged;

    public LocationWeatherView()
    {
        InitializeComponent();
    }

    private void WeatherContentStack_SizeChanged(object sender, SizeChangedEventArgs e) =>
        NotifyContentLayoutChanged();

    private void NotifyContentLayoutChanged() =>
        ContentLayoutChanged?.Invoke(this, EventArgs.Empty);

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
        NotifyContentLayoutChanged();
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
        SyncOutlookBadges();
        SyncErrorInfo();
        SyncPressureArrow();
        _ = NavigateMapAsync();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocationWeatherViewModel.CurrentConditions))
            SyncPressureArrow();

        if (e.PropertyName is nameof(LocationWeatherViewModel.ActiveAlerts) or nameof(LocationWeatherViewModel.HasAlertDetails))
            SyncAlertLinkVisibility();

        if (e.PropertyName is nameof(LocationWeatherViewModel.SeasonalOutlook)
            or nameof(LocationWeatherViewModel.HasSeasonalOutlook)
            or nameof(LocationWeatherViewModel.SeasonalOutlookLinkText)
            or nameof(LocationWeatherViewModel.WeekAheadThreats)
            or nameof(LocationWeatherViewModel.HasWeekAheadThreat)
            or nameof(LocationWeatherViewModel.WeekAheadThreatBadgeText))
            SyncOutlookBadges();

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

        AlertDetailsLink.Visibility = _vm.HasAlertDetails
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AlertDetailsLink_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null || !_vm.HasAlertDetails)
            return;

        var win = new AlertDetailsWindow(_vm.ActiveAlerts, _vm.Location.TabLabel);
        win.Activate();
    }

    /// <summary>One badge at a time: a week-ahead threat takes the seasonal outlook's spot while active.</summary>
    private void SyncOutlookBadges()
    {
        if (_vm is null)
            return;

        SeasonalOutlookLink.Content = _vm.SeasonalOutlookLinkText;

        if (_vm.WeekAheadThreats?.Top is { } threat)
        {
            WeekAheadThreatText.Text = _vm.WeekAheadThreatBadgeText;
            WeekAheadThreatBadge.Background = new SolidColorBrush(WeekAheadThreatColors.BackgroundFor(threat.Kind));
            var fg = new SolidColorBrush(WeekAheadThreatColors.ForegroundFor(threat.Kind));
            WeekAheadThreatLink.Foreground = fg;
            WeekAheadThreatText.Foreground = fg;
            WeekAheadThreatBadge.Visibility = Visibility.Visible;
            SeasonalOutlookBadge.Visibility = Visibility.Collapsed;
            return;
        }

        WeekAheadThreatBadge.Visibility = Visibility.Collapsed;

        if (_vm.HasSeasonalOutlook && _vm.SeasonalOutlook is { } snap)
        {
            var code = snap.Target.Code;
            SeasonalOutlookBadge.Background = new SolidColorBrush(SeasonalOutlookColors.BackgroundFor(code));
            SeasonalOutlookLink.Foreground = new SolidColorBrush(SeasonalOutlookColors.ForegroundFor(code));
            SeasonalOutlookBadge.BorderBrush = string.Equals(code, "DJF", StringComparison.OrdinalIgnoreCase)
                ? Application.Current.Resources["AppSectionBorderBrush"] as Brush
                : null;
            SeasonalOutlookBadge.BorderThickness = string.Equals(code, "DJF", StringComparison.OrdinalIgnoreCase)
                ? new Thickness(1)
                : new Thickness(0);
            SeasonalOutlookBadge.Visibility = Visibility.Visible;
        }
        else
        {
            SeasonalOutlookBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void SeasonalOutlookLink_Click(object sender, RoutedEventArgs e)
    {
        if (_vm?.SeasonalOutlook is null)
            return;

        var win = new SeasonalOutlookDetailsWindow(_vm.SeasonalOutlook, _vm.Location.TabLabel);
        win.Activate();
    }

    private void WeekAheadThreatLink_Click(object sender, RoutedEventArgs e)
    {
        if (_vm?.WeekAheadThreats is not { Top: not null } snapshot)
            return;

        var win = new WeekAheadThreatWindow(snapshot, _vm.Location.TabLabel);
        win.Activate();
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
        _radarPixelWidth = 0;
        _radarPixelHeight = 0;
        _radarSiteLat = null;
        _radarSiteLon = null;
        _radarSiteIdLoaded = null;

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

        await EnsureRadarSiteCoordinatesAsync().ConfigureAwait(true);

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
        foreach (var url in _vm.Location.CustomRadarImageUrls)
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

    private void ApplyCurrentRadarFramePresentation()
    {
        if (_radarFrameIndex < 0 || _radarFrameIndex >= _radarFrames.Count)
            return;

        ApplyRadarFramePresentation(_radarFrames[_radarFrameIndex]);
    }

    private async Task EnsureRadarSiteCoordinatesAsync()
    {
        if (_vm is null)
            return;

        var id = _vm.Location.NwsRadarStation;
        if (string.IsNullOrWhiteSpace(id))
            return;

        var normalized = id.Trim().ToUpperInvariant();

        // Prefer persisted site coords so local zoom can pan to the zip/location immediately.
        if (_vm.Location.NwsRadarStationLat is double savedLat
            && _vm.Location.NwsRadarStationLon is double savedLon
            && IsPlausibleRadarCoordinate(savedLat, savedLon)
            && string.Equals(_vm.Location.NwsRadarStation, id, StringComparison.OrdinalIgnoreCase))
        {
            _radarSiteIdLoaded = normalized;
            _radarSiteLat = savedLat;
            _radarSiteLon = savedLon;
            return;
        }

        if (string.Equals(_radarSiteIdLoaded, normalized, StringComparison.OrdinalIgnoreCase)
            && _radarSiteLat is double existingLat
            && _radarSiteLon is double existingLon
            && IsPlausibleRadarCoordinate(existingLat, existingLon))
            return;

        _radarSiteIdLoaded = normalized;
        var coords = await App.Current.NwsRadarStations.TryGetCoordinatesAsync(normalized)
            .ConfigureAwait(true);
        if (coords is { } c && IsPlausibleRadarCoordinate(c.Lat, c.Lon))
        {
            _radarSiteLat = c.Lat;
            _radarSiteLon = c.Lon;
            _vm.Location.NwsRadarStationLat = c.Lat;
            _vm.Location.NwsRadarStationLon = c.Lon;
            try
            {
                await App.Current.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
            }
            catch
            {
                // Non-fatal — zoom still works for this session.
            }
        }
        else
        {
            _radarSiteLat = null;
            _radarSiteLon = null;
        }
    }

    private static bool IsPlausibleRadarCoordinate(double lat, double lon) =>
        lat is >= 15 and <= 72 && lon is >= -180 and <= -50;

    private async Task ShowRadarFrameAsync(RadarFrame frame)
    {
        if (!_radarAvailable)
            return;

        try
        {
            if (frame.Kind == RadarFrameKind.NwsZoomed)
                await EnsureRadarSiteCoordinatesAsync().ConfigureAwait(true);

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

    private void ApplyRadarFramePresentation(RadarFrame frame)
    {
        if (!_radarAvailable || MapRasterImage.Source is null)
            return;

        if (frame.Kind == RadarFrameKind.NwsZoomed)
        {
            var w = RadarImageHost.ActualWidth;
            var h = RadarImageHost.ActualHeight;
            if (w <= 0 || h <= 0)
                return;

            var pw = _radarPixelWidth;
            var ph = _radarPixelHeight;
            if (pw <= 0 || ph <= 0)
            {
                if (MapRasterImage.Source is BitmapImage bmp && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
                {
                    pw = bmp.PixelWidth;
                    ph = bmp.PixelHeight;
                }
                else
                {
                    return;
                }
            }

            var (srcX, srcY) = ResolveZoomSourcePixels(pw, ph);

            // Size + margin placement (no RenderTransform): source focus lands on host center.
            var layout = NwsRadarLocalZoom.LayoutZoomedImage(
                w, h, pw, ph, srcX, srcY, RadarZoomScale);

            MapRasterImage.RenderTransform = null;
            MapRasterImage.Stretch = Stretch.Fill;
            MapRasterImage.HorizontalAlignment = HorizontalAlignment.Left;
            MapRasterImage.VerticalAlignment = VerticalAlignment.Top;
            MapRasterImage.Width = layout.Width;
            MapRasterImage.Height = layout.Height;
            MapRasterImage.Margin = new Thickness(layout.MarginLeft, layout.MarginTop, 0, 0);
        }
        else
        {
            MapRasterImage.Width = double.NaN;
            MapRasterImage.Height = double.NaN;
            MapRasterImage.Margin = new Thickness(0);
            MapRasterImage.RenderTransform = null;
            MapRasterImage.HorizontalAlignment = HorizontalAlignment.Center;
            MapRasterImage.VerticalAlignment = VerticalAlignment.Center;
            MapRasterImage.Stretch = Stretch.UniformToFill;
            UpdateRadarHostHeight();
        }

        RadarViewHint.Text = frame.Hint;
    }

    private (double X, double Y) ResolveZoomSourcePixels(int pw, int ph)
    {
        if (_vm is not null
            && _radarSiteLat is double rLat
            && _radarSiteLon is double rLon
            && IsPlausibleRadarCoordinate(rLat, rLon))
        {
            return NwsRadarLocalZoom.FocusInSourcePixels(
                pw,
                ph,
                _vm.Location.Latitude,
                _vm.Location.Longitude,
                rLat,
                rLon);
        }

        // Without site coords we can only center the radar disk (not the zip).
        return NwsRadarLocalZoom.RadarDiskCenterPixels(pw, ph);
    }

    private void UpdateRadarHostHeight()
    {
        var w = RadarImageHost.ActualWidth;
        if (w <= 0 || double.IsNaN(w))
            return;

        var target = Math.Round(Math.Clamp(w / 1.08, 120, 460));

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
            _radarPixelWidth = bmp.PixelWidth;
            _radarPixelHeight = bmp.PixelHeight;
            UpdateRadarHostHeight();
            ApplyCurrentRadarFramePresentation();
            UpdateRadarClip();
            NotifyContentLayoutChanged();
        };
    }
}
