namespace WeatherWizard.Models;

/// <summary>One pollen.com day: index 0-12 and the leading allergens.</summary>
public sealed record PollenDay(string Label, double Index, IReadOnlyList<string> Triggers)
{
    public string Category => PollenReport.CategoryFor(Index);
}

public sealed record PollenReport(string Zip, string? DisplayLocation, IReadOnlyList<PollenDay> Days)
{
    /// <summary>pollen.com "Medium-High" starts here.</summary>
    public const double ElevatedThreshold = 7.3;

    public const double HighThreshold = 9.7;

    public PollenDay? Today => Days.FirstOrDefault(d => d.Label == "Today");

    public PollenDay? Tomorrow => Days.FirstOrDefault(d => d.Label == "Tomorrow");

    /// <summary>The day that raises the badge: today at Medium-High or above, else tomorrow at High.</summary>
    public PollenDay? AlertDay =>
        Today is { Index: >= ElevatedThreshold } today ? today
        : Tomorrow is { Index: >= HighThreshold } tomorrow ? tomorrow
        : null;

    public bool IsElevated => AlertDay is not null;

    public bool IsHigh => AlertDay?.Index >= HighThreshold;

    public string BadgeText
    {
        get
        {
            if (AlertDay is not { } day)
                return "";
            var level = day.Index >= HighThreshold ? "High" : "Medium-high";
            var when = day.Label == "Tomorrow" ? " tomorrow" : "";
            var trigger = day.Triggers.Count > 0 ? $" ({day.Triggers[0].ToLowerInvariant()})" : "";
            return $"{level} pollen{when}{trigger}";
        }
    }

    public static string CategoryFor(double index) => index switch
    {
        >= HighThreshold => "High",
        >= ElevatedThreshold => "Medium-High",
        >= 4.9 => "Medium",
        >= 2.5 => "Low-Medium",
        _ => "Low",
    };
}
