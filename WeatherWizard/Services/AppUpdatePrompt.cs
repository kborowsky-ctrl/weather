using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WeatherWizard.Services;

/// <summary>UI prompts for GitHub update checks.</summary>
public static class AppUpdatePrompt
{
    public static async Task ShowIfNewerAsync(XamlRoot? xamlRoot, AppUpdateInfo? update, bool quietWhenCurrent)
    {
        if (xamlRoot is null)
            return;

        if (update is null)
        {
            if (!quietWhenCurrent)
            {
                await ShowMessageAsync(
                    xamlRoot,
                    "No releases found",
                    "GitHub has no published release yet. Create a Release and attach WeatherWizard-Setup-win-x64.exe.").ConfigureAwait(true);
            }

            return;
        }

        if (!update.IsNewer)
        {
            if (!quietWhenCurrent)
            {
                await ShowMessageAsync(
                    xamlRoot,
                    "Up to date",
                    $"You are running {Format(update.Current)}. Latest on GitHub is {Format(update.Latest)}.").ConfigureAwait(true);
            }

            return;
        }

        var hasSetup = !string.IsNullOrWhiteSpace(update.SetupDownloadUrl);
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Update available",
            Content =
                $"WeatherWizard {Format(update.Latest)} is available (you have {Format(update.Current)})."
                + (hasSetup
                    ? "\n\nDownload and run the installer? Close WeatherWizard when Setup asks."
                    : "\n\nOpen the GitHub release page to download?"),
            PrimaryButtonText = hasSetup ? "Download & install" : "Open release page",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        if (!hasSetup)
        {
            GitHubUpdateChecker.OpenReleasePage(update);
            return;
        }

        try
        {
            var progressDialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Downloading update…",
                Content = "Please wait.",
                CloseButtonText = "Hide",
            };
            var progressTask = progressDialog.ShowAsync().AsTask();

            try
            {
                await App.Current.Updates.DownloadAndLaunchSetupAsync(update).ConfigureAwait(true);
            }
            finally
            {
                progressDialog.Hide();
                try { await progressTask.ConfigureAwait(true); } catch { /* dismissed */ }
            }

            var exit = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Installer started",
                Content = "Setup is running. Close WeatherWizard now so files can be updated?",
                PrimaryButtonText = "Close app",
                CloseButtonText = "Keep running",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await exit.ShowAsync() == ContentDialogResult.Primary)
                Application.Current.Exit();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(xamlRoot, "Download failed", ex.Message).ConfigureAwait(true);
            GitHubUpdateChecker.OpenReleasePage(update);
        }
    }

    private static async Task ShowMessageAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }

    private static string Format(Version v) => $"v{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
}
