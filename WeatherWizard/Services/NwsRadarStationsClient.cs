using System.Collections.Concurrent;
using System.Text.Json;

namespace WeatherWizard.Services;

/// <summary>Looks up WSR-88D site coordinates from api.weather.gov/radar/stations.</summary>
public sealed class NwsRadarStationsClient(HttpClientFactory http)
{
    private readonly ConcurrentDictionary<string, (double Lat, double Lon)?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<(double Lat, double Lon)?> TryGetCoordinatesAsync(string radarStationId, CancellationToken ct = default)
    {
        var id = radarStationId.Trim().ToUpperInvariant();
        if (id.Length == 0)
            return null;

        if (_cache.TryGetValue(id, out var cached))
            return cached;

        try
        {
            var url = $"https://api.weather.gov/radar/stations/{id}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/geo+json, application/json;q=0.9, */*;q=0.8");

            using var resp = await http.Client.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("geometry", out var geom)
                || !geom.TryGetProperty("coordinates", out var coords)
                || coords.ValueKind != JsonValueKind.Array
                || coords.GetArrayLength() < 2)
                return null;

            var lon = coords[0].GetDouble();
            var lat = coords[1].GetDouble();
            var pair = (lat, lon);
            _cache[id] = pair;
            return pair;
        }
        catch
        {
            // Do not cache failures — allow a later retry.
            return null;
        }
    }
}
