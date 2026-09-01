namespace PdfToolbox.Models;

public sealed record OperationResult(string OutputDirectory, IReadOnlyList<string> OutputFiles, string Summary);
