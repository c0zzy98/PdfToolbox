using PdfToolbox.Helpers;
using PdfToolbox.Models;

namespace PdfToolbox.Controls;

public sealed class PdfRegionSelector : UserControl
{
    private const int HandleSize = 10;
    private readonly PageCoordinateMapper _mapper = new();
    private readonly List<PageRegionSelection> _regions = [];
    private Bitmap? _previewImage;
    private SizeF _pageSizePoints;
    private int _rotationDegrees;
    private int _pageNumber;
    private Point _dragStart;
    private EditMode _editMode;
    private ResizeHandle _resizeHandle;
    private RectangleF _originalBounds;

    public PdfRegionSelector()
    {
        DoubleBuffered = true;
        TabStop = true;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(226, 232, 240);
        SetStyle(ControlStyles.Selectable, true);
        MouseDown += HandleMouseDown;
        MouseMove += HandleMouseMove;
        MouseUp += HandleMouseUp;
        KeyDown += HandleKeyDown;
    }

    public event EventHandler? RegionsChanged;
    public event EventHandler? SelectedRegionChanged;

    public bool FitToWindow { get; set; } = true;

    public int SelectedRegionIndex { get; private set; } = -1;

    public IReadOnlyList<PageRegionSelection> Regions => _regions;

    public void SetPage(int pageNumber, Bitmap? previewImage, SizeF pageSizePoints, int rotationDegrees, IReadOnlyCollection<PageRegionSelection> regions)
    {
        _pageNumber = pageNumber;
        _pageSizePoints = pageSizePoints;
        _rotationDegrees = rotationDegrees;
        _previewImage?.Dispose();
        _previewImage = previewImage;
        _regions.Clear();
        _regions.AddRange(regions.Select(PageCoordinateMapper.Clamp));
        SelectedRegionIndex = _regions.Count == 0 ? -1 : Math.Min(SelectedRegionIndex, _regions.Count - 1);
        Invalidate();
    }

