// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.Linq;
using System.Collections.Generic;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using ExtensionUriAttribute = Microsoft.VisualStudio.TestPlatform.ObjectModel.ExtensionUriAttribute;
using System.Diagnostics;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.TestAdapter.Helpers;
using TaefTestAdapter.TestAdapter.Framework;
using TaefTestAdapter.TestAdapter.ProcessExecution;

namespace TaefTestAdapter.TestAdapter
{

    /// <summary>
    /// Test executor of the Test Adapter for TAEF: runs the tests of TAEF test DLLs with TE.exe (see
    /// <see cref="TaefExecutor"/>) and reports their results to the VsTest framework (Visual Studio's Test Explorer,
    /// vstest.console.exe).
    /// </summary>
    [ExtensionUri(ExecutorUriString)]
    public class TestExecutor : ITestExecutor
    {
        /// <summary>
        /// URI of the executor of the Test Adapter for TAEF; differs from the URI of the adapter shipped with TAEF
        /// (<c>executor://TaefTestAdapter</c>) so that both can be installed side by side.
        /// </summary>
        public const string ExecutorUriString = "executor://TestAdapterForTaef/v1";

        /// <summary>
        /// <see cref="ExecutorUriString"/> as <see cref="Uri"/>.
        /// </summary>
        public static readonly Uri ExecutorUri = new Uri(ExecutorUriString);

        private readonly object _lock = new object();

        private ILogger _logger;
        private SettingsWrapper _settings;
        private readonly IDebuggerAttacher _debuggerAttacher;
        private TaefExecutor _executor;

        private volatile bool _canceled;

        // ReSharper disable once UnusedMember.Global
        public TestExecutor() : this(null, null, null) { }

        public TestExecutor(ILogger logger, SettingsWrapper settings, IDebuggerAttacher debuggerAttacher)
        {
            _logger = logger;
            _settings = settings;
            _debuggerAttacher = debuggerAttacher;
        }


        /// <summary>
        /// Runs all tests of the TAEF test DLLs among <paramref name="sources"/> which match the test case filter of
        /// <paramref name="runContext"/> (if any). Other DLLs are skipped.
        /// </summary>
        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            try
            {
                TryRunTests(sources, runContext, frameworkHandle);
            }
            catch (Exception e)
            {
                LogError($"Exception while running tests: {e}", frameworkHandle);
            }

            ReportErrors();
        }

        /// <summary>
        /// Runs the given tests (which have been discovered by the <see cref="TestDiscoverer"/>).
        /// </summary>
        public void RunTests(IEnumerable<VsTestCase> vsTestCasesToRun, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            try
            {
                TryRunTests(vsTestCasesToRun, runContext, frameworkHandle);
            }
            catch (Exception e)
            {
                LogError($"Exception while running tests: {e}", frameworkHandle);
            }

            ReportErrors();
        }

        /// <summary>
        /// Cancels the test run: no further TE.exe processes are started, and running TE.exe processes are killed
        /// (together with their test host processes) if option <see cref="SettingsWrapper.KillProcessesOnCancel"/> is set.
        /// </summary>
        public void Cancel()
        {
            lock (_lock)
            {
                if (_canceled)
                    return;

                _canceled = true;
                _executor?.Cancel();
                _logger?.LogInfo("Test execution canceled.");
            }
        }

        private void TryRunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var stopwatch = StartStopWatchAndInitEnvironment(runContext, frameworkHandle);

            if (!AbleToRun(runContext))
                return;

            IList<TestCase> allTestCases = GetAllTestCasesInTestDlls(sources);
            if (_canceled)
                return;

            IList<TestCase> testCasesToRun = Filter(allTestCases.Select(tc => new TestCasePair(tc, tc.ToVsTestCase())), runContext);
            DoRunTests(testCasesToRun, runContext, frameworkHandle);

