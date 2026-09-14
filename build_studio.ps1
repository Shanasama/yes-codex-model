[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$project = Join-Path $projectRoot "native\SkinStudio\SkinStudio.csproj"
$output = Join-Path $projectRoot "dist\studio"
$engineSource = Join-Path $projectRoot "plugins\codex-skin-engine"
$localSdk = Join-Path $projectRoot ".dotnet-sdk\dotnet.exe"

function Resolve-FirstFile {
    param([string[]]$Candidates)
    foreach ($candidate in $Candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

function Get-DotnetSdk {
    # 只认“带 SDK 的 dotnet”：光有运行时（dotnet --list-sdks 为空）编译不了工坊。
    param([Parameter(Mandatory = $true)][string]$Executable)
    $output = @()
    try {
        $output = @(& $Executable --list-sdks 2>$null)
    } catch {
        return $null
    }
    if ($LASTEXITCODE -ne 0) { return $null }
    foreach ($line in $output) {
        if ("$line" -match '^\s*(\d+)\.\d+') {
            if ([int]$Matches[1] -ge 7) { return "$line".Trim() }
        }
    }
    return $null
}

function Resolve-Dotnet {
    $candidates = @()
    if (Test-Path -LiteralPath $localSdk) { $candidates += $localSdk }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }

    $withoutSdk = @()
    foreach ($candidate in $candidates) {
        $sdk = Get-DotnetSdk -Executable $candidate
        if ($sdk) {
            Write-Host ("使用的 .NET SDK：{0}（{1}）" -f $sdk, $candidate)
            return $candidate
        }
        $withoutSdk += $candidate
    }

    $detail = if ($withoutSdk.Count -gt 0) {
        "找到了 dotnet，但它里面没有可用的 SDK（dotnet --list-sdks 为空或版本低于 7）：`n" +
        (($withoutSdk | ForEach-Object { "  - $_" }) -join "`n")
    } else {
        "PATH 里没有 dotnet。"
    }
    throw @"
编译皮肤工坊需要 .NET 7 SDK，只装 .NET 运行时不够。$detail

请二选一，然后重新运行本脚本或双击 启动皮肤工坊.cmd：
  1. 安装 .NET 7 SDK（或更高版本）：https://dotnet.microsoft.com/download/dotnet/7.0
  2. 下载 .NET 7 SDK 的 ZIP 版，解压后把里面的文件（含 dotnet.exe）全部放到：
     $projectRoot\.dotnet-sdk\
     放好之后，这个目录里应该直接就有 dotnet.exe。

插件和酒狐皮肤那部分不需要 SDK，只有这个工坊窗口程序需要。
"@
}

function Resolve-Node {
    $candidates = @()
    $command = Get-Command node -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $candidates += @(
        (Join-Path ${env:ProgramFiles} "nodejs\node.exe"),
        (Join-Path ${env:LOCALAPPDATA} "Programs\nodejs\node.exe")
    )
    $runtimeRoot = Join-Path ${env:LOCALAPPDATA} "OpenAI\Codex\runtimes"
    if (Test-Path -LiteralPath $runtimeRoot -PathType Container) {
        $candidates += Get-ChildItem -LiteralPath $runtimeRoot -Filter node.exe -File -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    return (Resolve-FirstFile $candidates)
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "找不到项目文件：$project"
}

$dotnet = Resolve-Dotnet
$env:DOTNET_ROOT = Split-Path -Parent $dotnet

$runtimeArgs = if ($FrameworkDependent) { @("--self-contained", "false") } else { @("--self-contained", "true", "-p:PublishSingleFile=true") }

& $dotnet publish $project -c $Configuration -r win-x64 -o $output @runtimeArgs
if ($LASTEXITCODE -ne 0) {
    throw "编译皮肤工坊失败（dotnet publish 退出码 $LASTEXITCODE）。常见原因是缺 .NET 7 SDK，或没有联网下载运行时包；请把上面的报错原文发给 AI 一起看。"
}

# 把引擎脚本和 Node 运行时一起放进发布目录：dist\studio 拷到别的电脑也能直接双击运行。
$bundledEngine = Join-Path $output "plugins\codex-skin-engine"
if (Test-Path -LiteralPath $bundledEngine -PathType Container) {
    $resolvedEngine = (Resolve-Path -LiteralPath $bundledEngine).Path
    if (-not $resolvedEngine.StartsWith($output, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝清理发布目录之外的内容：$resolvedEngine"
    }
    Remove-Item -LiteralPath $resolvedEngine -Recurse -Force
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $bundledEngine) | Out-Null
Copy-Item -LiteralPath $engineSource -Destination $bundledEngine -Recurse -Force

$nodeExe = Resolve-Node
if ($nodeExe) {
    $nodeTarget = Join-Path $output "node"
    New-Item -ItemType Directory -Force -Path $nodeTarget | Out-Null
    Copy-Item -LiteralPath $nodeExe -Destination (Join-Path $nodeTarget "node.exe") -Force
}
else {
    Write-Warning "没找到 Node.js，dist\studio\node\node.exe 没有生成；运行时只能用系统安装的 Node。"
}

$exe = Join-Path $output "SkinStudio.exe"
Write-Host ("皮肤工坊已生成：{0}" -f $exe) -ForegroundColor Green
Write-Host ("exe {0:N0} KB；dist\studio 整个文件夹可以单独拷走使用。" -f ((Get-Item $exe).Length / 1KB))
