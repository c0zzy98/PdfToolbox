namespace PdfToolbox.Controls;

public sealed class OperationCard : UserControl
{
    private readonly Label _iconLabel = new();
    private readonly Label _titleLabel = new();
    private readonly Label _descriptionLabel = new();
    private bool _hovered;

    public OperationCard(string icon, string title, string description)
    {
        Size = new Size(286, 142);
        Margin = new Padding(8);
        Padding = new Padding(22, 17, 20, 14);
        BackColor = Color.White;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = title;

        _iconLabel.Text = icon;
        _iconLabel.Font = new Font("Segoe UI Symbol", 22F);
        _iconLabel.ForeColor = Theme.Accent;
        _iconLabel.AutoSize = true;
        _iconLabel.Location = new Point(20, 14);

        _titleLabel.Text = title;
        _titleLabel.Font = new Font("Segoe UI Semibold", 13F);
        _titleLabel.ForeColor = Theme.TextPrimary;
        _titleLabel.AutoSize = true;
        _titleLabel.Location = new Point(22, 59);

        _descriptionLabel.Text = description;
        _descriptionLabel.Font = new Font("Segoe UI", 10F);
        _descriptionLabel.ForeColor = Theme.TextMuted;
        _descriptionLabel.AutoSize = false;
        _descriptionLabel.Size = new Size(240, 42);
        _descriptionLabel.Location = new Point(22, 91);

        Controls.AddRange([_iconLabel, _titleLabel, _descriptionLabel]);
        foreach (Control control in Controls)
        {
            control.Cursor = Cursors.Hand;
            control.Click += (_, _) => OnClick(EventArgs.Empty);
            control.MouseEnter += (_, _) => SetHovered(true);
            control.MouseLeave += (_, _) => SetHovered(ClientRectangle.Contains(PointToClient(Cursor.Position)));
        }

        MouseEnter += (_, _) => SetHovered(true);
        MouseLeave += (_, _) => SetHovered(false);
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
        };
        Paint += PaintCard;
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
        BackColor = hovered ? Color.FromArgb(248, 251, 255) : Color.White;
        Invalidate();
    }

    private void PaintCard(object? sender, PaintEventArgs e)
    {
        var color = Focused ? Theme.Accent : _hovered ? Color.FromArgb(147, 197, 253) : Theme.Border;
        using var pen = new Pen(color, Focused ? 2 : 1);
        e.Graphics.DrawRoundedRectangle(pen, new Rectangle(1, 1, Width - 3, Height - 3), 12);
    }
}
