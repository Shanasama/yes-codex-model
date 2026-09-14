using System.Drawing.Drawing2D;
using SkinStudio.Core;

namespace SkinStudio.Ui;

/// <summary>
/// 所有自绘控件的基类。窗口是无边框的，控件在最外侧 6px 内把命中测试交回顶层窗体，
/// 这样拖动边角改变窗口大小的行为与系统窗口一致。
/// </summary>
internal abstract class SkinControl : Control
{
    protected SkinControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
    }

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x0084;
        if (m.Msg == wmNcHitTest && FindForm() is StudioForm form)
        {
            var raw = m.LParam.ToInt64();
            var screen = new Point(unchecked((short)(raw & 0xFFFF)), unchecked((short)((raw >> 16) & 0xFFFF)));
            var client = form.PointToClient(screen);
            var maximized = form.WindowState == FormWindowState.Maximized;
            if (StudioForm.HitTestCode(client, form.ClientSize, maximized) != StudioForm.HtClient)
            {
                m.Result = (IntPtr)(-1); // HTTRANSPARENT：交给顶层窗体处理
                return;
            }
        }
        base.WndProc(ref m);
    }
}

internal enum ButtonVariant { Primary, Secondary, Quiet, Danger, Glass }

// ---------------------------------------------------------------------------- 文本

internal sealed class StudioLabel : SkinControl
{
    public StudioLabel()
    {
        BackColor = Theme.Canvas;
        Font = Theme.Body;
        ForeColor = Theme.Ink;
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    public ContentAlignment Align { get; set; } = ContentAlignment.MiddleLeft;
    public bool WrapText { get; set; }
    public bool Ellipsis { get; set; } = true;

    protected override void OnPaint(PaintEventArgs e)
    {
        var flags = WrapText
            ? Painter.Wrap
            : Align switch
            {
                ContentAlignment.MiddleCenter => Painter.SingleLineCenter,
                ContentAlignment.MiddleRight => TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                                                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis |
                                                TextFormatFlags.NoPadding,
                ContentAlignment.TopLeft => TextFormatFlags.Left | TextFormatFlags.Top |
                                            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis |
                                            TextFormatFlags.NoPadding,
                _ => Ellipsis
                    ? Painter.SingleLine
                    : TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
                      TextFormatFlags.NoPadding
            };
        Painter.DrawText(e.Graphics, Text, Font, ForeColor, new Rectangle(0, 0, Width, Height), flags);
    }
}

// ---------------------------------------------------------------------------- 按钮

internal sealed class StudioButton : SkinControl
{
    private bool _hover;
    private bool _press;

    public StudioButton()
    {
        BackColor = Theme.Canvas;
        Font = Theme.SmallStrong;
        ForeColor = Theme.Ink;
        Cursor = Cursors.Hand;
        TabStop = false;
        Size = new Size(96, 34);
        SetStyle(ControlStyles.Selectable, false);
    }

    public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;
    public Glyph Icon { get; set; } = Glyph.None;
    public bool Stretch { get; set; }
    public Color? AccentOverride { get; set; }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var accent = AccentOverride ?? Theme.Accent;

        Color fill, text, border;
        if (!Enabled)
        {
            fill = Variant == ButtonVariant.Primary ? Theme.SurfaceMuted : BackColor;
            text = Theme.Faint;
            border = Variant is ButtonVariant.Primary or ButtonVariant.Quiet or ButtonVariant.Glass
                ? Color.Transparent
                : Theme.Line;
        }
        else
        {
            switch (Variant)
            {
                case ButtonVariant.Primary:
                    fill = _press ? Theme.AccentPress : _hover ? Theme.AccentHover : accent;
                    text = Theme.OnAccent;
                    border = Color.Transparent;
                    break;
                case ButtonVariant.Quiet:
                    fill = _press ? Theme.SurfaceMuted : _hover ? Theme.SurfaceHover : BackColor;
                    text = Theme.InkSoft;
                    border = Color.Transparent;
                    break;
                case ButtonVariant.Danger:
                    fill = _press ? Theme.DangerSoft : _hover ? Theme.DangerSoft : Theme.Surface;
                    text = Theme.Danger;
                    border = Theme.Line;
                    break;
                case ButtonVariant.Glass:
                    fill = Color.FromArgb(_press ? 42 : _hover ? 34 : 22, 255, 255, 255);
                    text = Theme.StageInk;
                    border = Color.FromArgb(46, 255, 255, 255);
                    break;
                default:
                    fill = _press ? Theme.SurfaceMuted : _hover ? Theme.SurfaceHover : Theme.Surface;
                    text = Theme.Ink;
                    border = _hover ? Theme.LineStrong : Theme.Line;
                    break;
            }
        }

