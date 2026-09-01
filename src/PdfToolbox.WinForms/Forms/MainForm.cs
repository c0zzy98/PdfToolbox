using System.Diagnostics;
using PdfToolbox.Controls;
using PdfToolbox.Exceptions;
using PdfToolbox.Helpers;
using PdfToolbox.Infrastructure.Logging;
using PdfToolbox.Models;
using PdfToolbox.Services;

namespace PdfToolbox.Forms;

public sealed class MainForm : Form
{
    private readonly FileService _fileService;
    private readonly PdfValidationService _validation;
    private readonly PdfSplitterService _splitter;
    private readonly PdfMergerService _merger;
    private readonly PdfPageService _pageService;
    private readonly PdfBatchSplitService _batchSplitter;
    private readonly LocalLogger _logger;
    private readonly Panel _content = new();
    private readonly Button _backButton = new();
    private readonly Label _windowTitle = new();
    private readonly List<PdfDocumentInfo> _documents = [];
    private readonly List<string> _documentStatuses = [];
    private ListBox? _documentList;
    private CheckedListBox? _pageList;
    private TextBox? _rangeText;
    private TextBox? _mergeName;
    private ComboBox? _rotationCombo;
    private RadioButton? _allPagesRadio;
    private RadioButton? _selectedPagesRadio;
    private Label? _outputLabel;
    private Button? _actionButton;
    private ProgressView? _progressView;
    private PdfDropZone? _dropZone;
    private string? _outputDirectory;
    private OperationType? _currentOperation;
    private CancellationTokenSource? _cancellation;
    private int _draggedDocumentIndex = -1;

