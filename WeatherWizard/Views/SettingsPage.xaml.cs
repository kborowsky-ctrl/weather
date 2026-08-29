using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WeatherWizard.Models;
using WeatherWizard.Services;

namespace WeatherWizard.Views;

public sealed partial class SettingsPage : Page
{
    private readonly ObservableCollection<SavedLocation> _locations = new();
    private bool _suppressRefreshMinutesEvents;
    private bool _suppressDarkModeEvents;
    private bool _suppressStartWithWindowsEvents;
    private bool _suppressCheckUpdatesEvents;
    private bool _suppressSpeakAlertsEvents;
    private bool _suppressWindowPlacementEvents;
    private AppUpdateInfo? _pendingUpdate;
    private bool _suppressRadarFlipSecondsEvents;
    private bool _suppressCustomRadarUrlEvents;
    private bool _suppressCustomRadarTabEvents;
    private readonly TextBox[] _customRadarUrlBoxes;
    private SavedLocation? _customRadarEditingLocation;

    public SettingsPage()
    {
        InitializeComponent();
        LocationsList.ItemsSource = _locations;
        _customRadarUrlBoxes = [CustomRadarUrl1, CustomRadarUrl2, CustomRadarUrl3, CustomRadarUrl4, CustomRadarUrl5];
        foreach (var box in _customRadarUrlBoxes)
            box.TextChanged += OnCustomRadarUrlChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _locations.Clear();
        foreach (var loc in App.Current.Locations.Settings.Locations)
            _locations.Add(loc);

        _suppressRefreshMinutesEvents = true;
        RefreshMinutesBox.Value = App.Current.Locations.Settings.RefreshIntervalMinutes;
        _suppressRefreshMinutesEvents = false;

        _suppressDarkModeEvents = true;
        DarkModeSwitch.IsOn = AppTheme.IsDark(App.Current.Locations.Settings);
        _suppressDarkModeEvents = false;

        _suppressSpeakAlertsEvents = true;
        SpeakWeatherAlertsSwitch.IsOn = App.Current.Locations.Settings.SpeakWeatherAlerts;
        _suppressSpeakAlertsEvents = false;

        _suppressStartWithWindowsEvents = true;
        StartWithWindowsSwitch.IsOn = App.Current.Locations.Settings.StartWithWindows;
        _suppressStartWithWindowsEvents = false;

        _suppressCheckUpdatesEvents = true;
        CheckUpdatesOnStartupSwitch.IsOn = App.Current.Locations.Settings.CheckForUpdatesOnStartup;
        _suppressCheckUpdatesEvents = false;
        UpdateVersionText.Text = $"Installed version: {AppVersion.Display}";
        UpdateStatusText.Text = string.Empty;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;

        _suppressWindowPlacementEvents = true;
        SyncWindowPlacementCombo();
        _suppressWindowPlacementEvents = false;

        _suppressRadarFlipSecondsEvents = true;
        RadarFlipSecondsBox.Value = App.Current.Locations.Settings.RadarFlipIntervalSeconds;
        _suppressRadarFlipSecondsEvents = false;

        RebuildCustomRadarTabs();

        AppTheme.Apply(App.Current.Locations.Settings);

        StatusText.Text = string.Empty;
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        PersistCurrentCustomRadarUrls();
        await PersistAsync().ConfigureAwait(true);
        base.OnNavigatedFrom(e);
    }

    private async void OnSearchLocations(object sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        var city = CityBox.Text.Trim();
        var state = StateBox.Text.Trim();
        var zip = ZipBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(zip) && string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(state))
        {
            StatusText.Text = "Enter a ZIP code and/or city (and optional state).";
            return;
        }

        var args = BuildGeocodeArgs(city, state, zip);
        if (args.Name is null)
        {
            StatusText.Text =
                "With a 2-letter state code, also enter a city or ZIP (e.g. city Austin and state TX).";
            return;
        }

