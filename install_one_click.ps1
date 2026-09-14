[CmdletBinding()]
param(
    [switch]$Plan,
    [switch]$NoLaunch,
    [switch]$NoPrompt,
    [switch]$SkipStoreCheck,
    [switch]$PluginOnly,
    [switch]$SkipStudio,
    [string]$ProjectRoot,
    [string]$NodePath,
    [string]$CodexPath
)

$ErrorActionPreference = "Stop"
if (-not $ProjectRoot) { $ProjectRoot = $PSScriptRoot }
$projectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$pluginRoot = Join-Path $projectRoot "plugins\codex-skin-engine"
$cliPath = Join-Path $pluginRoot "scripts\cli.mjs"
$shadowInstaller = Join-Path $pluginRoot "scripts\install_shadow_runtime.ps1"

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[Codex Skin Engine] $Message" -ForegroundColor Cyan
}

function Resolve-FirstFile {
    param([string[]]$Candidates)
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

function Resolve-Node {
    param([string]$Requested)
    $candidates = @()
    if ($Requested) { $candidates += $Requested }
    $command = Get-Command node -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $candidates += @(
        (Join-Path ${env:ProgramFiles} "nodejs\node.exe"),
        (Join-Path ${env:LOCALAPPDATA} "Programs\nodejs\node.exe")
    )
    $runtimeRoot = Join-Path ${env:LOCALAPPDATA} "OpenAI\Codex\runtimes"
    if (Test-Path -LiteralPath $runtimeRoot -PathType Container) {
        $candidates += Get-ChildItem -LiteralPath $runtimeRoot -Filter node.exe -File -Recurse -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }
    $resolved = Resolve-FirstFile $candidates
    if (-not $resolved) {
        throw "找不到 Node.js。请先安装 Node.js LTS，或先启动一次 Codex。"
    }
    return $resolved
}

function Resolve-Codex {
    param([string]$Requested, [string]$Fallback)
    $candidates = @()
    if ($Requested) { $candidates += $Requested }
    if ($Fallback) { $candidates += $Fallback }
    $command = Get-Command codex -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $binRoot = Join-Path ${env:LOCALAPPDATA} "OpenAI\Codex\bin"
    if (Test-Path -LiteralPath $binRoot -PathType Container) {
        $candidates += Get-ChildItem -LiteralPath $binRoot -Filter codex.exe -File -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    $resolved = Resolve-FirstFile $candidates
    if (-not $resolved) {
        throw "找不到 codex 命令。请先启动一次 Codex 桌面版。"
    }
    return $resolved
}

function Invoke-Required {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Label
    )
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label 失败，退出码：$LASTEXITCODE"
    }
}

function Resolve-StorePackage {
    param([switch]$Optional)
    $package = Get-AppxPackage -Name "OpenAI.Codex" -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending | Select-Object -First 1
    if (-not $package) {
        $package = Get-AppxPackage -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "OpenAI.Codex*" } |
            Sort-Object Version -Descending | Select-Object -First 1
    }
    if (-not $package) {
        if ($Optional) { return $null }
        throw "找不到 OpenAI Codex Store 安装包。"
    }
    $appRoot = Join-Path $package.InstallLocation "app"
    $executable = Join-Path $appRoot "ChatGPT.exe"
    $asar = Join-Path $appRoot "resources\app.asar"
    $codexCli = Join-Path $appRoot "resources\codex.exe"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "找不到 Store Codex 主程序：$executable"
    }
    if (-not (Test-Path -LiteralPath $asar -PathType Leaf)) {
        throw "找不到 Store Codex app.asar：$asar"
    }
    [pscustomobject]@{
        Name = [string]$package.Name
        Version = [string]$package.Version
        InstallLocation = (Resolve-Path -LiteralPath $package.InstallLocation).Path
        Executable = (Resolve-Path -LiteralPath $executable).Path
        Asar = (Resolve-Path -LiteralPath $asar).Path
        CodexCli = if (Test-Path -LiteralPath $codexCli -PathType Leaf) { (Resolve-Path -LiteralPath $codexCli).Path } else { $null }
    }
}

