using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WeatherWizard.Services;

/// <summary>Shared moon-phase disk drawing for tray and in-app condition icons.</summary>
public static class MoonPhaseIconRenderer
{
    public static readonly MoonPhasePalette TrayPalette = new(
        Lit: Color.FromArgb(255, 235, 240, 252),
        Shadow: Color.FromArgb(255, 45, 50, 62));

    public static readonly MoonPhasePalette UiPalette = new(
        Lit: Color.FromArgb(255, 196, 202, 214),
        Shadow: Color.FromArgb(255, 58, 64, 76));

    public static void DrawMoonPhase(
        Graphics g,
        float x,
        float y,
        float size,
        double phase,
        MoonPhasePalette palette,
        bool litPortionOnly = false)
    {
        if (litPortionOnly)
        {
            DrawLitPortionOnly(g, x, y, size, phase, palette.Lit);
            return;
        }

        using (var body = new SolidBrush(palette.Lit))
            g.FillEllipse(body, x, y, size, size);

        if (phase < 0.03 || phase > 0.97)
        {
            using var dark = new SolidBrush(palette.Shadow);
            g.FillEllipse(dark, x, y, size, size);
            return;
        }

        if (phase > 0.47 && phase < 0.53)
            return;

        var offset = (float)(Math.Cos(phase * 2 * Math.PI) * size * 0.85);
        using (var dark = new SolidBrush(palette.Shadow))
        {
            if (phase < 0.5)
                g.FillEllipse(dark, x - offset, y, size, size);
            else
                g.FillEllipse(dark, x + offset, y, size, size);
        }
    }

    private static void DrawLitPortionOnly(Graphics g, float x, float y, float size, double phase, Color lit)
    {
        if (phase < 0.03 || phase > 0.97)
            return;

        using var litBrush = new SolidBrush(lit);

        if (phase > 0.47 && phase < 0.53)
        {
            g.FillEllipse(litBrush, x, y, size, size);
            return;
        }

        using var moonPath = new GraphicsPath();
        moonPath.AddEllipse(x, y, size, size);

        var offset = (float)(Math.Cos(phase * 2 * Math.PI) * size * 0.85);
        using var shadowPath = new GraphicsPath();
        if (phase < 0.5)
            shadowPath.AddEllipse(x - offset, y, size, size);
        else
            shadowPath.AddEllipse(x + offset, y, size, size);

        using var litRegion = new Region(moonPath);
        litRegion.Exclude(shadowPath);
        g.FillRegion(litBrush, litRegion);
    }

    public static Bitmap CreateBitmap(
        int pixelSize,
        DateTimeOffset at,
        MoonPhasePalette palette,
        bool litPortionOnly = false,
        int weatherCode = 0)
    {
        var bmp = new Bitmap(pixelSize, pixelSize, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var phase = MoonPhaseCalculator.Phase01(at);
        var scale = pixelSize / 32f;

        if (weatherCode is 1 or 2)
        {
            var moonSize = 17f * scale;
            DrawMoonPhase(g, 0, 0, moonSize, phase, palette, litPortionOnly);
            DrawCloud(g, UiCloudLight, 1f * scale, 9f * scale, 30f * scale, 22f * scale);
        }
        else
        {
            var moonSize = 26f * scale;
            var offset = 3f * scale;
            DrawMoonPhase(g, offset, offset, moonSize, phase, palette, litPortionOnly);
        }

        return bmp;
    }

    private static readonly Color UiCloudLight = Color.FromArgb(255, 225, 230, 240);

    private static void DrawCloud(Graphics g, Color fill, float x, float y, float w, float h)
    {
        using var b = new SolidBrush(fill);
        g.FillEllipse(b, x, y + h * 0.30f, w * 0.62f, h * 0.62f);
        g.FillEllipse(b, x + w * 0.12f, y + h * 0.05f, w * 0.50f, h * 0.62f);
        g.FillEllipse(b, x + w * 0.34f, y, w * 0.50f, h * 0.62f);

        using var edge = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        g.DrawEllipse(edge, x + w * 0.34f, y, w * 0.50f, h * 0.62f);
    }
}

public readonly record struct MoonPhasePalette(Color Lit, Color Shadow);
