using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfToolbox.Exceptions;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

internal static class PdfDocumentWriter
{
    public static void WriteSelectedPages(
        string inputPath,
        string outputPath,
        IReadOnlyCollection<int> oneBasedPages,
        Func<PdfPage, PdfPage>? transform,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        string? partial = outputPath + $".partial-{Guid.NewGuid():N}";
        try
        {
            using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            using var output = new PdfDocument();
            var ordered = oneBasedPages.Order().ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceIndex = ordered[index] - 1;
                if (sourceIndex < 0 || sourceIndex >= input.PageCount) throw new UserFacingException("Wybrano stronę spoza dokumentu.");
                var page = output.AddPage(input.Pages[sourceIndex]);
                if (transform is not null) transform(page);
                progress?.Report(new ProcessingProgress((index + 1) * 90 / ordered.Length, $"Strona {index + 1} z {ordered.Length}"));
            }

            cancellationToken.ThrowIfCancellationRequested();
            output.Save(partial);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, outputPath);
            partial = null;
            progress?.Report(new ProcessingProgress(100, "Zapisywanie zakończone"));
        }
        catch (OperationCanceledException) { FileService.TryDeleteFile(partial); FileService.TryDeleteFile(outputPath); throw; }
        catch (UserFacingException) { FileService.TryDeleteFile(partial); throw; }
        catch (PdfReaderException ex)
        {
            FileService.TryDeleteFile(partial);
            throw new UserFacingException("Nie udało się otworzyć dokumentu. Plik może być uszkodzony lub zabezpieczony.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FileService.TryDeleteFile(partial);
            throw new UserFacingException("Nie udało się zapisać nowego dokumentu. Sprawdź wolne miejsce i dostęp do folderu.", ex);
        }
    }
}
