using System.Globalization;
using System.Text.Json;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

public sealed class OpenMeteoForecastClient(HttpClientFactory http)
{
    public async Task<ForecastBundle> GetForecastAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var url =
            "https://api.open-meteo.com/v1/forecast?" +
            $"latitude={lat}&longitude={lon}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code," +
            "wind_speed_10m,wind_direction_10m,dew_point_2m,surface_pressure,visibility" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,sunrise,sunset" +
            "&hourly=temperature_2m,precipitation_probability,weather_code" +
            "&temperature_unit=fahrenheit&wind_speed_unit=mph&timezone=auto&forecast_days=7";

        using var resp = await http.Client.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        var utcOffsetSeconds = root.TryGetProperty("utc_offset_seconds", out var offEl) ? offEl.GetInt32() : 0;
        var tzOffset = TimeSpan.FromSeconds(utcOffsetSeconds);

        CurrentConditionsPanel panel = CurrentConditionsPanel.Blank;
        if (root.TryGetProperty("current", out var cur))
        {
            var temp = cur.TryGetDouble("temperature_2m");
            var feels = cur.TryGetDouble("apparent_temperature");
            var code = cur.TryGetInt("weather_code") ?? 0;
            var wind = cur.TryGetDouble("wind_speed_10m");
            var windDir = cur.TryGetInt("wind_direction_10m");
            var rh = cur.TryGetInt("relative_humidity_2m");
            var dew = cur.TryGetDouble("dew_point_2m");
            var presHpa = cur.TryGetDouble("surface_pressure");
            var visM = cur.TryGetDouble("visibility");

            var wxShort = WeatherCodeInterpreter.Short(code);

            var r1L = temp is null ? "—" : $"{Math.Round(temp.Value)}°F";
            var r1R = feels is null ? "—" : $"Feels {Math.Round(feels.Value)}°F";

            var r2L = dew is null ? "—" : $"Dew {Math.Round(dew.Value)}°F";
            var r2R = rh is null ? "—" : $"{rh}% RH";

            string r3L;
            if (presHpa is null)
                r3L = "—";
            else
            {
                var inHg = presHpa.Value / 33.8639;
                r3L = $"{inHg:0.00} inHg";
            }

            string r3R;
            if (visM is null || visM <= 0)
                r3R = "—";
            else
            {
                var miles = visM.Value / 1609.344;
                r3R = miles >= 0.25 ? $"{miles:0.#} mi vis" : $"{visM.Value:0} m vis";
            }

            string r4L;
            if (wind is null)
                r4L = "—";
            else if (windDir is int wd)
                r4L = $"{WindCompass.FromDegrees(wd)} {Math.Round(wind.Value)} mph";
            else
                r4L = $"{Math.Round(wind.Value)} mph";

            var r4R = string.IsNullOrEmpty(wxShort) ? "—" : wxShort;

            panel = new CurrentConditionsPanel(
                r1L, r1R,
                r2L, r2R,
                r3L, r3R,
                r4L, r4R,
                "—", "—",
                WeatherCodeInterpreter.Emoji(code));
        }

