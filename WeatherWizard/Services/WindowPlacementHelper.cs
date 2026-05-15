using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WeatherWizard.Models;
using WinRT.Interop;
using Windows.Graphics;

namespace WeatherWizard.Services;

public static class WindowPlacementHelper
{
    private const int DefaultWidth = 460;
    private const int DefaultHeight = 980;
    private const int MinW = 360;
    private const int MinH = 400;
    private const int Margin = 8;

    public static void Apply(Window window, AppSettings settings)
    {
        if (window.AppWindow.Presenter is not OverlappedPresenter)
            return;

        var aw = window.AppWindow;
        var mode = NormalizeMode(settings.WindowPlacement);

        var w = settings.WindowWidthPixels is int sw && sw >= MinW && sw <= 4000 ? sw : DefaultWidth;
        var h = settings.WindowHeightPixels is int sh && sh >= MinH && sh <= 3000 ? sh : DefaultHeight;

        aw.Resize(new SizeInt32(w, h));

        if (!TryGetWorkArea(window, out var work))
        {
            if (mode == WindowPlacementMode.RememberLast
                && settings.WindowPositionXPixels is int sx
                && settings.WindowPositionYPixels is int sy)
            {
                var soft = new RectInt32(0, 0, 3840, 2160);
                aw.Move(ClampToWorkArea(sx, sy, w, h, soft));
            }

            return;
        }

        int x, y;
        if (mode == WindowPlacementMode.RememberLast
            && settings.WindowPositionXPixels is int rx
            && settings.WindowPositionYPixels is int ry)
        {
            x = rx;
            y = ry;
        }
        else
        {
            (x, y) = mode switch
            {
                WindowPlacementMode.TopLeft => (work.X + Margin, work.Y + Margin),
                WindowPlacementMode.TopRight => (work.X + work.Width - w - Margin, work.Y + Margin),
                WindowPlacementMode.BottomLeft => (work.X + Margin, work.Y + work.Height - h - Margin),
                WindowPlacementMode.BottomRight => (work.X + work.Width - w - Margin, work.Y + work.Height - h - Margin),
                WindowPlacementMode.Left => (work.X + Margin, work.Y + Math.Max(Margin, (work.Height - h) / 2)),
                WindowPlacementMode.Right => (work.X + work.Width - w - Margin, work.Y + Math.Max(Margin, (work.Height - h) / 2)),
                _ => (
                    work.X + Math.Max(Margin, (work.Width - w) / 2),
                    work.Y + Math.Max(Margin, (work.Height - h) / 2)),
            };
        }

        aw.Move(ClampToWorkArea(x, y, w, h, work));
    }

    /// <summary>Always saves size; saves position when placement is RememberLast.</summary>
    public static void PersistWindowGeometry(Window window, AppSettings settings)
    {
        var aw = window.AppWindow;
        var p = aw.Position;
        var s = aw.Size;
        if (s.Width >= MinW && s.Height >= MinH)
        {
            settings.WindowWidthPixels = s.Width;
            settings.WindowHeightPixels = s.Height;
        }

        if (!string.Equals(settings.WindowPlacement, "RememberLast", StringComparison.OrdinalIgnoreCase))
            return;

        if (s.Width < MinW || s.Height < MinH)
            return;

        settings.WindowPositionXPixels = p.X;
        settings.WindowPositionYPixels = p.Y;
    }

    public static string NormalizeMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return WindowPlacementMode.RememberLast;

        foreach (var m in WindowPlacementMode.All)
        {
            if (string.Equals(m, raw, StringComparison.OrdinalIgnoreCase))
                return m;
        }

        return WindowPlacementMode.RememberLast;
    }

    private static bool TryGetWorkArea(Window window, out RectInt32 work)
    {
        work = default;
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            work = displayArea.WorkArea;
            return work.Width > 0 && work.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static PointInt32 ClampToWorkArea(int x, int y, int w, int h, RectInt32 work)
    {
        x = ClampX(x, w, work);
        y = ClampY(y, h, work);
        return new PointInt32(x, y);
    }

    private static int ClampX(int x, int w, RectInt32 work)
    {
        var maxX = work.X + work.Width - w - Margin;
        var minX = work.X + Margin;
        if (maxX < minX)
            return work.X;
        return Math.Clamp(x, minX, maxX);
    }

    private static int ClampY(int y, int h, RectInt32 work)
    {
        var maxY = work.Y + work.Height - h - Margin;
        var minY = work.Y + Margin;
        if (maxY < minY)
            return work.Y;
        return Math.Clamp(y, minY, maxY);
    }
}

internal static class WindowPlacementMode
{
    public const string RememberLast = "RememberLast";
    public const string Center = "Center";
    public const string TopLeft = "TopLeft";
    public const string TopRight = "TopRight";
    public const string BottomLeft = "BottomLeft";
    public const string BottomRight = "BottomRight";
    public const string Left = "Left";
    public const string Right = "Right";

    public static readonly string[] All =
    [
        RememberLast, Center, TopLeft, TopRight, BottomLeft, BottomRight, Left, Right,
    ];
}
