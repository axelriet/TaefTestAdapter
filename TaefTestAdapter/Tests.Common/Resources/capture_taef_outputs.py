# Captures TE.exe outputs of the sample test DLLs as test data for the adapter's parser tests
# (TaefTestAdapter\Tests.Common\Resources\TestData\TaefOutput\*.txt + README.md).
import os, re, shutil, subprocess, sys, time

# usage: python capture_taef_outputs.py (after building SampleTests.sln for Debug|Win32 and Debug|x64 into <repo>\out)
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
# replaces the enlistment root in the captured outputs, so that they do not depend on the machine
NEUTRAL_REPO = r"C:\src\TAEF-Test-Adapter"
BIN = os.path.join(REPO, r"out\binaries\SampleTests")
SAMPLES = os.path.join(REPO, "SampleTests")
TARGET = os.path.join(REPO, r"TaefTestAdapter\Tests.Common\Resources\TestData\TaefOutput")
TE_ROOT = r"C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF"
CAPTURE_DIR = os.path.join(REPO, r"out\TaefOutputCapture")
OUT_FORMAT = "/unicodeOutput:false /coloredConsoleOutput:false"
TEST_DIRECTORY = r"C:\Windows\Temp"


def te(arch):
    return os.path.join(TE_ROOT, arch, "TE.exe")


def neutralize(text):
    return re.sub(re.escape(REPO), lambda m: NEUTRAL_REPO, text, flags=re.IGNORECASE)


def rel(path):
    return os.path.relpath(path, REPO) if path.lower().startswith(REPO.lower()) else path


captures = []


