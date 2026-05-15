using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WeatherWizard.Services;
using WeatherWizard.ViewModels;
using Windows.Storage.Streams;

namespace WeatherWizard.Views;

public sealed partial class LocationWeatherView : UserControl
{
    private LocationWeatherViewModel? _vm;
    private IRandomAccessStream? _heldRadarStream;

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

        // Scale height with width so the radar uses the full card width; UniformToFill crops vertically.
        var target = Math.Round(Math.Clamp(w / 1.08, 120, 460));
        if (double.IsNaN(host.Height) || Math.Abs(host.Height - target) > 0.5)
            host.Height = target;
    }

    public void Attach(LocationWeatherViewModel vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
        SyncAlertLinkVisibility();
        SyncErrorInfo();
        _ = NavigateMapAsync();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LocationWeatherViewModel.AlertLink) or nameof(LocationWeatherViewModel.HasAlertLink))
            SyncAlertLinkVisibility();

        if (e.PropertyName is nameof(LocationWeatherViewModel.ErrorBanner) or nameof(LocationWeatherViewModel.HasError))
            SyncErrorInfo();

        if (e.PropertyName is nameof(LocationWeatherViewModel.RadarStamp))
            _ = NavigateMapAsync();
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

        var uri = MapUrlBuilder.TryBuildNwsRadarUri(_vm.Location);
        if (uri is null)
        {
            MapRasterImage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            RadarPlaceholder.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            return;
        }

        RadarPlaceholder.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        MapRasterImage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

        var url = AppendCacheBuster(uri);
        await LoadNwsRadarGifAsync(url).ConfigureAwait(true);
    }

    private static string AppendCacheBuster(Uri uri)
    {
        var url = uri.AbsoluteUri;
        return url + (uri.Query.Length > 0 ? "&" : "?") + "cb=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private async Task LoadNwsRadarGifAsync(string url)
    {
        _heldRadarStream?.Dispose();
        _heldRadarStream = null;
        MapRasterImage.Source = null;

        try
        {
            var bytes = await App.Current.Http.Client.GetByteArrayAsync(url).ConfigureAwait(true);
            if (bytes.Length == 0)
            {
                MapRasterImage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                RadarPlaceholder.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                return;
            }

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
        }
        catch
        {
            _heldRadarStream?.Dispose();
            _heldRadarStream = null;
            MapRasterImage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            RadarPlaceholder.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
    }
}
