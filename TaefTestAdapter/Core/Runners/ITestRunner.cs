// This file has been modified for TAEF support.

using System.Collections.Generic;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution.Contracts;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Runs tests and reports their results.
    /// </summary>
    public interface ITestRunner
    {
        /// <summary>
        /// Runs <paramref name="testCasesToRun"/> (under the debugger if <paramref name="isBeingDebugged"/>), starting
        /// processes with <paramref name="processExecutorFactory"/>.
        /// </summary>
        void RunTests(IEnumerable<TestCase> testCasesToRun, bool isBeingDebugged, 
            IDebuggedProcessExecutorFactory processExecutorFactory);

        /// <summary>Cancels the test run.</summary>
        void Cancel();
    }

}