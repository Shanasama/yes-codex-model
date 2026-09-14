[CmdletBinding()]
param(
    [ValidateSet("Plan", "Install", "Launch", "Status", "Restore")]
    [string]$Action = "Install",
    [string]$ProjectRoot,
    [string]$NodePath,
    [string]$ShadowRoot,
    [string]$ProfilePath,
    [switch]$UseIsolatedProfile,
    [switch]$ForceRefresh
)

$ErrorActionPreference = "Stop"

if (-not $ProjectRoot) {
    $ProjectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$pluginRoot = Join-Path $projectRoot "plugins\codex-skin-engine"
$patcherPath = Join-Path $pluginRoot "scripts\patch_codex_gif_runtime.mjs"

function Resolve-Node {
    param([string]$Requested)
    if ($Requested -and (Test-Path -LiteralPath $Requested -PathType Leaf)) {
        return (Resolve-Path -LiteralPath $Requested).Path
    }
    $command = Get-Command node -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles "nodejs\node.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\nodejs\node.exe")
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    $runtimeRoot = Join-Path $env:LOCALAPPDATA "OpenAI\Codex\runtimes"
    if (Test-Path -LiteralPath $runtimeRoot -PathType Container) {
        $bundled = Get-ChildItem -LiteralPath $runtimeRoot -Filter node.exe -File -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1 -ExpandProperty FullName
        if ($bundled) { return (Resolve-Path -LiteralPath $bundled).Path }
    }
    throw "Node.js was not found."
}

function Resolve-CodexPackage {
    $package = Get-AppxPackage -Name "OpenAI.Codex" -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending | Select-Object -First 1
    if (-not $package) {
        $package = Get-AppxPackage -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "OpenAI.Codex*" } |
            Sort-Object Version -Descending | Select-Object -First 1
    }
    if (-not $package) { throw "Windows Store Codex package was not found." }
    $appRoot = Join-Path $package.InstallLocation "app"
    $asar = Join-Path $appRoot "resources\app.asar"
    if (-not (Test-Path -LiteralPath $asar -PathType Leaf)) {
        throw "Store app.asar was not found: $asar"
    }
    [pscustomobject]@{
        Name = [string]$package.Name
        Version = [string]$package.Version
        FullName = [string]$package.PackageFullName
        InstallLocation = (Resolve-Path -LiteralPath $package.InstallLocation).Path
        AppRoot = (Resolve-Path -LiteralPath $appRoot).Path
        AsarPath = (Resolve-Path -LiteralPath $asar).Path
    }
}

