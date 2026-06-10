using WeatherWizard.Models;

namespace WeatherWizard.Services;

public static class NwsAlertDisplayFormatter
{
    public static string FormatActiveAlerts(IReadOnlyList<WeatherAlertItem> alerts)
    {
        if (alerts.Count == 0)
            return "No active weather alerts.";

        if (alerts.Count == 1)
            return alerts[0].Summary;

        var parts = alerts
            .Select(a => a.Summary)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return parts.Count == 0 ? $"{alerts.Count} active alerts." : string.Join(" · ", parts);
    }
}
