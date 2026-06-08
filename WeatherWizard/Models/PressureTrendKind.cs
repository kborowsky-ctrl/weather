namespace WeatherWizard.Models;

/// <summary>Barometric trend vs ~3 hours ago (inHg).</summary>
public enum PressureTrendKind
{
    Unknown,
    Steady,
    Rising,
    Falling,
    /// <summary>Drop 0.10–0.20 inHg in 3 h — slow flashing ↓.</summary>
    FallingMild,
    /// <summary>Drop &gt; 0.20 and &lt; 0.30 inHg in 3 h — faster flashing ↓.</summary>
    FallingModerate,
    /// <summary>Drop ≥ 0.30 inHg in 3 h — red flashing ↓.</summary>
    FallingSevere,
}