function Invoke-PatcherJson {
    param(
        [Parameter(Mandatory = $true)][string]$Node,
        [Parameter(Mandatory = $true)][string]$Asar,
        [Parameter(Mandatory = $true)][string]$Patcher,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )
    $lines = @(& $Node $Patcher @Arguments "--asar" $Asar 2>&1)
    $exitCode = $LASTEXITCODE
    $text = ($lines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    if ($exitCode -ne 0) {
        throw "Runtime patcher failed (exit $exitCode): $text"
    }
    try {
        return $text | ConvertFrom-Json
    } catch {
        throw "Runtime patcher returned invalid JSON: $text"
    }
}

function Write-JsonAtomic {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Value)
    $temporary = "$Path.new-$PID"
    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

function Get-ProfilePath {
    if ($ProfilePath) { return [IO.Path]::GetFullPath($ProfilePath) }
    if ($UseIsolatedProfile) { return [IO.Path]::GetFullPath((Join-Path $ShadowRoot "profile")) }
    return [IO.Path]::GetFullPath((Join-Path $env:APPDATA "Codex\web\Codex"))
}

function Invoke-RobocopyApp {
    param([Parameter(Mandatory = $true)][string]$Source, [Parameter(Mandatory = $true)][string]$Destination)
    $sourceFull = [IO.Path]::GetFullPath($Source).TrimEnd("\")
    $destinationFull = [IO.Path]::GetFullPath($Destination).TrimEnd("\")
    if ($destinationFull.Equals($sourceFull, [StringComparison]::OrdinalIgnoreCase) -or
        $destinationFull.StartsWith($sourceFull + "\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Shadow destination must not be inside the Store package."
    }
    New-Item -ItemType Directory -Force -Path $destinationFull | Out-Null
    Write-Host "Copying the Store app to the writable shadow runtime. This is about 1.8 GB on this release."
    & robocopy.exe $sourceFull $destinationFull /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /MT:16 /XJ /NFL /NDL /NJH /NJS /NP
    $code = $LASTEXITCODE
    if ($code -ge 8) { throw "robocopy failed with exit code $code" }
}

function Get-RunningAtPath {
    param([Parameter(Mandatory = $true)][string]$Executable)
    $full = [IO.Path]::GetFullPath($Executable)
    @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -ieq "ChatGPT.exe" -and $_.ExecutablePath -and
        ([IO.Path]::GetFullPath([string]$_.ExecutablePath)).Equals($full, [StringComparison]::OrdinalIgnoreCase)
    })
}

function Write-ShadowLauncher {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$Profile
    )
    $root = Split-Path -Parent $Path
    $scriptPath = Join-Path $root "launch-codex-shadow.ps1"
    $escapedProfile = $Profile.Replace("'", "''")
    $scriptLines = @(
        '$ErrorActionPreference = "Stop"',
        '$root = Split-Path -Parent $MyInvocation.MyCommand.Path',
        ('$profile = ''{0}''' -f $escapedProfile),
        '$executable = Join-Path $root "app\ChatGPT.exe"',
        '$runningOther = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq "ChatGPT.exe" -and $_.ExecutablePath -and -not ([IO.Path]::GetFullPath([string]$_.ExecutablePath)).Equals([IO.Path]::GetFullPath($executable), [StringComparison]::OrdinalIgnoreCase) })',
        'if ($runningOther.Count -gt 0) { throw "Another Codex process is running. Close the Store app before starting the shadow runtime." }',
        '$env:CODEX_ELECTRON_USER_DATA_PATH = $profile',
        '$argument = ''--user-data-dir="'' + $profile + ''"''',
        'Start-Process -FilePath $executable -WorkingDirectory (Join-Path $root "app") -ArgumentList $argument | Out-Null'
    )
    Set-Content -LiteralPath $scriptPath -Value $scriptLines -Encoding UTF8
    $lines = @(
        "@echo off",
        "setlocal",
        ('set "CODEX_ELECTRON_USER_DATA_PATH={0}"' -f $Profile),
        '"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0launch-codex-shadow.ps1"',
        "exit /b 0"
    )
    Set-Content -LiteralPath $Path -Value $lines -Encoding ASCII
}

function Get-AsarHeaderSize {
    param([Parameter(Mandatory = $true)][string]$AsarPath)
    try {
        $stream = [IO.File]::OpenRead($AsarPath)
        try {
            $prefix = [byte[]]::new(8)
            if ($stream.Read($prefix, 0, 8) -ne 8) { return $null }
            return [int][BitConverter]::ToUInt32($prefix, 4)
        } finally {
            $stream.Dispose()
        }
    } catch {
        return $null
    }
}

function Copy-AsarHeaderBackup {
    param(
        [Parameter(Mandatory = $true)][string]$AsarPath,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][int]$ExpectedLength
    )
    $headerSize = Get-AsarHeaderSize -AsarPath $AsarPath
    if ($null -eq $headerSize -or $headerSize -ne $ExpectedLength) { return $false }
    try {
        $stream = [IO.File]::OpenRead($AsarPath)
        try {
            $buffer = [byte[]]::new($headerSize)
            $position = 0
            while ($position -lt $headerSize) {
                $read = $stream.Read($buffer, $position, $headerSize - $position)
                if ($read -le 0) { return $false }
                $position += $read
            }
        } finally {
            $stream.Dispose()
        }
    } catch {
        return $false
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
    [IO.File]::WriteAllBytes($Destination, $buffer)
    return $true
}

function Reset-ShadowRuntimeCopy {
    if ((Get-RunningAtPath $shadowExe).Count -gt 0) {
        throw "The shadow Codex process is running; stop it before rebuilding the shadow runtime."
    }
    if (Test-Path -LiteralPath $ShadowRoot -PathType Container) {
        $oldRoot = "$ShadowRoot.backup-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))"
        Move-Item -LiteralPath $ShadowRoot -Destination $oldRoot | Out-Null
        Write-Host "Moved the previous shadow runtime to $oldRoot"
    }
    New-Item -ItemType Directory -Force -Path $ShadowRoot | Out-Null
    Invoke-RobocopyApp -Source $package.AppRoot -Destination $shadowApp
    if (-not (Test-Path -LiteralPath $shadowAsar -PathType Leaf)) {
        throw "Shadow app.asar was not copied: $shadowAsar"
    }
    $report = Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "verify")
    return $report
}

