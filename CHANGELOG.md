# Changelog

All notable changes to Test Adapter for TAEF are documented in this file.

## [1.0.1.0] - 2026-09-29

Bug fixes:

* source locations of the tests were missing when the adapter ran in a native ARM64 process (for example, the ARM64 test host or `vstest.console.exe` on ARM64 Windows): the extension and the NuGet package now contain the arm64 `msdia140.dll`, which reads the PDBs in such processes
* debugging tests from Test Explorer attached the Visual Studio debugger to the test host process of the test platform as well; now only `TE.exe`, which runs the tests, is debugged
* test DLLs given with a root-relative or drive-relative path (for example, `\dir\tests.dll` or `D:tests.dll` on the command line of `vstest.console.exe`) could be ignored with the error that they came from another computer; downloaded test DLLs (mark of the web) are still blocked
* the TAEF Test Project template had no ARM64 configurations; it now has `Debug|ARM64` and `Release|ARM64` like `Debug|x64` and `Release|x64` (building them requires the MSVC ARM64 build tools)
* creating a project or a file from the TAEF templates failed with the error `this template attempted to load component assembly 'TaefTestAdapter.VsPackage, Version=1.0.0.0, Culture=neutral, PublicKeyToken=6dd9e1a7dfb9193e'`: the manifest of the extension did not declare the name of the templates' wizard assembly, by which Visual Studio finds it in the extension

Other changes:

* the NuGet package links to the project and to its source repository (with the commit it was built from) and contains the README, which nuget.org shows on the package page; the extension links to the project on GitHub

## [1.0.0.0] - 2026-09-27

First release of Test Adapter for TAEF, which runs native C++ tests written with the
[Test Authoring and Execution Framework (TAEF)](https://learn.microsoft.com/windows-hardware/drivers/taef/) from the
Visual Studio Test Explorer.

* supports Visual Studio 2026 and 2022 (and `vstest.console.exe` of these versions)
* discovery: TAEF test DLLs are recognized without running them (by their TAEF metadata and their imported or delay-loaded TAEF DLLs, by an indicator file `<test dll>.is_taef_test`, or by the option `TestDiscoveryRegex`); their tests are listed with `TE.exe /listProperties`
* source locations of the tests are read from the test DLL's PDB
* TAEF metadata (module, class and test properties) are shown as traits; additional traits can be assigned by regular expressions
* data-driven tests (lightweight `Data:` sets and table data sources) are shown as one test per data row; a failing data source is reported as a failed pseudo test `<test>#error`
* tests which TAEF ignores (property `Ignore` with value `true` or `1`) are reported as skipped unless the option `RunIgnoredTests` is enabled
* results: TAEF's `Passed`, `Failed` and `Skipped` are reported as such, `Blocked` is reported as failed (with a message starting with `Blocked: `), `NotRun` as not run; crashed tests are reported as failed, as are the remaining tests if `TE.exe` terminates outside of a test while running in process
* the output of each test (WEX logging, e.g. `Log::Comment`, and `VERIFY_*` messages) is shown with its result; error locations are clickable
* sequential and parallel test execution; test repetitions (`NrOfTestRepetitions`), test timeouts (`TestTimeout`) and isolation level (`IsolationLevel`); running tests inside `TE.exe` (`RunInProcess`, `/inproc`), where timeouts and isolation levels have no effect and repetitions repeat each test within the same process
* debugging of tests from the Test Explorer (always in process) and break on error (`BreakOnError`, `TE.exe /breakOnError`); with the NuGet package, tests can only be debugged with the debugger engine `VsTestFramework` (option `DebuggerKind`), which reads the results from a WTT log of `TE.exe` (no `printf`/`std::cout` output, no durations, and no results if `TE.exe` crashes)
* `TE.exe` of the Windows Kits (installed with the WDK) is found automatically for the architecture of each test DLL (x86, x64, arm64; ARM64EC test DLLs use the x64 `TE.exe`); option `TeExecutable` allows to use another TAEF installation
* additional `TE.exe` arguments (e.g. runtime parameters `/p:"Name=Value"`; a `/select` given there restricts discovery and is combined with the adapter's selection), working directory, `PATH` extension, environment variables and setup/teardown batch files can be configured, with placeholders such as `$(TestDll)`, `$(TestDllDir)` and `$(SolutionDir)`
* `TE.exe` runs in a job object together with all processes started by the tests: with `KillProcessesOnCancel`, canceling kills all of them, and they are killed if the test host terminates while `TE.exe` is running; processes left running by tests after `TE.exe` has exited are not waited for
* test DLLs in folders with non-ASCII names are passed to `TE.exe` by their file name or 8.3 short path
* configuration via Visual Studio options, toolbar, a solution settings file `<solution name>.taef.runsettings` and `.runsettings` files (`<TaefTestAdapterSettings>`; an invalid node is reported as error and ignored)
* project and item templates for TAEF tests; their wizard derives TitleCase C++ identifiers without underscores from the project and file names for the namespace and the test class (e.g. the project "Contoso.Unit Tests" gets the namespace `ContosoUnitTests` and the item "My New-Tests.cpp" the class `MyNewTests`), derives the namespace of an item in the same way from the project's `RootNamespace` (e.g. `Contoso.Unit-Tests`, which is not a C++ identifier, and `Contoso_Unit_Tests` both become `ContosoUnitTests`), and appends `Tests` to names that would clash with the templates' code or the headers they include (e.g. the class `AdditionTests` for "Addition.cpp")
