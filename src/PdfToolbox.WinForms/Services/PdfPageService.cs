using PdfSharp.Pdf;
using PdfToolbox.Exceptions;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

public sealed class PdfPageService(FileService fileService)
{
    public Task<OperationResult> ExtractAsync(
        PdfDocumentInfo document,
        IReadOnlyCollection<int> pages,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken) =>
        WriteAsync(document, pages, outputDirectory, "wybrane_strony", "Zapisano wybrane strony w nowym dokumencie.", null, progress, cancellationToken);

    public Task<OperationResult> RemoveAsync(
        PdfDocumentInfo document,
        IReadOnlyCollection<int> pagesToRemove,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidatePages(pagesToRemove, document.PageCount);
        if (pagesToRemove.Count >= document.PageCount) throw new UserFacingException("Nie można usunąć wszystkich stron dokumentu.");
        var remove = pagesToRemove.ToHashSet();
        var keep = Enumerable.Range(1, document.PageCount).Where(page => !remove.Contains(page)).ToArray();
        return WriteAsync(document, keep, outputDirectory, "po_usunieciu_stron", $"Usunięto {pagesToRemove.Count} stron. Oryginalny plik pozostał bez zmian.", null, progress, cancellationToken);
    }

    public Task<OperationResult> RotateAsync(
        PdfDocumentInfo document,
        IReadOnlyCollection<int> pagesToRotate,
        RotationDirection direction,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidatePages(pagesToRotate, document.PageCount);
        var rotate = pagesToRotate.ToHashSet();
        var sourcePage = 0;
        PdfPage Transform(PdfPage page)
        {
            sourcePage++;
            if (!rotate.Contains(sourcePage)) return page;
            var rotation = (page.Rotate + (int)direction) % 360;
            page.Rotate = rotation < 0 ? rotation + 360 : rotation;
            return page;
        }

        return WriteAsync(
            document,
            Enumerable.Range(1, document.PageCount).ToArray(),
            outputDirectory,
            "obrocona",
            $"Obrócono {pagesToRotate.Count} stron i zapisano nowy dokument.",
            Transform,
            progress,
            cancellationToken);
    }

    private Task<OperationResult> WriteAsync(
        PdfDocumentInfo document,
        IReadOnlyCollection<int> pages,
        string outputDirectory,
        string suffix,
        string summary,
        Func<PdfPage, PdfPage>? transform,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidatePages(pages, document.PageCount);
        fileService.ValidateOutputDirectory(outputDirectory);
        fileService.EnsureOutputDirectory(outputDirectory);
        var stem = Path.GetFileNameWithoutExtension(document.FileName);
        var outputPath = fileService.GetAvailableFilePath(outputDirectory, $"{stem}_{suffix}");
        return Task.Run(() =>
        {
            PdfDocumentWriter.WriteSelectedPages(document.Path, outputPath, pages, transform, progress, cancellationToken);
            return new OperationResult(outputDirectory, [outputPath], summary);
        }, cancellationToken);
    }

    private static void ValidatePages(IReadOnlyCollection<int> pages, int pageCount)
    {
        if (pages.Count == 0) throw new UserFacingException("Wybierz co najmniej jedną stronę.");
        if (pages.Any(page => page < 1 || page > pageCount)) throw new UserFacingException("Wybrano stronę spoza dokumentu.");
    }
}
