using System.Drawing;
using PdfToolbox.Helpers;
using PdfToolbox.Models;

namespace PdfToolbox.Tests;

public sealed class PageCoordinateMapperTests
{
    private readonly PageCoordinateMapper _mapper = new();

    [Theory]
    [InlineData(595, 842, 0, 1000, 800, 0.1, 0.2)]
    [InlineData(842, 595, 0, 1000, 800, 0.1, 0.2)]
    [InlineData(595, 842, 90, 1000, 800, 0.1, 0.2)]
    [InlineData(595, 842, 180, 1000, 800, 0.1, 0.2)]
    [InlineData(595, 842, 270, 1000, 800, 0.1, 0.2)]
    public void ScreenToNormalized_MapsVisiblePage(double pageWidth, double pageHeight, int rotation, int clientWidth, int clientHeight, double expectedX, double expectedY)
    {
        var pageSize = new SizeF((float)pageWidth, (float)pageHeight);
        var viewport = _mapper.GetViewport(new Size(clientWidth, clientHeight), pageSize, rotation, fitToWindow: true);
        var point = new Point(
            (int)Math.Round(viewport.Bounds.Left + viewport.Bounds.Width * expectedX),
            (int)Math.Round(viewport.Bounds.Top + viewport.Bounds.Height * expectedY));

        var success = _mapper.TryScreenToNormalized(point, new Size(clientWidth, clientHeight), pageSize, rotation, fitToWindow: true, out var normalized);

        Assert.True(success);
        Assert.Equal(expectedX, normalized.X, 2);
        Assert.Equal(expectedY, normalized.Y, 2);
    }

    [Theory]
    [InlineData(595, 842, 0, 1200, 500)]
    [InlineData(595, 842, 0, 500, 1200)]
    public void ScreenToNormalized_RejectsLetterboxArea(double pageWidth, double pageHeight, int rotation, int clientWidth, int clientHeight)
    {
        var pageSize = new SizeF((float)pageWidth, (float)pageHeight);
        var viewport = _mapper.GetViewport(new Size(clientWidth, clientHeight), pageSize, rotation, fitToWindow: true);
        var point = viewport.Bounds.Width < clientWidth
            ? new Point((int)Math.Max(0, viewport.Bounds.Left - 5), clientHeight / 2)
            : new Point(clientWidth / 2, (int)Math.Max(0, viewport.Bounds.Top - 5));

        var success = _mapper.TryScreenToNormalized(point, new Size(clientWidth, clientHeight), pageSize, rotation, fitToWindow: true, out _);

        Assert.False(success);
    }

    [Fact]
    public void NormalizedToBitmap_MapsToExpectedRectangle()
    {
        var region = new PageRegionSelection(1, 0.10, 0.20, 0.70, 0.18);

        var bitmap = _mapper.NormalizedToBitmap(region, new Size(2000, 3000));

        Assert.Equal(new Rectangle(200, 600, 1400, 540), bitmap);
    }

