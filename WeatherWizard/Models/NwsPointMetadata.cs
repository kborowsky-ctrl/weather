namespace WeatherWizard.Models;

/// <summary>Selected fields from api.weather.gov/points (Feature properties).</summary>
/// <param name="Office">Forecast office id (<c>cwa</c>), e.g. OKX.</param>
/// <param name="ForecastZone">Public forecast zone id, e.g. NYZ072.</param>
/// <param name="CountyZone">County zone id, e.g. NYC061.</param>
public sealed record NwsPointMetadata(
    string? RadarStation,
    Uri? GridForecastUri,
    string? Office = null,
    string? ForecastZone = null,
    string? CountyZone = null);
