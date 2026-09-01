using PdfToolbox.Forms;
using PdfToolbox.Infrastructure.Logging;
using PdfToolbox.Services;

namespace PdfToolbox;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var fileService = new FileService();
        var splitter = new PdfSplitterService(fileService);
        var validation = new PdfValidationService(fileService);
        var merger = new PdfMergerService(fileService);
        var pageService = new PdfPageService(fileService);
        var batchSplitter = new PdfBatchSplitService(fileService, splitter);
        Application.Run(new MainForm(fileService, validation, splitter, merger, pageService, batchSplitter, new LocalLogger()));
    }
}
