using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using PDFiumCore;

namespace PdfToolbox.Renderer;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        var pipeName = args.Length == 2 && args[0] == "--pipe" ? args[1] : null;
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return 1;
        }

        fpdfview.FPDF_InitLibrary();
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000).ConfigureAwait(false);
            var request = await RendererProtocol.ReadRequestAsync(pipe).ConfigureAwait(false);
            var response = Render(request);
            await RendererProtocol.WriteResponseAsync(pipe, response).ConfigureAwait(false);
            return response.Success ? 0 : 2;
        }
        catch
        {
            return 2;
        }
        finally
        {
            fpdfview.FPDF_DestroyLibrary();
        }
    }

    private static RenderResponse Render(RenderRequest request)
    {
        if (request.PageNumber < 1 || request.TargetWidth < 1 || request.TargetHeight < 1)
        {
            return RenderResponse.Fail("Nieprawidłowe żądanie renderowania.");
        }

        FpdfDocumentT? document = null;
        FpdfPageT? page = null;
        FpdfBitmapT? bitmap = null;

        try
        {
            document = fpdfview.FPDF_LoadDocument(request.InputPath, null);
            if (document is null)
            {
                return RenderResponse.Fail("Nie udało się otworzyć dokumentu do renderowania.");
            }

            page = fpdfview.FPDF_LoadPage(document, request.PageNumber - 1);
            if (page is null)
            {
                return RenderResponse.Fail("Nie udało się otworzyć strony do renderowania.");
            }

            bitmap = fpdfview.FPDFBitmapCreateEx(request.TargetWidth, request.TargetHeight, (int)FPDFBitmapFormat.BGRA, IntPtr.Zero, 0);
            if (bitmap is null)
            {
                return RenderResponse.Fail("Nie udało się utworzyć bufora renderowania.");
            }

            fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, request.TargetWidth, request.TargetHeight, uint.MaxValue);

            var pageWidth = fpdfview.FPDF_GetPageWidthF(page);
            var pageHeight = fpdfview.FPDF_GetPageHeightF(page);
            using var matrix = new FS_MATRIX_();
            using var clipping = new FS_RECTF_();
            matrix.A = request.TargetWidth / pageWidth;
            matrix.D = request.TargetHeight / pageHeight;
            clipping.Left = 0;
            clipping.Right = request.TargetWidth;
            clipping.Bottom = 0;
            clipping.Top = request.TargetHeight;
            fpdfview.FPDF_RenderPageBitmapWithMatrix(bitmap, page, matrix, clipping, (int)RenderFlags.RenderAnnotations);

            var stride = fpdfview.FPDFBitmapGetStride(bitmap);
            var buffer = fpdfview.FPDFBitmapGetBuffer(bitmap);
            var size = stride * request.TargetHeight;
            var bytes = new byte[size];
            Marshal.Copy(buffer, bytes, 0, size);

            using var image = new Bitmap(request.TargetWidth, request.TargetHeight, PixelFormat.Format32bppArgb);
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally
            {
                image.UnlockBits(data);
            }

            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            return RenderResponse.Ok(stream.ToArray());
        }
        catch
        {
            return RenderResponse.Fail("Nie udało się wyrenderować strony.");
        }
        finally
        {
            if (bitmap is not null) fpdfview.FPDFBitmapDestroy(bitmap);
            if (page is not null) fpdfview.FPDF_ClosePage(page);
            if (document is not null) fpdfview.FPDF_CloseDocument(document);
        }
    }

    private sealed record RenderRequest(string InputPath, int PageNumber, int TargetWidth, int TargetHeight)
    {
    }

    private sealed record RenderResponse(bool Success, string ErrorMessage, byte[] PngBytes)
    {
        public static RenderResponse Ok(byte[] pngBytes) => new(true, string.Empty, pngBytes);
        public static RenderResponse Fail(string message) => new(false, message, []);
    }

    private static class RendererProtocol
    {
        public static async Task<RenderRequest> ReadRequestAsync(Stream stream)
        {
            var payload = await ReadFrameAsync(stream).ConfigureAwait(false);
            using var buffer = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(buffer, System.Text.Encoding.UTF8, leaveOpen: true);
            return new RenderRequest(reader.ReadString(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static async Task WriteResponseAsync(Stream stream, RenderResponse response)
        {
            using var buffer = new MemoryStream();
            using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(response.Success);
                writer.Write(response.ErrorMessage);
                writer.Write(response.PngBytes.Length);
                writer.Write(response.PngBytes);
            }

            await WriteFrameAsync(stream, buffer.ToArray()).ConfigureAwait(false);
        }

        private static async Task WriteFrameAsync(Stream stream, byte[] payload)
        {
            await stream.WriteAsync(BitConverter.GetBytes(payload.Length)).ConfigureAwait(false);
            await stream.WriteAsync(payload).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadFrameAsync(Stream stream)
        {
            var lengthBuffer = new byte[sizeof(int)];
            await ReadExactAsync(stream, lengthBuffer).ConfigureAwait(false);
            var length = BitConverter.ToInt32(lengthBuffer, 0);
            var payload = new byte[length];
            await ReadExactAsync(stream, payload).ConfigureAwait(false);
            return payload;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var chunk = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read)).ConfigureAwait(false);
                if (chunk == 0) throw new IOException("Pipe closed.");
                read += chunk;
            }
        }
    }
}
