namespace WeatherWizard.Services;

public static class SolarTimeHelper
{
    /// <summary>True when local time is after today's sunset or before today's sunrise.</summary>
    public static bool IsNight(DateTimeOffset now, DateTimeOffset? sunrise, DateTimeOffset? sunset)
    {
        if (sunrise is null || sunset is null)
            return false;

        if (now >= sunset.Value)
            return true;

        return now < sunrise.Value;
    }
}
