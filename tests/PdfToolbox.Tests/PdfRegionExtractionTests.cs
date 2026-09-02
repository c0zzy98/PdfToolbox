using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfToolbox.Models;
using PdfToolbox.Services;

namespace PdfToolbox.Tests;

public sealed class PdfRegionExtractionTests : IDisposable
{
    static PdfRegionExtractionTests() => GlobalFontSettings.UseWindowsFontsUnderWindows = true;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"PdfToolboxRegionTests-{Guid.NewGuid():N}");
    private readonly FileService _files = new();
    private readonly PdfPagePreviewService _previewService = new();

    public PdfRegionExtractionTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task ExtractRegions_WritesSinglePdfForSingleSelection()
    {
        var input = CreateLabeledPdf("single-region.pdf", 1, addSecrets: false);
        var document = await InspectAsync(input);
        var output = NewDirectory("single-output");
        var service = new PdfRegionExtractionService(_files, _previewService);

        var result = await service.ExtractRegionsAsync(document, [new PageRegionSelection(1, 0.1, 0.1, 0.5, 0.3)], output, null, CancellationToken.None);

        Assert.Single(result.OutputFiles);
        Assert.Equal("single-region_strona_001_fragment_01.pdf", Path.GetFileName(result.OutputFiles[0]));
        Assert.Equal(1, GetPageCount(result.OutputFiles[0]));
    }

    [Fact]
    public async Task ExtractRegions_WritesSeparatePdfForEachSelection()
    {
        var input = CreateLabeledPdf("three-regions.pdf", 2, addSecrets: false);
        var document = await InspectAsync(input);
        var output = NewDirectory("three-output");
        var service = new PdfRegionExtractionService(_files, _previewService);
        var regions = new[]
        {
            new PageRegionSelection(1, 0.10, 0.10, 0.30, 0.20),
            new PageRegionSelection(1, 0.55, 0.10, 0.25, 0.20),
            new PageRegionSelection(2, 0.10, 0.55, 0.40, 0.25)
        };

        var result = await service.ExtractRegionsAsync(document, regions, output, null, CancellationToken.None);

        Assert.Equal(3, result.OutputFiles.Count);
        Assert.All(result.OutputFiles, path => Assert.Equal(1, GetPageCount(path)));
        Assert.Equal(2, GetPageCount(input));
    }

    [Fact]
    public async Task ExtractRegions_RasterizesOutputToAvoidHiddenTextLeak()
    {
        var input = CreateLabeledPdf("secrets.pdf", 1, addSecrets: true);
        var document = await InspectAsync(input);
        var output = NewDirectory("secrets-output");
        var service = new PdfRegionExtractionService(_files, _previewService);

        var result = await service.ExtractRegionsAsync(document, [new PageRegionSelection(1, 0.0, 0.0, 1.0, 0.48)], output, null, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(result.OutputFiles[0]);
        var text = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.DoesNotContain("TOP_SECRET_A", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TOP_SECRET_B", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractRegions_CleansUpPartialFilesWhenCancelled()
    {
        var input = CreateLabeledPdf("cancel-regions.pdf", 3, addSecrets: false);
        var document = await InspectAsync(input);
        var output = NewDirectory("cancel-output");
        var service = new PdfRegionExtractionService(_files, _previewService);
        using var cancellation = new CancellationTokenSource();
        var progress = new Progress<ProcessingProgress>(value =>
        {
            if (value.Percentage >= 34) cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExtractRegionsAsync(document,
            [
                new PageRegionSelection(1, 0.10, 0.10, 0.30, 0.20),
                new PageRegionSelection(2, 0.10, 0.10, 0.30, 0.20),
                new PageRegionSelection(3, 0.10, 0.10, 0.30, 0.20)
            ],
            output,
            progress,
            cancellation.Token));

        Assert.Empty(Directory.GetFiles(output, "*.partial-*", SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFiles(output, "*.pdf", SearchOption.TopDirectoryOnly));
    }

    private Task<PdfDocumentInfo> InspectAsync(string path) =>
        new PdfValidationService(_files).InspectAsync(path, CancellationToken.None);

    private string CreateLabeledPdf(string name, int pageCount, bool addSecrets)
    {
        var path = Path.Combine(_directory, name);
        using var document = new PdfDocument();
        for (var index = 0; index < pageCount; index++)
        {
            var page = document.AddPage();
            if (index == 1) page.Rotate = 90;
            if (index == 2) page.Rotate = 180;
            using var graphics = XGraphics.FromPdfPage(page);
            var titleFont = new XFont("Arial", 20);
            var bodyFont = new XFont("Arial", 18);
            graphics.DrawString($"Page {index + 1}", titleFont, XBrushes.Black, new XRect(40, 40, page.Width.Point - 80, 40), XStringFormats.TopLeft);
            graphics.DrawRectangle(XBrushes.LightBlue, 50, 120, page.Width.Point - 100, 180);
            graphics.DrawRectangle(XBrushes.LightGreen, 50, 340, page.Width.Point - 100, 180);
            graphics.DrawString(addSecrets ? "TOP_SECRET_A" : $"REGION_A_{index + 1}", bodyFont, XBrushes.Black, new XRect(70, 170, page.Width.Point - 140, 40), XStringFormats.TopLeft);
            graphics.DrawString(addSecrets ? "TOP_SECRET_B" : $"REGION_B_{index + 1}", bodyFont, XBrushes.Black, new XRect(70, 390, page.Width.Point - 140, 40), XStringFormats.TopLeft);
        }

        document.Save(path);
        return path;
    }

    private string NewDirectory(string name)
    {
        var path = Path.Combine(_directory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static int GetPageCount(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
