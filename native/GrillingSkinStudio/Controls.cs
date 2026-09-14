using System.Drawing.Drawing2D;

namespace GrillingSkinStudio;

internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(33, 37, 34);
    public static readonly Color Muted = Color.FromArgb(100, 108, 101);
    public static readonly Color Canvas = Color.FromArgb(246, 245, 241);
    public static readonly Color Surface = Color.FromArgb(255, 255, 253);
    public static readonly Color SurfaceSoft = Color.FromArgb(239, 239, 234);
    public static readonly Color Sidebar = Color.FromArgb(24, 28, 25);
    public static readonly Color SidebarSoft = Color.FromArgb(39, 45, 40);
    public static readonly Color Stage = Color.FromArgb(37, 42, 38);
    public static readonly Color Line = Color.FromArgb(217, 220, 215);
    public static readonly Color Accent = Color.FromArgb(218, 77, 43);
    public static readonly Color AccentHover = Color.FromArgb(194, 61, 31);
    public static readonly Color AccentSoft = Color.FromArgb(255, 235, 227);
    public static readonly Color Success = Color.FromArgb(42, 132, 79);
}

internal enum ButtonVariant { Primary, Secondary, Ghost, DarkGhost }

internal sealed class StudioButton : Button
{
    private bool _hovered;
    private bool _pressed;
    public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;
    public int Radius { get; set; } = 7;

    public StudioButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = DrawUtil.RoundRect(bounds, Radius);
        var (fill, ink, border) = Palette();
        using var fillBrush = new SolidBrush(fill);
        e.Graphics.FillPath(fillBrush, path);
        if (border != Color.Transparent)
        {
            using var pen = new Pen(border);
            e.Graphics.DrawPath(pen, path);
        }
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Variant == ButtonVariant.Primary ? Color.White : Theme.Accent) { DashStyle = DashStyle.Dot };
            using var focusPath = DrawUtil.RoundRect(Rectangle.Inflate(bounds, -3, -3), Math.Max(3, Radius - 3));
            e.Graphics.DrawPath(focus, focusPath);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private (Color fill, Color ink, Color border) Palette()
    {
        if (!Enabled) return (Color.FromArgb(225, 226, 222), Color.FromArgb(145, 149, 145), Color.Transparent);
        return Variant switch
        {
            ButtonVariant.Primary => (_pressed ? Color.FromArgb(174, 51, 25) : _hovered ? Theme.AccentHover : Theme.Accent, Color.White, Color.Transparent),
            ButtonVariant.Ghost => (_pressed ? Color.FromArgb(227, 228, 224) : _hovered ? Theme.SurfaceSoft : Color.Transparent, Theme.Ink, Color.Transparent),
            ButtonVariant.DarkGhost => (_pressed ? Color.FromArgb(60, 68, 61) : _hovered ? Theme.SidebarSoft : Color.Transparent, Color.White, Color.FromArgb(70, 78, 71)),
            _ => (_pressed ? Color.FromArgb(229, 230, 226) : _hovered ? Theme.SurfaceSoft : Theme.Surface, Theme.Ink, Theme.Line)
        };
    }
}

internal class SurfacePanel : Panel
{
    public int Radius { get; set; } = 10;
    public Color BorderColor { get; set; } = Theme.Line;
    public bool DrawBorder { get; set; } = true;

    public SurfacePanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        Resize += (_, _) => UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = DrawUtil.RoundRect(bounds, Radius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        if (DrawBorder)
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawPath(pen, path);
        }
        base.OnPaint(e);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using var path = DrawUtil.RoundRect(new Rectangle(0, 0, Width, Height), Radius);
        Region = new Region(path);
    }
}

internal sealed class BrandMark : Control
{
    public BrandMark() { Size = new Size(38, 38); DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var baseBrush = new SolidBrush(Theme.Accent);
        e.Graphics.FillEllipse(baseBrush, 1, 1, 36, 36);
        using var white = new Pen(Color.White, 2.4F) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawArc(white, 10, 12, 18, 14, 195, 150);
        e.Graphics.DrawLine(white, 13, 24, 18, 28);
        e.Graphics.DrawLine(white, 25, 24, 20, 28);
        using var eye = new SolidBrush(Color.White);
        e.Graphics.FillEllipse(eye, 13, 17, 2.8F, 2.8F);
        e.Graphics.FillEllipse(eye, 22, 17, 2.8F, 2.8F);
    }
}

internal sealed class PreviewStage : SurfacePanel
{
    public PreviewStage()
    {
        BackColor = Theme.Stage;
        BorderColor = Color.FromArgb(55, 62, 56);
        Radius = 12;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var line = new Pen(Color.FromArgb(50, 255, 255, 255));
        e.Graphics.DrawLine(line, 174, 22, 174, Height - 22);
        var stageBounds = new Rectangle(Width / 2 - 115, Height - 54, 230, 24);
        using var stageBrush = new SolidBrush(Color.FromArgb(22, 0, 0, 0));
        e.Graphics.FillEllipse(stageBrush, stageBounds);
    }
}

internal static class DrawUtil
{
    public static GraphicsPath RoundRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2);
        if (diameter <= 1) { path.AddRectangle(bounds); return path; }
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter; path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter; path.AddArc(arc, 0, 90);
        arc.X = bounds.Left; path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
