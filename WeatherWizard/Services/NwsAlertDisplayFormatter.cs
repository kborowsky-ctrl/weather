using WeatherWizard.Models;

namespace WeatherWizard.Services;

public static class NwsAlertDisplayFormatter
{
    public static string FormatActiveAlerts(IReadOnlyList<WeatherAlertItem> alerts)
    {
        if (alerts.Count == 0)
            return "No active weather alerts.";

        var summaries = alerts
            .Select(a => a.Summary.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (summaries.Count == 0)
            return $"{alerts.Count} active alerts.";

        if (summaries.Count == 1)
            return $"Weather Alert: {summaries[0]}";

        return $"Weather Alert: {string.Join(" - ", summaries)}";
    }
}
