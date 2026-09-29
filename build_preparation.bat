@echo off
rem Prepares a local build of the Test Adapter for TAEF:
rem  - locates Visual Studio 2026/2022 (vswhere),
rem  - copies msdia140.dll (x86/x64/arm64) from the VS DIA SDK into TaefTestAdapter\DiaResolver and
rem    generates TaefTestAdapter\DiaResolver\dia2\dia2.dll,
rem  - restores the NuGet packages.
rem This is a thin wrapper around "build.ps1 -PrepareOnly"; additional arguments are passed on
rem (e.g. "build_preparation.bat -ForceDia"). Run "build.ps1" to build everything.
rem Note: msdia140.dll is part of the Microsoft DIA SDK and subject to the Visual Studio license terms.

setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -PrepareOnly %*
exit /b %ERRORLEVEL%
