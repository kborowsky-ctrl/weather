using System.Text.Json;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

public sealed class OpenMeteoGeocodingClient(HttpClientFactory http)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<GeocodeHit>> SearchAsync(
        string query,
        string? countryCode = null,
        CancellationToken ct = default)
    {
        query = query.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var url =
            "https://geocoding-api.open-meteo.com/v1/search?" +
            $"name={Uri.EscapeDataString(query)}&count=10&language=en&format=json";

        if (!string.IsNullOrWhiteSpace(countryCode))
            url += $"&countryCode={Uri.EscapeDataString(countryCode)}";

        using var resp = await http.Client.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<GeocodeHit>();
        foreach (var el in results.EnumerateArray())
        {
            var hit = new GeocodeHit
            {
                Name = el.GetPropertyOrDefault("name"),
                Admin1 = el.GetPropertyOrDefault("admin1"),
                CountryCode = el.GetPropertyOrDefault("country_code"),
                Latitude = el.GetDoubleOr("latitude"),
                Longitude = el.GetDoubleOr("longitude"),
            };
            if (hit.Latitude is null || hit.Longitude is null)
                continue;
            list.Add(hit);
        }

        return list;
    }
}

public sealed class GeocodeHit
{
    public string Name { get; init; } = "";

    public string? Admin1 { get; init; }

    public string CountryCode { get; init; } = "";

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string DisplayLabel =>
        LocationDisplayFormatter.TabShort(Name, Name, Admin1, CountryCode);

    public SavedLocation ToSavedLocation()
    {
        return new SavedLocation
        {
            DisplayName = LocationDisplayFormatter.TabShort(Name, Name, Admin1, CountryCode),
            Latitude = Latitude!.Value,
            Longitude = Longitude!.Value,
            CountryCode = CountryCode.ToUpperInvariant(),
            Admin1 = Admin1,
            Locality = Name,
        };
    }
}

file static class JsonExtensions
{
    public static string GetPropertyOrDefault(this JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";

    public static double? GetDoubleOr(this JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.TryGetDouble(out var d) ? d : null;
}
