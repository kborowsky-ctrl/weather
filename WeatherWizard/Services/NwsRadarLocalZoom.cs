using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace WeatherWizard.Services;

/// <summary>
/// Computes zoom focus for NWS RIDGE standard GIFs so local zoom centers on the saved
/// location rather than the radar site / image midpoint (legend pushes the disk left).
/// </summary>
public static class NwsRadarLocalZoom
{
    /// <summary>Typical short-range RIDGE standard display radius (~124 nmi).</summary>
    private const double RangeKm = 230.0;

    // RIDGE II standard stills are usually 600×550 with title/legend chrome.
    private const double MapLeftFrac = 0.01;
    private const double MapRightFrac = 0.84;
    private const double MapTopFrac = 0.07;
    private const double MapBottomFrac = 0.96;

    public static Point FocusInControlCoordinates(
        double controlWidth,
        double controlHeight,
        int pixelWidth,
        int pixelHeight,
        double locationLat,
        double locationLon,
        double radarLat,
        double radarLon)
    {
        var (srcX, srcY) = FocusInSourcePixels(
            pixelWidth, pixelHeight, locationLat, locationLon, radarLat, radarLon);
        return MapSourcePixelToControl(controlWidth, controlHeight, pixelWidth, pixelHeight, srcX, srcY);
    }

    /// <summary>Radar-disk center in source pixels (accounts for right-side legend).</summary>
    public static (double X, double Y) RadarDiskCenterPixels(int pixelWidth, int pixelHeight)
    {
        var left = pixelWidth * MapLeftFrac;
        var right = pixelWidth * MapRightFrac;
        var top = pixelHeight * MapTopFrac;
        var bottom = pixelHeight * MapBottomFrac;
        return ((left + right) * 0.5, (top + bottom) * 0.5);
    }

    public static (double X, double Y) FocusInSourcePixels(
        int pixelWidth,
        int pixelHeight,
        double locationLat,
        double locationLon,
        double radarLat,
        double radarLon)
    {
        var (cx, cy) = RadarDiskCenterPixels(pixelWidth, pixelHeight);
        var left = pixelWidth * MapLeftFrac;
        var right = pixelWidth * MapRightFrac;
        var top = pixelHeight * MapTopFrac;
        var bottom = pixelHeight * MapBottomFrac;
        var radiusPx = Math.Min(right - left, bottom - top) * 0.5;

        var meanLat = (locationLat + radarLat) * 0.5 * Math.PI / 180.0;
        var eastKm = (locationLon - radarLon) * 111.32 * Math.Cos(meanLat);
        var northKm = (locationLat - radarLat) * 110.574;

        var dx = eastKm / RangeKm * radiusPx;
        var dy = -northKm / RangeKm * radiusPx; // north is up in the image

        // Keep focus inside the map disk so extreme offsets don't leave the frame empty,
        // but allow the zip/location to sit away from the radar site.
        var max = radiusPx * 0.95;
        var dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > max && dist > 0)
        {
            var s = max / dist;
            dx *= s;
            dy *= s;
        }

        return (cx + dx, cy + dy);
    }

    /// <summary>
    /// Matrix that places <paramref name="focus"/> at the viewport center after uniform scale.
    /// </summary>
    public static Matrix ZoomMatrix(Point focus, double viewCenterX, double viewCenterY, double scale)
    {
        // p' = scale * p + (center - focus * scale)
        return new Matrix(
            scale,
            0,
            0,
            scale,
            viewCenterX - focus.X * scale,
            viewCenterY - focus.Y * scale);
    }

    public static Point MapSourcePixelToControl(
        double controlWidth,
        double controlHeight,
        int pixelWidth,
        int pixelHeight,
        double srcX,
        double srcY)
    {
        if (controlWidth <= 0 || controlHeight <= 0 || pixelWidth <= 0 || pixelHeight <= 0)
            return new Point(controlWidth * 0.5, controlHeight * 0.5);

        var scale = Math.Min(controlWidth / pixelWidth, controlHeight / pixelHeight);
        var dispW = pixelWidth * scale;
        var dispH = pixelHeight * scale;
        var ox = (controlWidth - dispW) * 0.5;
        var oy = (controlHeight - dispH) * 0.5;
        return new Point(ox + srcX * scale, oy + srcY * scale);
    }
}
