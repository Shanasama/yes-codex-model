@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
for %%I in ("%~dp0..") do set "PROJECT_ROOT=%%~fI"
set "SCRIPT=%PROJECT_ROOT%\启动 Grilling 皮肤工坊.cmd"
if not exist "%SCRIPT%" (
  echo Grilling 原生工坊启动脚本不存在：%SCRIPT%
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
call "%SCRIPT%"
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" pause
exit /b %EXIT_CODE%