        var days = new List<ForecastDayItem>();
        if (root.TryGetProperty("daily", out var daily)
            && daily.TryGetProperty("time", out var times)
            && times.ValueKind == JsonValueKind.Array)
        {
            var codes = daily.GetIntArray("weather_code");
            var highs = daily.GetDoubleArray("temperature_2m_max");
            var lows = daily.GetDoubleArray("temperature_2m_min");
            var pops = daily.GetIntArrayNullable("precipitation_probability_max");
            var sunrises = daily.GetStringArray("sunrise");
            var sunsets = daily.GetStringArray("sunset");

            var i = 0;
            foreach (var timeEl in times.EnumerateArray())
            {
                if (timeEl.ValueKind != JsonValueKind.String)
                {
                    i++;
                    continue;
                }

                if (!DateOnly.TryParse(timeEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    i++;
                    continue;
                }

                var code = i < codes.Count ? codes[i] : 0;
                var hi = i < highs.Count ? highs[i] : 0;
                var lo = i < lows.Count ? lows[i] : 0;
                int? pop = i < pops.Count ? pops[i] : null;
                var sr = i < sunrises.Count ? sunrises[i] : null;
                var ss = i < sunsets.Count ? sunsets[i] : null;

                var label = date == DateOnly.FromDateTime(DateTime.Today)
                    ? "Today"
                    : date.ToString("ddd", CultureInfo.CurrentCulture);

                var hiLo = $"H {Math.Round(hi)}° / L {Math.Round(lo)}°";
                var popDisp = pop is int px ? $"{px}%" : "—";

                days.Add(new ForecastDayItem
                {
                    Date = date,
                    DayLabel = label,
                    PeriodTitle = label,
                    DateSubtitle = ForecastDisplayFormat.OrdinalDate(date),
                    ConditionsDisplay = WeatherCodeInterpreter.Describe(code),
                    ConditionEmoji = WeatherCodeInterpreter.Emoji(code),
                    HiLoDisplay = hiLo,
                    PrecipPercentDisplay = popDisp,
                    WeatherCode = code,
                    Summary = WeatherCodeInterpreter.Describe(code),
                    HighF = hi,
                    LowF = lo,
                    PrecipChance = pop,
                    SunriseText = ForecastFormatters.FormatIsoLocalTime(sr),
                    SunsetText = ForecastFormatters.FormatIsoLocalTime(ss),
                });
                i++;
            }
        }

        if (days.Count > 0)
        {
            var today = days[0];
            var sr = string.IsNullOrWhiteSpace(today.SunriseText) ? "—" : $"↑ {today.SunriseText}";
            var ss = string.IsNullOrWhiteSpace(today.SunsetText) ? "—" : $"↓ {today.SunsetText}";
            panel = panel with { Row5Left = sr, Row5Right = ss };
        }

        var hourlyTimes = new List<DateTimeOffset>();
        List<double> hourlyTemps = [];
        List<int?> hourlyPops = [];
        List<int> hourlyCodes = [];
        if (root.TryGetProperty("hourly", out var hourly))
        {
            hourlyTimes = ParseHourlyTimes(hourly, tzOffset);
            hourlyTemps = hourly.GetHourlyDoubleArray("temperature_2m");
            hourlyPops = hourly.GetHourlyIntArrayNullable("precipitation_probability");
            hourlyCodes = hourly.GetHourlyIntArray("weather_code");
        }

        List<string?> sunrisesIso = [];
        List<string?> sunsetsIso = [];
        if (root.TryGetProperty("daily", out var dSun))
        {
            sunrisesIso = dSun.GetStringArray("sunrise");
            sunsetsIso = dSun.GetStringArray("sunset");
        }

        ForecastDayItem? tonight = null;
        if (hourlyTimes.Count > 0 && sunsetsIso.Count > 0 && sunrisesIso.Count > 1)
        {
            tonight = BuildNightForecast(
                "Tonight",
                sunsetsIso[0],
                sunrisesIso.Count > 1 ? sunrisesIso[1] : null,
                hourlyTimes, hourlyTemps, hourlyPops, hourlyCodes);
        }

        var merged = new List<ForecastDayItem>();
        if (days.Count > 0)
        {
            merged.Add(days[0]);
            if (tonight is not null)
                merged.Add(tonight);
            for (var i = 1; i < days.Count; i++)
                merged.Add(days[i]);
        }
        else if (tonight is not null)
        {
            merged.Add(tonight);
        }

        return new ForecastBundle(panel, merged);
    }

    private static ForecastDayItem? BuildNightForecast(
        string periodTitle,
        string? sunsetIso,
        string? sunriseNextIso,
        IReadOnlyList<DateTimeOffset> hourTimes,
        IReadOnlyList<double> hourTemps,
        IReadOnlyList<int?> hourPops,
        IReadOnlyList<int> hourCodes)
    {
        if (string.IsNullOrWhiteSpace(sunsetIso) || string.IsNullOrWhiteSpace(sunriseNextIso))
            return null;

        if (!TryParseOpenMeteoInstant(sunsetIso, out var sunset))
            return null;
        if (!TryParseOpenMeteoInstant(sunriseNextIso, out var sunriseEnd))
            return null;

        if (sunriseEnd <= sunset)
            return null;

        var temps = new List<double>();
        var codes = new List<int>();
        int? maxPop = null;

        for (var i = 0; i < hourTimes.Count; i++)
        {
            var t = hourTimes[i];
            if (t < sunset || t >= sunriseEnd)
                continue;

            if (i < hourTemps.Count && !double.IsNaN(hourTemps[i]))
                temps.Add(hourTemps[i]);

            if (i < hourCodes.Count)
                codes.Add(hourCodes[i]);

            if (i < hourPops.Count && hourPops[i] is int p)
                maxPop = maxPop is int m ? Math.Max(m, p) : p;
        }

        if (temps.Count == 0)
            return null;

        var minT = temps.Min();
        var maxT = temps.Max();
        var wxCode = MostFrequentCode(codes);
        var hiLo = $"H {Math.Round(maxT)}° / L {Math.Round(minT)}°";
        var popDisp = maxPop is int px ? $"{px}%" : "—";
        var dateOnly = DateOnly.FromDateTime(sunset.LocalDateTime);

        return new ForecastDayItem
        {
            Date = dateOnly,
            DayLabel = periodTitle,
            PeriodTitle = periodTitle,
            DateSubtitle = ForecastDisplayFormat.OrdinalDate(dateOnly),
            ConditionsDisplay = WeatherCodeInterpreter.Describe(wxCode),
            ConditionEmoji = WeatherCodeInterpreter.Emoji(wxCode),
            HiLoDisplay = hiLo,
            PrecipPercentDisplay = popDisp,
            WeatherCode = wxCode,
            Summary = WeatherCodeInterpreter.Describe(wxCode),
            HighF = maxT,
            LowF = minT,
            PrecipChance = maxPop,
        };
    }

