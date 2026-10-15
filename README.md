# Test Adapter for TAEF

Test Adapter for TAEF discovers, runs and debugs native C++ and managed C# tests in Visual Studio's Test Explorer.
It uses the [Test Authoring and Execution Framework (TAEF)](https://learn.microsoft.com/windows-hardware/drivers/taef/)
runner, `TE.exe`. It supports Visual Studio 2026, Visual Studio 2022 and `vstest.console.exe`.

Install it from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=arsd.VisualStudioTaefTestAdapter)
or use the [TaefTestAdapter](https://www.nuget.org/packages/TaefTestAdapter) NuGet package; see *Install*.
The source code is on [GitHub](https://github.com/axelriet/TaefTestAdapter).

## Why TAEF

[TAEF](https://learn.microsoft.com/windows-hardware/drivers/taef/) tests Windows components and drivers. Microsoft's open-source
[Windows Terminal](https://github.com/microsoft/terminal/blob/main/doc/TAEF.md) project, for example, describes it as "used
extensively within the Windows organization" to test operating system code in a unified manner. TAEF ships with the Windows
Driver Kit (WDK) and the Windows Hardware Lab Kit; the WDK also provides
[TAEF runtime installers](https://learn.microsoft.com/windows-hardware/drivers/taef/getting-started#manually-installing-and-uninstalling-taef-on-a-test-computer)
for machines that do not need the full development kit. TAEF provides:

* **Distributable tests**: ship test DLLs with your product. Users can run them with the TAEF runtime to validate
  installations, qualify hardware or reproduce issues. Managed tests also require their target .NET runtime.
* **Native and managed tests**: C++ classes use `TEST_CLASS`/`TEST_METHOD`; C# classes use test attributes.
  Both support module, class and method fixtures, verification and WEX logging. TAEF also runs script tests.
* **Metadata-driven test selection**: properties on modules, classes and methods (such as `Owner`, `Priority` or your own)
  and a query language (`TE.exe /select:"..."`) to pick exactly the tests to run.
* **Data-driven testing**: lightweight `Data:` properties, XML tables, PICT and WMI data sources.
* **Robust execution**: tests run in separate test host processes by default, so a crashing test does not take the test run
  down; timeouts, isolation levels, runtime parameters, repetitions and parallel execution.
* **Built for automation labs**: x86, x64 and ARM64, running tests elevated, as System or in an AppContainer, WTT logging,
  minidumps and stack traces on errors (`/miniDumpOnError`, `/stackTraceOnError`).

The adapter uses the same test DLLs in Test Explorer and on the command line.

## Overview

* **Automatic test discovery**: a static check recognizes native TAEF metadata or managed assembly references and test attributes.
  Other DLLs do not start a process. The adapter lists tests with
  `TE.exe <dll> /listProperties`; managed assemblies and data providers can be loaded by TAEF during listing.
  You can force recognition with an indicator file or a regular expression.
* **Source locations** of the tests are read from the test DLL's PDB, so double-clicking a test opens its source code.
  Managed tests support matching Windows, portable and embedded portable PDBs, including async methods.
* **Traits from TAEF metadata**: module, class and test properties (for example, `Owner`, `Priority`, `Category`) are shown
  as traits; you can assign additional traits with regular expressions.
* **Data-driven tests**: every row of a lightweight (`Data:` properties) or table-based data source is a separate test;
  data source errors are reported as a failing test.
* **Ignored tests**: tests which TAEF ignores (property `Ignore` with value `true` or `1`) are shown and reported as skipped,
  or run on request.
* **Complete result information**: TAEF results are mapped to Visual Studio outcomes (including *Blocked* and *NotRun*), with
  error messages, clickable source locations of failed verifications, the test's log output (`Log::Comment`, `VERIFY_*`)
  and test durations.
* **Sequential and parallel test execution**, test repetitions, test timeouts and TAEF isolation levels (the latter two
  for tests run in test host processes), and running tests in process (`/inproc`).
* **Debugging** of tests from Test Explorer (tests are run in process under the debugger), optionally breaking into the
  debugger on the first error a test logs.
* **Configuration** with Visual Studio options, a toolbar, a solution settings file `<SolutionName>.taef.runsettings` that you
  can share via source control, and `.runsettings` files; settings can differ per test DLL.
* **Placeholders** such as `$(TestDllDir)` or `$(SolutionDir)` in settings, plus custom placeholders from settings helper files.
* **Automatic TE.exe selection**: the `TE.exe` of the Windows Kits matching the architecture of each test DLL (x86, x64,
  arm64) is used; AnyCPU managed DLLs use the operating system architecture. You can configure another TAEF.
* **Managed runtimes**: .NET Framework and modern .NET tests are supported. For modern .NET, the adapter selects an
  installed compatible runtime of the target major version for the selected `TE.exe`. Explicit `TAEF_CoreCLR`
  environment settings and TAEF `CoreClrProfile` metadata take precedence.
* Setup and teardown batch files, additional `TE.exe` arguments (for example, runtime parameters `/p:"Name=Value"`),
  working directory, `PATH` extension and environment variables.
* **Project and item templates** for C++ and C# TAEF test DLLs and test classes, whose wizard derives TitleCase namespace and
  class names from the project and file names (see *Write TAEF tests*).
* Support for `vstest.console.exe`, including test case filters.

## Prerequisites

* Visual Studio 2026 (18.x) or Visual Studio 2022 (17.x), 64 bit, or `vstest.console.exe` from either version.
  To author tests, install the C++ workload for native projects or .NET tooling for C# projects.
* For managed execution, install the target .NET runtime for the selected architecture.
  The .NET 10 runtime does not replace the .NET 8 runtime when running .NET 8 tests with automatic selection.
* **TAEF** (`TE.exe`). TAEF is installed with the Windows Driver Kit (WDK) and other Windows Kits that contain the Windows
  testing tools (for example, the Windows Hardware Lab Kit). They install TAEF into the *Testing* folder of the Windows
  Kits, for example `C:\Program Files (x86)\Windows Kits\10\Testing\`:
  * `Development\inc` and `Development\lib\<x86|x64|arm64>` contain the headers and import libraries you need to build TAEF
    tests,
  * `Runtimes\TAEF\<x86|x64|arm64>\TE.exe` is the test runner used by the adapter.
  * Managed runtime assemblies are in `Runtimes\TAEF\NetFx4.5` and `Runtimes\TAEF\netstandard2.0`.
    Keep the complete TAEF runtime installed so its managed bridge and native dependencies remain available.

  Alternatively, you can take TAEF from the `Microsoft.Taef` NuGet package (where available; it is not published on
  nuget.org) or any other location; set the option `TeExecutable` in that case (see *Settings* in *Reference*).

## Install

You can use Test Adapter for TAEF in three ways:

* **Visual Studio extension (VSIX)**: install
  [Test Adapter for TAEF](https://marketplace.visualstudio.com/items?itemName=arsd.VisualStudioTaefTestAdapter) from the Visual Studio
  Marketplace, in Visual Studio with *Extensions > Manage Extensions* (search for "TAEF"). Alternatively, download
  `TaefTestAdapter.vsix` from the [Releases](https://github.com/axelriet/TaefTestAdapter/releases) of this repository (or
  build it, see *Build from source*), double-click it and select Visual Studio 2026 and/or Visual Studio 2022
  in the installer. Restart Visual Studio and build your test DLLs to discover them.
  The extension provides all debugger engines, options, toolbar, solution settings and templates.
* **vstest.console.exe**: pass the folder containing `TaefTestAdapter.TestAdapter.dll` (and its dependencies) with
  `/TestAdapterPath:<folder>`, see *Run tests from the command line*.
* **NuGet package** [TaefTestAdapter](https://www.nuget.org/packages/TaefTestAdapter): add it to test projects through
  *Manage NuGet Packages*. This development dependency supplies discovery and execution without installing VSIX.
  Debugging requires `DebuggerKind=VsTestFramework` in a `.runsettings` file; see *Debug tests*.
  Configure through `.runsettings`; NuGet does not supply options pages, a toolbar or solution settings.
  See *Feature availability*.

If no or not all tests show up, see *Troubleshoot*.

## Write TAEF tests

TAEF supports native C++ and managed tests. See Microsoft's
[Authoring Tests](https://learn.microsoft.com/en-us/windows-hardware/drivers/taef/authoring-tests) for the language-specific
markup, fixtures and metadata.

### C++ tests

Install the extension, then create a **TAEF Test Project** (*File > New > Project*, search for "TAEF").
Add classes with the **TAEF Test** item template (*Add > New Item*).
The templates derive a namespace from the project name or root namespace. Test Explorer groups tests by namespace and class;
classes in the global namespace appear under a placeholder node.

The templates come with a wizard (part of the extension) that turns names into C++ identifiers in TitleCase without
underscores, as usual in Windows code: every character other than a letter or digit separates words, the first character
of each word is made uppercase and the others are kept as typed, and a name that starts with a digit gets the prefix
`Taef`. For example, the project "Contoso.Unit Tests" gets the root namespace `ContosoUnitTests`, and the item
"My New-Tests.cpp" declares the class `MyNewTests` (Visual Studio's own template parameters would give `Contoso_Unit_Tests`
and `My_New_Tests`). The item template derives the namespace in the same way from the project's `RootNamespace` (or,
without one, the project name), whether or not it is a valid identifier: `Contoso.Unit-Tests` (written into a project file
by hand) and `Contoso_Unit_Tests` both become `ContosoUnitTests`. The wizard appends `Tests` to a name that would clash
with the templates' code or with the headers they include, for example the class `AdditionTests` for the item
"Addition.cpp" (the item template's class has a test method `Addition`) or the namespace `LogTests` for the project "Log"
(the templates use `Log::Comment`).

The project template is set up as follows; an existing project needs at least steps 1 and 2 (and step 4 for source
locations):

1. The project is a **DLL** (*Configuration Type: Dynamic Library*).
2. **Include directory** `$(WindowsSdkDir)Testing\Development\inc`, **library directory**
   `$(WindowsSdkDir)Testing\Development\lib\$(PlatformShortName)`, **additional dependencies**
   `TE.Common.lib;Wex.Common.lib;Wex.Logger.lib`.
3. `INLINE_TEST_METHOD_MARKUP` is defined, which allows you to write the body of test methods and fixtures directly after
   `TEST_METHOD(...)`, and the sources are compiled with `/utf-8`, so that non-ASCII test names are handled correctly.
4. The linker creates a full PDB (*Generate Debug Info: /DEBUG:FULL*), from which the adapter reads the source locations of
   the tests.
5. F5 runs the tests under the debugger: the project file defines `TE.exe` of the Windows Kits as default debugging command,
   with the arguments `"$(TargetPath)" /inproc`. *Command* and *Command Arguments* entered in *Project Properties >
   Debugging* (stored in the `.vcxproj.user` file) take precedence; the arguments must then contain the quoted test DLL and
   `/inproc` (so that the tests run in the debugged `TE.exe` process and their breakpoints are hit), for example
   `"$(TargetPath)" /inproc /name:*Addition` to debug only some of the tests. For the ARM64 configurations, this applies
   where Visual Studio debugs ARM64 processes locally (on ARM64 Windows); elsewhere, Visual Studio uses the *Remote Windows
   Debugger* for ARM64 by default.

A test class needs no base class. Test methods use `void Method()`; setup and cleanup fixtures use `bool Fixture()`.

[Authoring Tests in C++](https://learn.microsoft.com/en-us/windows-hardware/drivers/taef/authoring-tests-in-c--)

```cpp
#include "WexTestClass.h"   // with INLINE_TEST_METHOD_MARKUP defined in the project settings

using namespace WEX::Common;
using namespace WEX::Logging;
using namespace WEX::TestExecution;

namespace Contoso
{
    namespace Tests
    {
        class MathTests
        {
            TEST_CLASS(MathTests);

            TEST_CLASS_SETUP(ClassSetup)          // once before the first test of the class
            {
                Log::Comment(L"setting up");      // WEX logging, visible in the TE.exe output
                return true;                      // false: the tests of the class are Blocked
            }

            TEST_METHOD(Addition)
            {
                VERIFY_ARE_EQUAL(4, 2 + 2);       // a failing VERIFY_* fails (and ends) the test
            }

            BEGIN_TEST_METHOD(Rounding)           // a test with metadata (shown as traits) ...
                TEST_METHOD_PROPERTY(L"Priority", L"1")
                TEST_METHOD_PROPERTY(L"Data:Value", L"{1, 2, 3}")   // ... which is data-driven
            END_TEST_METHOD()
        };

        void MathTests::Rounding()                // BEGIN_TEST_METHOD tests are defined outside of the class
        {
            int value = 0;
            VERIFY_SUCCEEDED(TestData::TryGetValue(L"Value", value));
            Log::Comment(String().Format(L"Value = %d", value));
            VERIFY_IS_GREATER_THAN(value, 0);
        }
    }
}
```

After you build the project, Test Explorer shows the tests `Contoso::Tests::MathTests::Addition` and
`Contoso::Tests::MathTests::Rounding#metadataSet0` to `#metadataSet2`. You can run the same tests on the command line with
`TE.exe <path of the test DLL>`. See the [TAEF documentation](https://learn.microsoft.com/windows-hardware/drivers/taef/) for
all macros, metadata, data sources and runtime parameters; the sample test DLLs in
[SampleTests](https://github.com/axelriet/TaefTestAdapter/tree/main/SampleTests) show many more examples.

### C# tests

Create a **TAEF C# Test Project** and add classes with **TAEF C# Test**. The project targets .NET Framework 4.8 by default.
For modern .NET, set `TargetFramework` to `net8.0` or `net10.0`; use `TargetFrameworks` to build multiple targets.

The template references TAEF's managed assemblies and includes the test SDK and adapter package.
Test attributes come from `WEX.TestExecution.Markup` in `TE.Managed.dll`. This example has one test, three data rows
and one async test:

[Authoring Tests in C#](https://learn.microsoft.com/en-us/windows-hardware/drivers/taef/authoring-tests-in-c-)

```csharp
using System;
using System.Threading.Tasks;
using WEX.TestExecution;
using WEX.TestExecution.Markup;

namespace Contoso.Tests
{
    [TestClass]
    public class MathTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void Addition()
        {
            Verify.AreEqual(4, 2 + 2);
        }

        [TestMethod]
        [TestProperty("Priority", "1")]
        [TestProperty("Data:Value", "{1, 2, 3}")]
        public void Rounding()
        {
            int value = Convert.ToInt32(TestContext.DataRow["Value"]);
            Verify.IsTrue(value > 0);
        }

        [TestMethod]
        public async Task AsyncOperation()
        {
            await Task.Delay(1);
            Verify.IsTrue(true);
        }
    }
}
```

Test Explorer shows `Contoso.Tests.MathTests.Addition`, `Rounding#metadataSet0` through `#metadataSet2`, and `AsyncOperation`.
Class and assembly setup and cleanup methods must be static. The `SampleTests\ManagedTests` project includes fixtures,
traits, ignored tests, async failures and XML data sources for .NET Framework, .NET 8 and .NET 10.

## Run and debug tests

Test Explorer shows each test with its TAEF name (see *Test names*) and groups the tests by namespace and class.
Run and debug them as any other tests; the following sections describe what the adapter does.

### Test discovery

Visual Studio passes a solution's DLLs to the adapter. `vstest.console.exe` passes the DLLs on its command line.
For each DLL, the adapter:

1. Checks for `<dll>.is_taef_test`. If it exists, the DLL is a candidate.
   Otherwise, a configured `TestDiscoveryRegex` decides whether the full path matches.
   Without either override, a native candidate needs the `testdata` section from `WexTestClass.h` and an import or
   delay-load import of `Wex.Logger.dll`, `Wex.Common.dll` or `TE.Common.dll`.
   A managed candidate needs a `TE.Managed` assembly reference and a recognized test-class attribute.
   The check reads the file without loading it. Other DLLs are ignored silently; `OutputMode` `Debug` explains why.
2. Selects `TE.exe` and, for managed tests, the runtime. See *TE.exe selection*.
3. Runs `TE.exe "<dll>" <additional TE.exe arguments> /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false`
   with the configured working directory, `PATH` extension and environment variables.
   `TestDiscoveryTimeoutInSeconds` limits discovery; a timeout kills `TE.exe` and its child processes.
   TAEF can load managed assemblies and execute data providers while listing tests. Required dependencies must be available.
   A nonzero exit code or timeout produces an error and no discovered tests.
   The adapter logs `Error:` and `Warning:` lines from the listing at their respective severities.
   A "not recognized to be a TAEF test" message is a warning for a statically recognized candidate; otherwise it appears only in debug output.
4. Resolves source locations when `ParseSymbolInformation` is enabled; see `AdditionalPdbs`.
   Managed tests use matching Windows, portable or embedded portable PDBs, including symbols for adjacent inherited implementations.
   Data rows share their test method's source location.

`TE.exe` prints property and data values without escaping line breaks. The adapter joins continuation lines with `\n`.
An empty line inside a value is ambiguous because it also ends a listing block.
The adapter ignores lines that cannot belong to the listing and reports
`TE.exe printed <n> unexpected line(s) while listing the tests of test DLL ...`.
Some tests or traits may then be missing or incorrect.

### Traits

Traits combine module, class and test properties; the most specific scope wins. For example, `Priority` appears as `Priority [1]`.
Multiple values of one property appear as one trait with space-separated values.
The adapter omits `TaefTestType`, `Metadata:Index`, `DataSource`, `Description`, `Data:*` and managed compiler attributes.
`Ignore` appears with its value, for example `Ignore [true]`.

You can assign additional traits with regular expressions which are matched against the TAEF names of the tests
(options `TraitsRegexesBefore` and `TraitsRegexesAfter`). Traits are assigned in three phases:

1. traits of the tests matching one of the regexes of option `TraitsRegexesBefore`,
2. the traits from the TAEF metadata, overriding traits with the same name from phase 1,
3. traits of the tests matching one of the regexes of option `TraitsRegexesAfter`, overriding traits with the same name from
   phases 1 and 2.

Within a phase, a test can receive several traits with the same name.
Use `<regex>///<trait name>,<trait value>` and separate entries with `//||//`.
For example: `.*::Performance::.*///Type,Slow//||//.*#metadataSet.*///Kind,DataDriven`.

### Test execution

The adapter runs one `TE.exe` process per test DLL (per thread in parallel execution, see *Parallel test execution*):

```
TE.exe "<dll>" <additional TE.exe arguments> /unicodeOutput:false /coloredConsoleOutput:false [/runIgnoredTests]
       [/breakOnError] [/testmode:Loop /Loop:<n> /LoopTest:1 | /testmode:Loop /Loop:1 /LoopTest:<n>]
       [/testTimeout:<t>] [/isolationLevel:<level>] [/inproc] [/disableTimeouts]
       [/enableWttLogging /logFile:"<file>"] [/select:"<query>" | /select:"(<your query>) and (<query>)"]
```

* The switches are added according to the options `RunIgnoredTests`, `BreakOnError` (only while debugging),
  `NrOfTestRepetitions` (only if greater than 1), `TestTimeout`, `IsolationLevel` and `RunInProcess` (`/inproc` and
  `/disableTimeouts` are always used while debugging, see *Debug tests*). `/enableWttLogging` is only used while
  debugging with `DebuggerKind` `VsTestFramework`.
* **In process** (`RunInProcess` or debugging), the adapter omits `/testTimeout` and `/isolationLevel`.
  TAEF ignores timeouts in process and cannot start the extra hosts required for isolation.
  Repetitions use `/testmode:Loop /Loop:1 /LoopTest:<n>`: each test repeats *n* times in the same process.
  Module and class fixtures run once. Out of process, repetitions instead use *n* loops with new hosts.
* `/select` is omitted if all tests of the DLL are run (the additional `TE.exe` arguments are then passed unchanged).
  Otherwise it selects whole classes if all of their tests are run, with
  `(@Name='<class>::*' and not @Name='<class>::*::*')`. This excludes nested classes.
  Tests whose names contain a further `::`, such as row `a::b`, use individual name terms: `@Name='<test>'`.
  A `/select` or `/name` in additional arguments is removed and combined with the adapter's query:
  `/select:"(<your query>) and (<query>)"`. A `/name:<name>` becomes `@Name='<name>'`.
  If the command line would get longer than 30,000 characters, the tests of the DLL are run with several `TE.exe`
  invocations. Managed class queries use `.` as the scope separator.
  Managed `ExecutionGroup` members stay together; an oversized atomic selection reports an error instead of splitting the group.
* TAEF cannot escape `*` or `?` in `@Name='...'`, and `/select:"..."` cannot contain a `"`.
  The adapter replaces those characters and control characters with `?`, which matches one character.
  For table rows, it also selects the row index: `(@Name='<pattern>' and @Data:Index=<n>)`.
  TAEF may still execute extra matching tests; their results are ignored. See *Limitations*.
* By default, TAEF runs the tests in a separate test host process (`TE.ProcessHost.exe`).
* The results are reported while the tests are running (except while debugging with `DebuggerKind` `VsTestFramework`);
  test durations are measured by the adapter (TAEF does not print them). Each repetition of a test produces its own result.
* Tests which TAEF ignores (property `Ignore` with value `true` (case-insensitive) or `1`) are not run unless option
  `RunIgnoredTests` is enabled; they are reported as skipped with the message
  `Test is marked Ignore=true - enable option 'Also run ignored tests' to run it.` Other values (for example, `false`, `yes`,
  `2`, or `true` with surrounding blanks) do not make `TE.exe` skip a test, so the adapter runs such tests as well.
* A Windows job object contains `TE.exe`, its hosts and child processes, unless a child explicitly breaks away.
  Canceling normally waits for `TE.exe` to finish. With `KillProcessesOnCancel`, it kills the job immediately.
  Terminating the adapter's host while `TE.exe` runs also kills the job.
  `VsTestFramework` debugging uses the platform launcher without this job object; cancellation kills the `TE.exe` process tree.
* Child processes that outlive `TE.exe` keep running; the adapter does not wait for them.
  If they keep its output open, the adapter waits at most three seconds, then warns with their names and IDs
  (`Process <id> ('<TE.exe>') has exited, but its output is still being kept open by processes started by it which are
  still running: <name> (<id>), ...`).
* Unexpected `TE.exe` exit codes (for example, `0x05000000` "no test files", `0x06000000` "startup or `/select` error",
  `0x07000000` "no tests executed") and TAEF errors outside of tests are logged to the *Tests* output window.

### Test results

| TAEF result | Visual Studio outcome |
|---|---|
| Passed | Passed |
| Failed | Failed |
| Skipped | Skipped |
| Blocked (for example, a setup method returned `false` or crashed, the DLL or a dependency could not be loaded) | Failed, with an error message starting with `Blocked: ` |
| NotRun | Not run (outcome *None*) |
| test crashed its test host process (out of process; `TE.exe` continues with the next test) | Failed (TAEF's result) |
| test crashed `TE.exe` (in process) | Failed, with the error message `!! This test has probably CRASHED !! (TE.exe terminated with exit code 0x<code>)` |
| not run because a test crashed `TE.exe` (in process) | Skipped, with the error message `reason is probably a crash of test <name>` |
| not run because `TE.exe` terminated abnormally outside of a test (for example, a setup or cleanup method crashed in process) | Failed, with the error message `Test has not been run: TE.exe terminated abnormally with exit code 0x<code> ...` and the last output of `TE.exe` |
| ignored (property `Ignore`, option `RunIgnoredTests` not enabled) | Skipped, without running the test |
| requested, but no result reported by `TE.exe` | according to option `MissingTestsReportMode` (default: *Not found*) |

Failed results include TAEF's `Error:` messages and `TestFailed:`/`TestBlocked:`/`TestSkipped:`/`TestNotRun:` comments.
These include failed verifications, exceptions, crashes and setup failures, with the setup output that preceded them.
Several errors are numbered `#1 - ...`. Errors with source information have clickable stack entries; managed exception traces retain their frames.
Multiline errors retain their complete message when TAEF supplies source information; otherwise the message uses the first line.
The test output retains the complete log, including `Log::Comment` and `Verify:` lines.
Enable `PrintTestOutput` to copy the complete `TE.exe` console output to the *Tests* output window.

**Cleanup failures do not change test results.** TAEF reports a test's result before method, class and module cleanup.
A later cleanup error produces a warning in the *Tests* output window and warning summary.
Put checks that must fail a test in the test method, such as the memory checks in `LeakCheckTests` under
[SampleTests](https://github.com/axelriet/TaefTestAdapter/tree/main/SampleTests).

### Debug tests

Choose *Debug* in Test Explorer. The adapter runs `TE.exe` with `/inproc` and `/disableTimeouts`.
Enable `BreakOnError` to add `/breakOnError`, which breaks when a test logs an error.
Timeouts and isolation levels do not apply during debugging. Repetitions run each test in a row within the same `TE.exe` process.

The debugger attaches to `TE.exe`, where the test code runs. The adapter's test-platform host remains outside the debugger.

Option `DebuggerKind` selects how the debugger is attached:

* `Native` (default) and `ManagedAndNative`: the adapter starts `TE.exe` and attaches the Visual Studio debugger (native, or
  native and managed code). For managed test DLLs it automatically includes the .NET Framework or CoreCLR engine used
  by TAEF. These engines need the extension (VSIX); with the NuGet package, debugging with them fails with
  an error.
* `VsTestFramework`: the test platform launches `TE.exe` under the debugger. This is the only engine available without the
  extension (NuGet package): set `<DebuggerKind>VsTestFramework</DebuggerKind>` in a `.runsettings` file. The console
  output of `TE.exe` is not available to the adapter in this mode, so `TE.exe` additionally gets
  `/enableWttLogging /logFile:"<temporary file>"`, and the results are read from that WTT log after `TE.exe` has
  finished. Consequently:
  * all results are reported when `TE.exe` has finished, not while the tests are running;
  * the output of a test contains only WEX logging (for example, `Log::Comment`) and `Verify:` lines, but no
    `printf`/`std::cout` output, and option `PrintTestOutput` has no effect;
  * test durations are not measured (they are shown as about 0 ms);
  * a crash leaves an incomplete WTT log. The adapter reads `<log file>.trace` to recover completed results and report the active test as crashed.
    Remaining tests follow the rules under *Crashes* in *Limitations*.

  Use `Native` or `ManagedAndNative` (with the extension) to avoid these restrictions.

If the debugger cannot be attached to `TE.exe` (`Native`, `ManagedAndNative`), `TE.exe` is terminated before it runs any
test, an error is logged, and the tests are reported according to option `MissingTestsReportMode`.

## Configure

### Visual Studio options

The options of the adapter are in *Tools > Options > Test Adapter for TAEF*, with the pages *General*, *Test Discovery*,
*Test Execution* and *TAEF* (only available if the extension is installed). These are the *global* settings.

In Visual Studio 2026 the pages are classic options pages: open *Tools > Options* and use the link to the legacy Options
dialog (or search for TAEF).

### Toolbar

The toolbar *Test Adapter for TAEF* (*View > Toolbars*) has the switches *Run tests in process (/inproc)*, *Break on error*,
*Parallel test execution* and *Print test output*; they change (and show) the corresponding global options.

### Settings files

* **Solution settings file**: a file `<SolutionName>.taef.runsettings` next to the solution
  file `<SolutionName>.sln` (for example, `Foo.taef.runsettings` for `Foo.sln`); you can share it via source control. It is
  used by the extension only (not with the NuGet package or `vstest.console.exe`).
* **User settings files**: `.runsettings` files selected in Visual Studio (*Test > Configure Run Settings*) or passed to
  `vstest.console.exe` with `/Settings:<file>`.
* Environment variable `TAEF_ADAPTER_FALLBACK_SETTINGS`: path of a settings file which is used *only* if the adapter does not
  receive any settings from the test platform.

Solution and user files use the same format. Within `<RunSettings>`, `<TaefTestAdapterSettings>` contains solution settings and optional project settings.
A project `<Settings ProjectRegex="...">` applies to DLLs whose full paths match its required regular expression.
The file
[AllTestSettings.taef.runsettings](https://github.com/axelriet/TaefTestAdapter/blob/main/TaefTestAdapter/Resources/AllTestSettings.taef.runsettings)
lists all settings (except `SkipOriginCheck`) with their default values, the schema is
[TaefTestAdapterSettings.xsd](https://github.com/axelriet/TaefTestAdapter/blob/main/TaefTestAdapter/TestAdapter/TaefTestAdapterSettings.xsd).

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <TaefTestAdapterSettings>
    <SolutionSettings>
      <Settings>
        <AdditionalTestExecutionParam>/p:"ServerName=test server"</AdditionalTestExecutionParam>
        <PathExtension>$(SolutionDir)ThirdParty\bin\$(PlatformName)</PathExtension>
        <ParallelTestExecution>true</ParallelTestExecution>
        <TestTimeout>0:05</TestTimeout>
      </Settings>
    </SolutionSettings>
    <ProjectSettings>
      <Settings ProjectRegex=".*\\LongRunningTests_taef\.dll$">
        <TestTimeout>1:00</TestTimeout>
        <IsolationLevel>Class</IsolationLevel>
      </Settings>
    </ProjectSettings>
  </TaefTestAdapterSettings>
</RunSettings>
```

An unknown element, invalid value or project `<Settings>` without `ProjectRegex` invalidates the whole `<TaefTestAdapterSettings>` node.
The adapter logs the problem and ignores that node:

* with the extension, the node of a user settings file is ignored (`Invalid run settings, node TaefTestAdapterSettings is
  ignored (the settings of the solution settings file and the Visual Studio options are used instead): ...`), and an invalid
  solution settings file is ignored as well (`Solution test settings file could not be parsed, ...`);
* without the extension (NuGet package, `vstest.console.exe`), the default settings are used (`ERROR: Invalid run settings,
  node TaefTestAdapterSettings is ignored and default settings are used: ...`).

Settings are merged in two stages:

1. Global settings, the solution settings file and the user settings file are merged, in increasing priority. Project settings
   of the solution and the user settings file are merged if they have exactly the same regular expression.
2. When a test DLL is discovered or run, its full path is matched against the regular expressions of the project settings;
   the first matching project settings are used, merged with the solution settings.

For a test DLL, settings have this priority, highest first: user project, solution project, user solution, solution file, global.
Keep settings files limited to values that differ from the defaults.

### Placeholders

| Placeholder | Value | Available in |
|---|---|---|
| `$(TestDll)` | full path of the test DLL | `TeExecutable`, `AdditionalPdbs`, `WorkingDir`, `PathExtension`, `EnvironmentVariables`, `AdditionalTestExecutionParam` |
| `$(TestDllDir)` | folder of the test DLL | as `$(TestDll)` |
| `$(SolutionDir)`, `$(PlatformName)`, `$(ConfigurationName)` | solution folder, platform and configuration of the solution | the settings listed for `$(TestDll)`, and with restrictions `BatchForTestSetup`/`BatchForTestTeardown` (see below and *Feature availability*) |
| `$(TestDir)` | a temporary folder the tests may use (one per thread, deleted after the test run) | `WorkingDir`, `EnvironmentVariables`, `AdditionalTestExecutionParam` (test execution only), `BatchForTestSetup`/`BatchForTestTeardown` |
| `$(ThreadId)` | id of the thread running the tests | as `$(TestDir)` |
| `%NAME%` | value of environment variable `NAME` | all settings listed in this table |
| `$(<key>)` | value from a *settings helper file* of the test DLL | the settings listed for `$(TestDll)` (not the batch files) |

During test discovery, `$(TestDir)` and `$(ThreadId)` are removed.

Batch files run outside the scope of a single test DLL, so they do not use settings helper files.
Their `$(PlatformName)` and `$(ConfigurationName)` require Visual Studio with the extension.
`$(SolutionDir)` requires a solution folder from Visual Studio or the test platform, for example through
`<RunConfiguration><SolutionDirectory>...</SolutionDirectory></RunConfiguration>` in the `.runsettings` file.
Unavailable values become empty strings. Use absolute paths or `%NAME%` environment variables in those batch settings.

### Settings helper files

The adapter cannot read Visual Studio project settings. Outside Visual Studio, it also lacks the solution folder, platform and configuration.
A *settings helper file* supplies these values. Place `<test dll>.taef_settings_helper` next to the DLL.
Use one line of `Key=Value` pairs separated by `::TAEF::`; each key becomes a `$(Key)` placeholder.
The keys `SolutionDir`, `PlatformName` and `ConfigurationName` supply their corresponding placeholders.
Create the file with a post-build event:

```
echo SolutionDir=$(SolutionDir)::TAEF::PlatformName=$(PlatformName)::TAEF::ConfigurationName=$(ConfigurationName)::TAEF::TheTarget=$(TargetFileName) > "$(TargetPath).taef_settings_helper"
```

Keep the file name quoted. An unquoted path containing spaces can prevent the write without a build error.

With this file, you can use `$(TheTarget)` in the settings (for example,
`<AdditionalTestExecutionParam>/p:"Target=$(TheTarget)"</AdditionalTestExecutionParam>`). Make sure your version control
ignores these files.

### Parallel test execution

Tests run sequentially by default. Enable `ParallelTestExecution` to distribute them across `MaxNrOfThreads` threads,
each with its own `TE.exe` invocations. Duration records in `<test dll>.taef.testdurations` help balance later runs.
Exclude those files from version control. Selected managed `ExecutionGroup` members remain together within their class,
in their listed order. The adapter does not add unselected prerequisites.
Test Explorer's *Run tests in parallel* and `vstest.console.exe /Parallel` do not control parallelism within one test DLL.

Module and class setup/cleanup methods run in every `TE.exe` invocation that runs tests of the module or class.

### Test setup and teardown

`BatchForTestSetup` and `BatchForTestTeardown` run before and after tests, once per thread, in the solution folder if known.
Use `$(TestDir)` and `$(ThreadId)` to prepare thread-specific resources.
A missing file produces an error; a nonzero exit produces a warning. Tests still run.
Child processes can outlive a batch file. If they keep its output open, the adapter continues after three seconds with a warning.
See *Test execution*. TAEF's module, class and method fixtures remain available.

## Reference

### Settings

| Setting (XML element) | Options page: option | Default | Description |
|---|---|---|---|
| `PrintTestOutput` | General: *Print test output* | `false` | Print the complete console output of `TE.exe` to the *Tests* output window (not with parallel execution, and not while debugging with `VsTestFramework`). `printf`/`std::cout` output of the tests is only part of it when running in process. |
| `OutputMode` | General: *Output mode* | `Info` | Amount of output of the adapter: `None`, `Info`, `Debug`, `Verbose`. |
| `TimestampMode` | General: *Timestamp output* | `Automatic` | Timestamps in the adapter's output: `Automatic`, `PrintTimestamp`, `DoNotPrintTimestamp` (Visual Studio 2022 and later add timestamps themselves, so `Automatic` never prints them). |
| `SeverityMode` | General: *Print severity* | `Automatic` | Severity in the adapter's output: `Automatic`, `PrintSeverity`, `DoNotPrintSeverity`. |
| `SummaryMode` | General: *Print summary* | `WarningOrError` | Print a summary of warnings and errors after discovery/execution: `Never`, `Error`, `WarningOrError`. |
| `PrefixOutputWithTaef` | General: *Prefix output with [TAEF]* | `false` | Prefix the adapter's output with `[TAEF]`. |
| `SkipOriginCheck` | General: *Skip check of file origin* | `false` | Skip test-DLL origin checks. With VSIX, set it only in Visual Studio options; settings-file values are ignored. With NuGet, `vstest.console.exe` or `TAEF_ADAPTER_FALLBACK_SETTINGS`, it is read from the settings file. Discovery warns when enabled; use only trusted settings files. |
| `TestDiscoveryRegex` | Test Discovery: *Regex for test discovery* | *(empty)* | If set, exactly the DLLs whose full path matches this regex are treated as TAEF test DLLs (instead of checking the DLLs for TAEF metadata); DLLs with an indicator file `<dll>.is_taef_test` always are. |
| `TestDiscoveryTimeoutInSeconds` | Test Discovery: *Test discovery timeout in s* | `30` | Timeout of `TE.exe /listProperties` for one DLL; `0`: no timeout. |
| `ParseSymbolInformation` | Test Discovery: *Parse symbol information* | `true` | Read the source locations of the tests from the PDBs. |
| `TraitsRegexesBefore`, `TraitsRegexesAfter` | Test Discovery: *Before test discovery*, *After test discovery* | *(empty)* | Assign traits by regexes on the test names, see *Traits*. |
| `AdditionalPdbs` | Test Execution: *Additional PDBs* | *(empty)* | Search additional PDB files, for example `$(TestDllDir)\pdbs\*.pdb`. Separate patterns with `;`; `*` and `?` are allowed in file names. Managed PDBs must match the owning assembly's identity. |
| `WorkingDir` | Test Execution: *Working directory* | `$(TestDllDir)` | Working directory of `TE.exe` (and the tests) for discovery and execution. |
| `PathExtension` | Test Execution: *PATH extension* | *(empty)* | Folders added in front of the `PATH` of `TE.exe` (and thus of the test host processes) for discovery and execution, for example to find dependencies of the test DLLs. |
| `EnvironmentVariables` | Test Execution: *Environment variables* | *(empty)* | Environment variables for `TE.exe` and the tests, `Name=Value` pairs separated by `//\|\|//`. If `PathExtension` is set, a `PATH` variable given here (in any letter case) is ignored with a warning. |
| `AdditionalTestExecutionParam` | Test Execution: *Additional TE.exe arguments* | *(empty)* | Additional `TE.exe` arguments for discovery and execution, inserted before the adapter's switches, for example `/p:"Name=Value"` (runtime parameters) or `/runas:<context>`. A `/select` or `/name` given here restricts test discovery and is combined with the adapter's selection, see *Test execution*. |
| `BatchForTestSetup`, `BatchForTestTeardown` | Test Execution: *Test setup batch file*, *Test teardown batch file* | *(empty)* | Batch files executed before/after the test run (once per thread in parallel execution). Settings helper files are not used for them, see *Placeholders*. |
| `KillProcessesOnCancel` | Test Execution: *Kill processes on cancel* | `false` | Kill `TE.exe`, its test host processes and all processes started by the tests when a test run is canceled (see *Test execution*); cleanup methods of the tests are then not run. |
| `DebuggerKind` | Test Execution: *Debugger engine* | `Native` | Choose `VsTestFramework`, `Native` or `ManagedAndNative`. Managed DLLs automatically select the required CLR engine. Without VSIX, only `VsTestFramework` works, with restrictions; see *Debug tests*. |
| `ParallelTestExecution` | Test Execution: *Parallel test execution* | `false` | Run tests in parallel threads (see *Parallel test execution*). |
| `MaxNrOfThreads` | Test Execution: *Maximum number of threads* | `0` | Maximum number of threads for parallel execution; `0`: number of processors. |
| `MissingTestsReportMode` | Test Execution: *Behavior for missing test results* | `ReportAsNotFound` | How tests without result are reported: `DoNotReport`, `ReportAsNotFound`, `ReportAsSkipped`, `ReportAsFailed`. |
| `TeExecutable` | TAEF: *TE.exe path* | *(empty: automatic)* | Path of `TE.exe`, or of a folder containing `<arch>\TE.exe` or `TE.exe`; placeholders allowed (see *TE.exe selection*). |
| `RunIgnoredTests` | TAEF: *Also run ignored tests* | `false` | Also run the tests which TAEF ignores, that is, tests with property `Ignore` = `true` or `1` (`/runIgnoredTests`). |
| `BreakOnError` | TAEF: *Break on error* | `false` | While debugging, break into the debugger as soon as a test logs an error (`/breakOnError`). |
| `NrOfTestRepetitions` | TAEF: *Number of test repetitions* | `1` | Run *n* loops with new test hosts using `/testmode:Loop /Loop:<n> /LoopTest:1`; minimum 1. In process or during debugging, use `/Loop:1 /LoopTest:<n>` instead. Each test repeats within the same process; module and class fixtures run once. |
| `TestTimeout` | TAEF: *Test timeout* | *(empty)* | Set each test and fixture's timeout with `/testTimeout:<value>`, overriding test metadata. Format: `[Day.]Hour[:Minute[:Second[.FractionalSeconds]]]`; hours 0-23, minutes and seconds 0-59. Examples: `0:05` is five minutes; `0:0:30` is 30 seconds. The switch is omitted in process and during debugging. |
| `IsolationLevel` | TAEF: *Isolation level* | `Default` | Set minimum host isolation with `/isolationLevel:<level>`. Tighter test metadata takes precedence. `Default` omits the switch; TAEF uses one host per DLL. `Test` and `Method` use one host per test; `Class` uses one per class; `Module` uses one per DLL. The switch is omitted in process and during debugging. |
| `RunInProcess` | TAEF: *Run tests in process* | `false` | Run inside `TE.exe` with `/inproc`, exposing `printf`/`std::cout` output. A crash aborts the remaining run. Timeouts and isolation levels have no effect; repetitions stay within the process. Always enabled during debugging. |

The XML settings `DebuggingNamedPipeId`, `SolutionDir`, `PlatformName` and `ConfigurationName` are used internally.

### Test names

| TAEF test | Test Explorer display name (the TAEF name) |
|---|---|
| test method of a class in a namespace | `Contoso::Tests::MathTests::Addition` |
| test method of a class in the global namespace | `MathTests::Addition` |
| test method of a class template instance | `Contoso::Tests::Container<int>::Size` |
| row of a lightweight data-driven test (`Data:` properties) | `Contoso::Tests::MathTests::Rounding#metadataSet0` |
| row of a table data source | `Contoso::Tests::MathTests::Parse#0` or, for rows with a `Name`, `...::Parse#<row name>` |
| test method of a data-driven class (class with `Data:` properties or a `DataSource`), per class row | `Contoso::Tests::MathTests#metadataSet0::Addition` (table rows: `...::MathTests#0::Addition` or `...::MathTests#<row name>::Addition`) |
| data-driven method of a data-driven class | `Contoso::Tests::MathTests#metadataSet0::Rounding#metadataSet1` |
| data source that could not be read | `Contoso::Tests::MathTests::Parse#error` (data-driven class: `Contoso::Tests::MathTests#error`) |
| managed test method | `Contoso.Tests.MathTests.Addition` |
| managed method data row | `Contoso.Tests.MathTests.Rounding#metadataSet0` or `Contoso.Tests.MathTests.Parse#row.with.dot` |
| managed class and method data rows | `Contoso.Tests.MathTests#classRow.Rounding#methodRow` |

The display name is the exact TAEF name. For native tests, the fully qualified name replaces scope separators `::` with `.`.
It preserves `::` inside template arguments and data-row names.
Examples: `Contoso.Tests.MathTests.Rounding#metadataSet0`, `Contoso.Tests.MathTests#metadataSet0.Addition`, and
`Contoso.Tests.MathTests.Parse#a::b`. The `FullyQualifiedName` filter uses this name.

Managed fully qualified names remain unchanged. Their Test Explorer hierarchy uses the class supplied by TAEF,
so dots and `::` inside row names stay in their class or method labels. Data rows have distinct test identities.

For native tests, row names containing `::` can make data-driven names ambiguous. A test of a data-driven class with a
*named* table row, in a namespace (`Contoso::MathTests#Row::Addition`), cannot be told apart from the row `Row::Addition`
of a data-driven method `Contoso::MathTests`; it is shown as the latter (method `MathTests#Row::Addition` of class
`Contoso`). Native tests do not supply an explicit hierarchy to Test Explorer, which therefore derives the hierarchy
from the fully qualified name; so a `.` in a row name (for example, a row named `1.5`) can move the test to an unexpected
place in the tree.

### TE.exe selection

The PE machine type determines `<arch>`: `x86`, `x64`, `arm64`, or `arm` for 32-bit ARM.
AnyCPU managed DLLs without the 32-bit-required flag use the Windows architecture. An unreadable machine type also falls back to Windows.
The adapter searches in this order:

1. the value of option `TeExecutable` (placeholders are replaced), if set: if it is a file, that file; if it is a folder,
   `<folder>\<arch>\TE.exe` or, if that does not exist, `<folder>\TE.exe`;
2. otherwise `<Windows Kits root>\Testing\Runtimes\TAEF\<arch>\TE.exe`, where the Windows Kits root is read from the registry
   (`HKLM\SOFTWARE\Microsoft\Windows Kits\Installed Roots`, value `KitsRoot10`, 64-bit and 32-bit view); the roots
   `%ProgramFiles(x86)%\Windows Kits\10\` and `%ProgramFiles%\Windows Kits\10\` are tried as well;
3. otherwise `TE.exe` from the `PATH` (of the process running the adapter).

An invalid `TeExecutable` setting produces a warning, then the search continues with steps 2 and 3.
If no runner is found, the adapter logs an error and ignores the DLL. `OutputMode` `Debug` shows the selected runner.

ARM64EC test DLLs have the machine type of x64 in their PE header, so they are run with the x64 `TE.exe` (on ARM64 Windows,
it runs emulated and can load ARM64EC code; an arm64 `TE.exe` cannot). ARM64X DLLs have the arm64 machine type and are run
with the arm64 `TE.exe`. If `TeExecutable` is a folder, it must therefore contain `x64\TE.exe` (or `TE.exe`) for ARM64EC
test DLLs.

For .NET Framework tests, TAEF uses its Framework runtime. For `.NETCoreApp` targets, the adapter selects an installed
CoreCLR matching the selected `TE.exe` architecture. It uses the newest compatible patch of the target minor version,
or a higher minor within the same major version. It does not automatically cross major versions.
Explicit `TAEF_CoreCLR` environment settings and `CoreClrProfile` metadata take precedence.
A missing compatible runtime produces an error with the required version and architecture.

### Feature availability

| Feature | VS with VSIX | VS with NuGet package | vstest.console.exe |
|---|:---:|:---:|:---:|
| Test discovery and execution | yes | yes | yes |
| Test debugging | yes | only with `DebuggerKind` `VsTestFramework` (2) | - |
| Configuration: VS options, toolbar | yes | no | - |
| Configuration: solution settings file | yes | no | no |
| Configuration: user settings file | yes (*Test > Configure Run Settings*) | yes | yes (`/Settings`) |
| Placeholder `$(SolutionDir)` | yes | with settings helper files (1, 3) | with helper files (3) |
| Placeholders `$(PlatformName)`, `$(ConfigurationName)` | yes | with helper files (3) | with helper files (3) |
| Placeholders `$(TestDll)`, `$(TestDllDir)`, `$(TestDir)`, `$(ThreadId)`, environment variables, helper file keys | yes | yes | yes |
| C++ and C# project and item templates | yes | no | - |

(1) During test execution, `$(SolutionDir)` is also available without helper files.  
(2) Set in a `.runsettings` file; the default engines `Native` and `ManagedAndNative` need the extension. See
*Debug tests* for the restrictions of `VsTestFramework`.  
(3) Not in `BatchForTestSetup`/`BatchForTestTeardown`, for which helper files are not used (see
*Placeholders*).

### Limitations

* **Console output of tests**: by default, TAEF runs the tests in a separate test host process whose console output is lost,
  so only output of the WEX logging API (`Log::Comment`, `Log::Error`, ...) and of the `VERIFY_*` macros is available.
  `printf`/`std::cout` output of the tests is only visible when running in process (`RunInProcess`, or while debugging
  with `DebuggerKind` `Native` or `ManagedAndNative`; not with `VsTestFramework`, see *Debug tests*).
* **Crashes**: out of process (default), a crashing test fails with the crash code (for example,
  `terminated with exit code 0xC0000005`), TAEF starts a new test host process (running the module setup again) and
  continues with the remaining tests. A crashing setup method blocks the following tests of its class (they fail with
  `Blocked: ` and the crash message); tests after a crashing cleanup method run normally. In process, a crash terminates
  `TE.exe`:
  * if a test crashes, it fails (`!! This test has probably CRASHED !!`), and the remaining tests of that `TE.exe` run are
    reported as skipped (`reason is probably a crash of test <name>`);
  * if a setup or cleanup method crashes (outside of a test), all tests of that `TE.exe` run without result are reported as
    failed (`Test has not been run: TE.exe terminated abnormally ...`, with the last output of `TE.exe`), and an error is
    logged.

  While debugging with `DebuggerKind` `VsTestFramework`, the results come from the incomplete WTT log of the crashed
  `TE.exe` run (see *Debug tests*).
* **Cleanup methods** cannot fail tests: failures of `TEST_METHOD_CLEANUP`, `TEST_CLASS_CLEANUP` and `MODULE_CLEANUP` are
  logged as warnings, see *Test results*.
* **Error dialogs** of the Debug CRT (`abort()`, `assert`, `_ASSERTE`) or of Windows Error Reporting stop a test run until
  they are closed, see *A test run hangs, or a "Debug Error! abort() has been called" or assertion dialog appears* in
  *Troubleshoot*.
* **TAEF's own test adapter**: TAEF ships its own VSTest adapter `TE.TestAdapter.dll` (for
  example, in `Runtimes\TAEF\<arch>`). If the test platform finds it as well - for example, because the `Microsoft.Taef`
  NuGet package copies it into the output folder of the test project, or because a TAEF runtime folder is passed with
  `/TestAdapterPath` - all tests are discovered twice. Make sure that only one of the adapters is used, for example delete
  `TE.TestAdapter.dll` from the output folder after the build:
  ```xml
  <Target Name="RemoveTaefOwnTestAdapter" AfterTargets="Build">
    <Delete Files="$(OutDir)TE.TestAdapter.dll" />
  </Target>
  ```
  With the `Microsoft.Taef` NuGet package, `TE.exe` is copied into the output folder as well; set `TeExecutable` to
  `$(TestDllDir)` to use it.
* **Blocked tests** are reported as failed (with an error message starting with `Blocked: `), since Visual Studio has no
  corresponding outcome. Typical reasons are setup methods returning `false` and test DLLs or dependencies that cannot be
  loaded (`0x8007007E`: a dependency is missing - dependencies are searched in the test DLL's folder and on the `PATH`, not
  in the working directory; see `PathExtension`).
* **NotRun** results are shown as *Not Run* in Test Explorer.
* **Long command lines** use several `TE.exe` invocations; module and class fixtures run once per invocation.
  Managed execution groups remain atomic. A group selection that cannot fit produces an error without splitting its dependencies.
* **Selection**: only one `/select` (or `/name`) query is used by `TE.exe`. A `/select` or `/name` in
  `AdditionalTestExecutionParam` restricts the tests shown in Test Explorer; when a subset of the tests of a DLL is run,
  the adapter combines it with its own selection (`/select:"(<your query>) and (<adapter's query>)"`). Quotes must directly
  follow the colon of a switch: `/p:"Name=Value with spaces"`, not `"/p:Name=Value with spaces"`.
* **Similar test names**: `TE.exe` compares names case-insensitively, and names containing `*`, `?`, `"` or control
  characters can only be selected by patterns (see *Test execution*). So when some tests of a DLL are run,
  `TE.exe` may run further tests matching their names (the adapter ignores their results, but their fixtures run): tests
  whose names differ only in case, instances of class templates whose names differ in a single character at such a
  position (for example, `Container<int *>` and `Container<int &>`), and - for data-driven classes whose row names contain
  such characters and whose methods are data-driven as well - tests of other class rows with the same method row. Besides,
  `TE.exe` always runs the `#error` pseudo tests of data sources which cannot be loaded.
* **Non-ASCII paths**: `TE.exe` cannot open test DLLs whose paths contain non-ASCII characters (and writes log files whose
  paths contain them into a different folder). The adapter therefore passes the file name of the test DLL (if the working
  directory of `TE.exe` is the test DLL's folder, the default `$(TestDllDir)`) or its 8.3 short path to `TE.exe`, and the
  WTT log file used while debugging with `DebuggerKind` `VsTestFramework` is passed with the 8.3 short path of the
  temporary folder (or created in the working directory). If there is no such ASCII path, an error is logged; move the
  test DLL to a folder with an ASCII path, set `WorkingDir` to `$(TestDllDir)`, or enable 8.3 file names on the volume.
* **Property values containing empty lines** make the output of `TE.exe /listProperties` ambiguous, so some tests or traits
  may be missing or wrong (a warning is logged, see *Test discovery*).
* **Timeouts** (option `TestTimeout` and the `TestTimeout` metadata of tests) and **isolation levels** (`IsolationLevel`)
  are not effective when running in process or while debugging: `TE.exe` ignores timeouts with `/inproc` and cannot start
  the further test host processes that isolation levels need. **Test repetitions** (`NrOfTestRepetitions`) then repeat
  each test in a row within the same process, so module and class fixtures are not run for each repetition.
* **Native Test Explorer hierarchy**: native names supply the hierarchy, so dots in row names or named class rows
  may move tests to unexpected nodes. Managed tests supply an explicit class hierarchy. See *Test names*.
* **Architecture**: each test DLL is run with the `TE.exe` of its own architecture (required for `/inproc`); ARM64EC test
  DLLs are run with the x64 `TE.exe` (see *TE.exe selection*).
* Managed test execution requires the TAEF managed runtime files and the test's .NET runtime of the selected architecture.
* The adapter supports native C++ and managed DLLs. TAEF's script and AXE test formats are outside its supported scope.

## Run tests from the command line

```
vstest.console.exe MyTests_taef.dll OtherTests_taef.dll /TestAdapterPath:"<folder with TaefTestAdapter.TestAdapter.dll>" /Settings:My.runsettings
```

The adapter folder is, for example, `out\binaries\TaefTestAdapter\Release\TestAdapter` of a build (see *Build from source*),
the `build\_common` folder of the NuGet package, or the folder of the extracted VSIX. `vstest.console.exe` does not use the
solution settings file; pass all settings with `/Settings` (and provide solution related placeholders with
*settings helper files*). Helper files are not used for the setup/teardown batch files: there,
`$(PlatformName)` and `$(ConfigurationName)` are empty, and `$(SolutionDir)` is only available if the settings file contains
`<RunConfiguration><SolutionDirectory>...</SolutionDirectory></RunConfiguration>`; otherwise use absolute paths or
environment variables in `BatchForTestSetup`/`BatchForTestTeardown` (see *Placeholders*).

You can select tests with `/TestCaseFilter` (see the
[documentation](https://learn.microsoft.com/visualstudio/test/vstest-console-options)) using the properties
`FullyQualifiedName` (the dotted name, for example `FullyQualifiedName~Contoso.Tests.MathTests`), `DisplayName` (the TAEF
name), `Source`, `CodeFilePath`, `LineNumber`, `Id` and `ExecutorUri` (`executor://TestAdapterForTaef/v1`), and all traits
(trait names are not case sensitive), for example `/TestCaseFilter:"Priority=1"`. If the filter is invalid, an error is
logged and no tests are run.

## Troubleshoot

### General advice

* Make sure the tests can be listed and run on the command line, from the folder of the test DLL:
  `TE.exe <test dll> /listProperties` and `TE.exe <test dll>` (with the `TE.exe` of the DLL's architecture, for example
  `C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF\x64\TE.exe`).
* Set option `OutputMode` to `Debug` (and possibly `PrintTestOutput` to `true`) and check the *Tests* output window: it shows
  which DLLs are recognized as TAEF test DLLs, which `TE.exe` is used, and details of the `TE.exe` invocations.

### None or not all of my tests show up

* The DLL is not recognized as TAEF test DLL: create a file `<test dll>.is_taef_test` next to it, or configure
  `TestDiscoveryRegex` (for example, `.*_taef\.dll$`; in a settings file if you use the NuGet package).
* `TE.exe` was not found (an error in the *Tests* output window says so): install TAEF (see *Prerequisites*)
  or set `TeExecutable`.
* The test DLL does not originate from this computer (for example, it was downloaded; an error in the *Tests* output window
  says so): remove its *mark of the web* with `Unblock-File <test dll>` (PowerShell) or the *Unblock* check box of the file
  properties, or see option `SkipOriginCheck`.
* Test discovery of the DLL timed out: increase `TestDiscoveryTimeoutInSeconds`.
* `AdditionalTestExecutionParam` contains a `/select` or `/name` query restricting the tests.
* Test Explorer only shows tests of DLLs that have been built; rebuild the solution.

### The *Test Adapter for TAEF* options pages or toolbar are missing

* Visual Studio did not pick up the extension's registration (this can happen, for example, after installing the
  extension into a new *Experimental Instance*). Close all instances of Visual Studio, run
  `devenv /updateconfiguration` (add `/rootSuffix Exp` for the experimental instance) from a developer command prompt,
  and start Visual Studio again.
* In Visual Studio 2026, the options pages are shown in the classic *Options* dialog (use the link to it on the new
  settings page, or search for *Test Adapter for TAEF*).

### All my tests show up twice

* TAEF's own adapter `TE.TestAdapter.dll` is used as well, see *TAEF's own test adapter* in *Limitations*.

### Tests fail with "Blocked: ... 0x8007007E" or "Failed to load"

* A dependency of the test DLL cannot be found. Copy it next to the test DLL or add its folder with `PathExtension`.

### Tests have no source locations

* For C++, generate a full PDB with `/DEBUG:FULL` and rebuild.
* For C#, build with `DebugType` set to `portable`, `embedded` or Windows PDB output.
  External PDBs must match the assembly; embedded portable PDBs need no sidecar file.
* If an external PDB is missing from the DLL folder and its recorded location, use `AdditionalPdbs`.
  Inherited managed methods can use matching symbols from an adjacent base assembly.
* `ParseSymbolInformation` is `false`.

### The output of `printf`/`std::cout` is missing

* See *Console output of tests* in *Limitations*: use the WEX logging API or enable `RunInProcess` (while debugging, use
  `DebuggerKind` `Native` or `ManagedAndNative`).

### A test run hangs, or a "Debug Error! abort() has been called" or assertion dialog appears

* Debug builds of the C runtime show a modal dialog for `abort()`, failed `assert`/`_ASSERTE` and other CRT errors, and
  Windows Error Reporting may show one for a crashing test. The dialog belongs to the test host process
  (`TE.ProcessHost.exe`, or `TE.exe` in process) and blocks the test run until it is closed; the adapter cannot suppress it.
  Make the test DLL write such messages to `stderr` instead, for example in its module setup (a test DLL can have only one
  `MODULE_SETUP`; add the calls to an existing one):
  ```cpp
  #include <windows.h>
  #include <crtdbg.h>
  #include <stdlib.h>
  #include "WexTestClass.h"   // with INLINE_TEST_METHOD_MARKUP defined in the project settings

  MODULE_SETUP(DisableErrorDialogs)
  {
      _set_error_mode(_OUT_TO_STDERR);                                  // assert()
      _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);     // abort(): no message box, no error report
      for (int reportType : { _CRT_ASSERT, _CRT_ERROR })                // _ASSERTE, _CrtDbgReport, ...
      {
          _CrtSetReportMode(reportType, _CRTDBG_MODE_DEBUG | _CRTDBG_MODE_FILE);
          _CrtSetReportFile(reportType, _CRTDBG_FILE_STDERR);
      }
      SetErrorMode(SetErrorMode(0) | SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX); // no crash dialogs
      return true;
  }
  ```
  Instead of waiting for user input, `abort()` and failed `assert`s then terminate the test host process (the test fails
  as crashed), and failed `_ASSERTE`s are only reported. Setting `TestTimeout` or enabling `KillProcessesOnCancel` helps
  to end runs that hang nevertheless.

### Canceling a test run takes long

* Enable `KillProcessesOnCancel`: `TE.exe`, its test host processes and all processes started by the tests are then killed
  immediately.

### A warning says that `TE.exe` "has exited, but its output is still being kept open"

* A test (or a setup batch file) started a process which is still running and has inherited the output of `TE.exe` (or of
  the batch file). The adapter does not wait for such processes; they keep running (see *Test execution*).
  If they are not intended to outlive the test, end them in the test's cleanup method.

### A warning says that `TE.exe` "printed unexpected line(s)" while listing the tests

* A property or data value of a test contains an empty line (for example, a `Description` with an empty line), which makes
  the listing ambiguous; some tests or traits of the DLL may be missing or wrong. Remove the empty line from the value.

### Tests with `Ignore` metadata are run

* TAEF (and so the adapter) only ignores tests whose `Ignore` property is `true` (case-insensitive) or `1`; values such as `yes`,
  `2` or `true` with surrounding blanks do not make `TE.exe` skip a test.
* Option `RunIgnoredTests` is enabled.

### Tests fail with "Test has not been run: TE.exe terminated abnormally"

* `TE.exe` terminated outside of a test while running the tests in process (`RunInProcess`, or while debugging), for
  example because a setup or cleanup method crashed. The error message contains the last output of `TE.exe`; run the tests
  out of process to find the crashing method.

### My settings are not used: "Invalid run settings, node TaefTestAdapterSettings is ignored"

* The `<TaefTestAdapterSettings>` node contains an unknown element, an invalid value, or project settings without
  `ProjectRegex`; the message names the problem. Check the file against
  [TaefTestAdapterSettings.xsd](https://github.com/axelriet/TaefTestAdapter/blob/main/TaefTestAdapter/TestAdapter/TaefTestAdapterSettings.xsd).

### Test discovery is slow

* Configure `TestDiscoveryRegex` or create indicator files to avoid checking all DLLs, and/or disable
  `ParseSymbolInformation` for test DLLs where source locations are not needed (for example, via project settings).

### Test Explorer or the extension does not work after installation

* Visual Studio's MEF cache might be corrupted. Close Visual Studio and delete the folder
  `%LOCALAPPDATA%\Microsoft\VisualStudio\<version>\ComponentModelCache`.

## FAQ

### Can I use Test Adapter for TAEF side by side with other test adapters?

Yes. Its executor URI (`executor://TestAdapterForTaef/v1`), package and command GUIDs and its settings node
(`<TaefTestAdapterSettings>`) differ from those of other adapters, including the adapter that ships with TAEF
(`TE.TestAdapter.dll`, executor `executor://TaefTestAdapter`). If both TAEF adapters find the same test DLLs, the tests are
shown twice; see *TAEF's own test adapter* in *Limitations*.

### Which TE.exe is used?

The `TE.exe` of the Windows Kits that matches the architecture of the test DLL, unless you set `TeExecutable`; see
*TE.exe selection*. With `OutputMode` `Debug`, the *Tests* output window shows the `TE.exe` in use.

### Why is the output of `printf`/`std::cout` missing?

By default, TAEF runs the tests in a separate test host process whose console output is lost. Use the WEX logging API
(`Log::Comment`) or enable `RunInProcess`; see *Limitations*.

### Are managed TAEF tests supported?

Yes. C# TAEF tests targeting .NET Framework or modern .NET are discovered, shown in Test Explorer, and can be run and
debugged. Native and managed tests can be used in the same solution. Mixed-mode DLLs whose tests are native TAEF tests
continue to use native discovery. Managed debugging automatically selects the CLR engine used by TAEF.

### Should I use the Visual Studio extension or the NuGet package?

Use the extension for its options, toolbar, solution settings, templates and direct native or managed debugger attachment.
Use NuGet or `vstest.console.exe /TestAdapterPath` on build servers or machines without the extension.
See *Feature availability* for the differences.

### How do I make the adapter find a test DLL it does not recognize?

Create an empty file `<test dll>.is_taef_test` next to the DLL, or set `TestDiscoveryRegex`; see *Test discovery*.

## Build from source

Use Visual Studio 2026 for the full build, including .NET 10 samples. Visual Studio 2022 can build the adapter with `-SkipSamples`.
Install *.NET desktop development*, *Desktop development with C++* and *Visual Studio extension development*.
The C++ workload includes the DIA SDK.
The sample tests require TAEF development files from the WDK; see *Prerequisites*.
Build all managed targets with the .NET 10 SDK. Run them with their respective .NET Framework, .NET 8 and .NET 10 runtimes.

Repository paths reach 150 characters relative to the root. Without long-path support, Windows limits paths to 259 characters.
Clone into a root of at most 108 characters, such as `C:\src\TAEF-Test-Adapter`, or enable Git long paths before cloning.
Use `git clone -c core.longpaths=true ...` or `git config --global core.longpaths true`.
The Windows `LongPathsEnabled` setting alone does not enable long paths in Git.

```powershell
.\build.ps1                      # prepare, restore and build everything (Debug and Release)
.\build.ps1 -PrepareOnly         # only copy/generate the DIA SDK files and restore NuGet packages
.\build.ps1 -Configuration Release -Test   # build Release (and all sample test DLLs) and run the unit tests
.\build.ps1 -SkipSamples         # build the adapter without the sample test DLLs (no TAEF needed)
Get-Help .\build.ps1 -Detailed   # all options
```

[build.ps1](https://github.com/axelriet/TaefTestAdapter/blob/main/build.ps1) locates Visual Studio with `vswhere` and prepares DIA.
It copies `msdia140.dll` into `TaefTestAdapter\DiaResolver\x86`, `\x64` and `\arm64`, then generates `dia2\dia2.dll`
with `midl` and `tlbimp`. A missing ARM64 DIA DLL produces a warning; that build cannot resolve symbols in native ARM64 hosts.
The script restores NuGet packages and builds:

* `SampleTests\SampleTests.sln`: Debug/Release, Win32/x64;
* `SampleTests\ManagedTests\ManagedTests.csproj`: Debug/Release, AnyCPU, for .NET Framework 4.8, .NET 8 and .NET 10;
* `TaefTestAdapter\TaefTestAdapter.sln`: the adapter and packages.

Use `-SampleConfiguration`, `-SamplePlatform` and `-SkipSamples` to control sample builds.
Optionally, the script runs the tests with
`vstest.console.exe`. By running it, you accept the license terms of the DIA SDK (part of your Visual Studio license).
[build_preparation.bat](https://github.com/axelriet/TaefTestAdapter/blob/main/build_preparation.bat) runs the preparation
steps only, after which you can build the solutions in Visual Studio.

`SampleTestsBuilder` builds the native matrix and managed targets before the adapter unless `SkipSampleTestsBuild=true`.
Building through Visual Studio therefore requires TAEF. `build.ps1` builds samples separately and passes that property itself.
With `-SkipSamples`, it builds the adapter without TAEF. Tests then need samples from an earlier build.
Set `TAEF_ADAPTER_SAMPLES_DIR` for native samples or `TAEF_ADAPTER_MANAGED_SAMPLES_DIR` for the managed sample root.

Outputs:

* `out\binaries\TaefTestAdapter\<Configuration>\Packaging\`: the VSIX and the NuGet package,
* `out\binaries\TaefTestAdapter\<Configuration>\TestAdapter\`: the adapter (usable with `/TestAdapterPath`),
* `out\binaries\SampleTests\<Configuration>[-x64]\`: the native sample test DLLs,
* `out\binaries\ManagedSamples\<Configuration>\AnyCPU\<target framework>\`: the managed sample DLLs and data files.

`-Test` defaults to unit tests in four projects. To run all six projects, including integration and end-to-end tests, use:

```powershell
.\build.ps1 -Configuration Release -SkipBuild -Test `
    -TestCaseFilter 'TestCategory=Unit|TestCategory=Integration|TestCategory=End to end' `
    -TestProjects DiaResolver.Tests,Core.Tests,TestAdapter.Tests,VsPackage.Tests.Unit,VsPackage.Tests.Generated,VsPackage.Tests
```

Other tools:

* `TaefTestAdapter\SetVersion.bat <major>.<minor>.<revision>.<build>` sets the version of all assemblies of the C# projects
  in `TaefTestAdapter` (adapter, tests and helper tools), the VSIX manifest, the NuGet package and the assembly version in
  the `WizardExtension` of the project and item templates (Visual Studio loads the templates' wizard from
  `TaefTestAdapter.VsPackage` by the full name of this assembly). The changes of each version are listed in
  [CHANGELOG.md](https://github.com/axelriet/TaefTestAdapter/blob/main/CHANGELOG.md), which the VSIX contains as its release
  notes.
* You can debug the VS package in the experimental instance of Visual Studio (`devenv /rootsuffix Exp`);
  [TaefTestAdapter.ChildProcessDbgSettings](https://github.com/axelriet/TaefTestAdapter/blob/main/TaefTestAdapter/TaefTestAdapter.ChildProcessDbgSettings)
  contains settings for the *Microsoft Child Process Debugging Power Tool* to automatically attach to the test host
  processes and `TE.exe`.
* [Tools\make_branding_assets.py](https://github.com/axelriet/TaefTestAdapter/blob/main/Tools/make_branding_assets.py)
  generates the logo, template and toolbar images;
  [Tools\Expand-Vsix.ps1](https://github.com/axelriet/TaefTestAdapter/blob/main/Tools/Expand-Vsix.ps1) extracts a VSIX for
  inspection, for example `Tools\Expand-Vsix.ps1 out\binaries\TaefTestAdapter\Release\Packaging\TaefTestAdapter.vsix` into
  `out\vsix\TaefTestAdapter\`.

## Credits

Test Adapter for TAEF is a modified version of [Google Test Adapter](https://github.com/csoltenborn/GoogleTestAdapter) by
Christian Soltenborn, Jonas Gefele and contributors, parts of which were contributed by Microsoft; see
[LICENSE.md](https://github.com/axelriet/TaefTestAdapter/blob/main/LICENSE.md) and
[NOTICE](https://github.com/axelriet/TaefTestAdapter/blob/main/NOTICE).

Modified for TAEF support by Axel Rietschin, Claude Code, and Codex.

Test Adapter for TAEF is an independent project; it is not affiliated with or endorsed by Google or Microsoft. Google is a
trademark of Google LLC. Microsoft, Visual Studio and Windows are trademarks of the Microsoft group of companies.

## License

Test Adapter for TAEF is licensed under the
[Apache License, Version 2.0](https://github.com/axelriet/TaefTestAdapter/blob/main/LICENSE.md); see
[LICENSE.md](https://github.com/axelriet/TaefTestAdapter/blob/main/LICENSE.md) for the notice of modification and
[NOTICE](https://github.com/axelriet/TaefTestAdapter/blob/main/NOTICE) for attributions and third-party components.
