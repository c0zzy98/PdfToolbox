using PdfToolbox.Exceptions;
using PdfToolbox.Infrastructure.Logging;
using PdfToolbox.Models;
using PdfToolbox.Services;
using System.Diagnostics;

namespace PdfToolbox.Controls;

public sealed class RegionExtractionView : UserControl
{
    private readonly FileService _fileService;
    private readonly PdfValidationService _validationService;
    private readonly PdfPagePreviewService _previewService;
    private readonly PdfRegionExtractionService _extractionService;
    private readonly LocalLogger _logger;
    private readonly PdfDropZone _dropZone = new() { AllowMultiple = false, Margin = new Padding(0, 0, 0, 14) };
    private readonly Label _documentLabel = new() { AutoSize = true, ForeColor = Theme.TextPrimary, Margin = new Padding(0, 0, 0, 10) };
    private readonly ComboBox _pageCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Label _pageSummary = new() { AutoSize = true, ForeColor = Theme.TextPrimary, Margin = new Padding(14, 8, 14, 0) };
    private readonly Button _previousButton = new();
    private readonly Button _nextButton = new();
    private readonly CheckBox _fitToWindow = new() { Text = "Dopasuj do okna", Checked = true, AutoSize = true, Margin = new Padding(18, 8, 0, 0) };
    private readonly PdfRegionSelector _selector = new() { Margin = new Padding(0, 0, 0, 14), MinimumSize = new Size(0, 200) };
    private readonly Button _deleteSelectionButton = new();
    private readonly Button _clearPageButton = new();
    private readonly Label _selectionSummary = new() { AutoSize = true, ForeColor = Theme.TextMuted, Margin = new Padding(0, 10, 0, 0) };
    private readonly Label _outputLabel = new() { AutoEllipsis = true, Height = 42, Dock = DockStyle.Fill, Padding = new Padding(0, 9, 10, 0) };
    private readonly Button _outputButton = new();
    private readonly Button _actionButton = new();
    private readonly Label _previewStatus = new() { AutoSize = true, ForeColor = Theme.TextMuted, Margin = new Padding(0, 6, 0, 8) };
    private readonly ProgressView _progressView = new() { Margin = new Padding(0, 10, 0, 0) };
    private readonly Dictionary<int, List<PageRegionSelection>> _regionsByPage = [];
    private readonly Stopwatch _timer = new();
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _previewCancellation;
    private PdfDocumentInfo? _document;
    private int _currentPageNumber = 1;
    private int _previewRequestVersion;
    private RenderedPdfPage? _renderedPage;
    private string? _outputDirectory;

    public RegionExtractionView(
        FileService fileService,
        PdfValidationService validationService,
        PdfPagePreviewService previewService,
        PdfRegionExtractionService extractionService,
        LocalLogger logger)
    {
        _fileService = fileService;
        _validationService = validationService;
        _previewService = previewService;
        _extractionService = extractionService;
        _logger = logger;

        Dock = DockStyle.Fill;
        BackColor = Theme.Canvas;
        BuildLayout();
        HookEvents();
        UpdateSelectionSummary();
        UpdateNavigationState();
    }

    public event EventHandler<OperationResult>? Completed;

    public bool IsBusy => _operationCancellation is not null;

    public void RequestCancellation() => _operationCancellation?.Cancel();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewCancellation?.Cancel();
            _previewCancellation?.Dispose();
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _renderedPage?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        Theme.ConfigureSecondaryButton(_previousButton, "← Poprzednia", 132);
        Theme.ConfigureSecondaryButton(_nextButton, "Następna →", 132);
        Theme.ConfigureSecondaryButton(_deleteSelectionButton, "Usuń zaznaczenie", 150);
        Theme.ConfigureSecondaryButton(_clearPageButton, "Wyczyść stronę", 135);
        Theme.ConfigureSecondaryButton(_outputButton, "Wybierz folder", 145);
        Theme.ConfigurePrimaryButton(_actionButton, "WYTNIJ ZAZNACZONE FRAGMENTY", 320);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(0),
            BackColor = Theme.Canvas
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(root);
        root.Controls.Add(_dropZone, 0, 0);
        root.Controls.Add(_documentLabel, 0, 1);

