using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WeatherWizard.Models;
using WeatherWizard.Services;
using WeatherWizard.ViewModels;

namespace WeatherWizard.Views;

public sealed partial class MainPage : Page
{
    private readonly Dictionary<Guid, LocationWeatherViewModel> _viewModels = new();
    private DispatcherTimer? _refreshTimer;
    private bool _handlersWired;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        AppTheme.Apply(App.Current.Locations.Settings);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_handlersWired)
            return;

        _handlersWired = true;
        App.Current.Locations.Changed += OnLocationsChanged;
        RebuildTabs();
        RestartRefreshTimer();
        _ = RefreshAllAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Current.Locations.Changed -= OnLocationsChanged;
        _handlersWired = false;
        if (_refreshTimer is not null)
        {
            _refreshTimer.Tick -= OnRefreshTick;
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
    }

    private void OnLocationsChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            RebuildTabs();
            RestartRefreshTimer();
            await RefreshAllAsync();
        });
    }

    private void RestartRefreshTimer()
    {
        if (_refreshTimer is not null)
        {
            _refreshTimer.Tick -= OnRefreshTick;
            _refreshTimer.Stop();
        }

        _refreshTimer = new DispatcherTimer
        {
            Interval = App.Current.Locations.Settings.RefreshInterval,
        };
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
    }

    private void OnRefreshTick(object? sender, object e)
    {
        _ = RefreshAllAsync();
    }

    private void RebuildTabs()
    {
        var app = App.Current;
        var locations = app.Locations.Settings.Locations;

        var keep = locations.Select(l => l.Id).ToHashSet();
        foreach (var id in _viewModels.Keys.Where(id => !keep.Contains(id)).ToArray())
        {
            _viewModels.Remove(id);
            app.AlertSpeech.ResetLocation(id);
        }

        LocationTabs.TabItems.Clear();

        if (locations.Count == 0)
        {
            EmptyState.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        foreach (var loc in locations)
        {
            var vm = new LocationWeatherViewModel(loc);
            _viewModels[loc.Id] = vm;

            var tab = new TabViewItem
            {
                Header = loc.TabLabel,
                Tag = loc.Id,
                MinHeight = 0,
            };

            var view = new LocationWeatherView { VerticalAlignment = VerticalAlignment.Top };
            view.Attach(vm);
            tab.Content = view;
            LocationTabs.TabItems.Add(tab);
        }
    }

    private async Task RefreshAllAsync()
    {
        var app = App.Current;
        var locations = app.Locations.Settings.Locations;
        if (locations.Count == 0)
            return;

        foreach (var loc in locations)
        {
            if (!_viewModels.TryGetValue(loc.Id, out var vm))
                continue;

            try
            {
                var bundle = await app.Forecast.GetForecastAsync(loc.Latitude, loc.Longitude).ConfigureAwait(true);

                NwsPointMetadata? pointMeta = null;
                if (loc.IsUnitedStates)
                {
                    try
                    {
                        pointMeta = await app.NwsPoints.GetPointMetadataAsync(loc.Latitude, loc.Longitude)
                            .ConfigureAwait(true);
                    }
                    catch
                    {
                        pointMeta = null;
                    }
                }

                IReadOnlyList<ForecastDayItem> listDays = bundle.Days;
                if (loc.IsUnitedStates && pointMeta?.GridForecastUri is { } forecastUri)
                {
                    var nwsDays = await app.NwsGridForecast.TryGetForecastDaysAsync(forecastUri).ConfigureAwait(true);
                    if (nwsDays is { Count: > 0 })
                        listDays = nwsDays;
                }

                vm.CurrentConditions = bundle.Current;
                vm.ForecastDays.Clear();
                foreach (var d in listDays)
                    vm.ForecastDays.Add(d);

                IReadOnlyList<WeatherAlertItem> alerts;
                if (loc.IsUnitedStates)
                    alerts = await app.Nws.GetActiveForPointAsync(loc.Latitude, loc.Longitude).ConfigureAwait(true);
                else
                    alerts = [];

                if (!loc.IsUnitedStates)
                {
                    vm.AlertSummary = "NWS alerts apply to U.S. locations only.";
                    vm.AlertLink = null;
                }
                else if (alerts.Count == 0)
                {
                    vm.AlertSummary = "No active weather alerts.";
                    vm.AlertLink = null;
                }
                else
                {
                    var first = alerts[0];
                    vm.AlertSummary = alerts.Count == 1
                        ? first.Headline
                        : $"{alerts.Count} active alerts: {first.Headline} (+{alerts.Count - 1} more)";
                    vm.AlertLink = first.Link;
                }

                await app.AlertSpeech.OnAlertsUpdatedAsync(loc, alerts).ConfigureAwait(true);

                if (loc.IsUnitedStates)
                {
                    try
                    {
                        var rid = pointMeta?.RadarStation;
                        if (!string.IsNullOrWhiteSpace(rid))
                        {
                            if (!string.Equals(loc.NwsRadarStation, rid, StringComparison.Ordinal))
                            {
                                loc.NwsRadarStation = rid;
                                await app.Locations.SaveAsync(raiseChanged: false).ConfigureAwait(true);
                            }

                            vm.RadarStamp++;
                        }
                    }
                    catch
                    {
                        // Keep existing radar station id if the points lookup fails.
                    }
                }

                vm.LastUpdatedText = $"Updated {DateTime.Now:t}";
                vm.ErrorBanner = string.Empty;
            }
            catch (Exception ex)
            {
                vm.ErrorBanner = ex.Message;
            }
        }
    }

    /// <summary>Triggers a full refresh (same as the toolbar refresh control).</summary>
    public void RequestRefresh() => _ = RefreshAllAsync();

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SettingsPage));
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _ = RefreshAllAsync();
    }
}
