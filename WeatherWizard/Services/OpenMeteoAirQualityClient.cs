using System.Globalization;
using System.Text.Json;

namespace WeatherWizard.Services;

public sealed class OpenMeteoAirQualityClient(HttpClientFactory http)
{
    public async Task<int?> GetUsAqiAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var url =
            "https://air-quality-api.open-meteo.com/v1/air-quality?" +
            $"latitude={lat}&longitude={lon}&current=us_aqi&timezone=auto";

        try
        {
            using var resp = await http.Client.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("current", out var cur))
                return null;

            if (cur.TryGetProperty("us_aqi", out var el)
                && el.ValueKind == JsonValueKind.Number
                && el.TryGetInt32(out var v))
                return v;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
