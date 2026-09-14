[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$project = Join-Path $projectRoot "native\GrillingSkinStudio\GrillingSkinStudio.csproj"
$output = Join-Path $projectRoot "dist\grilling"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue) -or -not (& dotnet --list-sdks)) { throw "找不到 .NET SDK。请安装 .NET 7 SDK 后重试。" }

$runtimeArgs = if ($SelfContained) { @("--self-contained", "true", "-p:PublishSingleFile=true", "-p:PublishTrimmed=true") } else { @("--self-contained", "false") }
dotnet publish $project -c $Configuration -r win-x64 -o $output @runtimeArgs
Write-Host "Grilling 原生工坊已生成：$output\GrillingSkinStudio.exe" -ForegroundColor Green
