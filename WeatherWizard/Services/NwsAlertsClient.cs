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
            $"https://api.weather.gov/alerts/active?point={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

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

            var headline = props.TryGetProperty("headline", out var hEl) && hEl.ValueKind == JsonValueKind.String
                ? hEl.GetString() ?? ""
                : props.TryGetProperty("event", out var eEl) && eEl.ValueKind == JsonValueKind.String
                    ? eEl.GetString() ?? "Alert"
                    : "Alert";

            Uri? link = null;
            if (props.TryGetProperty("web", out var webEl) && webEl.ValueKind == JsonValueKind.String)
            {
                var s = webEl.GetString();
                if (Uri.TryCreate(s, UriKind.Absolute, out var u))
                    link = u;
            }

            link ??= Uri.TryCreate(id, UriKind.Absolute, out var idUri) ? idUri : null;

            list.Add(new WeatherAlertItem { Id = id, Headline = headline, Link = link });
        }

        return list;
    }
}
