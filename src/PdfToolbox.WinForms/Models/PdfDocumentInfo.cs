namespace PdfToolbox.Models;

public sealed record PdfDocumentInfo(
    string Path,
    string FileName,
    int PageCount,
    long SizeBytes,
    bool MayContainDigitalSignature);
