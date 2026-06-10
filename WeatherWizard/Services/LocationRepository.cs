using System.Text.Json;
using System.Threading;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

/// <summary>
/// Persists settings to disk. Uses System.IO (not WinRT ApplicationData) so unpackaged
/// WinUI apps (WindowsPackageType None) save reliably; ApplicationData often throws
/// "operation is not valid due to the current state of the object" in that configuration.
/// </summary>
public sealed class LocationRepository
{
    private const string FileName = "settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _io = new(1, 1);
    private AppSettings _settings = new();

    public event EventHandler? Changed;

    public AppSettings Settings => _settings;

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeatherWizard");

    private static string SettingsPath => Path.Combine(SettingsDirectory, FileName);

    /// <summary>Only <c>Dark</c> selects dark mode; anything else (including empty/legacy) is light.</summary>
    private static void NormalizeTheme(AppSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.Theme))
        {
            s.Theme = "Light";
            return;
        }

        if (string.Equals(s.Theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            s.Theme = "Dark";
            return;
        }

        s.Theme = "Light";
    }

    public async Task LoadAsync()
    {
        await _io.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path))
            {
                _settings = new AppSettings();
                return;
            }

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            _settings = loaded ?? new AppSettings();
            NormalizeTheme(_settings);
            NormalizeWindowPlacement(_settings);
            NormalizeRadarSettings(_settings);
        }
        catch
        {
            _settings = new AppSettings();
        }
        finally
        {
            _io.Release();
        }
    }

    public async Task SaveAsync(AppSettings? settings = null, bool raiseChanged = true)
    {
        if (settings is not null)
        {
            NormalizeTheme(settings);
            NormalizeWindowPlacement(settings);
            _settings = settings;
        }

        await _io.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            NormalizeTheme(_settings);
            NormalizeWindowPlacement(_settings);
            NormalizeRadarSettings(_settings);
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            await File.WriteAllTextAsync(SettingsPath, json).ConfigureAwait(false);
        }
        finally
        {
            _io.Release();
        }

        if (raiseChanged)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceInMemory(AppSettings settings)
    {
        NormalizeTheme(settings);
        NormalizeWindowPlacement(settings);
        _settings = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void NormalizeWindowPlacement(AppSettings s)
    {
        s.WindowPlacement = WindowPlacementHelper.NormalizeMode(s.WindowPlacement);
    }

    private static void NormalizeRadarSettings(AppSettings s)
    {
        s.RadarFlipIntervalSeconds = Math.Clamp(s.RadarFlipIntervalSeconds, 3, 120);
        MigrateLegacyCustomRadarUrls(s);

        foreach (var loc in s.Locations)
            loc.CustomRadarImageUrls = CustomRadarUrlHelper.Normalize(loc.CustomRadarImageUrls);
    }

    private static void MigrateLegacyCustomRadarUrls(AppSettings s)
    {
        var legacy = CustomRadarUrlHelper.Normalize(s.CustomRadarImageUrls);
        if (legacy.Count == 0)
            return;

        foreach (var loc in s.Locations)
        {
            if (loc.CustomRadarImageUrls.Count == 0)
                loc.CustomRadarImageUrls = [.. legacy];
        }

        s.CustomRadarImageUrls = [];
    }
}
