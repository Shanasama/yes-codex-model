namespace SkinStudio.Core;

internal static class Format
{
    public static string Bytes(long value)
    {
        if (value >= 1024L * 1024 * 1024) return $"{value / 1024d / 1024 / 1024:0.##} GB";
        if (value >= 1024L * 1024) return $"{value / 1024d / 1024:0.#} MB";
        if (value >= 1024) return $"{value / 1024d:0.#} KB";
        return $"{value} B";
    }

    public static string Duration(int milliseconds)
    {
        if (milliseconds <= 0) return "—";
        if (milliseconds < 1000) return $"{milliseconds} ms";
        var seconds = milliseconds / 1000d;
        if (seconds < 60) return $"{seconds:0.#} 秒";
        var minutes = (int)(seconds / 60);
        return $"{minutes} 分 {seconds - minutes * 60:0} 秒";
    }

    public static string TimeAgo(DateTimeOffset? when)
    {
        if (when is null) return "—";
        var delta = DateTimeOffset.Now - when.Value;
        if (delta.TotalSeconds < 60) return "刚刚";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} 分钟前";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} 小时前";
        if (delta.TotalDays < 30) return $"{(int)delta.TotalDays} 天前";
        return when.Value.ToString("yyyy-MM-dd");
    }

    public static string Clock(DateTimeOffset? when) =>
        when?.ToString("MM-dd HH:mm") ?? "—";

    public static string Elide(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= max ? flat : flat[..Math.Max(0, max - 1)] + "…";
    }
}
