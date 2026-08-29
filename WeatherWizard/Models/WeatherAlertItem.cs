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

    /// <summary>NWS <c>description</c> — main alert body (same as public "text data").</summary>
    public string Description { get; init; } = "";

    /// <summary>NWS <c>instruction</c> — recommended actions when present.</summary>
    public string Instruction { get; init; } = "";

    public Uri? Link { get; init; }

    public bool HasDetailText =>
        !string.IsNullOrWhiteSpace(Description) || !string.IsNullOrWhiteSpace(Instruction);
}
