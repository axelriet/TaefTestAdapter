@echo off
rem Fake TE.exe for tests (TestResources.FakeTeExecutable, e.g. as value of option TeExecutable): ignores all arguments,
rem prints the file %TAEF_FAKE_TE_OUTPUT% (e.g. a captured TE.exe output from TaefOutput\, printed byte by byte, i.e. as
rem UTF-8) if set, and exits with exit code %TAEF_FAKE_TE_EXITCODE% (default: 0).
if not "%TAEF_FAKE_TE_OUTPUT%"=="" type "%TAEF_FAKE_TE_OUTPUT%"
if "%TAEF_FAKE_TE_EXITCODE%"=="" exit /b 0
exit /b %TAEF_FAKE_TE_EXITCODE%
