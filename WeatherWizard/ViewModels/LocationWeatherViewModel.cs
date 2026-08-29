using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WeatherWizard.Models;
using WeatherWizard.Services;
using WeatherWizard;

namespace WeatherWizard.ViewModels;

public partial class LocationWeatherViewModel : ObservableObject
{
    public SavedLocation Location { get; }

    public LocationWeatherViewModel(SavedLocation location)
    {
        Location = location;
    }

    [ObservableProperty] private CurrentConditionsPanel _currentConditions = CurrentConditionsPanel.Loading;

    [ObservableProperty] private string _alertSummary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlertDetails))]
    private IReadOnlyList<WeatherAlertItem> _activeAlerts = Array.Empty<WeatherAlertItem>();

    public bool HasAlertDetails => ActiveAlerts.Any(a => a.HasDetailText);

    /// <summary>Optional public web link (kept for reference; Details opens in-app text).</summary>
    [ObservableProperty] private Uri? _alertLink;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSeasonalOutlook))]
    [NotifyPropertyChangedFor(nameof(SeasonalOutlookLinkText))]
    private SeasonalOutlookSnapshot? _seasonalOutlook;

    public bool HasSeasonalOutlook => SeasonalOutlook is not null;

    public string SeasonalOutlookLinkText =>
        SeasonalOutlook is { } s ? $"{s.Target.DisplayName} outlook" : "Seasonal outlook";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorBanner = "";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorBanner);

    [ObservableProperty] private string _lastUpdatedText = "";

    /// <summary>Bumped after NWS radar site id is resolved so the radar image reloads.</summary>
    [ObservableProperty] private int _radarStamp;

    public ObservableCollection<ForecastDayItem> ForecastDays { get; } = new();
}
