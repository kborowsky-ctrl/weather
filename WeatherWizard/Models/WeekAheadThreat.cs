using System.Globalization;

namespace WeatherWizard.Models;

/// <summary>Kinds in badge priority order (lowest value wins when several apply).</summary>
public enum ThreatKind
{
    Severe,
    WinterStorm,
    Ice,
    Flooding,
    Heat,
    Cold,
    HeavyRain,
    HighWind,
    Frost,
}

public sealed record WeekAheadThreat(
    ThreatKind Kind,
    DateOnly Start,
    DateOnly End,
    IReadOnlyList<string> Sources)
{
    public string Title => Kind switch
    {
        ThreatKind.Severe => "Severe storm potential",
        ThreatKind.WinterStorm => "Winter storm potential",
        ThreatKind.Ice => "Ice storm potential",
        ThreatKind.Flooding => "Flooding potential",
        ThreatKind.Heat => "Extreme heat potential",
        ThreatKind.Cold => "Extreme cold potential",
        ThreatKind.HeavyRain => "Heavy rain potential",
        ThreatKind.HighWind => "High wind potential",
        ThreatKind.Frost => "Frost/freeze potential",
        _ => "Weather threat potential",
    };

    public string DateRange
    {
        get
        {
            var culture = CultureInfo.CurrentCulture;
            var start = Start.ToString("ddd MMM d", culture);
            return End <= Start ? start : $"{start} - {End.ToString("ddd MMM d", culture)}";
        }
    }

    public string ShortDays
    {
        get
        {
            var culture = CultureInfo.CurrentCulture;
            var start = Start.ToString("ddd", culture);
            return End <= Start ? start : $"{start}-{End.ToString("ddd", culture)}";
        }
    }
}

/// <summary>A regional map: an overlay image drawn on top of a basemap, centered on the location.</summary>
public sealed record ThreatMapPanel(string Title, Uri BaseMapUri, Uri OverlayUri);

public sealed record WeekAheadThreatSnapshot(
    IReadOnlyList<WeekAheadThreat> Threats,
    string? HazardOutlookText,
    string? HazardOutlookOffice,
    DateTimeOffset? HazardOutlookIssued,
    IReadOnlyList<ThreatMapPanel> Maps)
{
    public WeekAheadThreat? Top => Threats.Count == 0 ? null : Threats[0];

    public string BadgeText => Top is { } t ? $"{t.Title} ({t.ShortDays})" : "";
}
