using System.Globalization;
using System.Text.Json;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

public sealed class NwsAlertsClient(HttpClientFactory http)
{
    public async Task<IReadOnlyList<WeatherAlertItem>> GetActiveForPointAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var url =
            $"https://api.weather.gov/alerts/active?point={latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "application/geo+json, application/json;q=0.9, */*;q=0.8");

        using var resp = await http.Client.SendAsync(req, ct).ConfigureAwait(false);
        if ((int)resp.StatusCode == 404)
            return [];
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;
        if (!root.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<WeatherAlertItem>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var props))
                continue;

            var id = props.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(id))
                continue;

            var rawEvent = props.TryGetProperty("event", out var eEl) && eEl.ValueKind == JsonValueKind.String
                ? eEl.GetString() ?? ""
                : "";

            var rawHeadline = props.TryGetProperty("headline", out var hEl) && hEl.ValueKind == JsonValueKind.String
                ? hEl.GetString() ?? ""
                : string.IsNullOrWhiteSpace(rawEvent) ? "Alert" : rawEvent;

            var headline = NwsAlertHeadlineFormatter.Format(rawHeadline);
            var summary = NwsAlertHeadlineFormatter.ToSummary(rawEvent, rawHeadline);

            var webFromApi = props.TryGetProperty("web", out var webEl) && webEl.ValueKind == JsonValueKind.String
                ? webEl.GetString()
                : null;

            DateTimeOffset? sent = null;
            if (props.TryGetProperty("sent", out var sentEl) && sentEl.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(sentEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var sentDto))
                sent = sentDto;

            string? vtec = null;
            if (props.TryGetProperty("parameters", out var pars)
                && pars.TryGetProperty("VTEC", out var vtecArr)
                && vtecArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in vtecArr.EnumerateArray())
                {
                    if (v.ValueKind != JsonValueKind.String)
                        continue;
                    vtec = v.GetString();
                    break;
                }
            }

            var link = NwsAlertPublicLink.Resolve(webFromApi, vtec, sent, latitude, longitude);

            string? areaDesc = null;
            if (props.TryGetProperty("areaDesc", out var areaEl) && areaEl.ValueKind == JsonValueKind.String)
                areaDesc = areaEl.GetString();

            DateTimeOffset? ends = null;
            if (props.TryGetProperty("ends", out var endsEl) && endsEl.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(endsEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var endsDto))
                ends = endsDto;

            var description = props.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String
                ? descEl.GetString() ?? ""
                : "";
            var instruction = props.TryGetProperty("instruction", out var instEl) && instEl.ValueKind == JsonValueKind.String
                ? instEl.GetString() ?? ""
                : "";

            list.Add(new WeatherAlertItem
            {
                Id = id,
                Headline = headline,
                Summary = summary,
                Event = rawEvent.Trim(),
                AreaDesc = areaDesc,
                Ends = ends,
                Description = description.Trim(),
                Instruction = instruction.Trim(),
                Link = link,
            });
        }

        return NwsAlertRedundancyFilter.CollapseOverlapping(list);
    }
}
