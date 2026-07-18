namespace WeatherWizard.Services;

/// <summary>Shared HTTP identity (NWS requires a descriptive User-Agent).</summary>
public static class AppConstants
{
    public const string HttpUserAgent =
        "WeatherWizard/1.0 (+https://github.com/kborowsky-ctrl/weather; WinUI desktop)";
}
