# Plan: C# (managed) TAEF test support - version 1.1

Status: **sized, not started** (2026-10-03). Nothing in the adapter has been changed for this yet. This folder holds
everything needed to resume:

- `Plan.md` (this file): scope, owner decisions, estimate, resume checklist, and the consolidated sizing report.
- [AuditDetails.md](AuditDetails.md): the raw findings of the six code audits (names, detection, symbols, test host,
  debugging, tooling) with per-item file lists, plus the critic's review. Where it disagrees with this file, this file wins.
- [Probe](Probe): the C# TAEF probe used to collect the facts below. `Source/ManagedTests.cs` holds tests of every
  interesting kind (fixtures, properties, ignored, lightweight and table data, async, data-driven class, nested class,
  global namespace). `Net48` and `Net8` are SDK-style projects that reference the WDK's TAEF runtime (x64), and `DiaProbe`
  tries VSTest's `DiaSession` on the built DLLs. Build them with `dotnet build` in each folder. The probe is not part
  of the solution or of build.ps1, and it does not follow the sample style rules below (it is not a sample).

## Scope of 1.1

- In scope: C# TAEF tests on .NET Framework 4.x (and netstandard2.0 test DLLs, which TAEF runs on .NET Framework),
  C# project and item templates, samples, docs, packaging changes, version 1.1.0.0.
- Deferred: C# TAEF tests targeting .NET 6/8/10 (needs `TAEF_CoreCLR` injection and the adapter loading in a .NET test
  host, see phase `netcore` below). Until then, document them as unsupported.

## Owner decisions

- **Namespaces:** use whatever gives a proper Test Explorer tree. There must be no `<unnamed namespace>`-style
  placeholder node; nested namespaces are fine. Plan: dotted namespaces derived from the project (`Contoso.Tests`), each
  segment a valid C# identifier; never the global namespace.
  - To verify in VS: does `Contoso.Tests` show as one node or as two nested nodes? If it shows as one, look at the
    hierarchy property so that the two namespace levels fill two tree levels.
- **Indentation:** C# samples, templates and the README show two namespace levels as nested blocks, each level indented,
  like the README's C++ sample. No file-scoped namespaces. 4 spaces, never tabs.
- **Traits** (proposal, not yet confirmed by the owner): report `[TestProperty]` and TAEF's shortcut attributes
  (`[Owner]`, `[Priority]`, `[Description]`) as traits, as for C++ metadata. Drop the properties TAEF derives from every
  other attribute: their name is a full type name ending in `Attribute` (e.g. `AsyncStateMachineAttribute`,
  `DebuggerStepThroughAttribute`). Still to verify: that `[Owner]` is listed as `Property[Owner]`.
- **VSIX prerequisites:** only the minimum needed to install the adapter, so keep `Microsoft.VisualStudio.Component.CoreEditor`
  and drop `Microsoft.VisualStudio.ComponentGroup.NativeDesktop.Core`.
  - To verify: whether Test Explorer needs its own component.
  - To verify: that VS hides the C++ templates when C++ is not installed.
- **AnyCPU:** the C# template defaults to AnyCPU (x64, x86 and ARM64 are also offered). The adapter must run AnyCPU DLLs
  with the TE.exe of the OS architecture. Today it picks the x86 TE.exe, because AnyCPU DLLs carry machine type I386.
  The x86 TE.exe runs AnyCPU tests as 64-bit normally but as 32-bit with `/inproc` (debugging).

## Estimate

| Who | 1.1 (netfx + tooling) | .NET 6/8/10 on top |
|---|---|---|
| Experienced engineer | 35-45 days (280-360 hours) | +16-24 days (130-190 hours) |
| Claude with parallel agents | 20-35 hours of wall-clock time | +10-15 hours |
| Owner (VS experimental-instance checks, final hands-on pass, signing, upload) | 4-8 hours | a few more hours |

## Resume checklist

1. Prototypes first. Each one gates part of the work.
   - **Test Explorer, with the VSIX installed** (VS 2022 and VS 2026): an SDK-style net48 C# TAEF project. Does the
     bare `TestContainer` project capability suffice for discovery, or is `Microsoft.NET.Test.Sdk` needed? How does
     the tree show `Contoso.Tests`?
   - **Debugging in VS Exp:** with TE.exe `/inproc`, which engine binds breakpoints in tests and fixtures
     (Managed+Native interop vs managed-only)? How does `/breakOnError` behave? Check AnyCPU too.
   - **System.Reflection.Metadata:** does VS 2022's testhost.net48 provide it (assembly version \>= 1.4.3.0)? If yes, it
     need not be shipped.