        if (fill != BackColor && fill.A > 0) Painter.FillRounded(graphics, rect, Theme.RadiusButton, fill);
        if (border != Color.Transparent) Painter.DrawRounded(graphics, rect, Theme.RadiusButton, border);

        var textSize = Painter.MeasureText(Text, Font);
        var iconSize = Icon == Glyph.None ? 0 : 15;
        var gap = Icon == Glyph.None ? 0 : 7;
        var total = textSize.Width + iconSize + gap;
        var start = (Width - total) / 2f;

        if (Icon != Glyph.None)
        {
            var box = new RectangleF(start, (Height - iconSize) / 2f, iconSize, iconSize);
            Glyphs.Draw(graphics, Icon, box, text, 1.5f);
        }

        var textRect = new Rectangle((int)Math.Round(start + iconSize + gap), 0,
            (int)Math.Round((double)textSize.Width), Height);
        Painter.DrawText(graphics, Text, Font, text, textRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                                          TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}

internal sealed class IconButton : SkinControl
{
    private bool _hover;
    private bool _press;

    public IconButton()
    {
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
        TabStop = false;
        Size = new Size(30, 30);
        SetStyle(ControlStyles.Selectable, false);
    }

    public Glyph Icon { get; set; } = Glyph.None;
    public bool Dark { get; set; }
    public Color? IconColor { get; set; }
    public bool FillAlways { get; set; }
    public float Stroke { get; set; } = 1.5f;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var radius = Math.Min(8f, Height / 2f);

        if (Dark)
        {
            if (_press || _hover || FillAlways)
            {
                var alpha = _press ? 46 : _hover ? 34 : 20;
                Painter.FillRounded(e.Graphics, rect, radius, Color.FromArgb(alpha, 255, 255, 255));
            }
            if (FillAlways) Painter.DrawRounded(e.Graphics, rect, radius, Color.FromArgb(46, 255, 255, 255));
        }
        else
        {
            if (_press || _hover || FillAlways)
            {
                Painter.FillRounded(e.Graphics, rect, radius, _press ? Theme.SurfaceMuted : Theme.SurfaceHover);
            }
            if (FillAlways || _hover) Painter.DrawRounded(e.Graphics, rect, radius, Theme.Line);
        }

        var color = !Enabled
            ? (Dark ? Color.FromArgb(110, Theme.StageInk) : Theme.Faint)
            : IconColor ?? (Dark ? Theme.StageInk : Theme.InkSoft);
        var inset = Math.Max(7f, Width * 0.3f);
        var box = new RectangleF(inset, inset, Width - inset * 2, Height - inset * 2);
        Glyphs.Draw(e.Graphics, Icon, box, color, Stroke);
    }
}

// ---------------------------------------------------------------------------- 输入

internal sealed class Field : SkinControl
{
    private readonly TextBox _input = new();
    private bool _hover;

    public Field(bool multiline = false)
    {
        BackColor = Theme.Surface;
        SetStyle(ControlStyles.Selectable, false);

        _input.BorderStyle = BorderStyle.None;
        _input.BackColor = Theme.SurfaceMuted;
        _input.ForeColor = Theme.Ink;
        _input.Font = Theme.Body;
        _input.Multiline = multiline;
        _input.WordWrap = multiline;
        _input.ShortcutsEnabled = true;
        _input.TabStop = false;
        _input.GotFocus += (_, _) => { _input.BackColor = Theme.Surface; Invalidate(); };
        _input.LostFocus += (_, _) => { _input.BackColor = Theme.SurfaceMuted; Invalidate(); };
        _input.TextChanged += (_, _) => Invalidate();
        Controls.Add(_input);

        Height = multiline ? 78 : 34;
    }

    public string Value
    {
        get => _input.Text;
        set => _input.Text = value ?? "";
    }

    public string Placeholder { get; set; } = "";
    public bool Monospace { get; set; }
    public float Radius { get; set; } = Theme.RadiusField;
    public int MaxLength
    {
        get => _input.MaxLength;
        set => _input.MaxLength = value;
    }

