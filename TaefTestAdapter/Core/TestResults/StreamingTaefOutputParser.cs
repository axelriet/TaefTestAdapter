// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.TestResults
{

    /// <summary>
    /// Parses the console output of a TE.exe test run (switches <c>/unicodeOutput:false /coloredConsoleOutput:false</c>,
    /// tests run sequentially) line by line, reports each test as started as soon as its <c>StartGroup:</c> line is
    /// received and reports its result as soon as its <c>EndGroup:</c> line is received. The output of a test looks like
    /// this:
    /// <code>
    /// &lt;empty line&gt;
    /// StartGroup: Ns::Class::Method
    /// &lt;output of the test, e.g. Log::Comment() output, Error: ... [File: ..., Function: ..., Line: ...], TestSkipped: ...&gt;
    /// EndGroup: Ns::Class::Method [Passed|Failed|Blocked|Skipped|NotRun]
    /// </code>
    /// Details:
    /// <list type="bullet">
    /// <item>Within a group, all lines except the matching <c>EndGroup:</c> line (including empty lines and
    /// <c>StartGroup:</c>/<c>EndGroup:</c> lines for other names) are output of the test. An <c>EndGroup:</c> glued to
    /// the end of preceding (unflushed) output (possible with <c>/inproc</c>) is recognized.</item>
    /// <item>Groups of data source error pseudo tests (<c>...#error</c>, which TE.exe always runs) which are not part of
    /// the tests run are ignored. Other groups outside of tests which do not belong to a test of the run are log groups
    /// of fixtures (WEX <c>Log::StartGroup()</c>, ended by TE.exe with <c>EndGroup: Wex.Logger Mismatched Group: ...</c>
    /// if a failing <c>VERIFY</c> left them open); their content is fixture output. A <c>StartGroup:</c> line of a test
    /// of the run ends such groups.</item>
    /// <item>Fixture output (all lines outside of test groups before the next test, the TE.exe banner and trailer
    /// excepted) is collected. Fixture failures printed before the affected tests (<c>TestBlocked: TAEF: Setup fixture
    /// '...' for the scope '...' returned 'false'.</c> and/or <c>Error: TAEF: Setup fixture '...' for the scope '...'
    /// failed.</c>) are carried, together with the fixture output collected before them (e.g. the <c>Error:</c> lines of
    /// the fixture), to all following tests in the scope (a test, a class, or the test DLL); several failure lines of
    /// the same scope before the next test belong to the same failure. Failures to load the test DLL are carried to all
    /// tests.</item>
    /// <item>A fixture operation which crashed or timed out (<c>Error: TAEF: [HRESULT 0x...] A failure occurred|A test
    /// timeout expired while running a test operation: '&lt;class&gt;::&lt;fixture&gt;'. ...</c>, no fixture failure line) is
    /// logged as warning and explains the following tests of the fixture's class which are Blocked without any other
    /// message (a crashing setup fixture blocks them, while tests following a crashing cleanup fixture run
    /// normally).</item>
    /// <item>Cleanup fixture failures are printed after the affected test's result has been reported; they do not change
    /// the result, but are logged as warnings (together with the fixture output collected before them).</item>
    /// <item>Other <c>Error:</c> lines of the fixture output not followed by a fixture failure, and <c>Warning: TAEF:</c>
    /// and similar lines outside of groups are logged as warnings (the duplicates within TE.exe's trailing summary, which
    /// is recognized by its exact section headers and <c>Summary: Total=...</c> line, are ignored).</item>
    /// <item>If the output ends within the group of a test (e.g. because the test crashed TE.exe while running in
    /// process), the test is reported as crashed (see <see cref="Flush"/>). If TE.exe terminated abnormally outside of a
    /// test (e.g. because a fixture crashed TE.exe while running in process), all tests of the run without result are
    /// reported as failed (see <see cref="TerminatedOutsideOfTest"/>).</item>
    /// </list>
    /// Outcomes: Passed, Failed and Skipped are reported as such, Blocked as Failed (error message prefixed by
    /// <see cref="BlockedPrefix"/>), NotRun as None. Durations are measured between receiving the <c>StartGroup:</c> and
    /// the <c>EndGroup:</c> line. Each <c>EndGroup:</c> produces a result (i.e., repeated tests produce several results).
    /// </summary>
    public class StreamingTaefOutputParser
    {
        public const string CrashText = "!! This test has probably CRASHED !!";

        /// <summary>Prefix of the error message of tests reported as Blocked by TE.exe (which are reported as failed).</summary>
        public const string BlockedPrefix = "Blocked: ";

        /// <summary>Start of the error message of tests which have not been run because TE.exe terminated abnormally outside of a test.</summary>
        public const string TerminatedOutsideOfTestText = "Test has not been run: TE.exe terminated abnormally";

        public const string StartGroupPrefix = "StartGroup: ";
        public const string EndGroupPrefix = "EndGroup: ";

        /// <summary>Prefix of TE.exe's final summary line (<c>Summary: Total=..., Passed=..., ...</c>).</summary>
        public const string SummaryPrefix = "Summary: ";

        /// <summary>
        /// TE.exe does not report test durations; tests running for less than 1ms are assumed to have run for 0.25ms
        /// on average (which also makes VS display the duration properly as "&lt;1ms").
        /// 2500 ticks = 0.25ms
        /// </summary>
        public static readonly TimeSpan ShortTestDuration = TimeSpan.FromTicks(2500);

        /// <summary>Maximum number of lines of fixture output collected before a fixture failure (<c>Error:</c> lines are kept preferably).</summary>
        public const int MaxNrOfFixtureOutputLines = 200;

        /// <summary>Maximum number of lines of output outside of tests mentioned if TE.exe terminates abnormally outside of a test.</summary>
        public const int MaxNrOfLastOutputLines = 20;

        private const string LoopSummaryPrefix = "Loop Summary: ";
        private const string TaefWarningPrefix = "Warning: TAEF: ";
        private const string HresultPrefix = "[HRESULT ";
        private const string StartupErrorContinuationPrefix = " Exception: ";
        private const string MismatchedGroupPrefix = "Wex.Logger Mismatched Group: ";
        private const string MismatchedGroupErrorPrefix = ErrorMessageParser.ErrorPrefix + MismatchedGroupPrefix;

        private static readonly string[] TaefResults = { "Passed", "Failed", "Blocked", "Skipped", "NotRun" };

        private static readonly Regex BannerRegex = new Regex(
            @"^Test Authoring and Execution Framework v\S+ for \S+$",
            RegexOptions.Compiled);

        private static readonly Regex SummaryRegex = new Regex(
            @"^Summary: Total=\d+, Passed=\d+, Failed=\d+, Blocked=\d+, Not Run=\d+, Skipped=\d+",
            RegexOptions.Compiled);

        private static readonly Regex SummarySectionRegex = new Regex(
            @"^Summary of (?:TAEF Warnings|Errors Outside of Tests|Non-passing Tests)(?: \(showing \d+ of \d+\))?:$",
            RegexOptions.Compiled);

        private static readonly Regex FixtureFailureRegex = new Regex(
            @"^(?:TestBlocked|Error): TAEF: (?<kind>Setup|Cleanup) fixture '(?<fixture>.*?)' for the scope '(?<scope>.*)' (?:returned 'false'|failed)\.$",
            RegexOptions.Compiled);

        /// <summary>A crash or timeout of the test host process while running an operation (a fixture if printed outside of a group).</summary>
        private static readonly Regex OperationFailureRegex = new Regex(
            @"^Error: TAEF: \[HRESULT:? 0x[0-9A-Fa-f]{8}\] A (?:failure occurred|test timeout expired) while running a test operation: '(?<operation>.+)'\.(?: \(|$)",
            RegexOptions.Compiled);

        private static readonly Regex TestDllLoadFailureRegex = new Regex(
            @"^Error: TAEF: \[HRESULT:? 0x[0-9A-Fa-f]{8}\] A failure occurred while preparing to run tests in '",
            RegexOptions.Compiled);

        private enum ScopeKind { Test, Class, TestDll }

        private class Group
        {
            public string Name { get; }
            public TestCase TestCase { get; }
            public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
            public List<string> CarriedLines { get; } = new List<string>();

            /// <summary>Lines explaining the result if the test is Blocked without any message (see <see cref="OperationFailure"/>).</summary>
            public List<string> FallbackLines { get; } = new List<string>();

            public List<string> Lines { get; } = new List<string>();

            public Group(string name, TestCase testCase)
            {
                Name = name;
                TestCase = testCase;
            }
        }

        private class CarriedFailure
        {
            public string Scope { get; }
            public ScopeKind ScopeKind { get; }
            public List<string> Lines { get; }
            public bool IsAttached { get; set; }

            /// <summary>Number of test groups started before the failure has been printed.</summary>
            public int NrOfTestGroupsStartedBefore { get; }

            public CarriedFailure(string scope, ScopeKind scopeKind, IEnumerable<string> lines, int nrOfTestGroupsStartedBefore)
            {
                Scope = scope;
                ScopeKind = scopeKind;
                Lines = lines.ToList();
                NrOfTestGroupsStartedBefore = nrOfTestGroupsStartedBefore;
            }
        }

        /// <summary>A fixture operation which crashed or timed out (see <see cref="OperationFailureRegex"/>).</summary>
        private class OperationFailure
        {
            /// <summary>The class of the fixture, or "" for a fixture of the test DLL.</summary>
            public string ClassName { get; }
            public List<string> Lines { get; }

            public OperationFailure(string className, List<string> lines)
            {
                ClassName = className;
                Lines = lines;
            }
        }

        /// <summary>The test which was running when the output ended (i.e., which probably crashed TE.exe), or null.</summary>
        public TestCase CrashedTestCase { get; private set; }

        /// <summary>The results reported so far.</summary>
        public IList<TestResult> TestResults { get; } = new List<TestResult>();

        /// <summary>True if TE.exe's final <c>Summary: ...</c> line has been received (i.e., TE.exe did not terminate abnormally).</summary>
        public bool SummaryFound { get; private set; }

        /// <summary>
        /// True if TE.exe terminated abnormally outside of the group of a test of the run (e.g. because a setup or cleanup
        /// fixture crashed TE.exe while running in process): the output did not end within such a group, TE.exe did not
        /// print its final <c>Summary:</c> line, and its exit code (passed to <see cref="Flush"/>) is no exit code of
        /// TAEF itself (see <see cref="TaefConstants.GetExitCodeDescription"/>). All tests of the run without result have
        /// been reported as failed (<see cref="TerminatedOutsideOfTestText"/>) in this case.
        /// </summary>
        public bool TerminatedOutsideOfTest { get; private set; }

        /// <summary>
        /// The tests of the run which have been reported as failed since TE.exe terminated abnormally outside of a test
        /// before running them (see <see cref="TerminatedOutsideOfTest"/>).
        /// </summary>
        public IList<TestCase> TestCasesNotRun { get; } = new List<TestCase>();

        private readonly List<TestCase> _testCasesRunInOrder = new List<TestCase>();
        private readonly IDictionary<string, TestCase> _testCasesRun;
        private readonly ILogger _logger;
        private readonly ITestFrameworkReporter _reporter;
        private readonly string _threadName;
        private readonly string _testDll;
        private readonly string _testDllName;

        /// <summary>Output of fixtures since the last test group, fixture failure etc. (see <see cref="MaxNrOfFixtureOutputLines"/>).</summary>
        private readonly List<string> _fixtureOutput = new List<string>();

        /// <summary>The last lines outside of test groups since the last test group (see <see cref="MaxNrOfLastOutputLines"/>).</summary>
        private readonly List<string> _lastOutputOutsideOfTests = new List<string>();

        private readonly List<CarriedFailure> _carriedFailures = new List<CarriedFailure>();
        private readonly List<OperationFailure> _operationFailures = new List<OperationFailure>();
        private readonly HashSet<string> _namesOfGroupsSeen = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The group of the test (of the run or not) currently running, or null.</summary>
        private Group _currentGroup;

        /// <summary>Log groups of fixtures currently open (innermost last); only if <see cref="_currentGroup"/> is null.</summary>
        private readonly List<string> _logGroups = new List<string>();

        private int _nrOfTestGroupsStarted;
        private int _nrOfLinesReported;
        private bool _isInTrailer;

        /// <param name="testCasesRun">The tests which are run by TE.exe (all of the same test DLL).</param>
        /// <param name="logger">Logger for problems reported by TE.exe.</param>
        /// <param name="reporter">Receives test starts and results.</param>
        /// <param name="threadName">Prefix of log messages (name of the executing thread), may be empty.</param>
        public StreamingTaefOutputParser(IEnumerable<TestCase> testCasesRun, ILogger logger, ITestFrameworkReporter reporter, string threadName = "")
        {
            if (testCasesRun == null)
                throw new ArgumentNullException(nameof(testCasesRun));

            _testCasesRun = new Dictionary<string, TestCase>(StringComparer.Ordinal);
            foreach (TestCase testCase in testCasesRun)
            {
                if (!_testCasesRun.ContainsKey(testCase.FullyQualifiedName))
                {
                    _testCasesRun.Add(testCase.FullyQualifiedName, testCase);
                    _testCasesRunInOrder.Add(testCase);
                }
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
            _threadName = threadName ?? "";
            _testDll = _testCasesRunInOrder.FirstOrDefault()?.Source ?? "";
            _testDllName = GetFileName(_testDll);
        }

        /// <summary>
        /// Processes the next line of TE.exe's output (without line terminator).
        /// </summary>
        /// <exception cref="TestRunCanceledException">if the test run has been canceled (thrown by the reporter)</exception>
        public void ReportLine(string line)
        {
            if (line == null)
                return;

            _nrOfLinesReported++;
            if (_currentGroup != null)
                ReportLineInsideGroup(line);
            else if (_logGroups.Count > 0)
                ReportLineInsideLogGroup(line);
            else
                ReportLineOutsideGroup(line);
        }

        /// <summary>
        /// Must be called after the last line has been reported. If the output ended within the group of a test, that
        /// test is reported as failed (it has probably crashed TE.exe), see <see cref="CrashedTestCase"/>. If TE.exe
        /// terminated abnormally outside of a test, all tests without result are reported as failed, see
        /// <see cref="TerminatedOutsideOfTest"/>.
        /// </summary>
        /// <param name="teExitCode">
        /// Exit code of TE.exe (if known; <see cref="int.MaxValue"/> if TE.exe could not be run), mentioned in the result
        /// of a crashed test and needed to detect an abnormal termination outside of a test.
        /// </param>
        /// <exception cref="TestRunCanceledException">if the test run has been canceled (thrown by the reporter)</exception>
        public void Flush(int? teExitCode = null)
        {
            LogPendingErrors();

            Group group = _currentGroup;
            _currentGroup = null;
            if (group?.TestCase != null)
            {
                CrashedTestCase = group.TestCase;
                ReportResult(CreateCrashedTestResult(group, teExitCode));
            }
            else if (IsAbnormalTermination(teExitCode))
            {
                TerminatedOutsideOfTest = true;
                ReportTestsNotRun(teExitCode.Value, group, _logGroups.LastOrDefault());
            }
            _logGroups.Clear();

            foreach (CarriedFailure carriedFailure in _carriedFailures.Where(cf => !cf.IsAttached))
            {
                _logger.DebugWarning($"{_threadName}{_testDllName}: fixture failure for scope '{carriedFailure.Scope}' did not affect any of the tests run: {string.Join(Environment.NewLine, carriedFailure.Lines)}");
            }
            _carriedFailures.Clear();
            _operationFailures.Clear();
        }

        #region Lines within groups

        private void ReportLineInsideGroup(string line)
        {
            if (TryParseEndOfGroup(line, _currentGroup.Name, out string taefResult, out string gluedOutput))
            {
                if (gluedOutput.Length > 0)
                    _currentGroup.Lines.Add(gluedOutput);

                Group group = _currentGroup;
                _currentGroup = null;
                if (group.TestCase != null)
                {
                    ReportResult(CreateTestResult(group, taefResult));
                }
                else
                {
                    _logger.VerboseInfo($"{_threadName}{_testDllName}: ignoring result of test '{group.Name}' (not part of the tests run): {taefResult}");
                }
                return;
            }

            // Groups of tests which are not part of the run are only skipped. If such a group does not end properly (e.g.
            // because a test logged a fake StartGroup line), the start of a test of the run must not be missed.
            if (_currentGroup.TestCase == null && line.StartsWith(StartGroupPrefix, StringComparison.Ordinal)
                && _testCasesRun.ContainsKey(line.Substring(StartGroupPrefix.Length)))
            {
                _logger.DebugWarning($"{_threadName}{_testDllName}: group '{_currentGroup.Name}' has not been ended before test '{line.Substring(StartGroupPrefix.Length)}' started");
                _currentGroup = null;
                StartTestGroup(line.Substring(StartGroupPrefix.Length));
                return;
            }

            if (_currentGroup.TestCase != null)
                _currentGroup.Lines.Add(line);
        }

        private static bool TryParseEndOfGroup(string line, string name, out string taefResult, out string gluedOutput)
        {
            taefResult = null;
            gluedOutput = null;

            if (!line.EndsWith("]", StringComparison.Ordinal) || line.IndexOf(EndGroupPrefix, StringComparison.Ordinal) < 0)
                return false;

            foreach (string result in TaefResults)
            {
                string endOfGroup = $"{EndGroupPrefix}{name} [{result}]";
                if (line.EndsWith(endOfGroup, StringComparison.Ordinal))
                {
                    taefResult = result;
                    gluedOutput = line.Substring(0, line.Length - endOfGroup.Length);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Log groups of fixtures

        private void ReportLineInsideLogGroup(string line)
        {
            string name = _logGroups[_logGroups.Count - 1];

            // a log group left open by a fixture (e.g. because of a failing VERIFY) is ended by TE.exe as mismatched group
            bool isMismatched = false;
            if (TryParseEndOfGroup(line, name, out string taefResult, out string gluedOutput)
                || (isMismatched = TryParseEndOfGroup(line, MismatchedGroupPrefix + name, out taefResult, out gluedOutput)))
            {
                if (gluedOutput.Length > 0)
                    ReportFixtureOutput(gluedOutput);

                _logGroups.RemoveAt(_logGroups.Count - 1);
                if (isMismatched)
                    _logger.VerboseInfo($"{_threadName}{_testDllName}: {line}");
                else
                    ReportFixtureOutput(line.Substring(gluedOutput.Length));
                return;
            }

            if (line.StartsWith(StartGroupPrefix, StringComparison.Ordinal))
            {
                string groupName = line.Substring(StartGroupPrefix.Length);
                if (IsTestGroup(groupName))
                {
                    _logger.DebugWarning($"{_threadName}{_testDllName}: group '{name}' has not been ended before test '{groupName}' started");
                    StartTestGroup(groupName);
                }
                else
                {
                    StartLogGroup(groupName, line);
                }
                return;
            }

            ReportFixtureOutput(line);
        }

        private void StartLogGroup(string name, string line)
        {
            // TE.exe's trailer contains no groups, i.e. a (spoofed) trailer header has been fixture output
            _isInTrailer = false;
            _logGroups.Add(name);
            ReportFixtureOutput(line);
        }

        #endregion

        #region Lines outside of groups

        private void ReportLineOutsideGroup(string line)
        {
            if (line.StartsWith(StartGroupPrefix, StringComparison.Ordinal))
            {
                string name = line.Substring(StartGroupPrefix.Length);
                if (IsTestGroup(name))
                    StartTestGroup(name);
                else
                    StartLogGroup(name, line);
                return;
            }

            if (SummaryRegex.IsMatch(line))
            {
                LogPendingErrors();
                SummaryFound = true;
                _isInTrailer = true;
                _logger.DebugInfo($"{_threadName}{_testDllName}: {line}");
                return;
            }

            if (_isInTrailer)
                return;

            if (SummarySectionRegex.IsMatch(line))
            {
                LogPendingErrors();
                _isInTrailer = true;
                return;
            }

            if (line.StartsWith(LoopSummaryPrefix, StringComparison.Ordinal))
            {
                // the next loop runs all fixtures again
                LogPendingErrors();
                _carriedFailures.Clear();
                _operationFailures.Clear();
                _logger.DebugInfo($"{_threadName}{_testDllName}: {line}");
                return;
            }

            if (BannerRegex.IsMatch(line))
            {
                _logger.DebugInfo($"{_threadName}{_testDllName}: {line}");
                return;
            }

            ReportFixtureOutput(line);
        }

        /// <summary>
        /// Processes a line of fixture output (a line outside of test groups, which may be within a log group of a fixture).
        /// </summary>
        private void ReportFixtureOutput(string line)
        {
            if (line.Length == 0)
                return;

            // continuation of a TAEF startup error
            if (line.StartsWith(StartupErrorContinuationPrefix, StringComparison.Ordinal) && _fixtureOutput.Count > 0
                && _fixtureOutput[_fixtureOutput.Count - 1].StartsWith(ErrorMessageParser.ErrorPrefix, StringComparison.Ordinal))
            {
                int last = _fixtureOutput.Count - 1;
                _fixtureOutput[last] = _fixtureOutput[last] + Environment.NewLine + line;
                AddToLastOutput(line);
                return;
            }

            AddToLastOutput(line);

            Match match = FixtureFailureRegex.Match(line);
            if (match.Success)
            {
                HandleFixtureFailure(line, match.Groups["kind"].Value, match.Groups["scope"].Value);
                return;
            }

            if (TestDllLoadFailureRegex.IsMatch(line))
            {
                LogPendingErrors();
                CarryFailure(_testDll, ScopeKind.TestDll, new List<string> { line });
                _logger.LogWarning($"{_threadName}{_testDllName}: {line}");
                return;
            }

            match = OperationFailureRegex.Match(line);
            if (match.Success)
            {
                HandleOperationFailure(line, match.Groups["operation"].Value);
                return;
            }

            if (line.StartsWith(MismatchedGroupErrorPrefix, StringComparison.Ordinal))
            {
                // TE.exe ended a log group left open by a fixture (see ReportLineInsideLogGroup)
                _logger.VerboseInfo($"{_threadName}{_testDllName}: {line}");
                return;
            }

            AddToFixtureOutput(line);

            // errors are logged if they are not followed by a fixture failure (see LogPendingErrors)
            if (line.StartsWith(ErrorMessageParser.ErrorPrefix, StringComparison.Ordinal))
                return;

            if (line.StartsWith(TaefWarningPrefix, StringComparison.Ordinal) || line.StartsWith(HresultPrefix, StringComparison.Ordinal))
            {
                _logger.LogWarning($"{_threadName}{_testDllName}: {line}");
            }
            else
            {
                // output of fixtures (method setup of the next or cleanup of the previous test etc.)
                _logger.VerboseInfo($"{_threadName}{_testDllName}: {line}");
            }
        }

        private bool IsTestGroup(string name)
        {
            return _testCasesRun.ContainsKey(name) || TaefNames.IsDataSourceErrorPseudoTest(name);
        }

        private void StartTestGroup(string name)
        {
            LogPendingErrors();
            _logGroups.Clear();
            _lastOutputOutsideOfTests.Clear();
            _isInTrailer = false;
            _nrOfTestGroupsStarted++;

            _testCasesRun.TryGetValue(name, out TestCase testCase);
            _currentGroup = new Group(name, testCase);
            _namesOfGroupsSeen.Add(name);

            foreach (CarriedFailure carriedFailure in _carriedFailures.ToList())
            {
                if (IsInScope(name, carriedFailure))
                {
                    _currentGroup.CarriedLines.AddRange(carriedFailure.Lines);
                    if (testCase != null)
                        carriedFailure.IsAttached = true;
                    if (carriedFailure.ScopeKind == ScopeKind.Test)
                        _carriedFailures.Remove(carriedFailure);
                }
                else if (carriedFailure.ScopeKind != ScopeKind.TestDll)
                {
                    // fixture failures are printed immediately before the first affected test
                    _carriedFailures.Remove(carriedFailure);
                    if (!carriedFailure.IsAttached)
                        _logger.DebugWarning($"{_threadName}{_testDllName}: fixture failure for scope '{carriedFailure.Scope}' did not affect any of the tests run: {string.Join(Environment.NewLine, carriedFailure.Lines)}");
                }
            }

            foreach (OperationFailure operationFailure in _operationFailures.ToList())
            {
                if (IsInScope(name, operationFailure))
                    _currentGroup.FallbackLines.AddRange(operationFailure.Lines);
                else
                    _operationFailures.Remove(operationFailure);
            }

            if (testCase != null)
            {
                _reporter.ReportTestsStarted(testCase.Yield());
            }
            else
            {
                _logger.DebugInfo($"{_threadName}{_testDllName}: ignoring test '{name}' (not part of the tests run)");
            }
        }

        private void HandleFixtureFailure(string line, string kind, string scope)
        {
            List<string> lines = TakeFixtureOutput();
            lines.Add(line);

            ScopeKind scopeKind = GetScopeKind(scope);
            if (kind == "Setup")
            {
                CarryFailure(scope, scopeKind, lines);
                _logger.DebugInfo($"{_threadName}{_testDllName}: setup failed for scope '{scope}':{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
                return;
            }

            // results of the affected tests have already been reported, and TE.exe does not change them
            string scopeDescription;
            switch (scopeKind)
            {
                case ScopeKind.Test:
                    scopeDescription = $"Test '{scope}'";
                    break;
                case ScopeKind.Class:
                    scopeDescription = $"Test class '{scope}'";
                    break;
                default:
                    scopeDescription = $"Test DLL '{_testDll}'";
                    break;
            }
            _logger.LogWarning($"{_threadName}{scopeDescription}: cleanup failed after the result has been reported (TE.exe does not change the result because of this):{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
        }

        /// <summary>
        /// A setup fixture failure is carried to the affected tests. TE.exe prints several lines for the same failure (e.g.
        /// <c>TestBlocked: ... returned 'false'.</c> followed by <c>Error: ... failed.</c> if the fixture logged an error
        /// and returned false): lines for the same scope printed before the next test belong to the same failure.
        /// </summary>
        private void CarryFailure(string scope, ScopeKind scopeKind, List<string> lines)
        {
            CarriedFailure sameFailure = _carriedFailures.FirstOrDefault(cf =>
                cf.Scope == scope && cf.ScopeKind == scopeKind && cf.NrOfTestGroupsStartedBefore == _nrOfTestGroupsStarted);
            if (sameFailure != null)
            {
                sameFailure.Lines.AddRange(lines);
                return;
            }

            // e.g. a class setup which is run again after a crash of the test host process
            _carriedFailures.RemoveAll(cf => cf.Scope == scope && cf.ScopeKind == scopeKind);
            _carriedFailures.Add(new CarriedFailure(scope, scopeKind, lines, _nrOfTestGroupsStarted));
        }

        private void HandleOperationFailure(string line, string operation)
        {
            List<string> lines = TakeFixtureOutput();
            lines.Add(line);

            foreach (string error in ErrorMessageParser.GetErrors(lines))
            {
                _logger.LogWarning($"{_threadName}{_testDllName}: {error}");
            }

            // the operation is a fixture (the failures of test operations are printed within their groups): a setup
            // fixture blocks the following tests of its class, while the tests following a cleanup fixture run normally
            string className = TaefNames.GetClassName(operation);
            _operationFailures.RemoveAll(of => of.ClassName == className);
            _operationFailures.Add(new OperationFailure(className, lines));
        }

        private ScopeKind GetScopeKind(string scope)
        {
            if (IsTestDllScope(scope))
                return ScopeKind.TestDll;
            if (_testCasesRun.ContainsKey(scope) || _namesOfGroupsSeen.Contains(scope))
                return ScopeKind.Test;
            return ScopeKind.Class;
        }

        private bool IsTestDllScope(string scope)
        {
            // TE.exe prints the path of the test DLL as it has been passed on the command line
            if (string.Equals(scope, _testDll, StringComparison.OrdinalIgnoreCase))
                return true;

            return (scope.IndexOf('\\') >= 0 || scope.IndexOf('/') >= 0)
                && scope.EndsWith(TaefConstants.TestDllExtension, StringComparison.OrdinalIgnoreCase)
                && string.Equals(GetFileName(scope), _testDllName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInScope(string testName, CarriedFailure carriedFailure)
        {
            string scope = carriedFailure.Scope;
            switch (carriedFailure.ScopeKind)
            {
                case ScopeKind.TestDll:
                    return true;
                case ScopeKind.Test:
                    return testName == scope;
                default:
                    if (testName == scope)
                        return true;
                    // a class row (e.g. Ns::Class#Row, as printed by TE.exe for the fixtures of data-driven classes): its
                    // tests start with "<scope>::" (named rows of classes within a namespace can not be told apart from
                    // method rows by their names, see TaefNames)
                    if (scope.IndexOf(TaefConstants.DataRowSeparator) >= 0
                        && testName.StartsWith(scope + TaefConstants.ScopeSeparator, StringComparison.Ordinal))
                        return true;
                    string className = TaefNames.GetClassName(testName);
                    if (className.Length > 0)
                        return className == scope;
                    // e.g. Class#metadataSet0::Method (class level data)
                    return testName.StartsWith(scope + TaefConstants.DataRowSeparator, StringComparison.Ordinal)
                        || testName.StartsWith(scope + TaefConstants.ScopeSeparator, StringComparison.Ordinal);
            }
        }

        private static bool IsInScope(string testName, OperationFailure operationFailure)
        {
            string scope = operationFailure.ClassName;
            if (scope.Length == 0)
                return true;

            // the fixture's class may have class level data (Class#metadataSet0) or contain nested classes
            string className = TaefNames.GetClassName(testName);
            return className == scope
                || className.StartsWith(scope + TaefConstants.DataRowSeparator, StringComparison.Ordinal)
                || className.StartsWith(scope + TaefConstants.ScopeSeparator, StringComparison.Ordinal);
        }

        private void AddToFixtureOutput(string line)
        {
            _fixtureOutput.Add(line);
            if (_fixtureOutput.Count > MaxNrOfFixtureOutputLines)
            {
                // keep the errors, which explain fixture failures
                int index = _fixtureOutput.FindIndex(l => !l.StartsWith(ErrorMessageParser.ErrorPrefix, StringComparison.Ordinal));
                _fixtureOutput.RemoveAt(index >= 0 ? index : 0);
            }
        }

        private void AddToLastOutput(string line)
        {
            _lastOutputOutsideOfTests.Add(line);
            if (_lastOutputOutsideOfTests.Count > MaxNrOfLastOutputLines)
                _lastOutputOutsideOfTests.RemoveAt(0);
        }

        /// <returns>The fixture output collected so far (which is cleared).</returns>
        private List<string> TakeFixtureOutput()
        {
            var lines = new List<string>(_fixtureOutput);
            _fixtureOutput.Clear();
            return lines;
        }

        /// <summary>
        /// Logs the errors of the fixture output collected so far (which have not been followed by a fixture failure) as
        /// warnings and clears the fixture output.
        /// </summary>
        private void LogPendingErrors()
        {
            foreach (string error in ErrorMessageParser.GetErrors(TakeFixtureOutput()))
            {
                _logger.LogWarning($"{_threadName}{_testDllName}: {error}");
            }
        }

        #endregion

        #region Creation of test results

        private void ReportResult(TestResult result)
        {
            _reporter.ReportTestResults(result.Yield());
            TestResults.Add(result);
        }

        private TestResult CreateTestResult(Group group, string taefResult)
        {
            TestCase testCase = group.TestCase;
            TimeSpan duration = NormalizeDuration(group.Stopwatch.Elapsed);
            List<string> lines = group.CarriedLines.Concat(group.Lines).ToList();
            Messages messages = ParseMessages(lines, testCase);
            if (taefResult == "Blocked" && messages.NrOfMessages == 0 && group.FallbackLines.Count > 0)
            {
                // e.g. blocked because of a crashing setup fixture
                lines = group.FallbackLines.Concat(lines).ToList();
                messages = ParseMessages(lines, testCase);
            }
            string output = GetOutput(lines);

            TestResult result;
            switch (taefResult)
            {
                case "Passed":
                    result = CreatePassedTestResult(testCase, duration);
                    break;
                case "Skipped":
                    result = CreateSkippedTestResult(testCase, duration, messages.ErrorMessage, messages.ErrorStackTrace);
                    break;
                case "Blocked":
                    string errorMessage = messages.NrOfMessages > 0
                        ? BlockedPrefix + messages.ErrorMessage
                        : BlockedPrefix + "no reason has been reported by TE.exe";
                    result = CreateFailedTestResult(testCase, duration, errorMessage, messages.ErrorStackTrace);
                    break;
                case "NotRun":
                    result = CreateTestResult(testCase, TestOutcome.None, duration, messages.ErrorMessage, messages.ErrorStackTrace);
                    break;
                default:
                    result = CreateFailedTestResult(testCase, duration, messages.ErrorMessage, messages.ErrorStackTrace);
                    break;
            }

            result.Output = output;
            return result;
        }

        private TestResult CreateCrashedTestResult(Group group, int? teExitCode)
        {
            List<string> lines = group.CarriedLines.Concat(group.Lines).ToList();
            string output = GetOutput(lines);
            Messages messages = ParseMessages(lines, group.TestCase);

            string errorMessage = CrashText;
            if (teExitCode.HasValue)
                errorMessage += $" (TE.exe terminated with exit code 0x{teExitCode.Value:X8})";
            if (output != null)
                errorMessage += $"\nTest output:\n\n{output}";

            TestResult result = CreateFailedTestResult(
                group.TestCase, NormalizeDuration(group.Stopwatch.Elapsed), errorMessage, messages.ErrorStackTrace);
            result.Output = output;
            return result;
        }

        private bool IsAbnormalTermination(int? teExitCode)
        {
            // int.MaxValue: TE.exe could not be run; TAEF's own exit codes are reported by the caller
            return teExitCode.HasValue
                && teExitCode.Value != int.MaxValue
                && _nrOfLinesReported > 0
                && !SummaryFound
                && TaefConstants.GetExitCodeDescription(teExitCode.Value) == null;
        }

        /// <summary>Reports the tests of the run without result as failed since TE.exe terminated abnormally outside of a test.</summary>
        /// <param name="groupOfTestNotPartOfRun">The group of a test (not part of the run) TE.exe terminated in, or null.</param>
        /// <param name="openGroup">
        /// The innermost group TE.exe terminated in otherwise, or null. Such a group is taken for a log group of a fixture,
        /// but might be a test which is not part of the tests run (e.g. because TE.exe also ran tests matching the pattern
        /// of a selected test name), so it is mentioned.
        /// </param>
        private void ReportTestsNotRun(int teExitCode, Group groupOfTestNotPartOfRun, string openGroup)
        {
            var testsWithResult = new HashSet<string>(TestResults.Select(tr => tr.TestCase.FullyQualifiedName), StringComparer.Ordinal);
            List<TestCase> testCasesNotRun = _testCasesRunInOrder.Where(tc => !testsWithResult.Contains(tc.FullyQualifiedName)).ToList();

            string location;
            if (groupOfTestNotPartOfRun != null)
                location = $"while running test '{groupOfTestNotPartOfRun.Name}', which is not part of the tests run";
            else if (openGroup != null)
                location = $"outside of a test, within group '{openGroup}' (a log group of a setup or cleanup fixture running in process, or a test which is not part of the tests run)";
            else
                location = "outside of a test (probably in a setup or cleanup fixture running in process)";
            _logger.DebugWarning($"{_threadName}{_testDllName}: TE.exe terminated abnormally with exit code 0x{teExitCode:X8} {location}, {testCasesNotRun.Count} tests have not been run");
            if (testCasesNotRun.Count == 0)
                return;

            List<string> lastOutput = _lastOutputOutsideOfTests.ToList();
            string output = GetOutput(lastOutput);
            string errorMessage = $"{TerminatedOutsideOfTestText} with exit code 0x{teExitCode:X8} {location}";
            if (output != null)
                errorMessage += $"\nLast output of TE.exe:\n\n{output}";

            _reporter.ReportTestsStarted(testCasesNotRun);
            foreach (TestCase testCase in testCasesNotRun)
            {
                TestCasesNotRun.Add(testCase);
                Messages messages = ParseMessages(lastOutput, testCase);
                TestResult result = CreateFailedTestResult(testCase, TimeSpan.Zero, errorMessage, messages.ErrorStackTrace);
                result.Output = output;
                ReportResult(result);
            }
        }

        private class Messages
        {
            public string ErrorMessage { get; set; }
            public string ErrorStackTrace { get; set; }
            public int NrOfMessages { get; set; }
        }

        /// <summary>
        /// Creates error message and stack trace of the lines of a test with <see cref="ErrorMessageParser"/>. Should that
        /// fail, the lines are used as error message: a single test must never lose the results of the whole run.
        /// </summary>
        private Messages ParseMessages(IList<string> lines, TestCase testCase)
        {
            try
            {
                var parser = new ErrorMessageParser(lines, testCase.CodeFilePath);
                parser.Parse();
                return new Messages { ErrorMessage = parser.ErrorMessage, ErrorStackTrace = parser.ErrorStackTrace, NrOfMessages = parser.NrOfMessages };
            }
            catch (Exception e)
            {
                _logger.DebugWarning($"{_threadName}{_testDllName}: could not parse the error messages of test '{testCase.FullyQualifiedName}': {e}");
                string output = GetOutput(lines);
                return new Messages { ErrorMessage = output, ErrorStackTrace = null, NrOfMessages = output == null ? 0 : 1 };
            }
        }

        private static string GetOutput(IList<string> lines)
        {
            return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
        }

        public static TimeSpan NormalizeDuration(TimeSpan duration)
        {
            return duration.TotalMilliseconds < 1
                ? ShortTestDuration
                : duration;
        }

        public static TestResult CreatePassedTestResult(TestCase testCase, TimeSpan duration)
        {
            return CreateTestResult(testCase, TestOutcome.Passed, duration, null, null);
        }

        public static TestResult CreateSkippedTestResult(TestCase testCase, TimeSpan duration, string errorMessage, string errorStackTrace)
        {
            return CreateTestResult(testCase, TestOutcome.Skipped, duration, errorMessage, errorStackTrace);
        }

        public static TestResult CreateFailedTestResult(TestCase testCase, TimeSpan duration, string errorMessage, string errorStackTrace)
        {
            return CreateTestResult(testCase, TestOutcome.Failed, duration, errorMessage, errorStackTrace);
        }

        private static TestResult CreateTestResult(TestCase testCase, TestOutcome outcome, TimeSpan duration, string errorMessage, string errorStackTrace)
        {
            return new TestResult(testCase)
            {
                ComputerName = Environment.MachineName,
                DisplayName = testCase.DisplayName,
                Outcome = outcome,
                ErrorMessage = string.IsNullOrEmpty(errorMessage) ? null : errorMessage,
                ErrorStackTrace = string.IsNullOrEmpty(errorStackTrace) ? null : errorStackTrace,
                Duration = duration
            };
        }

        #endregion

        private static string GetFileName(string file)
        {
            try
            {
                return Path.GetFileName(file) ?? file;
            }
            catch (ArgumentException)
            {
                return file;
            }
        }

    }

}
