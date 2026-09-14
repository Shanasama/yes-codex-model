using System.Drawing.Drawing2D;

namespace SkinStudio.Ui;

internal static class Painter
{
    public const TextFormatFlags SingleLine =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

    public const TextFormatFlags SingleLineCenter =
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
        TextFormatFlags.NoPadding;

    public const TextFormatFlags Wrap =
        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix |
        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;

    public static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var limit = Math.Min(rect.Width, rect.Height) / 2f;
        var r = Math.Max(0f, Math.Min(radius, limit));
        if (r <= 0.4f)
        {
            path.AddRectangle(rect);
            return path;
        }

        var d = r * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics graphics, RectangleF rect, float radius, Color color)
    {
        using var path = Rounded(rect, radius);
        using var brush = new SolidBrush(color);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(brush, path);
    }

    public static void DrawRounded(Graphics graphics, RectangleF rect, float radius, Color color, float width = 1f)
    {
        using var path = Rounded(rect, radius);
        using var pen = new Pen(color, width);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawPath(pen, path);
    }

    public static void Hairline(Graphics graphics, float x1, float x2, float y, Color color)
    {
        using var pen = new Pen(color, 1f);
        graphics.DrawLine(pen, x1, y + 0.5f, x2, y + 0.5f);
    }

    public static void DrawText(Graphics graphics, string text, Font font, Color color, Rectangle bounds,
        TextFormatFlags flags)
    {
        if (string.IsNullOrEmpty(text)) return;
        TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
    }

    public static Size MeasureText(string text, Font font)
    {
        if (string.IsNullOrEmpty(text)) return Size.Empty;
        return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    public static void DrawImageContained(Graphics graphics, Image image, RectangleF bounds)
    {
        var ratio = Math.Min(bounds.Width / image.Width, bounds.Height / image.Height);
        var width = image.Width * ratio;
        var height = image.Height * ratio;
        var target = new RectangleF(
            bounds.X + (bounds.Width - width) / 2f,
            bounds.Y + (bounds.Height - height) / 2f,
            width,
            height);
        graphics.InterpolationMode = ratio >= 2f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(image, target);
    }
}
