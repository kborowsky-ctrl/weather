using System.Globalization;
using System.Text.Json;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

/// <summary>Parses NWS grid forecast (<c>.../gridpoints/.../forecast</c>) — same source as weather.gov.</summary>
public sealed class NwsGridForecastClient(HttpClientFactory http)
{
    public async Task<IReadOnlyList<ForecastDayItem>?> TryGetForecastDaysAsync(Uri forecastUri, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, forecastUri);
            req.Headers.TryAddWithoutValidation("Accept", "application/geo+json, application/json;q=0.9, */*;q=0.8");

            using var resp = await http.Client.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("properties", out var props))
                return null;

            if (!props.TryGetProperty("periods", out var periods) || periods.ValueKind != JsonValueKind.Array)
                return null;

            var list = new List<ForecastDayItem>();
            foreach (var p in periods.EnumerateArray())
            {
                if (!IncludeNwsPeriod(p))
                    continue;

                var item = TryMapPeriod(p);
                if (item is not null)
                    list.Add(item);
            }

            return list.Count == 0 ? null : list;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Drop NWS night/evening periods except the single "Tonight" block.</summary>
    private static bool IncludeNwsPeriod(JsonElement p)
    {
        var isDaytime = true;
        if (p.TryGetProperty("isDaytime", out var dayEl))
            isDaytime = dayEl.ValueKind == JsonValueKind.True;

        if (isDaytime)
            return true;

        if (!p.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return false;

        var name = nameEl.GetString();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        name = name.Trim();
        if (name.Equals("Tonight", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.StartsWith("Tonight ", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static ForecastDayItem? TryMapPeriod(JsonElement p)
    {
        if (!p.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return null;

        var name = nameEl.GetString();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (!p.TryGetProperty("startTime", out var startEl) || startEl.ValueKind != JsonValueKind.String)
            return null;

        if (!DateTimeOffset.TryParse(startEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start))
            return null;

        var date = DateOnly.FromDateTime(start.LocalDateTime);

        if (!p.TryGetProperty("temperature", out var tempEl) || tempEl.ValueKind != JsonValueKind.Number)
            return null;

        var temp = tempEl.GetDouble();

        var isDaytime = true;
        if (p.TryGetProperty("isDaytime", out var dayEl))
            isDaytime = dayEl.ValueKind == JsonValueKind.True;

        var shortFc = "";
        if (p.TryGetProperty("shortForecast", out var sf) && sf.ValueKind == JsonValueKind.String)
            shortFc = sf.GetString() ?? "";

        int? pop = null;
        if (p.TryGetProperty("probabilityOfPrecipitation", out var popObj)
            && popObj.ValueKind == JsonValueKind.Object
            && popObj.TryGetProperty("value", out var popVal)
            && popVal.ValueKind == JsonValueKind.Number
            && popVal.TryGetInt32(out var popInt))
        {
            pop = popInt;
        }

        var code = NwsShortForecastInterpreter.ApproximateWmoCode(shortFc);
        var hiLo = isDaytime ? $"H {Math.Round(temp)}°" : $"L {Math.Round(temp)}°";
        var popDisp = pop is int px ? $"{px}%" : "—";
        var isNightPeriod = !isDaytime;
        var phaseAt = start;

        return new ForecastDayItem
        {
            Date = date,
            DayLabel = name.Trim(),
            PeriodTitle = name.Trim(),
            DateSubtitle = ForecastDisplayFormat.OrdinalDate(date),
            ConditionsDisplay = string.IsNullOrWhiteSpace(shortFc) ? "—" : shortFc.Trim(),
            ConditionEmoji = WeatherCodeInterpreter.Emoji(code, isNightPeriod, phaseAt),
            HiLoDisplay = hiLo,
            PrecipPercentDisplay = popDisp,
            WeatherCode = code,
            Summary = shortFc.Trim(),
            HighF = temp,
            LowF = temp,
            PrecipChance = pop,
            IsNightPeriod = isNightPeriod,
            MoonPhaseAt = phaseAt,
        };
    }
}
