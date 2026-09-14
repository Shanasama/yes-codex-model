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

function Resolve-Dotnet {
    if (Test-Path -LiteralPath $localSdk) { return $localSdk }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw "找不到 dotnet。请安装 .NET 7 SDK，或把 SDK 解压到 $projectRoot\.dotnet-sdk。"
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
if ($LASTEXITCODE -ne 0) { throw "构建失败，退出码 $LASTEXITCODE" }

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