$node = Resolve-Node $NodePath
$package = Resolve-CodexPackage
$versionKey = $package.Version -replace "[^0-9A-Za-z._-]", "_"
if (-not $ShadowRoot) {
    $ShadowRoot = Join-Path $env:LOCALAPPDATA "OpenAI\Codex\skin-engine\shadow-runtimes\store-$versionKey"
}
$ShadowRoot = [IO.Path]::GetFullPath($ShadowRoot)
$profile = Get-ProfilePath
$shadowApp = Join-Path $ShadowRoot "app"
$shadowExe = Join-Path $shadowApp "ChatGPT.exe"
$shadowAsar = Join-Path $shadowApp "resources\app.asar"
$metadataPath = Join-Path $ShadowRoot "shadow-runtime.json"
$backupDir = Join-Path $ShadowRoot "runtime-backup"
$launcherPath = Join-Path $ShadowRoot "launch-codex-shadow.cmd"

$sourceStatus = Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $package.AsarPath -Arguments @("--action", "verify")
$base = [ordered]@{
    action = $Action
    package = $package.FullName
    version = $package.Version
    sourceAsar = $package.AsarPath
    sourceState = $sourceStatus.state
    sourceAsarSha256 = $sourceStatus.asarSha256
    shadowRoot = $ShadowRoot
    shadowAsar = $shadowAsar
    profile = $profile
    launcher = $launcherPath
}

if ($Action -eq "Plan") {
    $base.note = "Plan only. No files were copied or patched."
    $base | ConvertTo-Json -Depth 8
    exit 0
}

if ($Action -eq "Status") {
    $requiredShadowFiles = @(
        $shadowAsar,
        $shadowExe,
        (Join-Path $shadowApp "chrome.dll")
    )
    if ($requiredShadowFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }) {
        $base.state = "not-installed"
        $base.ok = $false
        $base | ConvertTo-Json -Depth 8
        exit 0
    }
    $status = Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "verify")
    $base.state = $status.state
    $base.ok = $status.state -eq "gif-patched"
    $base.runtime = $status
    if (Test-Path -LiteralPath $metadataPath -PathType Leaf) {
        $base.metadata = Get-Content -Raw -Encoding UTF8 -LiteralPath $metadataPath | ConvertFrom-Json
    }
    $base | ConvertTo-Json -Depth 12
    exit 0
}

if ($Action -eq "Restore") {
    if (-not (Test-Path -LiteralPath $shadowAsar -PathType Leaf)) {
        throw "Shadow runtime is not installed: $shadowAsar"
    }
    $status = Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "verify")
    if ($status.state -ne "baseline") {
        if (-not (Test-Path -LiteralPath (Join-Path $backupDir "header-baseline.pickle") -PathType Leaf)) {
            throw "Shadow runtime backup is missing; refusing restore."
        }
        Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "restore", "--backup-dir", $backupDir) | Out-Null
    }
    $base.state = "baseline"
    $base.ok = $true
    if (Test-Path -LiteralPath $metadataPath -PathType Leaf) {
        try {
            $restoreMetadata = Get-Content -Raw -Encoding UTF8 -LiteralPath $metadataPath | ConvertFrom-Json
            $restoreMetadata.state = "baseline"
            Write-JsonAtomic -Path $metadataPath -Value $restoreMetadata
        } catch { }
    }
    $base | ConvertTo-Json -Depth 8
    exit 0
}

if ($sourceStatus.state -ne "baseline" -and -not ($Action -eq "Launch" -and (Test-Path -LiteralPath $shadowAsar -PathType Leaf))) {
    throw "Store source runtime is not a clean baseline ($($sourceStatus.state)); refusing to copy."
}

