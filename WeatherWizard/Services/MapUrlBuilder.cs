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

    private const string RadarMosaicExport =
        "https://mapservices.weather.noaa.gov/eventdriven/rest/services/radar/radar_base_reflectivity/MapServer/export";

    private const string ReferenceMapExport =
        "https://mapservices.weather.noaa.gov/static/rest/services/nws_reference_maps/nws_reference_map/MapServer/export";

    /// <summary>Regional mosaic pixel size; aspect matches the radar host (width / 1.08).</summary>
    public const int RegionalRadarPixelWidth = 600;
    public const int RegionalRadarPixelHeight = 556;

    /// <summary>Ground width of the regional view, similar to a single-site RIDGE still.</summary>
    private const double RegionalWidthKm = 520;

    /// <summary>
    /// Regional radar centered on the saved location: a transparent NOAA mosaic reflectivity
    /// overlay plus a matching state-border basemap. Null outside the U.S.
    /// </summary>
    public static (Uri BaseMap, Uri Radar)? TryBuildRegionalRadarUris(SavedLocation location)
    {
        if (!location.IsUnitedStates)
            return null;

        var lat = Math.Clamp(location.Latitude, -84, 84);
        const double earthRadius = 6378137.0;
        var x = location.Longitude * Math.PI / 180.0 * earthRadius;
        var y = Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360.0)) * earthRadius;

        // Web Mercator meters are stretched by 1/cos(lat) relative to ground distance.
        var halfW = RegionalWidthKm * 1000 / 2 / Math.Cos(lat * Math.PI / 180.0);
        var halfH = halfW * RegionalRadarPixelHeight / RegionalRadarPixelWidth;

        var bbox = string.Create(
            CultureInfo.InvariantCulture,
            $"{x - halfW:F0},{y - halfH:F0},{x + halfW:F0},{y + halfH:F0}");
        var common = string.Create(
            CultureInfo.InvariantCulture,
            $"?bbox={bbox}&bboxSR=3857&imageSR=3857&size={RegionalRadarPixelWidth},{RegionalRadarPixelHeight}&dpi=96&f=image");

        return (
            new Uri(ReferenceMapExport + common + "&format=png32"),
            new Uri(RadarMosaicExport + common + "&format=png32&transparent=true"));
    }

    /// <summary>Returns the NWS RIDGE GIF for U.S. points with a resolved radar id; otherwise null.</summary>
    public static Uri? TryBuildNwsRadarUri(SavedLocation location)
    {
        if (location.IsUnitedStates && !string.IsNullOrWhiteSpace(location.NwsRadarStation))
            return NwsRidgeStandardGif(location.NwsRadarStation);

        return null;
    }
}