            stopwatch.Stop();
            _logger.LogInfo($"Test execution completed, overall duration: {stopwatch.Elapsed}.");
        }

        private void TryRunTests(IEnumerable<VsTestCase> vsTestCasesToRun, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var stopwatch = StartStopWatchAndInitEnvironment(runContext, frameworkHandle);

            if (!AbleToRun(runContext))
                return;

            IList<TestCase> testCasesToRun = Filter(vsTestCasesToRun.Select(vtc => new TestCasePair(vtc.ToTestCase(), vtc)), runContext);
            DoRunTests(testCasesToRun, runContext, frameworkHandle);

            stopwatch.Stop();
            _logger.LogInfo($"Test execution completed, overall duration: {stopwatch.Elapsed}.");
        }

        private Stopwatch StartStopWatchAndInitEnvironment(IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            InitOrRefreshEnvironment(runContext.RunSettings, frameworkHandle, runContext);

            CommonFunctions.LogVisualStudioVersion(_logger);

            _logger.LogInfo(Strings.Instance.TestExecutionStarting);
            _logger.DebugInfo($"Solution settings: {_settings}");

            return stopwatch;
        }

        private void InitOrRefreshEnvironment(IRunSettings runSettings, IMessageLogger messageLogger, IRunContext runContext)
        {
            if (_settings == null || _settings.GetType() == typeof(SettingsWrapper)) // the latter prevents test settings and logger from being replaced
                CommonFunctions.CreateEnvironment(runSettings, messageLogger, out _logger, out _settings, runContext.SolutionDirectory);
        }

        /// <summary>
        /// Debugging with the native debugger engines requires the debugger attacher service of the VS package (which
        /// attaches the debugger to TE.exe); the package publishes the service's pipe id only if the service is available.
        /// The VsTest framework's debugger is always available.
        /// </summary>
        private bool AbleToRun(IRunContext runContext)
        {
            if (runContext.IsBeingDebugged)
            {
                DebuggerKind debuggerKind = _settings.DebuggerKind;
                _logger.DebugInfo($"Tests are being debugged, debugger engine: {debuggerKind.ToReadableString()}");
                if (debuggerKind > DebuggerKind.VsTestFramework && !IsDebuggerAttacherServiceAvailable())
                {
                    _logger.LogError(
                        $"Debugging with debugger engine '{debuggerKind.ToReadableString()}' is only possible if the {Strings.Instance.ExtensionName} has been installed into Visual Studio and its debugger attacher service is running. " +
                        "Either the adapter has not been installed as Visual Studio extension (NuGet installation does not support this, nor other features such as Visual Studio Options, toolbar, and solution settings), " +
                        "or the extension could not start the service (see Visual Studio's activity log, written if Visual Studio is started with option /log). " +
                        $"Set option '{SettingsWrapper.OptionDebuggerKind}' to '{DebuggerKindConverter.VsTestFramework}' " +
                        $"(<{nameof(ITaefTestAdapterSettings.DebuggerKind)}>{nameof(DebuggerKind.VsTestFramework)}</{nameof(ITaefTestAdapterSettings.DebuggerKind)}> in a .runsettings file) " +
                        "to debug with the VsTest framework's debugger.");
                    return false;
                }
            }

            return true;
        }

        private IList<TestCase> GetAllTestCasesInTestDlls(IEnumerable<string> testDlls)
        {
            var discoverer = new TaefDiscoverer(_logger, _settings);
            IList<TestCase> testCases = discoverer.GetTestsFromTestDlls(testDlls.Distinct(StringComparer.OrdinalIgnoreCase), () => _canceled);
            return _canceled ? new List<TestCase>() : testCases;
        }

        /// <returns>The tests matching the test case filter of <paramref name="runContext"/> (if any).</returns>
        private IList<TestCase> Filter(IEnumerable<TestCasePair> testCasePairs, IRunContext runContext)
        {
            TestCasePair[] pairs = testCasePairs.ToArray();

            ISet<string> allTraitNames = GetAllTraitNames(pairs.Select(p => p.TestCase));
            var filter = new TestCaseFilter(runContext, allTraitNames, _logger);
            var matchingVsTestCases = new HashSet<VsTestCase>(filter.Filter(pairs.Select(p => p.VsTestCase)));

            return pairs
                .Where(p => matchingVsTestCases.Contains(p.VsTestCase))
                .Select(p => p.TestCase)
                .ToList();
        }

        private ISet<string> GetAllTraitNames(IEnumerable<TestCase> testCases)
        {
            var allTraitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TestCase testCase in testCases)
            {
                foreach (Trait trait in testCase.Traits)
                {
                    allTraitNames.Add(trait.Name);
                }
            }
            return allTraitNames;
        }

        private bool IsDebuggerAttacherServiceAvailable()
        {
            return _settings.DebuggingNamedPipeId != null;
        }

        private void DoRunTests(ICollection<TestCase> testCasesToRun, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            if (testCasesToRun.Count == 0)
            {
                _logger.DebugInfo("No tests to be run");
                return;
            }

            bool isRunningInsideVisualStudio = !string.IsNullOrEmpty(runContext.SolutionDirectory);
            var reporter = new VsTestFrameworkReporter(frameworkHandle, isRunningInsideVisualStudio, _logger);

            var debuggerAttacher = _debuggerAttacher ?? new MessageBasedDebuggerAttacher(_settings.DebuggingNamedPipeId, _logger);
            var processExecutorFactory = new DebuggedProcessExecutorFactory(frameworkHandle, debuggerAttacher);

            lock (_lock)
            {
                if (_canceled)
                    return;

                _executor = new TaefExecutor(_logger, _settings, processExecutorFactory);
            }
            _executor.RunTests(testCasesToRun, reporter, runContext.IsBeingDebugged);
            reporter.AllTestsFinished();
        }

        private void LogError(string message, IMessageLogger messageLogger)
        {
            if (_logger != null)
                _logger.LogError(message);
            else
                messageLogger?.SendMessage(TestMessageLevel.Error, message);
        }

        private void ReportErrors()
        {
            if (_logger != null && _settings != null)
                CommonFunctions.ReportErrors(_logger, "test execution", _settings.OutputMode, _settings.SummaryMode);
        }

        /// <summary>A test in both representations (the adapter's and the one of the VsTest framework).</summary>
        private class TestCasePair
        {
            public TestCase TestCase { get; }
            public VsTestCase VsTestCase { get; }

            public TestCasePair(TestCase testCase, VsTestCase vsTestCase)
            {
                TestCase = testCase;
                VsTestCase = vsTestCase;
            }
        }

    }

}