$metadata = $null
if (Test-Path -LiteralPath $metadataPath -PathType Leaf) {
    try { $metadata = Get-Content -Raw -Encoding UTF8 -LiteralPath $metadataPath | ConvertFrom-Json } catch { $metadata = $null }
}
$ready = Test-Path -LiteralPath $shadowAsar -PathType Leaf
$launchReady = $Action -eq "Launch" -and $ready -and $metadata -and $metadata.state -eq "gif-patched"
$sameSource = $launchReady -or ($metadata -and $metadata.package -eq $package.FullName -and $metadata.sourceAsarSha256 -eq $sourceStatus.asarSha256)
if ($ForceRefresh -and (Test-Path -LiteralPath $ShadowRoot -PathType Container)) {
    if ((Get-RunningAtPath $shadowExe).Count -gt 0) {
        throw "The shadow Codex process is running; stop it before refreshing the runtime."
    }
    $oldRoot = "$ShadowRoot.backup-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))"
    Move-Item -LiteralPath $ShadowRoot -Destination $oldRoot
    $ready = $false
    $sameSource = $false
}
if (-not ($ready -and $sameSource)) {
    if ($ready -and -not $ForceRefresh) {
        throw "A shadow runtime exists but its source metadata does not match. Use -ForceRefresh to create a versioned backup and refresh it."
    }
    Invoke-RobocopyApp -Source $package.AppRoot -Destination $shadowApp
}

if (-not (Test-Path -LiteralPath $shadowAsar -PathType Leaf)) {
    throw "Shadow app.asar was not copied: $shadowAsar"
}
$shadowStatus = Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "verify")
$headerBackup = Join-Path $backupDir "header-baseline.pickle"
if ($shadowStatus.state -eq "gif-patched-legacy" -and -not (Test-Path -LiteralPath $headerBackup -PathType Leaf)) {
    Write-Host "The shadow runtime still carries an older GIF patch; upgrading it in place needs the original header backup."
    $headerSize = [int]$shadowStatus.headerSize
    if (Copy-AsarHeaderBackup -AsarPath $package.AsarPath -Destination $headerBackup -ExpectedLength $headerSize) {
        Write-Host "Rebuilt the header backup from the Store copy: $headerBackup"
    } else {
        Write-Host "This shadow runtime was built from a different Codex build; rebuilding it from the Store copy."
        $shadowStatus = Reset-ShadowRuntimeCopy
    }
}
if ($shadowStatus.state -eq "baseline" -or $shadowStatus.state -eq "gif-patched-legacy") {
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "plan") | Out-Null
    Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "apply", "--backup-dir", $backupDir) | Out-Null
    $shadowStatus = Invoke-PatcherJson -Node $node -Patcher $patcherPath -Asar $shadowAsar -Arguments @("--action", "verify")
}
if ($shadowStatus.state -ne "gif-patched") {
    throw "Shadow runtime verification failed: $($shadowStatus.state)"
}
New-Item -ItemType Directory -Force -Path $ShadowRoot | Out-Null
Write-ShadowLauncher -Path $launcherPath -Executable $shadowExe -Profile $profile
$metadata = [ordered]@{
    package = $package.FullName
    version = $package.Version
    sourceAsar = $package.AsarPath
    sourceAsarSha256 = $sourceStatus.asarSha256
    shadowRoot = $ShadowRoot
    shadowAsar = $shadowAsar
    profile = $profile
    launcher = $launcherPath
    installedAt = [DateTime]::UtcNow.ToString("o")
    state = $shadowStatus.state
    runtimeProfile = $shadowStatus.runtimeProfile
}
Write-JsonAtomic -Path $metadataPath -Value $metadata

$base.state = $shadowStatus.state
$base.ok = $true
$base.runtime = $shadowStatus
$base.metadata = $metadata
if ($Action -eq "Launch") {
    $storeExe = Join-Path $package.AppRoot "ChatGPT.exe"
    if ((Get-RunningAtPath $storeExe).Count -gt 0) {
        throw "The Store Codex process is still running. Close it fully, then launch the shadow runtime."
    }
    if ((Get-RunningAtPath $shadowExe).Count -gt 0) {
        throw "The shadow Codex process is already running."
    }
    $oldUserData = $env:CODEX_ELECTRON_USER_DATA_PATH
    $env:CODEX_ELECTRON_USER_DATA_PATH = $profile
    try {
        $arg = '--user-data-dir="' + $profile.Replace('"', '\"') + '"'
        $process = Start-Process -FilePath $shadowExe -WorkingDirectory $shadowApp -ArgumentList $arg -PassThru
    } finally {
        $env:CODEX_ELECTRON_USER_DATA_PATH = $oldUserData
    }
    $base.launchedPid = $process.Id
}
$base | ConvertTo-Json -Depth 12