        var nav = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) };
        nav.Controls.Add(_previousButton);
        nav.Controls.Add(_pageSummary);
        nav.Controls.Add(_nextButton);
        nav.Controls.Add(new Label { Text = "Strona:", AutoSize = true, ForeColor = Theme.TextPrimary, Margin = new Padding(18, 8, 6, 0) });
        nav.Controls.Add(_pageCombo);
        nav.Controls.Add(_fitToWindow);
        root.Controls.Add(nav, 0, 2);

        var selectorHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(0, 0, 0, 6) };
        selectorHost.Controls.Add(_selector);
        root.Controls.Add(selectorHost, 0, 3);

        var tools = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        tools.Controls.Add(_deleteSelectionButton);
        tools.Controls.Add(_clearPageButton);
        tools.Controls.Add(_selectionSummary);
        root.Controls.Add(tools, 0, 4);

        var outputPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _outputLabel.ForeColor = Theme.TextMuted;
        _outputLabel.Text = "Folder zostanie wybrany po dodaniu dokumentu";
        outputPanel.Controls.Add(_outputLabel, 0, 0);
        outputPanel.Controls.Add(_outputButton, 1, 0);
        root.Controls.Add(outputPanel, 0, 5);

        var footer = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1 };
        footer.Controls.Add(_previewStatus);
        footer.Controls.Add(_actionButton);
        footer.Controls.Add(_progressView);
        root.Controls.Add(footer, 0, 6);
    }

    private void HookEvents()
    {
        _dropZone.FilesSelected += async (_, paths) => await LoadDocumentAsync(paths[0]);
        _pageCombo.SelectedIndexChanged += async (_, _) =>
        {
            if (_pageCombo.SelectedIndex >= 0)
            {
                _currentPageNumber = _pageCombo.SelectedIndex + 1;
                await RenderCurrentPageAsync();
            }
        };
        _previousButton.Click += (_, _) =>
        {
            if (_document is null || _currentPageNumber <= 1) return;
            _currentPageNumber--;
            _pageCombo.SelectedIndex = _currentPageNumber - 1;
        };
        _nextButton.Click += (_, _) =>
        {
            if (_document is null || _currentPageNumber >= _document.PageCount) return;
            _currentPageNumber++;
            _pageCombo.SelectedIndex = _currentPageNumber - 1;
        };
        _fitToWindow.CheckedChanged += (_, _) =>
        {
            _selector.FitToWindow = _fitToWindow.Checked;
            _selector.Invalidate();
        };
        _selector.RegionsChanged += (_, _) => PersistCurrentPageRegions();
        _selector.SelectedRegionChanged += (_, _) => UpdateSelectionSummary();
        _deleteSelectionButton.Click += (_, _) => _selector.DeleteSelectedSelection();
        _clearPageButton.Click += (_, _) => _selector.ClearPageSelections();
        _outputButton.Click += (_, _) => ChooseOutputDirectory();
        _actionButton.Click += async (_, _) => await ExecuteAsync();
        _progressView.CancelRequested += (_, _) => _operationCancellation?.Cancel();
    }

    private async Task LoadDocumentAsync(string path)
    {
        _dropZone.Enabled = false;
        try
        {
            _document = await _validationService.InspectAsync(path, CancellationToken.None).ConfigureAwait(true);
            _regionsByPage.Clear();
            _currentPageNumber = 1;
            _dropZone.IsCompact = true;
            _outputDirectory = _fileService.GetDefaultOutputDirectory(_document.Path);
            _documentLabel.Text = $"✓  {_document.FileName} — {_document.PageCount} stron, {FormatSize(_document.SizeBytes)}";
            _outputLabel.Text = _outputDirectory;
            _pageCombo.Items.Clear();
            for (var page = 1; page <= _document.PageCount; page++) _pageCombo.Items.Add(page.ToString());
            _pageCombo.SelectedIndex = 0;
            UpdateNavigationState();
            await RenderCurrentPageAsync().ConfigureAwait(true);
        }
        catch (UserFacingException ex)
        {
            MessageBox.Show(FindForm(), ex.Message, "Nie można otworzyć dokumentu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _dropZone.Enabled = true;
        }
    }

    private async Task RenderCurrentPageAsync()
    {
        if (_document is null) return;
        var document = _document;
        var pageNumber = _currentPageNumber;
        var requestVersion = ++_previewRequestVersion;
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = new CancellationTokenSource();
        _previewStatus.Text = "Ładowanie podglądu strony…";
        try
        {
            var viewport = _selector.ClientSize.Width > 0 && _selector.ClientSize.Height > 0 ? _selector.ClientSize : new Size(900, 560);
            var rendered = await _previewService.RenderPreviewAsync(document, pageNumber, viewport, _previewCancellation.Token).ConfigureAwait(true);
            if (IsDisposed || requestVersion != _previewRequestVersion || !ReferenceEquals(document, _document) || pageNumber != _currentPageNumber)
            {
                rendered.Dispose();
                return;
            }

            _renderedPage?.Dispose();
            _renderedPage = rendered;
            // The selector owns and disposes this independent display bitmap.
            _selector.SetPage(pageNumber, new Bitmap(rendered.Bitmap), rendered.PageSizePoints, rendered.RotationDegrees, GetRegionsForCurrentPage());
            _previewStatus.Text = string.Empty;
            UpdateNavigationState();
        }
        catch (OperationCanceledException)
        {
        }
        catch (UserFacingException ex)
        {
            if (requestVersion == _previewRequestVersion)
            {
                // Keep the last valid bitmap visible when a transient renderer error occurs.
                _previewStatus.Text = ex.Message;
            }
        }
    }

    private async Task ExecuteAsync()
    {
        if (_document is null)
        {
            MessageBox.Show(FindForm(), "Najpierw wybierz dokument PDF.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_outputDirectory))
        {
            MessageBox.Show(FindForm(), "Wybierz folder wynikowy.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        PersistCurrentPageRegions();
        var regions = _regionsByPage.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value).ToArray();
        if (regions.Length == 0)
        {
            MessageBox.Show(FindForm(), "Najpierw zaznacz co najmniej jeden fragment strony.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_document.MayContainDigitalSignature)
        {
            var confirmation = MessageBox.Show(
                FindForm(),
                "Ten dokument może zawierać podpis elektroniczny.\n\nUtworzenie osobnych fragmentów zapisze nowe dokumenty PDF na podstawie rasteryzacji zaznaczonego obszaru. Oryginalny podpis nie przejdzie do wyników.\n\nOryginalny dokument pozostanie bez zmian.\n\nCzy chcesz kontynuować?",
                "Ważne — podpis elektroniczny",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.OK) return;
        }

        _operationCancellation = new CancellationTokenSource();
        _timer.Restart();
        SetBusy(true);
        _progressView.Start("Wycinanie zaznaczonych fragmentów");
        try
        {
            var result = await _extractionService.ExtractRegionsAsync(
                _document,
                regions,
                _outputDirectory,
                new Progress<ProcessingProgress>(_progressView.Report),
                _operationCancellation.Token).ConfigureAwait(true);
            Completed?.Invoke(this, result);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(FindForm(), "Operacja została anulowana.\n\nOryginalny dokument nie został zmieniony.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _progressView.Visible = false;
        }
        catch (UserFacingException ex)
        {
            await _logger.LogErrorAsync(OperationType.CropRegions.ToString(), ex.InnerException ?? ex, _timer.Elapsed).ConfigureAwait(true);
            MessageBox.Show(FindForm(), ex.Message, "Nie udało się ukończyć operacji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _progressView.Visible = false;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(OperationType.CropRegions.ToString(), ex, _timer.Elapsed).ConfigureAwait(true);
            MessageBox.Show(FindForm(), "Wystąpił nieoczekiwany błąd. Oryginalny dokument nie został zmieniony.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _progressView.Visible = false;
        }
        finally
        {
            _timer.Stop();
            _progressView.Stop();
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private void PersistCurrentPageRegions()
    {
        var current = _selector.Regions.Where(region => region.Width > 0D && region.Height > 0D).ToList();
        if (current.Count == 0) _regionsByPage.Remove(_currentPageNumber);
        else _regionsByPage[_currentPageNumber] = current;
        UpdateSelectionSummary();
    }

    private IReadOnlyCollection<PageRegionSelection> GetRegionsForCurrentPage() =>
        _regionsByPage.TryGetValue(_currentPageNumber, out var regions) ? regions : [];

    private void UpdateSelectionSummary()
    {
        var total = _regionsByPage.Values.Sum(list => list.Count);
        var current = GetRegionsForCurrentPage().Count;
        _selectionSummary.Text = total == 0
            ? "Brak zaznaczeń."
            : $"Ta strona: {current} zaznaczeń. Łącznie w dokumencie: {total}.";
        _deleteSelectionButton.Enabled = _selector.SelectedRegionIndex >= 0;
        _clearPageButton.Enabled = current > 0;
    }

    private void UpdateNavigationState()
    {
        if (_document is null)
        {
            _previousButton.Enabled = false;
            _nextButton.Enabled = false;
            _pageSummary.Text = "Strona 0 z 0";
            return;
        }

        _previousButton.Enabled = _currentPageNumber > 1;
        _nextButton.Enabled = _currentPageNumber < _document.PageCount;
        _pageSummary.Text = $"Strona {_currentPageNumber} z {_document.PageCount}";
    }

    private void ChooseOutputDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Wybierz folder dla nowych plików",
            UseDescriptionForTitle = true,
            SelectedPath = _outputDirectory ?? string.Empty
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        _outputDirectory = dialog.SelectedPath;
        _outputLabel.Text = _outputDirectory;
    }

    private void SetBusy(bool busy)
    {
        _dropZone.Enabled = !busy;
        _pageCombo.Enabled = !busy && _document is not null;
        _previousButton.Enabled = !busy && _document is not null && _currentPageNumber > 1;
        _nextButton.Enabled = !busy && _document is not null && _currentPageNumber < _document.PageCount;
        _selector.Enabled = !busy;
        _outputButton.Enabled = !busy;
        _actionButton.Enabled = !busy;
        _fitToWindow.Enabled = !busy;
    }

    private static string FormatSize(long bytes) => bytes < 1024 * 1024 ? $"{bytes / 1024d:N0} KB" : $"{bytes / 1024d / 1024d:N1} MB";
}
