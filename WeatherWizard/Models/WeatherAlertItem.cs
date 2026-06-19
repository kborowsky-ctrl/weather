namespace WeatherWizard.Models;

public sealed class WeatherAlertItem
{
    public required string Id { get; init; }

    public string Headline { get; init; } = "";

    /// <summary>Short label for UI (event name, no issue/expire dates).</summary>
    public string Summary { get; init; } = "";

    /// <summary>NWS <c>event</c> (e.g. Heat Advisory).</summary>
    public string Event { get; init; } = "";

    /// <summary>NWS <c>areaDesc</c> — counties/zones affected.</summary>
    public string? AreaDesc { get; init; }

    /// <summary>NWS <c>ends</c> instant when present.</summary>
    public DateTimeOffset? Ends { get; init; }

    public Uri? Link { get; init; }
}
