namespace PdfToolbox.Models;

public sealed class RenderedPdfPage : IDisposable
{
    public RenderedPdfPage(Bitmap bitmap, SizeF pageSizePoints, int rotationDegrees)
    {
        Bitmap = bitmap;
        PageSizePoints = pageSizePoints;
        RotationDegrees = rotationDegrees;
    }

    public Bitmap Bitmap { get; }
    public SizeF PageSizePoints { get; }
    public int RotationDegrees { get; }

    public void Dispose() => Bitmap.Dispose();
}
