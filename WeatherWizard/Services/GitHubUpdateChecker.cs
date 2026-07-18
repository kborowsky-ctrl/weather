using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WeatherWizard.Services;

public sealed record AppUpdateInfo(
    Version Current,
    Version Latest,
    string TagName,
    string ReleasePageUrl,
    string? SetupDownloadUrl,
    string? SetupFileName)
{
    public bool IsNewer => Latest > Current;
}

/// <summary>Checks GitHub Releases for a newer WeatherWizard Setup EXE.</summary>
public sealed class GitHubUpdateChecker(HttpClientFactory http)
{
    public const string Owner = "kborowsky-ctrl";
    public const string Repo = "weather";
    public const string PreferredSetupAsset = "WeatherWizard-Setup-win-x64.exe";

    private static readonly Uri LatestReleaseApi =
        new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    public async Task<AppUpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        using var resp = await http.Client.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagEl) && tagEl.ValueKind == JsonValueKind.String
            ? tagEl.GetString() ?? ""
            : "";
        if (!AppVersion.TryParse(tag, out var latest))
            return null;

        var htmlUrl = root.TryGetProperty("html_url", out var htmlEl) && htmlEl.ValueKind == JsonValueKind.String
            ? htmlEl.GetString() ?? $"https://github.com/{Owner}/{Repo}/releases"
            : $"https://github.com/{Owner}/{Repo}/releases";

        string? setupUrl = null;
        string? setupName = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            (setupUrl, setupName) = PickSetupAsset(assets);

        return new AppUpdateInfo(
            Current: AppVersion.Semantic,
            Latest: latest,
            TagName: tag,
            ReleasePageUrl: htmlUrl,
            SetupDownloadUrl: setupUrl,
            SetupFileName: setupName);
    }

    /// <summary>Downloads the Setup EXE to a temp folder and launches it.</summary>
    public async Task<string> DownloadAndLaunchSetupAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.SetupDownloadUrl))
            throw new InvalidOperationException("This release has no Setup EXE asset to download.");

        var fileName = string.IsNullOrWhiteSpace(update.SetupFileName)
            ? PreferredSetupAsset
            : Path.GetFileName(update.SetupFileName);

        var dir = Path.Combine(Path.GetTempPath(), "WeatherWizard", "updates");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);

        using var req = new HttpRequestMessage(HttpMethod.Get, update.SetupDownloadUrl);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

        using var resp = await http.Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength;
        await using (var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                readTotal += read;
                if (total is > 0)
                    progress?.Report(readTotal / (double)total.Value);
            }
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });

        return path;
    }

    public static void OpenReleasePage(AppUpdateInfo update)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = update.ReleasePageUrl,
            UseShellExecute = true,
        });
    }

    private static (string? Url, string? Name) PickSetupAsset(JsonElement assets)
    {
        string? preferredUrl = null;
        string? preferredName = null;
        string? anySetupUrl = null;
        string? anySetupName = null;
        string? anyExeUrl = null;
        string? anyExeName = null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                ? n.GetString() ?? ""
                : "";
            var url = asset.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String
                ? u.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                continue;

            if (name.Equals(PreferredSetupAsset, StringComparison.OrdinalIgnoreCase))
            {
                preferredUrl = url;
                preferredName = name;
                break;
            }

            if (name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                anySetupUrl ??= url;
                anySetupName ??= name;
            }
            else if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                anyExeUrl ??= url;
                anyExeName ??= name;
            }
        }

        if (preferredUrl is not null)
            return (preferredUrl, preferredName);
        if (anySetupUrl is not null)
            return (anySetupUrl, anySetupName);
        return (anyExeUrl, anyExeName);
    }
}
