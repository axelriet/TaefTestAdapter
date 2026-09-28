// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.Framework;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Test discoverer of the Test Adapter for TAEF: the VsTest framework (Visual Studio's Test Explorer, vstest.console.exe)
    /// passes all DLLs of the test run; the tests of those which are TAEF test DLLs are listed with TE.exe (see
    /// <see cref="TaefDiscoverer"/>), all other DLLs are skipped without starting any process.
    /// </summary>
    [DefaultExecutorUri(TestExecutor.ExecutorUriString)]
    [FileExtension(TaefConstants.TestDllExtension)]
    public class TestDiscoverer : ITestDiscoverer
    {
        private ILogger _logger;
        private SettingsWrapper _settings;
        private TaefDiscoverer _discoverer;

        // ReSharper disable once UnusedMember.Global
        public TestDiscoverer() : this(null, null) { }

        public TestDiscoverer(ILogger logger, SettingsWrapper settings)
        {
            _settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// Discovers the tests of all TAEF test DLLs among <paramref name="sources"/> and reports them to
        /// <paramref name="discoverySink"/>.
        /// </summary>
        /// <param name="sources">Paths of the DLLs to be checked for tests</param>
        /// <param name="discoveryContext">Provides the run settings</param>
        /// <param name="logger">Receives the adapter's log messages</param>
        /// <param name="discoverySink">Receives the tests found</param>
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext discoveryContext,
            IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            if (_settings == null || _settings.GetType() == typeof(SettingsWrapper)) // check whether we have a mock
            {
                CommonFunctions.CreateEnvironment(discoveryContext.RunSettings,
                   logger, out _logger, out _settings);
            }
            _discoverer = new TaefDiscoverer(_logger, _settings);

            CommonFunctions.LogVisualStudioVersion(_logger);

            _logger.LogInfo(Strings.Instance.TestDiscoveryStarting);
            _logger.DebugInfo($"Solution settings: {_settings}");
            if (_settings.SkipOriginCheck)
                _logger.LogWarning($"Option '{SettingsWrapper.OptionSkipOriginCheck}' is true - this might impose a security risk to your system");

            try
            {
                var reporter = new VsTestFrameworkReporter(discoverySink, _logger);
                _discoverer.DiscoverTests(sources, reporter);

                stopwatch.Stop();
                _logger.LogInfo($"Test discovery completed, overall duration: {stopwatch.Elapsed}");
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception while discovering tests: {e}");
            }

            CommonFunctions.ReportErrors(_logger, "test discovery", _settings.OutputMode, _settings.SummaryMode);
        }

    }

}
