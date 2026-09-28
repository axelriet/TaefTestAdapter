// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestCases;

namespace TaefTestAdapter
{
    /// <summary>
    /// Discovers the tests of TAEF test DLLs (see <see cref="TestCaseFactory"/>). DLLs are only considered if they are
    /// TAEF test DLLs (see <see cref="IsTaefTestDll"/>) and are trusted (see <see cref="VerifyTestDllTrust"/>).
    /// </summary>
    public class TaefDiscoverer
    {
        /// <summary>Timeout of the regexes of options 'Regex for test discovery' and 'Before/After test discovery'.</summary>
        public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);

        /// <summary>Maximum number of test DLLs discovered in parallel.</summary>
        public static readonly int MaxNrOfParallelDiscoveries = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));

        private readonly ILogger _logger;
        private readonly SettingsWrapper _settings;
        private readonly IDiaResolverFactory _diaResolverFactory;
        private readonly IProcessExecutorFactory _processExecutorFactory;

        public TaefDiscoverer(ILogger logger, SettingsWrapper settings, IProcessExecutorFactory processExecutorFactory = null, IDiaResolverFactory diaResolverFactory = null)
        {
            _logger = logger;
            _settings = settings;
            _processExecutorFactory = processExecutorFactory ?? new ProcessExecutorFactory();
            _diaResolverFactory = diaResolverFactory ?? DefaultDiaResolverFactory.Instance;
        }

        /// <summary>
        /// Discovers the tests of the TAEF test DLLs among <paramref name="testDlls"/> (in parallel, with the settings of
        /// the respective test DLL) and reports them to <paramref name="reporter"/>. Other files are skipped without
        /// starting any process.
        /// </summary>
        public void DiscoverTests(IEnumerable<string> testDlls, ITestFrameworkReporter reporter)
        {
            ForEachTestDllInParallel(testDlls, (testDll, settings) =>
            {
                int nrOfTestCases = 0;
                void ReportTestCase(TestCase testCase)
                {
                    reporter.ReportTestsFound(testCase.Yield());
                    _logger.VerboseInfo("Added test case " + testCase.DisplayName);
                    nrOfTestCases++;
                }

                if (GetTestsFromTestDll(testDll, settings, ReportTestCase) != null)
                    _logger.LogInfo($"Found {nrOfTestCases} tests in test DLL {testDll}");
            });
        }

        /// <summary>
        /// Lists the tests of the TAEF test DLLs among <paramref name="testDlls"/> (in parallel, with the settings of the
        /// respective test DLL). Other files are skipped without starting any process.
        /// </summary>
        /// <param name="testDlls">The test DLLs</param>
        /// <param name="isCanceled">If given and returning true, the remaining test DLLs are skipped</param>
        public IList<TestCase> GetTestsFromTestDlls(IEnumerable<string> testDlls, Func<bool> isCanceled = null)
        {
            var allTestCases = new List<TestCase>();
            ForEachTestDllInParallel(testDlls, (testDll, settings) =>
            {
                if (isCanceled != null && isCanceled())
                    return;

                IList<TestCase> testCases = GetTestsFromTestDll(testDll, settings, null);
                if (testCases == null)
                    return;

                _logger.LogInfo($"Found {testCases.Count} tests in test DLL {testDll}");
                lock (allTestCases)
                {
                    allTestCases.AddRange(testCases);
                }
            });
            return allTestCases;
        }

        /// <summary>
        /// Lists the tests of <paramref name="testDll"/> if it is a trusted TAEF test DLL (see <see cref="IsTaefTestDll"/>
        /// and <see cref="VerifyTestDllTrust"/>); returns an empty list otherwise. Settings are not switched to the
        /// project settings of <paramref name="testDll"/> (use <see cref="SettingsWrapper.ExecuteWithSettingsForTestDll"/>,
        /// or <see cref="GetTestsFromTestDlls"/>).
        /// </summary>
        public IList<TestCase> GetTestsFromTestDll(string testDll)
        {
            IList<TestCase> testCases = GetTestsFromTestDll(testDll, _settings, null);
            if (testCases == null)
                return new List<TestCase>();

            _logger.LogInfo($"Found {testCases.Count} tests in test DLL {testDll}");
            return testCases;
        }

        /// <returns>The tests of <paramref name="testDll"/>, or null if it is not a trusted TAEF test DLL.</returns>
        private IList<TestCase> GetTestsFromTestDll(string testDll, SettingsWrapper settings, Action<TestCase> reportTestCase)
        {
            if (!IsTaefTestDll(testDll, settings.TestDiscoveryRegex, _logger) || !VerifyTestDllTrust(testDll, settings, _logger))
                return null;

            var factory = new TestCaseFactory(testDll, _logger, settings, _diaResolverFactory, _processExecutorFactory);
            IList<TestCase> testCases = factory.CreateTestCases(reportTestCase);
            if (reportTestCase == null)
            {
                foreach (TestCase testCase in testCases)
                {
                    _logger.VerboseInfo("Added test case " + testCase.DisplayName);
                }
            }
            return testCases;
        }

        /// <summary>
        /// Executes <paramref name="action"/> for each test DLL with a copy of the settings switched to the settings of the
        /// test DLL. The test DLLs are processed by a limited number of dedicated threads: test discovery blocks while
        /// TE.exe is running, and reading the output of TE.exe requires thread pool threads, which must not be exhausted by
        /// waiting threads. Exceptions are logged.
        /// </summary>
        private void ForEachTestDllInParallel(IEnumerable<string> testDlls, Action<string, SettingsWrapper> action)
        {
            var remainingTestDlls = new ConcurrentQueue<string>(testDlls);
            int nrOfThreads = Math.Min(remainingTestDlls.Count, MaxNrOfParallelDiscoveries);
            var threads = new List<Thread>(nrOfThreads);
            for (int i = 0; i < nrOfThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    while (remainingTestDlls.TryDequeue(out string testDll))
                    {
                        try
                        {
                            SettingsWrapper settings = _settings.Clone();
                            settings.ExecuteWithSettingsForTestDll(testDll, _logger, () => action(testDll, settings));
                        }
                        catch (Exception e)
                        {
                            _logger.LogError($"Exception while discovering the tests of test DLL '{testDll}': {e.Message}");
                            _logger.DebugError($"Exception:{Environment.NewLine}{e}");
                        }
                    }
                })
                {
                    Name = $"TAEF test discovery {i}",
                    IsBackground = true
                };
                threads.Add(thread);
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }
        }

        /// <summary>
        /// Checks whether <paramref name="dll"/> is to be treated as TAEF test DLL:
        /// <list type="number">
        /// <item>if a file <c>&lt;dll&gt;.is_taef_test</c> exists, it is;</item>
        /// <item>otherwise, if <paramref name="customRegex"/> (option 'Regex for test discovery') is set, it is if the regex
        /// matches the full path of <paramref name="dll"/>;</item>
        /// <item>otherwise, it is if it contains TAEF test metadata (see <see cref="PeParser.IsTaefTestDll"/>).</item>
        /// </list>
        /// No process is started, and only the headers of the file are read.
        /// </summary>
        public static bool IsTaefTestDll(string dll, string customRegex, ILogger logger)
        {
            string indicatorFile = $"{dll}{TaefConstants.IndicatorFileExtension}";
            if (File.Exists(indicatorFile))
            {
                logger.DebugInfo($"TAEF indicator file found for test DLL {dll} ({indicatorFile})");
                return true;
            }

            if (string.IsNullOrWhiteSpace(customRegex))
            {
                if (PeParser.IsTaefTestDll(dll, logger))
                {
                    logger.DebugInfo($"TAEF test metadata found in test DLL {dll}");
                    return true;
                }
            }
            else
            {
                if (SafeMatches(dll, customRegex, logger))
                {
                    logger.DebugInfo($"Custom regex '{customRegex}' matches test DLL '{dll}'");
                    return true;
                }
            }

            logger.DebugInfo($"File does not seem to be a TAEF test DLL: '{dll}'");
            return false;
        }

        private static bool SafeMatches(string dll, string regex, ILogger logger)
        {
            bool matches = false;
            try
            {
                matches = Regex.IsMatch(dll, regex, RegexOptions.None, RegexTimeout);
            }
            catch (ArgumentException e)
            {
                logger.LogError($"Regex '{regex}' can not be parsed: {e.Message}");
            }
            catch (RegexMatchTimeoutException e)
            {
                logger.LogError($"Regex '{regex}' timed out: {e.Message}");
            }
            return matches;
        }

        /// <summary>
        /// Checks that <paramref name="testDll"/> has not been downloaded from another computer (the security zone is
        /// MyComputer), unless option 'Skip check of file origin' is set.
        /// </summary>
        public static bool VerifyTestDllTrust(string testDll, SettingsWrapper settings, ILogger logger)
        {
            if (settings.SkipOriginCheck)
                return true;

            var zone = Zone.CreateFromUrl(testDll);
            if (zone.SecurityZone != System.Security.SecurityZone.MyComputer)
            {
                logger.LogError("Test DLL " + testDll + " came from another computer and was blocked to help protect this computer.");
                return false;
            }
            return true;
        }

    }

}
