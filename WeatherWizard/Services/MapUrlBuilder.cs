using System.Globalization;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

public static class MapUrlBuilder
{
    /// <summary>
    /// Official local radar still (updated routinely), same asset class linked from
    /// forecast.weather.gov MapClick "Radar &amp; Satellite" thumbnails.
    /// </summary>
    public static Uri NwsRidgeStandardGif(string radarStationId)
    {
        var rid = radarStationId.Trim().ToUpperInvariant();
        return new Uri($"https://radar.weather.gov/ridge/standard/{rid}_0.gif");
    }

    /// <summary>Returns the NWS RIDGE GIF for U.S. points with a resolved radar id; otherwise null.</summary>
    public static Uri? TryBuildNwsRadarUri(SavedLocation location)
    {
        if (location.IsUnitedStates && !string.IsNullOrWhiteSpace(location.NwsRadarStation))
            return NwsRidgeStandardGif(location.NwsRadarStation);

        return null;
    }
}
