using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SkinStudio.Core;

internal sealed class EngineException : Exception
{
    public EngineException(string message) : base(message) { }
}

/// <summary>
/// 本机皮肤引擎（studio-server.mjs）的客户端：探活、按需拉起隐藏的 Node 服务，以及全部 API 调用。
/// </summary>
internal sealed class EngineClient : IDisposable
{
    private readonly HttpClient _http;
    private Process? _server;

    public EngineClient(int port = 43821)
    {
        Port = port;
        _http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";
    public bool ReusedExistingServer { get; private set; }
    public string? ProjectRoot { get; private set; }
    public string? NodePath { get; private set; }
    public string LastEngineNote { get; private set; } = "尚未连接";

    // ---------------------------------------------------------------- 连接

    public async Task<bool> PingAsync(int timeoutMs = 1500)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            using var response = await _http.GetAsync("/api/health", cts.Token);
            if (!response.IsSuccessStatusCode) return false;
            var raw = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Bool("ok");
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureServerAsync(Action<string>? progress = null,
        CancellationToken cancellation = default)
    {
        if (await PingAsync(1200))
        {
            ReusedExistingServer = true;
            LastEngineNote = $"已连接本机引擎 · {BaseUrl}";
            progress?.Invoke("已连接本机引擎");
            return true;
        }

        ProjectRoot ??= ResolveProjectRoot();
        if (ProjectRoot is null)
        {
            LastEngineNote = @"未找到 plugins\codex-skin-engine 目录";
            return false;
        }

        var server = Path.Combine(ProjectRoot, "plugins", "codex-skin-engine", "scripts", "studio-server.mjs");
        if (!File.Exists(server))
        {
            LastEngineNote = $"引擎文件缺失：{server}";
            return false;
        }

        NodePath ??= ResolveNode();
        if (NodePath is null)
        {
            LastEngineNote = "未找到 Node.js，请安装 Node.js 20 或更新版本";
            return false;
        }

        progress?.Invoke("正在启动本地引擎…");
        try
        {
            var info = new ProcessStartInfo(NodePath)
            {
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            info.ArgumentList.Add(server);
            info.ArgumentList.Add("--port");
            info.ArgumentList.Add(Port.ToString());
            _server = Process.Start(info);
        }
        catch (Exception error)
        {
            LastEngineNote = $"启动本地引擎失败：{error.Message}";
            return false;
        }

        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (DateTime.UtcNow < deadline)
        {
            cancellation.ThrowIfCancellationRequested();
            if (await PingAsync(1200))
            {
                LastEngineNote = $"已启动本地引擎 · {BaseUrl}";
                progress?.Invoke("本地引擎已就绪");
                return true;
            }
            await Task.Delay(250, cancellation);
        }

        LastEngineNote = "本地引擎启动超时，请检查 Node.js 是否可用";
        return false;
    }

    public string? ResolveProjectRoot()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("CODEX_SKIN_ROOT"),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var candidate in candidates)
        {
            var found = WalkUp(candidate);
            if (found is not null) return found;
        }
        return null;
    }

    private static string? WalkUp(string? start)
    {
        if (string.IsNullOrWhiteSpace(start)) return null;
        var directory = new DirectoryInfo(start);
        for (var depth = 0; depth < 8 && directory is not null; depth++)
        {
            var probe = Path.Combine(directory.FullName, "plugins", "codex-skin-engine", "scripts", "studio-server.mjs");
            if (File.Exists(probe)) return directory.FullName;
            directory = directory.Parent;
        }
        return null;
    }