2. Implement the `netfx` items (F1-F15), keeping native behavior byte-identical. All 34 existing golden files must stay
   unchanged.
3. Implement the `tooling` items (T1-T4).
4. Run build.ps1 -Test, the VS Exp hands-on pass, then release 1.1.0.0. After any VSIX manifest edit, check that
   `AssemblyName="|VsPackage;AssemblyName|"` is still there.

## Facts established by running TE.exe (TAEF 10.104k from the WDK)

- `TE.exe /listProperties` lists C# DLLs (net48 and net8) in about 200 ms, without running test code. Each DLL is
  marked `Property[TaefTestType] =  Managed` (native DLLs: `Native`). Names use `.` throughout:
  - `Contoso.Managed.Tests.BasicTests.Passing`
  - `...LightweightData#metadataSet0`
  - `...TableData#First` (named row) and `...TableData#1` (unnamed row)
  - `Contoso.Managed.Tests.DataDrivenClass#metadataSet0.InDataClass`
  - `GlobalNamespaceTests.InGlobal`
- Nested test classes are listed by their simple name (`Nested.InNested`) and then Blocked. Every attribute shows up as
  a `Property[...]`.
- The run output is the same as for native DLLs (`StartGroup:` / `EndGroup: <name> [Passed|Failed|Blocked]`).
  - Verify failures carry `[File: ..., Function: ..., Line: ...]`.
  - Exceptions are followed by .NET stack frames, with `in <file>:line <n>` on CoreCLR only.
- `/select:"@Name='Contoso.Managed.Tests.DataDrivenClass*' or @Name='...TableData#1'"` and `/inproc` work.
- TAEF runs C# tests on .NET Framework 4.x by default. A .NET 8 DLL lists fine, but all its tests are Blocked unless
  `TAEF_CoreCLR` points at e.g. `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.31`. With it set, they run.
- VSTest's `DiaSession` gives correct locations for portable PDBs on .NET Framework, except for async methods. It fails
  on full Windows PDBs without msdia, and on net8 DLLs from a .NET Framework process. Decision: use our own
  System.Reflection.Metadata-based reader, with msdia lookups by metadata token for Windows PDBs.
- The WDK's TAEF runtime contains its own VSTest adapter, `TE.TestAdapter.dll`. Its executor URI
  (`executor://TaefTestAdapter`) and settings name (`TaefAdapterSettings`) differ from ours.

## Sizing report


### How TAEF discovers C# tests

- **Authoring.** Tests use `[TestClass]`/`[TestMethod]`/`[TestProperty]`/`[DataSource]`/`[Ignore]`/fixture attributes from `WEX.TestExecution.Markup` in TE.Managed.dll (10.0.0.0). The compiled DLL is IL-only and has an AssemblyRef to `TE.Managed`. It has no native `testdata` section and no native imports of TAEF DLLs. WDK reference assemblies are in `Testing\Development\lib\<arch>\ref4.5`, byte-identical across arches. Correction to established fact 1: that folder **does** include wex.logger.interop.dll.
- **Listing.** TE.exe reflects over the metadata. `/listProperties` has the same layout as for native DLLs (indentation, `Setup:`, `Property[]`, `Data[]`, `#error [Blocked]`). It takes about 200 ms. The module scope prints `Property[TaefTestType] =  Managed` before the first class. Discovery needs no CoreCLR, even for net8 DLLs.
- **Naming.** `.` is used for every separator: `Ns.Class.Method`, `GlobalClass.Method`. Nested classes are flattened to their simple name and then Blocked. Generic classes keep the `` `1 `` arity and are Blocked. Overloads fail with AmbiguousMatchException. Inherited `[TestMethod]`s are listed under the derived class.
- **Data rows.** Method rows look like `#metadataSetN`, `#<index>` or `#<row name>`. Row names may contain `.`, `::` and spaces. Data-driven class rows look like `Ns.Class#metadataSet0.Method`.
- **Properties.** Every custom attribute becomes a `Property[...]`, including compiler-generated ones (`AsyncStateMachineAttribute`, `DebuggerStepThroughAttribute`).
- **CLR selection.**
  - The default host is .NET Framework 4.x; netstandard2.0 DLLs run there.
  - .NET 6/8/10 DLLs list fine but every test is Blocked unless `TAEF_CoreCLR` points at a `Microsoft.NETCore.App\<ver>` folder. Roll-forward from 8 to 10 works.
  - TAEF builds the trusted platform assembly list from that single folder, so WinForms/WPF/ASP.NET dependencies fail. This is a TAEF limitation.
  - `CoreClrProfile` metadata and `/coreClrProfileSearchPath` are alternatives; their format is undocumented.
