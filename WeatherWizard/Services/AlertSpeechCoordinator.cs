using WeatherWizard.Models;

namespace WeatherWizard.Services;

public sealed class AlertSpeechCoordinator(SpeechService speech)
{
    private readonly Dictionary<Guid, Dictionary<string, string>> _lastAlerts = new();

    public async Task OnAlertsUpdatedAsync(
        SavedLocation location,
        IReadOnlyList<WeatherAlertItem> alerts,
        CancellationToken ct = default)
    {
        var current = alerts.ToDictionary(
            a => a.Id,
            a => NwsAlertDisplayFormatter.GetDisplayLabel(a),
            StringComparer.Ordinal);

        if (!_lastAlerts.TryGetValue(location.Id, out var previous))
        {
            if (alerts.Count > 0 && IsSpeechEnabled())
            {
                var names = NwsAlertDisplayFormatter.FormatSpeechAlertNames(alerts);
                await speech.SpeakAsync(
                    $"Weather alert for {location.SpeechLocationLabel}: {names}.",
                    ct).ConfigureAwait(false);
            }

            _lastAlerts[location.Id] = current;
            return;
        }

        var added = alerts
            .Where(a => !previous.ContainsKey(a.Id))
            .ToList();
        var removedLabels = previous
            .Where(kv => !current.ContainsKey(kv.Key))
            .Select(kv => kv.Value)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (IsSpeechEnabled())
        {
            if (added.Count > 0)
            {
                var names = NwsAlertDisplayFormatter.FormatSpeechAlertNames(added);
                await speech.SpeakAsync(
                    $"There is a new weather alert for {location.SpeechLocationLabel}: {names}.",
                    ct).ConfigureAwait(false);
            }

            if (removedLabels.Count > 0)
            {
                var names = NwsAlertDisplayFormatter.JoinSpeechNames(removedLabels);
                await speech.SpeakAsync(
                    $"Weather alert cancelled for {location.SpeechLocationLabel}: {names}.",
                    ct).ConfigureAwait(false);
            }
        }

        _lastAlerts[location.Id] = current;
    }

    private static bool IsSpeechEnabled()
    {
        try
        {
            return WeatherWizard.App.Current.Locations.Settings.SpeakWeatherAlerts;
        }
        catch
        {
            return true;
        }
    }

    public void ResetLocation(Guid locationId) => _lastAlerts.Remove(locationId);

    public void ClearBaselines() => _lastAlerts.Clear();
}
