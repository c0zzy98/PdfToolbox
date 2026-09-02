using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfToolbox.Exceptions;
using PdfToolbox.Helpers;
using PdfToolbox.Models;
using System.Runtime.InteropServices;

namespace PdfToolbox.Services;

public sealed class PdfRegionExtractionService(FileService fileService, PdfPagePreviewService previewService)
{
    private const int ExtractionDpi = 200;
    private static readonly PageCoordinateMapper Mapper = new();

    public async Task<OperationResult> ExtractRegionsAsync(
        PdfDocumentInfo document,
        IReadOnlyCollection<PageRegionSelection> regions,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateRegions(document, regions);
        fileService.ValidateOutputDirectory(outputDirectory);
        fileService.EnsureOutputDirectory(outputDirectory);

        var orderedRegions = regions
            .OrderBy(region => region.PageNumber)
            .ThenBy(region => region.Y)
            .ThenBy(region => region.X)
            .ToArray();

        var created = new List<string>();
        string? partialPath = null;
        try
        {
            var grouped = orderedRegions.GroupBy(region => region.PageNumber).ToArray();
            var completed = 0;
            foreach (var pageGroup in grouped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var renderedPage = await previewService.RenderForExtractionAsync(document, pageGroup.Key, ExtractionDpi, cancellationToken).ConfigureAwait(false);
                var pageRegions = pageGroup.ToArray();
                for (var index = 0; index < pageRegions.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var region = pageRegions[index];
                    using var cropped = CropBitmap(renderedPage.Bitmap, region);
                    var baseName = BuildOutputBaseName(document.FileName, region.PageNumber, index + 1);
                    var outputPath = fileService.GetAvailableFilePath(outputDirectory, baseName);
                    partialPath = outputPath + $".partial-{Guid.NewGuid():N}";
                    SaveCroppedRegionPdf(cropped, partialPath, renderedPage.PageSizePoints, region);
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(partialPath, outputPath);
                    partialPath = null;
                    created.Add(outputPath);
                    completed++;
                    progress?.Report(new ProcessingProgress(completed * 100 / orderedRegions.Length, $"Przetwarzanie fragmentu {completed} z {orderedRegions.Length}…"));
                }
            }

            return new OperationResult(outputDirectory, created, $"Zapisano {created.Count} fragmentów jako osobne pliki PDF.");
        }
        catch (OperationCanceledException)
        {
            Cleanup(created, partialPath);
            throw;
        }
        catch (UserFacingException)
        {
            Cleanup(created, partialPath);
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            Cleanup(created, partialPath);
            throw new UserFacingException("Nie udało się zapisać wszystkich fragmentów. Sprawdź folder wynikowy, wolne miejsce i dostęp do plików.", ex);
        }
    }

    private static void SaveCroppedRegionPdf(Bitmap cropped, string outputPath, SizeF renderedPagePoints, PageRegionSelection region)
    {
        using var pngStream = new MemoryStream();
        cropped.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
        pngStream.Position = 0;

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = PdfSharp.Drawing.XUnit.FromPoint(renderedPagePoints.Width * (float)region.Width);
        page.Height = PdfSharp.Drawing.XUnit.FromPoint(renderedPagePoints.Height * (float)region.Height);

        using var graphics = XGraphics.FromPdfPage(page);
        // We intentionally rasterize the selected region instead of using CropBox,
        // because CropBox may only hide content outside the visible area and would
        // not provide sufficient data isolation for HR documents.
        using var imageStream = new MemoryStream(pngStream.ToArray(), writable: false);
        using var image = XImage.FromStream(imageStream);
        graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        document.Save(outputPath);
    }

    private static Bitmap CropBitmap(Bitmap source, PageRegionSelection region)
    {
        var bounds = Mapper.NormalizedToBitmap(region, source.Size);
        return source.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

    private static string BuildOutputBaseName(string documentFileName, int pageNumber, int fragmentNumber)
    {
        var stem = Path.GetFileNameWithoutExtension(documentFileName);
        return $"{stem}_strona_{pageNumber:000}_fragment_{fragmentNumber:00}";
    }

    private static void ValidateRegions(PdfDocumentInfo document, IReadOnlyCollection<PageRegionSelection> regions)
    {
        if (regions.Count == 0)
        {
            throw new UserFacingException("Najpierw zaznacz co najmniej jeden fragment strony.");
        }

        foreach (var region in regions)
        {
            if (region.PageNumber < 1 || region.PageNumber > document.PageCount)
            {
                throw new UserFacingException("Co najmniej jedno zaznaczenie wskazuje stronę spoza dokumentu.");
            }

            var clamped = PageCoordinateMapper.Clamp(region);
            if (clamped != region || region.Width <= 0D || region.Height <= 0D)
            {
                throw new UserFacingException("Co najmniej jedno zaznaczenie ma nieprawidłowe współrzędne.");
            }
        }
    }

    private static void Cleanup(IEnumerable<string> created, string? partialPath)
    {
        FileService.TryDeleteFile(partialPath);
        foreach (var file in created) FileService.TryDeleteFile(file);
    }
}
