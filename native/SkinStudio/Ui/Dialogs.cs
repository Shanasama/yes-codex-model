using System.Runtime.InteropServices;

namespace SkinStudio.Ui;

/// <summary>与应用同一视觉语言的轻量模态对话框（确认 / 输入）。</summary>
internal sealed class StudioDialog : Form
{
    private readonly bool _prompt;
    private Field? _field;

    private StudioDialog(string title, string message, string confirmText, string? initialValue, bool danger)
    {
        _prompt = initialValue is not null;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Theme.Canvas;
        KeyPreview = true;
        Font = Theme.Body;
        DoubleBuffered = true;

        const int width = 432;
        var padding = 24;
        var textWidth = width - padding * 2;
        var messageHeight = TextRenderer.MeasureText(message, Theme.Body, new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        var bodyHeight = 52 + messageHeight + 4 + (_prompt ? 46 : 0) + 18 + 34 + padding;
        ClientSize = new Size(width, bodyHeight);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var heading = new StudioLabel
        {
            Text = title,
            Font = Theme.Title,
            ForeColor = Theme.Ink,
            BackColor = Theme.Canvas,
            Bounds = new Rectangle(padding, padding - 2, textWidth, 26)
        };
        Controls.Add(heading);

        var body = new StudioLabel
        {
            Text = message,
            Font = Theme.Body,
            ForeColor = Theme.InkSoft,
            BackColor = Theme.Canvas,
            WrapText = true,
            Bounds = new Rectangle(padding, padding + 28, textWidth, messageHeight + 4)
        };
        Controls.Add(body);

        var buttonsTop = ClientSize.Height - padding - 34;
        if (_prompt)
        {
            _field = new Field { Placeholder = "输入新的名称", BackColor = Theme.Canvas, Bounds = new Rectangle(padding, body.Bottom + 12, textWidth, 34) };
            _field.Value = initialValue ?? "";
            Controls.Add(_field);
        }

        var confirm = new StudioButton
        {
            Text = confirmText,
            Variant = danger ? ButtonVariant.Danger : ButtonVariant.Primary,
            BackColor = Theme.Canvas,
            Size = new Size(96, 34),
            Location = new Point(ClientSize.Width - padding - 96, buttonsTop)
        };
        confirm.Click += (_, _) => Confirm();
        Controls.Add(confirm);

        var cancel = new StudioButton
        {
            Text = "取消",
            Variant = ButtonVariant.Secondary,
            BackColor = Theme.Canvas,
            Size = new Size(80, 34),
            Location = new Point(confirm.Left - 10 - 80, buttonsTop)
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(cancel);

        Shown += (_, _) =>
        {
            RoundedCorners.Apply(Handle);
            if (_field is not null) { _field.Input.SelectAll(); _field.Input.Focus(); }
            else confirm.Focus();
        };
    }

    private void Confirm()
    {
        if (_prompt && string.IsNullOrWhiteSpace(_field?.Value))
        {
            _field?.Input.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new RectangleF(0.5f, 0.5f, ClientSize.Width - 1f, ClientSize.Height - 1f);
        Painter.FillRounded(e.Graphics, rect, 14f, Theme.Surface);
        Painter.DrawRounded(e.Graphics, rect, 14f, Theme.Line);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Y < 46)
        {
            RoundedCorners.Drag(this);
        }
        base.OnMouseDown(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            Confirm();
            return true;
        }
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    internal static StudioDialog CreateForTest(string title, string message, string confirmText, bool prompt) =>
        new(title, message, confirmText, prompt ? "初始名称" : null, false);

    public static bool Confirm(IWin32Window owner, string title, string message, string confirmText, bool danger = false)
    {
        using var dialog = new StudioDialog(title, message, confirmText, null, danger);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static string? Prompt(IWin32Window owner, string title, string message, string initial, string confirmText)
    {
        using var dialog = new StudioDialog(title, message, confirmText, initial, false);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._field?.Value.Trim() : null;
    }
}

internal static class RoundedCorners
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    public static void Apply(IntPtr handle)
    {
        try
        {
            var preference = DwmcpRound;
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 上没有这个属性，忽略
        }
    }

    public static void Drag(Form form)
    {
        ReleaseCapture();
        SendMessage(form.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
    }
}