        try
        {
            var hits = await App.Current.Geocoding.SearchAsync(args.Name, args.CountryCode).ConfigureAwait(true);
            hits = ApplyAdmin1Filter(hits, args.Admin1Exact, args.Admin1Contains);

            if (hits.Count == 0)
            {
                StatusText.Text = "No matches found. Try the city spelling, or ZIP alone.";
                return;
            }

            if (hits.Count == 1)
            {
                await AddHitAsync(hits[0]).ConfigureAwait(true);
                return;
            }

            var pick = await ShowDisambiguationAsync(hits).ConfigureAwait(true);
            if (pick is not null)
                await AddHitAsync(pick).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private static (string? Name, string? CountryCode, string? Admin1Exact, string? Admin1Contains) BuildGeocodeArgs(
        string city,
        string state,
        string zip)
    {
        var country = InferUsCountryCode(state, zip);

        if (!string.IsNullOrWhiteSpace(zip))
            return (zip.Trim(), country, null, null);

        if (!string.IsNullOrWhiteSpace(city))
        {
            string? admin1Exact = null;
            string? admin1Contains = null;
            if (UsStateAbbreviations.IsProbablyUsStateCode(state))
                admin1Exact = UsStateAbbreviations.TryExpandToAdmin1(state);
            else if (!string.IsNullOrWhiteSpace(state))
                admin1Contains = state;

            return (city.Trim(), country, admin1Exact, admin1Contains);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            if (UsStateAbbreviations.IsProbablyUsStateCode(state))
                return (null, null, null, null);

            return (state.Trim(), country, null, null);
        }

        return (null, null, null, null);
    }

    private static string? InferUsCountryCode(string state, string zip)
    {
        if (!string.IsNullOrWhiteSpace(zip) && LooksLikeUsZip(zip))
            return "US";
        if (UsStateAbbreviations.IsProbablyUsStateCode(state))
            return "US";
        return null;
    }

    private static bool LooksLikeUsZip(string z)
    {
        z = z.Trim();
        if (z.Length == 5)
            return z.All(char.IsAsciiDigit);
        if (z.Length == 10 && z[5] == '-')
        {
            for (var i = 0; i < 5; i++)
            {
                if (!char.IsAsciiDigit(z[i]))
                    return false;
            }

            for (var i = 6; i < 10; i++)
            {
                if (!char.IsAsciiDigit(z[i]))
                    return false;
            }

            return true;
        }

        return false;
    }

    private static IReadOnlyList<GeocodeHit> ApplyAdmin1Filter(
        IReadOnlyList<GeocodeHit> hits,
        string? admin1Exact,
        string? admin1Contains)
    {
        var list = hits.ToList();
        if (admin1Exact is not null)
        {
            var filtered = list
                .Where(h => string.Equals(h.Admin1, admin1Exact, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return filtered.Count > 0 ? filtered : list;
        }

        if (!string.IsNullOrWhiteSpace(admin1Contains))
        {
            var filtered = list
                .Where(h => h.Admin1?.Contains(admin1Contains, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            if (filtered.Count > 0)
                return filtered;
        }

        return list;
    }

    private async Task<GeocodeHit?> ShowDisambiguationAsync(IReadOnlyList<GeocodeHit> hits)
    {
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single };
        foreach (var h in hits)
            list.Items.Add(h.DisplayLabel);

        var dialog = new ContentDialog
        {
            Title = "Pick a location",
            Content = list,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return null;

        if (list.SelectedIndex < 0)
            return null;

        return hits[list.SelectedIndex];
    }

    private async Task AddHitAsync(GeocodeHit hit)
    {
        var loc = hit.ToSavedLocation();
        _locations.Add(loc);
        await PersistAsync().ConfigureAwait(true);
        RebuildCustomRadarTabs();
        CityBox.Text = string.Empty;
        StateBox.Text = string.Empty;
        ZipBox.Text = string.Empty;
        StatusText.Text = $"Added {loc.TabLabel}.";
    }

    private async void OnRemove(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not SavedLocation loc)
            return;

        _locations.Remove(loc);
        await PersistAsync().ConfigureAwait(true);
        RebuildCustomRadarTabs();
    }

    private async void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not SavedLocation loc)
            return;

        var i = _locations.IndexOf(loc);
        if (i <= 0)
            return;

        _locations.Move(i, i - 1);
        await PersistAsync().ConfigureAwait(true);
        RebuildCustomRadarTabs();
    }

    private async void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not SavedLocation loc)
            return;

        var i = _locations.IndexOf(loc);
        if (i < 0 || i >= _locations.Count - 1)
            return;

        _locations.Move(i, i + 1);
        await PersistAsync().ConfigureAwait(true);
        RebuildCustomRadarTabs();
    }

    private async void OnSpeakWeatherAlertsToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressSpeakAlertsEvents)
            return;

        App.Current.Locations.Settings.SpeakWeatherAlerts = SpeakWeatherAlertsSwitch.IsOn;
        await App.Current.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
        StatusText.Text = SpeakWeatherAlertsSwitch.IsOn
            ? "Weather warnings will be spoken aloud."
            : "Weather warning voice announcements are off.";
    }

    private async void OnDarkModeToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressDarkModeEvents)
            return;

        App.Current.Locations.Settings.Theme = DarkModeSwitch.IsOn ? "Dark" : "Light";
        AppTheme.Apply(App.Current.Locations.Settings);
        await App.Current.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
    }

    private async void OnStartWithWindowsToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressStartWithWindowsEvents)
            return;

        var enabled = StartWithWindowsSwitch.IsOn;
        try
        {
            StartupLaunchHelper.Apply(enabled);
            App.Current.Locations.Settings.StartWithWindows = enabled;
            await App.Current.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
            StatusText.Text = enabled
                ? "WeatherWizard will start when you sign in to Windows."
                : "WeatherWizard will no longer start with Windows.";
        }
        catch (Exception ex)
        {
            _suppressStartWithWindowsEvents = true;
            StartWithWindowsSwitch.IsOn = !enabled;
            _suppressStartWithWindowsEvents = false;
            StatusText.Text = $"Could not update startup setting: {ex.Message}";
        }
    }

    private async void OnCheckUpdatesOnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressCheckUpdatesEvents)
            return;

        App.Current.Locations.Settings.CheckForUpdatesOnStartup = CheckUpdatesOnStartupSwitch.IsOn;
        await App.Current.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        _pendingUpdate = null;
        UpdateStatusText.Text = "Checking GitHub for updates…";
        try
        {
            var update = await App.Current.Updates.CheckAsync().ConfigureAwait(true);
            _pendingUpdate = update;
            if (update is null)
            {
                UpdateStatusText.Text =
                    "No GitHub release found yet. Publish a Release with WeatherWizard-Setup-win-x64.exe attached.";
                return;
            }

            if (!update.IsNewer)
            {
                UpdateStatusText.Text =
                    $"You are up to date ({AppVersion.Display}). Latest release is {AppVersion.Format(update.Latest)} (tag {update.TagName}).";
                return;
            }

            UpdateStatusText.Text =
                $"Update available: {AppVersion.Format(update.Latest)} (tag {update.TagName}); you have {AppVersion.Format(update.Current)}.";
            DownloadUpdateButton.Visibility = string.IsNullOrWhiteSpace(update.SetupDownloadUrl)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (string.IsNullOrWhiteSpace(update.SetupDownloadUrl))
            {
                UpdateStatusText.Text +=
                    " Release has no Setup EXE asset — attach WeatherWizard-Setup-win-x64.exe on GitHub.";
            }

            await AppUpdatePrompt.ShowIfNewerAsync(XamlRoot, update, quietWhenCurrent: false).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Update check failed: {ex.Message}";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate is null || string.IsNullOrWhiteSpace(_pendingUpdate.SetupDownloadUrl))
        {
            UpdateStatusText.Text = "Check for updates first.";
            return;
        }

        DownloadUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Downloading installer…";
        try
        {
            await App.Current.Updates.DownloadAndLaunchSetupAsync(_pendingUpdate).ConfigureAwait(true);
            UpdateStatusText.Text = "Installer started. Close WeatherWizard when Setup asks to update files.";
            var exit = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Installer started",
                Content = "Close WeatherWizard now so Setup can update files?",
                PrimaryButtonText = "Close app",
                CloseButtonText = "Keep running",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await exit.ShowAsync() == ContentDialogResult.Primary)
                Application.Current.Exit();
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Download failed: {ex.Message}";
            GitHubUpdateChecker.OpenReleasePage(_pendingUpdate);
        }
        finally
        {
            DownloadUpdateButton.IsEnabled = true;
        }
    }

    private void SyncWindowPlacementCombo()
    {
        var mode = WindowPlacementHelper.NormalizeMode(App.Current.Locations.Settings.WindowPlacement);
        for (var i = 0; i < WindowPlacementBox.Items.Count; i++)
        {
            if (WindowPlacementBox.Items[i] is ComboBoxItem item && item.Tag is string tag && tag == mode)
            {
                WindowPlacementBox.SelectedIndex = i;
                return;
            }
        }

        WindowPlacementBox.SelectedIndex = 0;
    }

    private async void OnWindowPlacementChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressWindowPlacementEvents)
            return;

        if (WindowPlacementBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
            return;

        App.Current.Locations.Settings.WindowPlacement = tag;
        await App.Current.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
        App.Current.ApplyWindowPlacementFromSettings();
    }

    private async void OnRefreshMinutesChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressRefreshMinutesEvents)
            return;

        if (!double.IsFinite(args.NewValue))
            return;

        var minutes = (int)Math.Clamp(Math.Round(args.NewValue), 5, 120);
        App.Current.Locations.Settings.RefreshIntervalMinutes = minutes;
        await App.Current.Locations.SaveAsync().ConfigureAwait(true);
    }

    private async void OnRadarFlipSecondsChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressRadarFlipSecondsEvents)
            return;

        if (!double.IsFinite(args.NewValue))
            return;

        App.Current.Locations.Settings.RadarFlipIntervalSeconds =
            (int)Math.Clamp(Math.Round(args.NewValue), 3, 120);
        await App.Current.Locations.SaveAsync().ConfigureAwait(true);
    }

    private void RebuildCustomRadarTabs()
    {
        var previousId = _customRadarEditingLocation?.Id;
        PersistCurrentCustomRadarUrls();

        _suppressCustomRadarTabEvents = true;
        CustomRadarLocationTabs.TabItems.Clear();

        if (_locations.Count == 0)
        {
            CustomRadarLocationTabs.Visibility = Visibility.Collapsed;
            CustomRadarNoLocationsText.Visibility = Visibility.Visible;
            SetCustomRadarUrlBoxesEnabled(false);
            _customRadarEditingLocation = null;
            ClearCustomRadarUrlBoxes();
            _suppressCustomRadarTabEvents = false;
            return;
        }

        CustomRadarLocationTabs.Visibility = Visibility.Visible;
        CustomRadarNoLocationsText.Visibility = Visibility.Collapsed;
        SetCustomRadarUrlBoxesEnabled(true);

        foreach (var loc in _locations)
        {
            CustomRadarLocationTabs.TabItems.Add(new TabViewItem
            {
                Header = loc.TabLabel,
                Tag = loc,
            });
        }

        var selectIndex = 0;
        if (previousId is Guid id)
        {
            for (var i = 0; i < _locations.Count; i++)
            {
                if (_locations[i].Id == id)
                {
                    selectIndex = i;
                    break;
                }
            }
        }

        CustomRadarLocationTabs.SelectedIndex = selectIndex;
        _suppressCustomRadarTabEvents = false;
        LoadCustomRadarUrlsForSelectedTab();
    }

    private void OnCustomRadarTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressCustomRadarTabEvents)
            return;

        PersistCurrentCustomRadarUrls();
        LoadCustomRadarUrlsForSelectedTab();
    }

    private void LoadCustomRadarUrlsForSelectedTab()
    {
        _customRadarEditingLocation = null;

        if (CustomRadarLocationTabs.SelectedItem is TabViewItem tab && tab.Tag is SavedLocation loc)
            _customRadarEditingLocation = loc;

        _suppressCustomRadarUrlEvents = true;
        if (_customRadarEditingLocation is null)
        {
            ClearCustomRadarUrlBoxes();
        }
        else
        {
            var urls = _customRadarEditingLocation.CustomRadarImageUrls;
            for (var i = 0; i < _customRadarUrlBoxes.Length; i++)
                _customRadarUrlBoxes[i].Text = i < urls.Count ? urls[i] : string.Empty;
        }

        _suppressCustomRadarUrlEvents = false;
    }

    private void PersistCurrentCustomRadarUrls()
    {
        if (_customRadarEditingLocation is null)
            return;

        _customRadarEditingLocation.CustomRadarImageUrls =
            CustomRadarUrlHelper.Normalize(_customRadarUrlBoxes.Select(b => b.Text));
    }

    private void ClearCustomRadarUrlBoxes()
    {
        foreach (var box in _customRadarUrlBoxes)
            box.Text = string.Empty;
    }

    private void SetCustomRadarUrlBoxesEnabled(bool enabled)
    {
        foreach (var box in _customRadarUrlBoxes)
            box.IsEnabled = enabled;
    }

    private async void OnCustomRadarUrlChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressCustomRadarUrlEvents || _customRadarEditingLocation is null)
            return;

        PersistCurrentCustomRadarUrls();
        await App.Current.Locations.SaveAsync().ConfigureAwait(true);
    }

    private async Task PersistAsync()
    {
        PersistCurrentCustomRadarUrls();
        App.Current.Locations.Settings.Locations = _locations.ToList();
        await App.Current.Locations.SaveAsync().ConfigureAwait(true);
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
    }
}
