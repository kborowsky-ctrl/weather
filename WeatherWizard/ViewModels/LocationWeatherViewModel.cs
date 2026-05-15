using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WeatherWizard.Models;
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
    [NotifyPropertyChangedFor(nameof(HasAlertLink))]
    private Uri? _alertLink;

    public bool HasAlertLink => AlertLink is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorBanner = "";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorBanner);

    [ObservableProperty] private string _lastUpdatedText = "";

    /// <summary>Bumped after NWS radar site id is resolved so the radar image reloads.</summary>
    [ObservableProperty] private int _radarStamp;

    public ObservableCollection<ForecastDayItem> ForecastDays { get; } = new();
}
