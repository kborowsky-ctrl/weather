using System.Globalization;
using System.Text.RegularExpressions;

namespace WeatherWizard.Services;

/// <summary>
/// Builds a browser URL for a specific alert. The NWS GeoJSON <c>properties.web</c> field is often
/// only <c>http://www.weather.gov</c>, so we prefer VTEC-based Iowa Environmental Mesonet links.
/// </summary>
public static partial class NwsAlertPublicLink
{
    /// <summary>
    /// VTEC: /O.NEW.KLUB.SV.W.0074.260516T0007Z-260516T0100Z/
    /// </summary>
    [GeneratedRegex(@"^\/?[A-Z]\.[A-Z]+\.(?<wfo>[A-Z0-9]{4})\.(?<phen>[A-Z]{2})\.(?<sig>[SWY])\.(?<etn>\d+)\.", RegexOptions.CultureInvariant)]
    private static partial Regex VtecPrefixRegex();

    public static Uri? Resolve(string? webFromApi, string? vtecLine, DateTimeOffset? sentUtc, double latitude, double longitude)
    {
        if (TryUseWebProperty(webFromApi, out var webUri))
            return webUri;

        if (TryBuildIemVtecUrl(vtecLine, sentUtc, out var iem))
            return iem;

        return BuildMapClickFallback(latitude, longitude);
    }

    private static bool TryUseWebProperty(string? webFromApi, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(webFromApi))
            return false;

        if (!Uri.TryCreate(webFromApi.Trim(), UriKind.Absolute, out var u))
            return false;

        if (IsGenericWeatherGovHome(u))
            return false;

        uri = u;
        return true;
    }

    private static bool IsGenericWeatherGovHome(Uri u)
    {
        if (!u.Host.EndsWith("weather.gov", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = u.AbsolutePath.TrimEnd('/');
        return path.Length == 0 || string.Equals(path, "/", StringComparison.Ordinal);
    }

    private static bool TryBuildIemVtecUrl(string? vtecLine, DateTimeOffset? sentUtc, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(vtecLine))
            return false;

        var m = VtecPrefixRegex().Match(vtecLine.Trim());
        if (!m.Success)
            return false;

        var wfo = m.Groups["wfo"].Value;
        var phen = m.Groups["phen"].Value;
        var sig = m.Groups["sig"].Value;
        if (!int.TryParse(m.Groups["etn"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var etn))
            return false;

        var year = sentUtc?.Year ?? DateTime.UtcNow.Year;
        var etn4 = etn.ToString("D4", CultureInfo.InvariantCulture);
        var url =
            $"https://mesonet.agron.iastate.edu/vtec/?year={year}&wfo={Uri.EscapeDataString(wfo)}&phenomena={Uri.EscapeDataString(phen)}&significance={Uri.EscapeDataString(sig)}&eventid={Uri.EscapeDataString(etn4)}&tab=info";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var built))
            return false;

        uri = built;
        return true;
    }

    private static Uri? BuildMapClickFallback(double latitude, double longitude)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var url = $"https://forecast.weather.gov/MapClick.php?FcstType=graphical&unit=0&lg=english&lat={lat}&lon={lon}";
        return Uri.TryCreate(url, UriKind.Absolute, out var u) ? u : null;
    }
}
