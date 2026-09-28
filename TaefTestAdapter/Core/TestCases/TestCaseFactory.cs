// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// Creates the test cases of a TAEF test DLL: lists the tests with <c>TE.exe "&lt;test DLL&gt;" &lt;additional TE.exe
    /// arguments&gt; /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false</c>, locates the
    /// test methods in the PDBs (option 'Parse symbol information') and assigns traits (TAEF metadata and the regex
    /// options 'Before/After test discovery').
    /// </summary>
    public class TestCaseFactory
    {
        private const int ExecutionFailed = int.MaxValue;
        private const string NotATaefTestDllMessage = "not recognized to be a TAEF test";
        private const int MaxNrOfTestsListedInWarnings = 5;

        private readonly ILogger _logger;
        private readonly SettingsWrapper _settings;
        private readonly string _testDll;
        private readonly IDiaResolverFactory _diaResolverFactory;
        private readonly IProcessExecutorFactory _processExecutorFactory;

        public TestCaseFactory(string testDll, ILogger logger, SettingsWrapper settings,
            IDiaResolverFactory diaResolverFactory, IProcessExecutorFactory processExecutorFactory)
        {
            _logger = logger;
            _settings = settings;
            _testDll = testDll;
            _diaResolverFactory = diaResolverFactory ?? DefaultDiaResolverFactory.Instance;
            _processExecutorFactory = processExecutorFactory ?? new ProcessExecutorFactory();
        }

        /// <summary>
        /// Lists the tests of the test DLL with TE.exe and creates the test cases. Nothing is returned (and reported) if
        /// no TE.exe can be found, if TE.exe fails or if test discovery times out (option 'Test discovery timeout in s').
        /// </summary>
        /// <param name="reportTestCase">Called for every test case once all test cases have been created.</param>
        public IList<TestCase> CreateTestCases(Action<TestCase> reportTestCase = null)
        {
            string teExecutable = TaefLocator.FindTeExecutable(_testDll, _settings, _logger);
            if (teExecutable == null)
                return new List<TestCase>(); // TaefLocator has logged the problem

            IList<TestCaseDescriptor> descriptors = ListTests(teExecutable);
            return descriptors == null
                ? new List<TestCase>()
                : CreateTestCasesFromDescriptors(descriptors, reportTestCase);
        }

        /// <summary>
        /// Creates the test cases for tests listed by TE.exe: resolves the source locations (if option 'Parse symbol
        /// information' is set), assigns traits and adds the <see cref="TestCaseMetaDataProperty"/> (number of tests of
        /// the test's class and of the test DLL). Tests listed more than once (module-level data) are created once.
        /// </summary>
        /// <param name="reportTestCase">Called for every test case once all test cases have been created.</param>
        public IList<TestCase> CreateTestCasesFromDescriptors(IEnumerable<TestCaseDescriptor> descriptors, Action<TestCase> reportTestCase = null)
        {
            List<TestCaseDescriptor> uniqueDescriptors = GetUniqueDescriptors(descriptors);

            string symbolsProblem = null;
            IDictionary<string, TestCaseLocation> locations = _settings.ParseSymbolInformation
                ? FindTestCaseLocations(uniqueDescriptors, out symbolsProblem)
                : null;

            var traitsFactory = new TraitsFactory(_settings, _logger);
            var testCases = new List<TestCase>(uniqueDescriptors.Count);
            var classNames = new List<string>(uniqueDescriptors.Count);
            var nrOfTestCasesPerClass = new Dictionary<string, int>();
            foreach (TestCaseDescriptor descriptor in uniqueDescriptors)
            {
                TestCaseLocation location = null;
                locations?.TryGetValue(GetTestMethodName(descriptor), out location);

                var testCase = new TestCase(descriptor.Name, _testDll, descriptor.Name, location?.Sourcefile ?? "", (int)(location?.Line ?? 0));
                TestCaseDataRowIndexProperty dataRowIndex = GetDataRowIndex(descriptor);
                if (dataRowIndex != null)
                    testCase.Properties.Add(dataRowIndex);
                testCase.Traits.AddRange(traitsFactory.GetFinalTraits(descriptor.Name, GetTaefTraits(descriptor)));
                testCases.Add(testCase);

                string className = GetClassName(descriptor);
                classNames.Add(className);
                nrOfTestCasesPerClass.TryGetValue(className, out int nrOfTestCasesOfClass);
                nrOfTestCasesPerClass[className] = nrOfTestCasesOfClass + 1;
            }

            if (locations != null)
                LogMissingLocations(uniqueDescriptors, locations, symbolsProblem);

            for (int i = 0; i < testCases.Count; i++)
            {
                testCases[i].Properties.Add(new TestCaseMetaDataProperty(nrOfTestCasesPerClass[classNames[i]], testCases.Count));
                reportTestCase?.Invoke(testCases[i]);
            }

            return testCases;
        }

        /// <returns>
        /// True if the TAEF property <paramref name="propertyName"/> is reported as trait, i.e. if it is none of
        /// <see cref="TaefConstants.PropertiesNotReportedAsTraits"/> and does not start with
        /// <see cref="TaefConstants.DataPropertyPrefix"/> (ignoring case).
        /// </returns>
        public static bool IsReportedAsTrait(string propertyName)
        {
            return !TaefConstants.PropertiesNotReportedAsTraits.Contains(propertyName, StringComparer.OrdinalIgnoreCase)
                && !propertyName.StartsWith(TaefConstants.DataPropertyPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <returns>
        /// The index of the test's data row (its data value <see cref="TaefConstants.DataRowIndexName"/>, e.g. <c>Data[Index] = 2</c>),
        /// or null if it has none (e.g. a lightweight data row or a test which is not data-driven) or if the value is not a
        /// number (see <see cref="TestCaseDataRowIndexProperty.TryParse"/>). If both class and method are data-driven, it
        /// is the index of the method's row (as for TE.exe).
        /// </returns>
        public static TestCaseDataRowIndexProperty GetDataRowIndex(TestCaseDescriptor descriptor)
        {
            TaefProperty index = descriptor.Data.LastOrDefault(d => string.Equals(d.Name, TaefConstants.DataRowIndexName, StringComparison.OrdinalIgnoreCase));
            return index == null ? null : TestCaseDataRowIndexProperty.TryParse(index.Value);
        }

        /// <returns>The traits of a test resulting from its TAEF metadata (see <see cref="IsReportedAsTrait"/>).</returns>
        public static IList<Trait> GetTaefTraits(TestCaseDescriptor descriptor)
        {
            return descriptor.Properties
                .Where(p => IsReportedAsTrait(p.Name))
                .Select(p => new Trait(p.Name, p.Value))
                .ToList();
        }

        #region Listing the tests

        private IList<TestCaseDescriptor> ListTests(string teExecutable)
        {
            string workingDir = _settings.GetWorkingDirForDiscovery(_testDll);
            string arguments = GetDiscoveryArguments(workingDir);
            string pathExtension = _settings.GetPathExtension(_testDll);
            IDictionary<string, string> environmentVariables = _settings.GetEnvironmentVariablesForDiscovery(_testDll);

            var standardOutput = new List<string>();
            var descriptors = new List<TestCaseDescriptor>();
            var parser = new StreamingListPropertiesParser();
            parser.TestCaseDescriptorCreated += (sender, args) => descriptors.Add(args.TestCaseDescriptor);

            void OnReportOutputLine(string line)
            {
                lock (parser)
                {
                    standardOutput.Add(line);
                    parser.ReportLine(line);
                }
            }

            try
            {
                int processExitCode = ExecutionFailed;
                IProcessExecutor executor = _processExecutorFactory.CreateExecutor(false, _logger);
                // dedicated thread: waiting for a thread pool thread must not count against the discovery timeout
                var listTestsTask = Task.Factory.StartNew(() =>
                {
                    _logger.VerboseInfo($"Starting test discovery for {_testDll}");
                    processExitCode = executor.ExecuteCommandBlocking(
                        teExecutable,
                        arguments,
                        workingDir,
                        pathExtension,
                        environmentVariables,
                        OnReportOutputLine);
                    _logger.VerboseInfo($"Finished execution of TE.exe for {_testDll}");
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                _logger.VerboseInfo($"Scheduled test discovery for {_testDll}");

                if (!listTestsTask.Wait(GetDiscoveryTimeoutInMs()))
                {
                    executor.Cancel();
                    List<string> outputSoFar;
                    lock (parser)
                    {
                        outputSoFar = standardOutput.ToList();
                    }
                    LogTimeoutError(teExecutable, arguments, workingDir, outputSoFar);
                    return null;
                }

                lock (parser)
                {
                    parser.Flush();
                }

                if (!CheckProcessExitCode(processExitCode, standardOutput, teExecutable, arguments, workingDir))
                    return null;

                LogTeMessages(parser, descriptors.Count);
                return descriptors;
            }
            catch (Exception e)
            {
                LogDiscoveryError(teExecutable, arguments, workingDir, e);
                return null;
            }
        }

        /// <returns>
        /// <c>"&lt;test DLL&gt;" &lt;additional TE.exe arguments&gt; /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false</c>.
        /// TE.exe can not open test DLLs whose path contains non-ASCII characters, so the test DLL is passed as an
        /// alternative path consisting of ASCII characters then (relative to <paramref name="workingDir"/>, the working
        /// directory of TE.exe, or its 8.3 short path, see <see cref="TeArguments.GetTestDllArgument(string,string,ILogger)"/>);
        /// everything else (e.g. the source of the test cases) keeps the original path.
        /// </returns>
        private string GetDiscoveryArguments(string workingDir)
        {
            string arguments = $"\"{TeArguments.GetTestDllArgument(_testDll, workingDir, _logger)}\"";

            // user parameters come before the adapter's switches, so that e.g. a /select given by the user restricts discovery
            string userParams = _settings.GetUserParametersForDiscovery(_testDll);
            if (!string.IsNullOrWhiteSpace(userParams))
                arguments += $" {userParams.Trim()}";

            return $"{arguments} {TaefConstants.ListPropertiesOption} {TaefConstants.RunIgnoredTestsOption} {TaefConstants.OutputFormatOptions}";
        }

        private int GetDiscoveryTimeoutInMs()
        {
            int timeoutInSeconds = _settings.TestDiscoveryTimeoutInSeconds;
            return timeoutInSeconds <= 0 || timeoutInSeconds >= int.MaxValue / 1000
                ? Timeout.Infinite
                : timeoutInSeconds * 1000;
        }

        private void LogTeMessages(StreamingListPropertiesParser parser, int nrOfTests)
        {
            foreach (string error in parser.Errors)
            {
                if (error.Contains(NotATaefTestDllMessage))
                {
                    string message = $"TE.exe does not recognize '{_testDll}' as TAEF test DLL: {error}";
                    if (PeParser.IsTaefTestDll(_testDll, null))
                        _logger.LogWarning(message);
                    else
                        _logger.DebugWarning(message);
                }
                else
                {
                    _logger.LogError($"TE.exe reported an error while listing the tests of test DLL '{_testDll}': {error}");
                }
            }

            foreach (string warning in parser.Warnings)
            {
                _logger.LogWarning($"TE.exe reported a warning while listing the tests of test DLL '{_testDll}': {warning}");
            }

            if (parser.UnexpectedLines.Count > 0)
            {
                // e.g. continuations of TAEF property values containing empty lines, which make TE.exe's listing ambiguous
                _logger.LogWarning($"TE.exe printed {parser.UnexpectedLines.Count} unexpected line(s) while listing the tests of test DLL '{_testDll}', " +
                                   $"the first one being '{parser.UnexpectedLines[0].Trim()}'. Some tests or their traits may be missing or wrong " +
                                   "(e.g. because a TAEF property or data value contains empty lines).");
                foreach (string line in parser.UnexpectedLines)
                {
                    _logger.DebugWarning($"Unexpected output of TE.exe while listing the tests of test DLL '{_testDll}': {line}");
                }
            }

            if (nrOfTests == 0 && parser.Errors.Count == 0)
                _logger.DebugInfo($"TE.exe did not list any tests for test DLL '{_testDll}'");
        }

        private string GetCommand(string teExecutable, string arguments)
        {
            return $"\"{teExecutable}\" {arguments}";
        }

        private void LogTimeoutError(string teExecutable, string arguments, string workingDir, IList<string> outputSoFar)
        {
            string message =
                $"Test discovery was cancelled after {_settings.TestDiscoveryTimeoutInSeconds}s for test DLL '{_testDll}'";
            string output = outputSoFar.Any()
                ? $"Output of TE.exe so far:{Environment.NewLine}{string.Join(Environment.NewLine, outputSoFar)}"
                : "TE.exe produced no output.";
            string hint =
                $"Hint: test whether the following commands can be executed successfully on the command line (make sure all required binaries are on the PATH):{Environment.NewLine}" +
                $"cd \"{workingDir}\"{Environment.NewLine}" +
                GetCommand(teExecutable, arguments);

            _logger.LogError(message);
            _logger.DebugError(output);
            _logger.DebugError(hint);
        }

        private bool CheckProcessExitCode(int processExitCode, ICollection<string> standardOutput, string teExecutable, string arguments, string workingDir)
        {
            if (processExitCode == 0)
                return true;

            string description = TaefConstants.GetExitCodeDescription(processExitCode);
            string message =
                $"Could not list the tests of test DLL '{_testDll}': TE.exe returned with exit code {processExitCode} (0x{processExitCode:X8})";
            if (description != null)
                message += $" - {description}";
            message += $"{Environment.NewLine}Command executed: '{GetCommand(teExecutable, arguments)}', working directory: '{workingDir}'";
            message += standardOutput.Any(s => !string.IsNullOrWhiteSpace(s))
                ? $"{Environment.NewLine}Output of command:{Environment.NewLine}{string.Join(Environment.NewLine, standardOutput)}"
                : $"{Environment.NewLine}Command produced no output";

            _logger.LogError(message);
            return false;
        }

        private void LogDiscoveryError(string teExecutable, string arguments, string workingDir, Exception exception)
        {
            if (exception is AggregateException aggregateException)
                exception = aggregateException.Flatten().InnerExceptions.Count == 1 ? aggregateException.Flatten().InnerException : aggregateException;

            _logger.LogError($"Failed to list the tests of test DLL '{_testDll}' with TE.exe: {exception?.Message}");
            _logger.DebugError($"Exception:{Environment.NewLine}{exception}");
            _logger.LogError(
                $"{Strings.Instance.TroubleShootingLink}{Environment.NewLine}" +
                $"In particular: launch command prompt, change into directory '{workingDir}', and execute the following command to make sure your tests can be listed in general.{Environment.NewLine}" +
                GetCommand(teExecutable, arguments));
        }

        #endregion

        #region Test case creation

        private List<TestCaseDescriptor> GetUniqueDescriptors(IEnumerable<TestCaseDescriptor> descriptors)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<TestCaseDescriptor>();
            foreach (TestCaseDescriptor descriptor in descriptors)
            {
                if (names.Add(descriptor.Name))
                    result.Add(descriptor);
                else
                    _logger.DebugInfo($"Test '{descriptor.Name}' is listed more than once by TE.exe (e.g. because of module-level data), reporting it once");
            }
            return result;
        }

        /// <returns>
        /// The C++ name of the method to be located for the test (for data source errors of classes, a function generated
        /// for the class). The class as listed by TE.exe is used since test names of data-driven tests may be ambiguous.
        /// </returns>
        private static string GetTestMethodName(TestCaseDescriptor descriptor)
        {
            string name = descriptor.Name;
            if (descriptor.IsClassDataSourceError && name.EndsWith(TaefConstants.DataSourceErrorSuffix, StringComparison.Ordinal))
            {
                string className = name.Substring(0, name.Length - TaefConstants.DataSourceErrorSuffix.Length);
                return className + TaefConstants.ScopeSeparator + TestCaseResolver.TestClassInfoFunctionName;
            }

            string classPrefix = descriptor.ClassName + TaefConstants.ScopeSeparator;
            if (descriptor.ClassName.Length > 0 && name.StartsWith(classPrefix, StringComparison.Ordinal))
            {
                // C++ names do not contain '#': the class row starts at the first '#' of the class, the method row at the first '#' after the class
                string classBase = RemoveDataRow(descriptor.ClassName);
                string method = RemoveDataRow(name.Substring(classPrefix.Length));
                return classBase + TaefConstants.ScopeSeparator + method;
            }

            return TaefNames.GetBaseName(name);
        }

        private static string RemoveDataRow(string name)
        {
            int index = name.IndexOf(TaefConstants.DataRowSeparator);
            return index < 0 ? name : name.Substring(0, index);
        }

        /// <returns>
        /// The class the test is counted for in its <see cref="TestCaseMetaDataProperty"/>: always derived from the test's
        /// name (<see cref="TaefNames.GetClassName"/>), since test execution does the same.
        /// </returns>
        private string GetClassName(TestCaseDescriptor descriptor)
        {
            string className = TaefNames.GetClassName(descriptor.Name);
            if (!descriptor.IsClassDataSourceError && className != descriptor.ClassName)
            {
                _logger.DebugWarning($"The class of test '{descriptor.Name}' is '{descriptor.ClassName}' according to TE.exe, but '{className}' according to its name " +
                                     "(the names of data-driven classes with named rows are ambiguous)");
            }
            return className;
        }

        /// <param name="symbolsProblem">
        /// Receives an explanation why source locations may be missing (the PDB of the test DLL could not be found, or an
        /// exception occurred), or null.
        /// </param>
        private IDictionary<string, TestCaseLocation> FindTestCaseLocations(IList<TestCaseDescriptor> descriptors, out string symbolsProblem)
        {
            symbolsProblem = null;
            if (descriptors.Count == 0)
                return new Dictionary<string, TestCaseLocation>();

            var resolver = new TestCaseResolver(_testDll, _diaResolverFactory, _settings, _logger);
            try
            {
                IDictionary<string, TestCaseLocation> locations = resolver.FindTestCaseLocations(descriptors.Select(GetTestMethodName));
                if (resolver.TestDllPdb == null)
                    symbolsProblem = $"The PDB of the test DLL could not be found - make sure that it is located next to the test DLL (or use option '{SettingsWrapper.OptionAdditionalPdbs}').";
                return locations;
            }
            catch (Exception e)
            {
                // e.g. an unreadable PDB: do not claim that the PDB could not be found
                _logger.DebugWarning($"Exception while resolving the source locations of the tests of test DLL '{_testDll}':{Environment.NewLine}{e}");
                symbolsProblem = $"Resolving the source locations failed: {e.Message}";
                return new Dictionary<string, TestCaseLocation>();
            }
        }

        private void LogMissingLocations(IList<TestCaseDescriptor> descriptors, IDictionary<string, TestCaseLocation> locations, string symbolsProblem)
        {
            List<TestCaseDescriptor> testsWithoutLocation = descriptors
                .Where(d => !locations.ContainsKey(GetTestMethodName(d)))
                .ToList();
            if (testsWithoutLocation.Count == 0)
                return;

            foreach (TestCaseDescriptor descriptor in testsWithoutLocation)
            {
                _logger.DebugWarning($"Could not find source location for test {descriptor.Name}, test DLL: {_testDll}");
            }

            string examples = string.Join(", ", testsWithoutLocation.Take(MaxNrOfTestsListedInWarnings).Select(d => d.Name));
            if (testsWithoutLocation.Count > MaxNrOfTestsListedInWarnings)
                examples += ", ...";
            string message = $"Could not find source locations for {testsWithoutLocation.Count} of {descriptors.Count} tests of test DLL '{_testDll}': {examples}";
            if (symbolsProblem != null)
                message += Environment.NewLine + symbolsProblem;
            _logger.LogWarning(message);
        }

        #endregion

        /// <summary>
        /// Builds the final traits of tests in 3 phases: traits of option 'Before test discovery' (only for trait names
        /// not assigned in the later phases), the tests' TAEF metadata (without traits overridden in the last phase) and
        /// traits of option 'After test discovery'. The regexes are matched against the TAEF names of the tests.
        /// </summary>
        private class TraitsFactory
        {
            private readonly ILogger _logger;
            private readonly List<Tuple<Regex, Trait>> _traitsBefore;
            private readonly List<Tuple<Regex, Trait>> _traitsAfter;

            public TraitsFactory(SettingsWrapper settings, ILogger logger)
            {
                _logger = logger;
                _traitsBefore = CreateRegexes(settings.TraitsRegexesBefore);
                _traitsAfter = CreateRegexes(settings.TraitsRegexesAfter);
            }

            public IList<Trait> GetFinalTraits(string displayName, IList<Trait> traits)
            {
                if (_traitsBefore.Count == 0 && _traitsAfter.Count == 0)
                    return traits;

                var afterTraits = _traitsAfter
                    .Where(p => IsMatch(p.Item1, displayName))
                    .Select(p => p.Item2)
                    .ToArray();

                var namesOfAfterTraits = afterTraits
                    .Select(t => t.Name)
                    .Distinct()
                    .ToArray();

                var namesOfTestAndAfterTraits = namesOfAfterTraits
                    .Union(traits.Select(t => t.Name))
                    .Distinct()
                    .ToArray();

                var beforeTraits = _traitsBefore
                    .Where(p =>
                        !namesOfTestAndAfterTraits.Contains(p.Item2.Name)
                        && IsMatch(p.Item1, displayName))
                    .Select(p => p.Item2);

                var testTraits = traits
                    .Where(t => !namesOfAfterTraits.Contains(t.Name));

                var finalTraits = new List<Trait>();
                finalTraits.AddRange(beforeTraits);
                finalTraits.AddRange(testTraits);
                finalTraits.AddRange(afterTraits);

                return finalTraits;
            }

            private List<Tuple<Regex, Trait>> CreateRegexes(IEnumerable<RegexTraitPair> regexTraitPairs)
            {
                var result = new List<Tuple<Regex, Trait>>();
                foreach (RegexTraitPair pair in regexTraitPairs ?? Enumerable.Empty<RegexTraitPair>())
                {
                    try
                    {
                        result.Add(Tuple.Create(new Regex(pair.Regex, RegexOptions.None, TaefDiscoverer.RegexTimeout), pair.Trait));
                    }
                    catch (ArgumentException e)
                    {
                        _logger.LogError($"Regex '{pair.Regex}' can not be parsed: {e.Message}");
                    }
                }
                return result;
            }

            private bool IsMatch(Regex regex, string displayName)
            {
                try
                {
                    return regex.IsMatch(displayName);
                }
                catch (RegexMatchTimeoutException e)
                {
                    _logger.LogError($"Regex '{regex}' timed out on test '{displayName}': {e.Message}");
                    return false;
                }
            }
        }

    }

}
