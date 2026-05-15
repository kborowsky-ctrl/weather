namespace WeatherWizard.Models;

/// <summary>Selected fields from api.weather.gov/points (Feature properties).</summary>
public sealed record NwsPointMetadata(string? RadarStation, Uri? GridForecastUri);
