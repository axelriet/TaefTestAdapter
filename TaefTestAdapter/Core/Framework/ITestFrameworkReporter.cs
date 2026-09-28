// This file has been modified for TAEF support.

using System.Collections.Generic;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.Framework
{

    /// <summary>
    /// Reports discovered tests, started tests and test results to the test framework (e.g. to Visual Studio's Test
    /// Explorer).
    /// </summary>
    public interface ITestFrameworkReporter
    {
        /// <summary>Reports discovered tests.</summary>
        void ReportTestsFound(IEnumerable<TestCase> testCases);

        /// <summary>Reports that the execution of tests has started.</summary>
        void ReportTestsStarted(IEnumerable<TestCase> testCases);

        /// <summary>Reports test results.</summary>

        /// <exception cref="TestRunCanceledException">if test execution has been canceled in the meantime</exception>
        void ReportTestResults(IEnumerable<TestResult> testResults);
    }

}