    public TextBox Input => _input;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _input.Focus(); base.OnMouseDown(e); }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _input.Font = Monospace ? Theme.Mono : Font;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        _input.Font = Monospace ? Theme.Mono : Theme.Body;
        var inset = 12;
        if (_input.Multiline)
        {
            _input.Bounds = new Rectangle(inset, 6, Math.Max(10, Width - inset * 2), Math.Max(10, Height - 12));
        }
        else
        {
            var lineHeight = _input.PreferredHeight;
            _input.Bounds = new Rectangle(inset, Math.Max(0, (Height - lineHeight) / 2), Math.Max(10, Width - inset * 2), lineHeight);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var focused = _input.Focused;
        var fill = focused ? Theme.Surface : _hover ? Color.FromArgb(238, 241, 237) : Theme.SurfaceMuted;
        Painter.FillRounded(e.Graphics, rect, Radius, fill);
        if (focused) Painter.DrawRounded(e.Graphics, rect, Radius, Theme.Accent, 1.4f);
        else Painter.DrawRounded(e.Graphics, rect, Radius, Theme.Line);

        if (string.IsNullOrEmpty(_input.Text) && !focused && !string.IsNullOrEmpty(Placeholder))
        {
            var bounds = _input.Multiline
                ? new Rectangle(14, 8, Width - 26, Height - 14)
                : new Rectangle(14, 0, Width - 26, Height);
            Painter.DrawText(e.Graphics, Placeholder, Theme.Body, Theme.Faint, bounds,
                _input.Multiline ? Painter.Wrap : Painter.SingleLine);
        }
    }
}

internal sealed class Toggle : SkinControl
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private float _progress;
    private bool _hover;

    public Toggle()
    {
        BackColor = Theme.Surface;
        Cursor = Cursors.Hand;
        TabStop = false;
        Size = new Size(38, 22);
        SetStyle(ControlStyles.Selectable, false);
        _timer.Tick += (_, _) =>
        {
            var target = Checked ? 1f : 0f;
            _progress += (target - _progress) * 0.35f;
            if (Math.Abs(target - _progress) < 0.03f)
            {
                _progress = target;
                _timer.Stop();
            }
            Invalidate();
        };
    }

    public bool Checked { get; private set; }
    public event Action<bool>? CheckedChanged;

    public void SetChecked(bool value, bool animate = true)
    {
        if (Checked == value) return;
        Checked = value;
        if (animate) _timer.Start(); else { _progress = value ? 1f : 0f; Invalidate(); }
        CheckedChanged?.Invoke(value);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled) SetChecked(!Checked);
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var rect = new RectangleF(0.5f, (Height - 20f) / 2f, Width - 1f, 20f);
        var off = Color.FromArgb(216, 222, 216);
        var track = Blend(off, Theme.Accent, _progress);
        if (_hover && _progress < 1f) track = Blend(track, Color.FromArgb(203, 211, 204), 0.4f);

        Painter.FillRounded(graphics, rect, 10f, Enabled ? track : Theme.SurfaceMuted);

        var knobSize = 15f;
        var left = rect.X + 3f + (rect.Width - knobSize - 6f) * _progress;
        var knob = new RectangleF(left, rect.Y + (rect.Height - knobSize) / 2f, knobSize, knobSize);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Enabled ? Color.White : Color.FromArgb(246, 246, 244));
        graphics.FillEllipse(brush, knob);
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(from.R + (to.R - from.R) * amount),
            (int)(from.G + (to.G - from.G) * amount),
            (int)(from.B + (to.B - from.B) * amount));
    }
}

// ---------------------------------------------------------------------------- 小组件

internal sealed class Chip : SkinControl
{
    public Chip()
    {
        BackColor = Theme.Surface;
        Font = Theme.MicroStrong;
        ForeColor = Theme.AccentInk;
        TabStop = false;
        Height = 22;
        SetStyle(ControlStyles.Selectable, false);
    }

    public string Text2 { get => Text; set => Text = value; }
    public Color Fill { get; set; } = Theme.AccentSoft;
    public Color Ink { get; set; } = Theme.AccentInk;
    public Glyph Icon { get; set; } = Glyph.None;
    public int PadH { get; set; } = 9;
    public bool Bold { get; set; } = true;

