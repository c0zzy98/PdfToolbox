using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfToolbox.Exceptions;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

public sealed class PdfSplitterService(FileService fileService)
{
    public Task<IReadOnlyList<string>> SplitAsync(
        string inputPath,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(() => Split(inputPath, outputDirectory, progress, cancellationToken), cancellationToken);

    private IReadOnlyList<string> Split(
        string inputPath,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var created = new List<string>();
        string? partialPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            if (input.PageCount == 0)
            {
                throw new UserFacingException("Dokument PDF nie zawiera żadnych stron.");
            }

            var stem = Path.GetFileNameWithoutExtension(inputPath);
            var digits = Math.Max(3, input.PageCount.ToString().Length);
            for (var pageIndex = 0; pageIndex < input.PageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pageNumber = (pageIndex + 1).ToString().PadLeft(digits, '0');
                var baseName = $"{stem}_{pageNumber}";
                var outputPath = fileService.GetAvailableFilePath(outputDirectory, baseName);
                partialPath = outputPath + $".partial-{Guid.NewGuid():N}";

                using (var output = new PdfDocument())
                {
                    output.AddPage(input.Pages[pageIndex]);
                    output.Save(partialPath);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(partialPath, outputPath);
                partialPath = null;
                created.Add(outputPath);
                var percentage = (pageIndex + 1) * 100 / input.PageCount;
                progress?.Report(new ProcessingProgress(percentage, $"Przetwarzanie strony {pageIndex + 1} z {input.PageCount}…"));
            }

            return created;
        }
        catch (OperationCanceledException)
        {
            Cleanup();
            throw;
        }
        catch (UserFacingException)
        {
            Cleanup();
            throw;
        }
        catch (PdfReaderException ex)
        {
            Cleanup();
            throw new UserFacingException("Nie udało się otworzyć dokumentu. Plik może być uszkodzony lub zabezpieczony hasłem.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Cleanup();
            throw new UserFacingException("Nie udało się utworzyć plików wynikowych. Sprawdź wolne miejsce i uprawnienia do folderu.", ex);
        }

        void Cleanup()
        {
            FileService.TryDeleteFile(partialPath);
            foreach (var file in created) FileService.TryDeleteFile(file);
        }
    }
}
