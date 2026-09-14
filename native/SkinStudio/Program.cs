using SkinStudio.Ui;

namespace SkinStudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // 像素单位：和 Theme 里的字体一致，布局是像素常量，字体也必须是像素尺寸。
        Application.SetDefaultFont(new Font(Theme.Family, 12f, FontStyle.Regular, GraphicsUnit.Pixel));

        if (args.Contains("--selftest")) return SelfTest.Run();

        var options = new StudioOptions
        {
            Offline = args.Contains("--offline"),
            CapturePath = ValueAfter(args, "--capture"),
            CaptureSize = ParseSize(ValueAfter(args, "--size")),
            Stress = args.Contains("--stress"),
            StressRounds = ParseRounds(ValueAfter(args, "--rounds")),
            NoTrim = args.Contains("--no-trim"),
            StressDwellMs = ParseDwell(ValueAfter(args, "--dwell"))
        };
        Application.Run(new StudioForm(options));
        return 0;
    }

    private static string? ValueAfter(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return null;
        return args[index + 1];
    }

    private static int ParseDwell(string? value) =>
        int.TryParse(value, out var dwell) && dwell >= 0 ? Math.Min(dwell, 10000) : 60;

    private static int ParseRounds(string? value) =>
        int.TryParse(value, out var rounds) && rounds > 0 ? Math.Min(rounds, 20) : 2;

    private static Size ParseSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Size.Empty;
        var parts = value.Split('x', 'X', ',');
        if (parts.Length != 2) return Size.Empty;
        return int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height)
            ? new Size(width, height)
            : Size.Empty;
    }
}
