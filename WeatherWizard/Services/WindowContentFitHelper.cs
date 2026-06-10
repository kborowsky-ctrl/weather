using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace WeatherWizard.Services;

/// <summary>Resize the main window height to fit weather page content (no empty area below the radar).</summary>
public static class WindowContentFitHelper
{
    private const int TitleBarFallbackPx = 36;
    private const int MinH = 400;
    private const int MaxH = 3000;
    private const int HeightSlackPx = 2;

    public static void FitMainWindowHeight(Window window, FrameworkElement mainPage, FrameworkElement? titleChrome)
    {
        if (window.AppWindow.Presenter is not OverlappedPresenter)
            return;

        mainPage.UpdateLayout();

        var width = window.AppWindow.Size.Width;
        if (width < WindowPlacementHelper.MinWidth)
            width = WindowPlacementHelper.DefaultWidth;

        mainPage.Measure(new Size(width, double.PositiveInfinity));
        var pageHeight = mainPage.DesiredSize.Height;
        if (pageHeight <= 0 || double.IsNaN(pageHeight))
            return;

        var titleHeight = titleChrome?.ActualHeight is double th && th > 0 ? th : TitleBarFallbackPx;
        var target = (int)Math.Ceiling(titleHeight + pageHeight + HeightSlackPx);
        target = Math.Clamp(target, MinH, MaxH);

        var current = window.AppWindow.Size;
        if (Math.Abs(current.Height - target) <= 2)
            return;

        window.AppWindow.Resize(new SizeInt32(current.Width, target));
    }
}
