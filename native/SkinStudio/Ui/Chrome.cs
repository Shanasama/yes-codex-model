using System.Drawing.Drawing2D;

namespace SkinStudio.Ui;

internal sealed class BrandMark : SkinControl
{
    public BrandMark()
    {
        BackColor = Theme.Chrome;
        TabStop = false;
        Size = new Size(26, 26);
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Painter.Rounded(rect, 8f);
        using var brush = new LinearGradientBrush(rect,
            Color.FromArgb(42, 143, 108), Color.FromArgb(24, 104, 78), LinearGradientMode.ForwardDiagonal);
        graphics.FillPath(brush, path);
        Glyphs.Draw(graphics, Glyph.Paw, new RectangleF(6.5f, 6.5f, Width - 13f, Height - 13f), Color.White, 1.3f);
    }
}

internal enum WindowAction { Minimize, Maximize, Close }

internal sealed class WindowButton : SkinControl
{
    private bool _hover;
    private bool _press;

    public WindowButton()
    {
        BackColor = Theme.Chrome;
        Cursor = Cursors.Hand;
        TabStop = false;
        Size = new Size(46, Theme.ChromeHeight);
        SetStyle(ControlStyles.Selectable, false);
    }

    public WindowAction Action { get; set; }
    public bool Maximized { get; set; }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var isClose = Action == WindowAction.Close;
        Color background;
        if (isClose) background = _press ? Color.FromArgb(198, 52, 42) : _hover ? Color.FromArgb(229, 86, 72) : Color.Transparent;
        else background = _press ? Color.FromArgb(236, 239, 235) : _hover ? Color.FromArgb(244, 246, 243) : Color.Transparent;

        if (background.A > 0)
        {
            using var brush = new SolidBrush(background);
            graphics.FillRectangle(brush, ClientRectangle);
        }

        var color = isClose && (_hover || _press) ? Color.White : Theme.InkSoft;
        var icon = Action switch
        {
            WindowAction.Minimize => Glyph.Minimize,
            WindowAction.Maximize => Maximized ? Glyph.Restore : Glyph.Maximize,
            _ => Glyph.Close
        };
        var size = 17f;
        Glyphs.Draw(graphics, icon, new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size), color, 1.35f);
    }
}
