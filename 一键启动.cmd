@echo off
setlocal DisableDelayedExpansion
set "PROJECT_ROOT=%~dp0"
if "%PROJECT_ROOT:~-1%"=="\" set "PROJECT_ROOT=%PROJECT_ROOT:~0,-1%"
set "SCRIPT=%PROJECT_ROOT%\install_one_click.ps1"
if not exist "%SCRIPT%" (
  echo One-click script not found.
  pause
  exit /b 1
)
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%POWERSHELL%" (
  echo Windows PowerShell not found: %POWERSHELL%
  pause
  exit /b 1
)
set "PLAN_ARG="
if /I "%CODEX_SKIN_PLAN%"=="1" set "PLAN_ARG=-Plan"
set "NO_LAUNCH_ARG="
if /I "%CODEX_SKIN_NO_LAUNCH%"=="1" set "NO_LAUNCH_ARG=-NoLaunch"
"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -ProjectRoot "%PROJECT_ROOT%" %PLAN_ARG% %NO_LAUNCH_ARG%
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if /I "%CODEX_SKIN_PLAN%"=="1" (
  echo One-click GIF plan completed without changes.
) else if "%EXIT_CODE%"=="0" (
  echo One-click GIF installation completed.
) else if "%EXIT_CODE%"=="2" (
  echo Plugin, skin and GIF runtime are ready, but the skin studio was not built.
  echo Fix the error above, then double-click 启动皮肤工坊.cmd to retry.
) else (
  echo One-click GIF installation failed. Exit code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
