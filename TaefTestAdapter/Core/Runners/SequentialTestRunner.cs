// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Model;
using TaefTestAdapter.Framework;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Runs tests test DLL by test DLL: for each test DLL, tests marked as ignored are reported as skipped (unless option
    /// <see cref="SettingsWrapper.RunIgnoredTests"/> is set), then TE.exe (see <see cref="TaefLocator"/>) is run once
    /// for each command line created by the <see cref="CommandLineGenerator"/>; its output is parsed while TE.exe is
    /// running (see <see cref="StreamingTaefOutputParser"/>), and results for tests which have not been reported are
    /// created afterwards (see <see cref="TestResultCollector"/>). If TE.exe's output is not available (i.e., if the
    /// tests are debugged with the VsTest framework's debugger), the results are read from a WTT log written by TE.exe
    /// (see <see cref="WttLogParser"/>) after TE.exe has terminated; its path is chosen such that TE.exe can handle it
    /// (see <see cref="TeArguments.CreateWttLogFile(string,string,string,ILogger)"/>).
    /// </summary>
    public class SequentialTestRunner : ITestRunner
    {
        private volatile bool _canceled;

        private readonly string _threadName;
        private readonly int _threadId;
        private readonly string _testDir;
        private readonly ITestFrameworkReporter _frameworkReporter;
        private readonly ILogger _logger;
        private readonly SettingsWrapper _settings;
        private readonly SchedulingAnalyzer _schedulingAnalyzer;

        private readonly object _processExecutorLock = new object();
        private IProcessExecutor _processExecutor;

        // durations of debugged tests are not representative
        private bool _recordDurations = true;

        public SequentialTestRunner(string threadName, int threadId, string testDir, ITestFrameworkReporter reporter, ILogger logger, SettingsWrapper settings, SchedulingAnalyzer schedulingAnalyzer)
        {
            _threadName = threadName;
            _threadId = threadId;
            _testDir = testDir;
            _frameworkReporter = reporter;
            _logger = logger;
            _settings = settings;
            _schedulingAnalyzer = schedulingAnalyzer;
        }


        public void RunTests(IEnumerable<TestCase> testCasesToRun, bool isBeingDebugged, IDebuggedProcessExecutorFactory processExecutorFactory)
        {
            _recordDurations = !isBeingDebugged;
            IDictionary<string, List<TestCase>> groupedTestCases = testCasesToRun.GroupByTestDll();
            foreach (string testDll in groupedTestCases.Keys)
            {
                if (_canceled)
                    break;

                _settings.ExecuteWithSettingsForTestDll(testDll, _logger, () =>
                {
                    string workingDir = _settings.GetWorkingDirForExecution(testDll, _testDir, _threadId);
                    string userParameters = _settings.GetUserParametersForExecution(testDll, _testDir, _threadId);
                    IDictionary<string, string> environmentVariables = _settings.GetEnvironmentVariablesForExecution(testDll, _testDir, _threadId);

                    RunTestsFromTestDll(
                        testDll,
                        workingDir,
                        groupedTestCases[testDll],
                        userParameters,
                        environmentVariables,
                        isBeingDebugged,
                        processExecutorFactory);
                });

            }
        }

        public void Cancel()
        {
            _canceled = true;
            if (_settings.KillProcessesOnCancel)
            {
                lock (_processExecutorLock)
                {
                    _processExecutor?.Cancel();
                }
            }
        }


        private void RunTestsFromTestDll(string testDll, string workingDir,
            IList<TestCase> testCasesToRun, string userParameters, IDictionary<string, string> environmentVariables,
            bool isBeingDebugged, IDebuggedProcessExecutorFactory processExecutorFactory)
        {
            ReportIgnoredTests(testDll, testCasesToRun);
            if (!_settings.RunIgnoredTests && testCasesToRun.All(TaefConstants.IsIgnored))
                return;

            string teExecutable = TaefLocator.FindTeExecutable(testDll, _settings, _logger);
            if (teExecutable == null)
            {
                // error has been logged by TaefLocator
                _logger.DebugWarning($"{_threadName}Tests of test DLL '{testDll}' can not be run since no TE.exe has been found");
                ReportResults(testDll, CollectMissingResults(testCasesToRun.Where(tc => _settings.RunIgnoredTests || !TaefConstants.IsIgnored(tc)), null));
                return;
            }
            _logger.DebugInfo($"{_threadName}Running tests of test DLL '{testDll}' with '{teExecutable}'");

            // if TE.exe's output is not available (debugging with the VsTest framework), results are taken from a WTT log
            TeArguments.WttLogFile wttLogFile = IsTestOutputAvailable(isBeingDebugged)
                ? null
                : TeArguments.CreateWttLogFile(Path.GetTempPath(), workingDir, userParameters, _logger);

            var generator = new CommandLineGenerator(testCasesToRun, testDll, teExecutable.Length, userParameters, isBeingDebugged, _settings,
                wttLogFile?.Argument, workingDir, _logger);
            foreach (CommandLineGenerator.Args arguments in generator.GetCommandLines())
            {
                if (_canceled)
                {
                    break;
                }
                var streamingParser = new StreamingTaefOutputParser(arguments.TestCases, _logger, _frameworkReporter, _threadName);
                var results = RunTests(teExecutable, testDll, workingDir, isBeingDebugged, processExecutorFactory, arguments, environmentVariables, streamingParser, wttLogFile).ToArray();
                ReportResults(testDll, results);
            }
        }

        private bool IsTestOutputAvailable(bool isBeingDebugged)
        {
            return !isBeingDebugged || _settings.DebuggerKind > DebuggerKind.VsTestFramework;
        }

        private void ReportIgnoredTests(string testDll, IList<TestCase> testCasesToRun)
        {
            if (_settings.RunIgnoredTests)
                return;

            List<TestCase> ignoredTestCases = testCasesToRun.Where(TaefConstants.IsIgnored).ToList();
            if (ignoredTestCases.Count == 0)
                return;

            List<TestResult> results = ignoredTestCases
                .Select(tc => StreamingTaefOutputParser.CreateSkippedTestResult(tc, TimeSpan.Zero, TaefConstants.IgnoredTestMessage, null))
                .ToList();
            try
            {
                _frameworkReporter.ReportTestsStarted(ignoredTestCases);
                _frameworkReporter.ReportTestResults(results);
                _logger.DebugInfo($"{_threadName}Reported {results.Count} tests of test DLL '{testDll}' with metadata Ignore=true as skipped (option '{SettingsWrapper.OptionRunIgnoredTests}' is not set)");
            }
            catch (TestRunCanceledException e)
            {
                _logger.DebugInfo($"{_threadName}Execution has been canceled: {e.InnerException?.Message ?? e.Message}");
                Cancel();
            }
        }

        /// <summary>
        /// Reports results which have not been reported while TE.exe was running, and records all durations.
        /// </summary>
        private void ReportResults(string testDll, IList<TestResult> results)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                _frameworkReporter.ReportTestsStarted(results.Select(tr => tr.TestCase));
                _frameworkReporter.ReportTestResults(results);
                stopwatch.Stop();
                if (results.Count > 0)
                    _logger.DebugInfo($"{_threadName}Reported {results.Count} test results to VS, test DLL: '{testDll}', duration: {stopwatch.Elapsed}");
            }
            catch (TestRunCanceledException e)
            {
                _logger.DebugInfo($"{_threadName}Execution has been canceled: {e.InnerException?.Message ?? e.Message}");
                Cancel();
            }

            UpdateTestDurations(results);
        }

        private IEnumerable<TestResult> RunTests(string teExecutable, string testDll, string workingDir, bool isBeingDebugged,
            IDebuggedProcessExecutorFactory processExecutorFactory, CommandLineGenerator.Args arguments, IDictionary<string, string> environmentVariables,
            StreamingTaefOutputParser streamingParser, TeArguments.WttLogFile wttLogFile)
        {
            try
            {
                return TryRunTests(teExecutable, testDll, workingDir, isBeingDebugged, processExecutorFactory, arguments, environmentVariables, streamingParser, wttLogFile);
            }
            catch (Exception e)
            {
                LogExecutionError(_logger, teExecutable, workingDir, arguments.CommandLine, e, _threadName);

                if (_canceled)
                    return new TestResult[0];

                var remainingTestCases = arguments.TestCases.Except(streamingParser.TestResults.Select(tr => tr.TestCase));
                return CollectMissingResults(remainingTestCases, null);
            }
            finally
            {
                if (wttLogFile != null)
                    WttLogParser.DeleteLogFile(wttLogFile.File, _logger);
            }
        }

        /// <summary>
        /// Logs the failure to run TE.exe, together with a hint how to reproduce the problem.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="executable">The executable which has been started (usually TE.exe).</param>
        /// <param name="workingDir">The working directory of the process.</param>
        /// <param name="arguments">The arguments passed to the executable (starting with the quoted test DLL).</param>
        /// <param name="exception">The exception which occurred.</param>
        /// <param name="threadName">Prefix of the log messages (name of the executing thread), may be empty.</param>
        public static void LogExecutionError(ILogger logger, string executable, string workingDir, string arguments, Exception exception, string threadName = "")
        {
            logger.LogError($"{threadName}Failed to run '{executable}': {exception.Message}");
            if (exception is AggregateException aggregateException)
            {
               exception = aggregateException.Flatten();
            }
            logger.DebugError($@"{threadName}Exception:{Environment.NewLine}{exception}");
            logger.LogError(
                $"{threadName}{Strings.Instance.TroubleShootingLink}");
            logger.LogError(
                $"{threadName}In particular: launch a command prompt, change into directory '{workingDir}', and execute the following command to make sure your tests can be run in general.{Environment.NewLine}\"{executable}\" {arguments}");
        }

        private IEnumerable<TestResult> TryRunTests(string teExecutable, string testDll, string workingDir, bool isBeingDebugged,
            IDebuggedProcessExecutorFactory processExecutorFactory, CommandLineGenerator.Args arguments, IDictionary<string, string> environmentVariables,
            StreamingTaefOutputParser streamingParser, TeArguments.WttLogFile wttLogFile)
        {
            int exitCode = RunTeProcess(teExecutable, testDll, workingDir, arguments, environmentVariables, isBeingDebugged, processExecutorFactory, streamingParser);

            if (_canceled)
                return new List<TestResult>();

            IList<string> consoleOutput = null;
            if (wttLogFile != null)
            {
                consoleOutput = new WttLogParser(wttLogFile.File, _logger).GetConsoleOutput();
                _logger.DebugInfo($"{_threadName}Read {consoleOutput.Count} lines of output from WTT log '{wttLogFile.File}'");
                if (consoleOutput.Count == 0)
                {
                    // neither the WTT log nor the file TE.exe writes while tests are running exists (e.g. TE.exe did not
                    // start any test), or TE.exe could not handle the log file's path (an error has been logged then)
                    string reason = TeArguments.IsAscii(wttLogFile.Argument)
                        ? $"Set option '{SettingsWrapper.OptionDebuggerKind}' to '{DebuggerKindConverter.Native}' to get results even if TE.exe terminates abnormally."
                        : $"The path of the WTT log ('{wttLogFile.Argument}') contains non-ASCII characters, which TE.exe can not handle (see the preceding error).";
                    _logger.LogWarning($"{_threadName}Test DLL '{testDll}': TE.exe (exit code 0x{exitCode:X8}) did not write a WTT log, so no test results are available. {reason}");
                }
            }

            var remainingTestCases = arguments.TestCases
                .Except(streamingParser.TestResults.Select(tr => tr.TestCase));
            return CollectMissingResults(remainingTestCases, streamingParser.CrashedTestCase, consoleOutput);
        }

        private List<TestResult> CollectMissingResults(IEnumerable<TestCase> testCases, TestCase crashedTestCase, IList<string> consoleOutput = null)
        {
            return new TestResultCollector(_logger, _threadName, _settings)
                .CollectTestResults(testCases, consoleOutput ?? new List<string>(), crashedTestCase)
                .OrderBy(tr => tr.TestCase.FullyQualifiedName)
                .ToList();
        }

        /// <returns>The exit code of TE.exe</returns>
        private int RunTeProcess(string teExecutable, string testDll, string workingDir, CommandLineGenerator.Args arguments, IDictionary<string, string> environmentVariables,
            bool isBeingDebugged, IDebuggedProcessExecutorFactory processExecutorFactory, StreamingTaefOutputParser streamingParser)
        {
            string pathExtension = _settings.GetPathExtension(testDll);
            if (!string.IsNullOrEmpty(pathExtension))
            {
                if (environmentVariables.ContainsKey("PATH") && !string.IsNullOrEmpty(environmentVariables["PATH"]))
                {
                    _logger.LogWarning($"Test DLL {testDll}: Both a path extension and a PATH environment variable have been provided! The PATH environment variable will be ignored.");
                    environmentVariables.Remove("PATH");
                }
            }

            bool isTestOutputAvailable = IsTestOutputAvailable(isBeingDebugged);
            bool printTestOutput = _settings.PrintTestOutput &&
                                   !_settings.ParallelTestExecution &&
                                   isTestOutputAvailable;

            void OnNewOutputLine(string line)
            {
                try
                {
                    if (!_canceled) streamingParser.ReportLine(line);
                }
                catch (TestRunCanceledException e)
                {
                    _logger.DebugInfo($"{_threadName}Execution has been canceled: {e.InnerException?.Message ?? e.Message}");
                    Cancel();
                }
            }

            IProcessExecutor processExecutor = isBeingDebugged
                ? _settings.DebuggerKind == DebuggerKind.VsTestFramework
                    ? processExecutorFactory.CreateFrameworkDebuggingExecutor(printTestOutput, _logger)
                    : processExecutorFactory.CreateNativeDebuggingExecutor(
                        _settings.DebuggerKind == DebuggerKind.Native ? DebuggerEngine.Native : DebuggerEngine.ManagedAndNative,
                        printTestOutput, _logger)
                : processExecutorFactory.CreateExecutor(printTestOutput, _logger);
            lock (_processExecutorLock)
            {
                if (_canceled)
                    return int.MaxValue;
                _processExecutor = processExecutor;
            }

            int exitCode;
            try
            {
                _logger.VerboseInfo($"{_threadName}Executing \"{teExecutable}\" {arguments.CommandLine}");
                exitCode = processExecutor.ExecuteCommandBlocking(
                    teExecutable, arguments.CommandLine, workingDir, pathExtension, environmentVariables,
                    isTestOutputAvailable ? (Action<string>) OnNewOutputLine : null);
            }
            finally
            {
                lock (_processExecutorLock)
                {
                    _processExecutor = null;
                }
            }

            if (!_canceled)
            {
                try
                {
                    streamingParser.Flush(exitCode);
                }
                catch (TestRunCanceledException e)
                {
                    _logger.DebugInfo($"{_threadName}Execution has been canceled: {e.InnerException?.Message ?? e.Message}");
                    Cancel();
                }
            }

            LogExitCode(teExecutable, testDll, workingDir, arguments, exitCode, isTestOutputAvailable, streamingParser);

            _logger.DebugInfo(
                $"{_threadName}Reported {streamingParser.TestResults.Count} test results to VS during test execution, test DLL: '{testDll}'");
            UpdateTestDurations(streamingParser.TestResults);
            return exitCode;
        }

        private void LogExitCode(string teExecutable, string testDll, string workingDir, CommandLineGenerator.Args arguments, int exitCode,
            bool isTestOutputAvailable, StreamingTaefOutputParser streamingParser)
        {
            _logger.DebugInfo($"{_threadName}Exit code of TE.exe for test DLL '{testDll}': {exitCode} (0x{exitCode:X8})");
            if (_canceled)
                return;

            string exitCodeDescription = TaefConstants.GetExitCodeDescription(exitCode);
            if (exitCodeDescription != null)
            {
                _logger.LogError($"{_threadName}Test DLL '{testDll}': TE.exe returned exit code 0x{exitCode:X8}: {exitCodeDescription}. See the preceding messages of TE.exe (if any), or execute TE.exe manually for details (working directory: '{workingDir}').");
                _logger.DebugInfo($"{_threadName}Command: \"{teExecutable}\" {arguments.CommandLine}");
                return;
            }

            // e.g. a setup fixture crashed TE.exe while running in process: the parser has reported the tests without result
            // as failed, and the run must not look green (if all tests have results, e.g. because a cleanup fixture of the
            // test DLL crashed, a warning is logged below)
            int nrOfTestsNotRun = streamingParser.TestCasesNotRun.Count;
            if (nrOfTestsNotRun > 0)
            {
                _logger.LogError($"{_threadName}Test DLL '{testDll}': TE.exe terminated abnormally with exit code 0x{exitCode:X8} while not running any of the tests " +
                                 $"to be run (e.g. because a setup or cleanup fixture crashed while running in process); {nrOfTestsNotRun} test(s) " +
                                 "have not been run and are reported as failed (see their error messages for TE.exe's last output).");
                _logger.DebugInfo($"{_threadName}Command: \"{teExecutable}\" {arguments.CommandLine}");
                return;
            }

            // int.MaxValue: failure to execute the process (which has been logged by the process executor)
            if (isTestOutputAvailable && !streamingParser.SummaryFound && exitCode != int.MaxValue)
            {
                string crashedTest = streamingParser.CrashedTestCase != null
                    ? $" while running test '{streamingParser.CrashedTestCase.DisplayName}'"
                    : "";
                _logger.LogWarning($"{_threadName}Test DLL '{testDll}': TE.exe terminated abnormally with exit code 0x{exitCode:X8}{crashedTest}");
            }
        }

        private void UpdateTestDurations(IList<TestResult> results)
        {
            if (!_recordDurations)
                return;

            try
            {
                new TestDurationSerializer().UpdateTestDurations(results);
            }
            catch (Exception e)
            {
                _logger.DebugWarning($"{_threadName}Could not update test durations: {e.Message}");
            }

            // repeated tests (option NrOfTestRepetitions) have several results
            IEnumerable<TestResult> firstResultsOfTests = results
                .Where(tr => tr.Outcome == TestOutcome.Passed || tr.Outcome == TestOutcome.Failed)
                .GroupBy(tr => tr.TestCase)
                .Select(g => g.First());
            foreach (TestResult result in firstResultsOfTests)
            {
                if (!_schedulingAnalyzer.AddActualDuration(result.TestCase, (int) result.Duration.TotalMilliseconds))
                    _logger.DebugWarning($"{_threadName}TestCase already in analyzer: {result.TestCase.FullyQualifiedName}");
            }
        }
    }

}
