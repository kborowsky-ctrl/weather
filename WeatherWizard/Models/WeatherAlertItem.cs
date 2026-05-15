namespace WeatherWizard.Models;

public sealed class WeatherAlertItem
{
    public required string Id { get; init; }

    public string Headline { get; init; } = "";

    public Uri? Link { get; init; }
}
