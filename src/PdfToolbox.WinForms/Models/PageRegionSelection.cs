namespace PdfToolbox.Models;

public sealed record PageRegionSelection(
    int PageNumber,
    double X,
    double Y,
    double Width,
    double Height);