    public int PreferredWidth
    {
        get
        {
            var size = Painter.MeasureText(Text, Bold ? Theme.MicroStrong : Theme.Micro);
            var icon = Icon == Glyph.None ? 0 : 13;
            var gap = Icon == Glyph.None ? 0 : 5;
            return size.Width + icon + gap + PadH * 2;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Painter.FillRounded(e.Graphics, rect, Height / 2f, Fill);
        var font = Bold ? Theme.MicroStrong : Theme.Micro;
        var size = Painter.MeasureText(Text, font);
        var icon = Icon == Glyph.None ? 0 : 13;
        var gap = Icon == Glyph.None ? 0 : 5;
        var start = (Width - (size.Width + icon + gap)) / 2f;
        if (Icon != Glyph.None)
        {
            Glyphs.Draw(e.Graphics, Icon, new RectangleF(start, (Height - 13) / 2f, 13, 13), Ink, 1.4f);
        }
        Painter.DrawText(e.Graphics, Text, font, Ink,
            new Rectangle((int)Math.Round(start + icon + gap), 0, (int)size.Width + 2, Height),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}

internal sealed class StatusDot : SkinControl
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private float _phase;

    public StatusDot()
    {
        BackColor = Theme.Surface;
        TabStop = false;
        Size = new Size(10, 10);
        SetStyle(ControlStyles.Selectable, false);
        _timer.Tick += (_, _) =>
        {
            _phase += 0.06f;
            Invalidate();
        };
    }

    public Color DotColor { get; set; } = Theme.Success;
    public bool Pulse { get; private set; }

    public void SetPulse(bool value)
    {
        Pulse = value;
        if (value) _timer.Start(); else _timer.Stop();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var center = new PointF(Width / 2f, Height / 2f);
        var radius = Math.Min(Width, Height) / 2f - 0.5f;
        if (Pulse)
        {
            var alpha = (int)(80 + 60 * Math.Sin(_phase * Math.PI));
            using var halo = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 0, 140), DotColor));
            graphics.FillEllipse(halo, center.X - radius - 2.5f, center.Y - radius - 2.5f,
                (radius + 2.5f) * 2, (radius + 2.5f) * 2);
        }
        using var brush = new SolidBrush(DotColor);
        graphics.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
    }
}

// ---------------------------------------------------------------------------- 皮肤列表项

internal sealed class SkinListItem : SkinControl
{
    private bool _hover;

    public SkinListItem(SkinInfo skin)
    {
        Skin = skin;
        BackColor = Theme.Sidebar;
        Cursor = Cursors.Hand;
        Height = 68;
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
    }

    public SkinInfo Skin { get; }
    public Image? Thumb { get; set; }
    public bool Applied { get; set; }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var rect = new RectangleF(0f, 0f, Width, Height);
        var background = Selected ? Theme.AccentSoft : _hover ? Theme.SurfaceHover : BackColor;
        Painter.FillRounded(graphics, rect, Theme.RadiusCard, background);
        if (Selected) Painter.DrawRounded(graphics, new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f),
            Theme.RadiusCard, Color.FromArgb(150, Theme.Accent));

        if (Selected)
        {
            Painter.FillRounded(graphics, new RectangleF(6f, Height / 2f - 15f, 3f, 30f), 1.5f, Theme.Accent);
        }

        var thumbBox = new RectangleF(14f, 11f, 46f, 46f);
        Painter.FillRounded(graphics, thumbBox, 9f, Selected ? Color.FromArgb(255, 255, 255) : Theme.SurfaceMuted);
        if (Thumb is not null)
        {
            Painter.DrawImageContained(graphics, Thumb, new RectangleF(thumbBox.X + 3f, thumbBox.Y + 3f, 40f, 40f));
        }
        else
        {
            Glyphs.Draw(graphics, Glyph.Paw, new RectangleF(thumbBox.X + 13f, thumbBox.Y + 13f, 20f, 20f),
                Skin.Valid ? Theme.Faint : Theme.Danger, 1.4f);
        }

        var nameLeft = 72;
        var nameWidth = Math.Max(40, Width - nameLeft - 16);
        Painter.DrawText(graphics, Skin.DisplayName, Theme.BodyStrong,
            Selected ? Theme.AccentInk : Theme.Ink,
            new Rectangle(nameLeft, 12, nameWidth, 20), Painter.SingleLine);

        var meta = Skin.Valid
            ? $"{Skin.ReadyStates}/{Skin.TotalStates} 动作 · {Format.Bytes(Skin.Bytes)}"
            : "文件不可用";
        Painter.DrawText(graphics, meta, Theme.Small, Skin.Valid ? Theme.Muted : Theme.Danger,
            new Rectangle(nameLeft, 34, nameWidth, 18), Painter.SingleLine);

        if (Applied && Skin.Valid)
        {
            var label = "使用中";
            var size = Painter.MeasureText(label, Theme.MicroStrong);
            var chipWidth = size.Width + 16;
            var chip = new RectangleF(Width - chipWidth - 12f, 12f, chipWidth, 18f);
            Painter.FillRounded(graphics, chip, 9f, Selected ? Color.FromArgb(220, 255, 255, 255) : Theme.AccentSoft);
            Painter.DrawText(graphics, label, Theme.MicroStrong, Theme.AccentInk,
                new Rectangle((int)chip.X, (int)chip.Y, (int)chip.Width, (int)chip.Height), Painter.SingleLineCenter);
        }
    }
}

