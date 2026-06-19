using WeatherWizard.Models;

namespace WeatherWizard.Services;

/// <summary>
/// NWS often returns overlapping issuances (e.g. two Heat Advisories with different end times, same zones).
/// Keep the current continuation per event + area so the UI does not imply a second alert type.
/// </summary>
public static class NwsAlertRedundancyFilter
{
    public static List<WeatherAlertItem> CollapseOverlapping(IReadOnlyList<WeatherAlertItem> alerts)
    {
        if (alerts.Count <= 1)
            return alerts.ToList();

        return alerts
            .GroupBy(Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(a => a.Ends ?? DateTimeOffset.MinValue)
                .ThenByDescending(a => a.Id, StringComparer.Ordinal))
            .Select(g => g.First())
            .ToList();
    }

    private static string Key(WeatherAlertItem a)
    {
        var ev = string.IsNullOrWhiteSpace(a.Event) ? a.Summary : a.Event;
        var area = a.AreaDesc ?? "";
        return $"{ev.Trim()}|{area.Trim()}";
    }
}
