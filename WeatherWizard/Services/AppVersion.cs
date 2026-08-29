using System.Reflection;
using System.Text.RegularExpressions;

namespace WeatherWizard.Services;

public static partial class AppVersion
{
    public static string Display => Format(Semantic);

    public static Version Semantic
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info) && TryParse(info, out var fromInfo))
                return Normalize(fromInfo);

            var v = asm.GetName().Version;
            return v is null ? new Version(0, 0, 0) : Normalize(v);
        }
    }

    public static bool TryParse(string? raw, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..];

        var cut = VersionCutRegex().Match(s);
        if (cut.Success)
            s = cut.Groups[1].Value;

        return Version.TryParse(s, out version!);
    }

    public static Version Normalize(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public static string Format(Version v)
    {
        var n = Normalize(v);
        return $"v{n.Major}.{n.Minor}.{n.Build}";
    }

    public static int Compare(Version a, Version b) =>
        Normalize(a).CompareTo(Normalize(b));

    [GeneratedRegex(@"^(\d+(?:\.\d+){1,3})", RegexOptions.CultureInvariant)]
    private static partial Regex VersionCutRegex();
}