- **Execution.** Output uses the same `StartGroup`/`EndGroup` format. `/select` and `/inproc` work with dotted names.
- **The adapter today skips C# DLLs silently.** `PeParser.CheckTaefTestDll` returns "no 'testdata' section". If forced through with an indicator file, the listing parser produces 0 tests: probe output was "35 unexpected lines".
- **TAEF ships its own VSTest adapter.** It is `TE.TestAdapter.dll` (netstandard2.0, `executor://TaefTestAdapter`) and handles Native and Managed. If it lands in a .NET test output folder, the dotnet testhost loads it next to ours and tests are discovered twice.

### Gap analysis

| Area | Today | Needed for C# | Evidence |
|---|---|---|---|
| Detection | Native `testdata` section or TAEF imports only | CLI header + `TE.Managed` AssemblyRef | DiaResolver/PeParser.cs:332-364 |
| TE.exe arch | PE machine → AnyCPU uses x86 TE.exe | AnyCPU → OS arch. x86 TE.exe runs AnyCPU 64-bit out-of-proc but 32-bit with `/inproc`, so run and debug bitness differ | Core/TaefLocator.cs:101-135; probe run_c9d4 |
| Listing parser | Requires `<class>::` | Switch to `.` when module `TaefTestType=Managed` | Core/TestCases/StreamingListPropertiesParser.cs:307-310 |
| Name model / VS FQN | `::`-based. Managed class becomes `""`; `#Weird::Row` is corrupted | Managed split using the class TE.exe listed; FQN unchanged | Core/Helpers/TaefNames.cs:101-118,154-191; TestAdapter/DataConversionExtensions.cs:87 |
| `/select` | Class terms built with `::` | Dotted class term (probe: selects exactly 11/11) | Core/Runners/CommandLineGenerator.cs:238,316-335; Core/TaefConstants.cs:197-206 |
| Fixture/op failures | Scope split on `::` | `.` scopes; bare operation names | Core/TestResults/StreamingTaefOutputParser.cs:646-721 |
| Exceptions | First line only, no stack | Parse `   at X[ in f:line N]` frames | Core/TestResults/ErrorMessageParser.cs:247,293-315 |
| Traits | All properties except a deny list | Filter CLR attribute pseudo-properties | Core/TestCases/TestCaseFactory.cs:121-125 |
| Source locations | msdia name/UDT lookups (C++ only) | System.Reflection.Metadata (SRM) + portable/embedded PDB; DIA by token for Windows PDBs; skip line 0xFEEFEE | Core/TestCases/TestCaseResolver.cs:81-128; DiaResolver/DiaResolver.cs:210-218 |
| Debugging | Global DebuggerKind, default Native, one engine GUID | Per-DLL upgrade to ManagedAndNative (netfx); engine list Native+CoreSystemClr (CoreCLR) | Core/Settings/SettingsWrapper.cs:381; VsPackage/Debugging/VsDebuggerAttacher.cs:90-107; Common/IDebuggerAttacherService.cs:14-17 |
| NuGet | `native0.0` / `lib\native` only | Managed TFM + props that copy the adapter to the output folder. Today restore fails with NU1202 | Packaging/VsPackage.nuspec:22-41 |
| .NET testhost | Not loadable: Zone (CAS), MEF `[Export]`, WCF | Zone.Identifier/MapUrlToZone; conditional MEF; WCF guard; NuGet delivery | Core/TaefDiscoverer.cs:239-251; TestAdapter/Settings/RunSettingsProvider.cs:16-17 |
| Packaging/metadata | Requires C++ workload; a test asserts C# is unsupported | Relax prerequisite; add tags | Packaging/source.extension.vsixmanifest:27; VsPackage.Tests.Unit/PackageMetadataTests.cs:40-41 |

### Work breakdown

