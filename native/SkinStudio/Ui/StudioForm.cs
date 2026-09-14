using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkinStudio.Core;

namespace SkinStudio.Ui;

internal sealed class StudioOptions
{
    public bool Offline { get; init; }
    public string? CapturePath { get; init; }
    public Size CaptureSize { get; init; }
    public bool Stress { get; init; }
    public int StressRounds { get; init; } = 2;
    public int StressDwellMs { get; init; } = 60;
    public bool NoTrim { get; init; }
}

/// <summary>工坊主窗口：无边框自绘外壳 + 三栏布局。</summary>
internal sealed class StudioForm : Form
{
    public const int HtClient = 1;
    private const int HtLeft = 10, HtRight = 11, HtTop = 12, HtTopLeft = 13, HtTopRight = 14;
    private const int HtBottom = 15, HtBottomLeft = 16, HtBottomRight = 17;

    private readonly StudioOptions _options;
    private readonly EngineClient _engine = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _thumbGate = new(2);
    private readonly Dictionary<string, Image> _thumbCache = new();
    private readonly Dictionary<string, byte[]> _byteCache = new();
    private readonly List<string> _byteOrder = new();
    private readonly List<SkinListItem> _items = new();
    private readonly Dictionary<string, StateTile> _tiles = new();
    private readonly HashSet<string> _pendingThumbs = new();

    private Bootstrap _model = new();
    private SkinInfo? _selected;
    private string _stateId = "idle";
    private int _loadToken;
    private string _engineNote = "正在连接本地引擎…";
    private Color _engineColor = Theme.Muted;
    private bool _engineBusy = true;
    private bool _advancedOpen;
    private bool _headerCompact;
    private string _query = "";
    private bool _busy;

    // 顶部标题栏
    private readonly Panel _chrome = new();
    private readonly BrandMark _mark = new();
    private readonly StudioLabel _brandTitle = new();
    private readonly StudioLabel _brandSub = new();
    private readonly WindowButton _minimize = new() { Action = WindowAction.Minimize };
    private readonly WindowButton _maximize = new() { Action = WindowAction.Maximize };
    private readonly WindowButton _close = new() { Action = WindowAction.Close };

    // 左侧皮肤库
    private readonly Panel _sidebar = new();
    private readonly Field _search = new();
    private readonly StudioLabel _libraryLabel = new();
    private readonly Chip _countChip = new();
    private readonly StudioButton _importButton = new();
    private readonly ScrollHost _listHost = new();
    private readonly Panel _listContent = new();
    private readonly StudioLabel _listEmpty = new();
    private readonly StatusDot _engineDot = new();
    private readonly StudioLabel _engineStatus = new();
    private readonly StudioButton _reconnect = new();
    private readonly IconButton _openData = new();

    // 中间：预览与动作
    private readonly Panel _center = new();
    private readonly StudioLabel _title = new();
    private readonly StudioLabel _subtitle = new();
    private readonly StudioButton _copyButton = new();
    private readonly StudioButton _exportButton = new();
    private readonly StudioButton _applyButton = new();
    private readonly PreviewStage _stage = new();
    private readonly IconButton _playButton = new();
    private readonly IconButton _fitButton = new();
    private readonly StudioLabel _tilesLabel = new();
    private readonly StudioLabel _tilesHint = new();
    private readonly Chip _tilesChip = new();
    private readonly StudioButton _replaceButton = new();
    private readonly Panel _tilesPanel = new();

    // 右侧检查器
    private readonly Panel _inspector = new();
    private readonly ScrollHost _inspectorHost = new();
    private readonly Panel _inspectorContent = new();
    private readonly StudioLabel _infoLabel = new();
    private readonly Chip _builtInChip = new();
    private readonly Field _nameField = new();
    private readonly Field _authorField = new();
    private readonly Field _descField = new(true);
    private readonly StudioButton _saveMeta = new();
    private readonly Panel _divider1 = new();
    private readonly StudioLabel _runtimeLabel = new();
    private readonly Chip _runtimeChip = new();
    private readonly StudioLabel _runtimeHeadline = new();
    private readonly StudioLabel _runtimeDetail = new() { WrapText = true };
    private readonly StudioButton _runtimeAction = new();
    private readonly StudioButton _advancedToggle = new();
    private readonly StudioLabel _lowPowerLabel = new();
    private readonly Toggle _lowPower = new();
    private readonly Field _runtimePath = new();
    private readonly StudioButton _runtimeRecheck = new();
    private readonly StudioButton _runtimeRestore = new();
    private readonly StudioLabel _runtimeTechnical = new() { WrapText = true };
    private readonly Panel _divider2 = new();
    private readonly StudioLabel _applyLabel = new();
    private readonly StudioLabel _applyReceipt = new() { WrapText = true };
    private readonly StudioLabel _applyBackup = new() { WrapText = true };
    private readonly StudioLabel _applyHint = new() { WrapText = true };
    private readonly StudioButton _openPets = new();

    public StudioForm(StudioOptions options)
    {
        _options = options;
        SuspendLayout();

        Text = "Codex 皮肤工坊";
        FormBorderStyle = FormBorderStyle.None;
        // 布局全部由 LayoutAll 按像素常量算，禁止 WinForms 再按 DPI / 字体自动缩放一遍，
        // 否则高 DPI 显示器上字体被放大、容器不会，文字就溢出控件。
        AutoScaleMode = AutoScaleMode.None;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* 使用默认图标 */ }
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Canvas;
        Font = Theme.Body;
        KeyPreview = true;
        DoubleBuffered = true;
        MinimumSize = new Size(1080, 680);

        var working = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        var width = _options.CaptureSize.Width > 0
            ? _options.CaptureSize.Width
            : Math.Min(1280, Math.Max(1080, working.Width - 120));
        var height = _options.CaptureSize.Height > 0
            ? _options.CaptureSize.Height
            : Math.Min(850, Math.Max(700, working.Height - 120));
        ClientSize = new Size(width, height);

        BuildChrome();
        BuildSidebar();
        BuildCenter();
        BuildInspector();

        Controls.Add(_chrome);
        Controls.Add(_sidebar);
        Controls.Add(_center);
       Controls.Add(_inspector);

       BuildStateTiles();
        WireEvents();

