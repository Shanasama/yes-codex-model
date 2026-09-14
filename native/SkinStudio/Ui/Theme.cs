using System.Drawing;
using System.Drawing.Text;

namespace SkinStudio.Ui;

/// <summary>设计令牌：颜色、字号、间距、圆角。整套界面只从这里取值。</summary>
internal static class Theme
{
    // 画布与容器
    public static readonly Color Chrome = Color.FromArgb(255, 255, 255);
    public static readonly Color Canvas = Color.FromArgb(245, 246, 244);
    public static readonly Color Sidebar = Color.FromArgb(251, 252, 251);
    public static readonly Color Surface = Color.FromArgb(255, 255, 255);
    public static readonly Color SurfaceMuted = Color.FromArgb(242, 244, 241);
    public static readonly Color SurfaceHover = Color.FromArgb(246, 248, 245);

    // 文字
    public static readonly Color Ink = Color.FromArgb(23, 33, 28);
    public static readonly Color InkSoft = Color.FromArgb(66, 79, 72);
    public static readonly Color Muted = Color.FromArgb(124, 135, 128);
    public static readonly Color Faint = Color.FromArgb(167, 176, 170);
    public static readonly Color OnAccent = Color.FromArgb(255, 255, 255);

    // 描边
    public static readonly Color Line = Color.FromArgb(229, 233, 228);
    public static readonly Color LineStrong = Color.FromArgb(211, 218, 212);

    // 强调色（取自插件品牌色 #1F7A5C）
    public static readonly Color Accent = Color.FromArgb(31, 122, 92);
    public static readonly Color AccentHover = Color.FromArgb(26, 107, 80);
    public static readonly Color AccentPress = Color.FromArgb(21, 88, 66);
    public static readonly Color AccentSoft = Color.FromArgb(232, 243, 237);
    public static readonly Color AccentInk = Color.FromArgb(20, 92, 69);

    // 预览舞台（深色，衬托像素画）
    public static readonly Color StageTop = Color.FromArgb(26, 32, 28);
    public static readonly Color StageBottom = Color.FromArgb(14, 18, 16);
    public static readonly Color StageLine = Color.FromArgb(47, 58, 51);
    public static readonly Color StageInk = Color.FromArgb(238, 243, 239);
    public static readonly Color StageMuted = Color.FromArgb(146, 160, 151);
    public static readonly Color StageGlass = Color.FromArgb(64, 255, 255, 255);
    public static readonly Color StageTrack = Color.FromArgb(58, 70, 62);

    // 状态
    public static readonly Color Success = Color.FromArgb(45, 158, 108);
    public static readonly Color SuccessSoft = Color.FromArgb(230, 245, 237);
    public static readonly Color Warning = Color.FromArgb(178, 118, 26);
    public static readonly Color WarningSoft = Color.FromArgb(251, 244, 230);
    public static readonly Color Danger = Color.FromArgb(191, 70, 59);
    public static readonly Color DangerSoft = Color.FromArgb(251, 236, 234);

    // 尺寸
    public const int ChromeHeight = 44;
    public const int SidebarWidth = 292;
    public const int InspectorWidth = 324;
    public const int Pad = 20;
    public const int Gap = 12;
    public const int ResizeBorder = 6;

    // 圆角
    public const int RadiusPanel = 14;
    public const int RadiusCard = 10;
    public const int RadiusTile = 12;
    public const int RadiusField = 9;
    public const int RadiusButton = 9;

    public static readonly string Family = PickFamily();

    // 字号一律按像素给，不按 pt：整套布局的宽高都是像素常量，字体也必须是像素尺寸，
    // 否则在 125% / 150% 缩放的显示器上字体被 DPI 放大、卡片和按钮不会，文字就会溢出。
    private const GraphicsUnit FontUnit = GraphicsUnit.Pixel;
    public static readonly Font Display = new(Family, 19f, FontStyle.Bold, FontUnit);
    public static readonly Font Title = new(Family, 15f, FontStyle.Bold, FontUnit);
    public static readonly Font Section = new(Family, 13f, FontStyle.Bold, FontUnit);
    public static readonly Font Body = new(Family, 13f, FontStyle.Regular, FontUnit);
    public static readonly Font BodyStrong = new(Family, 13f, FontStyle.Bold, FontUnit);
    public static readonly Font Small = new(Family, 11f, FontStyle.Regular, FontUnit);
    public static readonly Font SmallStrong = new(Family, 11f, FontStyle.Bold, FontUnit);
    public static readonly Font Micro = new(Family, 10f, FontStyle.Regular, FontUnit);
    public static readonly Font MicroStrong = new(Family, 10f, FontStyle.Bold, FontUnit);
    public static readonly Font Mono = new("Consolas", 11f, FontStyle.Regular, FontUnit);

    private static string PickFamily()
    {
        string[] preferred = { "Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "Segoe UI" };
        using var installed = new InstalledFontCollection();
        var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in preferred)
        {
            if (names.Contains(name)) return name;
        }
        return FontFamily.GenericSansSerif.Name;
    }

    public static int Scale(Control control) => control.DeviceDpi <= 0 ? 1 : Math.Max(1, control.DeviceDpi / 96);
}
