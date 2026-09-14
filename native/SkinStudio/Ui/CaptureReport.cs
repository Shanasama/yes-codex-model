using System.Text.Json;

namespace SkinStudio.Ui;

/// <summary>把渲染结果转成可核对的数字（无可视化通道时的替代验证手段）。</summary>
internal static class CaptureReport
{
    public static string Analyze(Bitmap bitmap, Rectangle stage, Size client)
    {
        var colors = new HashSet<int>();
        var accentHits = 0;
        var stageLuma = 0d;
        var stageSamples = 0;
        var canvasHits = 0;
        var samples = 0;

        var accent = Theme.Accent;
        var canvas = Theme.Canvas;

        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                var pixel = bitmap.GetPixel(x, y);
                colors.Add((pixel.R << 16) | (pixel.G << 8) | pixel.B);
                samples++;
                if (Math.Abs(pixel.R - accent.R) < 14 && Math.Abs(pixel.G - accent.G) < 14 &&
                    Math.Abs(pixel.B - accent.B) < 14)
                {
                    accentHits++;
                }
                if (Math.Abs(pixel.R - canvas.R) < 3 && Math.Abs(pixel.G - canvas.G) < 3 &&
                    Math.Abs(pixel.B - canvas.B) < 3)
                {
                    canvasHits++;
                }
                if (stage.Contains(x, y))
                {
                    stageLuma += 0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B;
                    stageSamples++;
                }
            }
        }

        var report = new Dictionary<string, object?>
        {
            ["size"] = $"{bitmap.Width}x{bitmap.Height}",
            ["client"] = $"{client.Width}x{client.Height}",
            ["stage"] = $"{stage.X},{stage.Y},{stage.Width}x{stage.Height}",
            ["distinctColors"] = colors.Count,
            ["accentPixels"] = accentHits,
            ["canvasRatio"] = samples == 0 ? 0 : Math.Round(canvasHits / (double)samples, 4),
            ["stageMeanLuma"] = stageSamples == 0 ? -1 : Math.Round(stageLuma / stageSamples, 1),
            ["blank"] = colors.Count < 12
        };
        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }

    public static string Probe(Bitmap bitmap, Rectangle stage)
    {
        var builder = new System.Text.StringBuilder();
        var probes = new (string Label, int X, int Y)[]
        {
            ("chrome", 200, 20),
            ("mark", 30, 22),
            ("sidebarTop", 120, 60),
            ("sidebarList", 80, 150),
            ("stageCenter", Math.Max(0, stage.Left + stage.Width / 2), Math.Max(0, stage.Top + stage.Height / 2)),
            ("inspector", Math.Max(0, bitmap.Width - 120), 200)
        };
        foreach (var (label, x, y) in probes)
        {
            if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height) continue;
            var pixel = bitmap.GetPixel(x, y);
            builder.Append($"{label}=#{pixel.R:X2}{pixel.G:X2}{pixel.B:X2} ");
        }
        return builder.ToString();
    }
}