    private static string? ResolveNode()
    {
        // 发布目录里自带的 Node 优先，这样 dist\studio 可以整个拷走单独用。
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "node", "node.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "node", "node.exe")
        };
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            candidates.AddRange(path
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => Path.Combine(entry.Trim().Trim('"'), "node.exe")));
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"));

        var runtimes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "runtimes");
        if (Directory.Exists(runtimes))
        {
            try
            {
                candidates.AddRange(Directory
                    .EnumerateFiles(runtimes, "node.exe", SearchOption.AllDirectories)
                    .OrderByDescending(value => value));
            }
            catch
            {
                // 忽略无权限目录
            }
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    // ---------------------------------------------------------------- 数据

    public async Task<Bootstrap> BootstrapAsync(CancellationToken cancellation = default)
    {
        var data = await GetDataAsync("/api/bootstrap", cancellation);
        return ParseBootstrap(data);
    }

    public async Task<RuntimeReport> RuntimeAsync(string? path = null, CancellationToken cancellation = default)
    {
        var query = string.IsNullOrWhiteSpace(path) ? "" : $"?path={Uri.EscapeDataString(path)}";
        var data = await GetDataAsync($"/api/runtime{query}", cancellation);
        return ParseRuntime(data);
    }

    public async Task<RuntimeReport> PatchRuntimeAsync(string? path = null, CancellationToken cancellation = default)
    {
        var payload = JsonSerializer.Serialize(new { path = string.IsNullOrWhiteSpace(path) ? null : path });
        var data = await SendDataAsync(HttpMethod.Post, "/api/runtime/patch", Body.Json(payload), cancellation);
        return ParseRuntime(data);
    }

    public async Task<RuntimeReport> RestoreRuntimeAsync(string? path = null, CancellationToken cancellation = default)
    {
        var payload = JsonSerializer.Serialize(new { path = string.IsNullOrWhiteSpace(path) ? null : path });
        var data = await SendDataAsync(HttpMethod.Post, "/api/runtime/restore", Body.Json(payload), cancellation);
        return ParseRuntime(data);
    }

    public async Task<RuntimeReport> SetRuntimePathAsync(string path, CancellationToken cancellation = default)
    {
        var payload = JsonSerializer.Serialize(new { path });
        var data = await SendDataAsync(HttpMethod.Post, "/api/runtime/path", Body.Json(payload), cancellation);
        return ParseRuntime(data);
    }

    public async Task<SkinInfo> UpdateSkinAsync(string id, string name, string author, string description,
        CancellationToken cancellation = default)
    {
        var payload = JsonSerializer.Serialize(new { name, author, description });
        var data = await SendDataAsync(HttpMethod.Patch, $"/api/skin/{Uri.EscapeDataString(id)}", Body.Json(payload), cancellation);
        return ParseSkin(data);
    }

    public async Task<SkinInfo> ReplaceStateAsync(string id, string stateId, byte[] gif, CancellationToken cancellation = default)
    {
        var data = await SendDataAsync(HttpMethod.Post,
            $"/api/skin/{Uri.EscapeDataString(id)}/state/{Uri.EscapeDataString(stateId)}",
            Body.Raw(gif, "image/gif"), cancellation);
        return ParseSkin(data);
    }

    public async Task<SkinInfo> DuplicateAsync(string id, string name, CancellationToken cancellation = default)
    {
        var payload = JsonSerializer.Serialize(new { name });
        var data = await SendDataAsync(HttpMethod.Post, $"/api/skin/{Uri.EscapeDataString(id)}/duplicate", Body.Json(payload), cancellation);
        return ParseSkin(data);
    }

    public async Task<SkinInfo> ImportAsync(byte[] package, CancellationToken cancellation = default)
    {
        var data = await SendDataAsync(HttpMethod.Post, "/api/import", Body.Raw(package, "application/zip"), cancellation);
        return ParseSkin(data);
    }

    public async Task<ApplyReceipt> ApplyAsync(string id, CancellationToken cancellation = default)
    {
        var payload = JsonSerializer.Serialize(new { id });
        var data = await SendDataAsync(HttpMethod.Post, "/api/apply", Body.Json(payload), cancellation);
        return new ApplyReceipt
        {
            AppliedAt = data.Str("appliedAt"),
            Id = data.Str("id"),
            Name = data.Str("name"),
            Version = data.Str("version"),
            Target = data.Str("target"),
            Backup = string.IsNullOrWhiteSpace(data.Str("backup")) ? null : data.Str("backup")
        };
    }

    public Task<byte[]> ExportAsync(string id, CancellationToken cancellation = default) =>
        _http.GetByteArrayAsync($"/api/export?id={Uri.EscapeDataString(id)}", cancellation);

    public Task<byte[]> AssetAsync(string id, string file, string? hash, CancellationToken cancellation = default)
    {
        var path = string.Join("/", file.Split('/').Select(Uri.EscapeDataString));
        var suffix = string.IsNullOrWhiteSpace(hash) ? "" : $"?v={Uri.EscapeDataString(hash)}";
        return _http.GetByteArrayAsync($"/api/asset/{Uri.EscapeDataString(id)}/{path}{suffix}", cancellation);
    }

    // ---------------------------------------------------------------- 传输

    private readonly record struct Body(string? Text, byte[]? Bytes, string? ContentType)
    {
        public static Body Json(string text) => new(text, null, "application/json");
        public static Body Raw(byte[] bytes, string contentType) => new(null, bytes, contentType);
    }

    private async Task<JsonElement> GetDataAsync(string path, CancellationToken cancellation)
    {
        using var response = await _http.GetAsync(path, cancellation);
        return await UnwrapAsync(response);
    }

    private async Task<JsonElement> SendDataAsync(HttpMethod method, string path, Body body, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body.Bytes is not null)
        {
            request.Content = new ByteArrayContent(body.Bytes);
            request.Content.Headers.TryAddWithoutValidation("Content-Type", body.ContentType);
        }
        else
        {
            request.Content = new StringContent(body.Text ?? "", Encoding.UTF8, body.ContentType ?? "application/json");
        }

        using var response = await _http.SendAsync(request, cancellation);
        return await UnwrapAsync(response);
    }

    private static async Task<JsonElement> UnwrapAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch
        {
            throw new EngineException($"引擎返回了无法解析的内容（HTTP {(int)response.StatusCode}）");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var ok = root.Bool("ok");
            if (!response.IsSuccessStatusCode || !ok)
            {
                var message = root.Str("error");
                throw new EngineException(string.IsNullOrWhiteSpace(message)
                    ? $"请求失败（HTTP {(int)response.StatusCode}）"
                    : message);
            }
            return root.TryGetProperty("data", out var data) ? data.Clone() : default;
        }
    }

    // ---------------------------------------------------------------- 解析

    private static Bootstrap ParseBootstrap(JsonElement root)
    {
        var stateDefinitions = new List<StateInfo>();
        if (root.Arr("states") is { } states)
        {
            foreach (var item in states.EnumerateArray())
            {
                stateDefinitions.Add(new StateInfo
                {
                    Id = item.Str("id"),
                    Label = item.Str("label"),
                    Hint = item.Str("hint")
                });
            }
        }

        var skins = new List<SkinInfo>();
        if (root.Arr("skins") is { } list)
        {
            foreach (var item in list.EnumerateArray()) skins.Add(ParseSkin(item, stateDefinitions));
        }

        return new Bootstrap
        {
            Version = root.Str("version"),
            States = stateDefinitions,
            Skins = skins,
            Runtime = root.Obj("runtime") is { } runtime ? ParseRuntime(runtime) : new RuntimeReport(),
            LastApply = ParseReceipt(root.Obj("lastApply") ?? default),
            Paths = ParsePaths(root.Obj("paths") ?? default)
        };
    }

    private static PathsInfo ParsePaths(JsonElement root) => new()
    {
        Plugin = root.Str("plugin"),
        Data = root.Str("data"),
        Pets = root.Str("pets")
    };

    private static ApplyReceipt ParseReceipt(JsonElement root) => new()
    {
        AppliedAt = root.Str("appliedAt"),
        Id = root.Str("id"),
        Name = root.Str("name"),
        Version = root.Str("version"),
        Target = root.Str("target"),
        Backup = string.IsNullOrWhiteSpace(root.Str("backup")) ? null : root.Str("backup")
    };

    public static SkinInfo ParseSkin(JsonElement root, IReadOnlyList<StateInfo>? definitions = null)
    {
        var states = new Dictionary<string, StateInfo>(StringComparer.OrdinalIgnoreCase);
        if (root.Obj("states") is { } stateMap)
        {
            foreach (var property in stateMap.EnumerateObject())
            {
                var item = property.Value;
                states[property.Name] = new StateInfo
                {
                    Id = item.Str("id", property.Name),
                    Label = item.Str("label"),
                    Hint = item.Str("hint"),
                    File = item.Str("file"),
                    Sha256 = item.Str("sha256"),
                    Width = item.Int("width"),
                    Height = item.Int("height"),
                    Frames = item.Int("frames"),
                    Bytes = item.Long("bytes"),
                    DurationMs = item.Int("durationMs"),
                    AverageFrameMs = item.Int("averageFrameMs")
                };
            }
        }

        var ordered = new List<StateInfo>();
        if (definitions is { Count: > 0 })
        {
            foreach (var definition in definitions)
            {
                if (!states.TryGetValue(definition.Id, out var state)) continue;
                ordered.Add(new StateInfo
                {
                    Id = state.Id,
                    Label = string.IsNullOrWhiteSpace(state.Label) ? definition.Label : state.Label,
                    Hint = string.IsNullOrWhiteSpace(state.Hint) ? definition.Hint : state.Hint,
                    File = state.File,
                    Sha256 = state.Sha256,
                    Width = state.Width,
                    Height = state.Height,
                    Frames = state.Frames,
                    Bytes = state.Bytes,
                    DurationMs = state.DurationMs,
                    AverageFrameMs = state.AverageFrameMs
                });
            }
        }
        else
        {
            ordered.AddRange(states.Values);
        }

        return new SkinInfo
        {
            Id = root.Str("id"),
            Name = root.Str("name"),
            Description = root.Str("description"),
            Version = root.Str("version"),
            AuthorName = root.Obj("author") is { } author ? author.Str("name") : "",
            License = root.Str("license"),
            BuiltIn = root.Bool("builtIn"),
            Valid = root.Bool("valid", true),
            Error = string.IsNullOrWhiteSpace(root.Str("error")) ? null : root.Str("error"),
            Bytes = root.Long("bytes"),
            Files = root.Int("files"),
            Thumbnail = string.IsNullOrWhiteSpace(root.Str("thumbnail")) ? null : root.Str("thumbnail"),
            States = states,
            OrderedStates = ordered
        };
    }

    public static RuntimeReport ParseRuntime(JsonElement root)
    {
        var targets = root.Obj("targets");
        var main = targets?.Obj("main");
        var renderer = targets?.Obj("renderer");
        return new RuntimeReport
        {
            State = root.Str("state", "unknown"),
            Message = root.Str("message"),
            Asar = root.Str("asar"),
            RuntimeProfile = root.Str("runtimeProfile"),
            Ok = root.Bool("ok"),
            Compatible = root.Bool("compatible"),
            HeaderSize = root.Int("headerSize"),
            MainPatched = main?.Bool("gifPatched") ?? false,
            RendererPatched = renderer?.Bool("gifPatched") ?? false,
            MainIntegrityOk = main?.Bool("integrityOk") ?? false,
            RendererIntegrityOk = renderer?.Bool("integrityOk") ?? false
        };
    }

    public void Dispose()
    {
        _http.Dispose();
        if (_server is { HasExited: false })
        {
            try
            {
                _server.Kill(true);
                _server.WaitForExit(2000);
            }
            catch
            {
                // 进程已退出
            }
        }
        _server?.Dispose();
    }
}
