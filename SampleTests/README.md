# TAEF sample tests

`SampleTests.sln` contains the native TAEF test DLLs the Test Adapter for TAEF is developed and tested against. They
exercise everything the adapter supports: passing/failing/skipped/blocked/not-run tests, the different `VERIFY_*`
macros and error locations, exceptions and crashes, fixtures (also failing ones), module/class/method/row
properties (traits, `Ignore`), lightweight and table based data-driven tests, namespaces, class templates,
non-ASCII names, runtime parameters (`TE.exe /p:`), environment variables, working directory, long running tests,
5000 load tests, a runtime DLL dependency, a settings helper file and a mixed-mode (C++/CLI) test DLL.

Roughly half of the tests fail on purpose.

All test classes of all sample test DLLs are in the root namespace `TaefSamples` (nested and anonymous namespaces
and class templates below it; helper types used as template arguments are in it as well), so every TAEF test name
starts with `TaefSamples::` and every fully qualified name in Visual Studio with `TaefSamples.`: Test Explorer groups
all sample tests below one namespace node. Only the module level macros (`BEGIN_MODULE`, `MODULE_SETUP`,
`MODULE_CLEANUP`) are at global scope. Class names in the tables below are relative to `TaefSamples`.

## Building

Requirements: Visual Studio 2026 or 2022 with the C++ desktop workload (and C++/CLI support for `ClrTests`), the
.NET Framework 4.8 targeting pack and the TAEF development files of the Windows Kits (installed with the WDK:
`C:\Program Files (x86)\Windows Kits\10\Testing\Development\` and `...\Testing\Runtimes\TAEF\<arch>\TE.exe`).

* Build `SampleTests.sln` for `Debug|Win32`, `Release|Win32`, `Debug|x64` and `Release|x64`. `Any CPU` builds the
  Win32 variant. The project `SampleTestsBuilder` of `TaefTestAdapter\TaefTestAdapter.sln` builds all four
  configurations before the adapter's test projects are built (set `SkipSampleTestsBuild=true` to skip that).
* Output (`$(OutRoot)` defaults to `<repo>\out\`): `$(OutRoot)binaries\SampleTests\{Debug,Release,Debug-x64,Release-x64}\`,
  intermediate files per configuration and platform in `$(OutRoot)intermediate\SampleTests\`. The C# library of the
  CLR sample is built to `...\{Debug,Release}-AnyCPU\` and copied next to `ClrTests_taef.dll`. When the sample
  projects are built as part of another solution, the solution name replaces `SampleTests` in these paths.
* All test DLLs import `Taef.props`: TAEF include/lib folders (`TaefDevelopmentDir`, default
  `$(WindowsSdkDir)Testing\Development\`, fallback `C:\Program Files (x86)\Windows Kits\10\Testing\Development\`;
  override it with `/p:TaefDevelopmentDir=...`), `INLINE_TEST_METHOD_MARKUP`, `/utf-8`, `TE.Common.lib`,
  `Wex.Common.lib`, `Wex.Logger.lib`, target name `<ProjectName>_taef.dll`, full PDBs in Debug and Release, no
  incremental linking and no identical COMDAT folding (so that every test method keeps its own symbol and source
  location in Release builds).
* `Tests` is compiled as C++17: TAEF then uses inline variables and the DLL exports no TAEF symbols. All other test
  DLLs use the compiler default (C++14) and export TAEF's `*_TAEF_PinTestMethodInfo` etc. functions.

## Sample projects

| Project | Output | Tests | Results (as run by TE.exe with the settings below) | Purpose |
|---|---|---|---|---|
| Tests | `Tests_taef.dll` | 138 (+3 `Ignore=true`) | 69 Passed, 62 Failed, 5 Blocked, 1 Skipped, 1 NotRun; ignored tests: 2 Passed, 1 Failed | main sample, see below |
| CrashingTests | `CrashingTests_taef.dll` | 9 | 4 Passed, 2 Failed, 3 crashes (reported as Failed) | crashes of the test host process |
| LoadTests | `LoadTests_taef.dll` | 5000 | 2500 Passed, 2500 Failed | performance of discovery, symbol lookup and test selection |
| LongRunningTests | `LongRunningTests_taef.dll` | 2 | 1 Passed, 1 Failed (2 s each) | parallel execution, cancellation, durations |
| DllTests (folder DllDependentProject) | `DllTests_taef.dll` | 2 | 1 Passed, 1 Failed | test DLL depending on `DllProject.dll` |
| LeakCheckTests | `LeakCheckTests_taef.dll` | 4 | Debug: 1 Passed, 3 Failed; Release: 2 Passed, 2 Failed | CRT debug heap leak detection, errors in cleanup fixtures |
| HelperFileTests | `HelperFileTests_taef.dll` | 1 | Passed with `/p:"TheTarget=$(TheTarget)"`, Failed without | settings helper file |
| ClrTests | `ClrTests_taef.dll` | 2 | 1 Passed, 1 Failed | mixed-mode (native TAEF + C++/CLI) test DLL |
| LibProject | `LibProject.lib` | - | - | static library (`Add()`) used by Tests and CrashingTests |
| DllProject | `DllProject.dll` | - | - | plain native DLL (no TAEF DLL: discovery must skip it) |
| ClrLibProject, ClrDotNetLibProject | `ClrLibProject.lib`, `ClrDotNetLibProject.dll` | - | - | C++/CLI static library and the C# library it calls |

Other files: `Tests\Returns0.bat`/`Tests\Returns1.bat` (setup/teardown batch files returning 0/1),
`SampleTests.taef.runsettings` (solution settings, see below), `No.runsettings` (empty user settings),
`NonDeterministic.runsettings` (user settings with repetitions, parallel execution and isolation level).

### Settings needed by the samples

`SampleTests.taef.runsettings` is found automatically by the adapter (`<SolutionName>.taef.runsettings`). The tests
which depend on it fail without it (so both cases can be tested):

| Test | Setting | TE.exe equivalent |
|---|---|---|
| `TaefSamples::RuntimeParameterTests::TestDirectoryIsSet` | `AdditionalTestExecutionParam` `/p:"TestDirectory=$(TestDir)"` | `/p:"TestDirectory=<existing folder>"` |
| `TaefSamples::WorkingDir::IsSolutionDirectory` | `WorkingDir` `$(SolutionDir)` | working directory `SampleTests` |
| `TaefSamples::EnvironmentVariable::IsSet` | `EnvironmentVariables` `MYENVVAR=MyValue` (project settings of `Tests_taef.dll`) | environment variable `MYENVVAR=MyValue` |
| `TaefSamples::HelperFileTests::TheTargetIsSet` | `AdditionalTestExecutionParam` `/p:"TheTarget=$(TheTarget)"` (project settings of `HelperFileTests_taef.dll`) | `/p:"TheTarget=HelperFileTests_taef.dll"` |

The runsettings also configure `BatchForTestSetup`/`BatchForTestTeardown` (`Returns0.bat`/`Returns1.bat`), a
`TestDiscoveryRegex` for `LoadTests_taef.dll`/`CrashingTests_taef.dll` and a longer discovery timeout for `Tests_taef.dll`.

### Tests (`Tests_taef.dll`)

Module properties `Component=SampleTests` and `Owner=ModuleOwner` (TestModule.cpp) are inherited by every test,
module setup/cleanup write output outside of the tests.

| File | Classes | What is covered |
|---|---|---|
| BasicTests.cpp | `TestMath`, `OutputHandling`, `abcd`/`bbcd`/`bcd`, `Exceptions`, `ExplicitResults`, `LongRunning` | `VERIFY_*` passing/failing, LibProject dependency; output via `Log::Comment`/`Log::Warning` (multi-line, trailing newlines, context), `printf`/`std::cout`/`std::cerr` (visible only with `/inproc`); class names which are suffixes of each other; `std::exception`, `throw 42`, `WEX::Common::Exception`, `VERIFY_THROWS`, `VERIFY_NO_THROW`; `Log::Result` Skipped/Blocked/NotRun/Failed, `Log::Error`, warning only; tests running 1 and 2 seconds |
| EnvironmentTests.cpp | `RuntimeParameterTests`, `WorkingDir`, `EnvironmentVariable` | runtime parameters (`TestDirectory`, built-in `FullTestName`/`TestDeploymentDir`), working directory, environment variable (see above) |
| FixtureTests.cpp | `ClassWithFixtures`, `FailingClassSetup`, `FailingMethodSetup`, `VerifyInClassSetup`, `VerifyInMethodSetup`, `FailingMethodCleanup` | class and method setup/cleanup; setup returning false (Blocked), `VERIFY` failing in class/method setup (Failed, errors printed outside the test), cleanup returning false (test stays Passed); traits `Type`, `Author`, `TestCategory` |
| DataDrivenTests.cpp (+ DataDrivenTests.xml, embedded via Tests.rc) | `TableDataTests`, `LightweightDataTests`, `StringTableDataTests`, `NamedRows`, `MissingDataSource` | table data sources (`#0`, `#1`, named rows `#Underscore`), lightweight data (`#metadataSet<N>`, cross product), row names with space, `'`, `#`, `::`, `[x]`, `\` and non-ASCII characters, row level metadata (`Priority`, `Owner`), a missing data source (`TaefSamples::MissingDataSource::Test#error`, Blocked) |
| ClassTemplateTests.cpp | `TemplateTests<...>`, `ClassTemplates::NumberTemplateTests<...>` | class templates with explicit instantiations, e.g. `TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate` (`::` inside template arguments) and `TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>` (fully qualified template arguments) |
| NamespaceTests.cpp | `Namespace_1::...`, `` `anonymous-namespace'::... ``, `Namespace_Root` | named, nested (C++17 `A::B` syntax) and anonymous namespaces below `TaefSamples` (e.g. `` TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test ``), a class directly in `TaefSamples` |
| TraitsTests.cpp | `Traits`, `ClassAndMethodProperties`, `IgnoredClass` | 1 to 8 properties, duplicate keys (`Author` = `Alice Bob`), class properties overridden by method properties, `Priority`, custom properties, a value with spaces, `Description` (not a trait), `Ignore=true` on method and class level |
| UmlautTests.cpp | `Ümlautß`, `Nämespace::KlässWithSetüp`, `DataDrivenTästs`, `ÜmlautTemplateTests<...>` | non-ASCII class, namespace, method, property and data names/values, Unicode output |
| MessageParserTests.cpp (+ Helpers.h/.cpp) | `MessageParserTests` | failures in the test, in a helper function, in another .cpp file and in a header; several failures per test (`DisableVerifyExceptions`); `LOG_SCOPE` helper (RAII `Log::Comment`); `Log::Error` with and without source information |

### CrashingTests (`CrashingTests_taef.dll`)

Class `Crashing`: `AddFailsBeforeCrash`, `AddPassesBeforeCrash`, `TheCrash` (access violation, exit code
0xC0000005), `AddFailsAfterCrash`, `AddPassesAfterCrash`, `LongRunning` (2 s), `TheAbort` (`abort()`, exit code 3),
`TheStackOverflow` (0xC00000FD), `AddPassesAfterAllCrashes`. By default TE.exe runs the tests in `TE.ProcessHost.exe`:
a crash fails only the crashing test (`Error: TAEF: [HRESULT 0x800706BE] ... terminated with exit code 0x...`), TE.exe
starts a new host (class setup runs again, `TestSkipped: TAEF: The cleanup method ... will not be run ...` is printed)
and continues. With `/inproc` (adapter option *Run tests in process* and when debugging) TE.exe itself dies in
`TheCrash`: no `EndGroup`, no summary, the six tests after it are never run.

### LoadTests (`LoadTests_taef.dll`)

5000 test methods `TaefSamples::LoadTests::Test0` ... `TaefSamples::LoadTests::Test4999` (odd numbers pass), generated by preprocessor macros in
LoadTests.cpp (no code generator needed). Real methods instead of one data-driven test make discovery, PDB symbol
lookup and `/select` command lines as large as possible.

### DllTests (`DllTests_taef.dll`)

`TaefSamples::Passing::InvokeFunction` and `TaefSamples::Failing::InvokeFunction` call `ReturnZero()` of `DllProject.dll`. TE.exe finds the DLL in
the test DLL's folder or on the PATH (adapter setting `PathExtension`), not in the working directory; if it is missing,
TE.exe reports the tests as Blocked.

### LeakCheckTests (`LeakCheckTests_taef.dll`)

`TaefSamples::MemoryLeaks::Passing`, `Failing`, `PassingAndLeaking`, `FailingAndLeaking`. `LeakDetector.h` takes a CRT debug heap
checkpoint in the method setup; `VerifyNoMemoryLeaks()` at the end of a test fails the test if memory has leaked.
The method cleanup checks again and logs leaks of tests which failed before their own check - TAEF prints these
errors after the test (`Error: TAEF: Cleanup fixture 'TaefSamples::MemoryLeaks::CheckForLeaks' for the scope '...' failed.`)
without changing the test's result. Leaks are only detected in Debug builds, so `PassingAndLeaking` passes in Release.

### HelperFileTests (`HelperFileTests_taef.dll`)

The post-build step writes `HelperFileTests_taef.dll.taef_settings_helper` next to the DLL (one line,
`Key=Value` pairs separated by `::TAEF::`): `SolutionPath`, `SolutionDir`, `PlatformName`, `ConfigurationName`,
`TheTarget` (`HelperFileTests_taef.dll`) and `TheWorkingDirectory` (the project folder). The adapter provides them as
placeholders; `TaefSamples::HelperFileTests::TheTargetIsSet` checks the runtime parameter `TheTarget`.

### ClrTests (`ClrTests_taef.dll`)

A native TAEF test DLL linking the C++/CLI static library `ClrLibProject` (which calls the C# `ClrDotNetLibProject.dll`),
i.e. a mixed-mode DLL. TE.exe lists and runs it like any native TAEF DLL (`TaefTestType` is `Native`). Because the CLR
resolves assemblies relative to `TE.ProcessHost.exe`, the module setup registers an `AssemblyResolve` handler for the
test DLL's folder (`TestDeploymentDir`).

## How the adapter's tests use the samples

The adapter's test projects (`TaefTestAdapter\*.Tests`, test resources in `Tests.Common`) run discovery and execution
against the built sample DLLs:

| Sample | Used for |
|---|---|
| `Tests_taef.dll` (all four configurations) | discovery: number of tests, TAEF names and their VS form, traits (module/class/method/row properties, duplicate keys, `Ignore`), source locations from the PDB (also for templates, anonymous namespaces, non-ASCII names and helper files), data rows and the `#error` pseudo test; execution: outcome mapping (Passed/Failed/Skipped, Blocked -> Failed, NotRun -> None), error messages and clickable stack traces (file/line of `VERIFY`s in helpers and headers), test output, selection of subsets (names with `'`, `#`, `::`, spaces, non-ASCII), runtime parameters, working directory, environment variables, batch files, placeholders, run in process, repetitions and isolation levels; end-to-end tests with vstest.console |
| `CrashingTests_taef.dll` | results after a crash out of process (TE.exe continues) and with `/inproc` (TE.exe dies, remaining tests are not run); `TestDiscoveryRegex` in the solution settings |
| `LoadTests_taef.dll` (usually Release x86) | discovery performance, PDB/DIA and PE parsing tests, splitting of long `/select` command lines, `PathExtension` |
| `LongRunningTests_taef.dll` | parallel execution speed-up, cancellation (killing TE.exe together with all processes started by it), test durations, `TestTimeout` |
| `DllTests_taef.dll` + `DllProject.dll` | test DLLs with runtime dependencies (`PathExtension`, missing DLL -> Blocked), PE import parsing, skipping non-TAEF DLLs |
| `LeakCheckTests_taef.dll` | failures of cleanup fixtures, which TAEF reports after the test |
| `HelperFileTests_taef.dll` | settings helper files (`.taef_settings_helper`, `::TAEF::`) and their placeholders |
| `ClrTests_taef.dll` | mixed-mode test DLLs (manual scenario) |

## Running the samples with TE.exe

Use the TE.exe matching the DLL's architecture (`Debug`/`Release` are x86, `*-x64` are x64):

```
set TE=C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF\x64\TE.exe
"%TE%" out\binaries\SampleTests\Debug-x64\Tests_taef.dll /listProperties /runIgnoredTests /unicodeOutput:false
cd SampleTests
set MYENVVAR=MyValue
"%TE%" ..\out\binaries\SampleTests\Debug-x64\Tests_taef.dll /unicodeOutput:false /p:"TestDirectory=%TEMP%"
"%TE%" ..\out\binaries\SampleTests\Debug-x64\HelperFileTests_taef.dll /unicodeOutput:false /p:"TheTarget=HelperFileTests_taef.dll"
```

## TAEF behaviour worth knowing when working with the samples

* Test names are TAEF's names: `Namespace::Class::Method`, data rows `...::Method#<index|row name|metadataSet<N>>`,
  class templates with the compiler's spelling of the template arguments (`<class std::array<int,3> >`,
  `<struct TaefSamples::ReversedArray>`), anonymous namespaces as `` `anonymous-namespace' ``. The undecorated names in
  the PDB differ for templates and anonymous namespaces (`TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >`,
  `` `anonymous namespace' ``, nested anonymous namespaces even as `` TaefSamples::A0x<hash>::`anonymous namespace' ``).
* Tests with `Ignore=true` are neither listed nor run without `/runIgnoredTests`.
* `TaefSamples::MissingDataSource::Test#error` is run (Blocked) whenever any test of `Tests_taef.dll` is run, even if it is not
  selected by `/select` or `/name`.
* A double quote cannot be selected literally in `/select:"..."` (it would end the argument); none of the sample names
  contains one.
* Out of process, only `WEX::Logging` output and `VERIFY_*` messages reach TE.exe's console.
