using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GrillingSkinStudio;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new StudioForm());
    }
}

internal sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:43821"), Timeout = TimeSpan.FromMinutes(3) };
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    public async Task<JsonElement> Get(string path) => await Send(HttpMethod.Get, path, null, null);
    public async Task<JsonElement> PostJson(string path, object body) => await Send(HttpMethod.Post, path, JsonSerializer.Serialize(body), "application/json");
    public async Task<JsonElement> PatchJson(string path, object body) => await Send(HttpMethod.Patch, path, JsonSerializer.Serialize(body), "application/json");
    public async Task<JsonElement> PostBytes(string path, byte[] bytes, string contentType) => await Send(HttpMethod.Post, path, bytes, contentType);
    private async Task<JsonElement> Send(HttpMethod method, string path, object? body, string? contentType)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is string text) request.Content = new StringContent(text, Encoding.UTF8, contentType ?? "application/json");
        else if (body is byte[] bytes) request.Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue(contentType!) } };
        using var response = await _http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        if (!response.IsSuccessStatusCode || !doc.RootElement.GetProperty("ok").GetBoolean())
            throw new InvalidOperationException(doc.RootElement.TryGetProperty("error", out var error) ? error.GetString() : $"请求失败：{(int)response.StatusCode}");
        return doc.RootElement.GetProperty("data").Clone();
    }
    public async Task<byte[]> Asset(string skinId, string file, string hash)
        => await _http.GetByteArrayAsync($"/api/asset/{Uri.EscapeDataString(skinId)}/{string.Join("/", file.Split('/').Select(Uri.EscapeDataString))}?v={Uri.EscapeDataString(hash)}");
    public async Task<byte[]> Export(string skinId) => await _http.GetByteArrayAsync($"/api/export?id={Uri.EscapeDataString(skinId)}");
    public void Dispose() => _http.Dispose();
}

internal sealed class StudioForm : Form
{
    private readonly ApiClient _api = new();
    private readonly Color _ink = Theme.Ink;
    private readonly Color _muted = Theme.Muted;
    private readonly Color _line = Theme.Line;
    private readonly Color _accent = Theme.Accent;
    private readonly ListBox _library = new();
    private readonly TextBox _search = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _previewState = new();
    private readonly Label _previewMeta = new();
    private readonly PictureBox _preview = new();
    private readonly FlowLayoutPanel _states = new();
    private readonly TextBox _name = new();
    private readonly TextBox _author = new();
    private readonly TextBox _description = new();
    private readonly Label _runtimeStatus = new();
    private readonly Label _runtimeDetail = new();
    private readonly TextBox _runtimePath = new();
    private readonly StudioButton _apply = new();
    private readonly StudioButton _export = new();
    private readonly StudioButton _duplicate = new();
    private readonly StudioButton _runtimePatch = new();
    private readonly StudioButton _runtimeRestore = new();
    private readonly ToolTip _tooltips = new() { AutoPopDelay = 6000, InitialDelay = 500, ReshowDelay = 100 };
    private JsonElement _model;
    private JsonElement _runtime;
    private string? _selectedId;
    private string _selectedState = "idle";
    private MemoryStream? _previewStream;
    private Process? _serverProcess;
    private bool _loading;

