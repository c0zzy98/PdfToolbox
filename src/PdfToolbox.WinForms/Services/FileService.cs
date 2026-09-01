using PdfToolbox.Exceptions;

namespace PdfToolbox.Services;

public sealed class FileService
{
    public void ValidateInputFile(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new UserFacingException("Wybierz plik PDF do przetworzenia.");
        }

        if (!string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFacingException("Wybrany plik nie jest dokumentem PDF.");
        }

        if (!File.Exists(inputPath))
        {
            throw new UserFacingException("Wybrany plik nie istnieje.");
        }

    }

    public void ValidateOutputDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new UserFacingException("Wybierz folder wynikowy.");
        }

    }

    public string GetDefaultOutputDirectory(string inputPath)
    {
        var parent = Path.GetDirectoryName(inputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(parent, $"{Path.GetFileNameWithoutExtension(inputPath)}_PDFToolbox");
    }

    public void EnsureOutputDirectory(string outputDirectory)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            var probe = Path.Combine(outputDirectory, $".pdftoolbox-write-test-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe, 4096, FileOptions.DeleteOnClose)) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UserFacingException("Nie można zapisywać w wybranym folderze. Wybierz inny folder lub sprawdź uprawnienia.", ex);
        }
    }

    public string GetAvailableFilePath(string directory, string baseName, string extension = ".pdf")
    {
        var candidate = Path.Combine(directory, baseName + extension);
        var number = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}_{number++}{extension}");
        }

        return candidate;
    }

    public string GetAvailableDirectoryPath(string parentDirectory, string baseName)
    {
        var candidate = Path.Combine(parentDirectory, baseName);
        var number = 2;
        while (Directory.Exists(candidate) || File.Exists(candidate))
        {
            candidate = Path.Combine(parentDirectory, $"{baseName}_{number++}");
        }

        return candidate;
    }

    public static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    public static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