// ---------------------------------------------------------------------------- 动作卡片

internal sealed class StateTile : SkinControl
{
    private bool _hover;

    public StateTile(StateInfo definition)
    {
        Id = definition.Id;
        State = definition;
        Label = definition.Label;
        BackColor = Theme.Canvas;
        Cursor = Cursors.Hand;
        Height = 88;
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
    }

    public string Id { get; }
    public StateInfo State { get; private set; }
    public string Label { get; private set; }
    public Image? Thumb { get; set; }

    public void SetState(StateInfo? state)
    {
        State = state ?? StateCatalog.Definition(Id);
        Label = string.IsNullOrWhiteSpace(State.Label) ? Id : State.Label;
        Invalidate();
    }

    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; Invalidate(); }
    }

    public Rectangle ReplaceBounds => new(Width - 34, 8, 26, 26);

    public event Action<StateInfo>? ReplaceRequested;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ReplaceBounds.Contains(e.Location))
        {
            ReplaceRequested?.Invoke(State);
            return;
        }
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var fill = Selected ? Theme.AccentSoft : _hover ? Theme.SurfaceHover : Theme.Surface;
        Painter.FillRounded(graphics, rect, Theme.RadiusTile, fill);
        Painter.DrawRounded(graphics, rect, Theme.RadiusTile, Selected ? Theme.Accent : Theme.Line, Selected ? 1.5f : 1f);

        var thumbBox = new RectangleF(12f, (Height - 40f) / 2f, 40f, 40f);
        Painter.FillRounded(graphics, thumbBox, 9f, Selected ? Color.FromArgb(255, 255, 255) : Theme.SurfaceMuted);
        if (Thumb is not null)
        {
            Painter.DrawImageContained(graphics, Thumb, new RectangleF(thumbBox.X + 3f, thumbBox.Y + 3f, 34f, 34f));
        }
        else
        {
            Painter.DrawText(graphics, Label.Length > 1 ? Label[..1] : "·", Theme.BodyStrong,
                Theme.Faint, new Rectangle((int)thumbBox.X, (int)thumbBox.Y, (int)thumbBox.Width, (int)thumbBox.Height),
                Painter.SingleLineCenter);
        }

        var left = 62;
        var width = Math.Max(30, Width - left - 14);
        Painter.DrawText(graphics, Label, Theme.BodyStrong, Selected ? Theme.AccentInk : Theme.Ink,
            new Rectangle(left, 20, width, 20), Painter.SingleLine);

        var meta = State.Ready
            ? $"{State.Frames} 帧 · {Format.Bytes(State.Bytes)}"
            : "缺少 GIF";
        Painter.DrawText(graphics, meta, Theme.Small, State.Ready ? Theme.Muted : Theme.Danger,
            new Rectangle(left, 40, width, 18), Painter.SingleLine);

        if (_hover)
        {
            var box = ReplaceBounds;
            var round = new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1f, box.Height - 1f);
            Painter.FillRounded(graphics, round, 8f, Color.FromArgb(246, 248, 245));
            Painter.DrawRounded(graphics, round, 8f, Theme.LineStrong);
            Glyphs.Draw(graphics, Glyph.Refresh, new RectangleF(box.X + 7f, box.Y + 7f, 12f, 12f), Theme.InkSoft, 1.4f);
        }
    }
}

// ---------------------------------------------------------------------------- 预览舞台

