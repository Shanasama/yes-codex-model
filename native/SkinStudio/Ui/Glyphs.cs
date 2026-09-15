using System.Drawing.Drawing2D;

namespace SkinStudio.Ui;

internal enum Glyph
{
    None,
    Play,
    Pause,
    Fit,
    Refresh,
    Folder,
    Search,
    Close,
    Minimize,
    Maximize,
    Restore,
    ChevronDown,
    ChevronRight,
    Import,
    Export,
    Copy,
    Check,
    Warning,
    Info,
    Paw,
    Plus,
    Trash
}

/// <summary>矢量图标：全部按 16×16 视盒描述，绘制时等比缩放到目标方框。</summary>
internal static class Glyphs
{
    public static void Draw(Graphics graphics, Glyph icon, RectangleF box, Color color, float stroke = 1.6f)
    {
        if (icon == Glyph.None) return;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var scale = Math.Min(box.Width, box.Height) / 16f;
        var ox = box.X + (box.Width - 16f * scale) / 2f;
        var oy = box.Y + (box.Height - 16f * scale) / 2f;
        float X(float value) => ox + value * scale;
        float Y(float value) => oy + value * scale;
        PointF P(float x, float y) => new(X(x), Y(y));

        using var pen = new Pen(color, Math.Max(1f, stroke * scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        switch (icon)
        {
            case Glyph.Play:
                graphics.FillPolygon(brush, new[] { P(5.5f, 3.4f), P(12.8f, 8f), P(5.5f, 12.6f) });
                break;

            case Glyph.Pause:
                graphics.FillRectangle(brush, X(5f), Y(3.6f), 2.1f * scale, 8.8f * scale);
                graphics.FillRectangle(brush, X(8.9f), Y(3.6f), 2.1f * scale, 8.8f * scale);
                break;

            case Glyph.Fit:
                graphics.DrawLines(pen, new[] { P(2.8f, 6.2f), P(2.8f, 2.8f), P(6.2f, 2.8f) });
                graphics.DrawLines(pen, new[] { P(9.8f, 2.8f), P(13.2f, 2.8f), P(13.2f, 6.2f) });
                graphics.DrawLines(pen, new[] { P(13.2f, 9.8f), P(13.2f, 13.2f), P(9.8f, 13.2f) });
                graphics.DrawLines(pen, new[] { P(6.2f, 13.2f), P(2.8f, 13.2f), P(2.8f, 9.8f) });
                break;

            case Glyph.Refresh:
                graphics.DrawArc(pen, X(2.6f), Y(2.6f), 10.8f * scale, 10.8f * scale, 40f, 265f);
                graphics.FillPolygon(brush, new[]
                {
                    P(11.6f, 1.6f), P(14.4f, 4.6f), P(10.6f, 5.4f)
                });
                break;

            case Glyph.Folder:
                using (var folder = new GraphicsPath())
                {
                    folder.AddLines(new[]
                    {
                        P(2.4f, 5.2f), P(6.2f, 5.2f), P(7.4f, 7f), P(13.6f, 7f),
                        P(13.6f, 12.8f), P(2.4f, 12.8f)
                    });
                    folder.CloseFigure();
                    graphics.DrawPath(pen, folder);
                }
                break;

            case Glyph.Search:
                graphics.DrawEllipse(pen, X(3f), Y(3f), 7.6f * scale, 7.6f * scale);
                graphics.DrawLine(pen, P(10.2f, 10.2f), P(13.4f, 13.4f));
                break;

            case Glyph.Close:
                graphics.DrawLine(pen, P(4.4f, 4.4f), P(11.6f, 11.6f));
                graphics.DrawLine(pen, P(11.6f, 4.4f), P(4.4f, 11.6f));
                break;

            case Glyph.Minimize:
                graphics.DrawLine(pen, P(4f, 8f), P(12f, 8f));
                break;

            case Glyph.Maximize:
                using (var path = Painter.Rounded(new RectangleF(X(4f), Y(4f), 8f * scale, 8f * scale), 1.6f * scale))
                {
                    graphics.DrawPath(pen, path);
                }
                break;

            case Glyph.Restore:
                using (var back = Painter.Rounded(new RectangleF(X(3.2f), Y(5.4f), 7.4f * scale, 7.4f * scale), 1.4f * scale))
                {
                    graphics.DrawPath(pen, back);
                }
                graphics.DrawLine(pen, P(5.6f, 5.2f), P(5.6f, 3.4f));
                graphics.DrawLine(pen, P(5.6f, 3.4f), P(12.6f, 3.4f));
                graphics.DrawLine(pen, P(12.6f, 3.4f), P(12.6f, 10.2f));
                graphics.DrawLine(pen, P(12.6f, 10.2f), P(10.8f, 10.2f));
                break;

            case Glyph.ChevronDown:
                graphics.DrawLines(pen, new[] { P(4.6f, 6.4f), P(8f, 9.8f), P(11.4f, 6.4f) });
                break;

            case Glyph.ChevronRight:
                graphics.DrawLines(pen, new[] { P(6.4f, 4.6f), P(9.8f, 8f), P(6.4f, 11.4f) });
                break;

            case Glyph.Import:
                graphics.DrawLine(pen, P(8f, 2.8f), P(8f, 9.6f));
                graphics.DrawLines(pen, new[] { P(5.2f, 6.8f), P(8f, 9.8f), P(10.8f, 6.8f) });
                graphics.DrawLines(pen, new[] { P(3.2f, 11f), P(3.2f, 13.2f), P(12.8f, 13.2f), P(12.8f, 11f) });
                break;

            case Glyph.Export:
                graphics.DrawLine(pen, P(8f, 9.8f), P(8f, 3f));
                graphics.DrawLines(pen, new[] { P(5.2f, 5.8f), P(8f, 2.8f), P(10.8f, 5.8f) });
                graphics.DrawLines(pen, new[] { P(3.2f, 11f), P(3.2f, 13.2f), P(12.8f, 13.2f), P(12.8f, 11f) });
                break;

            case Glyph.Copy:
                using (var front = Painter.Rounded(new RectangleF(X(2.8f), Y(2.8f), 7.2f * scale, 7.2f * scale), 1.8f * scale))
                {
                    graphics.DrawPath(pen, front);
                }
                using (var back = Painter.Rounded(new RectangleF(X(6.8f), Y(6.8f), 6.4f * scale, 6.4f * scale), 1.8f * scale))
                {
                    graphics.DrawPath(pen, back);
                }
                break;

            case Glyph.Check:
                graphics.DrawLines(pen, new[] { P(3.4f, 8.4f), P(6.6f, 11.6f), P(12.6f, 4.6f) });
                break;

            case Glyph.Warning:
                using (var triangle = new GraphicsPath())
                {
                    triangle.AddLines(new[] { P(8f, 2.4f), P(14f, 13f), P(2f, 13f) });
                    triangle.CloseFigure();
                    graphics.DrawPath(pen, triangle);
                }
                graphics.DrawLine(pen, P(8f, 6.2f), P(8f, 9.4f));
                graphics.FillEllipse(brush, X(7.3f), Y(10.6f), 1.5f * scale, 1.5f * scale);
                break;

            case Glyph.Info:
                graphics.DrawEllipse(pen, X(2.6f), Y(2.6f), 10.8f * scale, 10.8f * scale);
                graphics.DrawLine(pen, P(8f, 7.2f), P(8f, 11f));
                graphics.FillEllipse(brush, X(7.3f), Y(4.4f), 1.5f * scale, 1.5f * scale);
                break;

            case Glyph.Paw:
                graphics.FillEllipse(brush, X(5.6f), Y(8f), 4.8f * scale, 4.2f * scale);
                graphics.FillEllipse(brush, X(2.6f), Y(5.2f), 2.5f * scale, 2.6f * scale);
                graphics.FillEllipse(brush, X(6.75f), Y(3.4f), 2.5f * scale, 2.6f * scale);
                graphics.FillEllipse(brush, X(10.9f), Y(5.2f), 2.5f * scale, 2.6f * scale);
                break;

            case Glyph.Plus:
                graphics.DrawLine(pen, P(8f, 3.6f), P(8f, 12.4f));
                graphics.DrawLine(pen, P(3.6f, 8f), P(12.4f, 8f));
                break;

            case Glyph.Trash:
                graphics.DrawLine(pen, P(2.8f, 4.6f), P(13.2f, 4.6f));
                graphics.DrawLines(pen, new[] { P(6.2f, 4.6f), P(6.2f, 2.8f), P(9.8f, 2.8f), P(9.8f, 4.6f) });
                graphics.DrawLines(pen, new[] { P(4.4f, 4.6f), P(5.2f, 13.2f), P(10.8f, 13.2f), P(11.6f, 4.6f) });
                graphics.DrawLine(pen, P(6.9f, 7f), P(7.1f, 11.4f));
                graphics.DrawLine(pen, P(9.1f, 7f), P(8.9f, 11.4f));
                break;
        }
    }
}
