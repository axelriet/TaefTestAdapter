<img src="TaefTestAdapter/Packaging/Resources/taef-logo.png" alt="Test Adapter for TAEF logo" width="64" align="right" />

# Test Adapter for TAEF

Test Adapter for TAEF enables Visual Studio's testing tools with native C++ unit tests written for the
[Test Authoring and Execution Framework (TAEF)](https://learn.microsoft.com/windows-hardware/drivers/taef/). It discovers the
tests of your TAEF test DLLs, shows them in Test Explorer, and runs and debugs them with TAEF's test runner `TE.exe`. It
works in Visual Studio 2026 and Visual Studio 2022, and with `vstest.console.exe` (for example, on build servers).

In this article:

* [Why TAEF](#why_taef)
* [Overview](#overview)
* [Prerequisites](#prerequisites)
* [Install](#install)
* [Write TAEF tests](#write_tests)
* [Run and debug tests](#run_and_debug)
* [Configure](#configure)
* [Reference](#reference)
* [Run tests from the command line](#vstest_console)
* [Troubleshoot](#troubleshooting)
* [FAQ](#faq)
* [Build from source](#building)
* [Credits](#credits)
* [License](#license)

## <a name="why_taef"></a>Why TAEF

[TAEF](https://learn.microsoft.com/windows-hardware/drivers/taef/) is an industrial-grade test framework, fully supported by
Microsoft for many years and heavily used internally: TAEF is what tests *Windows itself*, its components and its drivers,
with millions of tests run every night on more than 300 branches. Microsoft's open-source
[Windows Terminal](https://github.com/microsoft/terminal/blob/main/doc/TAEF.md) project, for example, describes it as "used
extensively within the Windows organization" to test operating system code in a unified manner. TAEF ships with the Windows
Driver Kit (WDK) and the Windows Hardware Lab Kit; the WDK also provides
[TAEF runtime installers](https://learn.microsoft.com/windows-hardware/drivers/taef/getting-started#manually-installing-and-uninstalling-taef-on-a-test-computer)
for test machines that don't need the full development kit. TAEF gives developers and testers one consistent way to write,
share and run automated tests across teams and disciplines:

* **Tests that ship with your product**: TAEF test DLLs are standalone binaries. Ship them as artifacts alongside your
  product, beta or retail, and your users and customers can run them on their own rigs with nothing but the TAEF runtime:
  no Visual Studio, no development kit, no source code. They can validate an installation, qualify their hardware or
  reproduce an issue with the very same tests that run in your labs and your build pipeline.
* **Native C++ tests without boilerplate**: plain classes with `TEST_CLASS`/`TEST_METHOD`, fixtures at module, class and
  method level, and the `VERIFY_*` macros and WEX logging API that report failures with their source location. TAEF also
  runs managed and script tests; Test Adapter for TAEF supports native C++ test DLLs.
* **Metadata-driven test selection**: properties on modules, classes and methods (such as `Owner`, `Priority` or your own)
  and a query language (`TE.exe /select:"..."`) to pick exactly the tests to run.
* **Data-driven testing**: lightweight `Data:` properties, XML tables, PICT and WMI data sources.
* **Robust execution**: tests run in separate test host processes by default, so a crashing test does not take the test run
  down; timeouts, isolation levels, runtime parameters, repetitions and parallel execution.
* **Built for automation labs**: x86, x64 and ARM64, running tests elevated, as System or in an AppContainer, WTT logging,
  minidumps and stack traces on errors (`/miniDumpOnError`, `/stackTraceOnError`).

Test Adapter for TAEF brings all of this into Visual Studio's Test Explorer. Write your tests once, run and debug them in
Visual Studio, and run the same test DLLs in your build pipeline, in your labs and on your customers' machines.

## <a name="overview"></a>Overview

* **Test discovery without running test code**: TAEF test DLLs are recognized by their TAEF test metadata (a static check of
  the DLL file, no process is started for other DLLs). The tests are listed with `TE.exe <dll> /listProperties`, which reads
  the metadata of the DLL without loading it. You can force recognition with an indicator file or a regular expression.
* **Source locations** of the tests are read from the test DLL's PDB, so double-clicking a test opens its source code.
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
  arm64) is used; you can configure another TAEF.
* Setup and teardown batch files, additional `TE.exe` arguments (for example, runtime parameters `/p:"Name=Value"`),
  working directory, `PATH` extension and environment variables.
* **Project and item templates** for TAEF test DLLs and test classes, whose wizard derives TitleCase C++ namespace and
  class names from the project and file names (see [Write TAEF tests](#write_tests)).
* Support for `vstest.console.exe`, including test case filters.

## <a name="prerequisites"></a>Prerequisites

* Visual Studio 2026 (18.x) or Visual Studio 2022 (17.x), 64 bit, with the *Desktop development with C++* workload, or
  `vstest.console.exe` of these versions.
* **TAEF** (`TE.exe`). TAEF is installed with the Windows Driver Kit (WDK) and other Windows Kits that contain the Windows
  testing tools (for example, the Windows Hardware Lab Kit). They install TAEF into the *Testing* folder of the Windows
  Kits, for example `C:\Program Files (x86)\Windows Kits\10\Testing\`:
  * `Development\inc` and `Development\lib\<x86|x64|arm64>` contain the headers and import libraries you need to build TAEF
    tests,
  * `Runtimes\TAEF\<x86|x64|arm64>\TE.exe` is the test runner used by the adapter.

  Alternatively, you can take TAEF from the `Microsoft.Taef` NuGet package (where available; it is not published on
  nuget.org) or any other location; set the option [`TeExecutable`](#settings_reference) in that case.

## <a name="install"></a>Install

You can use Test Adapter for TAEF in three ways:

* **Visual Studio extension (VSIX)** - recommended: double-click the `.vsix` file (see [Build from source](#building)) and
  select Visual Studio 2026 and/or Visual Studio 2022 in the installer. After you restart Visual Studio, Test Explorer shows
  the tests of your TAEF test DLLs once they are built. This installation provides all features (debugging with all
  debugger engines, options, toolbar, solution settings file).
* **vstest.console.exe**: pass the folder containing `TaefTestAdapter.TestAdapter.dll` (and its dependencies) with
  `/TestAdapterPath:<folder>`, see [Run tests from the command line](#vstest_console).
* **NuGet package** `TaefTestAdapter` (built together with the VSIX, see [Build from source](#building); it is not on
  nuget.org, so add its folder as a package source): a development dependency of your test projects that makes Visual
  Studio find the adapter without installing the extension. Visual Studio integration is limited this way: tests can be
  discovered and run, but they can only be debugged with the debugger engine `VsTestFramework` (option `DebuggerKind`,
  set in a `.runsettings` file, see [Debug tests](#debugging)), there is no options page and no toolbar, and the
  solution settings file is not used; you can provide settings with a `.runsettings` file (see
  [Feature availability](#feature_availability)).

If no or not all tests show up, see [Troubleshoot](#troubleshooting).

## <a name="write_tests"></a>Write TAEF tests

If the extension is installed, create a project with the **TAEF Test Project** template (*File > New > Project*, search for
"TAEF") and add further test classes with the **TAEF Test** item template (*Add > New Item*). Both templates declare the test
classes in a namespace derived from the project's name or root namespace (see below): Test Explorer groups tests by
namespace and class and puts test classes of the global namespace under a placeholder node, so declare your own test classes
in a namespace, too.

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
   `"$(TargetPath)" /inproc /name:*Addition` to debug only some of the tests.

A test DLL contains test classes; a test class needs no base class, test methods have the signature `void Method()`, fixtures
(setup and cleanup methods) the signature `bool Fixture()`:

```cpp
#include "WexTestClass.h"   // with INLINE_TEST_METHOD_MARKUP defined in the project settings

using namespace WEX::Common;
using namespace WEX::Logging;
using namespace WEX::TestExecution;

namespace Contoso { namespace Tests
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
} }
```

After you build the project, Test Explorer shows the tests `Contoso::Tests::MathTests::Addition` and
`Contoso::Tests::MathTests::Rounding#metadataSet0` to `#metadataSet2`. You can run the same tests on the command line with
`TE.exe <path of the test DLL>`. See the [TAEF documentation](https://learn.microsoft.com/windows-hardware/drivers/taef/) for
all macros, metadata, data sources and runtime parameters; the sample test DLLs in [SampleTests](SampleTests) show many more
examples.

## <a name="run_and_debug"></a>Run and debug tests

Test Explorer shows each test with its TAEF name (see [Test names](#test_names)) and groups the tests by namespace and class.
Run and debug them as any other tests; the following sections describe what the adapter does.

### <a name="discovery"></a>Test discovery

Visual Studio passes all DLLs of a solution (and `vstest.console.exe` the DLLs given on its command line) to the adapter. For
each DLL, the adapter

1. decides whether it is a TAEF test DLL: it is if a file `<dll>.is_taef_test` exists next to it; otherwise, if option
   `TestDiscoveryRegex` is set, if its full path matches that regex; otherwise, if the DLL contains TAEF test metadata (the
   `testdata` section written by `WexTestClass.h`) and imports or delay-loads (`/DELAYLOAD`) one of the TAEF DLLs
   `Wex.Logger.dll`, `Wex.Common.dll` and `TE.Common.dll`. Other DLLs are ignored silently (see `OutputMode` `Debug` for
   details). This check does not start any process;
2. selects the `TE.exe` to be used (see [TE.exe selection](#te_selection));
3. runs `TE.exe "<dll>" <additional TE.exe arguments> /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false`
   (with the configured working directory, `PATH` extension and environment variables, and a timeout of
   `TestDiscoveryTimeoutInSeconds`, after which `TE.exe` and all processes started by it are killed). `TE.exe` reads the
   metadata from the DLL without loading it, so no test code (not even static constructors) is executed during discovery,
   and missing dependencies of the DLL do not prevent discovery. If `TE.exe` fails (non-zero exit code, timeout), an error
   is logged and no tests of the DLL are shown; `Error:` and `Warning:` messages `TE.exe` prints within the listing are
   logged as errors and warnings (except `TE.exe`'s message that a DLL is "not recognized to be a TAEF test" DLL, which is
   a warning if the DLL passes the static check of step 1, and only logged with `OutputMode` `Debug` otherwise);
4. reads the source locations of the tests from the PDB (option `ParseSymbolInformation`; also see `AdditionalPdbs`). All rows
   of a data-driven test share the source location of the test method.

`TE.exe` prints property and data values as they are, so a value containing line breaks (for example, a multi-line
`Description`) continues on the following lines of the listing. The adapter joins such lines to the value (separated by
`\n`). If a value contains an *empty* line, the listing is ambiguous, since `TE.exe` ends each block of values with an empty
line: the lines after the empty line cannot be told apart from the listing itself. Those which cannot belong to the
listing are ignored, and a warning (`TE.exe printed <n> unexpected line(s) while listing the tests of test DLL ...`) says
that some tests or their traits may be missing or wrong.

### <a name="traits"></a>Traits

The TAEF metadata of a test - its test properties, merged with the properties of its class and module (the most specific
wins) - are shown as traits, for example `Priority [1]`. Multi-valued properties (for example, two `Owner` properties) are
shown as one trait with space-separated values. The properties `TaefTestType`, `Metadata:Index`, `DataSource`,
`Description` and `Data:*` are not shown. `Ignore` is shown with its value like any other property, for example
`Ignore [true]`.

You can assign additional traits with regular expressions which are matched against the TAEF names of the tests
(options `TraitsRegexesBefore` and `TraitsRegexesAfter`). Traits are assigned in three phases:

1. traits of the tests matching one of the regexes of option `TraitsRegexesBefore`,
2. the traits from the TAEF metadata, overriding traits with the same name from phase 1,
3. traits of the tests matching one of the regexes of option `TraitsRegexesAfter`, overriding traits with the same name from
   phases 1 and 2.

Within a phase, traits are added (a test can have several traits with the same name). Syntax: `<regex>///<trait name>,<trait value>`,
several of those separated by `//||//`, for example `.*::Performance::.*///Type,Slow//||//.*#metadataSet.*///Kind,DataDriven`.

### <a name="execution"></a>Test execution

The adapter runs one `TE.exe` process per test DLL (per thread in [parallel execution](#parallelization)):

```
TE.exe "<dll>" <additional TE.exe arguments> /unicodeOutput:false /coloredConsoleOutput:false [/runIgnoredTests]
       [/breakOnError] [/testmode:Loop /Loop:<n> /LoopTest:1 | /testmode:Loop /Loop:1 /LoopTest:<n>]
       [/testTimeout:<t>] [/isolationLevel:<level>] [/inproc] [/disableTimeouts]
       [/enableWttLogging /logFile:"<file>"] [/select:"<query>" | /select:"(<your query>) and (<query>)"]
```

* The switches are added according to the options `RunIgnoredTests`, `BreakOnError` (only while debugging),
  `NrOfTestRepetitions` (only if greater than 1), `TestTimeout`, `IsolationLevel` and `RunInProcess` (`/inproc` and
  `/disableTimeouts` are always used while debugging, see [Debug tests](#debugging)). `/enableWttLogging` is only used while
  debugging with `DebuggerKind` `VsTestFramework`.
* **In process** (`/inproc`, that is, with option `RunInProcess` and always while debugging), `/testTimeout` and
  `/isolationLevel` are not passed: `TE.exe` ignores timeouts in process, and isolation levels need further test host
  processes, which `TE.exe` cannot start with `/inproc` (it would block the affected tests). So options `TestTimeout` and
  `IsolationLevel` have no effect then. For the same reason, repetitions are run with `/testmode:Loop /Loop:1 /LoopTest:<n>`
  in process: each test is repeated *n* times in a row within the same process (instead of *n* loops over all tests, each
  in new test host processes), so module and class setup and cleanup methods run only once.
* `/select` is omitted if all tests of the DLL are run (the additional `TE.exe` arguments are then passed unchanged).
  Otherwise it selects whole classes if all of their tests are run, with
  `(@Name='<class>::*' and not @Name='<class>::*::*')` (so that tests of nested classes are not included; tests of the class
  whose names contain a further `::`, for example data rows named `a::b`, are then selected by name), and otherwise single tests
  (`@Name='<test>'`). If the additional `TE.exe` arguments contain a `/select` or `/name`, it is removed from them and
  combined with the adapter's query: `/select:"(<your query>) and (<query>)"` (`/name:<name>` becomes `@Name='<name>'`).
  If the command line would get longer than 30,000 characters, the tests of the DLL are run with several `TE.exe`
  invocations.
* TAEF has no escape for the wildcards `*` and `?` in `@Name='...'`, and a `"` cannot be passed within `/select:"..."`.
  So for a test whose name contains `*`, `?`, `"` or control characters (for example, a data row named `a*`), the adapter
  replaces them by `?` (which matches any single character) and, for rows of table data sources, also selects the row by its
  index: `(@Name='<pattern>' and @Data:Index=<n>)`. `TE.exe` might nevertheless run a few more tests (see
  [Limitations](#limitations)); their results are ignored.
* By default, TAEF runs the tests in a separate test host process (`TE.ProcessHost.exe`).
* The results are reported while the tests are running (except while debugging with `DebuggerKind` `VsTestFramework`);
  test durations are measured by the adapter (TAEF does not print them). Each repetition of a test produces its own result.
* Tests which TAEF ignores (property `Ignore` with value `true` (case-insensitive) or `1`) are not run unless option
  `RunIgnoredTests` is enabled; they are reported as skipped with the message
  `Test is marked Ignore=true - enable option 'Also run ignored tests' to run it.` Other values (for example, `false`, `yes`,
  `2`, or `true` with surrounding blanks) do not make `TE.exe` skip a test, so the adapter runs such tests as well.
* `TE.exe` runs in a Windows job object together with all processes it starts: its test host processes, but also processes
  started by the tests (unless they explicitly break away from the job). Canceling a test run stops after the running
  `TE.exe` has finished, or - with option `KillProcessesOnCancel` - kills `TE.exe` and all these processes immediately. If
  the process running the adapter terminates while `TE.exe` is running (for example, because the test host process of
  Visual Studio or `vstest.console.exe` is killed), `TE.exe` and all these processes are killed as well. While debugging with
  `DebuggerKind` `VsTestFramework`, the test platform starts `TE.exe`, so there is no job object; canceling then kills the
  process tree of `TE.exe`.
* Processes started by the tests which are still running when `TE.exe` has exited (for example, a server a test started) are
  not waited for and keep running. If they keep the output of `TE.exe` open (they inherit it), the adapter waits for further
  output for at most 3 seconds after `TE.exe` has exited, and then continues with a warning naming these processes
  (`Process <id> ('<TE.exe>') has exited, but its output is still being kept open by processes started by it which are
  still running: <name> (<id>), ...`).
* Unexpected `TE.exe` exit codes (for example, `0x05000000` "no test files", `0x06000000` "startup or `/select` error",
  `0x07000000` "no tests executed") and TAEF errors outside of tests are logged to the *Tests* output window.

### <a name="results"></a>Test results

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

The error message of a failed test consists of the `Error:` messages TAEF logged for it (for example, failed `VERIFY_*`
macros, exceptions, crashes, failures of its setup methods, including the errors the setup methods logged before) and
`TestFailed:`/`TestBlocked:`/`TestSkipped:`/`TestNotRun:` comments (for example, from
`Log::Result(TestResults::Failed, L"...")`), numbered `#1 - ...` if there are several; the stack trace contains a clickable
entry for each error with source information. An error message containing line breaks is shown completely if TAEF logged
source information for it (for example, failed `VERIFY_*` macros); otherwise only its first line is part of the error
message. The complete log output of the test (for example, `Log::Comment` and `Verify:` lines) is shown as its output.
Option `PrintTestOutput` additionally prints the complete console output of `TE.exe` to the *Tests* output window.

<a name="cleanup_failures"></a>**Failures of cleanup methods do not change test results.** `TE.exe` reports the result of
a test before its cleanup methods (`TEST_METHOD_CLEANUP`, `TEST_CLASS_CLEANUP`, `MODULE_CLEANUP`) run, and does not change
it if a cleanup method returns `false` or logs errors. The adapter logs such failures as warnings to the *Tests* output
window (and in the summary of warnings), not in the test's own results. So a check which must fail a test, for example a
check for memory leaks, belongs in the test method itself (see the `LeakCheckTests` sample in [SampleTests](SampleTests)),
not only in a cleanup method.

### <a name="debugging"></a>Debug tests

You can debug tests from Test Explorer (*Debug*). The adapter then runs `TE.exe` with `/inproc` (so the test code runs in
the debugged `TE.exe` process) and `/disableTimeouts`, and with `/breakOnError` if option `BreakOnError` is enabled (the
debugger then breaks as soon as a test logs an error). As the tests run in process, test timeouts and isolation levels
(`IsolationLevel`) do not apply while debugging, and test repetitions (`NrOfTestRepetitions`) repeat each test in a row within
the `TE.exe` process (see [Test execution](#execution)).

Option `DebuggerKind` selects how the debugger is attached:

* `Native` (default) and `ManagedAndNative`: the adapter starts `TE.exe` and attaches the Visual Studio debugger (native, or
  native and managed code). These engines need the extension (VSIX); with the NuGet package, debugging with them fails with
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
  * if a test crashes `TE.exe`, the WTT log is not completed; the adapter then reads the incomplete log `TE.exe` wrote
    while the tests were running (`<log file>.trace`): the tests which finished before the crash have their results, the
    test that was running is reported as crashed, and the remaining tests are reported as in [Crashes](#limitations).

  Use `Native` or `ManagedAndNative` (with the extension) to avoid these restrictions.

If the debugger cannot be attached to `TE.exe` (`Native`, `ManagedAndNative`), `TE.exe` is terminated before it runs any
test, an error is logged, and the tests are reported according to option `MissingTestsReportMode`.

## <a name="configure"></a>Configure

### <a name="global_settings"></a>Visual Studio options

The options of the adapter are in *Tools > Options > Test Adapter for TAEF*, with the pages *General*, *Test Discovery*,
*Test Execution* and *TAEF* (only available if the extension is installed). These are the *global* settings.

In Visual Studio 2026 the pages are classic options pages: open *Tools > Options* and use the link to the legacy Options
dialog (or search for TAEF).

### <a name="toolbar"></a>Toolbar

The toolbar *Test Adapter for TAEF* (*View > Toolbars*) has the switches *Run tests in process (/inproc)*, *Break on error*,
*Parallel test execution* and *Print test output*; they change (and show) the corresponding global options.

### <a name="settings_files"></a>Settings files

* <a name="solution_settings"></a>**Solution settings file**: a file `<SolutionName>.taef.runsettings` next to the solution
  file `<SolutionName>.sln` (for example, `Foo.taef.runsettings` for `Foo.sln`); you can share it via source control. It is
  used by the extension only (not with the NuGet package or `vstest.console.exe`).
* **User settings files**: `.runsettings` files selected in Visual Studio (*Test > Configure Run Settings*) or passed to
  `vstest.console.exe` with `/Settings:<file>`.
* Environment variable `TAEF_ADAPTER_FALLBACK_SETTINGS`: path of a settings file which is used *only* if the adapter does not
  receive any settings from the test platform.

Solution and user settings files have the same format: a `<TaefTestAdapterSettings>` node within the `<RunSettings>` node
contains the solution settings and (optionally) project settings. The settings of a `<Settings ProjectRegex="...">` node apply
to the test DLLs whose full path matches the regular expression (the attribute `ProjectRegex` is required there). The file
[AllTestSettings.taef.runsettings](TaefTestAdapter/Resources/AllTestSettings.taef.runsettings) lists all settings (except
`SkipOriginCheck`) with their default values, the schema is
[TaefTestAdapterSettings.xsd](TaefTestAdapter/TestAdapter/TaefTestAdapterSettings.xsd).

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

If the `<TaefTestAdapterSettings>` node of a file contains an unknown element, an invalid value, or a `<Settings>` element of
`<ProjectSettings>` without `ProjectRegex`, the whole node is ignored and an error naming the problem is logged:

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

So for a test DLL, the settings are taken from (in decreasing priority): matching project settings of the user settings file,
matching project settings of the solution settings file, solution settings of the user settings file, solution settings of the
solution settings file, global settings. It is thus usually best to only put the settings that differ from the defaults into
settings files.

### <a name="placeholders"></a>Placeholders

| Placeholder | Value | Available in |
|---|---|---|
| `$(TestDll)` | full path of the test DLL | `TeExecutable`, `AdditionalPdbs`, `WorkingDir`, `PathExtension`, `EnvironmentVariables`, `AdditionalTestExecutionParam` |
| `$(TestDllDir)` | folder of the test DLL | as `$(TestDll)` |
| `$(SolutionDir)`, `$(PlatformName)`, `$(ConfigurationName)` | solution folder, platform and configuration of the solution | the settings listed for `$(TestDll)`, and with restrictions `BatchForTestSetup`/`BatchForTestTeardown` (see [below](#batch_placeholders) and [availability](#feature_availability)) |
| `$(TestDir)` | a temporary folder the tests may use (one per thread, deleted after the test run) | `WorkingDir`, `EnvironmentVariables`, `AdditionalTestExecutionParam` (test execution only), `BatchForTestSetup`/`BatchForTestTeardown` |
| `$(ThreadId)` | id of the thread running the tests | as `$(TestDir)` |
| `%NAME%` | value of environment variable `NAME` | all settings listed in this table |
| `$(<key>)` | value from a [settings helper file](#settings_helper_files) of the test DLL | the settings listed for `$(TestDll)` (not the batch files) |

During test discovery, `$(TestDir)` and `$(ThreadId)` are removed.

<a name="batch_placeholders"></a>The batch files are not related to a single test DLL, so [settings helper files](#settings_helper_files) are
not used for them: `$(PlatformName)` and `$(ConfigurationName)` only have a value there inside Visual Studio with the
extension, `$(SolutionDir)` only if Visual Studio or the test platform provides the solution folder (for example, with
`<RunConfiguration><SolutionDirectory>...</SolutionDirectory></RunConfiguration>` in the `.runsettings` file). Otherwise
they are replaced by an empty string; use absolute paths or environment variables (`%NAME%`) in the batch file settings
in that case.

### <a name="settings_helper_files"></a>Settings helper files

The adapter has no access to Visual Studio project settings, and when running outside of Visual Studio (NuGet package,
`vstest.console.exe`) it does not know the solution folder, platform or configuration. A *settings helper file* provides such
values: a file `<test dll>.taef_settings_helper` next to the test DLL containing one line of `Key=Value` pairs separated by
`::TAEF::`. Each key can be used as placeholder `$(Key)` in the settings, and the keys `SolutionDir`, `PlatformName` and
`ConfigurationName` provide the corresponding placeholders. Create such a file with a post-build event:

```
echo SolutionDir=$(SolutionDir)::TAEF::PlatformName=$(PlatformName)::TAEF::ConfigurationName=$(ConfigurationName)::TAEF::TheTarget=$(TargetFileName) > "$(TargetPath).taef_settings_helper"
```

Keep the quotes around the file name: project paths may contain spaces, and without the quotes the helper file would not be
written (without any error in the build).

With this file, you can use `$(TheTarget)` in the settings (for example,
`<AdditionalTestExecutionParam>/p:"Target=$(TheTarget)"</AdditionalTestExecutionParam>`). Make sure your version control
ignores these files.

### <a name="parallelization"></a>Parallel test execution

Tests are run sequentially by default. With `ParallelTestExecution`, the tests are distributed to `MaxNrOfThreads` threads,
each of which runs its tests with its own `TE.exe` invocations. The adapter remembers the durations of the tests in files
`<test dll>.taef.testdurations` to distribute the tests evenly in later runs (make sure your version control ignores these
files). The *Run tests in parallel* switch of Test Explorer and `vstest.console.exe /Parallel` have no effect on the
tests of a test DLL.

Module and class setup/cleanup methods run in every `TE.exe` invocation that runs tests of the module or class.

### <a name="setup_teardown"></a>Test setup and teardown

Batch files configured with `BatchForTestSetup`/`BatchForTestTeardown` are executed before/after the tests (once per thread);
placeholders such as `$(TestDir)` and `$(ThreadId)` let them prepare resources for the tests. They run in the solution
folder (if known). A missing batch file is logged as error, a non-zero exit code as warning; the tests are run anyway.
Processes started by a batch file (for example, a server started with `start`) keep running after the batch file has
finished; if they keep its output open, the adapter continues after at most 3 seconds with a warning (see
[Test execution](#execution)). TAEF's own `MODULE_SETUP`, `TEST_CLASS_SETUP` and `TEST_METHOD_SETUP` fixtures are of
course available as well.

## <a name="reference"></a>Reference

### <a name="settings_reference"></a>Settings

| Setting (XML element) | Options page: option | Default | Description |
|---|---|---|---|
| `PrintTestOutput` | General: *Print test output* | `false` | Print the complete console output of `TE.exe` to the *Tests* output window (not with parallel execution, and not while debugging with `VsTestFramework`). `printf`/`std::cout` output of the tests is only part of it when running in process. |
| `OutputMode` | General: *Output mode* | `Info` | Amount of output of the adapter: `None`, `Info`, `Debug`, `Verbose`. |
| `TimestampMode` | General: *Timestamp output* | `Automatic` | Timestamps in the adapter's output: `Automatic`, `PrintTimestamp`, `DoNotPrintTimestamp` (Visual Studio 2022 and later add timestamps themselves, so `Automatic` never prints them). |
| `SeverityMode` | General: *Print severity* | `Automatic` | Severity in the adapter's output: `Automatic`, `PrintSeverity`, `DoNotPrintSeverity`. |
| `SummaryMode` | General: *Print summary* | `WarningOrError` | Print a summary of warnings and errors after discovery/execution: `Never`, `Error`, `WarningOrError`. |
| `PrefixOutputWithTaef` | General: *Prefix output with [TAEF]* | `false` | Prefix the adapter's output with `[TAEF]`. |
| `SkipOriginCheck` | General: *Skip check of file origin* | `false` | Do not check whether test DLLs originate from this computer (a security check against downloaded binaries). With the extension (VSIX), it can only be set in the Visual Studio options; values in settings files are ignored. With the NuGet package only, with `vstest.console.exe` or with `TAEF_ADAPTER_FALLBACK_SETTINGS`, it is read from the settings file like any other setting (a warning is logged during test discovery if it is `true`), so only use settings files you trust. |
| `TestDiscoveryRegex` | Test Discovery: *Regex for test discovery* | *(empty)* | If set, exactly the DLLs whose full path matches this regex are treated as TAEF test DLLs (instead of checking the DLLs for TAEF metadata); DLLs with an indicator file `<dll>.is_taef_test` always are. |
| `TestDiscoveryTimeoutInSeconds` | Test Discovery: *Test discovery timeout in s* | `30` | Timeout of `TE.exe /listProperties` for one DLL; `0`: no timeout. |
| `ParseSymbolInformation` | Test Discovery: *Parse symbol information* | `true` | Read the source locations of the tests from the PDBs. |
| `TraitsRegexesBefore`, `TraitsRegexesAfter` | Test Discovery: *Before test discovery*, *After test discovery* | *(empty)* | Assign traits by regexes on the test names, see [Traits](#traits). |
| `AdditionalPdbs` | Test Execution: *Additional PDBs* | *(empty)* | Additional PDB files (patterns separated by `;`, `*` and `?` allowed in the file part) to search for source locations, for example `$(TestDllDir)\pdbs\*.pdb`. |
| `WorkingDir` | Test Execution: *Working directory* | `$(TestDllDir)` | Working directory of `TE.exe` (and the tests) for discovery and execution. |
| `PathExtension` | Test Execution: *PATH extension* | *(empty)* | Folders added in front of the `PATH` of `TE.exe` (and thus of the test host processes) for discovery and execution, for example to find dependencies of the test DLLs. |
| `EnvironmentVariables` | Test Execution: *Environment variables* | *(empty)* | Environment variables for `TE.exe` and the tests, `Name=Value` pairs separated by `//\|\|//`. If `PathExtension` is set, a `PATH` variable given here (in any letter case) is ignored with a warning. |
| `AdditionalTestExecutionParam` | Test Execution: *Additional TE.exe arguments* | *(empty)* | Additional `TE.exe` arguments for discovery and execution, inserted before the adapter's switches, for example `/p:"Name=Value"` (runtime parameters) or `/runas:<context>`. A `/select` or `/name` given here restricts test discovery and is combined with the adapter's selection, see [Test execution](#execution). |
| `BatchForTestSetup`, `BatchForTestTeardown` | Test Execution: *Test setup batch file*, *Test teardown batch file* | *(empty)* | Batch files executed before/after the test run (once per thread in parallel execution). Settings helper files are not used for them, see [Placeholders](#placeholders). |
| `KillProcessesOnCancel` | Test Execution: *Kill processes on cancel* | `false` | Kill `TE.exe`, its test host processes and all processes started by the tests when a test run is canceled (see [Test execution](#execution)); cleanup methods of the tests are then not run. |
| `DebuggerKind` | Test Execution: *Debugger engine* | `Native` | How tests are debugged: `VsTestFramework`, `Native`, `ManagedAndNative`; without the extension (NuGet package), only `VsTestFramework` works, with restrictions (see [Debug tests](#debugging)). |
| `ParallelTestExecution` | Test Execution: *Parallel test execution* | `false` | Run tests in parallel threads (see [Parallel test execution](#parallelization)). |
| `MaxNrOfThreads` | Test Execution: *Maximum number of threads* | `0` | Maximum number of threads for parallel execution; `0`: number of processors. |
| `MissingTestsReportMode` | Test Execution: *Behavior for missing test results* | `ReportAsNotFound` | How tests without result are reported: `DoNotReport`, `ReportAsNotFound`, `ReportAsSkipped`, `ReportAsFailed`. |
| `TeExecutable` | TAEF: *TE.exe path* | *(empty: automatic)* | Path of `TE.exe`, or of a folder containing `<arch>\TE.exe` or `TE.exe`; placeholders allowed (see [TE.exe selection](#te_selection)). |
| `RunIgnoredTests` | TAEF: *Also run ignored tests* | `false` | Also run the tests which TAEF ignores, that is, tests with property `Ignore` = `true` or `1` (`/runIgnoredTests`). |
| `BreakOnError` | TAEF: *Break on error* | `false` | While debugging, break into the debugger as soon as a test logs an error (`/breakOnError`). |
| `NrOfTestRepetitions` | TAEF: *Number of test repetitions* | `1` | Run each test *n* times (`/testmode:Loop /Loop:<n> /LoopTest:1`: *n* loops over all tests, each in new test host processes); minimum 1. When running in process or debugging, each test is repeated *n* times in a row within the same process instead (`/testmode:Loop /Loop:1 /LoopTest:<n>`; module and class fixtures run only once). |
| `TestTimeout` | TAEF: *Test timeout* | *(empty)* | Timeout of each test and fixture (`/testTimeout:<value>`), overriding the `TestTimeout` metadata of the tests; format `[Day.]Hour[:Minute[:Second[.FractionalSeconds]]]` (hours 0-23, minutes and seconds 0-59), for example `0:05` (5 minutes) or `0:0:30` (30 seconds). Not effective when running in process or debugging (the switch is then not passed). |
| `IsolationLevel` | TAEF: *Isolation level* | `Default` | Minimum isolation of the tests in test host processes (`/isolationLevel:<level>`; the `IsolationLevel` metadata of a test wins if it requests a tighter isolation): `Default` (switch not passed; TAEF uses one test host process per test DLL), `Test`, `Method` (a test host process per test), `Class` (per class), `Module` (per test DLL). Not effective when running in process or debugging (the switch is then not passed). |
| `RunInProcess` | TAEF: *Run tests in process* | `false` | Run the tests inside `TE.exe` (`/inproc`) instead of a separate test host process; makes `printf`/`std::cout` output of the tests visible, but a crash aborts the remaining tests of the `TE.exe` run, `TestTimeout` and `IsolationLevel` have no effect, and repetitions (`NrOfTestRepetitions`) repeat each test within the same process. Always used while debugging. |

The XML settings `DebuggingNamedPipeId`, `SolutionDir`, `PlatformName` and `ConfigurationName` are used internally.

### <a name="test_names"></a>Test names

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

The display name of a test is its TAEF name. Its fully qualified name, which Test Explorer uses for its
*Namespace > Class* hierarchy and which is used by the `FullyQualifiedName` test case filter, is the TAEF name with the `::`
that separate namespaces, classes and methods replaced by `.`; `::` inside template arguments `<...>` and inside data row
names are kept. Examples: `Contoso.Tests.MathTests.Rounding#metadataSet0`, `Contoso.Tests.MathTests#metadataSet0.Addition`,
`Contoso.Tests.MathTests.Parse#a::b` (row `a::b`).

Since row names may contain `::`, some names of data-driven tests are ambiguous. A test of a data-driven class with a
*named* table row, in a namespace (`Contoso::MathTests#Row::Addition`), cannot be told apart from the row `Row::Addition`
of a data-driven method `Contoso::MathTests`; it is shown as the latter (method `MathTests#Row::Addition` of class
`Contoso`). The adapter does not provide an explicit hierarchy to Test Explorer, which therefore derives the hierarchy
from the fully qualified name; so a `.` in a row name (for example, a row named `1.5`) can move the test to an unexpected
place in the tree.

### <a name="te_selection"></a>TE.exe selection

For each test DLL, the adapter determines its architecture `<arch>` (`x86`, `x64` or `arm64`; `arm` for 32-bit ARM) from
the machine type in its PE header (if it cannot be determined, the architecture of Windows is assumed) and uses

1. the value of option `TeExecutable` (placeholders are replaced), if set: if it is a file, that file; if it is a folder,
   `<folder>\<arch>\TE.exe` or, if that does not exist, `<folder>\TE.exe`;
2. otherwise `<Windows Kits root>\Testing\Runtimes\TAEF\<arch>\TE.exe`, where the Windows Kits root is read from the registry
   (`HKLM\SOFTWARE\Microsoft\Windows Kits\Installed Roots`, value `KitsRoot10`, 64-bit and 32-bit view); the roots
   `%ProgramFiles(x86)%\Windows Kits\10\` and `%ProgramFiles%\Windows Kits\10\` are tried as well;
3. otherwise `TE.exe` from the `PATH` (of the process running the adapter).

If `TeExecutable` is set but does not lead to a `TE.exe`, a warning is logged and `TE.exe` is searched as in steps 2 and 3.
If no `TE.exe` is found, an error is logged and the DLL is ignored. The `TE.exe` in use is logged with `OutputMode`
`Debug`.

ARM64EC test DLLs have the machine type of x64 in their PE header, so they are run with the x64 `TE.exe` (on ARM64 Windows,
it runs emulated and can load ARM64EC code; an arm64 `TE.exe` cannot). ARM64X DLLs have the arm64 machine type and are run
with the arm64 `TE.exe`. If `TeExecutable` is a folder, it must therefore contain `x64\TE.exe` (or `TE.exe`) for ARM64EC
test DLLs.

### <a name="feature_availability"></a>Feature availability

| Feature | VS with VSIX | VS with NuGet package | vstest.console.exe |
|---|:---:|:---:|:---:|
| Test discovery and execution | yes | yes | yes |
| Test debugging | yes | only with `DebuggerKind` `VsTestFramework`<sup>2</sup> | - |
| Configuration: VS options, toolbar | yes | no | - |
| Configuration: solution settings file | yes | no | no |
| Configuration: user settings file | yes (*Test > Configure Run Settings*) | yes | yes (`/Settings`) |
| Placeholder `$(SolutionDir)` | yes | with [helper files](#settings_helper_files)<sup>1, 3</sup> | with helper files<sup>3</sup> |
| Placeholders `$(PlatformName)`, `$(ConfigurationName)` | yes | with helper files<sup>3</sup> | with helper files<sup>3</sup> |
| Placeholders `$(TestDll)`, `$(TestDllDir)`, `$(TestDir)`, `$(ThreadId)`, environment variables, helper file keys | yes | yes | yes |
| Project and item templates (with the wizard for C++ names) | yes | no | - |

<sup>1</sup> During test execution, `$(SolutionDir)` is also available without helper files.  
<sup>2</sup> Set in a `.runsettings` file; the default engines `Native` and `ManagedAndNative` need the extension. See
[Debug tests](#debugging) for the restrictions of `VsTestFramework`.  
<sup>3</sup> Not in `BatchForTestSetup`/`BatchForTestTeardown`, for which helper files are not used (see
[Placeholders](#batch_placeholders)).

### <a name="limitations"></a>Limitations

* **Console output of tests**: by default, TAEF runs the tests in a separate test host process whose console output is lost,
  so only output of the WEX logging API (`Log::Comment`, `Log::Error`, ...) and of the `VERIFY_*` macros is available.
  `printf`/`std::cout` output of the tests is only visible when running in process (`RunInProcess`, or while debugging
  with `DebuggerKind` `Native` or `ManagedAndNative`; not with `VsTestFramework`, see [Debug tests](#debugging)).
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
  `TE.exe` run (see [Debug tests](#debugging)).
* **Cleanup methods** cannot fail tests: failures of `TEST_METHOD_CLEANUP`, `TEST_CLASS_CLEANUP` and `MODULE_CLEANUP` are
  logged as warnings, see [Test results](#cleanup_failures).
* **Error dialogs** of the Debug CRT (`abort()`, `assert`, `_ASSERTE`) or of Windows Error Reporting stop a test run until
  they are closed, see [Troubleshoot](#crt_dialogs).
* <a name="taef_own_adapter"></a>**TAEF's own test adapter**: TAEF ships its own VSTest adapter `TE.TestAdapter.dll` (for
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
* **Long command lines** are split: if the selection of tests of a DLL does not fit into one command line, the tests are run
  with several `TE.exe` invocations; module and class fixtures then run once per invocation.
* **Selection**: only one `/select` (or `/name`) query is used by `TE.exe`. A `/select` or `/name` in
  `AdditionalTestExecutionParam` restricts the tests shown in Test Explorer; when a subset of the tests of a DLL is run,
  the adapter combines it with its own selection (`/select:"(<your query>) and (<adapter's query>)"`). Quotes must directly
  follow the colon of a switch: `/p:"Name=Value with spaces"`, not `"/p:Name=Value with spaces"`.
* **Similar test names**: `TE.exe` compares names case-insensitively, and names containing `*`, `?`, `"` or control
  characters can only be selected by patterns (see [Test execution](#execution)). So when some tests of a DLL are run,
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
  may be missing or wrong (a warning is logged, see [Test discovery](#discovery)).
* **Timeouts** (option `TestTimeout` and the `TestTimeout` metadata of tests) and **isolation levels** (`IsolationLevel`)
  are not effective when running in process or while debugging: `TE.exe` ignores timeouts with `/inproc` and cannot start
  the further test host processes that isolation levels need. **Test repetitions** (`NrOfTestRepetitions`) then repeat
  each test in a row within the same process, so module and class fixtures are not run for each repetition.
* **Test Explorer hierarchy**: the hierarchy is derived from the fully qualified names, so data rows with `.` in their names
  and tests of data-driven classes with named rows in a namespace may be shown at unexpected places, see
  [Test names](#test_names).
* **Architecture**: each test DLL is run with the `TE.exe` of its own architecture (required for `/inproc`); ARM64EC test
  DLLs are run with the x64 `TE.exe` (see [TE.exe selection](#te_selection)).
* Only native (C++) TAEF test DLLs are supported.

## <a name="vstest_console"></a>Run tests from the command line

```
vstest.console.exe MyTests_taef.dll OtherTests_taef.dll /TestAdapterPath:"<folder with TaefTestAdapter.TestAdapter.dll>" /Settings:My.runsettings
```

The adapter folder is, for example, `out\binaries\TaefTestAdapter\Release\TestAdapter` of a [build](#building), the
`build\_common` folder of the NuGet package, or the folder of the extracted VSIX. `vstest.console.exe` does not use the
solution settings file; pass all settings with `/Settings` (and provide solution related placeholders with
[settings helper files](#settings_helper_files)). Helper files are not used for the setup/teardown batch files: there,
`$(PlatformName)` and `$(ConfigurationName)` are empty, and `$(SolutionDir)` is only available if the settings file contains
`<RunConfiguration><SolutionDirectory>...</SolutionDirectory></RunConfiguration>`; otherwise use absolute paths or
environment variables in `BatchForTestSetup`/`BatchForTestTeardown` (see [Placeholders](#batch_placeholders)).

<a name="test_case_filters"></a>You can select tests with `/TestCaseFilter` (see the
[documentation](https://learn.microsoft.com/visualstudio/test/vstest-console-options)) using the properties
`FullyQualifiedName` (the dotted name, for example `FullyQualifiedName~Contoso.Tests.MathTests`), `DisplayName` (the TAEF
name), `Source`, `CodeFilePath`, `LineNumber`, `Id` and `ExecutorUri` (`executor://TestAdapterForTaef/v1`), and all traits
(trait names are not case sensitive), for example `/TestCaseFilter:"Priority=1"`. If the filter is invalid, an error is
logged and no tests are run.

## <a name="troubleshooting"></a>Troubleshoot

### General advice

* Make sure the tests can be listed and run on the command line, from the folder of the test DLL:
  `TE.exe <test dll> /listProperties` and `TE.exe <test dll>` (with the `TE.exe` of the DLL's architecture, for example
  `C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF\x64\TE.exe`).
* Set option `OutputMode` to `Debug` (and possibly `PrintTestOutput` to `true`) and check the *Tests* output window: it shows
  which DLLs are recognized as TAEF test DLLs, which `TE.exe` is used, and details of the `TE.exe` invocations.

### None or not all of my tests show up

* The DLL is not recognized as TAEF test DLL: create a file `<test dll>.is_taef_test` next to it, or configure
  `TestDiscoveryRegex` (for example, `.*_taef\.dll$`; in a settings file if you use the NuGet package).
* `TE.exe` was not found (an error in the *Tests* output window says so): install TAEF (see [Prerequisites](#prerequisites))
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

* TAEF's own adapter `TE.TestAdapter.dll` is used as well, see [TAEF's own test adapter](#taef_own_adapter).

### Tests fail with "Blocked: ... 0x8007007E" or "Failed to load"

* A dependency of the test DLL cannot be found. Copy it next to the test DLL or add its folder with `PathExtension`.

### Tests have no source locations

* The PDB of the test DLL is missing or does not contain the necessary information: link with *Generate Debug Info*
  `/DEBUG:FULL` and rebuild.
* The PDB is not next to the DLL or at the location recorded in the DLL: use `AdditionalPdbs`.
* `ParseSymbolInformation` is `false`.

### The output of `printf`/`std::cout` is missing

* See [Console output of tests](#limitations): use the WEX logging API or enable `RunInProcess` (while debugging, use
  `DebuggerKind` `Native` or `ManagedAndNative`).

### <a name="crt_dialogs"></a>A test run hangs, or a "Debug Error! abort() has been called" or assertion dialog appears

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
  the batch file). The adapter does not wait for such processes; they keep running (see [Test execution](#execution)).
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
  [TaefTestAdapterSettings.xsd](TaefTestAdapter/TestAdapter/TaefTestAdapterSettings.xsd).

### Test discovery is slow

* Configure `TestDiscoveryRegex` or create indicator files to avoid checking all DLLs, and/or disable
  `ParseSymbolInformation` for test DLLs where source locations are not needed (for example, via project settings).

### Test Explorer or the extension does not work after installation

* Visual Studio's MEF cache might be corrupted. Close Visual Studio and delete the folder
  `%LOCALAPPDATA%\Microsoft\VisualStudio\<version>\ComponentModelCache`.

## <a name="faq"></a>FAQ

### Can I use Test Adapter for TAEF side by side with other test adapters?

Yes. Its executor URI (`executor://TestAdapterForTaef/v1`), package and command GUIDs and its settings node
(`<TaefTestAdapterSettings>`) differ from those of other adapters, including the adapter that ships with TAEF
(`TE.TestAdapter.dll`, executor `executor://TaefTestAdapter`). If both TAEF adapters find the same test DLLs, the tests are
shown twice; see [TAEF's own test adapter](#taef_own_adapter).

### Which TE.exe is used?

The `TE.exe` of the Windows Kits that matches the architecture of the test DLL, unless you set `TeExecutable`; see
[TE.exe selection](#te_selection). With `OutputMode` `Debug`, the *Tests* output window shows the `TE.exe` in use.

### Why is the output of `printf`/`std::cout` missing?

By default, TAEF runs the tests in a separate test host process whose console output is lost. Use the WEX logging API
(`Log::Comment`) or enable `RunInProcess`; see [Limitations](#limitations).

### Are managed TAEF tests supported?

No. Only native (C++) TAEF test DLLs are supported; mixed-mode DLLs whose tests are native TAEF tests work as well.

### Should I use the Visual Studio extension or the NuGet package?

Use the extension on developer machines: it provides the options pages, the toolbar, the solution settings file and
debugging with the native debugger engines. Use the NuGet package (or `vstest.console.exe /TestAdapterPath`) on build
servers or where you cannot install the extension; see [Feature availability](#feature_availability).

### How do I make the adapter find a test DLL it does not recognize?

Create an empty file `<test dll>.is_taef_test` next to the DLL, or set `TestDiscoveryRegex`; see [Test discovery](#discovery).

## <a name="building"></a>Build from source

Requirements: Visual Studio 2026 or Visual Studio 2022 with the workloads *.NET desktop development*, *Desktop development
with C++* (it contains the DIA SDK) and *Visual Studio extension development*, and the TAEF development files (installed
with the WDK, see [Prerequisites](#prerequisites)) for the sample test DLLs, which the adapter's own tests use.

The repository contains paths of up to 150 characters (relative to its root; the golden files of the adapter's tests are
the longest), and Windows limits paths to 259 characters unless long paths are enabled. So clone the repository into a
folder whose path has at most 108 characters (for example, `C:\src\TAEF-Test-Adapter`), or enable long paths in git before
cloning (`git clone -c core.longpaths=true ...` or `git config --global core.longpaths true`; the Windows setting
`LongPathsEnabled` alone is not enough for git).

```powershell
.\build.ps1                      # prepare, restore and build everything (Debug and Release)
.\build.ps1 -PrepareOnly         # only copy/generate the DIA SDK files and restore NuGet packages
.\build.ps1 -Configuration Release -Test   # build Release (and all sample test DLLs) and run the unit tests
.\build.ps1 -SkipSamples         # build the adapter without the sample test DLLs (no TAEF needed)
Get-Help .\build.ps1 -Detailed   # all options
```

[build.ps1](build.ps1) locates Visual Studio with `vswhere`, copies `msdia140.dll` from the DIA SDK of Visual Studio into
`TaefTestAdapter\DiaResolver\x86` and `\x64` and generates the interop assembly `TaefTestAdapter\DiaResolver\dia2\dia2.dll`
(`midl` and `tlbimp` in a Visual Studio developer environment), restores the NuGet packages, builds
`SampleTests\SampleTests.sln` (Debug/Release, Win32/x64; see `-SampleConfiguration`, `-SamplePlatform` and `-SkipSamples`)
and `TaefTestAdapter\TaefTestAdapter.sln`, and optionally runs the tests with `vstest.console.exe`. By running it, you accept
the license terms of the DIA SDK (part of your Visual Studio license). [build_preparation.bat](build_preparation.bat) runs
the preparation steps only, after which you can build the solutions in Visual Studio.

When `TaefTestAdapter.sln` is built, its project `SampleTestsBuilder` first builds the sample test DLLs for all four
configurations (so building the adapter solution in Visual Studio requires TAEF), unless the MSBuild property
`SkipSampleTestsBuild` is `true`. `build.ps1` builds the samples in its own step and always passes
`/p:SkipSampleTestsBuild=true`, so `.\build.ps1 -SkipSamples` builds the adapter without TAEF. The adapter's tests which use
the sample test DLLs then need them from an earlier build (or from the folder given by the environment variable
`TAEF_ADAPTER_SAMPLES_DIR`).

Outputs:

* `out\binaries\TaefTestAdapter\<Configuration>\Packaging\`: the VSIX and the NuGet package,
* `out\binaries\TaefTestAdapter\<Configuration>\TestAdapter\`: the adapter (usable with `/TestAdapterPath`),
* `out\binaries\SampleTests\<Configuration>[-x64]\`: the sample test DLLs.

Other tools:

* `TaefTestAdapter\SetVersion.bat <major>.<minor>.<revision>.<build>` sets the version of all assemblies of the C# projects
  in `TaefTestAdapter` (adapter, tests and helper tools), the VSIX manifest, the NuGet package and the assembly version in
  the `WizardExtension` of the project and item templates (Visual Studio loads the templates' wizard from
  `TaefTestAdapter.VsPackage` by the full name of this assembly). The changes of each version are listed in
  [CHANGELOG.md](CHANGELOG.md), which the VSIX contains as its release notes.
* You can debug the VS package in the experimental instance of Visual Studio (`devenv /rootsuffix Exp`);
  [TaefTestAdapter.ChildProcessDbgSettings](TaefTestAdapter/TaefTestAdapter.ChildProcessDbgSettings) contains settings for the
  *Microsoft Child Process Debugging Power Tool* to automatically attach to the test host processes and `TE.exe`.
* [Tools\make_branding_assets.py](Tools/make_branding_assets.py) generates the logo, template and toolbar images;
  [Tools\Expand-Vsix.ps1](Tools/Expand-Vsix.ps1) extracts a VSIX for inspection, for example
  `Tools\Expand-Vsix.ps1 out\binaries\TaefTestAdapter\Release\Packaging\TaefTestAdapter.vsix` into
  `out\vsix\TaefTestAdapter\`.

## <a name="credits"></a>Credits

Test Adapter for TAEF is a modified version of [Google Test Adapter](https://github.com/csoltenborn/GoogleTestAdapter) by
Christian Soltenborn, Jonas Gefele and contributors, parts of which were contributed by Microsoft; see
[LICENSE.md](LICENSE.md) and [NOTICE](NOTICE).

Modified for TAEF support by Axel Rietschin and Claude Code.

Test Adapter for TAEF is an independent project; it is not affiliated with or endorsed by Google or Microsoft. Google is a
trademark of Google LLC. Microsoft, Visual Studio and Windows are trademarks of the Microsoft group of companies.

## <a name="license"></a>License

Test Adapter for TAEF is licensed under the [Apache License, Version 2.0](LICENSE.md); see [LICENSE.md](LICENSE.md) for the
notice of modification and [NOTICE](NOTICE) for attributions and third-party components.
