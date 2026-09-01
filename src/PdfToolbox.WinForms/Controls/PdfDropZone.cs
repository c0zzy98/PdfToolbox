namespace PdfToolbox.Controls;

public sealed class PdfDropZone : UserControl
{
    private readonly Label _title = new();
    private readonly Label _or = new();
    private readonly Button _chooseButton = new();

    public PdfDropZone()
    {
        Height = 160;
        Dock = DockStyle.Top;
        BackColor = Color.White;
        AllowDrop = true;
        TabStop = true;

        _title.Text = "Przeciągnij PDF tutaj";
        _title.Font = new Font("Segoe UI Semibold", 14F);
        _title.ForeColor = Theme.TextPrimary;
        _title.AutoSize = true;
        _or.Text = "lub";
        _or.ForeColor = Theme.TextMuted;
        _or.AutoSize = true;
        Theme.ConfigureSecondaryButton(_chooseButton, "Wybierz dokument", 170);

        Controls.AddRange([_title, _or, _chooseButton]);
        Resize += (_, _) => LayoutChildren();
        Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(147, 197, 253), 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            e.Graphics.DrawRoundedRectangle(pen, new Rectangle(2, 2, Width - 5, Height - 5), 12);
        };
        DragEnter += HandleDragEnter;
        DragDrop += HandleDragDrop;
        _chooseButton.Click += (_, _) => ChooseFiles();
    }

    public bool AllowMultiple { get; set; }
    public event EventHandler<IReadOnlyList<string>>? FilesSelected;

    private void ChooseFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Dokumenty PDF (*.pdf)|*.pdf",
            CheckFileExists = true,
            Multiselect = AllowMultiple,
            Title = AllowMultiple ? "Wybierz dokumenty PDF" : "Wybierz dokument PDF"
        };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) FilesSelected?.Invoke(this, dialog.FileNames);
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = GetPdfFiles(e.Data).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void HandleDragDrop(object? sender, DragEventArgs e)
    {
        var files = GetPdfFiles(e.Data);
        if (!AllowMultiple && files.Count > 1) files = [files[0]];
        if (files.Count > 0) FilesSelected?.Invoke(this, files);
    }

    private static List<string> GetPdfFiles(IDataObject? data) =>
        data?.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)).ToList()
            : [];

    private void LayoutChildren()
    {
        _title.Location = new Point((Width - _title.Width) / 2, 26);
        _or.Location = new Point((Width - _or.Width) / 2, 67);
        _chooseButton.Location = new Point((Width - _chooseButton.Width) / 2, 91);
    }
}
