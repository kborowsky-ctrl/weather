using System.Reflection;

namespace WeatherWizard.Services;

public static class AppVersion
{
    public static string Display
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+', StringComparison.Ordinal);
                var version = plus >= 0 ? info[..plus] : info;
                return $"v{version}";
            }

            var v = asm.GetName().Version;
            return v is null ? "v0.0.0" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
