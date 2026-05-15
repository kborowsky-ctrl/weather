using WeatherWizard.Services;

namespace WeatherWizard.Models;

public sealed class SavedLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = "";

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>ISO 3166-1 alpha-2, e.g. US.</summary>
    public string CountryCode { get; set; } = "";

    /// <summary>State / region (Open-Meteo admin1).</summary>
    public string? Admin1 { get; set; }

    /// <summary>City / locality name.</summary>
    public string? Locality { get; set; }

    /// <summary>4-letter WSR-88D site id from NWS points (e.g. KOKX); used for official RIDGE GIF.</summary>
    public string? NwsRadarStation { get; set; }

    public string TabLabel =>
        LocationDisplayFormatter.TabShort(Locality, DisplayName, Admin1, CountryCode);

    public bool IsUnitedStates =>
        string.Equals(CountryCode, "US", StringComparison.OrdinalIgnoreCase);

    public string SpeechLocationLabel
    {
        get
        {
            var city = LocationDisplayFormatter.PrimaryCity(Locality, DisplayName);
            var state = Admin1;
            if (!string.IsNullOrWhiteSpace(state))
                return $"{city}, {state}";
            return string.IsNullOrWhiteSpace(city) ? DisplayName : city;
        }
    }
}
