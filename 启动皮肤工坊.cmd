@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
for %%I in ("%~dp0.") do set "PROJECT_ROOT=%%~fI"
set "EXE=%PROJECT_ROOT%\dist\studio\SkinStudio.exe"
set "BUILD=%PROJECT_ROOT%\build_studio.ps1"
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

if not exist "%POWERSHELL%" (
  echo 找不到 Windows PowerShell：%POWERSHELL%
  pause
  exit /b 1
)

if not exist "%EXE%" (
  echo 首次运行，正在编译皮肤工坊（要等几分钟，请勿关闭这个窗口）...
  if not exist "%BUILD%" (
    echo 找不到构建脚本：%BUILD%
    pause
    exit /b 1
  )
  "%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%BUILD%"
  if not exist "%EXE%" (
    echo.
    echo 编译失败，请查看上面的输出。
    pause
    exit /b 1
  )
)

start "" "%EXE%"
exit /b 0
