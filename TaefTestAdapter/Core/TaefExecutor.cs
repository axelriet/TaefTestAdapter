using System.Linq;
using System.Collections.Generic;
using TaefTestAdapter.Common;
using TaefTestAdapter.Model;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Framework;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter
{

    /// <summary>
    /// Runs TAEF tests: each test DLL is run by TE.exe (see <see cref="TaefLocator"/>), sequentially or (if option
    /// <see cref="SettingsWrapper.ParallelTestExecution"/> is set and the tests are not being debugged) on several
    /// threads. Test starts and results are reported to the <see cref="ITestFrameworkReporter"/> while TE.exe is
    /// running.
    /// </summary>
    public class TaefExecutor
    {

        private readonly ILogger _logger;
        private readonly SettingsWrapper _settings;
        private readonly IDebuggedProcessExecutorFactory _processExecutorFactory;
        private readonly SchedulingAnalyzer _schedulingAnalyzer;

        private ITestRunner _runner;
        private bool _canceled;

        public TaefExecutor(ILogger logger, SettingsWrapper settings, IDebuggedProcessExecutorFactory processExecutorFactory)
        {
            _logger = logger;
            _settings = settings;
            _processExecutorFactory = processExecutorFactory;
            _schedulingAnalyzer = new SchedulingAnalyzer(logger);
        }


        /// <summary>
        /// Runs the given tests and blocks until all of them have been run (or execution has been canceled).
        /// </summary>
        /// <param name="testCasesToRun">The tests to be run (of any number of test DLLs).</param>
        /// <param name="reporter">Receives test starts and results.</param>
        /// <param name="isBeingDebugged">If true, TE.exe is started with the debugger attached (and tests are run in process).</param>
        public void RunTests(IEnumerable<TestCase> testCasesToRun, ITestFrameworkReporter reporter, bool isBeingDebugged)
        {
            TestCase[] testCasesToRunAsArray = testCasesToRun as TestCase[] ?? testCasesToRun.ToArray();
            _logger.LogInfo("Running " + testCasesToRunAsArray.Length + " tests...");

            lock (this)
            {
                if (_canceled)
                {
                    return;
                }
                ComputeTestRunner(reporter, isBeingDebugged);
            }

            _runner.RunTests(testCasesToRunAsArray, isBeingDebugged, _processExecutorFactory);

            if (_settings.ParallelTestExecution)
                _schedulingAnalyzer.PrintStatisticsToDebugOutput();
        }

        /// <summary>
        /// Cancels the test run: no further TE.exe processes are started; if option
        /// <see cref="SettingsWrapper.KillProcessesOnCancel"/> is set, running TE.exe processes are killed (including their
        /// test host processes).
        /// </summary>
        public void Cancel()
        {
            lock (this)
            {
                _canceled = true;
                _runner?.Cancel();
            }
        }

        private void ComputeTestRunner(ITestFrameworkReporter reporter, bool isBeingDebugged)
        {
            if (_settings.ParallelTestExecution && !isBeingDebugged)
            {
                _runner = new ParallelTestRunner(reporter, _logger, _settings, _schedulingAnalyzer);
            }
            else
            {
                _runner = new PreparingTestRunner(reporter, _logger, _settings, _schedulingAnalyzer);
                if (_settings.ParallelTestExecution && isBeingDebugged)
                {
                    _logger.DebugInfo(
                        "Parallel execution is selected in options, but tests are executed sequentially because debugger is attached.");
                }
            }
        }

    }

}