    [Fact]
    public void NormalizedToScreen_PreservesTheSamePageRegionAfterResize()
    {
        var pageSize = new SizeF(595, 842);
        var region = new PageRegionSelection(1, 0.12, 0.23, 0.45, 0.31);
        var before = _mapper.NormalizedToScreen(region, new Size(800, 420), pageSize, 0, fitToWindow: true);
        var after = _mapper.NormalizedToScreen(region, new Size(1600, 900), pageSize, 0, fitToWindow: true);
        var beforeViewport = _mapper.GetViewport(new Size(800, 420), pageSize, 0, fitToWindow: true).Bounds;
        var afterViewport = _mapper.GetViewport(new Size(1600, 900), pageSize, 0, fitToWindow: true).Bounds;

        Assert.Equal(region.X, (before.Left - beforeViewport.Left) / beforeViewport.Width, 4);
        Assert.Equal(region.Y, (before.Top - beforeViewport.Top) / beforeViewport.Height, 4);
        Assert.Equal(region.Width, before.Width / beforeViewport.Width, 4);
        Assert.Equal(region.Height, before.Height / beforeViewport.Height, 4);
        Assert.Equal(region.X, (after.Left - afterViewport.Left) / afterViewport.Width, 4);
        Assert.Equal(region.Y, (after.Top - afterViewport.Top) / afterViewport.Height, 4);
        Assert.Equal(region.Width, after.Width / afterViewport.Width, 4);
        Assert.Equal(region.Height, after.Height / afterViewport.Height, 4);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(400)]
    public void NormalizedToScreen_PreservesTheSamePageRegionAtManualZoom(int zoomPercent)
    {
        var pageSize = new SizeF(595, 842);
        var region = new PageRegionSelection(1, 0.18, 0.32, 0.41, 0.24);
        var canvas = _mapper.GetCanvasSize(pageSize, 0, zoomPercent);
        var viewport = _mapper.GetViewport(canvas, pageSize, 0, fitToWindow: false, zoomPercent).Bounds;
        var screenBounds = _mapper.NormalizedToScreen(region, canvas, pageSize, 0, fitToWindow: false, zoomPercent);

        Assert.Equal(region.X, (screenBounds.Left - viewport.Left) / viewport.Width, 4);
        Assert.Equal(region.Y, (screenBounds.Top - viewport.Top) / viewport.Height, 4);
        Assert.Equal(region.Width, screenBounds.Width / viewport.Width, 4);
        Assert.Equal(region.Height, screenBounds.Height / viewport.Height, 4);
    }

    [Fact]
    public void CreateSelection_HandlesReversedDrag()
    {
        var pageSize = new SizeF(595, 842);
        var viewport = _mapper.GetViewport(new Size(1000, 800), pageSize, 0, fitToWindow: true);
        var start = new Point((int)viewport.Bounds.Right - 100, (int)viewport.Bounds.Bottom - 80);
        var end = new Point((int)viewport.Bounds.Left + 100, (int)viewport.Bounds.Top + 80);

        var selection = _mapper.CreateSelection(1, start, end, new Size(1000, 800), pageSize, 0, fitToWindow: true);

        Assert.NotNull(selection);
        Assert.True(selection!.Width > 0);
        Assert.True(selection.Height > 0);
    }

    [Fact]
    public void CreateSelection_ClampsToVisiblePage()
    {
        var pageSize = new SizeF(595, 842);
        var viewport = _mapper.GetViewport(new Size(1000, 800), pageSize, 0, fitToWindow: true);
        var start = new Point((int)viewport.Bounds.Left + 20, (int)viewport.Bounds.Top + 20);
        var end = new Point(5000, 5000);

        var selection = _mapper.CreateSelection(1, start, end, new Size(1000, 800), pageSize, 0, fitToWindow: true);

        Assert.NotNull(selection);
        Assert.True(selection!.X >= 0);
        Assert.True(selection.Y >= 0);
        Assert.True(selection.X + selection.Width <= 1);
        Assert.True(selection.Y + selection.Height <= 1);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(5, 5)]
    public void CreateSelection_RejectsZeroAndMicroSelections(int widthPixels, int heightPixels)
    {
        var pageSize = new SizeF(595, 842);
        var viewport = _mapper.GetViewport(new Size(1000, 800), pageSize, 0, fitToWindow: true);
        var start = new Point((int)viewport.Bounds.Left + 40, (int)viewport.Bounds.Top + 40);
        var end = new Point(start.X + widthPixels, start.Y + heightPixels);

        var selection = _mapper.CreateSelection(1, start, end, new Size(1000, 800), pageSize, 0, fitToWindow: true);

        Assert.Null(selection);
    }

    [Fact]
    public void Clamp_AdjustsOutsideRegion()
    {
        var clamped = PageCoordinateMapper.Clamp(new PageRegionSelection(2, -0.1, 0.9, 0.5, 0.5));

        Assert.Equal(0, clamped.X);
        Assert.Equal(0.9, clamped.Y, 3);
        Assert.Equal(0.5, clamped.Width, 3);
        Assert.Equal(0.1, clamped.Height, 3);
    }
}
