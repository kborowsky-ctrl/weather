namespace WeatherWizard.Services;

public static class CustomRadarUrlHelper
{
    public const int MaxUrls = 5;

    public static List<string> Normalize(IEnumerable<string>? urls)
    {
        if (urls is null)
            return [];

        var list = new List<string>();
        foreach (var raw in urls)
        {
            if (list.Count >= MaxUrls)
                break;

            var s = raw?.Trim();
            if (string.IsNullOrWhiteSpace(s))
                continue;

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
                continue;

            if (uri.Scheme is not "http" and not "https")
                continue;

            list.Add(uri.AbsoluteUri);
        }

        return list;
    }
}
