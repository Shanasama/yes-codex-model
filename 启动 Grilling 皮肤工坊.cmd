@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "EXE=%PROJECT_ROOT%\dist\grilling\GrillingSkinStudio.exe"
if not exist "%EXE%" (
  echo 首次使用需要先构建 Grilling 原生工坊。
  echo 正在调用 build_grilling.ps1 ...
  powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_ROOT%\build_grilling.ps1"
  if errorlevel 1 (
    echo 构建失败，请确认已安装 .NET 7 SDK。
    pause
    exit /b 1
  )
)
start "Grilling" "%EXE%"
exit /b 0
