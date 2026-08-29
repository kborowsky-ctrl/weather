using System.Globalization;
using System.Text.Json;

namespace WeatherWizard.Services;

public sealed class CpcSeasonalOutlookClient(HttpClientFactory http)
{
    private const string TempMap =
        "https://mapservices.weather.noaa.gov/vector/rest/services/outlooks/cpc_sea_temp_outlk/MapServer";
    private const string PrecipMap =
        "https://mapservices.weather.noaa.gov/vector/rest/services/outlooks/cpc_sea_precip_outlk/MapServer";

    public async Task<SeasonalOutlookSnapshot?> TryGetAsync(
        double latitude,
        double longitude,
        SeasonalOutlookWindow.SeasonTarget target,
        CancellationToken ct = default)
    {
        var lead = await FindLeadIndexAsync(TempMap, target, ct).ConfigureAwait(false);
        if (lead is null)
            return null;

        var temp = await QueryPointAsync(TempMap, lead.Value, latitude, longitude, ct).ConfigureAwait(false);
        var precip = await QueryPointAsync(PrecipMap, lead.Value, latitude, longitude, ct).ConfigureAwait(false);

        if (temp is null && precip is null)
            return null;

        var seas = temp?.ValidSeas ?? precip?.ValidSeas ?? $"{target.Code} {target.SeasonStart.Year}";
        var issued = temp?.ForecastDate ?? precip?.ForecastDate ?? target.ExpectedIssuance;

        var bbox = BuildRegionalBbox(latitude, longitude);
        var tempMap = BuildExportUri(TempMap, lead.Value, bbox);
        var precipMap = BuildExportUri(PrecipMap, lead.Value, bbox);

        return new SeasonalOutlookSnapshot(
            target,
            seas,
            issued,
            ToCell(temp),
            ToCell(precip),
            tempMap,
            precipMap);
    }

    private static CpcOutlookCell? ToCell(OutlookPoint? p) =>
        p is null ? null : new CpcOutlookCell(p.Cat, p.Prob);

    private async Task<int?> FindLeadIndexAsync(
        string mapServer,
        SeasonalOutlookWindow.SeasonTarget target,
        CancellationToken ct)
    {
        // Near a season boundary only Lead 1–3 are relevant.
        for (var lead = 0; lead <= 3; lead++)
        {
            var sample = await QueryFirstAsync(mapServer, lead, ct).ConfigureAwait(false);
            if (sample is not null && SeasonalOutlookWindow.MatchesValidSeas(sample.ValidSeas, target))
                return lead;
        }

        return null;
    }

    private async Task<OutlookPoint?> QueryFirstAsync(string mapServer, int lead, CancellationToken ct)
    {
        var url =
            $"{mapServer}/{lead}/query?where=1%3D1&outFields=fcst_date,valid_seas,prob,cat&returnGeometry=false&resultRecordCount=1&f=json";
        return await ReadFirstFeatureAsync(url, ct).ConfigureAwait(false);
    }

    private async Task<OutlookPoint?> QueryPointAsync(
        string mapServer,
        int lead,
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var url =
            $"{mapServer}/{lead}/query?geometry={lon}%2C{lat}&geometryType=esriGeometryPoint&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=fcst_date%2Cvalid_seas%2Cprob%2Ccat&returnGeometry=false&f=json";
        return await ReadFirstFeatureAsync(url, ct).ConfigureAwait(false);
    }

    private async Task<OutlookPoint?> ReadFirstFeatureAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var resp = await http.Client.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("features", out var features)
                || features.ValueKind != JsonValueKind.Array
                || features.GetArrayLength() == 0)
                return null;

            var attrs = features[0].GetProperty("attributes");
            var cat = attrs.TryGetProperty("cat", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() ?? ""
                : "";
            var seas = attrs.TryGetProperty("valid_seas", out var vs) && vs.ValueKind == JsonValueKind.String
                ? vs.GetString() ?? ""
                : "";
            double? prob = null;
            if (attrs.TryGetProperty("prob", out var p) && p.ValueKind == JsonValueKind.Number)
                prob = p.GetDouble();

            DateOnly? issued = null;
            if (attrs.TryGetProperty("fcst_date", out var fd) && fd.ValueKind == JsonValueKind.Number)
            {
                var ms = fd.GetInt64();
                issued = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime);
            }

            return new OutlookPoint(seas, cat, prob, issued);
        }
        catch
        {
            return null;
        }
    }

    private static (double Xmin, double Ymin, double Xmax, double Ymax) BuildRegionalBbox(
        double latitude,
        double longitude)
    {
        // ~400–500 mile regional crop around the saved location.
        const double LatPad = 3.5;
        var lonPad = 3.5 / Math.Max(0.2, Math.Cos(latitude * Math.PI / 180.0));
        lonPad = Math.Clamp(lonPad, 3.0, 6.0);

        return (
            longitude - lonPad,
            Math.Clamp(latitude - LatPad, 15, 72),
            longitude + lonPad,
            Math.Clamp(latitude + LatPad, 15, 72));
    }

    private static Uri BuildExportUri(
        string mapServer,
        int lead,
        (double Xmin, double Ymin, double Xmax, double Ymax) bbox)
    {
        var b =
            $"{bbox.Xmin.ToString(CultureInfo.InvariantCulture)},{bbox.Ymin.ToString(CultureInfo.InvariantCulture)},{bbox.Xmax.ToString(CultureInfo.InvariantCulture)},{bbox.Ymax.ToString(CultureInfo.InvariantCulture)}";
        var url =
            $"{mapServer}/export?bbox={Uri.EscapeDataString(b)}&bboxSR=4326&imageSR=4326&size=560,400&dpi=96&format=png32&transparent=false&layers=show%3A{lead}&f=image";
        return new Uri(url);
    }

    private sealed record OutlookPoint(string ValidSeas, string Cat, double? Prob, DateOnly? ForecastDate);
}

public sealed record SeasonalOutlookSnapshot(
    SeasonalOutlookWindow.SeasonTarget Target,
    string ValidSeasLabel,
    DateOnly IssuedOn,
    CpcOutlookCell? Temperature,
    CpcOutlookCell? Precipitation,
    Uri TemperatureMapUri,
    Uri PrecipitationMapUri)
{
    public string SummaryText
    {
        get
        {
            var season = $"{Target.DisplayName} ({ValidSeasLabel})";
            var temp = FormatCell("Temperature", Temperature);
            var precip = FormatCell("Precipitation", Precipitation);
            return
                $"{season} outlook for your area\n" +
                $"Issued {IssuedOn:MMM d, yyyy} (CPC)\n\n" +
                $"{temp}\n{precip}\n\n" +
                "Maps show the regional Climate Prediction Center probability outlook " +
                "(above / near / below normal), not a day-by-day forecast.";
        }
    }

    private static string FormatCell(string label, CpcOutlookCell? cell)
    {
        if (cell is null || string.IsNullOrWhiteSpace(cell.Category))
            return $"{label}: no outlook for this point";

        var cat = DescribeCategory(cell.Category);
        if (cell.Probability is double p && p > 0)
            return $"{label}: {cat} ({p:0}% chance in CPC categories)";
        return $"{label}: {cat}";
    }

    private static string DescribeCategory(string cat) =>
        cat.Trim().ToUpperInvariant() switch
        {
            "ABOVE" => "Above-normal more likely",
            "BELOW" => "Below-normal more likely",
            "NORMAL" => "Near-normal more likely",
            "EC" => "Equal chances (no strong tilt)",
            _ => cat.Trim(),
        };
}

public sealed record CpcOutlookCell(string Category, double? Probability);
