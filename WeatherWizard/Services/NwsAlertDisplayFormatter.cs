using WeatherWizard.Models;

namespace WeatherWizard.Services;

public static class NwsAlertDisplayFormatter
{
    public static IReadOnlyList<string> GetDisplaySummaries(IReadOnlyList<WeatherAlertItem> alerts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        foreach (var alert in alerts)
        {
            var label = GetDisplayLabel(alert);
            if (label.Length == 0)
                continue;

            if (seen.Add(label))
                list.Add(label);
        }

        return list;
    }

    public static string GetDisplayLabel(WeatherAlertItem alert)
    {
        if (!string.IsNullOrWhiteSpace(alert.Summary))
            return alert.Summary.Trim();

        var fromHeadline = NwsAlertHeadlineFormatter.ToSummary(null, alert.Headline);
        if (!string.IsNullOrWhiteSpace(fromHeadline))
            return fromHeadline.Trim();

        return "Alert";
    }

    public static string FormatActiveAlerts(IReadOnlyList<WeatherAlertItem> alerts)
    {
        if (alerts.Count == 0)
            return "No active weather alerts.";

        var summaries = GetDisplaySummaries(alerts);

        if (summaries.Count == 0)
            return $"{alerts.Count} active alerts.";

        if (summaries.Count == 1)
            return $"Weather Alert: {summaries[0]}";

        return $"Weather Alert: {string.Join(" - ", summaries)}";
    }
}
