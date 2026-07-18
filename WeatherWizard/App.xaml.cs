using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WeatherWizard.Services;
using WeatherWizard.Views;

namespace WeatherWizard;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? window;
    private Frame? _rootFrame;
    private Grid? _titleBarHost;
    private TaskTrayIcon? _trayIcon;

    public static new App Current => (App)Application.Current;

    /// <summary>Root host grid (title chrome + frame); theme applies here.</summary>
    public FrameworkElement? ThemeScope { get; private set; }

    public Grid? TitleChromeHost { get; private set; }

    private bool _suppressWindowFitResize;
    private bool _userSizedMainWindow;

    /// <summary>User dragged the window edge; stop auto height fitting for this session.</summary>
    public void NotifyMainWindowUserSized() => _userSizedMainWindow = true;

    /// <summary>Shrink window height to the active weather tab content (until the user resizes manually).</summary>
    public void FitMainWindowToContent()
    {
        if (_userSizedMainWindow || window is null || _rootFrame?.Content is not MainPage mainPage)
            return;

        _suppressWindowFitResize = true;
        try
        {
            WindowContentFitHelper.FitMainWindowHeight(window, mainPage, TitleChromeHost);
        }
        finally
        {
            _suppressWindowFitResize = false;
        }
    }

    /// <summary>Primary navigation frame (main / settings).</summary>
    public Frame? RootFrame => _rootFrame;

    public Window? MainWindow => window;

    public LocationRepository Locations { get; } = new();

    public HttpClientFactory Http { get; }

    public OpenMeteoGeocodingClient Geocoding { get; }

    public OpenMeteoAirQualityClient AirQuality { get; }

    public OpenMeteoForecastClient Forecast { get; }

    public NwsAlertsClient Nws { get; }

    public NwsPointsClient NwsPoints { get; }

    public NwsGridForecastClient NwsGridForecast { get; }

    public SpeechService Speech { get; }

    public AlertSpeechCoordinator AlertSpeech { get; }

    public GitHubUpdateChecker Updates { get; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        Http = new HttpClientFactory();
        Geocoding = new OpenMeteoGeocodingClient(Http);
        AirQuality = new OpenMeteoAirQualityClient(Http);
        Forecast = new OpenMeteoForecastClient(Http, AirQuality);
        Nws = new NwsAlertsClient(Http);
        NwsPoints = new NwsPointsClient(Http);
        NwsGridForecast = new NwsGridForecastClient(Http);
        Speech = new SpeechService();
        AlertSpeech = new AlertSpeechCoordinator(Speech);
        Updates = new GitHubUpdateChecker(Http);
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="e">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs e)
    {
        window ??= new Window();

        Grid host;
        if (window.Content is Grid existing && existing.Name == "WwRootHost")
        {
            host = existing;
            WireChromeReferences(host);
        }
        else
        {
            host = BuildWindowHost();
            window.Content = host;
            WireChromeReferences(host);
        }

        ThemeScope = host;

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = true;
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(true, true);
        }

        window.AppWindow.Title = "WeatherWizard";

        window.Closed -= OnWindowClosed;
        window.Closed += OnWindowClosed;

        window.AppWindow.Changed += OnAppWindowChanged;

        window.ExtendsContentIntoTitleBar = true;
        if (_titleBarHost is not null)
            window.SetTitleBar(_titleBarHost);

        await Locations.LoadAsync().ConfigureAwait(true);
        var startWithWindows = StartupLaunchHelper.SyncFromSettings(Locations.Settings.StartWithWindows);
        if (startWithWindows != Locations.Settings.StartWithWindows)
        {
            Locations.Settings.StartWithWindows = startWithWindows;
            try
            {
                await Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
            }
            catch
            {
                // Best-effort; registry entry already present.
            }
        }

        _suppressWindowFitResize = true;
        try
        {
            WindowPlacementHelper.Apply(window, Locations.Settings);
        }
        finally
        {
            _suppressWindowFitResize = false;
        }

        // Keep the user's last manual size; otherwise auto-fit would overwrite it on every launch.
        if (Locations.Settings.WindowSizeUserAdjusted)
            _userSizedMainWindow = true;

        if (_rootFrame is null)
            return;

        _ = _rootFrame.Navigate(typeof(MainPage), e.Arguments);
        window.Activate();

        if (_trayIcon is null)
        {
            TaskTrayIcon? tray = null;
            try
            {
                tray = new TaskTrayIcon(window);
                TaskTrayIcon.ApplyNoTaskbarButton(window);
                _trayIcon = tray;
                tray = null;
            }
            finally
            {
                tray?.Dispose();
            }
        }

        AppTheme.Apply(Locations.Settings);

        if (Locations.Settings.CheckForUpdatesOnStartup)
            _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            // Let the main UI settle before showing a dialog.
            await Task.Delay(1500).ConfigureAwait(true);
            var update = await Updates.CheckAsync().ConfigureAwait(true);
            var root = window?.Content?.XamlRoot;
            await AppUpdatePrompt.ShowIfNewerAsync(root, update, quietWhenCurrent: true).ConfigureAwait(true);
        }
        catch
        {
            // Startup checks are best-effort (offline, rate limit, etc.).
        }
    }

    /// <summary>Re-applies size/position after changing window placement in settings.</summary>
    public void ApplyWindowPlacementFromSettings()
    {
        if (window is null)
            return;

        _suppressWindowFitResize = true;
        try
        {
            WindowPlacementHelper.Apply(window, Locations.Settings);
        }
        finally
        {
            _suppressWindowFitResize = false;
        }
    }

    /// <summary>Runs a full weather refresh (same as the in-page Refresh link).</summary>
    public void RequestMainRefresh()
    {
        if (_rootFrame?.Content is MainPage mp)
            mp.RequestRefresh();
    }

    /// <summary>Tray icon for the first location; red badge when <paramref name="hasActiveAlert"/>.</summary>
    public void UpdateTrayWeatherIcon(CurrentConditionsPanel current, bool hasActiveAlert)
    {
        if (_trayIcon is null || window is null)
            return;

        var at = current.ObservationTime ?? DateTimeOffset.Now;
        var isNight = SolarTimeHelper.IsNight(at, current.SunriseToday, current.SunsetToday);

        var dq = window.DispatcherQueue;
        if (dq.HasThreadAccess)
            _trayIcon.SetWeatherIcon(current.WeatherCode, hasActiveAlert, isNight, at);
        else
            _ = dq.TryEnqueue(() => _trayIcon.SetWeatherIcon(current.WeatherCode, hasActiveAlert, isNight, at));
    }

    private Grid BuildWindowHost()
    {
        var host = new Grid { Name = "WwRootHost" };
        if (Current.Resources["AppPageBackgroundBrush"] is Brush hostBg)
            host.Background = hostBg;

        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleChrome = new Grid
        {
            Padding = new Thickness(0, 0, 132, 0),
        };
        TitleChromeHost = titleChrome;
        if (Current.Resources["AppPageBackgroundBrush"] is Brush pageBg)
            titleChrome.Background = pageBg;
        else
            titleChrome.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        Brush? bodyFg = Current.Resources["AppBodyTextBrush"] as Brush;

        _titleBarHost = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var titleText = new TextBlock
        {
            Text = "WeatherWizard",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsHitTestVisible = false,
        };
        if (bodyFg is not null)
            titleText.Foreground = bodyFg;

        _titleBarHost.Children.Add(titleText);
        titleChrome.Children.Add(_titleBarHost);

        Grid.SetRow(titleChrome, 0);
        host.Children.Add(titleChrome);

        var frameShell = new Border();
        if (Current.Resources["AppPageBackgroundBrush"] is Brush shellBg)
            frameShell.Background = shellBg;

        _rootFrame = new Frame { VerticalAlignment = VerticalAlignment.Top };
        _rootFrame.NavigationFailed += OnNavigationFailed;
        frameShell.Child = _rootFrame;
        Grid.SetRow(frameShell, 1);
        host.Children.Add(frameShell);

        return host;
    }

    private void WireChromeReferences(Grid host)
    {
        if (host.Children.Count < 2)
            return;

        if (host.Children[0] is Grid titleGrid && titleGrid.Children.Count >= 1 && titleGrid.Children[0] is Grid titleHost)
            _titleBarHost = titleHost;

        if (host.Children[0] is Grid titleChrome)
            TitleChromeHost = titleChrome;

        if (host.Children[1] is Border shell && shell.Child is Frame frame)
            _rootFrame = frame;
    }

    private DispatcherTimer? _geometrySaveTimer;

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((!args.DidSizeChange && !args.DidPositionChange) || _suppressWindowFitResize)
            return;

        if (args.DidSizeChange)
        {
            _userSizedMainWindow = true;
            Locations.Settings.WindowSizeUserAdjusted = true;
        }

        SchedulePersistWindowGeometry();
    }

    private void SchedulePersistWindowGeometry()
    {
        if (window is null)
            return;

        _geometrySaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _geometrySaveTimer.Tick -= OnGeometrySaveTick;
        _geometrySaveTimer.Tick += OnGeometrySaveTick;
        _geometrySaveTimer.Stop();
        _geometrySaveTimer.Start();
    }

    private async void OnGeometrySaveTick(object? sender, object e)
    {
        _geometrySaveTimer?.Stop();
        if (window is null)
            return;

        WindowPlacementHelper.PersistWindowGeometry(window, Locations.Settings);
        try
        {
            await Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
        }
        catch
        {
            // Best-effort persist while resizing.
        }
    }

    private async void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_geometrySaveTimer is not null)
        {
            _geometrySaveTimer.Tick -= OnGeometrySaveTick;
            _geometrySaveTimer.Stop();
            _geometrySaveTimer = null;
        }

        if (window?.AppWindow is AppWindow aw)
            aw.Changed -= OnAppWindowChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;

        if (window is not null)
        {
            WindowPlacementHelper.PersistWindowGeometry(window, Locations.Settings);
            try
            {
                await Locations.SaveAsync(raiseChanged: false).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort persist of window geometry.
            }
        }

        Speech.Dispose();
        Http.Dispose();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
    }
}
