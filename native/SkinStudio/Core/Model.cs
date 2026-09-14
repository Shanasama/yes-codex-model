using System.Text.Json;

namespace SkinStudio.Core;

internal sealed class StateInfo
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Hint { get; init; } = "";
    public string File { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public int Frames { get; init; }
    public long Bytes { get; init; }
    public int DurationMs { get; init; }
    public int AverageFrameMs { get; init; }

    public bool Ready => Frames > 0 && Width > 0;
    public double Fps => AverageFrameMs > 0 ? 1000d / AverageFrameMs : 0d;
}

internal sealed class SkinInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Version { get; init; } = "";
    public string AuthorName { get; init; } = "";
    public string License { get; init; } = "";
    public bool BuiltIn { get; init; }
    public bool Valid { get; init; } = true;
    public string? Error { get; init; }
    public long Bytes { get; init; }
    public int Files { get; init; }
    public string? Thumbnail { get; init; }
    public IReadOnlyDictionary<string, StateInfo> States { get; init; } =
        new Dictionary<string, StateInfo>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<StateInfo> OrderedStates { get; init; } = Array.Empty<StateInfo>();

    public int ReadyStates => OrderedStates.Count(s => s.Ready);
    public int TotalStates => OrderedStates.Count;

    public StateInfo? State(string id) =>
        States.TryGetValue(id, out var state) ? state : null;

    /// <summary>错误条目用名称占位，保证列表不会出现空白项。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

internal sealed class RuntimeReport
{
    public string State { get; init; } = "unknown";
    public string Message { get; init; } = "";
    public string Asar { get; init; } = "";
    public string RuntimeProfile { get; init; } = "";
    public bool Ok { get; init; }
    public bool Compatible { get; init; }
    public int HeaderSize { get; init; }
    public bool MainPatched { get; init; }
    public bool RendererPatched { get; init; }
    public bool MainIntegrityOk { get; init; }
    public bool RendererIntegrityOk { get; init; }

    public bool GifPatched => State is "gif-patched" or "gif-patched-legacy";
    public bool Legacy => State == "gif-patched-legacy";
    public bool Found => State != "not-found";

    public string Headline => State switch
    {
        "gif-patched" => "完整动画已开启",
        "gif-patched-legacy" => "完整动画已开启（旧版补丁）",
        "baseline" => "可以开启完整动画",
        "unsupported" => "当前版本不受支持",
        "not-found" => "未找到可写的程序文件",
        _ => "状态未知"
    };

    public string Detail => State switch
    {
        "gif-patched" => "工作与待机动画可以完整循环播放",
        "gif-patched-legacy" => "已启用旧版补丁，建议重新开启一次",
        "baseline" => "开启后工作、等待等动画会完整循环",
        "unsupported" => Trim(Message),
        "not-found" => "可在高级设置中手动指定程序文件位置",
        _ => Trim(Message)
    };

    private static string Trim(string value) =>
        string.IsNullOrWhiteSpace(value) ? "未获取到详细信息" : value.Length > 120 ? value[..120] + "…" : value;
}

internal sealed class ApplyReceipt
{
    public string AppliedAt { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Target { get; init; } = "";
    public string? Backup { get; init; }
    public bool Exists => !string.IsNullOrWhiteSpace(Id);

    public DateTimeOffset? When =>
        DateTimeOffset.TryParse(AppliedAt, out var value) ? value.ToLocalTime() : null;
}

internal sealed class PathsInfo
{
    public string Plugin { get; init; } = "";
    public string Data { get; init; } = "";
    public string Pets { get; init; } = "";
}

internal sealed record Bootstrap
{
    public string Version { get; init; } = "";
    public IReadOnlyList<StateInfo> States { get; init; } = Array.Empty<StateInfo>();
    public IReadOnlyList<SkinInfo> Skins { get; init; } = Array.Empty<SkinInfo>();
    public RuntimeReport Runtime { get; init; } = new();
    public ApplyReceipt LastApply { get; init; } = new();
    public PathsInfo Paths { get; init; } = new();
}

internal static class JsonHelp
{
    public static string Str(this JsonElement element, string name, string fallback = "")
    {
        if (element.ValueKind != JsonValueKind.Object) return fallback;
        if (!element.TryGetProperty(name, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Null or JsonValueKind.Undefined => fallback,
            _ => value.ToString()
        };
    }

    public static string Str(this JsonElement element, string name, JsonElement? parent)
    {
        _ = parent;
        return element.Str(name);
    }

    public static int Int(this JsonElement element, string name, int fallback = 0)
    {
        if (element.ValueKind != JsonValueKind.Object) return fallback;
        if (!element.TryGetProperty(name, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : (int)Math.Round(value.GetDouble()),
            JsonValueKind.String => int.TryParse(value.GetString(), out var parsed) ? parsed : fallback,
            _ => fallback
        };
    }

    public static long Long(this JsonElement element, string name, long fallback = 0)
    {
        if (element.ValueKind != JsonValueKind.Object) return fallback;
        if (!element.TryGetProperty(name, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt64(out var number) ? number : (long)Math.Round(value.GetDouble()),
            JsonValueKind.String => long.TryParse(value.GetString(), out var parsed) ? parsed : fallback,
            _ => fallback
        };
    }

    public static bool Bool(this JsonElement element, string name, bool fallback = false)
    {
        if (element.ValueKind != JsonValueKind.Object) return fallback;
        if (!element.TryGetProperty(name, out var value)) return fallback;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback
        };
    }

    public static JsonElement? Obj(this JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.Object ? value : null;
    }

    public static JsonElement? Arr(this JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(name, out var value)) return null;
        return value.ValueKind == JsonValueKind.Array ? value : null;
    }
}
