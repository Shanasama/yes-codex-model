@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "SCRIPT=%PROJECT_ROOT%\install_one_click.ps1"
if not exist "%SCRIPT%" (
  echo 安装脚本不存在：%SCRIPT%
  pause
  exit /b 1
)
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%POWERSHELL%" (
  echo 找不到 Windows PowerShell：%POWERSHELL%
  pause
  exit /b 1
)
set "PLAN_ARG="
if /I "%CODEX_SKIN_PLAN%"=="1" set "PLAN_ARG=-Plan"
"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -ProjectRoot "%PROJECT_ROOT%" -PluginOnly -NoLaunch %PLAN_ARG%
set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="2" (
  echo 插件和酒狐皮肤已经装好，只有皮肤工坊没编译成功。
  echo 按上面的提示修好 .NET 7 SDK 后，双击根目录的 启动皮肤工坊.cmd 重试。
  pause
  exit /b 2
)
if not "%EXIT_CODE%"=="0" (
  echo.
  echo 插件安装失败，退出码：%EXIT_CODE%
  pause
  exit /b %EXIT_CODE%
)
if /I "%CODEX_SKIN_PLAN%"=="1" (
  pause
  exit /b 0
)
call "%PROJECT_ROOT%\启动皮肤工坊.cmd"
set "STUDIO_EXIT_CODE=%ERRORLEVEL%"
pause
exit /b %STUDIO_EXIT_CODE%
