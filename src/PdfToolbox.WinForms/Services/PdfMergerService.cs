using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfToolbox.Exceptions;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

public sealed class PdfMergerService(FileService fileService)
{
    public Task<OperationResult> MergeAsync(
        IReadOnlyList<string> inputPaths,
        string outputDirectory,
        string requestedName,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => Merge(inputPaths, outputDirectory, requestedName, progress, cancellationToken), cancellationToken);

    private OperationResult Merge(
        IReadOnlyList<string> inputPaths,
        string outputDirectory,
        string requestedName,
        IProgress<ProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (inputPaths.Count == 0) throw new UserFacingException("Dodaj co najmniej jeden dokument PDF.");
        foreach (var path in inputPaths) fileService.ValidateInputFile(path);
        fileService.ValidateOutputDirectory(outputDirectory);
        fileService.EnsureOutputDirectory(outputDirectory);
        var safeName = SanitizeName(requestedName);
        var outputPath = fileService.GetAvailableFilePath(outputDirectory, safeName);
        string? partial = outputPath + $".partial-{Guid.NewGuid():N}";
        try
        {
            using var output = new PdfDocument();
            var processedPages = 0;
            for (var documentIndex = 0; documentIndex < inputPaths.Count; documentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var input = PdfReader.Open(inputPaths[documentIndex], PdfDocumentOpenMode.Import);
                for (var pageIndex = 0; pageIndex < input.PageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output.AddPage(input.Pages[pageIndex]);
                    processedPages++;
                }

                progress?.Report(new ProcessingProgress(
                    (documentIndex + 1) * 90 / inputPaths.Count,
                    $"Dokument {documentIndex + 1} z {inputPaths.Count}"));
            }

            if (processedPages == 0) throw new UserFacingException("Wybrane dokumenty nie zawierają stron.");
            cancellationToken.ThrowIfCancellationRequested();
            output.Save(partial);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, outputPath);
            partial = null;
            progress?.Report(new ProcessingProgress(100, "Zapisywanie zakończone"));
            return new OperationResult(outputDirectory, [outputPath], $"Połączono {inputPaths.Count} dokumentów w jeden plik.");
        }
        catch (OperationCanceledException) { Cleanup(); throw; }
        catch (UserFacingException) { Cleanup(); throw; }
        catch (PdfReaderException ex)
        {
            Cleanup();
            throw new UserFacingException("Nie udało się otworzyć jednego z dokumentów. Plik może być uszkodzony lub zabezpieczony.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Cleanup();
            throw new UserFacingException("Nie udało się zapisać połączonego dokumentu. Sprawdź wolne miejsce i dostęp do folderu.", ex);
        }

        void Cleanup()
        {
            FileService.TryDeleteFile(partial);
            FileService.TryDeleteFile(outputPath);
        }
    }

    public static string SanitizeName(string requestedName)
    {
        var name = Path.GetFileNameWithoutExtension(requestedName?.Trim());
        if (string.IsNullOrWhiteSpace(name)) name = "Polaczone_dokumenty";
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "Polaczone_dokumenty" : name;
    }
}
