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

        var office = props.TryGetProperty("cwa", out var cwa) && cwa.ValueKind == JsonValueKind.String
            ? cwa.GetString()?.Trim().ToUpperInvariant()
            : null;

        return new NwsPointMetadata(
            radar,
            forecastUri,
            string.IsNullOrWhiteSpace(office) ? null : office,
            ZoneIdFromUrl(props, "forecastZone"),
            ZoneIdFromUrl(props, "county"));
    }

    /// <summary>Last path segment of a zone URL, e.g. ".../zones/forecast/NYZ072" to NYZ072.</summary>
    private static string? ZoneIdFromUrl(JsonElement props, string name)
    {
        if (!props.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
            return null;

        var url = el.GetString();
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var id = url.TrimEnd('/').Split('/')[^1].Trim().ToUpperInvariant();
        return id.Length == 0 ? null : id;
    }
}
