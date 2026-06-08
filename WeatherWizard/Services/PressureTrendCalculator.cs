using WeatherWizard.Models;

namespace WeatherWizard.Services;

public static class PressureTrendCalculator
{
    private const double RisingThresholdInHg = 0.02;
    private const double FallingThresholdInHg = 0.02;

    public static double? HpaToInHg(double? hpa)
    {
        if (hpa is null || double.IsNaN(hpa.Value) || double.IsInfinity(hpa.Value))
            return null;
        return hpa.Value / 33.8639;
    }

    /// <summary>Pressure inHg from hourly series closest to <paramref name="targetUtc"/>.</summary>
    public static double? SampleInHgAt(
        IReadOnlyList<DateTimeOffset> times,
        IReadOnlyList<double> pressureHpa,
        DateTimeOffset targetUtc)
    {
        if (times.Count == 0 || pressureHpa.Count == 0)
            return null;

        var bestIdx = -1;
        var bestAbs = TimeSpan.MaxValue;
        for (var i = 0; i < times.Count && i < pressureHpa.Count; i++)
        {
            var d = (times[i] - targetUtc).Duration();
            if (d < bestAbs)
            {
                bestAbs = d;
                bestIdx = i;
            }
        }

        if (bestIdx < 0 || double.IsNaN(pressureHpa[bestIdx]))
            return null;

        return HpaToInHg(pressureHpa[bestIdx]);
    }

    public static (string Arrow, PressureTrendKind Trend) Analyze(double? currentInHg, double? pastInHg)
    {
        if (currentInHg is null)
            return ("", PressureTrendKind.Unknown);

        if (pastInHg is null)
            return ("", PressureTrendKind.Unknown);

        var rise = currentInHg.Value - pastInHg.Value;
        var drop = pastInHg.Value - currentInHg.Value;

        if (rise > RisingThresholdInHg)
            return ("↑", PressureTrendKind.Rising);

        if (drop >= 0.30)
            return ("↓", PressureTrendKind.FallingSevere);

        if (drop > 0.20)
            return ("↓", PressureTrendKind.FallingModerate);

        if (drop >= 0.10)
            return ("↓", PressureTrendKind.FallingMild);

        if (drop > FallingThresholdInHg)
            return ("↓", PressureTrendKind.Falling);

        return ("→", PressureTrendKind.Steady);
    }
}