function Install-PluginAndSkin {
    param(
        [Parameter(Mandatory = $true)][string]$CodexExecutable,
        [Parameter(Mandatory = $true)][string]$NodeExecutable
    )
    Write-Step "登记当前目录中的本地插件市场。"
    Invoke-Required $CodexExecutable @("plugin", "marketplace", "add", $projectRoot) "本地插件市场登记"

    Write-Step "检查并安装 codex-skin-engine 插件。"
    $pluginListText = (& $CodexExecutable plugin list --marketplace personal --json 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw "无法读取本地插件状态：$pluginListText"
    }
    try {
        $pluginState = $pluginListText | ConvertFrom-Json
    } catch {
        throw "本地插件状态不是有效 JSON：$pluginListText"
    }
    $installedPlugin = @($pluginState.installed | Where-Object { $_.pluginId -eq "codex-skin-engine@personal" }) |
        Select-Object -First 1
    if ($installedPlugin -and $installedPlugin.enabled -eq $true) {
        Write-Step "插件已安装并启用，跳过重复安装。"
    } elseif ($installedPlugin) {
        throw "插件已安装但处于禁用状态。请先在 Codex 设置中启用它。"
    } else {
        Invoke-Required $CodexExecutable @("plugin", "add", "codex-skin-engine@personal") "插件安装"
    }

    Write-Step "安装 WineFox 原版酒狐皮肤。"
    Invoke-Required $NodeExecutable @($cliPath, "apply", "winefox-pixel-classic") "皮肤安装"
}

function Get-StoreProcesses {
    param([Parameter(Mandatory = $true)][string]$Executable)
    $fullPath = [IO.Path]::GetFullPath($Executable)
    @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -ieq "ChatGPT.exe" -and $_.ExecutablePath -and
        ([IO.Path]::GetFullPath([string]$_.ExecutablePath)).Equals($fullPath, [StringComparison]::OrdinalIgnoreCase)
    })
}

function Ensure-StoreStopped {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][bool]$AllowPrompt
    )
    $running = @(Get-StoreProcesses -Executable $Executable)
    if ($running.Count -eq 0) { return }
    if (-not $AllowPrompt) {
        throw "Store Codex 仍在运行，已停止以避免无提示关闭。"
    }
    Write-Host "检测到 Store 版 Codex。脚本下一步会关闭它的全部进程，请先保存工作。" -ForegroundColor Yellow
    [void](Read-Host "确认关闭 Store Codex 后按 Enter 继续；取消请关闭此窗口")
    $running = @(Get-StoreProcesses -Executable $Executable)
    foreach ($process in $running) {
        $processId = [int]$process.ProcessId
        if (-not (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
            continue
        }
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
        } catch {
            if (-not (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
                continue
            }
            throw "无法关闭 Store Codex PID $processId：$($_.Exception.Message)"
        }
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 300
        $running = @(Get-StoreProcesses -Executable $Executable)
    } while ($running.Count -gt 0 -and [DateTime]::UtcNow -lt $deadline)
    if ($running.Count -gt 0) {
        throw "Store Codex 进程未能在 20 秒内退出，请手动关闭后重试。"
    }
    Write-Step "Store Codex 已关闭，继续安装。"
}

function Ensure-Studio {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $studioExe = Join-Path $ProjectRoot "dist\studio\SkinStudio.exe"
    if (Test-Path -LiteralPath $studioExe -PathType Leaf) {
        Write-Step "皮肤工坊已经编译过了。"
        return $studioExe
    }
    $builder = Join-Path $ProjectRoot "build_studio.ps1"
    if (-not (Test-Path -LiteralPath $builder -PathType Leaf)) {
        Write-Warning "找不到构建脚本：$builder，跳过皮肤工坊编译。"
        return $null
    }
    Write-Step "编译皮肤工坊（独立窗口程序，首次需要几分钟，会下载 .NET 运行时包）。"
    & $powershellExe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $builder
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $studioExe -PathType Leaf)) {
        Write-Warning "皮肤工坊编译没成功，稍后可以双击 启动皮肤工坊.cmd 重试。"
        return $null
    }
    return $studioExe
}

if (-not (Test-Path -LiteralPath $cliPath -PathType Leaf)) {
    throw "插件文件不完整，找不到：$cliPath"
}
if (-not (Test-Path -LiteralPath $shadowInstaller -PathType Leaf)) {
    throw "插件文件不完整，找不到：$shadowInstaller"
}

$package = Resolve-StorePackage -Optional:$PluginOnly
$nodeExe = Resolve-Node -Requested $NodePath

$powershellExe = Join-Path ${env:SystemRoot} "System32\WindowsPowerShell\v1.0\powershell.exe"
if (-not (Test-Path -LiteralPath $powershellExe -PathType Leaf)) {
    throw "找不到 Windows PowerShell：$powershellExe"
}

