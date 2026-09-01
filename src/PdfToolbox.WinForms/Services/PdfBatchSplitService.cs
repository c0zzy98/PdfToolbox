using PdfToolbox.Exceptions;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

public sealed class PdfBatchSplitService(FileService fileService, PdfSplitterService splitter)
{
    public async Task<OperationResult> SplitManyAsync(
        IReadOnlyList<PdfDocumentInfo> documents,
        string outputDirectory,
        IProgress<ProcessingProgress>? progress,
        Action<int, string>? statusChanged,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0) throw new UserFacingException("Dodaj co najmniej jeden dokument PDF.");
        fileService.ValidateOutputDirectory(outputDirectory);
        fileService.EnsureOutputDirectory(outputDirectory);
        var outputs = new List<string>();

        for (var index = 0; index < documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusChanged?.Invoke(index, "W trakcie");
            var document = documents[index];
            var stem = Path.GetFileNameWithoutExtension(document.FileName);
            var documentDirectory = fileService.GetAvailableDirectoryPath(outputDirectory, stem);
            Directory.CreateDirectory(documentDirectory);
            try
            {
                var currentIndex = index;
                var pageProgress = new Progress<ProcessingProgress>(p =>
                {
                    var overall = (currentIndex * 100 + p.Percentage) / documents.Count;
                    progress?.Report(new ProcessingProgress(overall, $"{document.FileName} — {p.Message}"));
                });
                var files = await splitter.SplitAsync(document.Path, documentDirectory, pageProgress, cancellationToken);
                outputs.AddRange(files);
                statusChanged?.Invoke(index, "Gotowe");
            }
            catch
            {
                FileService.TryDeleteDirectory(documentDirectory);
                statusChanged?.Invoke(index, "Nie ukończono");
                throw;
            }
        }

        return new OperationResult(outputDirectory, outputs, $"Podzielono {documents.Count} dokumentów. Utworzono {outputs.Count} plików.");
    }
}
