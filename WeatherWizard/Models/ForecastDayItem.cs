using WeatherWizard.Services;

namespace WeatherWizard.Models;

public sealed class ForecastDayItem
{
    public DateOnly Date { get; init; }

    public string DayLabel { get; init; } = "";

    /// <summary>First line in the Period column (e.g. Tonight, Today, Thu).</summary>
    public string PeriodTitle { get; init; } = "";

    /// <summary>Second line under the period (e.g. May 15th).</summary>
    public string DateSubtitle { get; init; } = "";

    /// <summary>Plain-language conditions for the grid.</summary>
    public string ConditionsDisplay { get; init; } = "";

    /// <summary>Emoji for the weather-code column (same family as current conditions).</summary>
    public string ConditionEmoji { get; init; } = "";

    /// <summary>Hi/Lo column text (e.g. H 72° / L 54°).</summary>
    public string HiLoDisplay { get; init; } = "";

    /// <summary>Precip column (e.g. 40% or —).</summary>
    public string PrecipPercentDisplay { get; init; } = "";

    public int WeatherCode { get; init; }

    public string Summary { get; init; } = "";

    public double HighF { get; init; }

    public double LowF { get; init; }

    public int? PrecipChance { get; init; }

    public string PrecipDisplay =>
        PrecipChance is int p ? $"{p}% precip" : "";

    public string HighLowLine =>
        HiLoDisplay.Length > 0 ? HiLoDisplay : $"H {Math.Round(HighF)}° / L {Math.Round(LowF)}°";

    public string? SunriseText { get; init; }

    public string? SunsetText { get; init; }

    public string SunLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(SunriseText))
                parts.Add($"↑ {SunriseText}");
            if (!string.IsNullOrWhiteSpace(SunsetText))
                parts.Add($"↓ {SunsetText}");
            return parts.Count == 0 ? string.Empty : string.Join("  ", parts);
        }
    }

    /// <summary>Legacy compact line (e.g. logging / diagnostics).</summary>
    public string CompactLine =>
        $"{PeriodTitle}  {ConditionEmoji}  {HiLoDisplay}  {WeatherCodeInterpreter.Short(WeatherCode)}  {PrecipPercentDisplay}";
}
