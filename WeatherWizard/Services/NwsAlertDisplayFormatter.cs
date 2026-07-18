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
        // Prefer NWS event name for speech/UI (e.g. "Air Quality Alert").
        if (!string.IsNullOrWhiteSpace(alert.Event))
            return alert.Event.Trim();

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

    /// <summary>Spoken alert names without the "Weather Alert:" UI prefix.</summary>
    public static string FormatSpeechAlertNames(IReadOnlyList<WeatherAlertItem> alerts)
        => JoinSpeechNames(GetDisplaySummaries(alerts));

    public static string JoinSpeechNames(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return "a weather alert";
        if (names.Count == 1)
            return names[0];
        if (names.Count == 2)
            return $"{names[0]} and {names[1]}";

        return string.Join(", ", names.Take(names.Count - 1)) + ", and " + names[^1];
    }
}
