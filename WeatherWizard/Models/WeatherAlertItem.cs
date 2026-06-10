namespace WeatherWizard.Models;

public sealed class WeatherAlertItem
{
    public required string Id { get; init; }

    public string Headline { get; init; } = "";

    /// <summary>Short label for UI (event name, no issue/expire dates).</summary>
    public string Summary { get; init; } = "";

    public Uri? Link { get; init; }
}
