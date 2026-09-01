using PdfToolbox.Models;

namespace PdfToolbox.Controls;

public sealed class ProgressView : UserControl
{
    private readonly Label _heading = new();
    private readonly Label _status = new();
    private readonly ProgressBar _bar = new();
    private readonly Button _cancel = new();

    public ProgressView()
    {
        Dock = DockStyle.Top;
        Height = 150;
        BackColor = Color.White;
        Padding = new Padding(20);
        Visible = false;
        _heading.Text = "Praca nad dokumentem";
        _heading.Font = new Font("Segoe UI Semibold", 13F);
        _heading.ForeColor = Theme.TextPrimary;
        _heading.AutoSize = true;
        _heading.Location = new Point(20, 16);
        _status.Text = "Rozpoczynanie…";
        _status.ForeColor = Theme.TextMuted;
        _status.AutoSize = true;
        _status.Location = new Point(20, 48);
        _bar.Location = new Point(20, 76);
        _bar.Size = new Size(470, 18);
        _bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Theme.ConfigureSecondaryButton(_cancel, "Anuluj", 100);
        _cancel.Location = new Point(20, 106);
        _cancel.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        Controls.AddRange([_heading, _status, _bar, _cancel]);
    }

    public event EventHandler? CancelRequested;

    public void Start(string heading)
    {
        _heading.Text = heading;
        _status.Text = "Rozpoczynanie…";
        _bar.Value = 0;
        _cancel.Enabled = true;
        Visible = true;
    }

    public void Report(ProcessingProgress progress)
    {
        _bar.Value = Math.Clamp(progress.Percentage, 0, 100);
        _status.Text = $"{progress.Message} — {_bar.Value}%";
    }

    public void Stop() => _cancel.Enabled = false;
}