    public MainForm(
        FileService fileService,
        PdfValidationService validation,
        PdfSplitterService splitter,
        PdfMergerService merger,
        PdfPageService pageService,
        PdfBatchSplitService batchSplitter,
        LocalLogger logger)
    {
        _fileService = fileService;
        _validation = validation;
        _splitter = splitter;
        _merger = merger;
        _pageService = pageService;
        _batchSplitter = batchSplitter;
        _logger = logger;

        Text = "PDF Toolbox";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 700);
        ClientSize = new Size(900, 820);
        BackColor = Theme.Canvas;
        Font = new Font("Segoe UI", 10.5F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BuildChrome();
        ShowHome();
        FormClosing += (_, e) =>
        {
            if (_cancellation is null) return;
            _cancellation.Cancel();
            e.Cancel = true;
            MessageBox.Show(this, "Najpierw poczekaj na bezpieczne zatrzymanie operacji.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }

    private void BuildChrome()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.White, Padding = new Padding(28, 16, 28, 12) };
        _backButton.Text = "←";
        _backButton.Font = new Font("Segoe UI Symbol", 18F);
        _backButton.Size = new Size(44, 40);
        _backButton.FlatStyle = FlatStyle.Flat;
        _backButton.FlatAppearance.BorderSize = 0;
        _backButton.BackColor = Color.White;
        _backButton.ForeColor = Theme.TextPrimary;
        _backButton.Visible = false;
        _backButton.AccessibleName = "Wróć do początku";
        _backButton.Click += (_, _) => ShowHome();

        _windowTitle.Text = "PDF Toolbox";
        _windowTitle.Font = new Font("Segoe UI Semibold", 20F);
        _windowTitle.ForeColor = Theme.TextPrimary;
        _windowTitle.AutoSize = true;
        _windowTitle.Location = new Point(82, 19);

        var help = new Button
        {
            Text = "?",
            Font = new Font("Segoe UI Semibold", 13F),
            Size = new Size(40, 40),
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Theme.TextPrimary,
            AccessibleName = "Pomoc"
        };
        help.FlatAppearance.BorderColor = Theme.Border;
        help.Click += (_, _) => ShowHelp();
        header.Controls.AddRange([_backButton, _windowTitle, help]);

        _content.Dock = DockStyle.Fill;
        _content.AutoScroll = true;
        _content.BackColor = Theme.Canvas;
        Controls.Add(_content);
        Controls.Add(header);
    }

    private void ShowHome()
    {
        if (_cancellation is not null) return;
        ResetState();
        _backButton.Visible = false;
        _windowTitle.Location = new Point(30, 19);
        _content.Controls.Clear();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(48, 34, 48, 40),
            BackColor = Theme.Canvas
        };
        var heading = MakeHeading("Co chcesz zrobić?");
        heading.Margin = new Padding(8, 0, 0, 18);
        var cards = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 3, Margin = new Padding(0), Anchor = AnchorStyles.Top };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddCard(cards, 0, 0, "✂", "Podziel PDF", "Zapisz każdą stronę osobno", OperationType.Split);
        AddCard(cards, 1, 0, "▤", "Połącz PDF-y", "Połącz kilka plików w jeden", OperationType.Merge);
        AddCard(cards, 0, 1, "☑", "Wybierz strony", "Zapisz tylko potrzebne strony", OperationType.Extract);
        AddCard(cards, 1, 1, "⌫", "Usuń strony", "Utwórz kopię bez zbędnych stron", OperationType.Remove);
        AddCard(cards, 0, 2, "↻", "Obróć strony", "Popraw kierunek wybranych stron", OperationType.Rotate);
        AddCard(cards, 1, 2, "▦", "Podziel wiele PDF-ów", "Podziel kolejno kilka dokumentów", OperationType.BatchSplit);
        root.Controls.Add(heading);
        root.Controls.Add(cards);
        _content.Controls.Add(root);
    }

    private void AddCard(TableLayoutPanel parent, int column, int row, string icon, string title, string description, OperationType operation)
    {
        var card = new OperationCard(icon, title, description) { Anchor = AnchorStyles.None };
        card.Click += (_, _) => ShowWizard(operation);
        parent.Controls.Add(card, column, row);
    }

    private void ShowWizard(OperationType operation)
    {
        ResetState();
        _currentOperation = operation;
        _backButton.Visible = true;
        _windowTitle.Location = new Point(82, 19);
        _content.Controls.Clear();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(54, 28, 54, 44),
            BackColor = Theme.Canvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var (title, description, buttonText, multiple) = OperationCopy(operation);
        var heading = MakeHeading(title);
        var intro = new Label { Text = description, ForeColor = Theme.TextMuted, AutoSize = true, Margin = new Padding(0, 2, 0, 20) };
        _dropZone = new PdfDropZone { AllowMultiple = multiple, Margin = new Padding(0, 0, 0, 16) };
        _dropZone.FilesSelected += async (_, files) => await AddDocumentsAsync(files);
        root.Controls.Add(heading);
        root.Controls.Add(intro);
        root.Controls.Add(_dropZone);

        _documentList = new ListBox
        {
            Dock = DockStyle.Top,
            Height = multiple ? 125 : 72,
            Font = new Font("Segoe UI", 10.5F),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            AllowDrop = operation == OperationType.Merge,
            Margin = new Padding(0, 0, 0, 10)
        };
        ConfigureDocumentListDragAndDrop();
        root.Controls.Add(_documentList);

        if (operation == OperationType.Merge) root.Controls.Add(BuildMergeControls());
        if (operation is OperationType.Extract or OperationType.Remove) root.Controls.Add(BuildPageSelectionControls());
        if (operation == OperationType.Rotate) root.Controls.Add(BuildRotationControls());

        var outputPanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 12, 0, 18) };
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outputPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _outputLabel = new Label { Text = "Folder zostanie wybrany po dodaniu dokumentu", ForeColor = Theme.TextMuted, AutoEllipsis = true, Dock = DockStyle.Fill, Padding = new Padding(0, 9, 10, 0), AutoSize = false, Height = 42 };
        var folderButton = new Button();
        Theme.ConfigureSecondaryButton(folderButton, "Wybierz folder", 145);
        folderButton.Click += (_, _) => ChooseOutputDirectory();
        outputPanel.Controls.Add(_outputLabel, 0, 0);
        outputPanel.Controls.Add(folderButton, 1, 0);
        root.Controls.Add(outputPanel);

        _actionButton = new Button { Margin = new Padding(0, 0, 0, 18) };
        Theme.ConfigurePrimaryButton(_actionButton, buttonText, 260);
        _actionButton.Click += async (_, _) => await ExecuteAsync();
        root.Controls.Add(_actionButton);

        _progressView = new ProgressView { Margin = new Padding(0, 0, 0, 10) };
        _progressView.CancelRequested += (_, _) => _cancellation?.Cancel();
        root.Controls.Add(_progressView);
        _content.Controls.Add(root);
    }

    private Control BuildMergeControls()
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 0, 0, 8) };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) };
        var up = new Button();
        var down = new Button();
        var remove = new Button();
        Theme.ConfigureSecondaryButton(up, "Przenieś wyżej", 140);
        Theme.ConfigureSecondaryButton(down, "Przenieś niżej", 145);
        Theme.ConfigureSecondaryButton(remove, "Usuń z listy", 130);
        up.Click += (_, _) => MoveSelectedDocument(-1);
        down.Click += (_, _) => MoveSelectedDocument(1);
        remove.Click += (_, _) => RemoveSelectedDocument();
        buttons.Controls.AddRange([up, down, remove]);
        var nameLabel = MakeFieldLabel("Nazwa nowego dokumentu");
        _mergeName = new TextBox { Text = "Polaczone_dokumenty.pdf", Dock = DockStyle.Top, Height = 34, Margin = new Padding(0, 2, 0, 0) };
        panel.Controls.Add(buttons);
        panel.Controls.Add(nameLabel);
        panel.Controls.Add(_mergeName);
        return panel;
    }

    private Control BuildPageSelectionControls()
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 6, 0, 8) };
        panel.Controls.Add(MakeFieldLabel(_currentOperation == OperationType.Remove ? "Zaznacz strony do usunięcia" : "Zaznacz strony do zapisania"));
        _pageList = new CheckedListBox { Dock = DockStyle.Top, Height = 150, CheckOnClick = true, IntegralHeight = false, MultiColumn = true, ColumnWidth = 82, BorderStyle = BorderStyle.FixedSingle };
        _pageList.ItemCheck += (_, _) => BeginInvoke(SyncRangeFromChecks);
        panel.Controls.Add(_pageList);
        panel.Controls.Add(new Label { Text = "Możesz też wpisać zakres, np. 1-3, 5, 8-12", ForeColor = Theme.TextMuted, AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        var rangePanel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2 };
        rangePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rangePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _rangeText = new TextBox { Dock = DockStyle.Top, Height = 34, Margin = new Padding(0, 0, 8, 0) };
        var apply = new Button();
        Theme.ConfigureSecondaryButton(apply, "Zastosuj", 100);
        apply.Click += (_, _) => ApplyRangeToChecks();
        rangePanel.Controls.Add(_rangeText, 0, 0);
        rangePanel.Controls.Add(apply, 1, 0);
        panel.Controls.Add(rangePanel);
        return panel;
    }

    private Control BuildRotationControls()
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Margin = new Padding(0, 6, 0, 8) };
        panel.Controls.Add(MakeFieldLabel("Które strony obrócić?"));
        var radios = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        _allPagesRadio = new RadioButton { Text = "Wszystkie strony", Checked = true, AutoSize = true, Margin = new Padding(0, 4, 22, 6) };
        _selectedPagesRadio = new RadioButton { Text = "Wybrane strony", AutoSize = true, Margin = new Padding(0, 4, 0, 6) };
        _selectedPagesRadio.CheckedChanged += (_, _) => { if (_pageList is not null) _pageList.Enabled = _selectedPagesRadio.Checked; };
        radios.Controls.AddRange([_allPagesRadio, _selectedPagesRadio]);
        panel.Controls.Add(radios);
        _pageList = new CheckedListBox { Dock = DockStyle.Top, Height = 120, CheckOnClick = true, IntegralHeight = false, MultiColumn = true, ColumnWidth = 82, BorderStyle = BorderStyle.FixedSingle, Enabled = false };
        panel.Controls.Add(_pageList);
        panel.Controls.Add(MakeFieldLabel("Kierunek obrotu"));
        _rotationCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Margin = new Padding(0, 3, 0, 0) };
        _rotationCombo.Items.AddRange(["Obróć w prawo", "Obróć w lewo", "Odwróć"]);
        _rotationCombo.SelectedIndex = 0;
        panel.Controls.Add(_rotationCombo);
        return panel;
    }

    private async Task AddDocumentsAsync(IReadOnlyList<string> paths)
    {
        if (_dropZone is null) return;
        _dropZone.Enabled = false;
        try
        {
            foreach (var path in paths)
            {
                if (_documents.Any(document => string.Equals(document.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                var document = await _validation.InspectAsync(path, CancellationToken.None);
                if (_currentOperation is not OperationType.Merge and not OperationType.BatchSplit) _documents.Clear();
                _documents.Add(document);
                _documentStatuses.Add("Oczekuje");
                if (_currentOperation is not OperationType.Merge and not OperationType.BatchSplit) break;
            }

            RefreshDocumentList();
            if (_documents.Count > 0)
            {
                _outputDirectory ??= _fileService.GetDefaultOutputDirectory(_documents[0].Path);
                if (_outputLabel is not null) _outputLabel.Text = _outputDirectory;
                PopulatePageList();
            }
        }
        catch (UserFacingException ex)
        {
            MessageBox.Show(this, ex.Message, "Nie można dodać dokumentu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _dropZone.Enabled = true;
        }
    }

    private void PopulatePageList()
    {
        if (_pageList is null || _documents.Count == 0) return;
        _pageList.Items.Clear();
        for (var page = 1; page <= _documents[0].PageCount; page++) _pageList.Items.Add($"Strona {page}", false);
        if (_rangeText is not null) _rangeText.Text = string.Empty;
    }

    private void RefreshDocumentList()
    {
        if (_documentList is null) return;
        var selected = _documentList.SelectedIndex;
        _documentList.Items.Clear();
        for (var index = 0; index < _documents.Count; index++)
        {
            var document = _documents[index];
            var details = $"{document.PageCount} stron, {FormatSize(document.SizeBytes)}";
            var status = _currentOperation == OperationType.BatchSplit ? $" — {_documentStatuses.ElementAtOrDefault(index) ?? "Oczekuje"}" : string.Empty;
            _documentList.Items.Add($"✓  {document.FileName} — {details}{status}");
        }
        if (_documentList.Items.Count > 0) _documentList.SelectedIndex = Math.Clamp(selected, 0, _documentList.Items.Count - 1);
    }

    private async Task ExecuteAsync()
    {
        if (_currentOperation is null || _actionButton is null || _progressView is null) return;
        if (_documents.Count == 0)
        {
            MessageBox.Show(this, "Najpierw wybierz dokument PDF.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputDirectory))
        {
            MessageBox.Show(this, "Wybierz folder, w którym zapisać nowe pliki.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_documents.Any(document => document.MayContainDigitalSignature) && !ConfirmSignedDocument()) return;

        var selectedPages = GetSelectedPages();
        if (_currentOperation == OperationType.Remove)
        {
            if (selectedPages.Count == 0) { ShowPageSelectionRequired(); return; }
            var confirmation = MessageBox.Show(this,
                $"Z dokumentu zostaną usunięte {selectedPages.Count} strony.\n\nOryginalny plik pozostanie bez zmian.",
                "Potwierdź usunięcie stron", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
            if (confirmation != DialogResult.OK) return;
        }

        _cancellation = new CancellationTokenSource();
        SetBusy(true);
        _progressView.Start(OperationProgressHeading(_currentOperation.Value));
        var progress = new Progress<ProcessingProgress>(_progressView.Report);
        var timer = Stopwatch.StartNew();
        try
        {
            OperationResult result = _currentOperation switch
            {
                OperationType.Split => await SplitSingleAsync(progress, _cancellation.Token),
                OperationType.Merge => await _merger.MergeAsync(_documents.Select(d => d.Path).ToArray(), _outputDirectory, _mergeName?.Text ?? string.Empty, progress, _cancellation.Token),
                OperationType.Extract => selectedPages.Count == 0 ? throw new UserFacingException("Wybierz strony, które chcesz zapisać.") : await _pageService.ExtractAsync(_documents[0], selectedPages, _outputDirectory, progress, _cancellation.Token),
                OperationType.Remove => await _pageService.RemoveAsync(_documents[0], selectedPages, _outputDirectory, progress, _cancellation.Token),
                OperationType.Rotate => await RotateAsync(progress, _cancellation.Token),
                OperationType.BatchSplit => await _batchSplitter.SplitManyAsync(_documents, _outputDirectory, progress, UpdateBatchStatus, _cancellation.Token),
                _ => throw new ArgumentOutOfRangeException()
            };
            ShowSuccess(result);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(this, "Operacja została anulowana.\n\nOryginalne dokumenty nie zostały zmienione.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _progressView.Visible = false;
        }
        catch (UserFacingException ex)
        {
            await _logger.LogErrorAsync(_currentOperation.ToString() ?? "Unknown", ex.InnerException ?? ex, timer.Elapsed);
            MessageBox.Show(this, ex.Message, "Nie udało się ukończyć operacji", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _progressView.Visible = false;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(_currentOperation.ToString() ?? "Unknown", ex, timer.Elapsed);
            MessageBox.Show(this, "Wystąpił nieoczekiwany błąd. Oryginalne dokumenty nie zostały zmienione.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _progressView.Visible = false;
        }
        finally
        {
            _progressView.Stop();
            _cancellation.Dispose();
            _cancellation = null;
            SetBusy(false);
        }
    }

    private async Task<OperationResult> SplitSingleAsync(IProgress<ProcessingProgress> progress, CancellationToken cancellationToken)
    {
        _fileService.ValidateOutputDirectory(_outputDirectory!);
        _fileService.EnsureOutputDirectory(_outputDirectory!);
        var files = await _splitter.SplitAsync(_documents[0].Path, _outputDirectory!, progress, cancellationToken);
        return new OperationResult(_outputDirectory!, files, $"Dokument został podzielony na {files.Count} plików.");
    }

    private Task<OperationResult> RotateAsync(IProgress<ProcessingProgress> progress, CancellationToken cancellationToken)
    {
        var pages = _allPagesRadio?.Checked == true
            ? Enumerable.Range(1, _documents[0].PageCount).ToArray()
            : GetSelectedPages();
        if (pages.Count == 0) throw new UserFacingException("Wybierz strony, które chcesz obrócić.");
        var direction = _rotationCombo?.SelectedIndex switch
        {
            1 => RotationDirection.Left,
            2 => RotationDirection.UpsideDown,
            _ => RotationDirection.Right
        };
        return _pageService.RotateAsync(_documents[0], pages, direction, _outputDirectory!, progress, cancellationToken);
    }

    private IReadOnlyList<int> GetSelectedPages()
    {
        if (_pageList is null) return [];
        return _pageList.CheckedIndices.Cast<int>().Select(index => index + 1).ToArray();
    }

    private void ApplyRangeToChecks()
    {
        if (_pageList is null || _rangeText is null || _documents.Count == 0) return;
        try
        {
            var pages = PageRangeParser.Parse(_rangeText.Text, _documents[0].PageCount).ToHashSet();
            for (var index = 0; index < _pageList.Items.Count; index++) _pageList.SetItemChecked(index, pages.Contains(index + 1));
        }
        catch (UserFacingException ex)
        {
            MessageBox.Show(this, ex.Message, "Sprawdź numery stron", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SyncRangeFromChecks()
    {
        if (_rangeText is not null) _rangeText.Text = PageRangeParser.Format(GetSelectedPages());
    }

    private void MoveSelectedDocument(int offset)
    {
        if (_documentList is null) return;
        var current = _documentList.SelectedIndex;
        var target = current + offset;
        if (current < 0 || target < 0 || target >= _documents.Count) return;
        (_documents[current], _documents[target]) = (_documents[target], _documents[current]);
        (_documentStatuses[current], _documentStatuses[target]) = (_documentStatuses[target], _documentStatuses[current]);
        RefreshDocumentList();
        _documentList.SelectedIndex = target;
    }

    private void RemoveSelectedDocument()
    {
        if (_documentList?.SelectedIndex is not >= 0) return;
        var index = _documentList.SelectedIndex;
        _documents.RemoveAt(index);
        _documentStatuses.RemoveAt(index);
        RefreshDocumentList();
    }

    private void ConfigureDocumentListDragAndDrop()
    {
        if (_documentList is null || _currentOperation != OperationType.Merge) return;
        _documentList.MouseDown += (_, e) => _draggedDocumentIndex = _documentList.IndexFromPoint(e.Location);
        _documentList.MouseMove += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && _draggedDocumentIndex >= 0) _documentList.DoDragDrop(_draggedDocumentIndex, DragDropEffects.Move);
        };
        _documentList.DragOver += (_, e) => e.Effect = e.Data?.GetDataPresent(typeof(int)) == true ? DragDropEffects.Move : DragDropEffects.None;
        _documentList.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(typeof(int)) is not int source) return;
            var clientPoint = _documentList.PointToClient(new Point(e.X, e.Y));
            var target = _documentList.IndexFromPoint(clientPoint);
            if (target < 0) target = _documents.Count - 1;
            if (source == target || source < 0 || target < 0) return;
            var document = _documents[source];
            var status = _documentStatuses[source];
            _documents.RemoveAt(source);
            _documentStatuses.RemoveAt(source);
            _documents.Insert(target, document);
            _documentStatuses.Insert(target, status);
            RefreshDocumentList();
            _documentList.SelectedIndex = target;
        };
    }

    private void UpdateBatchStatus(int index, string status)
    {
        if (InvokeRequired) { BeginInvoke(() => UpdateBatchStatus(index, status)); return; }
        if (index >= 0 && index < _documentStatuses.Count) _documentStatuses[index] = status;
        RefreshDocumentList();
    }

    private void ShowSuccess(OperationResult result)
    {
        _content.Controls.Clear();
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(90, 70, 90, 50), ColumnCount = 1 };
        var icon = new Label { Text = "✓", Font = new Font("Segoe UI Semibold", 34F), ForeColor = Color.FromArgb(22, 163, 74), AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        var heading = MakeHeading("Gotowe");
        var summary = new Label { Text = result.Summary, ForeColor = Theme.TextPrimary, AutoSize = true, MaximumSize = new Size(620, 0), Margin = new Padding(0, 6, 0, 18) };
        var pathTitle = new Label { Text = "Pliki zapisano w:", ForeColor = Theme.TextMuted, AutoSize = true };
        var path = new Label { Text = result.OutputDirectory, ForeColor = Theme.TextPrimary, AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(0, 3, 0, 22) };
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var open = new Button();
        var again = new Button();
        Theme.ConfigurePrimaryButton(open, "Otwórz folder", 170);
        Theme.ConfigureSecondaryButton(again, "Wykonaj kolejną operację", 220);
        open.Click += (_, _) => OpenFolder(result.OutputDirectory);
        again.Click += (_, _) => ShowHome();
        buttons.Controls.AddRange([open, again]);
        panel.Controls.Add(icon);
        panel.Controls.Add(heading);
        panel.Controls.Add(summary);
        panel.Controls.Add(pathTitle);
        panel.Controls.Add(path);
        panel.Controls.Add(buttons);
        _content.Controls.Add(panel);
    }

    private bool ConfirmSignedDocument() => MessageBox.Show(this,
        "Ten dokument może zawierać podpis elektroniczny.\n\nZmiana dokumentu może spowodować, że podpis przestanie być ważny.\n\nOryginalny dokument nie zostanie zmieniony.\n\nCzy mimo to chcesz utworzyć zmodyfikowaną kopię?",
        "Ważne — podpis elektroniczny", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.OK;

    private void ShowHelp() => MessageBox.Show(this,
        "PDF Toolbox działa całkowicie lokalnie i zawsze tworzy nowe pliki.\n\nWażne\nZmiana podpisanego dokumentu PDF może spowodować utratę ważności podpisu elektronicznego.\n\nPDF Toolbox nie weryfikuje podpisów kryptograficznie, nie omija zabezpieczeń i nigdy nie zmienia oryginału.",
        "Pomoc i bezpieczeństwo", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ChooseOutputDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Wybierz folder dla nowych plików", UseDescriptionForTitle = true, SelectedPath = _outputDirectory ?? string.Empty };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _outputDirectory = dialog.SelectedPath;
        if (_outputLabel is not null) _outputLabel.Text = _outputDirectory;
    }

    private void SetBusy(bool busy)
    {
        if (_actionButton is not null) _actionButton.Enabled = !busy;
        if (_dropZone is not null) _dropZone.Enabled = !busy;
        _backButton.Enabled = !busy;
    }

    private void ShowPageSelectionRequired() => MessageBox.Show(this, "Wybierz co najmniej jedną stronę.", "PDF Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ResetState()
    {
        _documents.Clear();
        _documentStatuses.Clear();
        _documentList = null;
        _pageList = null;
        _rangeText = null;
        _mergeName = null;
        _rotationCombo = null;
        _allPagesRadio = null;
        _selectedPagesRadio = null;
        _outputLabel = null;
        _actionButton = null;
        _progressView = null;
        _dropZone = null;
        _outputDirectory = null;
        _currentOperation = null;
    }

    private static (string Title, string Description, string Action, bool Multiple) OperationCopy(OperationType operation) => operation switch
    {
        OperationType.Split => ("Podziel PDF", "Wybierz dokument. Każda strona zostanie zapisana jako osobny plik.", "PODZIEL DOKUMENT", false),
        OperationType.Merge => ("Połącz PDF-y", "Dodaj dokumenty i ustaw ich kolejność przed zapisaniem.", "POŁĄCZ DOKUMENTY", true),
        OperationType.Extract => ("Wybierz strony", "Zaznacz strony, które mają znaleźć się w nowym dokumencie.", "ZAPISZ WYBRANE STRONY", false),
        OperationType.Remove => ("Usuń strony", "Zaznacz zbędne strony. Oryginał pozostanie bez zmian.", "UTWÓRZ DOKUMENT BEZ STRON", false),
        OperationType.Rotate => ("Obróć strony", "Wybierz strony i kierunek obrotu.", "OBRÓĆ STRONY", false),
        OperationType.BatchSplit => ("Podziel wiele PDF-ów", "Dodaj kilka dokumentów. Zostaną podzielone kolejno i bezpiecznie.", "PODZIEL DOKUMENTY", true),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static string OperationProgressHeading(OperationType operation) => operation switch
    {
        OperationType.Split or OperationType.BatchSplit => "Dzielenie dokumentu",
        OperationType.Merge => "Łączenie dokumentów",
        OperationType.Extract => "Zapisywanie wybranych stron",
        OperationType.Remove => "Tworzenie dokumentu bez wybranych stron",
        OperationType.Rotate => "Obracanie stron",
        _ => "Praca nad dokumentem"
    };

    private static Label MakeHeading(string text) => new() { Text = text, Font = new Font("Segoe UI Semibold", 22F), ForeColor = Theme.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, 0, 3) };
    private static Label MakeFieldLabel(string text) => new() { Text = text, Font = new Font("Segoe UI Semibold", 10F), ForeColor = Theme.TextPrimary, AutoSize = true, Margin = new Padding(0, 6, 0, 3) };
    private static string FormatSize(long bytes) => bytes < 1024 * 1024 ? $"{bytes / 1024d:N0} KB" : $"{bytes / 1024d / 1024d:N1} MB";

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", ArgumentList = { path }, UseShellExecute = true });
    }
}
