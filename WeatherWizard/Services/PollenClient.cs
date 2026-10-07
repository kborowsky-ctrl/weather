using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

/// <summary>
/// U.S. pollen index from pollen.com's public forecast feed (by ZIP), plus a Census TIGERweb
/// lookup to find the ZIP for a saved location's coordinates.
/// </summary>
public sealed class PollenClient(HttpClientFactory http)
{
    private const string ZctaQuery =
        "https://tigerweb.geo.census.gov/arcgis/rest/services/TIGERweb/PUMA_TAD_TAZ_UGA_ZCTA/MapServer/1/query";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, PollenReport Report)> _cache = new();

    public async Task<string?> TryResolveZipAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var point = string.Create(CultureInfo.InvariantCulture, $"{longitude:F5},{latitude:F5}");
        var url = $"{ZctaQuery}?geometry={point}&geometryType=esriGeometryPoint&inSR=4326"
            + "&spatialRel=esriSpatialRelIntersects&outFields=ZCTA5&returnGeometry=false&f=json";
        try
        {
            await using var stream = await http.Client.GetStreamAsync(url, ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                return null;
            var zip = features[0].GetProperty("attributes").GetProperty("ZCTA5").GetString();
            return zip is { Length: 5 } ? zip : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<PollenReport?> TryGetAsync(string zip, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(zip, out var hit) && DateTimeOffset.UtcNow - hit.At < CacheLifetime)
            return hit.Report;

        var url = $"https://www.pollen.com/api/forecast/current/pollen/{zip}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // The feed rejects requests without a pollen.com referer.
        request.Headers.Referrer = new Uri($"https://www.pollen.com/forecast/current/pollen/{zip}");
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await http.Client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("Location", out var location)
                || location.ValueKind != JsonValueKind.Object
                || !location.TryGetProperty("periods", out var periods))
                return null;

            var days = new List<PollenDay>();
            foreach (var p in periods.EnumerateArray())
            {
                var label = p.TryGetProperty("Type", out var t) ? t.GetString() ?? "" : "";
                var index = p.TryGetProperty("Index", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetDouble() : 0;
                var triggers = new List<string>();
                if (p.TryGetProperty("Triggers", out var trig) && trig.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tr in trig.EnumerateArray())
                    {
                        if (tr.TryGetProperty("Name", out var n) && n.GetString() is { Length: > 0 } name)
                            triggers.Add(name);
                    }
                }

                days.Add(new PollenDay(label, index, triggers));
            }

            var display = location.TryGetProperty("DisplayLocation", out var dl) ? dl.GetString() : null;
            var report = new PollenReport(zip, display, days);
            _cache[zip] = (DateTimeOffset.UtcNow, report);
            return report;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
