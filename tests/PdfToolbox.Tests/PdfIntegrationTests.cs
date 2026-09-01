using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfToolbox.Exceptions;
using PdfToolbox.Models;
using PdfToolbox.Services;

namespace PdfToolbox.Tests;

public sealed class PdfIntegrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"PdfToolboxPdfTests-{Guid.NewGuid():N}");
    private readonly FileService _files = new();

    public PdfIntegrationTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task Split_HandlesSinglePage()
    {
        var input = CreatePdf("single.pdf", 1);
        var output = NewDirectory("split-one");
        var files = await new PdfSplitterService(_files).SplitAsync(input, output, null, CancellationToken.None);
        Assert.Single(files);
        Assert.Equal("single_001.pdf", Path.GetFileName(files[0]));
        Assert.Equal(1, GetPageCount(files[0]));
    }

    [Fact]
    public async Task Split_HandlesMoreThanOneHundredPages()
    {
        var input = CreatePdf("large.pdf", 105);
        var output = NewDirectory("split-large");
        var files = await new PdfSplitterService(_files).SplitAsync(input, output, null, CancellationToken.None);
        Assert.Equal(105, files.Count);
        Assert.Equal("large_105.pdf", Path.GetFileName(files[^1]));
    }

    [Fact]
    public async Task Merge_PreservesRequestedOrderAndPageCount()
    {
        var first = CreatePdf("first.pdf", 2);
        var second = CreatePdf("second.pdf", 3);
        var output = NewDirectory("merge");
        var result = await new PdfMergerService(_files).MergeAsync([first, second], output, "Razem.pdf", null, CancellationToken.None);
        Assert.Single(result.OutputFiles);
        Assert.Equal(5, GetPageCount(result.OutputFiles[0]));
        Assert.Equal("Razem.pdf", Path.GetFileName(result.OutputFiles[0]));
    }

    [Fact]
    public async Task Merge_RejectsEmptyList()
    {
        var output = NewDirectory("empty-merge");
        await Assert.ThrowsAsync<UserFacingException>(() =>
            new PdfMergerService(_files).MergeAsync([], output, "Razem", null, CancellationToken.None));
    }

    [Fact]
    public async Task Merge_DoesNotOverwriteExistingFile()
    {
        var input = CreatePdf("source.pdf", 1);
        var output = NewDirectory("merge-conflict");
        File.WriteAllText(Path.Combine(output, "Razem.pdf"), "keep");
        var result = await new PdfMergerService(_files).MergeAsync([input], output, "Razem", null, CancellationToken.None);
        Assert.Equal("Razem_2.pdf", Path.GetFileName(result.OutputFiles[0]));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(output, "Razem.pdf")));
    }

    [Fact]
    public async Task Extract_WritesOnlySelectedPages()
    {
        var info = await InspectAsync(CreatePdf("extract.pdf", 5));
        var output = NewDirectory("extract-output");
        var result = await new PdfPageService(_files).ExtractAsync(info, [1, 3, 5], output, null, CancellationToken.None);
        Assert.Equal(3, GetPageCount(result.OutputFiles[0]));
        Assert.Equal("extract_wybrane_strony.pdf", Path.GetFileName(result.OutputFiles[0]));
    }

    [Fact]
    public async Task Remove_WritesRemainingPages()
    {
        var info = await InspectAsync(CreatePdf("remove.pdf", 5));
        var output = NewDirectory("remove-output");
        var result = await new PdfPageService(_files).RemoveAsync(info, [2, 4], output, null, CancellationToken.None);
        Assert.Equal(3, GetPageCount(result.OutputFiles[0]));
    }

    [Fact]
    public async Task Remove_RejectsRemovingEveryPage()
    {
        var info = await InspectAsync(CreatePdf("remove-all.pdf", 2));
        var output = NewDirectory("remove-all-output");
        await Assert.ThrowsAsync<UserFacingException>(() =>
            new PdfPageService(_files).RemoveAsync(info, [1, 2], output, null, CancellationToken.None));
    }

    [Fact]
    public async Task Rotate_RotatesOnlySelectedPages()
    {
        var info = await InspectAsync(CreatePdf("rotate.pdf", 3));
        var output = NewDirectory("rotate-output");
        var result = await new PdfPageService(_files).RotateAsync(info, [2], RotationDirection.Right, output, null, CancellationToken.None);
        using var document = PdfReader.Open(result.OutputFiles[0], PdfDocumentOpenMode.Import);
        Assert.Equal(0, document.Pages[0].Rotate);
        Assert.Equal(90, document.Pages[1].Rotate);
        Assert.Equal(0, document.Pages[2].Rotate);
    }

    [Fact]
    public async Task BatchSplit_CreatesFolderPerDocument()
    {
        var first = await InspectAsync(CreatePdf("Umowa_A.pdf", 2));
        var second = await InspectAsync(CreatePdf("Umowa_B.pdf", 3));
        var output = NewDirectory("batch");
        var service = new PdfBatchSplitService(_files, new PdfSplitterService(_files));
        var result = await service.SplitManyAsync([first, second], output, null, null, CancellationToken.None);
        Assert.Equal(5, result.OutputFiles.Count);
        Assert.True(Directory.Exists(Path.Combine(output, "Umowa_A")));
        Assert.True(Directory.Exists(Path.Combine(output, "Umowa_B")));
    }

    [Fact]
    public async Task Validation_RejectsCorruptPdf()
    {
        var path = Path.Combine(_directory, "corrupt.pdf");
        File.WriteAllText(path, "not a PDF");
        await Assert.ThrowsAsync<UserFacingException>(() => new PdfValidationService(_files).InspectAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task Split_HonorsPreCancelledTokenAndCreatesNoFiles()
    {
        var input = CreatePdf("cancel.pdf", 10);
        var output = NewDirectory("cancel-output");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PdfSplitterService(_files).SplitAsync(input, output, null, cancellation.Token));
        Assert.Empty(Directory.GetFiles(output));
    }

    private Task<PdfDocumentInfo> InspectAsync(string path) =>
        new PdfValidationService(_files).InspectAsync(path, CancellationToken.None);

    private string CreatePdf(string name, int pages)
    {
        var path = Path.Combine(_directory, name);
        using var document = new PdfDocument();
        for (var index = 0; index < pages; index++) document.AddPage();
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
