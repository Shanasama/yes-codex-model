[CmdletBinding()]
param(
    [switch]$Plan,
    [string]$ProjectRoot,
    [string]$NodePath
)

$ErrorActionPreference = "Stop"
if (-not $ProjectRoot) { $ProjectRoot = $PSScriptRoot }
$projectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$studioPath = Join-Path $projectRoot "plugins\codex-skin-engine\scripts\studio-server.mjs"

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
            Sort-Object FullName -Descending |
            Select-Object -ExpandProperty FullName
    }
    $resolved = Resolve-FirstFile $candidates
    if (-not $resolved) {
        throw "找不到 Node.js。请安装 Node.js 20 或更新版本，或先启动一次 Codex。"
    }
    return $resolved
}

if (-not (Test-Path -LiteralPath $studioPath -PathType Leaf)) {
    throw "皮肤工坊文件不完整：$studioPath"
}

$nodeExe = Resolve-Node -Requested $NodePath
if ($Plan) {
    [pscustomobject]@{
        projectRoot = $projectRoot
        node = $nodeExe
        studio = $studioPath
        url = "http://127.0.0.1:43821"
        note = "Plan 模式未启动服务或浏览器。"
    } | ConvertTo-Json -Depth 4
    exit 0
}

$studioArgument = '"' + $studioPath + '"'
Start-Process -FilePath $nodeExe -ArgumentList @($studioArgument, "--open") -WorkingDirectory $projectRoot -WindowStyle Hidden
Write-Host "Codex 皮肤工坊正在启动：http://127.0.0.1:43821" -ForegroundColor Green
