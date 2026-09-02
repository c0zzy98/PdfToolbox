using PdfToolbox.Models;

namespace PdfToolbox.Helpers;

public sealed class PageCoordinateMapper(int padding = 18)
{
    private const float MinRegionPixels = 12F;
    private readonly int _padding = Math.Max(0, padding);

    public PageViewport GetViewport(Size clientSize, SizeF pageSizePoints, int rotationDegrees, bool fitToWindow, int zoomPercent = 100)
    {
        var oriented = GetOrientedPageSize(pageSizePoints, rotationDegrees);
        var availableWidth = Math.Max(1F, clientSize.Width - (_padding * 2F));
        var availableHeight = Math.Max(1F, clientSize.Height - (_padding * 2F));

        float width;
        float height;
        if (fitToWindow)
        {
            var scale = Math.Min(availableWidth / oriented.Width, availableHeight / oriented.Height);
            width = Math.Max(1F, oriented.Width * scale);
            height = Math.Max(1F, oriented.Height * scale);
        }
        else
        {
            var zoom = Math.Clamp(zoomPercent, 25, 400) / 100F;
            width = Math.Max(1F, oriented.Width * 96F / 72F * zoom);
            height = Math.Max(1F, oriented.Height * 96F / 72F * zoom);
        }

        var x = fitToWindow ? (clientSize.Width - width) / 2F : _padding;
        var y = fitToWindow ? (clientSize.Height - height) / 2F : _padding;
        return new PageViewport(new RectangleF(x, y, width, height), oriented);
    }

    public Size GetCanvasSize(SizeF pageSizePoints, int rotationDegrees, int zoomPercent)
    {
        var oriented = GetOrientedPageSize(pageSizePoints, rotationDegrees);
        var zoom = Math.Clamp(zoomPercent, 25, 400) / 100F;
        return new Size(
            Math.Max(1, (int)Math.Ceiling(oriented.Width * 96F / 72F * zoom) + (_padding * 2)),
            Math.Max(1, (int)Math.Ceiling(oriented.Height * 96F / 72F * zoom) + (_padding * 2)));
    }

    public static SizeF GetOrientedPageSize(SizeF pageSizePoints, int rotationDegrees) =>
        NormalizeRotation(rotationDegrees) is 90 or 270
            ? new SizeF(pageSizePoints.Height, pageSizePoints.Width)
            : pageSizePoints;

    public bool TryScreenToNormalized(Point location, Size clientSize, SizeF pageSizePoints, int rotationDegrees, bool fitToWindow, out PointF normalized, int zoomPercent = 100)
    {
        var viewport = GetViewport(clientSize, pageSizePoints, rotationDegrees, fitToWindow, zoomPercent);
        if (!viewport.Bounds.Contains(location))
        {
            normalized = default;
            return false;
        }

        normalized = new PointF(
            Math.Clamp((location.X - viewport.Bounds.Left) / viewport.Bounds.Width, 0F, 1F),
            Math.Clamp((location.Y - viewport.Bounds.Top) / viewport.Bounds.Height, 0F, 1F));
        return true;
    }

    public PointF ClampPointToViewport(Point location, Size clientSize, SizeF pageSizePoints, int rotationDegrees, bool fitToWindow, int zoomPercent = 100)
    {
        var bounds = GetViewport(clientSize, pageSizePoints, rotationDegrees, fitToWindow, zoomPercent).Bounds;
        return new PointF(
            Math.Clamp(location.X, bounds.Left, bounds.Right),
            Math.Clamp(location.Y, bounds.Top, bounds.Bottom));
    }

    public RectangleF NormalizedToScreen(PageRegionSelection region, Size clientSize, SizeF pageSizePoints, int rotationDegrees, bool fitToWindow, int zoomPercent = 100)
    {
        var viewport = GetViewport(clientSize, pageSizePoints, rotationDegrees, fitToWindow, zoomPercent).Bounds;
        return RectangleF.FromLTRB(
            viewport.Left + (float)region.X * viewport.Width,
            viewport.Top + (float)region.Y * viewport.Height,
            viewport.Left + (float)(region.X + region.Width) * viewport.Width,
            viewport.Top + (float)(region.Y + region.Height) * viewport.Height);
    }

    public Rectangle NormalizedToBitmap(PageRegionSelection region, Size bitmapSize)
    {
        var left = Math.Clamp((int)Math.Floor(region.X * bitmapSize.Width), 0, Math.Max(0, bitmapSize.Width - 1));
        var top = Math.Clamp((int)Math.Floor(region.Y * bitmapSize.Height), 0, Math.Max(0, bitmapSize.Height - 1));
        var right = Math.Clamp((int)Math.Ceiling((region.X + region.Width) * bitmapSize.Width), left + 1, bitmapSize.Width);
        var bottom = Math.Clamp((int)Math.Ceiling((region.Y + region.Height) * bitmapSize.Height), top + 1, bitmapSize.Height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public PageRegionSelection? CreateSelection(int pageNumber, Point start, Point end, Size clientSize, SizeF pageSizePoints, int rotationDegrees, bool fitToWindow, int zoomPercent = 100)
    {
        if (!TryScreenToNormalized(start, clientSize, pageSizePoints, rotationDegrees, fitToWindow, out _, zoomPercent))
        {
            return null;
        }

        var clampedStart = ClampPointToViewport(start, clientSize, pageSizePoints, rotationDegrees, fitToWindow, zoomPercent);
        var clampedEnd = ClampPointToViewport(end, clientSize, pageSizePoints, rotationDegrees, fitToWindow, zoomPercent);
        var viewport = GetViewport(clientSize, pageSizePoints, rotationDegrees, fitToWindow, zoomPercent).Bounds;
        var left = Math.Min(clampedStart.X, clampedEnd.X);
        var top = Math.Min(clampedStart.Y, clampedEnd.Y);
        var right = Math.Max(clampedStart.X, clampedEnd.X);
        var bottom = Math.Max(clampedStart.Y, clampedEnd.Y);
        var width = right - left;
        var height = bottom - top;
        var minimumWidth = MinRegionPixels / viewport.Width;
        var minimumHeight = MinRegionPixels / viewport.Height;

        var normalized = new PageRegionSelection(
            pageNumber,
            (left - viewport.Left) / viewport.Width,
            (top - viewport.Top) / viewport.Height,
            width / viewport.Width,
            height / viewport.Height);

        return normalized.Width < minimumWidth || normalized.Height < minimumHeight
            ? null
            : Clamp(normalized);
    }

    public static PageRegionSelection Clamp(PageRegionSelection region)
    {
        var x = Math.Clamp(region.X, 0D, 1D);
        var y = Math.Clamp(region.Y, 0D, 1D);
        var width = Math.Clamp(region.Width, 0D, 1D - x);
        var height = Math.Clamp(region.Height, 0D, 1D - y);
        return region with { X = x, Y = y, Width = width, Height = height };
    }

    private static int NormalizeRotation(int rotationDegrees)
    {
        var normalized = rotationDegrees % 360;
        if (normalized < 0) normalized += 360;
        return normalized;
    }
}

public sealed record PageViewport(RectangleF Bounds, SizeF PageSizePoints);