The six audits overlapped heavily. After applying the critic's corrections:

- Samples and golden files were counted six times (about 10.5 days); one consolidated item is 5-6 days.
- Docs were counted three times; consolidated to 2 days.
- VS-7 duplicated RT-6/RT-7 and is dropped.
- RT-9 option B duplicated VS-3..6 and is folded into C2/C3.
- Detection falls from 3 days to about 1.5 days. It uses only the CLI header plus an AssemblyRef/#Strings check, so it has no SRM dependency.
- The CoreCLR debugging upper bound goes up to 3 days.

| ID | Work item | Phase | Days | Risk |
|---|---|---|---|---|
| F1 | Managed TaefNames model (class-aware split, identity FQN) | netfx | 1-1.5 | M |
| F2 | Listing parser managed mode + `TestCaseDescriptor.IsManaged` | netfx | 1 | L |
| F3 | TestCaseFactory: listed class, location key, trait filter (NAMES-3 + LOC-3 overlap) | netfx | 1-1.5 | M |
| F4 | `TestCaseTaefClassProperty` / VS property round trip = managed flag (also covers DBG-1 and RT-2 kind) | netfx | 1 | L |
| F5 | CommandLineGenerator/TaefConstants dotted class terms | netfx | 1-1.5 | L |
| F6 | StreamingTaefOutputParser managed scopes and operation failures | netfx | 1-2 | M |
| F7 | ErrorMessageParser/WttLogParser .NET stack frames | netfx | 1-1.5 | M |
| F8 | PeParser: data directory 14, CorFlags, TE.Managed ref; IsTaefTestDll kind; trust test | netfx | 1.5-2.5 | M |
| F9 | TaefLocator AnyCPU → OS arch (mandatory) | netfx | 1-1.5 | M |
| F10 | Locations: ManagedSymbolResolver (SRM), DIA FindFunctionByToken, 0xFEEFEE skip, resolver dispatch | netfx | 4-5 | L-M |
| F11 | Debug prototype in VS Exp (DBG-0) + auto Native→ManagedAndNative per managed DLL | netfx | 3-4 | H |
| F12 | NuGet props for net4x C# projects + C++ restore regression | netfx | 1-1.5 | M |
| F13 | One managed sample (x64/x86/AnyCPU, portable and full PDB), TE.exe captures, ~8 golden files, separate `AllManagedSampleTestDlls` | netfx | 5-6 | M |
| F14 | Consolidated settings plumbing (AnyCPU arch, trait filter): XSD/RunSettings/options page/goldens | netfx | 1-1.5 | L |
| F15 | Missing probes (critic): managed crashes, `/isolationLevel`, C++/CLI classification, PE/PDB fuzzing + module-initializer check, duration/parallel tests | netfx | 3-3.5 | M |
| | **netfx subtotal** | | **26.5-35.5** | |
| T1 | C# project/item templates, language-aware wizard, VSIX zips, CSharpProjectTemplateTests (after a 0.5 d TestContainer IDE check) | tooling | 5-5.5 | M |
| T2 | README/CHANGELOG/sample docs (consolidated) | tooling | 2 | L |
| T3 | Tags, VSIX prerequisite, PackageMetadataTests, version 1.1.0.0 | tooling | 1 | L |
| T4 | Settings-helper target, data-file copy, redistribution checklist | tooling | 0.5-1.5 | L |
| | **tooling subtotal** | | **8.5-10** | |
| C1 | VS Test Explorer hosting prototype (mandatory gate) | netcore | 1-2 | H |
| C2 | Zone replacement, conditional MEF export, WCF guard | netcore | 2 | M |
| C3 | Ship net48 binaries (0.5 d) **or** multi-target net48;net8.0-windows (4 d) | netcore | 0.5-4 | M |
| C4 | NuGet net8 payload | netcore | 0.75-1 | L |
| C5 | Target framework detection, DotNetRuntimeLocator, `TAEF_CoreCLR` injection + option, diagnostics | netcore | 5-6 | M |
| C6 | CoreCLR debug engine list (contract value + Guid[]) | netcore | 2-3 | H |
| C7 | net8 sample, golden files, E2E (dotnet test / vstest / Test Explorer) | netcore | 3-3.5 | M |
| C8 | net8 template variant | netcore | 1.5-2 | H |
| | **netcore subtotal** | | **16-24** | |
| O1 | ManagedType/ManagedMethod or Hierarchy (by string id) for dotted rows | optional | 1.5-2 | M |
| O2 | Warnings for unrunnable nested/generic tests; empty-listing retry | optional | 1.5 | L |
| O3 | Managed-only DebuggerKind; IFrameworkHandle2 attach without the VSIX | optional | 3 | M |
| O4 | ARM64 validation, cross-assembly base classes, `/_/` path mapping, "discover managed" opt-out, coverage note | optional | 4-4.5 | M |
| O5 | Microsoft.Testing.Platform (MTP) bridge | optional | 3 | H |
| | **optional subtotal** | | **13-14** | |
| | **Total (excluding optional)** | | **51-69.5** | |

