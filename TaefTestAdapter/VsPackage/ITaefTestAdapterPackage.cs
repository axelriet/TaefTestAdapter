// This file has been modified for TAEF support.

using System;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// The options of the adapter which can be switched from the toolbar. Setting a value changes (and persists)
    /// the according option of the options pages.
    /// </summary>
    public interface ITaefTestAdapterPackage : IServiceProvider
    {
        /// <summary>Option 'Break on error' (TAEF page).</summary>
        bool BreakOnError { get; set; }
        /// <summary>Option 'Parallel test execution' (Test Execution page).</summary>
        bool ParallelTestExecution { get; set; }
        /// <summary>Option 'Print test output' (General page).</summary>
        bool PrintTestOutput { get; set; }
        /// <summary>Option 'Run tests in process' (TAEF page).</summary>
        bool RunInProcess { get; set; }
    }
}
