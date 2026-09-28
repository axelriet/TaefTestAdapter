// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestResults;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Creates the results of tests for which no result has been reported while TE.exe was running: from TE.exe console
    /// output which has not been parsed yet (if any), as "probably not run because of the crash of test X" if TE.exe
    /// crashed, and according to option <see cref="SettingsWrapper.MissingTestsReportMode"/> otherwise.
    /// </summary>
    public class TestResultCollector
    {
        private static readonly IReadOnlyList<string> TestsNotRunCauses = new List<string>
        {
            "A test run is repeated, but tests have changed in the meantime",
            "A test dependency has been removed or changed without Visual Studio noticing",
            "TE.exe could not be started or failed to run the test DLL (see the Tests output window)"
        };

        private readonly ILogger _logger;
        private readonly SettingsWrapper _settings;

        private readonly string _threadName;

        public TestResultCollector(ILogger logger, string threadName, SettingsWrapper settings)
        {
            _logger = logger;
            _threadName = threadName;
            _settings = settings;
        }

        /// <param name="testCasesRun">Tests which have been run, but for which no result has been reported yet.</param>
        /// <param name="consoleOutput">Console output of TE.exe which has not been parsed while TE.exe was running (may be empty).</param>
        /// <param name="crashedTestCase">The test which crashed TE.exe (if any).</param>
        public List<TestResult> CollectTestResults(IEnumerable<TestCase> testCasesRun, IList<string> consoleOutput, TestCase crashedTestCase)
        {
            var testResults = new List<TestResult>();
            TestCase[] arrTestCasesRun = testCasesRun as TestCase[] ?? testCasesRun.ToArray();

            if (consoleOutput != null && consoleOutput.Count > 0)
            {
                var consoleParser = new TaefOutputParser(arrTestCasesRun, consoleOutput, _logger, _threadName);
                CollectResultsFromConsoleOutput(consoleParser, testResults);
                if (crashedTestCase == null)
                    crashedTestCase = consoleParser.CrashedTestCase;
            }

            var remainingTestCases = arrTestCasesRun
                .Where(tc => !testResults.Exists(tr => tr.TestCase.FullyQualifiedName == tc.FullyQualifiedName))
                .ToArray();
            if (remainingTestCases.Length > 0)
            {
                if (crashedTestCase != null)
                    CreateMissingResults(remainingTestCases, crashedTestCase, testResults);
                else
                    ReportSuspiciousTestCases(remainingTestCases, testResults);
            }

            return testResults;
        }

        private void CollectResultsFromConsoleOutput(TaefOutputParser consoleParser, List<TestResult> testResults)
        {
            var consoleResults = consoleParser.GetTestResults();
            int nrOfCollectedTestResults = 0;
            foreach (TestResult testResult in consoleResults)
            {
                testResults.Add(testResult);
                nrOfCollectedTestResults++;
            }
            if (nrOfCollectedTestResults > 0)
                _logger.DebugInfo($"{_threadName}Collected {nrOfCollectedTestResults} test results from console output");
        }

        private void CreateMissingResults(TestCase[] testCases, TestCase crashedTestCase, List<TestResult> testResults)
        {
            var errorMessage = $"reason is probably a crash of test {crashedTestCase.DisplayName}";
            var errorStackTrace = string.IsNullOrEmpty(crashedTestCase.CodeFilePath)
                ? null
                : ErrorMessageParser.CreateStackTraceEntry("crash suspect", crashedTestCase.CodeFilePath, crashedTestCase.LineNumber.ToString());

            testResults.AddRange(testCases.Select(testCase =>
                CreateTestResult(testCase, TestOutcome.Skipped, errorMessage, errorStackTrace)));
            _logger.DebugInfo($"{_threadName}Created {testCases.Length} test results for tests which have not been run because of the crash of test {crashedTestCase.DisplayName}");
        }

        private void ReportSuspiciousTestCases(TestCase[] testCases, List<TestResult> testResults)
        {
            string causesAsString = $" - possible causes:{Environment.NewLine}{string.Join(Environment.NewLine, TestsNotRunCauses.Select(s => $"- {s}"))}";
            string testCasesAsString = string.Join(Environment.NewLine, testCases.Select(tc => tc.DisplayName));

            _logger.DebugWarning($"{_threadName}{testCases.Length} test cases seem to not have been run{causesAsString}");
            _logger.VerboseInfo($"{_threadName}Test cases:{Environment.NewLine}{testCasesAsString}");

            TestOutcome? testOutcome = GetTestOutcomeOfMissingTests();
            if (testOutcome.HasValue)
            {
                string errorMessage = $"Test case has not been run{causesAsString}";
                testResults.AddRange(testCases.Select(tc => CreateTestResult(tc, testOutcome.Value, errorMessage, null)));
            }
        }

        private TestOutcome? GetTestOutcomeOfMissingTests()
        {
            switch (_settings.MissingTestsReportMode)
            {
                case MissingTestsReportMode.DoNotReport:
                    return null;
                case MissingTestsReportMode.ReportAsFailed:
                    return TestOutcome.Failed;
                case MissingTestsReportMode.ReportAsSkipped:
                    return TestOutcome.Skipped;
                case MissingTestsReportMode.ReportAsNotFound:
                    return TestOutcome.NotFound;
                default:
                    throw new InvalidOperationException($"Unknown {nameof(MissingTestsReportMode)}: {_settings.MissingTestsReportMode}");
            }
        }

        private TestResult CreateTestResult(TestCase testCase, TestOutcome outcome, string errorMessage, string errorStackTrace)
        {
            return new TestResult(testCase)
            {
                ComputerName = Environment.MachineName,
                DisplayName = testCase.DisplayName,
                Outcome = outcome,
                ErrorMessage = errorMessage,
                ErrorStackTrace = errorStackTrace
            };
        }

    }
}