### Recommended plan

1. **Prototypes first (about 4-5 days, in parallel):**
   - VS 2022 and VS 2026 Test Explorer with an SDK net48 C# project and the VSIX: does a bare `ProjectCapability TestContainer` suffice, or is Microsoft.NET.Test.Sdk needed?
   - DBG-0 engine matrix in VS Exp: engine choice, `/breakOnError`, AnyCPU, early fixture breakpoints.
   - Check that VS 2022 testhost.net48 provides SRM (assembly version ≥ 1.4.3.0).
2. **Release 1.1 = netfx + tooling (about 35-45 days).** Gate every change on the managed flag so native behaviour stays byte-identical, and byte-compare all 34 existing golden files. F9 (AnyCPU) and F11 (debug engine upgrade) must ship with discovery, otherwise C# breakpoints silently never bind.
3. **Source-location decision:** use the SRM reader for portable and embedded PDBs, and msdia `findSymbolByToken` for Windows PDBs.
   - Reject DiaSession: it loads the assembly, fails on net8, and returns sentinels for async methods.
   - Compile against SRM 1.6.0 with `Private=false`, isolate it in one class, and catch load failures so discovery still succeeds without locations. Ship SRM (about 0.7 MB) only if the VS 2022 check fails.
   - Detection stays dependency-free (F8).
4. **netcore is deferred** until C1 settles VSIX vs NuGet delivery and runsettings-forced netfx vs a .NET-loadable adapter. Two independent audits found that the dotnet testhost only loads `*TestAdapter.dll` from the source folder. Until then, document netstandard2.0 as supported and net8 as unsupported.

### Risks and open questions

- **Native regression across 16 `ScopeSeparator` references.** Mitigation: managed-only code paths and byte-compared golden files.
- **Older TAEF may not print `TaefTestType`.** Mitigation: use the PE CLR-header hint from F8; warn on dotted unexpected lines.
- **Test Explorer may not offer the DLL without Test.Sdk.** Mitigation: the IDE prototype; fall back to adding a Test.Sdk PackageReference in the template.
- **CoreSystemClr attach to a suspended TE.exe that custom-hosts CoreCLR may fail.** Mitigation: DBG-0; attach after `coreclr.dll` loads (+1-2 days).
- **`/breakOnError` under managed engines is undefined.** Mitigation: default to mixed mode; suppress the switch for managed-only engines.
- **Stale portable PDB gives wrong lines.** Mitigation: compare the CodeView GUID/stamp with the PDB id.
- **WCF enum skew between a NuGet adapter and an older VSIX.** Mitigation: only append enum values; turn the fault into an actionable message.
- **Behavior change for C++ users with helper libraries that reference TE.Managed** (one extra TE.exe listing each). Mitigation: O4 opt-out setting.
- **Open product decisions for the owner:**
  - Wizard namespace style: keep dots (`Contoso.UnitTests`) or apply the C++ rule (`ContosoUnitTests`)?
  - Which CLR attribute properties become traits?
  - Drop the C++ workload prerequisite from the VSIX?
  - Is AnyCPU offered in the C# template?

### Side findings

- `VerifyTestDllTrust` dereferences `Zone.CreateFromUrl` with no null check (Core/TaefDiscoverer.cs:245).
- `DiaResolver.ToSourceFileLocation` takes the first line without skipping hidden line 0xFEEFEE (DiaResolver/DiaResolver.cs:210-218).
- The x86 TE.exe twice printed an empty listing with exit code 0 right after a build. Low confidence; if real, it affects native DLLs too.
- README.md:686 and :854-856 state managed tests are unsupported. Correct today, but they must change with 1.1.