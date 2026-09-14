namespace SkinStudio.Core;

/// <summary>离线模式与自检用的样例数据，不读取本机皮肤库。</summary>
internal static class SampleData
{
    public static Bootstrap Build()
    {
        var skins = new List<SkinInfo>
        {
            Make("winefox-pixel-classic", "原版酒狐", "酒石酸君", true, 32694523, 267, 512, 512),
            Make("winefox-snow", "雪狐", "酒石酸君", false, 31619141, 240, 512, 512),
            Make("studio-demo", "Studio Demo", "Skin Engine", false, 18432210, 168, 768, 768)
        };

        return new Bootstrap
        {
            Version = "1.0.0",
            States = StateCatalog.All,
            Skins = skins,
            Runtime = new RuntimeReport
            {
                State = "baseline",
                Ok = false,
                Compatible = true,
                RuntimeProfile = "codex-26.908.4834",
                Asar = @"%LOCALAPPDATA%\OpenAI\Codex\skin-engine\shadow-runtimes\store-26.908.4834.0\app\resources\app.asar"
            },
            LastApply = new ApplyReceipt
            {
                AppliedAt = DateTimeOffset.Now.AddMinutes(-42).ToString("O"),
                Id = "winefox-pixel-classic",
                Name = "原版酒狐",
                Version = "1.0.0",
                Target = @"%USERPROFILE%\.codex\pets\winefox-pixel-classic",
                Backup = @"%USERPROFILE%\.codex\skin-engine\backups\pets\winefox-pixel-classic\20260914-155519"
            },
            Paths = new PathsInfo
            {
                Plugin = @"%USERPROFILE%\codex-skin-engine\plugins\codex-skin-engine",
                Data = @"%USERPROFILE%\.codex\skin-engine",
                Pets = @"%USERPROFILE%\.codex\pets"
            }
        };
    }

    private static SkinInfo Make(string id, string name, string author, bool builtIn, long bytes,
        int frames, int width, int height)
    {
        var states = new Dictionary<string, StateInfo>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<StateInfo>();
        foreach (var definition in StateCatalog.All)
        {
            var framesForState = definition.Id == "running" ? Math.Max(24, frames / 6) : frames;
            var state = new StateInfo
            {
                Id = definition.Id,
                Label = definition.Label,
                Hint = definition.Hint,
                File = $"animations/{definition.Id}.gif",
                Sha256 = new string('a', 64),
                Width = width,
                Height = height,
                Frames = framesForState,
                Bytes = bytes / 9,
                DurationMs = framesForState * 40,
                AverageFrameMs = 40
            };
            states[state.Id] = state;
            ordered.Add(state);
        }

        return new SkinInfo
        {
            Id = id,
            Name = name,
            Description = $"{name} 的样例说明文字，用于离线预览与布局自检。",
            Version = "1.0.0",
            AuthorName = author,
            License = "MIT",
            BuiltIn = builtIn,
            Valid = true,
            Bytes = bytes,
            Files = 12,
            Thumbnail = "animations/idle.gif",
            States = states,
            OrderedStates = ordered
        };
    }
}
