using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;
using PdfToolbox.Exceptions;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

public sealed class PdfValidationService(FileService fileService)
{
    public Task<PdfDocumentInfo> InspectAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() => Inspect(path, cancellationToken), cancellationToken);

    private PdfDocumentInfo Inspect(string path, CancellationToken cancellationToken)
    {
        fileService.ValidateInputFile(path);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            if (document.PageCount == 0) throw new UserFacingException("Dokument nie zawiera żadnych stron.");
            var hasSignature = document.Internals.Catalog.Elements.ContainsKey("/AcroForm") &&
                               HasSignatureField(document.AcroForm.Fields);
            return new PdfDocumentInfo(path, Path.GetFileName(path), document.PageCount, new FileInfo(path).Length, hasSignature);
        }
        catch (OperationCanceledException) { throw; }
        catch (UserFacingException) { throw; }
        catch (Exception ex) when (ex is PdfReaderException or InvalidOperationException)
        {
            throw new UserFacingException(
                "Nie można otworzyć tego dokumentu. Plik może być chroniony hasłem lub posiadać zabezpieczenia uniemożliwiające jego zmianę. PDF Toolbox nie omija zabezpieczeń dokumentów.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UserFacingException("Nie można odczytać dokumentu. Sprawdź, czy plik nie jest używany przez inny program i czy masz do niego dostęp.", ex);
        }
    }

    private static bool HasSignatureField(PdfAcroField.PdfAcroFieldCollection? fields)
    {
        if (fields is null) return false;
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            if (field is PdfSignatureField || string.Equals(field.Elements.GetName("/FT"), "/Sig", StringComparison.Ordinal)) return true;
            if (field.HasKids && HasSignatureField(field.Fields)) return true;
        }

        return false;
    }
}
