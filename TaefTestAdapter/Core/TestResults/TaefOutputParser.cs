// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Parses the complete console output of a TE.exe test run (see <see cref="StreamingTaefOutputParser"/>) without
    /// reporting the test starts and results to a test framework.
    /// </summary>
    public class TaefOutputParser
    {
        private class DummyTestFrameworkReporter : ITestFrameworkReporter
        {
            public void ReportTestResults(IEnumerable<TestResult> testResults)
            {
            }

            public void ReportTestsFound(IEnumerable<TestCase> testCases)
            {
            }

            public void ReportTestsStarted(IEnumerable<TestCase> testCases)
            {
            }
        }

        /// <summary>The test which was running when the output ended, or null (available after <see cref="GetTestResults"/>).</summary>
        public TestCase CrashedTestCase { get; private set; }

        /// <summary>True if the output contains TE.exe's final summary line (available after <see cref="GetTestResults"/>).</summary>
        public bool SummaryFound { get; private set; }

        private readonly List<string> _consoleOutput;
        private readonly List<TestCase> _testCasesRun;
        private readonly ILogger _logger;
        private readonly string _threadName;

        public TaefOutputParser(IEnumerable<TestCase> testCasesRun, IEnumerable<string> consoleOutput, ILogger logger, string threadName = "")
        {
            _consoleOutput = consoleOutput.ToList();
            _testCasesRun = testCasesRun.ToList();
            _logger = logger;
            _threadName = threadName;
        }

        public IList<TestResult> GetTestResults()
        {
            var streamingParser = new StreamingTaefOutputParser(_testCasesRun, _logger, new DummyTestFrameworkReporter(), _threadName);
            _consoleOutput.ForEach(streamingParser.ReportLine);
            streamingParser.Flush();

            CrashedTestCase = streamingParser.CrashedTestCase;
            SummaryFound = streamingParser.SummaryFound;
            return streamingParser.TestResults;
        }
    }

}
