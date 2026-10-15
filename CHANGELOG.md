# Changelog

This file records notable changes to Test Adapter for TAEF.

## [1.0.2.0] - 2026-10-15

* added managed C# TAEF test discovery, execution and debugging for .NET Framework and modern .NET
* added managed source navigation using matching Windows, portable and embedded portable PDBs, including async methods
* added automatic AnyCPU architecture and modern .NET runtime selection, preserving explicit TAEF runtime settings
* added C# project and item templates and managed test fixtures

## [1.0.1.0] - 2026-09-29

Bug fixes:

* source locations were missing when the adapter ran in a native ARM64 process, such as the ARM64 test host or `vstest.console.exe`. The extension and NuGet package now include the arm64 `msdia140.dll` to read PDBs in these processes
* debugging from Test Explorer also attached the Visual Studio debugger to the test platform's host process. It now debugs only `TE.exe`, which runs the tests
* the adapter could reject test DLLs with root-relative or drive-relative paths as files from another computer. Examples include `\dir\tests.dll` and `D:tests.dll` on the `vstest.console.exe` command line. Downloaded test DLLs (mark of the web) remain blocked
* the TAEF Test Project template now includes `Debug|ARM64` and `Release|ARM64`, matching its `Debug|x64` and `Release|x64` configurations. ARM64 builds require the MSVC ARM64 build tools
* creating a project or file from the TAEF templates failed with the error `this template attempted to load component assembly 'TaefTestAdapter.VsPackage, Version=1.0.0.0, Culture=neutral, PublicKeyToken=6dd9e1a7dfb9193e'`. The extension manifest now declares the templates' wizard assembly name so Visual Studio can find it

Other changes:

* the NuGet package links to the project and source repository, including its build commit. It also contains the README for the nuget.org package page. The extension links to the project on GitHub

## [1.0.0.0] - 2026-09-27

First release of Test Adapter for TAEF, which runs native C++ tests written with the
[Test Authoring and Execution Framework (TAEF)](https://learn.microsoft.com/windows-hardware/drivers/taef/) from the
Visual Studio Test Explorer.

* supports Visual Studio 2026 and 2022 (and `vstest.console.exe` of these versions)
* discovery: the adapter recognizes TAEF test DLLs without running them. It checks TAEF metadata and imported or delay-loaded TAEF DLLs, an indicator file `<test dll>.is_taef_test`, or `TestDiscoveryRegex`. It lists tests with `TE.exe /listProperties`
* source locations of the tests are read from the test DLL's PDB
* TAEF metadata (module, class and test properties) are shown as traits; additional traits can be assigned by regular expressions
* data-driven tests (lightweight `Data:` sets and table data sources) appear as one test per data row. A failing data source becomes a failed pseudo test `<test>#error`
* tests which TAEF ignores (property `Ignore` with value `true` or `1`) are reported as skipped unless the option `RunIgnoredTests` is enabled
* results: TAEF's `Passed`, `Failed` and `Skipped` keep their outcomes. `Blocked` becomes failed, with a message starting with `Blocked: `. `NotRun` becomes not run. Crashed tests become failed. Remaining tests also become failed if `TE.exe` terminates outside a test while running in process
* the output of each test (WEX logging, e.g. `Log::Comment`, and `VERIFY_*` messages) is shown with its result; error locations are clickable
* sequential and parallel execution, repetitions (`NrOfTestRepetitions`), timeouts (`TestTimeout`) and isolation levels (`IsolationLevel`). Tests can run inside `TE.exe` (`RunInProcess`, `/inproc`). In this mode, timeouts and isolation levels have no effect; repetitions repeat each test within the same process
* debugging from Test Explorer always runs in process and supports break on error (`BreakOnError`, `TE.exe /breakOnError`). With the NuGet package, debugging requires `VsTestFramework` (option `DebuggerKind`). This engine reads results from a WTT log of `TE.exe`, without `printf`/`std::cout` output or durations. It reports no results if `TE.exe` crashes
* the adapter finds the Windows Kits `TE.exe` for each DLL's architecture (x86, x64, arm64). ARM64EC DLLs use the x64 `TE.exe`. The WDK installs these runtimes; `TeExecutable` selects another TAEF installation
* additional `TE.exe` arguments (e.g. runtime parameters `/p:"Name=Value"`; a `/select` given there restricts discovery and is combined with the adapter's selection), working directory, `PATH` extension, environment variables and setup/teardown batch files can be configured, with placeholders such as `$(TestDll)`, `$(TestDllDir)` and `$(SolutionDir)`
* `TE.exe` and processes started by tests run in a job object. With `KillProcessesOnCancel`, cancellation kills all of them. They also terminate if the test host exits while `TE.exe` runs. The adapter does not wait for processes that tests leave running after `TE.exe` exits
* test DLLs in folders with non-ASCII names are passed to `TE.exe` by their file name or 8.3 short path
* configuration via Visual Studio options, toolbar, a solution settings file `<solution name>.taef.runsettings` and `.runsettings` files (`<TaefTestAdapterSettings>`; an invalid node is reported as error and ignored)
* project and item templates for TAEF tests. The wizard derives TitleCase C++ identifiers without underscores from project and file names. For example, project "Contoso.Unit Tests" gets namespace `ContosoUnitTests`, and item "My New-Tests.cpp" gets class `MyNewTests`. It derives an item's namespace from the project's `RootNamespace` by the same rules: `Contoso.Unit-Tests` and `Contoso_Unit_Tests` both become `ContosoUnitTests`. It appends `Tests` to names that conflict with template code or included headers, such as `AdditionTests` for "Addition.cpp"
