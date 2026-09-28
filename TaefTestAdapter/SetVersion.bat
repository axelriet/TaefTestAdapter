@echo off
setlocal

if "%~1"=="" goto USAGE

echo Setting versions to %~1
PowerShell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SetVersion.ps1" -version "%~1"
exit /b %ERRORLEVEL%


:USAGE
echo Usage  : SetVersion ^<version^>, where ^<version^> should be ^<major^>.^<minor^>.^<revision^>.^<build^>
echo Example: SetVersion 1.0.0.42
exit /b 1
