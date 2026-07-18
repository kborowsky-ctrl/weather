using Microsoft.Win32;

namespace WeatherWizard.Services;

/// <summary>Registers or clears HKCU Run so WeatherWizard can start with Windows.</summary>
public static class StartupLaunchHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WeatherWizard";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(bool enabled)
    {
        if (enabled)
            Enable();
        else
            Disable();
    }

    public static void Enable()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            throw new InvalidOperationException("Could not resolve WeatherWizard.exe path for startup.");

        var command = $"\"{exe}\"";
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");
        key.SetValue(ValueName, command);
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort remove.
        }
    }

    /// <summary>
    /// Aligns the Run key with settings. If the installer enabled startup but settings still say off,
    /// adopts the registry entry (returns true). Refreshes the EXE path when startup is enabled.
    /// </summary>
    public static bool SyncFromSettings(bool startWithWindows)
    {
        try
        {
            if (startWithWindows)
            {
                Enable();
                return true;
            }

            if (IsEnabled())
            {
                Enable();
                return true;
            }

            return false;
        }
        catch
        {
            return startWithWindows;
        }
    }
}
