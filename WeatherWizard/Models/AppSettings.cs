using System.Text.Json.Serialization;

namespace WeatherWizard.Models;

public sealed class AppSettings
{
    public List<SavedLocation> Locations { get; set; } = [];

    /// <summary>Refresh interval in minutes. Default 15.</summary>
    public int RefreshIntervalMinutes { get; set; } = 15;

    /// <summary>"Light" or "Dark". Controls app chrome and high-contrast page backgrounds.</summary>
    public string Theme { get; set; } = "Light";

    /// <summary>
    /// Where to place the window on launch: RememberLast, Center, TopLeft, TopRight, BottomLeft,
    /// BottomRight, Left (vertical center), Right (vertical center).
    /// </summary>
    public string WindowPlacement { get; set; } = "RememberLast";

    /// <summary>Last window left edge in screen pixels (Remember last).</summary>
    public int? WindowPositionXPixels { get; set; }

    /// <summary>Last window top edge in screen pixels (Remember last).</summary>
    public int? WindowPositionYPixels { get; set; }

    public int? WindowWidthPixels { get; set; }

    public int? WindowHeightPixels { get; set; }

    /// <summary>Seconds between NWS radar / custom image flips (3–120).</summary>
    public int RadarFlipIntervalSeconds { get; set; } = 8;

    /// <summary>Legacy global custom radar URLs; migrated to each <see cref="SavedLocation"/> on load.</summary>
    public List<string> CustomRadarImageUrls { get; set; } = [];

    [JsonIgnore]
    public TimeSpan RefreshInterval =>
        TimeSpan.FromMinutes(Math.Clamp(RefreshIntervalMinutes, 5, 120));

    [JsonIgnore]
    public TimeSpan RadarFlipInterval =>
        TimeSpan.FromSeconds(Math.Clamp(RadarFlipIntervalSeconds, 3, 120));
}
