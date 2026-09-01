namespace PdfToolbox.Controls;

internal static class Theme
{
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color TextPrimary = Color.FromArgb(30, 41, 59);
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color Canvas = Color.FromArgb(248, 250, 252);

    public static void ConfigurePrimaryButton(Button button, string text, int width = 230)
    {
        button.Text = text;
        button.Size = new Size(width, 46);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 10.5F);
        button.Cursor = Cursors.Hand;
    }

    public static void ConfigureSecondaryButton(Button button, string text, int width = 140)
    {
        button.Text = text;
        button.Size = new Size(width, 38);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = Color.White;
        button.ForeColor = TextPrimary;
        button.Cursor = Cursors.Hand;
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }
}
