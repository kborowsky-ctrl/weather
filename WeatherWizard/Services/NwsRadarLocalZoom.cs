using Windows.Foundation;

namespace WeatherWizard.Services;

/// <summary>
/// Maps a saved lat/lon onto NWS RIDGE standard GIFs and sizes/positions the image
/// so local zoom is centered on that point (not the radar site).
/// </summary>
public static class NwsRadarLocalZoom
{
    /// <summary>Typical short-range RIDGE standard display radius (~124 nmi).</summary>
    private const double RangeKm = 230.0;

    // RIDGE II standard stills are usually 600×550: title strip, map, bottom color bar,
    // and a right-side product legend.
    private const double MapLeftFrac = 0.02;
    private const double MapRightFrac = 0.90;
    private const double MapTopFrac = 0.08;
    private const double MapBottomFrac = 0.94;

    /// <summary>Radar-disk center in source pixels (accounts for chrome / legend).</summary>
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

        // Keep focus inside the map disk so extreme offsets don't leave an empty frame.
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
    /// Layout for local zoom: scale the GIF so <paramref name="srcFocusX"/>/<paramref name="srcFocusY"/>
    /// land at the host center. Returns image width/height and top-left margin.
    /// </summary>
    public static (double Width, double Height, double MarginLeft, double MarginTop) LayoutZoomedImage(
        double hostWidth,
        double hostHeight,
        int pixelWidth,
        int pixelHeight,
        double srcFocusX,
        double srcFocusY,
        double zoomScale)
    {
        if (hostWidth <= 0 || hostHeight <= 0 || pixelWidth <= 0 || pixelHeight <= 0 || zoomScale <= 0)
            return (hostWidth, hostHeight, 0, 0);

        // Fit the full GIF in the host, then magnify.
        var fit = Math.Min(hostWidth / pixelWidth, hostHeight / pixelHeight);
        var displayScale = fit * zoomScale;
        var width = pixelWidth * displayScale;
        var height = pixelHeight * displayScale;
        var left = hostWidth * 0.5 - srcFocusX * displayScale;
        var top = hostHeight * 0.5 - srcFocusY * displayScale;
        return (width, height, left, top);
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
