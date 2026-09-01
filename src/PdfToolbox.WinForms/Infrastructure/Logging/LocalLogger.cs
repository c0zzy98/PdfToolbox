using System.Reflection;

namespace PdfToolbox.Infrastructure.Logging;

public sealed class LocalLogger
{
    private readonly string _logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfToolbox", "Logs");

    public async Task LogErrorAsync(string operation, Exception exception, TimeSpan elapsed)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var logPath = Path.Combine(_logDirectory, $"errors-{DateTime.UtcNow:yyyy-MM}.log");
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            var entry = $"[{DateTimeOffset.Now:O}] Version={version}; Operation={operation}; DurationMs={elapsed.TotalMilliseconds:F0}; " +
                        $"Exception={exception.GetType().FullName}; HResult=0x{exception.HResult:X8}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath, entry).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never prevent the application from recovering.
        }
    }
}
