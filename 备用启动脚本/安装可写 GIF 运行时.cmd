@echo off
setlocal DisableDelayedExpansion
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "SCRIPT=%PROJECT_ROOT%\plugins\codex-skin-engine\scripts\install_shadow_runtime.ps1"
if not exist "%SCRIPT%" (
  echo Shadow runtime script not found.
  pause
  exit /b 1
)
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%POWERSHELL%" (
  echo Windows PowerShell not found: %POWERSHELL%
  pause
  exit /b 1
)
set "REFRESH_ARG="
if /I "%CODEX_SKIN_FORCE_REFRESH%"=="1" set "REFRESH_ARG=-ForceRefresh"
set "ACTION=Install"
if /I "%CODEX_SKIN_PLAN%"=="1" set "ACTION=Plan"
"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" -ProjectRoot "%PROJECT_ROOT%" -Action %ACTION% %REFRESH_ARG%
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if /I "%CODEX_SKIN_PLAN%"=="1" (
  echo Shadow runtime plan completed without changes.
) else if "%EXIT_CODE%"=="0" (
  echo Writable GIF shadow runtime is ready.
  echo Close the Store Codex completely, then run the launch script.
) else (
  echo Shadow runtime install failed. Exit code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
