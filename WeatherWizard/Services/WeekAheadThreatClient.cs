using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

/// <summary>
/// Week-ahead (days 3-14) threats for a point from NOAA outlooks: CPC/WPC Hazards Outlook,
/// SPC convective outlooks (days 3-8), WPC winter storm severity (WSSI-P), plus the local
/// NWS Hazardous Weather Outlook text for context.
/// </summary>
public sealed partial class WeekAheadThreatClient(HttpClientFactory http)
{
    private const string Services = "https://mapservices.weather.noaa.gov/vector/rest/services";
    private const string CpcHazards = Services + "/hazards/cpc_weather_hazards/MapServer";
    private const string SpcOutlooks = Services + "/outlooks/SPC_wx_outlks/MapServer";
    private const string WssiP = Services + "/outlooks/wpc_wssi_p/MapServer";
    private const string BaseMap =
        "https://mapservices.weather.noaa.gov/static/rest/services/nws_reference_maps/nws_reference_map/MapServer";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, (DateTimeOffset At, WeekAheadThreatSnapshot Snapshot)> _cache = new();

    /// <summary>
    /// Threats not already covered by an active alert, highest priority first; null on failure.
    /// </summary>
    public async Task<WeekAheadThreatSnapshot?> TryGetAsync(
        double latitude,
        double longitude,
        string? office,
        string? forecastZone,
        string? countyZone,
        IReadOnlyList<WeatherAlertItem> activeAlerts,
        CancellationToken ct = default)
    {
        var key = string.Create(CultureInfo.InvariantCulture, $"{latitude:F2},{longitude:F2}");
        WeekAheadThreatSnapshot raw;
        if (_cache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow - hit.At < CacheLifetime)
        {
            raw = hit.Snapshot;
        }
        else
        {
            var fetched = await FetchAsync(latitude, longitude, office, forecastZone, countyZone, ct)
                .ConfigureAwait(false);
            if (fetched is null)
                return null;

            raw = fetched;
            _cache[key] = (DateTimeOffset.UtcNow, raw);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var threats = raw.Threats
            .Where(t => t.End >= today && !IsCoveredByAlert(t.Kind, activeAlerts))
            .ToList();
        return raw with { Threats = threats };
    }

    private async Task<WeekAheadThreatSnapshot?> FetchAsync(
        double latitude,
        double longitude,
        string? office,
        string? forecastZone,
        string? countyZone,
        CancellationToken ct)
    {
        var cpcTask = QueryCpcAsync(latitude, longitude, ct);
        var spcTask = QuerySpcAsync(latitude, longitude, ct);
        var wssiTask = QueryWssiAsync(latitude, longitude, ct);
        var hwoTask = FetchHazardOutlookAsync(office, forecastZone, countyZone, ct);
        await Task.WhenAll(cpcTask, spcTask, wssiTask, hwoTask).ConfigureAwait(false);

        var cpc = cpcTask.Result;
        var spc = spcTask.Result;
        var wssi = wssiTask.Result;
        if (cpc is null && spc is null && wssi is null)
            return null;

        var found = new List<(ThreatKind Kind, DateOnly Start, DateOnly End, string Source)>();
        found.AddRange(cpc ?? []);
        found.AddRange(spc?.Hits ?? []);
        found.AddRange(wssi ?? []);

        var threats = found
            .GroupBy(f => f.Kind)
            .Select(g => new WeekAheadThreat(
                g.Key,
                g.Min(f => f.Start),
                g.Max(f => f.End),
                g.Select(f => f.Source).Distinct().ToList()))
            .OrderBy(t => t.Kind)
            .ThenBy(t => t.Start)
            .ToList();

        var bbox = RegionalBbox(latitude, longitude);
        var baseMap = ExportUri(BaseMap, bbox, layers: null, transparent: false);
        var maps = new List<ThreatMapPanel>
        {
            new("Days 3-7 hazards outlook (CPC/WPC)", baseMap, ExportUri(CpcHazards, bbox, "1,4", transparent: true)),
        };
        if (threats.Any(t => (t.Start.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber) >= 7))
            maps.Add(new("Days 8-14 hazards outlook (CPC)", baseMap, ExportUri(CpcHazards, bbox, "3,6", transparent: true)));
        if (spc?.MapLayer is int spcLayer)
            maps.Add(new(spc.MapTitle, baseMap, ExportUri(SpcOutlooks, bbox, spcLayer.ToString(CultureInfo.InvariantCulture), transparent: true)));

        var hwo = hwoTask.Result;
        return new WeekAheadThreatSnapshot(threats, hwo?.Text, office, hwo?.Issued, maps);
    }

    private async Task<List<(ThreatKind, DateOnly, DateOnly, string)>?> QueryCpcAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        // 1 = days 3-7 temperature, 4 = days 3-7 precipitation, 3/6 = days 8-14.
        var layers = new[] { (1, "CPC days 3-7"), (4, "CPC days 3-7"), (3, "CPC days 8-14"), (6, "CPC days 8-14") };
        var tasks = layers.Select(l => QueryPointAsync(CpcHazards, l.Item1, latitude, longitude, "label,start_date,end_date", ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (tasks.All(t => t.Result is null))
            return null;

        var month = DateTime.Now.Month;
        var hits = new List<(ThreatKind, DateOnly, DateOnly, string)>();
        for (var i = 0; i < layers.Length; i++)
        {
            foreach (var attrs in tasks[i].Result ?? [])
            {
                var label = GetString(attrs, "label");
                if (MapCpcLabel(label, month) is not ThreatKind kind)
                    continue;

                var start = GetEpochDate(attrs, "start_date");
                var end = GetEpochDate(attrs, "end_date") ?? start;
                if (start is null || end is null)
                    continue;

                hits.Add((kind, start.Value, end.Value, $"{layers[i].Item2}: {label}"));
            }
        }

        return hits;
    }

    /// <summary>Maps CPC hazard labels to kinds; drought, wildfire, and waves are ignored.</summary>
    private static ThreatKind? MapCpcLabel(string? label, int month)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var l = label.Trim().ToLowerInvariant();
        if (l.Contains("severe")) return ThreatKind.Severe;
        if (l.Contains("heavy snow")) return ThreatKind.WinterStorm;
        if (l.Contains("freezing rain") || l.Contains("heavy ice")) return ThreatKind.Ice;
        if (l.Contains("flood")) return ThreatKind.Flooding;
        if (l.Contains("heavy rain") || l.Contains("heavy precipitation")) return ThreatKind.HeavyRain;
        if (l.Contains("high wind")) return ThreatKind.HighWind;
        if (l.Contains("heat")) return ThreatKind.Heat;
        if (l.Contains("hazardous cold")) return ThreatKind.Cold;
        if (l.Contains("frost") || l.Contains("freeze")) return ThreatKind.Frost;
        // Days 8-14 uses departure-from-normal labels; only treat them as threats in their season.
        if (l.Contains("much above normal") && month is >= 5 and <= 9) return ThreatKind.Heat;
        if (l.Contains("much below normal") && (month >= 11 || month <= 3)) return ThreatKind.Cold;
        return null;
    }

    private sealed record SpcResult(
        List<(ThreatKind, DateOnly, DateOnly, string)> Hits,
        int? MapLayer,
        string MapTitle);

    private async Task<SpcResult?> QuerySpcAsync(double latitude, double longitude, CancellationToken ct)
    {
        // 19 = Day 3 probabilistic; 21-25 = Days 4-8 probabilistic.
        var layers = new[] { 19, 21, 22, 23, 24, 25 };
        var tasks = layers.Select(l => QueryPointAsync(SpcOutlooks, l, latitude, longitude, "dn,idp_source,valid", ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (tasks.All(t => t.Result is null))
            return null;

        var hits = new List<(ThreatKind, DateOnly, DateOnly, string)>();
        int? mapLayer = null;
        var mapTitle = "";
        for (var i = 0; i < layers.Length; i++)
        {
            var day = layers[i] == 19 ? 3 : layers[i] - 17;
            foreach (var attrs in tasks[i].Result ?? [])
            {
                // 15% is the SPC "slight risk" equivalent for days 3-8.
                var prob = GetDouble(attrs, "dn") ?? 0;
                if (prob < 15)
                    continue;

                var date = SpcValidDate(attrs, day);
                if (date is null)
                    continue;

                hits.Add((ThreatKind.Severe, date.Value, date.Value, $"SPC day {day}: {prob:0}% severe probability"));
                if (mapLayer is null)
                {
                    mapLayer = layers[i];
                    mapTitle = $"Day {day} severe storm outlook (SPC)";
                }
            }
        }

        return new SpcResult(hits, mapLayer, mapTitle);
    }

    /// <summary>Day 3 has a <c>valid</c> time; days 4-8 are issue date (from idp_source) plus day - 1.</summary>
    private static DateOnly? SpcValidDate(JsonElement attrs, int day)
    {
        var valid = GetString(attrs, "valid");
        if (valid is { Length: >= 8 }
            && DateOnly.TryParseExact(valid[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v))
            return v;

        var source = GetString(attrs, "idp_source");
        var m = source is null ? null : EightDigitDateRegex().Match(source);
        if (m is { Success: true }
            && DateOnly.TryParseExact(m.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issued))
            return issued.AddDays(day - 1);

        return null;
    }

    private async Task<List<(ThreatKind, DateOnly, DateOnly, string)>?> QueryWssiAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        // Overall winter storm impacts: 2 = moderate, 3 = major, 4 = extreme.
        var layers = new[] { (2, "moderate", 40.0), (3, "major", 20.0), (4, "extreme", 20.0) };
        var tasks = layers.Select(l => QueryPointAsync(WssiP, l.Item1, latitude, longitude, "prob,validstartdatetime,validenddatetime", ct)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (tasks.All(t => t.Result is null))
            return null;

        var hits = new List<(ThreatKind, DateOnly, DateOnly, string)>();
        for (var i = 0; i < layers.Length; i++)
        {
            foreach (var attrs in tasks[i].Result ?? [])
            {
                var prob = GetDouble(attrs, "prob") ?? 0;
                if (prob < layers[i].Item3)
                    continue;

                var start = GetEpochDate(attrs, "validstartdatetime");
                var end = GetEpochDate(attrs, "validenddatetime") ?? start;
                if (start is null || end is null)
                    continue;

                hits.Add((ThreatKind.WinterStorm, start.Value, end.Value,
                    $"WPC winter storm severity: {prob:0}% chance of {layers[i].Item2} impacts"));
            }
        }

        return hits;
    }

    private async Task<List<JsonElement>?> QueryPointAsync(
        string mapServer,
        int layer,
        double latitude,
        double longitude,
        string outFields,
        CancellationToken ct)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var url =
            $"{mapServer}/{layer}/query?geometry={lon}%2C{lat}&geometryType=esriGeometryPoint&inSR=4326" +
            $"&spatialRel=esriSpatialRelIntersects&outFields={Uri.EscapeDataString(outFields)}&returnGeometry=false&f=json";
        try
        {
            using var resp = await http.Client.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                return null;

            return features.EnumerateArray()
                .Where(f => f.TryGetProperty("attributes", out _))
                .Select(f => f.GetProperty("attributes").Clone())
                .ToList();
        }
        catch
        {
            return null;
        }
    }

    private sealed record HazardOutlook(string Text, DateTimeOffset? Issued);

    private async Task<HazardOutlook?> FetchHazardOutlookAsync(
        string? office,
        string? forecastZone,
        string? countyZone,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(office) || (forecastZone is null && countyZone is null))
            return null;

        try
        {
            using var listReq = new HttpRequestMessage(HttpMethod.Get, $"https://api.weather.gov/products/types/HWO/locations/{office}");
            listReq.Headers.TryAddWithoutValidation("Accept", "application/ld+json, application/json;q=0.9");
            using var listResp = await http.Client.SendAsync(listReq, ct).ConfigureAwait(false);
            if (!listResp.IsSuccessStatusCode)
                return null;

            string? productId;
            DateTimeOffset? issued = null;
            await using (var s = await listResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            using (var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false))
            {
                if (!doc.RootElement.TryGetProperty("@graph", out var graph)
                    || graph.ValueKind != JsonValueKind.Array
                    || graph.GetArrayLength() == 0)
                    return null;

                var first = graph[0];
                productId = GetString(first, "id");
                if (GetString(first, "issuanceTime") is { } t
                    && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                    issued = dto;
            }

            if (string.IsNullOrWhiteSpace(productId))
                return null;

            using var prodReq = new HttpRequestMessage(HttpMethod.Get, $"https://api.weather.gov/products/{productId}");
            prodReq.Headers.TryAddWithoutValidation("Accept", "application/ld+json, application/json;q=0.9");
            using var prodResp = await http.Client.SendAsync(prodReq, ct).ConfigureAwait(false);
            if (!prodResp.IsSuccessStatusCode)
                return null;

            await using var ps = await prodResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var pdoc = await JsonDocument.ParseAsync(ps, cancellationToken: ct).ConfigureAwait(false);
            var text = GetString(pdoc.RootElement, "productText");
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var days2To7 = HazardOutlookParser.DaysTwoThroughSeven(text, forecastZone, countyZone);
            return days2To7 is null ? null : new HazardOutlook(days2To7, issued);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Covered when an active alert event names the same hazard (e.g. Winter Storm Watch).</summary>
    private static bool IsCoveredByAlert(ThreatKind kind, IReadOnlyList<WeatherAlertItem> alerts)
    {
        string[] keywords = kind switch
        {
            ThreatKind.Severe => ["severe thunderstorm", "tornado"],
            ThreatKind.WinterStorm => ["winter storm", "blizzard", "winter weather", "heavy snow", "lake effect snow", "snow squall"],
            ThreatKind.Ice => ["ice storm", "freezing rain", "winter storm"],
            ThreatKind.Flooding or ThreatKind.HeavyRain => ["flood"],
            ThreatKind.HighWind => ["high wind", "wind advisory", "extreme wind"],
            ThreatKind.Heat => ["heat"],
            ThreatKind.Cold => ["cold", "wind chill"],
            ThreatKind.Frost => ["frost", "freeze"],
            _ => [],
        };

        return alerts.Any(a =>
        {
            var ev = $"{a.Event} {a.Summary}".ToLowerInvariant();
            return keywords.Any(ev.Contains);
        });
    }

    private static (double Xmin, double Ymin, double Xmax, double Ymax) RegionalBbox(double latitude, double longitude)
    {
        // Same shape as the 560x420 export (4:3) so the location stays at the image center.
        const double LatPad = 3.75;
        var lonPad = LatPad * (560.0 / 420.0) / Math.Max(0.2, Math.Cos(latitude * Math.PI / 180.0));
        return (longitude - lonPad, latitude - LatPad, longitude + lonPad, latitude + LatPad);
    }

    private static Uri ExportUri(
        string mapServer,
        (double Xmin, double Ymin, double Xmax, double Ymax) bbox,
        string? layers,
        bool transparent)
    {
        var inv = CultureInfo.InvariantCulture;
        var b = string.Join(",", new[] { bbox.Xmin, bbox.Ymin, bbox.Xmax, bbox.Ymax }.Select(v => v.ToString("F4", inv)));
        var sb = new StringBuilder($"{mapServer}/export?bbox={Uri.EscapeDataString(b)}&bboxSR=4326&imageSR=4326");
        sb.Append("&size=560,420&dpi=96&format=png32");
        sb.Append(transparent ? "&transparent=true" : "&transparent=false");
        if (layers is not null)
            sb.Append("&layers=").Append(Uri.EscapeDataString("show:" + layers));
        sb.Append("&f=image");
        return new Uri(sb.ToString());
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? GetDouble(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static DateOnly? GetEpochDate(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(v.GetInt64()).UtcDateTime)
            : null;

    [GeneratedRegex(@"\d{8}")]
    private static partial Regex EightDigitDateRegex();
}
