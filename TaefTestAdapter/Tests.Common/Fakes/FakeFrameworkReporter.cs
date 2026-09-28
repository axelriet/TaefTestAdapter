// This file has been modified for TAEF support.

using System.Collections.Generic;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.Tests.Common.Fakes
{
    /// <summary>
    /// Records everything reported to it (thread-safe: discovery and parallel execution report from several threads).
    /// </summary>
    public class FakeFrameworkReporter : ITestFrameworkReporter
    {
        private readonly object _lock = new object();

        private readonly List<TestCase> _reportedTestCasesFound = new List<TestCase>();
        private readonly List<TestCase> _reportedTestCasesStarted = new List<TestCase>();
        private readonly List<TestResult> _reportedTestResults = new List<TestResult>();

        /// <summary>Snapshot of the test cases reported by <see cref="ReportTestsFound"/>.</summary>
        public IList<TestCase> ReportedTestCasesFound { get { lock (_lock) return new List<TestCase>(_reportedTestCasesFound); } }

        /// <summary>Snapshot of the test cases reported by <see cref="ReportTestsStarted"/>.</summary>
        public IList<TestCase> ReportedTestCasesStarted { get { lock (_lock) return new List<TestCase>(_reportedTestCasesStarted); } }

        /// <summary>Snapshot of the test results reported by <see cref="ReportTestResults"/>.</summary>
        public IList<TestResult> ReportedTestResults { get { lock (_lock) return new List<TestResult>(_reportedTestResults); } }

        public void ReportTestsFound(IEnumerable<TestCase> testCases)
        {
            lock (_lock)
                _reportedTestCasesFound.AddRange(testCases);
        }

        public void ReportTestsStarted(IEnumerable<TestCase> testCases)
        {
            lock (_lock)
                _reportedTestCasesStarted.AddRange(testCases);
        }

        public void ReportTestResults(IEnumerable<TestResult> testResults)
        {
            lock (_lock)
                _reportedTestResults.AddRange(testResults);
        }
    }

}