    public StudioForm()
    {
        Text = "Grilling · Codex 皮肤工坊";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 720);
        ClientSize = new Size(1280, 820);
        BackColor = Theme.Canvas;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BuildLayout();
        Shown += async (_, _) => await StartAsync();
        FormClosed += (_, _) => { _previewStream?.Dispose(); _api.Dispose(); if (_serverProcess is { HasExited: false }) { try { _serverProcess.Kill(); } catch { } } };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = BackColor };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 264));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 324));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 1, 0);
        root.Controls.Add(BuildInspector(), 2, 0);
        Controls.Add(root);
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(16) };
        var mark = new BrandMark { Location = new Point(20, 22) };
        var brand = new Label { Text = "Grilling", ForeColor = Color.White, Font = new Font("Segoe UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(70, 18) };
        var hint = new Label { Text = "CODEX 皮肤工坊", ForeColor = Color.FromArgb(153, 164, 154), Font = new Font("Segoe UI", 8.5F), AutoSize = true, Location = new Point(72, 51) };
        panel.Controls.Add(mark); panel.Controls.Add(brand); panel.Controls.Add(hint);

        var searchSurface = new SurfacePanel { Location = new Point(20, 98), Size = new Size(224, 40), BackColor = Theme.SidebarSoft, BorderColor = Color.FromArgb(65, 74, 66), Radius = 8 };
        var searchLabel = new Label { Text = "搜索", ForeColor = Color.FromArgb(148, 160, 150), AutoSize = true, Location = new Point(12, 11) };
        _search.Location = new Point(52, 8); _search.Size = new Size(158, 24); _search.BorderStyle = BorderStyle.None; _search.BackColor = Theme.SidebarSoft; _search.ForeColor = Color.White; _search.PlaceholderText = "皮肤名称或作者"; _search.TextChanged += (_, _) => RenderLibrary();
        searchSurface.Controls.Add(searchLabel); searchSurface.Controls.Add(_search); panel.Controls.Add(searchSurface);

        var libraryLabel = SectionLabel("皮肤库", new Point(20, 166), Color.White);
        var import = ActionButton("导入皮肤", ButtonVariant.DarkGhost, new Point(149, 157), new Size(95, 32));
        import.Click += async (_, _) => await ImportAsync(); _tooltips.SetToolTip(import, "导入 .codexskin 皮肤包");
        panel.Controls.Add(libraryLabel); panel.Controls.Add(import);

        _library.Location = new Point(12, 205); _library.Size = new Size(240, 520); _library.BorderStyle = BorderStyle.None; _library.BackColor = Theme.Sidebar; _library.ForeColor = Color.White; _library.Font = new Font("Segoe UI", 9.5F); _library.DrawMode = DrawMode.OwnerDrawFixed; _library.ItemHeight = 64; _library.SelectedIndexChanged += async (_, _) => await SelectSkinAsync(); _library.DrawItem += DrawLibraryItem; panel.Controls.Add(_library);
        var footer = new Label { Text = "本机皮肤库", ForeColor = Color.FromArgb(154, 165, 155), AutoSize = true, Location = new Point(42, 774) };
        var dot = new Panel { BackColor = Theme.Success, Size = new Size(7, 7), Location = new Point(22, 779) };
        panel.Controls.Add(dot); panel.Controls.Add(footer);
        panel.Resize += (_, _) => { _library.Height = Math.Max(220, panel.Height - 278); footer.Top = panel.Height - 39; dot.Top = panel.Height - 34; };
        return panel;
    }

    private Control BuildWorkspace()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(32, 26, 30, 24), BackColor = Theme.Canvas };
        _title.Text = "正在载入"; _title.AutoSize = true; _title.Font = new Font("Segoe UI", 21F, FontStyle.Bold); _title.ForeColor = _ink; _title.Location = new Point(32, 24); panel.Controls.Add(_title);
        _subtitle.Text = "选择动作并预览原始 GIF"; _subtitle.AutoSize = true; _subtitle.ForeColor = _muted; _subtitle.Location = new Point(34, 65); panel.Controls.Add(_subtitle);

        _apply.Text = "应用到 Codex"; StyleButton(_apply, ButtonVariant.Primary); _apply.Size = new Size(132, 38); _apply.Anchor = AnchorStyles.Top | AnchorStyles.Right; _apply.Location = new Point(panel.Width - 162, 27); _apply.Click += async (_, _) => await ApplyAsync(); panel.Controls.Add(_apply);
        _export.Text = "导出"; StyleButton(_export, ButtonVariant.Secondary); _export.Size = new Size(72, 38); _export.Anchor = AnchorStyles.Top | AnchorStyles.Right; _export.Location = new Point(panel.Width - 242, 27); _export.Click += async (_, _) => await ExportAsync(); panel.Controls.Add(_export);
        _duplicate.Text = "创建副本"; StyleButton(_duplicate, ButtonVariant.Ghost); _duplicate.Size = new Size(88, 38); _duplicate.Anchor = AnchorStyles.Top | AnchorStyles.Right; _duplicate.Location = new Point(panel.Width - 338, 27); _duplicate.Click += async (_, _) => await DuplicateAsync(); panel.Controls.Add(_duplicate);
        _tooltips.SetToolTip(_apply, "校验并安装当前皮肤到 Codex 宠物目录");

        var previewCard = new PreviewStage { Location = new Point(32, 112), Size = new Size(panel.Width - 62, 326), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _previewState.Text = "待机"; _previewState.ForeColor = Color.White; _previewState.Font = new Font("Segoe UI", 18F, FontStyle.Bold); _previewState.AutoSize = true; _previewState.Location = new Point(24, 32); previewCard.Controls.Add(_previewState);
        _previewMeta.Text = "读取动画信息中"; _previewMeta.ForeColor = Color.FromArgb(172, 183, 174); _previewMeta.MaximumSize = new Size(132, 80); _previewMeta.AutoSize = true; _previewMeta.Location = new Point(26, 76); previewCard.Controls.Add(_previewMeta);
        var fidelity = new Label { Text = "原始 GIF 播放", ForeColor = Color.FromArgb(136, 150, 139), Font = new Font("Segoe UI", 8.5F), AutoSize = true, Location = new Point(26, 118) }; previewCard.Controls.Add(fidelity);
        _preview.Location = new Point(196, 22); _preview.Size = new Size(Math.Max(240, previewCard.Width - 220), 282); _preview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; _preview.SizeMode = PictureBoxSizeMode.Zoom; _preview.BackColor = Theme.Stage; previewCard.Controls.Add(_preview);
        var replace = ActionButton("替换当前 GIF", ButtonVariant.Primary, new Point(24, 258), new Size(132, 38)); replace.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; replace.Click += async (_, _) => await ReplaceAsync(_selectedState); previewCard.Controls.Add(replace);
        panel.Controls.Add(previewCard);

        var stateHeader = SectionLabel("动作状态", new Point(32, 470), _ink); panel.Controls.Add(stateHeader);
        var stateHint = new Label { Text = "九种状态独立保存。点击切换预览，按“更换”只替换当前动作。", AutoSize = true, ForeColor = _muted, Location = new Point(32, 498) }; panel.Controls.Add(stateHint);
        _states.Location = new Point(32, 532); _states.Size = new Size(panel.Width - 62, panel.Height - 556); _states.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; _states.AutoScroll = true; _states.WrapContents = true; _states.FlowDirection = FlowDirection.LeftToRight; _states.Padding = new Padding(0, 0, 4, 4); _states.BackColor = Theme.Canvas; panel.Controls.Add(_states);
        panel.Resize += (_, _) =>
        {
            _apply.Left = panel.Width - 162; _export.Left = panel.Width - 242; _duplicate.Left = panel.Width - 338;
            previewCard.Height = Math.Max(280, Math.Min(346, panel.Height / 2 - 58));
            _preview.Width = Math.Max(240, previewCard.Width - 220); _preview.Height = previewCard.Height - 44; replace.Top = previewCard.Height - 62;
            stateHeader.Top = previewCard.Bottom + 29; stateHint.Top = stateHeader.Bottom + 8; _states.Top = stateHint.Bottom + 18; _states.Height = panel.Height - _states.Top - 22;
        };
        return panel;
    }

    private Control BuildInspector()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(252, 251, 248), Padding = new Padding(24, 26, 22, 18), AutoScroll = true };
        panel.Paint += (_, e) => { using var pen = new Pen(Theme.Line); e.Graphics.DrawLine(pen, 0, 0, 0, panel.Height); };
        var heading = SectionLabel("皮肤信息", new Point(24, 28), _ink); panel.Controls.Add(heading);
        var origin = new Label { Text = "本机编辑", ForeColor = Theme.Success, BackColor = Color.FromArgb(229, 244, 235), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(70, 24), Location = new Point(224, 25) }; panel.Controls.Add(origin);
        AddField(panel, "名称", _name, 76, 270); AddField(panel, "作者", _author, 148, 270);
        _description.Multiline = true; _description.Height = 78; _description.ScrollBars = ScrollBars.Vertical; AddField(panel, "说明", _description, 220, 270);
        var save = ActionButton("保存皮肤信息", ButtonVariant.Secondary, new Point(24, 326), new Size(270, 38)); save.Click += async (_, _) => await SaveMetaAsync(); panel.Controls.Add(save);

        var divider = new Panel { BackColor = Theme.Line, Location = new Point(24, 398), Size = new Size(270, 1) }; panel.Controls.Add(divider);
        var runtimeTitle = SectionLabel("完整动画", new Point(24, 429), _ink); panel.Controls.Add(runtimeTitle);
        _runtimeStatus.Text = "检查中"; _runtimeStatus.AutoSize = true; _runtimeStatus.ForeColor = _accent; _runtimeStatus.Font = new Font("Segoe UI", 10F, FontStyle.Bold); _runtimeStatus.Location = new Point(24, 467); panel.Controls.Add(_runtimeStatus);
        _runtimeDetail.Text = "正在检查 Codex 运行时"; _runtimeDetail.MaximumSize = new Size(270, 56); _runtimeDetail.AutoEllipsis = true; _runtimeDetail.AutoSize = false; _runtimeDetail.Size = new Size(270, 46); _runtimeDetail.ForeColor = _muted; _runtimeDetail.Location = new Point(24, 494); panel.Controls.Add(_runtimeDetail);
        _runtimePatch.Text = "一键开启完整动画"; StyleButton(_runtimePatch, ButtonVariant.Primary); _runtimePatch.Location = new Point(24, 551); _runtimePatch.Size = new Size(270, 40); _runtimePatch.Click += async (_, _) => await PatchRuntimeAsync(); panel.Controls.Add(_runtimePatch);

        var advanced = SectionLabel("高级设置", new Point(24, 634), _ink); panel.Controls.Add(advanced);
        _runtimePath.PlaceholderText = "app.asar 路径（一般留空）"; _runtimePath.Width = 270; _runtimePath.Location = new Point(24, 667); StyleInput(_runtimePath); panel.Controls.Add(_runtimePath);
        var recheck = ActionButton("重新检查", ButtonVariant.Secondary, new Point(24, 710), new Size(128, 34)); recheck.Click += async (_, _) => await RefreshRuntimeAsync(); panel.Controls.Add(recheck);
        _runtimeRestore.Text = "恢复兼容模式"; StyleButton(_runtimeRestore, ButtonVariant.Ghost); _runtimeRestore.Location = new Point(160, 710); _runtimeRestore.Size = new Size(134, 34); _runtimeRestore.Click += async (_, _) => await RestoreRuntimeAsync(); panel.Controls.Add(_runtimeRestore);
        _tooltips.SetToolTip(_runtimeRestore, "从已保存的备份恢复原始运行时");
        return panel;
    }

    private async Task StartAsync()
    {
        try { StartServer(); await WaitForServerAsync(); await RefreshAsync(); }
        catch (Exception ex) { MessageBox.Show($"皮肤工坊启动失败：{ex.Message}", "Grilling", MessageBoxButtons.OK, MessageBoxIcon.Error); Close(); }
    }

    private void StartServer()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 7 && root != null; i++, root = root.Parent)
        {
            var script = Path.Combine(root.FullName, "plugins", "codex-skin-engine", "scripts", "studio-server.mjs");
            if (!File.Exists(script)) continue;
            var node = FindNode();
            _serverProcess = Process.Start(new ProcessStartInfo(node, $"\"{script}\" --port 43821") { WorkingDirectory = root.FullName, CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            return;
        }
        throw new InvalidOperationException("找不到皮肤引擎文件。请从完整的项目目录启动 Grilling。");
    }

    private static string FindNode()
    {
        var candidates = new List<string>();
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path)) candidates.AddRange(path.Split(Path.PathSeparator).Select(x => Path.Combine(x, "node.exe")));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"));
        var runtimes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "runtimes");
        if (Directory.Exists(runtimes)) candidates.AddRange(Directory.EnumerateFiles(runtimes, "node.exe", SearchOption.AllDirectories));
        return candidates.FirstOrDefault(File.Exists) ?? "node.exe";
    }

    private static async Task WaitForServerAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(700) };
        for (var i = 0; i < 30; i++) { try { if ((await client.GetAsync("http://127.0.0.1:43821/api/health")).IsSuccessStatusCode) return; } catch { } await Task.Delay(100); }
        throw new InvalidOperationException("本地皮肤引擎没有响应，请确认 Node.js 可用。");
    }

    private async Task RefreshAsync(string? preferred = null)
    {
        SetBusy(true, "正在读取皮肤库");
        try { _model = await _api.Get("/api/bootstrap"); _runtime = _model.GetProperty("runtime"); var skins = _model.GetProperty("skins"); if (preferred == null || !skins.EnumerateArray().Any(x => x.GetProperty("id").GetString() == preferred)) _selectedId = skins.EnumerateArray().FirstOrDefault().GetProperty("id").GetString(); else _selectedId = preferred; RenderLibrary(); await RenderSelectedAsync(); RenderRuntime(); }
        finally { SetBusy(false); }
    }

    private void RenderLibrary()
    {
        var old = _selectedId; _library.Items.Clear(); var query = _search.Text.Trim(); foreach (var skin in _model.GetProperty("skins").EnumerateArray()) { var name = skin.GetProperty("name").GetString() ?? ""; var author = skin.GetProperty("author").GetProperty("name").GetString() ?? ""; if (query.Length > 0 && !($"{name} {author}").Contains(query, StringComparison.OrdinalIgnoreCase)) continue; _library.Items.Add(new SkinListItem(skin, name, author)); } var index = Enumerable.Range(0, _library.Items.Count).FirstOrDefault(i => ((SkinListItem)_library.Items[i]).Id == old); if (_library.Items.Count > 0) _library.SelectedIndex = index;
    }

    private async Task SelectSkinAsync() { if (_loading || _library.SelectedItem is not SkinListItem item) return; _selectedId = item.Id; _selectedState = "idle"; await RenderSelectedAsync(); }

    private async Task RenderSelectedAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedId)) return; _loading = true; try { var skin = _model.GetProperty("skins").EnumerateArray().First(x => x.GetProperty("id").GetString() == _selectedId); _title.Text = skin.GetProperty("name").GetString(); _name.Text = skin.GetProperty("name").GetString(); _author.Text = skin.GetProperty("author").GetProperty("name").GetString(); _description.Text = skin.GetProperty("description").GetString(); RenderStates(skin); await LoadPreviewAsync(skin, _selectedState); } finally { _loading = false; }
    }

    private void RenderStates(JsonElement skin)
    {
        _states.Controls.Clear();
        foreach (var state in _model.GetProperty("states").EnumerateArray())
        {
            var id = state.GetProperty("id").GetString()!;
            var item = skin.GetProperty("states").GetProperty(id);
            var selected = id == _selectedState;
            var card = new SurfacePanel
            {
                Width = 174,
                Height = 92,
                Margin = new Padding(0, 0, 10, 10),
                BackColor = selected ? Theme.AccentSoft : Theme.Surface,
                BorderColor = selected ? Theme.Accent : Theme.Line,
                Radius = 9,
                Cursor = Cursors.Hand,
                Tag = id
            };
            var label = new Label { Text = state.GetProperty("label").GetString(), Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = _ink, AutoSize = true, Location = new Point(13, 12) };
            var meta = new Label { Text = $"{item.GetProperty("frames").GetInt32()} 帧  ·  {FormatBytes(item.GetProperty("bytes").GetInt64())}", ForeColor = _muted, AutoSize = true, Location = new Point(13, 39) };
            var replace = ActionButton("更换", ButtonVariant.Ghost, new Point(105, 59), new Size(58, 26));
            replace.Tag = id;
            replace.Click += async (_, _) => await ReplaceAsync(id);
            card.Click += async (_, _) => { _selectedState = id; await RenderSelectedAsync(); };
            foreach (Control child in new Control[] { label, meta }) child.Click += async (_, _) => { _selectedState = id; await RenderSelectedAsync(); };
            card.Controls.Add(label); card.Controls.Add(meta); card.Controls.Add(replace);
            _states.Controls.Add(card);
        }
    }

    private async Task LoadPreviewAsync(JsonElement skin, string stateId)
    {
        var item = skin.GetProperty("states").GetProperty(stateId); _previewState.Text = skin.GetProperty("states").GetProperty(stateId).GetProperty("label").GetString(); _previewMeta.Text = $"{item.GetProperty("frames").GetInt32()} 帧 · {item.GetProperty("width").GetInt32()}×{item.GetProperty("height").GetInt32()} · {FormatBytes(item.GetProperty("bytes").GetInt64())}"; var bytes = await _api.Asset(skin.GetProperty("id").GetString()!, item.GetProperty("file").GetString()!, item.GetProperty("sha256").GetString()!); _preview.Image?.Dispose(); _previewStream?.Dispose(); _previewStream = new MemoryStream(bytes); _preview.Image = Image.FromStream(_previewStream); _preview.SizeMode = PictureBoxSizeMode.Zoom;
    }

    private async Task ReplaceAsync(string stateId)
    {
        if (string.IsNullOrWhiteSpace(_selectedId)) return; using var dialog = new OpenFileDialog { Filter = "GIF 动画|*.gif", Title = "选择 GIF" }; if (dialog.ShowDialog(this) != DialogResult.OK) return; SetBusy(true, "正在校验并替换 GIF"); try { var data = await _api.PostBytes($"/api/skin/{Uri.EscapeDataString(_selectedId)}/state/{Uri.EscapeDataString(stateId)}", await File.ReadAllBytesAsync(dialog.FileName), "image/gif"); _model = ReplaceSkin(data); _selectedState = stateId; await RenderSelectedAsync(); RenderLibrary(); MessageBox.Show($"已保留全部 {data.GetProperty("states").GetProperty(stateId).GetProperty("frames").GetInt32()} 帧。", "替换成功", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); }
    }

    private JsonElement ReplaceSkin(JsonElement updated) { using var doc = JsonDocument.Parse(_model.GetRawText()); var root = doc.RootElement; var skins = root.GetProperty("skins").EnumerateArray().Select(x => x.GetProperty("id").GetString() == updated.GetProperty("id").GetString() ? updated : x).ToArray(); var json = JsonSerializer.Serialize(new { version = root.GetProperty("version").GetString(), states = root.GetProperty("states"), skins, runtime = root.GetProperty("runtime"), paths = root.GetProperty("paths"), lastApply = root.GetProperty("lastApply") }); return JsonDocument.Parse(json).RootElement.Clone(); }

    private async Task SaveMetaAsync() { if (_selectedId == null) return; try { SetBusy(true, "正在保存皮肤信息"); var updated = await _api.PatchJson($"/api/skin/{Uri.EscapeDataString(_selectedId)}", new { name = _name.Text, author = _author.Text, description = _description.Text }); _model = ReplaceSkin(updated); RenderLibrary(); await RenderSelectedAsync(); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task ApplyAsync() { if (_selectedId == null) return; try { SetBusy(true, "正在应用皮肤"); var result = await _api.PostJson("/api/apply", new { id = _selectedId }); MessageBox.Show($"已安装到：\n{result.GetProperty("target").GetString()}\n\n请在 Codex 宠物设置中选择它。", "应用成功", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task ExportAsync() { if (_selectedId == null) return; using var dialog = new SaveFileDialog { Filter = "Codex 皮肤包|*.codexskin", FileName = $"{_selectedId}.codexskin" }; if (dialog.ShowDialog(this) != DialogResult.OK) return; try { SetBusy(true, "正在准备皮肤包"); await File.WriteAllBytesAsync(dialog.FileName, await _api.Export(_selectedId)); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task DuplicateAsync() { if (_selectedId == null) return; var name = Prompt.Show(this, "新皮肤名称", "复制皮肤", $"{_title.Text} 副本"); if (string.IsNullOrWhiteSpace(name)) return; try { SetBusy(true, "正在创建副本"); var created = await _api.PostJson($"/api/skin/{Uri.EscapeDataString(_selectedId)}/duplicate", new { name }); await RefreshAsync(created.GetProperty("id").GetString()); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task ImportAsync() { using var dialog = new OpenFileDialog { Filter = "Codex 皮肤包|*.codexskin", Title = "导入皮肤包" }; if (dialog.ShowDialog(this) != DialogResult.OK) return; try { SetBusy(true, "正在安全校验皮肤包"); var data = await _api.PostBytes("/api/import", await File.ReadAllBytesAsync(dialog.FileName), "application/zip"); await RefreshAsync(data.GetProperty("id").GetString()); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task RefreshRuntimeAsync() { try { SetBusy(true, "正在检查动画设置"); _runtime = _runtimePath.Text.Trim().Length > 0 ? await _api.PostJson("/api/runtime/path", new { path = _runtimePath.Text.Trim() }) : await _api.Get("/api/runtime"); RenderRuntime(); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task PatchRuntimeAsync() { try { SetBusy(true, "正在开启完整动画"); _runtime = await _api.PostJson("/api/runtime/patch", new { path = _runtimePath.Text.Trim() }); RenderRuntime(); MessageBox.Show("运行时已更新，请完全退出并重新启动 Codex。", "需要重启", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private async Task RestoreRuntimeAsync() { try { SetBusy(true, "正在恢复兼容模式"); _runtime = await _api.PostJson("/api/runtime/restore", new { path = _runtimePath.Text.Trim() }); RenderRuntime(); } catch (Exception ex) { ShowError(ex); } finally { SetBusy(false); } }
    private void RenderRuntime()
    {
        var state = _runtime.TryGetProperty("state", out var value) ? value.GetString() : "unknown";
        _runtimeStatus.Text = state switch { "gif-patched" => "已开启，完整 GIF 正在使用", "baseline" => "还差一步", "gif-patched-legacy" => "可以升级", "not-found" => "未找到 Codex", _ => "当前版本暂不支持" };
        _runtimeStatus.ForeColor = state == "gif-patched" ? Theme.Success : _accent;
        _runtimeDetail.Text = _runtime.TryGetProperty("message", out var message) && !string.IsNullOrWhiteSpace(message.GetString())
            ? message.GetString()
            : state == "gif-patched" ? "工作、待机等动画会完整循环；更新设置后需要重新启动 Codex。" : "开启后，九种状态会直接播放完整 GIF。";
        if (_runtime.TryGetProperty("asar", out var asar)) _runtimePath.Text = asar.GetString();
        _runtimePatch.Enabled = state is "baseline" or "gif-patched-legacy";
        _runtimePatch.Text = state == "gif-patched-legacy" ? "更新完整动画功能" : state == "gif-patched" ? "完整动画已开启" : "一键开启完整动画";
        _runtimeRestore.Enabled = state is "gif-patched" or "gif-patched-legacy";
    }

    private void SetBusy(bool busy, string text = "正在处理") { Cursor = busy ? Cursors.WaitCursor : Cursors.Default; _subtitle.Text = busy ? text : "选择动作并预览原始 GIF"; UseWaitCursor = busy; _apply.Enabled = !busy; _export.Enabled = !busy; _duplicate.Enabled = !busy; }
    private void ShowError(Exception ex) => MessageBox.Show(ex.Message, "Grilling", MessageBoxButtons.OK, MessageBoxIcon.Error);
    private Label SectionLabel(string text, Point location, Color color) => new() { Text = text, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = color, AutoSize = true, Location = location };
    private StudioButton ActionButton(string text, ButtonVariant variant, Point location, Size size) { var button = new StudioButton { Text = text, Location = location, Size = size, Variant = variant }; return button; }
    private void StyleButton(StudioButton button, ButtonVariant variant) { button.Variant = variant; button.Invalidate(); }
    private void StyleInput(TextBox input) { input.BorderStyle = BorderStyle.FixedSingle; input.BackColor = Theme.Surface; input.ForeColor = Theme.Ink; input.Font = new Font("Segoe UI", 9.5F); }
    private void AddField(Control parent, string label, TextBox input, int top, int width) { parent.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = _muted, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Location = new Point(24, top) }); input.Location = new Point(24, top + 23); input.Width = width; StyleInput(input); parent.Controls.Add(input); }
    private static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:0.0} KB" : $"{bytes / 1024d / 1024d:0.0} MB";
    private void DrawLibraryItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _library.Items.Count) return;
        var item = (SkinListItem)_library.Items[e.Index];
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Theme.Sidebar);
        var selected = e.Index == _library.SelectedIndex;
        var bounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + 3, e.Bounds.Width - 8, e.Bounds.Height - 6);
        using var path = DrawUtil.RoundRect(bounds, 8);
        using var background = new SolidBrush(selected ? Theme.SidebarSoft : Theme.Sidebar);
        e.Graphics.FillPath(background, path);
        if (selected)
        {
            using var accent = new SolidBrush(Theme.Accent);
            e.Graphics.FillEllipse(accent, bounds.Left + 10, bounds.Top + 22, 7, 7);
        }
        using var title = new SolidBrush(Color.White);
        using var meta = new SolidBrush(Color.FromArgb(158, 169, 160));
        using var titleFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        using var metaFont = new Font("Segoe UI", 8F);
        var textLeft = bounds.Left + (selected ? 28 : 14);
        e.Graphics.DrawString(item.Name, titleFont, title, textLeft, bounds.Top + 10);
        e.Graphics.DrawString($"{item.Author}  ·  {FormatBytes(item.Bytes)}", metaFont, meta, textLeft, bounds.Top + 33);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }
}

internal sealed class SkinListItem
{
    public string Id { get; } public string Name { get; } public string Author { get; } public long Bytes { get; }
    public SkinListItem(JsonElement skin, string name, string author) { Id = skin.GetProperty("id").GetString()!; Name = name; Author = author; Bytes = skin.GetProperty("bytes").GetInt64(); }
    public override string ToString() => Name;
}

internal static class Prompt
{
    public static string? Show(IWin32Window owner, string text, string caption, string initial)
    {
        using var form = new Form { Width = 390, Height = 155, Text = caption, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var label = new Label { Text = text, Left = 18, Top = 16, AutoSize = true }; var input = new TextBox { Left = 18, Top = 43, Width = 335, Text = initial }; var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 190, Top = 82, Width = 76 }; var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 277, Top = 82, Width = 76 }; form.Controls.AddRange(new Control[] { label, input, ok, cancel }); form.AcceptButton = ok; form.CancelButton = cancel; return form.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : null;
    }
}