    private static int MostFrequentCode(IReadOnlyList<int> codes)
    {
        if (codes.Count == 0)
            return 0;

        return codes
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Key;
    }

    private static bool TryParseOpenMeteoInstant(string iso, out DateTimeOffset dto)
    {
        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dto))
            return true;

        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            dto = new DateTimeOffset(dt, TimeSpan.Zero);
            return true;
        }

        dto = default;
        return false;
    }

    private static List<DateTimeOffset> ParseHourlyTimes(JsonElement hourly, TimeSpan tzOffset)
    {
        var list = new List<DateTimeOffset>();
        if (!hourly.TryGetProperty("time", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.String)
                continue;

            var s = el.GetString();
            if (string.IsNullOrWhiteSpace(s))
                continue;

            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                list.Add(dto);
                continue;
            }

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                list.Add(new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tzOffset));
            }
        }

        return list;
    }

}

public sealed record ForecastBundle(CurrentConditionsPanel Current, IReadOnlyList<ForecastDayItem> Days);

file static class WindCompass
{
    private static readonly string[] Rose =
    [
        "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW",
    ];

    public static string FromDegrees(int degrees)
    {
        degrees = ((degrees % 360) + 360) % 360;
        var idx = (int)Math.Round(degrees / 22.5) % 16;
        return Rose[idx];
    }
}

file static class JsonCurrentExtensions
{
    public static double? TryGetDouble(this JsonElement cur, string name) =>
        cur.TryGetProperty(name, out var el) && el.TryGetDouble(out var v) ? v : null;

    public static int? TryGetInt(this JsonElement cur, string name) =>
        cur.TryGetProperty(name, out var el) && el.TryGetInt32(out var v) ? v : null;
}

file static class ForecastFormatters
{
    public static string? FormatIsoLocalTime(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
            return null;

        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            return dto.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
            return local.ToString("t", CultureInfo.CurrentCulture);

        return iso;
    }
}

file static class DailyJsonExtensions
{
    public static List<int> GetIntArray(this JsonElement daily, string name)
    {
        var list = new List<int>();
        if (!daily.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.TryGetInt32(out var v)) list.Add(v);
            else list.Add(0);
        }
        return list;
    }

    public static List<int?> GetIntArrayNullable(this JsonElement daily, string name)
    {
        var list = new List<int?>();
        if (!daily.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Null) list.Add(null);
            else if (el.TryGetInt32(out var v)) list.Add(v);
            else list.Add(null);
        }
        return list;
    }

    public static List<double> GetDoubleArray(this JsonElement daily, string name)
    {
        var list = new List<double>();
        if (!daily.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.TryGetDouble(out var v)) list.Add(v);
            else list.Add(double.NaN);
        }
        return list;
    }

    public static List<string?> GetStringArray(this JsonElement daily, string name)
    {
        var list = new List<string?>();
        if (!daily.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Null)
                list.Add(null);
            else if (el.ValueKind == JsonValueKind.String)
                list.Add(el.GetString());
            else
                list.Add(null);
        }
        return list;
    }

    public static List<double> GetHourlyDoubleArray(this JsonElement hourly, string name)
    {
        var list = new List<double>();
        if (!hourly.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Null)
                list.Add(double.NaN);
            else if (el.TryGetDouble(out var v))
                list.Add(v);
            else
                list.Add(double.NaN);
        }
        return list;
    }

    public static List<int?> GetHourlyIntArrayNullable(this JsonElement hourly, string name)
    {
        var list = new List<int?>();
        if (!hourly.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Null) list.Add(null);
            else if (el.TryGetInt32(out var v)) list.Add(v);
            else list.Add(null);
        }
        return list;
    }

    public static List<int> GetHourlyIntArray(this JsonElement hourly, string name)
    {
        var list = new List<int>();
        if (!hourly.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.TryGetInt32(out var v)) list.Add(v);
            else list.Add(0);
        }
        return list;
    }
}