        ResumeLayout(true);
        LayoutAll();
    }

    // ---------------------------------------------------------------- 构建

    private void BuildChrome()
    {
        _chrome.BackColor = Theme.Chrome;
        _brandTitle.Text = "Codex 皮肤工坊";
        _brandTitle.Font = Theme.Section;
        _brandTitle.ForeColor = Theme.Ink;
        _brandTitle.BackColor = Theme.Chrome;
        _brandSub.Text = "SKIN STUDIO";
        _brandSub.Font = Theme.Micro;
        _brandSub.ForeColor = Theme.Faint;
        _brandSub.BackColor = Theme.Chrome;

        _chrome.Controls.AddRange(new Control[] { _mark, _brandTitle, _brandSub, _minimize, _maximize, _close });
       _chrome.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) RoundedCorners.Drag(this); };
        _chrome.Paint += (_, e) => Painter.Hairline(e.Graphics, 0, _chrome.Width, _chrome.Height - 1, Theme.Line);
    }

    private void BuildSidebar()
    {
        _sidebar.BackColor = Theme.Sidebar;

        _search.Placeholder = "搜索皮肤名称或作者";
        _search.BackColor = Theme.Sidebar;
        _search.Font = Theme.Body;

        _libraryLabel.Text = "皮肤库";
        _libraryLabel.Font = Theme.Section;
        _libraryLabel.BackColor = Theme.Sidebar;

        _countChip.BackColor = Theme.Sidebar;
        _countChip.Fill = Theme.SurfaceMuted;
        _countChip.Ink = Theme.Muted;
        _countChip.Text = "0";

        _importButton.Text = "导入";
        _importButton.Icon = Glyph.Import;
        _importButton.Variant = ButtonVariant.Quiet;
        _importButton.BackColor = Theme.Sidebar;
        _importButton.Size = new Size(80, 30);

        _listContent.BackColor = Theme.Sidebar;
        _listEmpty.Text = "还没有可用的皮肤";
        _listEmpty.Font = Theme.Small;
        _listEmpty.ForeColor = Theme.Faint;
        _listEmpty.BackColor = Theme.Sidebar;
        _listEmpty.Align = ContentAlignment.MiddleCenter;
        _listEmpty.Visible = false;
        _listContent.Controls.Add(_listEmpty);

        _listHost.Content = _listContent;
        _listHost.BackColor = Theme.Sidebar;
        _listHost.Padding = Padding.Empty;

        _engineDot.DotColor = Theme.Muted;
        _engineDot.BackColor = Theme.Sidebar;
        _engineStatus.Text = _engineNote;
        _engineStatus.Font = Theme.Small;
        _engineStatus.ForeColor = Theme.Muted;
        _engineStatus.BackColor = Theme.Sidebar;

        _reconnect.Text = "重连";
        _reconnect.Variant = ButtonVariant.Quiet;
        _reconnect.BackColor = Theme.Sidebar;
        _reconnect.Size = new Size(60, 28);
        _reconnect.Visible = false;

        _openData.Icon = Glyph.Folder;
        _openData.BackColor = Theme.Sidebar;
        _openData.IconColor = Theme.Muted;

        _sidebar.Controls.AddRange(new Control[]
        {
            _search, _libraryLabel, _countChip, _importButton, _listHost,
           _engineDot, _engineStatus, _reconnect, _openData
       });
        _sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Line, 1f);
            e.Graphics.DrawLine(pen, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
        };
    }

    private void BuildCenter()
    {
        _center.BackColor = Theme.Canvas;

        _title.Font = Theme.Display;
        _title.BackColor = Theme.Canvas;
        _title.Text = "正在载入皮肤库";
        _subtitle.Font = Theme.Small;
        _subtitle.ForeColor = Theme.Muted;
        _subtitle.BackColor = Theme.Canvas;
        _subtitle.Text = "读取本机皮肤与动画状态";

        _copyButton.Text = "复制";
        _copyButton.Icon = Glyph.Copy;
        _copyButton.Variant = ButtonVariant.Secondary;
        _copyButton.BackColor = Theme.Canvas;
        _copyButton.Size = new Size(84, 34);

        _exportButton.Text = "导出";
        _exportButton.Icon = Glyph.Export;
        _exportButton.Variant = ButtonVariant.Secondary;
        _exportButton.BackColor = Theme.Canvas;
        _exportButton.Size = new Size(84, 34);

        _applyButton.Text = "应用到 Codex";
        _applyButton.Icon = Glyph.Check;
        _applyButton.Variant = ButtonVariant.Primary;
        _applyButton.BackColor = Theme.Canvas;
        _applyButton.Size = new Size(126, 34);

        _playButton.Icon = Glyph.Pause;
       _playButton.Dark = true;
        _playButton.BackColor = Theme.StageTop;
       _playButton.FillAlways = true;
        _playButton.Size = new Size(30, 30);
        _fitButton.Icon = Glyph.Fit;
       _fitButton.Dark = true;
        _fitButton.BackColor = Theme.StageTop;
       _fitButton.FillAlways = true;
        _fitButton.Size = new Size(30, 30);

        _stage.BackColor = Theme.Canvas;
        _stage.ShowMessage("正在连接本地皮肤引擎…", true);
        _stage.Controls.Add(_playButton);
        _stage.Controls.Add(_fitButton);

        _tilesLabel.Text = "动作";
        _tilesLabel.Font = Theme.Section;
        _tilesLabel.BackColor = Theme.Canvas;
        _tilesHint.Text = "选择动作预览，或把 GIF 拖进画布替换";
        _tilesHint.Font = Theme.Small;
        _tilesHint.ForeColor = Theme.Muted;
        _tilesHint.BackColor = Theme.Canvas;

        _tilesChip.BackColor = Theme.Canvas;
        _tilesChip.Text = "0 / 9 可用";

        _replaceButton.Text = "替换当前动作";
        _replaceButton.Icon = Glyph.Refresh;
        _replaceButton.Variant = ButtonVariant.Secondary;
        _replaceButton.BackColor = Theme.Canvas;
        _replaceButton.Size = new Size(134, 30);

        _tilesPanel.BackColor = Theme.Canvas;

        _center.Controls.AddRange(new Control[]
        {
            _title, _subtitle, _copyButton, _exportButton, _applyButton,
            _stage, _tilesLabel, _tilesHint, _tilesChip, _replaceButton, _tilesPanel
        });
    }

    private void BuildInspector()
    {
        _inspector.BackColor = Theme.Sidebar;
        _inspectorContent.BackColor = Theme.Sidebar;
        _inspectorHost.Content = _inspectorContent;
        _inspectorHost.BackColor = Theme.Sidebar;

        _infoLabel.Text = "皮肤信息";
        _infoLabel.Font = Theme.Section;
        _infoLabel.BackColor = Theme.Sidebar;

        _builtInChip.BackColor = Theme.Sidebar;
        _builtInChip.Fill = Theme.SurfaceMuted;
        _builtInChip.Ink = Theme.Muted;
        _builtInChip.Text = "自带皮肤";

        _nameField.Placeholder = "皮肤名称";
        _nameField.BackColor = Theme.Sidebar;
        _nameField.MaxLength = 80;
        _authorField.Placeholder = "作者";
        _authorField.BackColor = Theme.Sidebar;
        _authorField.MaxLength = 80;
        _descField.Placeholder = "说明（写在 pet.json 里，会显示在 Codex 中）";
        _descField.BackColor = Theme.Sidebar;
        _descField.MaxLength = 500;

        _saveMeta.Text = "保存信息";
        _saveMeta.Variant = ButtonVariant.Secondary;
        _saveMeta.BackColor = Theme.Sidebar;
        _saveMeta.Stretch = true;

        _divider1.BackColor = Theme.Line;
        _divider2.BackColor = Theme.Line;

        _runtimeLabel.Text = "完整动画";
        _runtimeLabel.Font = Theme.Section;
        _runtimeLabel.BackColor = Theme.Sidebar;
        _runtimeChip.BackColor = Theme.Sidebar;
        _runtimeChip.Text = "检查中";

        _runtimeHeadline.Font = Theme.BodyStrong;
        _runtimeHeadline.BackColor = Theme.Sidebar;
        _runtimeHeadline.Text = "正在检查动画设置";
        _runtimeDetail.Font = Theme.Small;
        _runtimeDetail.ForeColor = Theme.Muted;
        _runtimeDetail.BackColor = Theme.Sidebar;
        _runtimeDetail.Text = "确认工作、等待动画能否完整循环。";

        _runtimeAction.Text = "一键开启完整动画";
        _runtimeAction.Variant = ButtonVariant.Primary;
        _runtimeAction.BackColor = Theme.Sidebar;
        _runtimeAction.Stretch = true;

        _advancedToggle.Text = "高级设置";
        _advancedToggle.Icon = Glyph.ChevronRight;
        _advancedToggle.Variant = ButtonVariant.Quiet;
        _advancedToggle.BackColor = Theme.Sidebar;
        _advancedToggle.Height = 26;
        _advancedToggle.Font = Theme.Small;

        _lowPowerLabel.Text = "低性能模式：预览只显示首帧";
        _lowPowerLabel.Font = Theme.Small;
        _lowPowerLabel.ForeColor = Theme.Muted;
        _lowPowerLabel.BackColor = Theme.Sidebar;
        _lowPower.BackColor = Theme.Sidebar;

        _runtimePath.Placeholder = "程序文件位置（一般留空，自动查找）";
        _runtimePath.Monospace = true;
        _runtimePath.BackColor = Theme.Sidebar;
        _runtimePath.Height = 34;

        _runtimeRecheck.Text = "重新检查";
        _runtimeRecheck.Variant = ButtonVariant.Secondary;
        _runtimeRecheck.BackColor = Theme.Sidebar;
        _runtimeRestore.Text = "恢复兼容模式";
        _runtimeRestore.Variant = ButtonVariant.Secondary;
        _runtimeRestore.BackColor = Theme.Sidebar;

        _runtimeTechnical.Font = Theme.Micro;
        _runtimeTechnical.ForeColor = Theme.Faint;
        _runtimeTechnical.BackColor = Theme.Sidebar;
        _runtimeTechnical.Text = "正在自动查找程序文件";

        _applyLabel.Text = "应用状态";
        _applyLabel.Font = Theme.Section;
        _applyLabel.BackColor = Theme.Sidebar;
        _applyReceipt.Font = Theme.Small;
        _applyReceipt.ForeColor = Theme.InkSoft;
        _applyReceipt.BackColor = Theme.Sidebar;
        _applyBackup.Font = Theme.Micro;
        _applyBackup.ForeColor = Theme.Faint;
        _applyBackup.BackColor = Theme.Sidebar;

        _applyHint.Font = Theme.Small;
        _applyHint.ForeColor = Theme.AccentInk;
        _applyHint.BackColor = Theme.Sidebar;
        _applyHint.Text = "应用完还要完全退出并重启 Codex，再到 Codex 设置 → 宠物 → 自定义宠物 里选中它，皮肤才会显示。";

        _openPets.Text = "打开宠物目录";
        _openPets.Icon = Glyph.Folder;
        _openPets.Variant = ButtonVariant.Quiet;
        _openPets.BackColor = Theme.Sidebar;
        _openPets.Height = 28;
        _openPets.Font = Theme.Small;

        _inspectorContent.Controls.AddRange(new Control[]
        {
            _infoLabel, _builtInChip, _nameField, _authorField, _descField, _saveMeta, _divider1,
            _runtimeLabel, _runtimeChip, _runtimeHeadline, _runtimeDetail, _runtimeAction, _advancedToggle,
            _lowPowerLabel, _lowPower, _runtimePath, _runtimeRecheck, _runtimeRestore, _runtimeTechnical,
            _divider2, _applyLabel, _applyReceipt, _applyBackup, _applyHint, _openPets
        });

       _inspector.Controls.Add(_inspectorHost);
        _inspector.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Line, 1f);
            e.Graphics.DrawLine(pen, 0.5f, 0, 0.5f, _inspector.Height);
        };
    }

    private void BuildStateTiles()
    {
        _tilesPanel.Controls.Clear();
        _tiles.Clear();
        foreach (var definition in StateCatalog.All)
        {
            var tile = new StateTile(definition) { BackColor = Theme.Canvas };
            tile.Click += (_, _) => SelectState(definition.Id);
            tile.ReplaceRequested += state => _ = ReplaceStateAsync(state.Id);
            _tiles[definition.Id] = tile;
            _tilesPanel.Controls.Add(tile);
        }
    }

    private void WireEvents()
    {
        _minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _maximize.Click += (_, _) => ToggleMaximize();
        _close.Click += (_, _) => Close();

        _search.Input.TextChanged += (_, _) =>
        {
            _query = _search.Value.Trim();
            RenderSkinList();
        };

        _importButton.Click += (_, _) => _ = ImportAsync();
        _reconnect.Click += (_, _) => _ = ReconnectAsync();
        _openData.Click += (_, _) => OpenFolder(_model.Paths.Data);

        _copyButton.Click += (_, _) => _ = DuplicateAsync();
        _exportButton.Click += (_, _) => _ = ExportAsync();
        _applyButton.Click += (_, _) => _ = ApplyAsync();

        _playButton.Click += (_, _) => { _stage.SetPlaying(!_stage.Playing); UpdatePlayButton(); };
        _fitButton.Click += (_, _) => { _stage.SetActualSize(!_stage.ActualSize); UpdateFitButton(); };
        _stage.GifDropped += path => _ = ReplaceStateAsync(_stateId, path);

        _replaceButton.Click += (_, _) => _ = ReplaceStateAsync(_stateId);

        _saveMeta.Click += (_, _) => _ = SaveMetaAsync();
        _runtimeAction.Click += (_, _) => _ = RuntimePrimaryAsync();
        _advancedToggle.Click += (_, _) => { _advancedOpen = !_advancedOpen; UpdateAdvanced(); LayoutInspector(); };
        _runtimeRecheck.Click += (_, _) => _ = RecheckRuntimeAsync();
        _runtimeRestore.Click += (_, _) => _ = RestoreRuntimeAsync();
        _lowPower.CheckedChanged += value =>
        {
            _stage.SetLowPower(value);
            if (!value) _ = LoadStateAsync(_selected, _stateId);
        };
        _openPets.Click += (_, _) => OpenFolder(_model.Paths.Pets);

        Shown += async (_, _) =>
        {
            LayoutAll();
            await StartAsync();
            if (_options.Stress) await RunStressAsync();
            if (_options.CapturePath is not null) await CaptureAsync();
        };

        Resize += (_, _) => { LayoutAll(); UpdateMaximizeIcon(); };
        FormClosed += (_, _) =>
        {
            _lifetime.Cancel();
            _engine.Dispose();
            _lifetime.Dispose();
        };
    }

    // ---------------------------------------------------------------- 布局

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_chrome.Controls.Count > 0) LayoutAll();
    }

    public void LayoutAll()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        var chromeHeight = Theme.ChromeHeight;
        _chrome.SetBounds(0, 0, ClientSize.Width, chromeHeight);
        _mark.SetBounds(18, (chromeHeight - 26) / 2, 26, 26);
        _brandTitle.SetBounds(54, 9, Math.Max(120, ClientSize.Width - 260), 18);
        _brandSub.SetBounds(54, 26, 220, 13);
        _close.SetBounds(ClientSize.Width - 46, 0, 46, chromeHeight);
        _maximize.SetBounds(_close.Left - 46, 0, 46, chromeHeight);
        _minimize.SetBounds(_maximize.Left - 46, 0, 46, chromeHeight);

        var bodyTop = chromeHeight;
        var bodyHeight = Math.Max(80, ClientSize.Height - chromeHeight);
        var sidebarWidth = Theme.SidebarWidth;
        var inspectorWidth = Theme.InspectorWidth;
        var minimumCenter = 420;
        if (ClientSize.Width - sidebarWidth - inspectorWidth < minimumCenter)
        {
            var extra = minimumCenter - (ClientSize.Width - sidebarWidth - inspectorWidth);
            sidebarWidth = Math.Max(232, sidebarWidth - extra / 2);
            inspectorWidth = Math.Max(268, inspectorWidth - extra / 2);
        }

        _sidebar.SetBounds(0, bodyTop, sidebarWidth, bodyHeight);
        _inspector.SetBounds(ClientSize.Width - inspectorWidth, bodyTop, inspectorWidth, bodyHeight);
        _center.SetBounds(_sidebar.Right, bodyTop,
            Math.Max(minimumCenter - 40, _inspector.Left - _sidebar.Right), bodyHeight);

       LayoutSidebar();
       LayoutCenter();
       LayoutInspector();
    }

    private void LayoutSidebar()
    {
        var pad = 16;
        var width = Math.Max(120, _sidebar.Width - pad * 2);
        var y = pad;

        _search.SetBounds(pad, y, width, 36);
        y += 36 + 14;

        _libraryLabel.SetBounds(pad, y + 4, 70, 20);
        _countChip.SetBounds(pad + 74, y + 2, _countChip.PreferredWidth, 22);
        _importButton.SetBounds(pad + width - 80, y - 2, 80, 30);
        y += 36;

        var footerTop = _sidebar.Height - 48;
        var listHeight = Math.Max(80, footerTop - y - 12);
        _listHost.SetBounds(pad, y, width, listHeight);
        _listContent.Width = width;
        _listEmpty.SetBounds(0, 0, width, 60);

       _engineDot.SetBounds(pad, footerTop + 10, 10, 10);
        var folderX = pad + width - 30;
        var reconnectX = folderX - 68;
        var statusWidth = Math.Max(40, reconnectX - 8 - (pad + 18));
        _engineStatus.SetBounds(pad + 18, footerTop + 4, statusWidth, 22);
        _openData.SetBounds(folderX, footerTop + 3, 30, 30);
        _reconnect.SetBounds(reconnectX, footerTop + 4, 62, 28);

       LayoutListItems();
    }

    private void LayoutCenter()
    {
        var pad = Theme.Pad;
        var inner = Math.Max(200, _center.Width - pad * 2);
        const int headerHeight = 52;
        const int gap = 16;
        const int headingHeight = 22;
        const int headingGap = 10;
        const int tileGap = 10;
        const int previewMinimum = 176;

        var top = pad - 2;
        ApplyHeaderCompact(inner < 560);

        var right = pad + inner;
        _applyButton.SetBounds(right - _applyButton.Width, top + 4, _applyButton.Width, 34);
        _exportButton.SetBounds(_applyButton.Left - 8 - _exportButton.Width, top + 4, _exportButton.Width, 34);
        _copyButton.SetBounds(_exportButton.Left - 8 - _copyButton.Width, top + 4, _copyButton.Width, 34);

        var titleWidth = Math.Max(80, _copyButton.Left - 12 - pad);
        _title.SetBounds(pad, top, titleWidth, 26);
        _subtitle.SetBounds(pad, top + 27, titleWidth, 18);

        var bodyTop = top + headerHeight + gap;
        var bodyBottom = _center.Height - pad;
        var gridMaximum = bodyBottom - bodyTop - previewMinimum - gap - headingHeight - headingGap;
        var tileHeight = Math.Clamp((gridMaximum - tileGap * 2) / 3, 70, 96);
        var gridHeight = tileHeight * 3 + tileGap * 2;
        var tilesTop = bodyBottom - (headingHeight + headingGap + gridHeight);
        var previewHeight = Math.Max(140, tilesTop - gap - bodyTop);

        _stage.SetBounds(pad, bodyTop, inner, previewHeight);
        _playButton.SetBounds(_stage.Width - 42, 14, 30, 30);
        _fitButton.SetBounds(_stage.Width - 78, 14, 30, 30);

        _tilesLabel.SetBounds(pad, tilesTop + 2, 52, 20);
        _tilesHint.SetBounds(pad + 64, tilesTop + 3, Math.Max(40, inner - 328), 18);
        _replaceButton.SetBounds(pad + inner - _replaceButton.Width, tilesTop - 3, _replaceButton.Width, 30);
        _tilesChip.SetBounds(_replaceButton.Left - 10 - _tilesChip.PreferredWidth, tilesTop - 1, _tilesChip.PreferredWidth, 22);

        _tilesPanel.SetBounds(pad, tilesTop + headingHeight + headingGap, inner, gridHeight);
        var columns = 3;
        var tileWidth = (inner - tileGap * (columns - 1)) / columns;
        var index = 0;
        foreach (var tile in _tiles.Values)
        {
            var row = index / columns;
            var column = index % columns;
            tile.SetBounds(column * (tileWidth + tileGap), row * (tileHeight + tileGap), tileWidth, tileHeight);
            index++;
        }
    }

    private void ApplyHeaderCompact(bool compact)
    {
        if (_headerCompact == compact) return;
        _headerCompact = compact;
        _copyButton.Text = compact ? "" : "复制";
        _exportButton.Text = compact ? "" : "导出";
        _applyButton.Text = compact ? "" : "应用到 Codex";
        _copyButton.Size = new Size(compact ? 36 : 84, 34);
        _exportButton.Size = new Size(compact ? 36 : 84, 34);
        _applyButton.Size = new Size(compact ? 42 : 126, 34);
    }

    private void LayoutInspector()
    {
        var pad = Theme.Pad - 2;
        var inner = Math.Max(160, _inspector.Width - pad * 2);
        var y = pad - 4;

        _infoLabel.SetBounds(pad, y, 120, 20);
        _builtInChip.SetBounds(pad + inner - _builtInChip.PreferredWidth, y - 1, _builtInChip.PreferredWidth, 22);
        y += 30;

        _nameField.SetBounds(pad, y, inner, 34);
        y += 34 + 8;
        _authorField.SetBounds(pad, y, inner, 34);
        y += 34 + 8;
        _descField.SetBounds(pad, y, inner, 76);
        y += 76 + 10;
        _saveMeta.SetBounds(pad, y, inner, 34);
        y += 34 + 16;

        _divider1.SetBounds(pad, y, inner, 1);
        y += 17;

        _runtimeLabel.SetBounds(pad, y + 1, 100, 20);
        _runtimeChip.SetBounds(pad + inner - _runtimeChip.PreferredWidth, y, _runtimeChip.PreferredWidth, 22);
        y += 30;

        _runtimeHeadline.SetBounds(pad, y, inner, 20);
        y += 22;
        _runtimeDetail.SetBounds(pad, y, inner, 34);
        y += 38;
        _runtimeAction.SetBounds(pad, y, inner, 34);
        y += 34 + 8;
        _advancedToggle.SetBounds(pad, y, 110, 26);
        y += 26 + 6;

        _lowPowerLabel.Visible = _advancedOpen;
        _lowPower.Visible = _advancedOpen;
        _runtimePath.Visible = _advancedOpen;
        _runtimeRecheck.Visible = _advancedOpen;
        _runtimeRestore.Visible = _advancedOpen;
        _runtimeTechnical.Visible = _advancedOpen;

        if (_advancedOpen)
        {
            _lowPowerLabel.SetBounds(pad, y + 2, inner - 46, 20);
            _lowPower.SetBounds(pad + inner - 38, y, 38, 22);
            y += 30;
            _runtimePath.SetBounds(pad, y, inner, 34);
            y += 34 + 8;
            var half = (inner - 8) / 2;
            _runtimeRecheck.SetBounds(pad, y, half, 32);
            _runtimeRestore.SetBounds(pad + half + 8, y, inner - half - 8, 32);
            y += 32 + 8;
            _runtimeTechnical.SetBounds(pad, y, inner, 42);
            y += 46;
        }

        _divider2.SetBounds(pad, y, inner, 1);
        y += 17;
        _applyLabel.SetBounds(pad, y, 120, 20);
        y += 26;
        _applyReceipt.SetBounds(pad, y, inner, 36);
        y += 38;
        _applyBackup.SetBounds(pad, y, inner, 32);
        y += 34;
        _applyHint.SetBounds(pad, y, inner, 46);
        y += 48;
        _openPets.SetBounds(pad, y, 136, 28);
        y += 28 + pad;

        _inspectorContent.Height = Math.Max(y, _inspector.Height);
        _inspectorContent.Width = _inspector.Width;
        _inspectorHost.SetBounds(0, 0, _inspector.Width, _inspector.Height);
    }

    private void LayoutListItems()
    {
        var y = 4;
        var width = _listHost.Width - (_listHost.MaxOffset > 0 ? 10 : 0);
        foreach (var item in _items)
        {
            item.SetBounds(0, y, Math.Max(80, width), 68);
            y += 76;
        }
        _listEmpty.SetBounds(0, 4, Math.Max(80, width), 60);
        _listContent.Height = Math.Max(_listHost.Height, y);
        _listContent.Width = Math.Max(80, width);
    }

    // ---------------------------------------------------------------- 启动

    private async Task StartAsync()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ShowToast(args.ExceptionObject is Exception error ? error.Message : "发生未知错误", true);

        try
        {
            if (_options.Offline)
            {
                _model = SampleData.Build();
                SetEngineState("离线预览模式", Theme.Warning, false);
            }
            else
            {
                SetEngineState("正在连接本地引擎…", Theme.Warning, true);
                var ok = await _engine.EnsureServerAsync(note => SetEngineState(note, Theme.Warning, true), _lifetime.Token);
                if (ok)
                {
                    _model = await _engine.BootstrapAsync(_lifetime.Token);
                    var note = _engine.ReusedExistingServer ? "本地引擎已连接" : "本地引擎已启动";
                    SetEngineState(note, Theme.Success, false);
                }
                else
                {
                    SetEngineState(_engine.LastEngineNote, Theme.Danger, false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error)
        {
            SetEngineState(error.Message, Theme.Danger, false);
        }

        _busy = false;
        var first = _model.Skins.FirstOrDefault(skin => skin.Id == _model.LastApply.Id && skin.Valid)
                    ?? _model.Skins.FirstOrDefault(skin => skin.Valid)
                    ?? _model.Skins.FirstOrDefault();
        _selected = first;
        _stateId = _selected?.OrderedStates.FirstOrDefault()?.Id ?? "idle";

        RenderSkinList();
        await RenderSelectionAsync();
        RenderRuntime();
        RenderReceipt();

        _ready.TrySetResult();
    }

    private void SetEngineState(string note, Color color, bool busy)
    {
        _engineNote = note;
        _engineColor = color;
        _engineBusy = busy;
        _engineStatus.Text = note;
        _engineStatus.ForeColor = busy ? Theme.Muted : color == Theme.Success ? Theme.InkSoft : color;
        _engineDot.DotColor = color;
        _engineDot.SetPulse(busy);
        _reconnect.Visible = !busy && color != Theme.Success;
    }

    // ---------------------------------------------------------------- 渲染

    private void RenderSkinList()
    {
        foreach (var item in _items)
        {
            _listContent.Controls.Remove(item);
            item.Dispose();
        }
        _items.Clear();

        var skins = _model.Skins
            .Where(skin => string.IsNullOrWhiteSpace(_query)
                           || skin.DisplayName.Contains(_query, StringComparison.OrdinalIgnoreCase)
                           || skin.Id.Contains(_query, StringComparison.OrdinalIgnoreCase)
                           || skin.AuthorName.Contains(_query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var skin in skins)
        {
            var item = new SkinListItem(skin)
            {
                BackColor = Theme.Sidebar,
                Selected = skin.Id == _selected?.Id,
                Applied = skin.Id == _model.LastApply.Id
            };
            item.Click += (_, _) => SelectSkin(skin.Id);
            _items.Add(item);
            _listContent.Controls.Add(item);
        }

        _listEmpty.Visible = _items.Count == 0;
        _listEmpty.Text = _model.Skins.Count == 0
            ? "还没有可用的皮肤，先导入一个 .codexskin"
            : "没有匹配的皮肤";
        _countChip.Text = _model.Skins.Count == 0 ? "空" : $"{_model.Skins.Count} 个";
        _countChip.Fill = Theme.SurfaceMuted;

        LayoutListItems();
        _listHost.ScrollToTop();
        foreach (var item in _items)
        {
            var key = $"list|{item.Skin.Id}";
            if (_thumbCache.TryGetValue(key, out var image)) item.Thumb = image;
            else QueueThumb(item.Skin, item.Skin.Thumbnail ?? item.Skin.OrderedStates.FirstOrDefault()?.File, key, new Size(46, 46), image => item.Thumb = image);
        }
    }

    private async Task RenderSelectionAsync()
    {
        var skin = _selected;
        if (skin is null)
        {
            _title.Text = "皮肤库是空的";
            _subtitle.Text = "导入一个 .codexskin，或者先安装自带皮肤";
            _stage.ShowMessage("还没有选择皮肤", false);
            _tilesChip.Text = "0 / 9 可用";
            _nameField.Value = _authorField.Value = _descField.Value = "";
            SetActionsEnabled(false);
            return;
        }

        _title.Text = skin.DisplayName;
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(skin.AuthorName)) pieces.Add($"作者 {skin.AuthorName}");
        if (!string.IsNullOrWhiteSpace(skin.Version)) pieces.Add($"v{skin.Version}");
        pieces.Add(skin.BuiltIn ? "内置皮肤" : "用户皮肤");
        pieces.Add($"{skin.ReadyStates}/{skin.TotalStates} 个动作");
        pieces.Add(Format.Bytes(skin.Bytes));
        _subtitle.Text = string.Join(" · ", pieces);

        _nameField.Value = skin.Name;
        _authorField.Value = skin.AuthorName;
        _descField.Value = skin.Description;
        _builtInChip.Text = skin.BuiltIn ? "自带皮肤" : "用户皮肤";
        _tilesChip.Text = $"{skin.ReadyStates} / {skin.TotalStates} 可用";
        _tilesChip.Fill = skin.ReadyStates == skin.TotalStates ? Theme.SuccessSoft : Theme.WarningSoft;
        _tilesChip.Ink = skin.ReadyStates == skin.TotalStates ? Color.FromArgb(34, 122, 84) : Theme.Warning;

        SetActionsEnabled(true);
        UpdateTiles();
        await LoadStateAsync(skin, _stateId);
    }

    private void UpdateTiles()
    {
        var skin = _selected;
        foreach (var (id, tile) in _tiles)
        {
            var state = skin?.State(id);
            tile.Selected = id == _stateId;
            tile.SetState(state);
            var key = $"tile|{skin?.Id}|{id}";
            if (skin is null || state is null || !state.Ready)
            {
                tile.Thumb = null;
                continue;
            }
            if (_thumbCache.TryGetValue(key, out var image)) tile.Thumb = image;
            else QueueThumb(skin, state.File, key, new Size(34, 34), image => tile.Thumb = image);
        }
    }

    private void QueueThumb(SkinInfo skin, string? file, string key, Size size, Action<Image> apply)
    {
        if (string.IsNullOrWhiteSpace(file) || _pendingThumbs.Contains(key)) return;
        _pendingThumbs.Add(key);
        _ = Task.Run(async () =>
        {
            try
            {
                await _thumbGate.WaitAsync(_lifetime.Token);
                try
                {
                    var bytes = await GetAssetBytesAsync(skin, file, _lifetime.Token);
                    if (bytes is null) return;
                    using var image = GifThumbnails.FirstFrame(bytes, size);
                    if (image is null) return;
                    var clone = new Bitmap(image);
                    if (IsDisposed) { clone.Dispose(); return; }
                    BeginInvoke(() =>
                    {
                        _thumbCache[key] = clone;
                        apply(clone);
                        itemInvalidate();
                    });
                }
                finally
                {
                    _thumbGate.Release();
                }
            }
            catch
            {
                // 缩略图失败不影响主流程
            }
            finally
            {
                _pendingThumbs.Remove(key);
            }
        }, _lifetime.Token);

        void itemInvalidate()
        {
            foreach (var item in _items) item.Invalidate();
            _tilesPanel.Invalidate(true);
        }
    }

    private async Task<byte[]?> GetAssetBytesAsync(SkinInfo skin, string file, CancellationToken token)
    {
        var key = $"{skin.Id}|{file}";
        if (_byteCache.TryGetValue(key, out var cached)) return cached;
        var state = skin.OrderedStates.FirstOrDefault(item =>
            string.Equals(item.File, file, StringComparison.OrdinalIgnoreCase));
        var bytes = await _engine.AssetAsync(skin.Id, file, state?.Sha256, token);
        _byteCache[key] = bytes;
        return bytes;
    }

    private async Task LoadStateAsync(SkinInfo? skin, string stateId)
    {
        _stateId = stateId;
        UpdateTiles();
        UpdateReplaceLabel();
        if (skin is null) return;

        var state = skin.State(stateId);
        if (state is null || !state.Ready)
        {
            _stage.ShowMessage("这个动作还没有 GIF 文件", false);
            return;
        }

        var token = ++_loadToken;
        if (_options.Offline)
        {
            _stage.ShowMessage($"离线预览：{state.Label}", false);
            return;
        }

        try
        {
            var bytes = await GetAssetBytesAsync(skin, state.File, _lifetime.Token);
            if (bytes is null || token != _loadToken) return;
            var animation = await Task.Run(() => GifAnimation.Load(bytes), _lifetime.Token);
            if (token != _loadToken) { animation.Dispose(); return; }
            _stage.ShowAnimation(animation, state.Label,
                $"{state.Width}×{state.Height} · {state.Frames} 帧 · {Format.Bytes(state.Bytes)}" +
                (state.Fps > 0 ? $" · {state.Fps:0.#} fps" : ""));
            UpdatePlayButton();
            UpdateFitButton();
            TrimMemory();
        }
        catch (Exception error)
        {
            if (token == _loadToken) _stage.ShowMessage(error.Message, false);
        }
    }

    private void UpdateReplaceLabel()
    {
        var label = _selected?.State(_stateId)?.Label ?? StateCatalog.Label(_stateId);
        _replaceButton.Text = $"替换「{label}」";
    }

    private void UpdatePlayButton()
    {
        _playButton.Icon = _stage.Playing ? Glyph.Pause : Glyph.Play;
        _playButton.IconColor = Theme.StageInk;
        _playButton.Invalidate();
    }

    private void UpdateFitButton()
    {
        _fitButton.Icon = _stage.ActualSize ? Glyph.Fit : Glyph.Fit;
        _fitButton.IconColor = _stage.ActualSize ? Theme.Accent : Theme.StageInk;
        _fitButton.Invalidate();
    }

    private void RenderRuntime()
    {
        var runtime = _model.Runtime;
        _runtimeHeadline.Text = runtime.Headline;
        _runtimeDetail.Text = runtime.Detail;

        switch (runtime.State)
        {
            case "gif-patched":
                _runtimeChip.Text = "已开启";
                _runtimeChip.Fill = Theme.SuccessSoft;
                _runtimeChip.Ink = Color.FromArgb(34, 122, 84);
                _runtimeAction.Text = "重新检查动画设置";
                _runtimeAction.Variant = ButtonVariant.Secondary;
                break;
            case "gif-patched-legacy":
                _runtimeChip.Text = "旧版补丁";
                _runtimeChip.Fill = Theme.WarningSoft;
                _runtimeChip.Ink = Theme.Warning;
                _runtimeAction.Text = "重新开启完整动画";
                _runtimeAction.Variant = ButtonVariant.Primary;
                break;
            case "baseline":
                _runtimeChip.Text = "可开启";
                _runtimeChip.Fill = Theme.WarningSoft;
                _runtimeChip.Ink = Theme.Warning;
                _runtimeAction.Text = "一键开启完整动画";
                _runtimeAction.Variant = ButtonVariant.Primary;
                break;
            default:
                _runtimeChip.Text = "需处理";
                _runtimeChip.Fill = Theme.DangerSoft;
                _runtimeChip.Ink = Theme.Danger;
                _runtimeAction.Text = "重新检查";
                _runtimeAction.Variant = ButtonVariant.Secondary;
                break;
        }

        _runtimeAction.Icon = runtime.State == "baseline" ? Glyph.Plus : Glyph.Refresh;
        _runtimeTechnical.Text = string.IsNullOrWhiteSpace(runtime.Asar)
            ? "没有找到可写的程序文件，可在上面手动填写路径。"
            : $"{runtime.RuntimeProfile}\n{runtime.Asar}";
        LayoutInspector();
        UpdateAdvanced();
    }

    private void UpdateAdvanced()
    {
        _advancedToggle.Text = _advancedOpen ? "收起高级设置" : "高级设置";
        _advancedToggle.Icon = _advancedOpen ? Glyph.ChevronDown : Glyph.ChevronRight;
        _advancedToggle.Invalidate();
    }

    private void RenderReceipt()
    {
        var receipt = _model.LastApply;
        if (!receipt.Exists)
        {
            _applyReceipt.Text = "还没有应用过皮肤。应用后这里会记录时间和备份位置。";
            _applyBackup.Text = "";
            _openPets.Visible = !string.IsNullOrWhiteSpace(_model.Paths.Pets);
            return;
        }
        _applyReceipt.Text = $"已应用「{receipt.Name}」，{Format.TimeAgo(receipt.When)}（{Format.Clock(receipt.When)}）。";
        _applyBackup.Text = string.IsNullOrWhiteSpace(receipt.Backup)
            ? "没有历史版本需要备份。"
            : $"上一个版本已备份到 {Path.GetFileName(receipt.Backup)}";
        _openPets.Visible = !string.IsNullOrWhiteSpace(_model.Paths.Pets);
    }

    private void SetActionsEnabled(bool enabled)
    {
        _copyButton.Enabled = enabled;
        _exportButton.Enabled = enabled;
        _applyButton.Enabled = enabled;
        _saveMeta.Enabled = enabled;
        _replaceButton.Enabled = enabled;
    }

    // ---------------------------------------------------------------- 交互

    private void SelectSkin(string id)
    {
        if (_selected?.Id == id) return;
        var skin = _model.Skins.FirstOrDefault(item => item.Id == id);
        if (skin is null) return;
        _selected = skin;
        _stateId = skin.OrderedStates.FirstOrDefault(state => state.Id == "idle")?.Id
                   ?? skin.OrderedStates.FirstOrDefault()?.Id ?? "idle";
        _byteCache.Clear();
        foreach (var item in _items) item.Selected = item.Skin.Id == id;
        _listHost.ScrollToTop();
        _ = RenderSelectionAsync();
    }

    private void SelectState(string id)
    {
        if (_stateId == id) return;
        _ = LoadStateAsync(_selected, id);
    }

    private async Task ReconnectAsync()
    {
        SetEngineState("正在重新连接…", Theme.Warning, true);
        await StartAsync();
        ShowToast("已重新连接本地引擎");
    }

    private async Task ApplyAsync()
    {
        var skin = _selected;
        if (skin is null || _busy) return;
        if (!StudioDialog.Confirm(this, "应用到 Codex",
                $"将把「{skin.DisplayName}」的九种动作写入 Codex 宠物目录，现有版本会自动备份。应用后请完全退出并重启 Codex，再打开 Codex 设置 → 宠物 → 自定义宠物 选中它，皮肤才会显示。",
                "应用")) return;

        await RunBusyAsync("正在应用到 Codex…", async () =>
        {
            var receipt = await _engine.ApplyAsync(skin.Id, _lifetime.Token);
            _model = _model with { LastApply = receipt };
            RenderSkinList();
            RenderReceipt();
            ShowToast($"已应用「{receipt.Name}」。重启 Codex 后，到 设置 → 宠物 → 自定义宠物 选中它");
        });
    }

    private async Task DuplicateAsync()
    {
        var skin = _selected;
        if (skin is null || _busy) return;
        var name = StudioDialog.Prompt(this, "复制皮肤", "新皮肤会复制当前的全部动作与素材，方便二次修改。",
            $"{skin.DisplayName} 副本", "创建副本");
        if (string.IsNullOrWhiteSpace(name)) return;

        await RunBusyAsync("正在复制皮肤…", async () =>
        {
            var created = await _engine.DuplicateAsync(skin.Id, name, _lifetime.Token);
            _model = _model with { Skins = _model.Skins.Concat(new[] { created }).OrderBy(item => item.DisplayName, StringComparer.CurrentCulture).ToList() };
            _selected = created;
            _byteCache.Clear();
            RenderSkinList();
            await RenderSelectionAsync();
            ShowToast($"已创建「{created.DisplayName}」");
        });
    }

    private async Task ExportAsync()
    {
        var skin = _selected;
        if (skin is null || _busy) return;

        using var dialog = new SaveFileDialog
        {
            Title = "导出皮肤包",
            Filter = "Codex 皮肤包 (*.codexskin)|*.codexskin",
            FileName = $"{skin.Id}-{skin.Version}.codexskin".Replace("-.", "."),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await RunBusyAsync("正在导出皮肤包…", async () =>
        {
            var bytes = await _engine.ExportAsync(skin.Id, _lifetime.Token);
            await File.WriteAllBytesAsync(dialog.FileName, bytes, _lifetime.Token);
            ShowToast($"已导出到 {dialog.FileName}");
        });
    }

    private async Task ImportAsync()
    {
        if (_busy) return;
        using var dialog = new OpenFileDialog
        {
            Title = "导入皮肤包",
            Filter = "Codex 皮肤包 (*.codexskin)|*.codexskin|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        await RunBusyAsync("正在导入皮肤包…", async () =>
        {
            var bytes = await File.ReadAllBytesAsync(dialog.FileName, _lifetime.Token);
            var skin = await _engine.ImportAsync(bytes, _lifetime.Token);
            var others = _model.Skins.Where(item => item.Id != skin.Id);
            _model = _model with { Skins = others.Concat(new[] { skin }).OrderBy(item => item.DisplayName, StringComparer.CurrentCulture).ToList() };
            _selected = skin;
            _byteCache.Clear();
            RenderSkinList();
            await RenderSelectionAsync();
            ShowToast($"已导入「{skin.DisplayName}」");
        });
    }

    private async Task ReplaceStateAsync(string stateId, string? filePath = null)
    {
        var skin = _selected;
        if (skin is null || _busy) return;

        if (filePath is null)
        {
            using var dialog = new OpenFileDialog
            {
                Title = $"替换「{skin.State(stateId)?.Label ?? stateId}」的 GIF",
                Filter = "GIF 动画 (*.gif)|*.gif"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            filePath = dialog.FileName;
        }

        if (!filePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("只支持 GIF 文件", true);
            return;
        }

        var label = skin.State(stateId)?.Label ?? StateCatalog.Label(stateId);
        await RunBusyAsync($"正在替换「{label}」…", async () =>
        {
            var bytes = await File.ReadAllBytesAsync(filePath, _lifetime.Token);
            var updated = await _engine.ReplaceStateAsync(skin.Id, stateId, bytes, _lifetime.Token);
            var skins = _model.Skins.Select(item => item.Id == updated.Id ? updated : item).ToList();
            _model = _model with { Skins = skins };
            _selected = updated;
            _byteCache.Clear();
            _thumbCache.Remove($"tile|{updated.Id}|{stateId}");
            RenderSkinList();
            await RenderSelectionAsync();
            ShowToast($"已替换「{label}」");
        });
    }

    private async Task SaveMetaAsync()
    {
        var skin = _selected;
        if (skin is null || _busy) return;
        await RunBusyAsync("正在保存皮肤信息…", async () =>
        {
            var updated = await _engine.UpdateSkinAsync(skin.Id, _nameField.Value.Trim(),
                _authorField.Value.Trim(), _descField.Value.Trim(), _lifetime.Token);
            var skins = _model.Skins.Select(item => item.Id == updated.Id ? updated : item).ToList();
            _model = _model with { Skins = skins };
            _selected = updated;
            RenderSkinList();
            await RenderSelectionAsync();
            ShowToast("皮肤信息已保存");
        });
    }

    private async Task RuntimePrimaryAsync()
    {
        if (_busy) return;
        var runtime = _model.Runtime;
        if (runtime.State == "gif-patched")
        {
            await RecheckRuntimeAsync();
            return;
        }
        if (runtime.State == "not-found" || runtime.State == "unsupported")
        {
            _advancedOpen = true;
            UpdateAdvanced();
            LayoutInspector();
            ShowToast("请先在高级设置中填写程序文件位置", true);
            return;
        }

        if (!StudioDialog.Confirm(this, "开启完整动画",
                "开启前会自动备份程序文件，完成后需要完整退出并重启 Codex。继续吗？", "开启")) return;

        await RunBusyAsync("正在开启完整动画…", async () =>
        {
            await ApplyRuntimeAsync(() => _engine.PatchRuntimeAsync(PathOrNull(), _lifetime.Token));
            ShowToast("完整动画已开启，重启 Codex 后生效");
        });
    }

    private async Task RecheckRuntimeAsync()
    {
        if (_busy) return;
        await RunBusyAsync("正在重新检查动画设置…", async () =>
        {
            var path = PathOrNull();
            await ApplyRuntimeAsync(() => path is null
                ? _engine.RuntimeAsync(null, _lifetime.Token)
                : _engine.SetRuntimePathAsync(path, _lifetime.Token));
            ShowToast("动画设置检查完成");
        });
    }

    private async Task RestoreRuntimeAsync()
    {
        if (_busy) return;
        if (!StudioDialog.Confirm(this, "恢复兼容模式",
                "将关闭完整动画并回到默认播放方式，重启 Codex 后生效。继续吗？", "恢复", danger: true)) return;

        await RunBusyAsync("正在恢复兼容模式…", async () =>
        {
            await ApplyRuntimeAsync(() => _engine.RestoreRuntimeAsync(PathOrNull(), _lifetime.Token));
            ShowToast("已恢复兼容模式，重启 Codex 后生效");
        });
    }

    private async Task ApplyRuntimeAsync(Func<Task<RuntimeReport>> action)
    {
        var report = await action();
        _model = _model with { Runtime = report };
        RenderRuntime();
    }

    private string? PathOrNull()
    {
        var value = _runtimePath.Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private async Task RunBusyAsync(string label, Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        var previous = _applyButton.Text;
        _applyButton.Text = "处理中…";
        _applyButton.Enabled = false;
        SetEngineState(label, Theme.Warning, true);
        try
        {
            await action();
        }
        catch (Exception error)
        {
            ShowToast(error.Message, true);
        }
        finally
        {
            _busy = false;
            _applyButton.Text = previous;
            _applyButton.Enabled = _selected is not null;
            SetEngineState(_engineNote.StartsWith("正在") ? "本地引擎已连接" : _engineNote,
                _model.Skins.Count > 0 ? Theme.Success : Theme.Muted, false);
        }
    }

    private void ShowToast(string message, bool danger = false)
   {
       if (IsDisposed) return;
        _stage.ShowOverlay(message, danger);
    }

    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        var maximized = WindowState == FormWindowState.Maximized;
        _maximize.Maximized = maximized;
        _maximize.Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.F:
                _search.Input.Focus();
                _search.Input.SelectAll();
                return true;
            case Keys.F5:
                _ = ReconnectAsync();
                return true;
            case Keys.Control | Keys.Enter:
                _ = ApplyAsync();
                return true;
            case Keys.Escape when _search.Input.Focused:
               _search.Input.Clear();
                Focus();
               return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var target = GetChildAtPoint(PointToClient(MousePosition), GetChildAtPointSkip.Invisible | GetChildAtPointSkip.Disabled);
        while (target is not null and not ScrollHost)
        {
            target = target.Parent;
        }
        if (target is ScrollHost host) host.Scroll(e.Delta);
        base.OnMouseWheel(e);
    }

    // ---------------------------------------------------------------- 窗口外壳

    public static int HitTestCode(Point point, Size size, bool maximized, int band = Theme.ResizeBorder)
    {
        if (maximized) return HtClient;
        var left = point.X <= band;
        var right = point.X >= size.Width - band;
        var top = point.Y <= band;
        var bottom = point.Y >= size.Height - band;
        if (top && left) return HtTopLeft;
        if (top && right) return HtTopRight;
        if (bottom && left) return HtBottomLeft;
        if (bottom && right) return HtBottomRight;
        if (left) return HtLeft;
        if (right) return HtRight;
        if (top) return HtTop;
        if (bottom) return HtBottom;
        return HtClient;
    }

    private int Band => Math.Max(5, Theme.ResizeBorder * DeviceDpi / 96);

    protected override void WndProc(ref Message m)
    {
        const int wmNcHitTest = 0x0084;
        const int wmGetMinMaxInfo = 0x0024;

        if (m.Msg == wmNcHitTest && WindowState == FormWindowState.Normal)
        {
            var raw = m.LParam.ToInt64();
            var screen = new Point(unchecked((short)(raw & 0xFFFF)), unchecked((short)((raw >> 16) & 0xFFFF)));
            var code = HitTestCode(PointToClient(screen), ClientSize, false, Band);
            if (code != HtClient)
            {
                m.Result = (IntPtr)code;
                return;
            }
        }
        else if (m.Msg == wmGetMinMaxInfo)
        {
            ApplyWorkArea(m.LParam);
        }
        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32 { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point32 Reserved, MaxSize, MaxPosition, MinTrack, MaxTrack;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32 { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect32 Monitor;
        public Rect32 Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern int GetGuiResources(IntPtr process, int flags);

    private static string HandleSample(Process process) =>
        ",\"gdiObjects\":" + GetGuiResources(process.Handle, 0) +
        ",\"userObjects\":" + GetGuiResources(process.Handle, 1);

    private void ApplyWorkArea(IntPtr lParam)
    {
        try
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            var monitor = MonitorFromWindow(Handle, 2);
            var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref monitorInfo)) return;
            var work = monitorInfo.Work;
            var screen = monitorInfo.Monitor;
            info.MaxPosition.X = work.Left - screen.Left;
            info.MaxPosition.Y = work.Top - screen.Top;
            info.MaxSize.X = work.Right - work.Left;
            info.MaxSize.Y = work.Bottom - work.Top;
            info.MinTrack.X = MinimumSize.Width + 16;
            info.MinTrack.Y = MinimumSize.Height + 16;
            Marshal.StructureToPtr(info, lParam, false);
        }
        catch
        {
            // 保持系统默认值
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RoundedCorners.Apply(Handle);
    }

    // ---------------------------------------------------------------- 截图自检

    /// <summary>切换动作后立即回收上一次解码占用的内存，避免任务管理器里一路上涨。</summary>
    private void TrimMemory()
    {
        if (_options.NoTrim) return;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
        GC.WaitForPendingFinalizers();
    }

    /// <summary>压力测试：反复切换九种动作，观察托管堆与私有内存是否线性增长。</summary>
    private async Task RunStressAsync()
    {
        var skin = _selected;
        var states = skin?.OrderedStates.Where(state => state.Ready).Select(state => state.Id).ToList()
                     ?? new List<string>();
        if (states.Count == 0)
        {
            Console.WriteLine("{\"error\":\"没有可测试的动作\"}");
            Close();
            return;
        }

        var process = Process.GetCurrentProcess();
        var samples = new List<string>();
        var stateLog = new List<string>();
        var baselineManaged = GC.GetTotalMemory(true) / 1048576;
        process.Refresh();
        var baselinePrivate = process.PrivateMemorySize64 / 1048576;
        var rounds = Math.Max(1, _options.StressRounds);

        for (var round = 1; round <= rounds; round++)
        {
            var peakPrivate = 0L;
            var peakManaged = 0L;
            foreach (var id in states)
            {
                var loadStart = Environment.TickCount64;
                await LoadStateAsync(skin, id);
                var loadMs = Environment.TickCount64 - loadStart;
                process.Refresh();
                stateLog.Add("{\"state\":\"" + id + "\",\"loadMs\":" + loadMs + ",\"privateMB\":" + process.PrivateMemorySize64 / 1048576 +
                    ",\"managedMB\":" + GC.GetTotalMemory(false) / 1048576 + "}");
                var waitUntil = Environment.TickCount64 + Math.Max(0, _options.StressDwellMs);
                while (Environment.TickCount64 < waitUntil)
                {
                    Application.DoEvents();
                    process.Refresh();
                    peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64 / 1048576);
                    peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false) / 1048576);
                    await Task.Delay(30);
                }
            }
            Application.DoEvents();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            process.Refresh();
            samples.Add("{\"round\":" + round +
                        ",\"managedMB\":" + GC.GetTotalMemory(true) / 1048576 +
                        ",\"privateMB\":" + process.PrivateMemorySize64 / 1048576 +
                        ",\"workingSetMB\":" + process.WorkingSet64 / 1048576 +
                        ",\"peakPrivateMB\":" + peakPrivate +
                        ",\"peakManagedMB\":" + peakManaged + HandleSample(process) + "}");
        }

        Console.WriteLine("{");
        Console.WriteLine("  \"states\": " + states.Count + ",");
        Console.WriteLine("  \"rounds\": " + rounds + ",");
        Console.WriteLine("  \"baselineManagedMB\": " + baselineManaged + ",");
        Console.WriteLine("  \"baselinePrivateMB\": " + baselinePrivate + ",");
        Console.WriteLine("  \"samples\": [" + string.Join(",", samples) + "],");
        Console.WriteLine("  \"states\": [" + string.Join(",", stateLog) + "]");
        Console.WriteLine("}");
        Close();
    }
    public Rectangle StageBoundsInForm() =>
       new(_stage.Left + _center.Left, _stage.Top + _center.Top, _stage.Width, _stage.Height);

    // ---------------------------------------------------------------- 自检支持

    internal Rectangle StageBounds => _stage.Bounds;
    internal Size CenterSize => _center.Size;
    internal Size SidebarSize => _sidebar.Size;
    internal Size InspectorSize => _inspector.Size;
    internal IReadOnlyDictionary<string, StateTile> Tiles => _tiles;

    internal void LoadSampleForTest()
    {
        _model = SampleData.Build();
        _selected = _model.Skins.FirstOrDefault();
        _stateId = _selected?.OrderedStates.FirstOrDefault()?.Id ?? "idle";
        RenderSkinList();
        _ = RenderSelectionAsync();
        RenderRuntime();
        RenderReceipt();
    }

    private async Task CaptureAsync()
    {
        try { await _ready.Task.WaitAsync(TimeSpan.FromSeconds(40)); } catch { /* 超时也出图 */ }
        await Task.Delay(700);
        Refresh();
        using var bitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
        DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        var path = _options.CapturePath!;
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
        bitmap.Save(path, ImageFormat.Png);
        var report = CaptureReport.Analyze(bitmap, StageBoundsInForm(), ClientSize);
        File.WriteAllText(path + ".json", report);
        Console.WriteLine(report);
        Close();
    }
}
