using System.Diagnostics;
using System.IO.Pipes;
using PdfSharp.Pdf.IO;
using PdfToolbox.Exceptions;
using PdfToolbox.Helpers;
using PdfToolbox.Models;

namespace PdfToolbox.Services;

public sealed class PdfPagePreviewService
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(15);

    public async Task<RenderedPdfPage> RenderPreviewAsync(
        PdfDocumentInfo document,
        int pageNumber,
        Size viewportSize,
        CancellationToken cancellationToken)
    {
        var pageInfo = GetPageInfo(document.Path, pageNumber);
        var maxWidth = Math.Max(240, viewportSize.Width * 2);
        var maxHeight = Math.Max(240, viewportSize.Height * 2);
        var scale = Math.Min(maxWidth / pageInfo.PageSizePoints.Width, maxHeight / pageInfo.PageSizePoints.Height);
        var targetWidth = Math.Max(1, (int)Math.Ceiling(pageInfo.PageSizePoints.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Ceiling(pageInfo.PageSizePoints.Height * scale));
        return await RenderAsync(document.Path, pageNumber, targetWidth, targetHeight, pageInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RenderedPdfPage> RenderForExtractionAsync(
        PdfDocumentInfo document,
        int pageNumber,
        int dpi,
        CancellationToken cancellationToken)
    {
        var pageInfo = GetPageInfo(document.Path, pageNumber);
        var targetWidth = Math.Max(1, (int)Math.Ceiling(pageInfo.PageSizePoints.Width * dpi / 72D));
        var targetHeight = Math.Max(1, (int)Math.Ceiling(pageInfo.PageSizePoints.Height * dpi / 72D));
        return await RenderAsync(document.Path, pageNumber, targetWidth, targetHeight, pageInfo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RenderedPdfPage> RenderAsync(
        string documentPath,
        int pageNumber,
        int targetWidth,
        int targetHeight,
        PageRenderInfo pageInfo,
        CancellationToken cancellationToken)
    {
        var pipeName = $"PdfToolbox.Renderer.{Guid.NewGuid():N}";
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var process = StartRenderer(pipeName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RenderTimeout);

        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
            await RendererProtocol.WriteRequestAsync(pipe, new RendererRequest(documentPath, pageNumber, targetWidth, targetHeight), timeout.Token).ConfigureAwait(false);
            var response = await RendererProtocol.ReadResponseAsync(pipe, timeout.Token).ConfigureAwait(false);
            if (!response.Success)
            {
                throw new UserFacingException(string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? "Nie udało się wyświetlić podglądu tej strony."
                    : response.ErrorMessage);
            }

            using var pngStream = new MemoryStream(response.PngBytes, writable: false);
            var bitmap = new Bitmap(pngStream);
            await WaitForProcessExitAsync(process, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            return new RenderedPdfPage(new Bitmap(bitmap), pageInfo.PageSizePoints, pageInfo.RotationDegrees);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw new UserFacingException("Nie udało się wyświetlić podglądu tej strony.");
        }
        catch (UserFacingException)
        {
            TryTerminate(process);
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            TryTerminate(process);
            throw new UserFacingException("Nie udało się wyświetlić podglądu tej strony.", ex);
        }
    }

    private static PageRenderInfo GetPageInfo(string documentPath, int pageNumber)
    {
        try
        {
            using var document = PdfReader.Open(documentPath, PdfDocumentOpenMode.Import);
            if (pageNumber < 1 || pageNumber > document.PageCount)
            {
                throw new UserFacingException("Wybrano stronę spoza dokumentu.");
            }

            var page = document.Pages[pageNumber - 1];
            var rotation = NormalizeRotation(page.Rotate);
            var rawSize = new SizeF((float)page.Width.Point, (float)page.Height.Point);
            var oriented = PageCoordinateMapper.GetOrientedPageSize(rawSize, rotation);
            return new PageRenderInfo(oriented, rotation);
        }
        catch (UserFacingException)
        {
            throw;
        }
        catch (Exception ex) when (ex is PdfReaderException or IOException or InvalidOperationException)
        {
            throw new UserFacingException("Nie udało się przygotować strony do podglądu.", ex);
        }
    }

    private static Process StartRenderer(string pipeName)
    {
        var rendererPath = ResolveRendererPath();
        return Process.Start(new ProcessStartInfo
        {
            FileName = rendererPath,
            ArgumentList = { "--pipe", pipeName },
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new UserFacingException("Nie udało się uruchomić lokalnego procesu renderowania PDF.");
    }

    private static async Task WaitForProcessExitAsync(Process process, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
        }
    }

    private static string ResolveRendererPath()
    {
        var direct = Path.Combine(AppContext.BaseDirectory, "PdfToolbox.Renderer.exe");
        if (File.Exists(direct)) return direct;

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solution = Path.Combine(current.FullName, "PdfToolbox.sln");
            if (File.Exists(solution))
            {
                var candidates = Directory.GetFiles(Path.Combine(current.FullName, "src", "PdfToolbox.Renderer"), "PdfToolbox.Renderer.exe", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();
                if (candidates.Length > 0) return candidates[0];
                break;
            }

            current = current.Parent;
        }

        throw new UserFacingException("Nie udało się odnaleźć lokalnego procesu renderowania PDF.");
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }
    }

    private static int NormalizeRotation(int rotationDegrees)
    {
        var normalized = rotationDegrees % 360;
        if (normalized < 0) normalized += 360;
        return normalized;
    }

    private sealed record PageRenderInfo(SizeF PageSizePoints, int RotationDegrees);

    private sealed record RendererRequest(string InputPath, int PageNumber, int TargetWidth, int TargetHeight);

    private sealed record RendererResponse(bool Success, string ErrorMessage, byte[] PngBytes);

    private static class RendererProtocol
    {
        public static async Task WriteRequestAsync(Stream stream, RendererRequest request, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(request.InputPath);
                writer.Write(request.PageNumber);
                writer.Write(request.TargetWidth);
                writer.Write(request.TargetHeight);
            }

            await WriteFrameAsync(stream, buffer.ToArray(), cancellationToken).ConfigureAwait(false);
        }

        public static async Task<RendererResponse> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
        {
            var payload = await ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
            using var buffer = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(buffer, System.Text.Encoding.UTF8, leaveOpen: true);
            var success = reader.ReadBoolean();
            var message = reader.ReadString();
            var size = reader.ReadInt32();
            return new RendererResponse(success, message, reader.ReadBytes(size));
        }

        private static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
        {
            var length = BitConverter.GetBytes(payload.Length);
            await stream.WriteAsync(length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
        {
            var lengthBuffer = new byte[sizeof(int)];
            await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);
            var length = BitConverter.ToInt32(lengthBuffer, 0);
            var payload = new byte[length];
            await ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
            return payload;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken).ConfigureAwait(false);
                if (chunk == 0) throw new IOException("Kanał renderera został zamknięty.");
                read += chunk;
            }
        }
    }
}
