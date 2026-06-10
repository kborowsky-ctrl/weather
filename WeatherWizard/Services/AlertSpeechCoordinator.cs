using WeatherWizard.Models;

namespace WeatherWizard.Services;

public sealed class AlertSpeechCoordinator(SpeechService speech)
{
    private readonly Dictionary<Guid, HashSet<string>> _lastIds = new();

    public async Task OnAlertsUpdatedAsync(
        SavedLocation location,
        IReadOnlyList<WeatherAlertItem> alerts,
        CancellationToken ct = default)
    {
        var current = alerts.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);

        if (!_lastIds.TryGetValue(location.Id, out var previous))
        {
            if (alerts.Count > 0)
            {
                var lead = alerts[0].Summary.Trim();
                if (lead.Length == 0)
                    lead = alerts[0].Headline.Trim();
                if (lead.Length == 0)
                    lead = "Active alert.";
                if (alerts.Count == 1)
                    await speech.SpeakAsync($"Weather alert in effect for {location.SpeechLocationLabel}. {lead}", ct).ConfigureAwait(false);
                else
                    await speech.SpeakAsync($"Weather alerts in effect for {location.SpeechLocationLabel}. {lead} and {alerts.Count - 1} more.", ct).ConfigureAwait(false);
            }

            _lastIds[location.Id] = current;
            return;
        }

        var added = current.Except(previous, StringComparer.Ordinal).Any();
        var removed = previous.Except(current, StringComparer.Ordinal).Any();

        if (added)
            await speech.SpeakAsync($"There is a new weather alert for {location.SpeechLocationLabel}.", ct).ConfigureAwait(false);

        if (removed)
            await speech.SpeakAsync($"Weather alert for {location.SpeechLocationLabel} has been cancelled.", ct).ConfigureAwait(false);

        _lastIds[location.Id] = current;
    }

    public void ResetLocation(Guid locationId) => _lastIds.Remove(locationId);

    public void ClearBaselines() => _lastIds.Clear();
}