    public void ClearPageSelections()
    {
        if (_regions.Count == 0) return;
        _regions.Clear();
        SelectedRegionIndex = -1;
        RegionsChanged?.Invoke(this, EventArgs.Empty);
        SelectedRegionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void DeleteSelectedSelection()
    {
        if (SelectedRegionIndex < 0 || SelectedRegionIndex >= _regions.Count) return;
        _regions.RemoveAt(SelectedRegionIndex);
        SelectedRegionIndex = _regions.Count == 0 ? -1 : Math.Min(SelectedRegionIndex, _regions.Count - 1);
        RegionsChanged?.Invoke(this, EventArgs.Empty);
        SelectedRegionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (_previewImage is null || _pageSizePoints.Width <= 0 || _pageSizePoints.Height <= 0)
        {
            TextRenderer.DrawText(e.Graphics, "Podgląd strony pojawi się tutaj.", Font, ClientRectangle, Theme.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var viewport = _mapper.GetViewport(ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow).Bounds;
        e.Graphics.FillRectangle(Brushes.White, viewport);
        e.Graphics.DrawImage(_previewImage, viewport);
        using var borderPen = new Pen(Color.FromArgb(148, 163, 184), 1.5F);
        e.Graphics.DrawRectangle(borderPen, Rectangle.Round(viewport));

        for (var index = 0; index < _regions.Count; index++)
        {
            var regionBounds = _mapper.NormalizedToScreen(_regions[index], ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow);
            var isSelected = index == SelectedRegionIndex;
            using var fill = new SolidBrush(Color.FromArgb(isSelected ? 92 : 62, 37, 99, 235));
            using var pen = new Pen(isSelected ? Color.FromArgb(29, 78, 216) : Theme.Accent, isSelected ? 2.5F : 1.8F);
            e.Graphics.FillRectangle(fill, regionBounds);
            e.Graphics.DrawRectangle(pen, Rectangle.Round(regionBounds));

            var badge = new RectangleF(regionBounds.Left + 6, regionBounds.Top + 6, 28, 22);
            using var badgeBrush = new SolidBrush(Color.FromArgb(230, 15, 23, 42));
            e.Graphics.FillRectangle(badgeBrush, badge);
            TextRenderer.DrawText(e.Graphics, (index + 1).ToString(), new Font(Font, FontStyle.Bold), Rectangle.Round(badge), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (isSelected)
            {
                foreach (var handle in GetHandleBounds(regionBounds))
                {
                    e.Graphics.FillRectangle(Brushes.White, handle);
                    e.Graphics.DrawRectangle(Pens.DodgerBlue, Rectangle.Round(handle));
                }
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Regions are stored in normalized page coordinates, so repainting is enough
        // to place the current image and overlays in the newly calculated viewport.
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewImage?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        Focus();
        if (_previewImage is null || e.Button != MouseButtons.Left) return;

        var hit = HitTest(e.Location);
        _dragStart = e.Location;
        _originalBounds = hit.Index >= 0
            ? _mapper.NormalizedToScreen(_regions[hit.Index], ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow)
            : RectangleF.Empty;

        if (hit.Index >= 0)
        {
            SelectedRegionIndex = hit.Index;
            _editMode = hit.Handle == ResizeHandle.None ? EditMode.Moving : EditMode.Resizing;
            _resizeHandle = hit.Handle;
            SelectedRegionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
            return;
        }

        if (_mapper.TryScreenToNormalized(e.Location, ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow, out _))
        {
            SelectedRegionIndex = -1;
            _editMode = EditMode.Drawing;
            SelectedRegionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    private void HandleMouseMove(object? sender, MouseEventArgs e)
    {
        Cursor = HitTest(e.Location).Handle switch
        {
            ResizeHandle.TopLeft or ResizeHandle.BottomRight => Cursors.SizeNWSE,
            ResizeHandle.TopRight or ResizeHandle.BottomLeft => Cursors.SizeNESW,
            ResizeHandle.Move => Cursors.SizeAll,
            _ => Cursors.Cross
        };

        if (_editMode == EditMode.None || _previewImage is null || e.Button != MouseButtons.Left) return;

        switch (_editMode)
        {
            case EditMode.Drawing:
                PreviewDrawSelection(e.Location);
                break;
            case EditMode.Moving:
                MoveSelection(e.Location);
                break;
            case EditMode.Resizing:
                ResizeSelection(e.Location);
                break;
        }
    }

    private void HandleMouseUp(object? sender, MouseEventArgs e)
    {
        if (_editMode == EditMode.Drawing)
        {
            var selection = _mapper.CreateSelection(_pageNumber, _dragStart, e.Location, ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow);
            if (selection is not null)
            {
                _regions.Add(selection);
                SelectedRegionIndex = _regions.Count - 1;
                RegionsChanged?.Invoke(this, EventArgs.Empty);
                SelectedRegionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        _editMode = EditMode.None;
        _resizeHandle = ResizeHandle.None;
        Invalidate();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && _editMode == EditMode.Drawing)
        {
            _editMode = EditMode.None;
            Invalidate();
        }

        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedSelection();
        }
    }

    private void PreviewDrawSelection(Point location)
    {
        Invalidate();
        using var graphics = CreateGraphics();
        var selection = _mapper.CreateSelection(_pageNumber, _dragStart, location, ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow);
        if (selection is null) return;
        var bounds = _mapper.NormalizedToScreen(selection, ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow);
        using var fill = new SolidBrush(Color.FromArgb(52, 37, 99, 235));
        using var pen = new Pen(Theme.Accent, 1.5F);
        graphics.FillRectangle(fill, bounds);
        graphics.DrawRectangle(pen, Rectangle.Round(bounds));
    }

    private void MoveSelection(Point location)
    {
        if (SelectedRegionIndex < 0) return;
        var viewport = _mapper.GetViewport(ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow).Bounds;
        var offsetX = location.X - _dragStart.X;
        var offsetY = location.Y - _dragStart.Y;
        var moved = _originalBounds;
        moved.Offset(offsetX, offsetY);
        moved.X = Math.Clamp(moved.X, viewport.Left, viewport.Right - moved.Width);
        moved.Y = Math.Clamp(moved.Y, viewport.Top, viewport.Bottom - moved.Height);
        _regions[SelectedRegionIndex] = ToNormalizedSelection(moved);
        RegionsChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void ResizeSelection(Point location)
    {
        if (SelectedRegionIndex < 0) return;
        var viewport = _mapper.GetViewport(ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow).Bounds;
        var bounds = _originalBounds;
        var clamped = _mapper.ClampPointToViewport(location, ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow);

        switch (_resizeHandle)
        {
            case ResizeHandle.TopLeft:
                bounds = RectangleF.FromLTRB(clamped.X, clamped.Y, bounds.Right, bounds.Bottom);
                break;
            case ResizeHandle.TopRight:
                bounds = RectangleF.FromLTRB(bounds.Left, clamped.Y, clamped.X, bounds.Bottom);
                break;
            case ResizeHandle.BottomLeft:
                bounds = RectangleF.FromLTRB(clamped.X, bounds.Top, bounds.Right, clamped.Y);
                break;
            case ResizeHandle.BottomRight:
                bounds = RectangleF.FromLTRB(bounds.Left, bounds.Top, clamped.X, clamped.Y);
                break;
        }

        bounds = RectangleF.FromLTRB(
            Math.Clamp(Math.Min(bounds.Left, bounds.Right), viewport.Left, viewport.Right - 1),
            Math.Clamp(Math.Min(bounds.Top, bounds.Bottom), viewport.Top, viewport.Bottom - 1),
            Math.Clamp(Math.Max(bounds.Left, bounds.Right), viewport.Left + 1, viewport.Right),
            Math.Clamp(Math.Max(bounds.Top, bounds.Bottom), viewport.Top + 1, viewport.Bottom));

        var candidate = ToNormalizedSelection(bounds);
        if (candidate.Width <= 0 || candidate.Height <= 0) return;
        _regions[SelectedRegionIndex] = candidate;
        RegionsChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private PageRegionSelection ToNormalizedSelection(RectangleF screenBounds)
    {
        var viewport = _mapper.GetViewport(ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow).Bounds;
        return PageCoordinateMapper.Clamp(new PageRegionSelection(
            _pageNumber,
            (screenBounds.Left - viewport.Left) / viewport.Width,
            (screenBounds.Top - viewport.Top) / viewport.Height,
            screenBounds.Width / viewport.Width,
            screenBounds.Height / viewport.Height));
    }

    private HitResult HitTest(Point location)
    {
        for (var index = _regions.Count - 1; index >= 0; index--)
        {
            var bounds = _mapper.NormalizedToScreen(_regions[index], ClientSize, _pageSizePoints, _rotationDegrees, FitToWindow);
            foreach (var pair in GetHandleBounds(bounds))
            {
                if (pair.Contains(location))
                {
                    return new HitResult(index, GetHandleKind(bounds, pair));
                }
            }

            if (bounds.Contains(location))
            {
                return new HitResult(index, ResizeHandle.Move);
            }
        }

        return new HitResult(-1, ResizeHandle.None);
    }

    private static IEnumerable<RectangleF> GetHandleBounds(RectangleF bounds)
    {
        yield return new RectangleF(bounds.Left - HandleSize / 2F, bounds.Top - HandleSize / 2F, HandleSize, HandleSize);
        yield return new RectangleF(bounds.Right - HandleSize / 2F, bounds.Top - HandleSize / 2F, HandleSize, HandleSize);
        yield return new RectangleF(bounds.Left - HandleSize / 2F, bounds.Bottom - HandleSize / 2F, HandleSize, HandleSize);
        yield return new RectangleF(bounds.Right - HandleSize / 2F, bounds.Bottom - HandleSize / 2F, HandleSize, HandleSize);
    }

    private static ResizeHandle GetHandleKind(RectangleF bounds, RectangleF handle)
    {
        if (Math.Abs(handle.Left - (bounds.Left - HandleSize / 2F)) < 0.1F && Math.Abs(handle.Top - (bounds.Top - HandleSize / 2F)) < 0.1F) return ResizeHandle.TopLeft;
        if (Math.Abs(handle.Left - (bounds.Right - HandleSize / 2F)) < 0.1F && Math.Abs(handle.Top - (bounds.Top - HandleSize / 2F)) < 0.1F) return ResizeHandle.TopRight;
        if (Math.Abs(handle.Left - (bounds.Left - HandleSize / 2F)) < 0.1F && Math.Abs(handle.Top - (bounds.Bottom - HandleSize / 2F)) < 0.1F) return ResizeHandle.BottomLeft;
        return ResizeHandle.BottomRight;
    }

    private enum EditMode
    {
        None,
        Drawing,
        Moving,
        Resizing
    }

    private enum ResizeHandle
    {
        None,
        Move,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private readonly record struct HitResult(int Index, ResizeHandle Handle);
}
