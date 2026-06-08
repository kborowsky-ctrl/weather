using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WeatherWizard.Services;

/// <summary>
/// Vector tray glyphs sized to fill the notification icon (GDI+ emoji does not render reliably in HICON).
/// </summary>
public static class TrayWeatherIconFactory
{
    private const int S = 32;

    public static Bitmap CreateBitmap(int weatherCode)
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        DrawForCode(g, weatherCode);
        return bmp;
    }

    private static void DrawForCode(Graphics g, int code)
    {
        if (code < 0)
        {
            DrawPartlyCloudy(g);
            return;
        }

        switch (code)
        {
            case 0: DrawClear(g); break;
            case 1 or 2: DrawPartlyCloudy(g); break;
            case 3: DrawOvercast(g); break;
            case 45 or 48: DrawFog(g); break;
            case 51 or 53 or 55: DrawDrizzle(g); break;
            case 56 or 57: DrawFreezingMix(g); break;
            case 61 or 63 or 65: DrawRain(g); break;
            case 66 or 67: DrawFreezingRain(g); break;
            case 71 or 73 or 75 or 77: DrawSnow(g); break;
            case 80 or 81 or 82: DrawShowers(g); break;
            case 85 or 86: DrawSnowShowers(g); break;
            case 95 or 96 or 99: DrawThunder(g); break;
            default: DrawPartlyCloudy(g); break;
        }
    }

    private static void DrawClear(Graphics g)
    {
        using var sun = new SolidBrush(Color.FromArgb(255, 255, 210, 0));
        g.FillEllipse(sun, 3, 3, 26, 26);
        using var rays = new Pen(Color.FromArgb(240, 255, 210, 0), 2.4f) { EndCap = LineCap.Round };
        for (var i = 0; i < 8; i++)
        {
            var a = i * (MathF.PI / 4f);
            var c = MathF.Cos(a);
            var s = MathF.Sin(a);
            g.DrawLine(rays, 16 + c * 7f, 16 + s * 7f, 16 + c * 15f, 16 + s * 15f);
        }
    }

    private static void DrawPartlyCloudy(Graphics g)
    {
        using var sun = new SolidBrush(Color.FromArgb(255, 255, 210, 0));
        g.FillEllipse(sun, 0, 0, 17, 17);
        DrawCloud(g, CloudLight, 1, 9, 30, 22);
    }

    private static void DrawOvercast(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 7, 32, 24);
        DrawCloud(g, CloudDark, 0, 11, 30, 20);
    }

    private static void DrawFog(Graphics g)
    {
        DrawCloud(g, CloudLight, 0, 4, 32, 18);
        using var mist = new SolidBrush(Color.FromArgb(200, 230, 235, 245));
        for (var y = 18; y <= 28; y += 3)
            g.FillRectangle(mist, 2, y, 28, 2);
    }

    private static void DrawDrizzle(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 2, 32, 20);
        DrawRainStreaks(g, Color.FromArgb(255, 100, 170, 255), 4);
    }

    private static void DrawFreezingMix(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 2, 32, 20);
        using var p = new Pen(Color.FromArgb(255, 180, 230, 255), 2f) { EndCap = LineCap.Round };
        for (var i = 0; i < 5; i++)
            g.DrawLine(p, 6 + i * 4, 21, 8 + i * 4, 29);
    }

    private static void DrawRain(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 1, 32, 19);
        DrawRainStreaks(g, Color.FromArgb(255, 80, 160, 255), 5);
    }

    private static void DrawFreezingRain(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 1, 32, 19);
        using var p = new Pen(Color.FromArgb(255, 200, 240, 255), 2f) { EndCap = LineCap.Round };
        for (var i = 0; i < 5; i++)
            g.DrawLine(p, 6 + i * 4, 21, 7 + i * 4, 29);
    }

    private static void DrawSnow(Graphics g)
    {
        DrawCloud(g, CloudLight, 0, 2, 32, 20);
        using var flake = new SolidBrush(Color.White);
        for (var i = 0; i < 7; i++)
            g.FillEllipse(flake, 4 + (i % 4) * 6, 19 + (i / 4) * 4, 4, 4);
    }

    private static void DrawShowers(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 1, 32, 19);
        DrawRainStreaks(g, Color.FromArgb(255, 60, 140, 255), 6);
    }

    private static void DrawSnowShowers(Graphics g)
    {
        DrawCloud(g, CloudMid, 0, 1, 32, 19);
        using var flake = new SolidBrush(Color.FromArgb(255, 245, 250, 255));
        for (var i = 0; i < 8; i++)
            g.FillEllipse(flake, 3 + (i % 4) * 6, 18 + (i / 4) * 4, 4, 4);
    }

    private static void DrawThunder(Graphics g)
    {
        DrawCloud(g, CloudDark, 0, 0, 32, 20);
        using var bolt = new SolidBrush(Color.FromArgb(255, 255, 230, 40));
        g.FillPolygon(bolt, new PointF[]
        {
            new(17, 12), new(11, 23), new(16, 23), new(12, 30), new(23, 17), new(18, 17),
        });
    }

    private static readonly Color CloudLight = Color.FromArgb(255, 225, 230, 240);
    private static readonly Color CloudMid = Color.FromArgb(255, 195, 200, 215);
    private static readonly Color CloudDark = Color.FromArgb(255, 165, 172, 190);

    private static void DrawCloud(Graphics g, Color fill, float x, float y, float w, float h)
    {
        using var b = new SolidBrush(fill);
        g.FillEllipse(b, x, y + h * 0.30f, w * 0.62f, h * 0.62f);
        g.FillEllipse(b, x + w * 0.12f, y + h * 0.05f, w * 0.50f, h * 0.62f);
        g.FillEllipse(b, x + w * 0.34f, y, w * 0.50f, h * 0.62f);

        using var edge = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        g.DrawEllipse(edge, x + w * 0.34f, y, w * 0.50f, h * 0.62f);
    }

    private static void DrawRainStreaks(Graphics g, Color c, int count)
    {
        using var p = new Pen(c, 2f) { EndCap = LineCap.Round };
        var span = 24f / Math.Max(count, 1);
        for (var i = 0; i < count; i++)
        {
            var x = 5 + i * span;
            g.DrawLine(p, x, 20, x + 2f, 30);
        }
    }
}
