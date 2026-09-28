@echo off
rem Generates TAEF_Console.csv (the test cases of the generated end-to-end tests, see ConsoleTests.tt) from the PICT model
rem TAEF_Console.pictmodel with PICT (https://github.com/microsoft/pict). The existing CSV file is used as seed, i.e. its
rem test cases are kept as far as possible. pict.exe is taken from environment variable PICT_EXE, else from the PATH.
rem After generating the CSV file, regenerate ConsoleTests.cs (see ConsoleTests.tt) and the golden files of new tests.

set BASE_DIR=%~dp0

if "%PICT_EXE%"=="" set PICT_EXE=pict.exe
set MODEL="%BASE_DIR%TAEF_Console.pictmodel"
set CSV="%BASE_DIR%TAEF_Console.csv"
set TEMP_SEED="%BASE_DIR%seed.pictmodel"

where "%PICT_EXE%" >nul 2>&1 || if not exist "%PICT_EXE%" (
  echo PICT has not been found: put pict.exe on the PATH or set PICT_EXE
  exit /b 1
)

if exist %CSV% (
  echo Generating test data using existing data as seed =====
  move %CSV% %TEMP_SEED%
  "%PICT_EXE%" %MODEL% -a:# -e:%TEMP_SEED% >%CSV%
  del %TEMP_SEED%
) else (
  echo Generating test data from scratch =====
  "%PICT_EXE%" %MODEL% -a:# >%CSV%
)
echo ===== Generation done, target file is
echo %CSV%
