using PdfToolbox.Exceptions;
using PdfToolbox.Helpers;
using PdfToolbox.Services;

namespace PdfToolbox.Tests;

public sealed class FileServiceAndParserTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"PdfToolboxTests-{Guid.NewGuid():N}");
    private readonly FileService _service = new();

    public FileServiceAndParserTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void AvailableFilePath_ReturnsBaseName_WhenNoConflict() =>
        Assert.Equal(Path.Combine(_directory, "Dokument.pdf"), _service.GetAvailableFilePath(_directory, "Dokument"));

    [Fact]
    public void AvailableFilePath_AddsIncrementingSuffix()
    {
        File.WriteAllText(Path.Combine(_directory, "Dokument.pdf"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "Dokument_2.pdf"), string.Empty);
        Assert.Equal(Path.Combine(_directory, "Dokument_3.pdf"), _service.GetAvailableFilePath(_directory, "Dokument"));
    }

    [Fact]
    public void AvailableDirectoryPath_AddsIncrementingSuffix()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "Umowa"));
        Assert.Equal(Path.Combine(_directory, "Umowa_2"), _service.GetAvailableDirectoryPath(_directory, "Umowa"));
    }

    [Fact]
    public void DefaultOutputDirectory_UsesInputStem() =>
        Assert.Equal(Path.Combine(_directory, "Kadry_PDFToolbox"), _service.GetDefaultOutputDirectory(Path.Combine(_directory, "Kadry.pdf")));

    [Theory]
    [InlineData("1-3, 5, 8-10", new[] { 1, 2, 3, 5, 8, 9, 10 })]
    [InlineData("3, 1, 2, 2", new[] { 1, 2, 3 })]
    [InlineData("1–2", new[] { 1, 2 })]
    public void PageRangeParser_ParsesAndSorts(string text, int[] expected) =>
        Assert.Equal(expected, PageRangeParser.Parse(text, 10));

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1-11")]
    [InlineData("5-2")]
    [InlineData("abc")]
    public void PageRangeParser_RejectsInvalidRanges(string text) =>
        Assert.Throws<UserFacingException>(() => PageRangeParser.Parse(text, 10));

    [Fact]
    public void PageRangeParser_FormatsCompactRanges() =>
        Assert.Equal("1-3, 5, 8-10", PageRangeParser.Format([1, 2, 3, 5, 8, 9, 10]));

    [Fact]
    public void ValidateInputFile_RejectsNonPdf()
    {
        var path = Path.Combine(_directory, "notatki.txt");
        File.WriteAllText(path, "test");
        Assert.Throws<UserFacingException>(() => _service.ValidateInputFile(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
