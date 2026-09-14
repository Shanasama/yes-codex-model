using SkinStudio.Core;

namespace SkinStudio.Ui;

/// <summary>
/// 布局自检：在没有可视化通道的环境里，用几何不变量代替肉眼检查。
/// 覆盖控件越界、零尺寸、兄弟控件重叠、文字溢出与窗口边缘命中测试。
/// </summary>
internal static class SelfTest
{
    private const int HtClient = 1;
    private const int HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14;
    private const int HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;

    public static int Run()
    {
        var failures = new List<string>();
        var checks = 0;

        var sizes = new[]
        {
            new Size(1080, 680),
            new Size(1180, 760),
            new Size(1280, 850),
            new Size(1440, 900),
            new Size(1760, 1000)
        };

        using var form = new StudioForm(new StudioOptions { Offline = true });
        form.StartPosition = FormStartPosition.Manual;
        form.ShowInTaskbar = false;
        form.Location = new Point(-6000, -6000);
        form.Show();
        Application.DoEvents();
        form.LoadSampleForTest();
        Application.DoEvents();

        foreach (var size in sizes)
        {
            form.ClientSize = size;
            form.LayoutAll();
            form.PerformLayout();
            form.LayoutAll();
            Inspect(form, size, failures, ref checks);
            CheckInvariants(form, size, failures, ref checks);
        }

        CheckHitTest(failures, ref checks);
        CheckDialogs(failures, ref checks);
        if (checks < 240)
        {
            failures.Add($"自检覆盖不足：只检查了 {checks} 项，说明界面没有真正参与布局");
        }
        form.Close();

        foreach (var failure in failures) Console.WriteLine("FAIL  " + failure);
        Console.WriteLine($"[selftest] checks={checks} failures={failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void CheckDialogs(List<string> failures, ref int checks)
    {
        using var confirm = StudioDialog.CreateForTest("开启完整动画",
            "开启前会自动备份程序文件，完成后需要完整退出并重启 Codex。继续吗？", "开启", false);
        confirm.StartPosition = FormStartPosition.Manual;
        confirm.ShowInTaskbar = false;
        confirm.Location = new Point(-6000, -6000);
        confirm.Show();
        Application.DoEvents();
        checks++;
        if (confirm.ClientSize.Width < 360) failures.Add($"确认对话框过窄：{confirm.ClientSize.Width}");
        Inspect(confirm, confirm.ClientSize, failures, ref checks);
        confirm.Close();

        using var prompt = StudioDialog.CreateForTest("复制皮肤",
            "新皮肤会复制当前的全部动作与素材，方便二次修改。", "创建副本", true);
        prompt.StartPosition = FormStartPosition.Manual;
        prompt.ShowInTaskbar = false;
        prompt.Location = new Point(-6000, -6000);
        prompt.Show();
        Application.DoEvents();
        Inspect(prompt, prompt.ClientSize, failures, ref checks);
        prompt.Close();
    }

    private static void CheckHitTest(List<string> failures, ref int checks)
    {
        var size = new Size(1280, 850);
        var cases = new (Point Point, int Expected, string Name)[]
        {
            (new Point(2, 2), HtTopLeft, "左上角"),
            (new Point(size.Width - 2, 2), HtTopRight, "右上角"),
            (new Point(2, size.Height - 2), HtBottomLeft, "左下角"),
            (new Point(size.Width - 2, size.Height - 2), HtBottomRight, "右下角"),
            (new Point(2, 400), HtLeft, "左边"),
            (new Point(size.Width - 2, 400), HtRight, "右边"),
            (new Point(400, 2), HtTop, "上边"),
            (new Point(400, size.Height - 2), HtBottom, "下边"),
            (new Point(400, 400), HtClient, "窗口内部"),
            (new Point(2, 2), HtClient, "最大化后不再缩放")
        };

        for (var index = 0; index < cases.Length; index++)
        {
            checks++;
            var (point, expected, name) = cases[index];
            var maximized = index == cases.Length - 1;
            var actual = StudioForm.HitTestCode(point, size, maximized);
            if (actual != expected)
            {
                failures.Add($"命中测试 {name}：期望 {expected}，实际 {actual}");
            }
        }
    }

    private static void CheckInvariants(StudioForm form, Size size, List<string> failures, ref int checks)
    {
        checks += 4;
        if (form.Tiles.Count != 9) failures.Add($"{size.Width}x{size.Height}：动作卡片数量为 {form.Tiles.Count}，应为 9");
        if (form.StageBounds.Width < 300) failures.Add($"{size.Width}x{size.Height}：预览舞台过窄（{form.StageBounds.Width}）");
        if (form.StageBounds.Height < 140) failures.Add($"{size.Width}x{size.Height}：预览舞台过矮（{form.StageBounds.Height}）");
        if (form.SidebarSize.Width < 232) failures.Add($"{size.Width}x{size.Height}：侧栏被压缩过度（{form.SidebarSize.Width}）");

        foreach (var tile in form.Tiles.Values)
        {
            checks++;
            if (tile.Width < 120) failures.Add($"{size.Width}x{size.Height}：动作卡片过窄（{tile.Width}）");
            if (tile.Height < 66) failures.Add($"{size.Width}x{size.Height}：动作卡片过矮（{tile.Height}）");
        }
    }

    private static void Inspect(Control parent, Size size, List<string> failures, ref int checks)
    {
        var visible = parent.Controls.Cast<Control>().Where(control => control.Visible).ToList();

        foreach (var control in visible)
        {
            checks++;
            if (control.Width <= 0 || control.Height <= 0)
            {
                failures.Add($"{size.Width}x{size.Height}：{Describe(control)} 尺寸非法 {control.Width}x{control.Height}");
            }

            if (parent is not ScrollHost)
            {
                var bounds = control.Bounds;
                if (bounds.Left < -1 || bounds.Top < -1 ||
                    bounds.Right > parent.ClientSize.Width + 1 || bounds.Bottom > parent.ClientSize.Height + 1)
                {
                    failures.Add(
                        $"{size.Width}x{size.Height}：{Describe(control)} 越界 {bounds} 超出父容器 {parent.ClientSize}");
                }
            }
        }

        for (var i = 0; i < visible.Count; i++)
        {
            for (var j = i + 1; j < visible.Count; j++)
            {
                var a = visible[i];
                var b = visible[j];
                var overlap = Rectangle.Intersect(a.Bounds, b.Bounds);
                if (overlap.Width <= 1 || overlap.Height <= 1) continue;
                if (parent is ScrollHost) continue;
                checks++;
                failures.Add($"{size.Width}x{size.Height}：{Describe(a)} 与 {Describe(b)} 重叠 {overlap}");
            }
        }

        foreach (var control in visible)
        {
            switch (control)
            {
                case StudioButton button:
                    checks++;
                    var buttonText = Painter.MeasureText(button.Text, button.Font);
                    var icon = button.Icon == Glyph.None ? 0 : 22;
                    if (buttonText.Width + icon + (buttonText.Width > 0 ? 14 : 0) > button.Width)
                    {
                        failures.Add(
                            $"{size.Width}x{size.Height}：按钮「{button.Text}」文字溢出（需要 {buttonText.Width + icon + (buttonText.Width > 0 ? 14 : 0)}，可用 {button.Width}）");
                    }
                    break;
                case Chip chip:
                    checks++;
                    var chipText = Painter.MeasureText(chip.Text, chip.Bold ? Theme.MicroStrong : Theme.Micro);
                    if (chipText.Width + 12 > chip.Width)
                    {
                        failures.Add($"{size.Width}x{size.Height}：标签「{chip.Text}」文字溢出（{chip.Width}）");
                    }
                    break;
            }

            Inspect(control, size, failures, ref checks);
        }
    }

    private static string Describe(Control control)
    {
        var parts = new List<string>();
        var current = control;
        for (var depth = 0; depth < 4 && current is not null; depth++)
        {
            parts.Insert(0, current.GetType().Name);
            current = current.Parent;
        }
        return string.Join("/", parts);
    }
}