internal sealed class PreviewStage : SkinControl
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private GifAnimation? _animation;
    private int _frame;
    private bool _playing = true;
    private bool _actualSize;
    private bool _dragOver;
    private Size _cacheTarget;
    private readonly System.Windows.Forms.Timer _overlayTimer = new() { Interval = 3200 };
    private string _overlayMessage = "";
    private bool _overlayDanger;

    public PreviewStage()
    {
        BackColor = Theme.Canvas;
        TabStop = false;
        AllowDrop = true;
       SetStyle(ControlStyles.Selectable, false);
       _timer.Tick += (_, _) => Advance();
        _overlayTimer.Tick += (_, _) =>
        {
            _overlayTimer.Stop();
            _overlayMessage = "";
            Invalidate();
        };
    }

    public string StateLabel { get; set; } = "";
    public string Meta { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Busy { get; set; }
    public bool LowPower { get; set; }

    public event Action<string>? GifDropped;
    public event Action? PlayStateChanged;

    public bool Playing => _playing;
    public bool ActualSize => _actualSize;

    public void ShowAnimation(GifAnimation? animation, string label, string meta)
    {
        _animation = animation;
        _frame = 0;
        StateLabel = label;
        Meta = meta;
        Message = "";
        Busy = false;
        _cacheTarget = Size.Empty;
        RestartTimer();
        Invalidate();
    }

    public void ShowMessage(string message, bool busy)
    {
        _animation?.Dispose();
        _animation = null;
        Message = message;
        Busy = busy;
        StateLabel = "";
       Meta = "";
       _timer.Stop();
       Invalidate();
    }

    /// <summary>舞台内悬浮提示条（应用成功、失败等一次性反馈）。</summary>
    public void ShowOverlay(string message, bool danger = false)
    {
        _overlayMessage = message;
        _overlayDanger = danger;
        _overlayTimer.Stop();
        _overlayTimer.Start();
        Invalidate();
    }

    public void SetPlaying(bool playing)
    {
        _playing = playing;
        RestartTimer();
        Invalidate();
        PlayStateChanged?.Invoke();
    }

    public void SetActualSize(bool actual)
    {
        if (_actualSize == actual) return;
        _actualSize = actual;
        _cacheTarget = Size.Empty;
        _animation?.ResetCache();
        Invalidate();
    }

    public void SetLowPower(bool low)
    {
        LowPower = low;
        _frame = 0;
        RestartTimer();
        Invalidate();
    }

    private void RestartTimer()
    {
        _timer.Stop();
        if (_animation is null || !_playing || LowPower || _animation.FrameCount <= 1) return;
        _timer.Interval = Math.Clamp(_animation.DelayFor(_frame), 16, 1000);
        _timer.Start();
    }

    private void Advance()
    {
        if (_animation is null) return;
        _frame++;
        Invalidate();
        _timer.Interval = Math.Clamp(_animation.DelayFor(_frame), 16, 1000);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _cacheTarget = Size.Empty;
        _animation?.ResetCache();
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        _dragOver = IsGif(e);
        e.Effect = _dragOver ? DragDropEffects.Copy : DragDropEffects.None;
        Invalidate();
        base.OnDragEnter(e);
    }

    protected override void OnDragLeave(EventArgs e)
    {
        _dragOver = false;
        Invalidate();
        base.OnDragLeave(e);
    }

    protected override void OnDragDrop(DragEventArgs e)
    {
        _dragOver = false;
        Invalidate();
        if (IsGif(e) && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            GifDropped?.Invoke(files[0]);
        }
        base.OnDragDrop(e);
    }

    private static bool IsGif(DragEventArgs e) =>
        e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files &&
        files[0].EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

    private RectangleF InnerRect => new(24f, 24f, Math.Max(10, Width - 48f), Math.Max(10, Height - 48f));

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Painter.Rounded(rect, 16f);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.SetClip(path);

        using (var brush = new LinearGradientBrush(rect, Theme.StageTop, Theme.StageBottom, LinearGradientMode.Vertical))
        {
            graphics.FillRectangle(brush, rect);
        }

        using (var glow = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
        {
            graphics.FillEllipse(glow, Width / 2f - Width * 0.42f, -Height * 0.35f, Width * 0.84f, Height * 0.7f);
        }

        var frame = ResolveFrame();
        if (frame is not null)
        {
            var target = TargetRect(frame.Size);
            graphics.InterpolationMode = _actualSize ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(frame, target);
        }
        else if (Busy)
        {
            DrawBusy(graphics);
        }
        else
        {
            Painter.DrawText(graphics, Message, Theme.Body, Theme.StageMuted,
                new Rectangle(24, 0, Math.Max(10, Width - 48), Height), Painter.SingleLineCenter);
        }

        var trackRect = new RectangleF(24f, Height - 18f, Math.Max(10f, Width - 48f), 3f);
        Painter.FillRounded(graphics, trackRect, 1.5f, Theme.StageTrack);
        if (_animation is { FrameCount: > 1 })
        {
            var progress = (float)(((_frame % _animation.FrameCount) + 1) / (double)_animation.FrameCount);
            var fill = new RectangleF(trackRect.X, trackRect.Y, Math.Max(3f, trackRect.Width * progress), 3f);
            Painter.FillRounded(graphics, fill, 1.5f, Theme.Accent);
        }
        graphics.ResetClip();

        var borderColor = _dragOver ? Theme.Accent : Theme.StageLine;
        Painter.DrawRounded(graphics, rect, 16f, borderColor, _dragOver ? 2f : 1f);

        if (!string.IsNullOrEmpty(StateLabel))
        {
            var size = Painter.MeasureText(StateLabel, Theme.MicroStrong);
            var chip = new RectangleF(18f, 16f, size.Width + 20f, 24f);
            Painter.FillRounded(graphics, chip, 12f, Color.FromArgb(64, 255, 255, 255));
            Painter.DrawText(graphics, StateLabel, Theme.MicroStrong, Theme.StageInk,
                new Rectangle((int)chip.X, (int)chip.Y, (int)chip.Width, (int)chip.Height), Painter.SingleLineCenter);
        }

        var metaText = _dragOver ? "松开即可替换这个动作的 GIF" : Meta;
        if (!string.IsNullOrEmpty(metaText))
        {
            Painter.DrawText(graphics, metaText, Theme.Small, _dragOver ? Theme.Accent : Theme.StageMuted,
                new Rectangle(18, Height - 44, Math.Max(10, Width - 150), 20), Painter.SingleLine);
        }

        if (_animation is not null && !_playing)
        {
            var size = Painter.MeasureText("已暂停", Theme.SmallStrong);
            var chip = new RectangleF(Width / 2f - (size.Width + 26f) / 2f, Height / 2f - 15f, size.Width + 26f, 30f);
            Painter.FillRounded(graphics, chip, 15f, Color.FromArgb(150, 12, 16, 14));
           Painter.DrawText(graphics, "已暂停", Theme.SmallStrong, Theme.StageInk,
               new Rectangle((int)chip.X, (int)chip.Y, (int)chip.Width, (int)chip.Height), Painter.SingleLineCenter);
       }

        if (!string.IsNullOrEmpty(_overlayMessage))
        {
            var textSize = Painter.MeasureText(_overlayMessage, Theme.SmallStrong);
            var width = Math.Min(Math.Max(160f, Width - 48f), textSize.Width + 56f);
            var chip = new RectangleF((Width - width) / 2f, Height - 64f, width, 36f);
            Painter.FillRounded(graphics, chip, 12f, Color.FromArgb(244, 22, 32, 27));
            Painter.DrawRounded(graphics, chip, 12f, Color.FromArgb(72, 255, 255, 255));
            Glyphs.Draw(graphics, _overlayDanger ? Glyph.Warning : Glyph.Check,
                new RectangleF(chip.X + 14f, chip.Y + 11f, 14f, 14f),
                _overlayDanger ? Color.FromArgb(255, 188, 178) : Theme.StageInk, 1.5f);
            Painter.DrawText(graphics, _overlayMessage, Theme.SmallStrong, Theme.StageInk,
                new Rectangle((int)chip.X + 36, (int)chip.Y, Math.Max(24, (int)chip.Width - 48), (int)chip.Height),
                Painter.SingleLine);
        }
    }

    private void DrawBusy(Graphics graphics)
    {
        var center = new PointF(Width / 2f, Height / 2f);
        using var pen = new Pen(Color.FromArgb(150, Theme.StageInk), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawArc(pen, center.X - 11, center.Y - 11, 22, 22, 0, 250);
        Painter.DrawText(graphics, Message, Theme.Small, Theme.StageMuted,
            new Rectangle(24, (int)center.Y + 22, Math.Max(10, Width - 48), 20), Painter.SingleLineCenter);
    }

    private Bitmap? ResolveFrame()
    {
        if (_animation is null) return null;
        var target = SourceTargetSize();
        if (_cacheTarget != target)
        {
            _animation.ResetCache();
            _cacheTarget = target;
        }
        return _animation.Frame(_frame, target);
    }

    private Size SourceTargetSize()
    {
        if (_animation is null) return Size.Empty;
        var source = _animation.SourceSize;
        if (_actualSize)
        {
            return new Size(Math.Min(source.Width, Math.Max(1, Width - 24)),
                Math.Min(source.Height, Math.Max(1, Height - 24)));
        }
        var inner = InnerRect;
        var ratio = Math.Min(inner.Width / source.Width, inner.Height / source.Height);
        ratio = Math.Min(ratio, 3f);
        return new Size(Math.Max(1, (int)Math.Round(source.Width * ratio)),
            Math.Max(1, (int)Math.Round(source.Height * ratio)));
    }

    private RectangleF TargetRect(Size size)
    {
        var inner = InnerRect;
        var x = inner.X + (inner.Width - size.Width) / 2f;
        var y = inner.Y + (inner.Height - size.Height) / 2f;
        return new RectangleF(x, y, size.Width, size.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
           _timer.Dispose();
            _overlayTimer.Dispose();
           _animation?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// ---------------------------------------------------------------------------- 滚动容器

internal sealed class ScrollHost : SkinControl
{
    private int _offset;
    private bool _dragging;
    private int _dragStart;
    private int _dragOrigin;
    private bool _hover;

    public ScrollHost()
    {
        BackColor = Theme.Sidebar;
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
    }

    private Control? _content;

    public Control? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value)) return;
            if (_content is not null) Controls.Remove(_content);
            _content = value;
            if (value is not null)
            {
                value.Parent = this;
                LayoutContent();
            }
            Invalidate();
        }
    }

    public int Gap { get; set; } = 8;

    public int ContentHeight => Content?.Height ?? 0;
    public int Viewport => Height;
    public int MaxOffset => Math.Max(0, ContentHeight - Viewport);
    public int Offset => _offset;

    public void Scroll(int delta)
    {
        var next = Math.Clamp(_offset - delta / 3, 0, MaxOffset);
        if (next == _offset) return;
        _offset = next;
        LayoutContent();
        Invalidate();
    }

    public void ScrollToTop()
    {
        _offset = 0;
        LayoutContent();
        Invalidate();
    }

    public void EnsureVisible(Rectangle childBounds)
    {
        var top = childBounds.Top - Gap;
        var bottom = childBounds.Bottom + Gap;
        if (top < _offset) _offset = Math.Max(0, top);
        else if (bottom > _offset + Height) _offset = Math.Min(MaxOffset, bottom - Height);
        LayoutContent();
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        LayoutContent();
    }

    private void LayoutContent()
    {
        if (Content is null) return;
        _offset = Math.Clamp(_offset, 0, MaxOffset);
        var width = Math.Max(10, Width - (MaxOffset > 0 ? 10 : 0));
        Content.SetBounds(0, -_offset, width, Content.Height);
    }

    private RectangleF ThumbRect()
    {
        var ratio = (float)Viewport / Math.Max(1, ContentHeight);
        var height = Math.Max(36f, Viewport * ratio);
        var travel = Viewport - height;
        var position = MaxOffset == 0 ? 0f : travel * (_offset / (float)MaxOffset);
        return new RectangleF(Width - 8f, position + 2f, 4f, height - 4f);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (MaxOffset > 0 && ThumbRect().Contains(e.Location))
        {
            _dragging = true;
            _dragStart = e.Y;
            _dragOrigin = _offset;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging && MaxOffset > 0)
        {
            var travel = Math.Max(1f, Viewport - ThumbRect().Height);
            var delta = (e.Y - _dragStart) / travel * MaxOffset;
            _offset = Math.Clamp((int)Math.Round(_dragOrigin + delta), 0, MaxOffset);
            LayoutContent();
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Content is null) return;
        if (MaxOffset > 0)
        {
            var thumb = ThumbRect();
            var color = _dragging ? Theme.Accent : _hover ? Theme.LineStrong : Theme.Line;
            Painter.FillRounded(e.Graphics, thumb, 2f, color);
        }
    }
}
