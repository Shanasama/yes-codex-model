namespace SkinStudio.Core;

/// <summary>九种动作的定义（与引擎 lib/engine.mjs 中的 STATES 一致）。</summary>
internal static class StateCatalog
{
    public static readonly IReadOnlyList<StateInfo> All = new[]
    {
        new StateInfo { Id = "idle", Label = "待机", Hint = "无任务时持续播放" },
        new StateInfo { Id = "running-right", Label = "向右移动", Hint = "宠物向右移动" },
        new StateInfo { Id = "running-left", Label = "向左移动", Hint = "宠物向左移动" },
        new StateInfo { Id = "waving", Label = "互动", Hint = "招手或回应" },
        new StateInfo { Id = "jumping", Label = "跳跃", Hint = "悬停交互" },
        new StateInfo { Id = "failed", Label = "失败", Hint = "任务失败" },
        new StateInfo { Id = "waiting", Label = "等待", Hint = "等待输入或授权" },
        new StateInfo { Id = "running", Label = "工作中", Hint = "Codex 正在运行" },
        new StateInfo { Id = "review", Label = "审查", Hint = "检查结果" }
    };

    public static string Label(string id) =>
        All.FirstOrDefault(state => state.Id == id)?.Label ?? id;

    public static StateInfo Definition(string id) =>
        All.FirstOrDefault(state => state.Id == id) ?? new StateInfo { Id = id, Label = id };
}