def capture(name, arch, dll, args, cwd=None, env=None, description=""):
    cmd = '"%s" "%s" %s' % (te(arch), dll, args)
    e = dict(os.environ)
    e.pop("MYENVVAR", None)
    if env:
        e.update(env)
    cwd = cwd or os.path.dirname(dll)
    start = time.time()
    p = subprocess.run(cmd, cwd=cwd, env=e, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    secs = time.time() - start
    text = p.stdout.decode("utf-8")  # must be valid UTF-8
    text = neutralize(text.replace("\r\n", "\n"))
    with open(os.path.join(TARGET, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    stderr_text = neutralize(p.stderr.decode("utf-8").replace("\r\n", "\n"))
    if stderr_text:
        with open(os.path.join(TARGET, name.replace(".txt", ".stderr.txt")), "w", encoding="utf-8", newline="\n") as f:
            f.write(stderr_text)
    rc = p.returncode & 0xFFFFFFFF
    rc_text = str(rc) if rc < 0x01000000 else "0x%08X" % rc
    captures.append(dict(name=name, arch=arch, dll=rel(dll), args=args, cwd=rel(cwd), env=env or {}, rc=rc_text,
                         lines=text.count("\n"), description=description,
                         stderr=p.stderr.decode("utf-8", "replace").strip()))
    print("%-50s exit=%-10s lines=%5d %.1fs stderr=%d" % (name, rc_text, text.count("\n"), secs, len(p.stderr)))


def main():
    os.makedirs(TARGET, exist_ok=True)
    for f in os.listdir(TARGET):
        os.remove(os.path.join(TARGET, f))
    dbg64 = os.path.join(BIN, "Debug-x64")
    tests = os.path.join(dbg64, "Tests_taef.dll")
    sample_env = {"MYENVVAR": "MyValue"}
    sample_params = '/p:"TestDirectory=%s"' % TEST_DIRECTORY

    capture("Tests_taef.dll.listProperties.txt", "x64", tests, "/listProperties /runIgnoredTests " + OUT_FORMAT,
            description="Discovery as done by the adapter: all 141 tests incl. the 3 ignored ones, module/class/test properties, fixtures, data rows (Data[...]), the '#error [Blocked]' pseudo test of a missing data source.")
    capture("Tests_taef.dll.listPropertiesWithoutIgnored.txt", "x64", tests, "/listProperties " + OUT_FORMAT,
            description="As above without /runIgnoredTests: the 3 tests with Ignore=true are not listed (138 tests).")
    capture("Tests_taef.dll.list.txt", "x64", tests, "/list " + OUT_FORMAT,
            description="Plain /list output (names only, 138 tests).")
    capture("Tests_taef.dll.run.txt", "x64", tests, OUT_FORMAT + " " + sample_params, cwd=SAMPLES, env=sample_env,
            description="Complete run with the settings of SampleTests.taef.runsettings (runtime parameter, working directory SampleTests, MYENVVAR): 69 Passed, 62 Failed, 5 Blocked, 1 Skipped, 1 NotRun; module setup/cleanup output outside groups, fixture failures, Log::Comment/Warning output, error lines with [File: ..., Function: ..., Line: ...], 'Summary of ...' sections and 'Summary:' line.")
    capture("Tests_taef.dll.runInProcess.txt", "x64", tests, OUT_FORMAT + " " + sample_params + " /inproc", cwd=SAMPLES, env=sample_env,
            description="As Tests_taef.dll.run.txt with /inproc: printf/std::cout output of the tests is visible and may be glued to the following TE line (e.g. 'EndGroup:' or 'Error:' at the end of a line).")
    capture("Tests_taef.dll.runWithoutSettings.txt", "x64", tests, OUT_FORMAT,
            description="Complete run without the sample settings (working directory = DLL folder, no /p:, no MYENVVAR): TaefSamples::RuntimeParameterTests::TestDirectoryIsSet, TaefSamples::WorkingDir::IsSolutionDirectory and TaefSamples::EnvironmentVariable::IsSet fail (66 Passed, 65 Failed).")
    ignored = ["TaefSamples::ClassAndMethodProperties::IgnoredTest", "TaefSamples::IgnoredClass::IgnoredPassing", "TaefSamples::IgnoredClass::IgnoredFailing"]
    capture("Tests_taef.dll.runIgnored.txt", "x64", tests,
            OUT_FORMAT + " " + sample_params + " /runIgnoredTests /select:\"" + " or ".join("@Name='%s'" % n for n in ignored) + "\"",
            cwd=SAMPLES, env=sample_env,
            description="The 3 ignored tests run with /runIgnoredTests and /select (2 Passed, 1 Failed); TaefSamples::MissingDataSource::Test#error is run (Blocked) although it is not selected.")
    capture("Tests_taef.dll.runSelected.txt", "x64", tests,
            OUT_FORMAT + " " + sample_params + " /select:\"@Name='TaefSamples::TestMath::AddPasses' or @Name='TaefSamples::NamedRows::SpecialCharacters#with''quote' or @Name='TaefSamples::\u00dcmlaut\u00df::*' or (@Name='TaefSamples::ClassWithFixtures::*' and not @Name='TaefSamples::ClassWithFixtures::*::*')\"",
            cwd=SAMPLES, env=sample_env,
            description="Selection of single tests (incl. a row name with a doubled quote), a class via wildcard and a class term as generated by the adapter's CommandLineGenerator; TaefSamples::MissingDataSource::Test#error is run although not selected.")
    capture("CrashingTests_taef.dll.run.txt", "x64", os.path.join(dbg64, "CrashingTests_taef.dll"), OUT_FORMAT,
            description="Crashes out of process: TE.exe reports each crashing test (TheCrash 0xC0000005, TheAbort 0x00000003, TheStackOverflow 0xC00000FD) as Failed ('Error: TAEF: [HRESULT 0x800706BE] ...'), starts a new host (class setup runs again, 'TestSkipped: TAEF: The cleanup method ... will not be run ...' outside the groups) and continues: 4 Passed, 5 Failed.")
    capture("CrashingTests_taef.dll.runInProcess.txt", "x64", os.path.join(dbg64, "CrashingTests_taef.dll"), OUT_FORMAT + " /inproc",
            description="Crash with /inproc: TE.exe itself dies in TaefSamples::Crashing::TheCrash (no EndGroup, no Summary, exit code 0xC0000005); the 6 following tests are never started.")
    capture("LeakCheckTests_taef.dll.run.txt", "x64", os.path.join(dbg64, "LeakCheckTests_taef.dll"), OUT_FORMAT,
            description="Debug build: failing cleanup fixtures print their errors ('Error: memory leak detected ...', 'Error: TAEF: Cleanup fixture ... for the scope '<test>' failed.') AFTER the EndGroup of the affected test (1 Passed, 3 Failed).")
    capture("DllTests_taef.dll.run.txt", "x64", os.path.join(dbg64, "DllTests_taef.dll"), OUT_FORMAT,
            description="Small complete run: 1 Passed, 1 Failed.")
    capture("DllTests_taef.dll.runLoop.txt", "x64", os.path.join(dbg64, "DllTests_taef.dll"), OUT_FORMAT + " /testmode:Loop /Loop:2 /LoopTest:1",
            description="Repetitions (adapter option NrOfTestRepetitions=2): every test has 2 StartGroup/EndGroup frames.")
    # DLL whose dependency (DllProject.dll) can not be found
    if os.path.isdir(CAPTURE_DIR):
        shutil.rmtree(CAPTURE_DIR)
    os.makedirs(CAPTURE_DIR)
    for f in ("DllTests_taef.dll", "DllTests_taef.pdb"):
        shutil.copy(os.path.join(dbg64, f), CAPTURE_DIR)
    capture("DllTests_taef.dll.runWithoutDependency.txt", "x64", os.path.join(CAPTURE_DIR, "DllTests_taef.dll"), OUT_FORMAT,
            description="DllProject.dll can not be found (DLL copied into a folder of its own): TE.exe can not load the test DLL and reports both tests as Blocked (HRESULT 0x8007007E).")
    shutil.rmtree(CAPTURE_DIR)
    capture("LongRunningTests_taef.dll.runTimeout.txt", "x64", os.path.join(dbg64, "LongRunningTests_taef.dll"), OUT_FORMAT + " /testTimeout:0:0:1",
            description="Test timeout (adapter option TestTimeout=0:0:1): both 2-second tests are aborted and Failed.")
    capture("Error.SelectSyntaxError.txt", "x64", tests, OUT_FORMAT + " /select:\"@Name='TaefSamples::TestMath::AddPasses\"",
            description="Syntax error in the /select query (missing closing quote): startup error, exit code 0x06000000 (TaefConstants.ExitCodeStartupError).")
    capture("Tests_taef.dll.runNoMatch.txt", "x64", tests, OUT_FORMAT + " /select:\"@Name='DoesNotExist::Test'\"",
            description="/select matching no test of Tests_taef.dll: TaefSamples::MissingDataSource::Test#error is run (Blocked) nevertheless.")
    capture("Error.NoMatchingTests.txt", "x64", os.path.join(dbg64, "DllTests_taef.dll"), OUT_FORMAT + " /select:\"@Name='DoesNotExist::Test'\"",
            description="/select matching no test (DllTests_taef.dll).")
    capture("Error.NoTestFiles.txt", "x64", os.path.join(dbg64, "DoesNotExist_taef.dll"), OUT_FORMAT, cwd=dbg64,
            description="Test DLL does not exist.")
    capture("Error.NotATestDll.txt", "x64", os.path.join(dbg64, "DllProject.dll"), "/listProperties /runIgnoredTests " + OUT_FORMAT,
            description="/listProperties of a DLL which is not a TAEF test DLL (DllProject.dll).")
    capture("Tests_taef.dll.x86.listProperties.txt", "x86", os.path.join(BIN, "Debug", "Tests_taef.dll"), "/listProperties /runIgnoredTests " + OUT_FORMAT,
            description="x86 TE.exe (its version may differ from the x64 one) listing the x86 Debug build.")
    write_readme()


def write_readme():
    lines = []
    lines.append("# Captured TE.exe outputs")
    lines.append("")
    lines.append("Verbatim console outputs of TE.exe (TAEF) for the sample test DLLs of `SampleTests\\SampleTests.sln`, used as test data")
    lines.append("by the adapter's parser tests (`TestResources.TaefOutputDir`, `TestResources.ReadTaefOutputLines(fileName)`).")
    lines.append("TE.exe writes UTF-8 (without BOM) when called with `/unicodeOutput:false`; the files are UTF-8 with LF line endings")
    lines.append("(TE.exe itself writes CRLF). The enlistment root the outputs were captured in is replaced by `C:\\src\\TAEF-Test-Adapter` (same length).")
    lines.append("")
    lines.append("Captured with `Tests.Common\\Resources\\capture_taef_outputs.py` (TE.exe 10.104k x64, 10.103k x86) from the Debug x64 build")
    lines.append("(`out\\binaries\\SampleTests\\Debug-x64`) unless noted otherwise; TE.exe is `%s\\<arch>\\TE.exe`." % TE_ROOT)
    lines.append("TE.exe's stderr was empty except for the in-process run, whose stderr (the tests' std::cerr output) is in")
    lines.append("`Tests_taef.dll.runInProcess.stderr.txt`. If the samples change, recapture the outputs (and adapt the tests using them).")
    lines.append("")
    lines.append("| File | Arguments (after `TE.exe \"<dll>\"`) | Working directory / environment | Exit code | Content |")
    lines.append("|---|---|---|---|---|")
    for c in captures:
        env = ", ".join("%s=%s" % kv for kv in c["env"].items())
        wd = "`%s`" % c["cwd"] + (", %s" % env if env else "")
        lines.append("| `%s` | %s`%s` | %s | %s | %s |" % (c["name"], "" if c["arch"] == "x64" else "(x86 TE.exe) ",
                                                         c["args"].replace("|", "\\|"), wd, c["rc"],
                                                         c["description"].replace("|", "\\|")))
    lines.append("")
    lines.append("The DLL of a file is `out\\binaries\\SampleTests\\Debug-x64\\<first part of the file name>` (x86: `...\\Debug\\...`), except")
    lines.append("`DllTests_taef.dll.runWithoutDependency.txt` (a copy of DllTests_taef.dll without DllProject.dll in `out\\TaefOutputCapture`),")
    lines.append("`Error.NoMatchingTests.txt` (DllTests_taef.dll), `Error.NoTestFiles.txt` (`DoesNotExist_taef.dll`) and `Error.NotATestDll.txt` (`DllProject.dll`).")
    lines.append("`TestDirectory` runtime parameter: `%s`." % TEST_DIRECTORY)
    lines.append("")
    with open(os.path.join(TARGET, "README.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    stderr = [c["name"] for c in captures if c["stderr"]]
    if stderr:
        print("captures with stderr output:", stderr)


if __name__ == "__main__":
    main()
