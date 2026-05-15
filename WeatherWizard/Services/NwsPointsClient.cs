using System.Text.Json;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

/// <summary>
/// NWS grid metadata from api.weather.gov/points — radar site id and grid forecast URL
/// (same sources as forecast.weather.gov).
/// </summary>
public sealed class NwsPointsClient(HttpClientFactory http)
{
    public async Task<string?> GetRadarStationIdAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var meta = await GetPointMetadataAsync(latitude, longitude, ct).ConfigureAwait(false);
        return meta?.RadarStation;
    }

    public async Task<NwsPointMetadata?> GetPointMetadataAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var lat = latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lon = longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var url = $"https://api.weather.gov/points/{lat},{lon}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "application/geo+json, application/json;q=0.9, */*;q=0.8");

        using var resp = await http.Client.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
            return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("properties", out var props))
            return null;

        string? radar = null;
        if (props.TryGetProperty("radarStation", out var rs) && rs.ValueKind == JsonValueKind.String)
        {
            var id = rs.GetString();
            if (!string.IsNullOrWhiteSpace(id))
                radar = id.Trim().ToUpperInvariant();
        }

        Uri? forecastUri = null;
        if (props.TryGetProperty("forecast", out var fc) && fc.ValueKind == JsonValueKind.String)
        {
            var href = fc.GetString();
            if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(href, UriKind.Absolute, out var u))
                forecastUri = u;
        }

        return new NwsPointMetadata(radar, forecastUri);
    }
}
