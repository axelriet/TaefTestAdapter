// This file has been modified for TAEF support.

using System.Collections.Generic;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.Scheduling
{
    /// <summary>
    /// Distributes tests to the threads of parallel test execution.
    /// </summary>
    public interface ITestsSplitter
    {
        /// <returns>One list of tests per thread.</returns>
        List<List<TestCase>> SplitTestcases();
    }

}