if ($PluginOnly) {
    $packageCodex = if ($package) { $package.CodexCli } else { $null }
    $codexExe = Resolve-Codex -Requested $CodexPath -Fallback $packageCodex
    if ($Plan) {
        [pscustomobject]@{
            mode = "plugin-only"
            projectRoot = $projectRoot
            node = $nodeExe
            codex = $codexExe
            package = if ($package) { $package.Name } else { $null }
            version = if ($package) { $package.Version } else { $null }
            note = "Plan 模式未登记市场、安装插件或应用皮肤。"
        } | ConvertTo-Json -Depth 5
        exit 0
    }
    Install-PluginAndSkin -CodexExecutable $codexExe -NodeExecutable $nodeExe
    $studioExe = if ($SkipStudio) { $null } else { Ensure-Studio -ProjectRoot $projectRoot }
    Write-Host "完成：插件和 WineFox 皮肤已安装。" -ForegroundColor Green
    Write-Host "皮肤工坊启动器：$(Join-Path $projectRoot '启动皮肤工坊.cmd')"
    if ($studioExe) { Write-Host "皮肤工坊程序：$studioExe（双击就能改皮肤）" }
    Write-Host "请新建一个 Codex 任务以载入插件。" -ForegroundColor Green
    exit 0
}

$versionKey = $package.Version -replace "[^0-9A-Za-z._-]", "_"
$shadowRoot = Join-Path ${env:LOCALAPPDATA} "OpenAI\Codex\skin-engine\shadow-runtimes\store-$versionKey"
$shadowCodex = Join-Path $shadowRoot "app\resources\codex.exe"

if ($Plan) {
    [pscustomobject]@{
        projectRoot = $projectRoot
        node = $nodeExe
        codex = $shadowCodex
        package = $package.Name
        version = $package.Version
        storeExecutable = $package.Executable
        storeAsar = $package.Asar
        shadowInstaller = $shadowInstaller
        note = "Plan 模式未执行插件安装、皮肤应用、文件复制或运行时启动。"
    } | ConvertTo-Json -Depth 5
    exit 0
}

if ($SkipStoreCheck) {
    Write-Step "已跳过关闭 Store Codex 的步骤（免交互模式），安装结束后请自行退出 Codex 再启动。"
} else {
    Ensure-StoreStopped -Executable $package.Executable -AllowPrompt (-not $NoPrompt)
}

Write-Step "准备可写 shadow runtime。首次安装需要复制约 1.8 GB，请耐心等待。"
& $powershellExe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $shadowInstaller -Action Install -ProjectRoot $projectRoot
if ($LASTEXITCODE -ne 0) {
    throw "shadow runtime 准备失败，退出码：$LASTEXITCODE"
}
if (-not (Test-Path -LiteralPath $shadowCodex -PathType Leaf)) {
    throw "shadow runtime 缺少可用 codex.exe：$shadowCodex"
}
$codexExe = Resolve-Codex -Requested $shadowCodex -Fallback $CodexPath

Install-PluginAndSkin -CodexExecutable $codexExe -NodeExecutable $nodeExe

if ($NoLaunch) {
    $studioExe = if ($SkipStudio) { $null } else { Ensure-Studio -ProjectRoot $projectRoot }
    Write-Host "安装完成。" -ForegroundColor Green
    Write-Host "启动器：$(Join-Path $projectRoot '备用启动脚本\启动可写 GIF 运行时.cmd')"
    if ($studioExe) { Write-Host "皮肤工坊程序：$studioExe（双击就能改皮肤）" }
    Write-Host "使用前请完全退出 Codex，再双击上面的启动器。" -ForegroundColor Green
    exit 0
}

$shadowExecutable = Join-Path $shadowRoot "app\ChatGPT.exe"
$shadowRunning = @(Get-StoreProcesses -Executable $shadowExecutable)
if ($shadowRunning.Count -gt 0) {
    Write-Host "shadow Codex 已经在运行，跳过重复启动。" -ForegroundColor Green
    exit 0
}

Write-Step "启动可写 GIF runtime。"
& $powershellExe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $shadowInstaller -Action Launch -ProjectRoot $projectRoot
if ($LASTEXITCODE -ne 0) {
    throw "shadow runtime 启动失败，退出码：$LASTEXITCODE"
}
Write-Host "完成：WineFox GIF runtime 已启动。" -ForegroundColor Green
