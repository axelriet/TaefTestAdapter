// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.ProcessExecution;

namespace TaefTestAdapter.Settings
{
    /// <summary>
    /// The effective settings of the adapter: the values of the current settings (the solution settings, or the project
    /// settings of the test DLL while <see cref="ExecuteWithSettingsForTestDll"/> runs), or their default values, with
    /// placeholders replaced where applicable.
    /// </summary>
    /// <remarks>
    /// Every setting <c>X</c> is described by three constants, which are used by the options pages of the Visual Studio
    /// package, the documentation of the settings and the tests: <c>OptionX</c> (the display name of the option),
    /// <c>OptionXDescription</c> (its description) and <c>OptionXDefaultValue</c> (its default value). The <c>Category*</c>
    /// and <c>Page*</c> constants name the categories and options pages.
    /// </remarks>
    public class SettingsWrapper
    {
        private readonly object _lock = new object();

        private readonly ITaefTestAdapterSettingsContainer _settingsContainer;
        private readonly string _solutionDir;
        private readonly SettingsPrinter _settingsPrinter;

        public RegexTraitParser RegexTraitParser { private get; set; }
        public EnvironmentVariablesParser EnvironmentVariablesParser { private get; set; }

        private HelperFilesCache _cache;
        public HelperFilesCache HelperFilesCache
        {
            private get { return _cache; }
            set
            {
                _cache = value ?? throw new ArgumentNullException(nameof(HelperFilesCache));
                _placeholderReplacer = new PlaceholderReplacer(() => SolutionDir, () => _currentSettings, HelperFilesCache, HelperFilesCache.Logger);
            }
        }

        private PlaceholderReplacer _placeholderReplacer;

        private int _nrOfRunningExecutions;
        private string _currentTestDll;
        private Thread _currentThread;
        private ITaefTestAdapterSettings _currentSettings;

        // needed for mocking
        // ReSharper disable once UnusedMember.Global
        public SettingsWrapper() { }

        public SettingsWrapper(ITaefTestAdapterSettingsContainer settingsContainer, string solutionDir = null)
        {
            _settingsContainer = settingsContainer;
            _solutionDir = solutionDir;
            _currentSettings = _settingsContainer.SolutionSettings;
            _settingsPrinter = new SettingsPrinter(this);
        }

        public virtual SettingsWrapper Clone()
        {
            return new SettingsWrapper(_settingsContainer, _solutionDir)
            {
                RegexTraitParser = RegexTraitParser,
                HelperFilesCache = HelperFilesCache,
                EnvironmentVariablesParser = EnvironmentVariablesParser
            };
        }

        public override string ToString()
        {
            return _settingsPrinter.ToReadableString();
        }

        #region Handling of execution settings

        /// <summary>
        /// Executes <paramref name="action"/> with the project settings whose ProjectRegex matches the full path of
        /// <paramref name="testDll"/> (or with the solution settings if no project settings match).
        /// </summary>
        public void ExecuteWithSettingsForTestDll(string testDll, ILogger logger, Action action)
        {
            lock (_lock)
            {
                CheckCorrectUsage(testDll);

                _nrOfRunningExecutions++;
                if (_nrOfRunningExecutions == 1)
                {
                    EnableSettingsForTestDll();
                }
            }
            try
            {
                action.Invoke();
            }
            finally
            {
                lock (_lock)
                {
                    _nrOfRunningExecutions--;
                    if (_nrOfRunningExecutions == 0)
                    {
                        ReenableSolutionSettings();
                    }
                }
            }

            void EnableSettingsForTestDll()
            {
                _currentTestDll = testDll;
                _currentThread = Thread.CurrentThread;

                var projectSettings = _settingsContainer.GetSettingsForTestDll(testDll);
                if (projectSettings != null)
                {
                    _currentSettings = projectSettings;
                    string settingsString = ToString();
                    _currentSettings = _settingsContainer.SolutionSettings;
                    logger.DebugInfo($"Test DLL '{testDll}': Project settings apply, regex: {projectSettings.ProjectRegex}");
                    logger.VerboseInfo($"Settings for test DLL '{testDll}': {settingsString}");

                    _currentSettings = projectSettings;
                }
                else
                {
                    logger.DebugInfo(
                        $"No settings configured for test DLL '{testDll}'; running with solution settings");
                }
            }

            void ReenableSolutionSettings()
            {
                _currentTestDll = null;
                _currentThread = null;
                if (_currentSettings != _settingsContainer.SolutionSettings)
                {
                    _currentSettings = _settingsContainer.SolutionSettings;
                    logger.DebugInfo($"Back to solution settings");
                }
            }

        }

        // public virtual for mocking
        public virtual void CheckCorrectUsage(string testDll)
        {
            if (_nrOfRunningExecutions == 0)
                return;
            if (_nrOfRunningExecutions < 0)
                throw new InvalidOperationException($"{nameof(_nrOfRunningExecutions)} must never be < 0");

            if (_currentThread != Thread.CurrentThread)
                throw new InvalidOperationException(
                    $"SettingsWrapper is already running with settings for a test DLL on thread '{_currentThread.Name}', can not also be used by thread {Thread.CurrentThread.Name}");

            if (testDll != _currentTestDll)
                throw new InvalidOperationException(
                    $"Execution is already running with settings for test DLL {_currentTestDll}, can not switch to settings for {testDll}");
        }

        #endregion

        #region Page and category names

        public const string PageGeneralName = "General";
        public const string PageTaefName = "TAEF";
        public const string PageTestDiscovery = "Test Discovery";
        public const string PageTestExecution = "Test Execution";

        public const string CategoryTestExecutionName = "Test execution";
        public const string CategoryTraitsName = "Regexes for trait assignment";
        public const string CategoryRuntimeBehaviorName = "Runtime behavior";
        public const string CategoryParallelizationName = "Parallelization";
        public const string CategoryMiscName = "Misc";
        public const string CategoryOutputName = "Output";
        public const string CategorySecurityName = "Security";
        public const string CategoryRunConfigurationName = "Run configuration (also applies to test discovery)";
        public const string CategorySetupAndTeardownName = "Setup and teardown";
        public const string CategoryTeExecutableName = "TE.exe";

        #endregion

        #region GeneralOptionsPage

        public const string OptionPrintTestOutput = "Print test output";
        public const string OptionPrintTestOutputDescription =
            "Print the console output of TE.exe (including the tests' log output) to the Tests Output window. " +
            "Note that output the tests write to the console (e.g. with printf or std::cout) is only available if tests are run in process (option '" + OptionRunInProcess + "').";
        public const bool OptionPrintTestOutputDefaultValue = false;

        public virtual bool PrintTestOutput => _currentSettings.PrintTestOutput ?? OptionPrintTestOutputDefaultValue;


        public const string OptionOutputMode = "Output mode";
        public const string OptionOutputModeDescription =
            "Controls the amount of output printed to the Tests Output window";
        public const OutputMode OptionOutputModeDefaultValue = OutputMode.Info;

        public virtual OutputMode OutputMode => _currentSettings.OutputMode ?? OptionOutputModeDefaultValue;


        public const string OptionTimestampMode = "Timestamp output";
        public const string OptionTimestampModeDescription =
            "Controls whether a timestamp is added to the output.\n" +
            TimestampModeConverter.Automatic + ": add timestamp only if Visual Studio does not add one itself (Visual Studio 2022 and later never need it)\n" +
            TimestampModeConverter.PrintTimeStamp + ": always add timestamp\n" +
            TimestampModeConverter.DoNotPrintTimeStamp + ": never add timestamp";
        public const TimestampMode OptionTimestampModeDefaultValue = TimestampMode.Automatic;

        public virtual TimestampMode TimestampMode => _currentSettings.TimestampMode ?? OptionTimestampModeDefaultValue;


        public const string OptionSeverityMode = "Print severity";
        public const string OptionSeverityModeDescription =
            "Controls whether the messages' severity is added to the output.\n" +
            SeverityModeConverter.Automatic + ": print severity only if Visual Studio does not print it itself (Visual Studio 2022 and later never need it)\n" +
            SeverityModeConverter.PrintSeverity + ": always print severity\n" +
            SeverityModeConverter.DoNotPrintSeverity + ": never print severity";
        public const SeverityMode OptionSeverityModeDefaultValue = SeverityMode.Automatic;

        public virtual SeverityMode SeverityMode => _currentSettings.SeverityMode ?? OptionSeverityModeDefaultValue;


        public const string OptionSummaryMode = "Print summary";
        public const string OptionSummaryModeDescription =
            "Controls whether a summary of warnings and errors is printed after test discovery/execution.";
        public const SummaryMode OptionSummaryModeDefaultValue = SummaryMode.WarningOrError;

        public virtual SummaryMode SummaryMode => _currentSettings.SummaryMode ?? OptionSummaryModeDefaultValue;


        public const string OptionPrefixOutputWithTaef = "Prefix output with [" + TaefConstants.OutputPrefix + "]";
        public const string OptionPrefixOutputWithTaefDescription =
            "Controls whether the prefix [" + TaefConstants.OutputPrefix + "] is added to the output of the Test Adapter for TAEF.";
        public const bool OptionPrefixOutputWithTaefDefaultValue = false;

        public virtual bool PrefixOutputWithTaef => _currentSettings.PrefixOutputWithTaef ?? OptionPrefixOutputWithTaefDefaultValue;


        public const string OptionSkipOriginCheck = "Skip check of file origin";
        public const string OptionSkipOriginCheckDescription =
            "If true, it will not be checked whether test DLLs originate from this computer. Note that this might impose security risks, e.g. when building downloaded solutions. " +
            "With the Visual Studio extension, this setting can only be changed via the Visual Studio options (values of settings files are ignored); " +
            "with the NuGet package only or vstest.console.exe, it is read from the settings file.";
        public const bool OptionSkipOriginCheckDefaultValue = false;

        public virtual bool SkipOriginCheck => _currentSettings.SkipOriginCheck ?? OptionSkipOriginCheckDefaultValue;


        #endregion

        #region TaefOptionsPage

        public const string OptionTeExecutable = "TE.exe path";
        public const string OptionTeExecutableDescription =
            "Path of TE.exe, or of a folder containing <architecture>\\TE.exe or TE.exe (e.g. the TAEF runtime folder '...\\Windows Kits\\10\\Testing\\Runtimes\\TAEF'). " +
            "If empty, the TE.exe matching the architecture of the test DLL (x86, x64, arm64) is taken from the installed Windows Kits (Testing\\Runtimes\\TAEF\\<architecture>\\TE.exe); if there is none, TE.exe is searched on the PATH.\n" +
            PlaceholderReplacer.TeExecutablePlaceholders;
        public const string OptionTeExecutableDefaultValue = "";

        public virtual string TeExecutable => _currentSettings.TeExecutable ?? OptionTeExecutableDefaultValue;

        /// <returns>The value of option <see cref="OptionTeExecutable"/> with all placeholders replaced ("" if the option is not set).</returns>
        public string GetTeExecutable(string testDll)
            => _placeholderReplacer.ReplaceTeExecutablePlaceholders(TeExecutable, testDll);


        public const string OptionRunIgnoredTests = "Also run ignored tests";
        public const string OptionRunIgnoredTestsDescription =
            "If true, tests marked with metadata 'Ignore' = true are run like all other tests. Otherwise, they are reported as skipped without being run.\n"
            + "TE.exe switch: " + TaefConstants.RunIgnoredTestsOption;
        public const bool OptionRunIgnoredTestsDefaultValue = false;

        public virtual bool RunIgnoredTests => _currentSettings.RunIgnoredTests ?? OptionRunIgnoredTestsDefaultValue;


        public const string OptionBreakOnError = "Break on error";
        public const string OptionBreakOnErrorDescription =
            "If enabled and tests are being debugged, TE.exe breaks into the debugger as soon as a test error (e.g. a failing VERIFY macro) is logged.\n"
            + "TE.exe switch: " + TaefConstants.BreakOnErrorOption + " (only passed while debugging)";
        public const bool OptionBreakOnErrorDefaultValue = false;

        public virtual bool BreakOnError => _currentSettings.BreakOnError ?? OptionBreakOnErrorDefaultValue;


        public const string OptionNrOfTestRepetitions = "Number of test repetitions";
        public const string OptionNrOfTestRepetitionsDescription =
            "Tests will be run for the selected number of times (minimum: 1); each repetition produces its own test result. " +
            "Usually, all tests are run n times in a row, each time in new test host processes. " +
            "If tests are run in process (option '" + OptionRunInProcess + "') or are being debugged, TE.exe can not start further test hosts: " +
            "each test is then repeated n times in a row within the same process, i.e. class and module setup and cleanup methods run only once.\n"
            + "TE.exe switches (if n > 1): " + TaefConstants.TestModeLoopOption + " " + TaefConstants.LoopOption + "<n> " + TaefConstants.LoopTestOneOption
            + ", or " + TaefConstants.TestModeLoopOption + " " + TaefConstants.LoopOption + "1 " + TaefConstants.LoopTestOption + "<n> if tests are run in process or are being debugged";
        public const int OptionNrOfTestRepetitionsDefaultValue = 1;
        public const int OptionNrOfTestRepetitionsMinValue = 1;

        public virtual int NrOfTestRepetitions
        {
            get
            {
                int nrOfRepetitions = _currentSettings.NrOfTestRepetitions ?? OptionNrOfTestRepetitionsDefaultValue;
                if (nrOfRepetitions < OptionNrOfTestRepetitionsMinValue)
                {
                    nrOfRepetitions = OptionNrOfTestRepetitionsDefaultValue;
                }
                return nrOfRepetitions;
            }
        }


        public const string OptionTestTimeout = "Test timeout";
        public const string OptionTestTimeoutDescription =
            "If non-empty, each test and each setup/cleanup method is aborted (and the test fails) if it runs longer than the given time; this overrides the 'TestTimeout' metadata of the tests. " +
            "Format: " + Utils.TestTimeoutFormat + " (hours 0-23, minutes and seconds 0-59), e.g. 0:05 for 5 minutes or 0:0:30 for 30 seconds. " +
            "Timeouts (this option and the 'TestTimeout' metadata) have no effect if tests are run in process (option '" + OptionRunInProcess + "'), since TE.exe ignores them then, " +
            "or are being debugged; the switch is then not passed (while debugging, " + TaefConstants.DisableTimeoutsOption + " is passed instead).\n" +
            "TE.exe switch: " + TaefConstants.TestTimeoutOption + "<value> (only if tests are neither run in process nor debugged)";
        public const string OptionTestTimeoutDefaultValue = "";

        /// <summary>
        /// The trimmed value of option <see cref="OptionTestTimeout"/>; "" if the option is not set or its value is invalid
        /// (see <see cref="Utils.IsValidTestTimeout"/>).
        /// </summary>
        public virtual string TestTimeout
        {
            get
            {
                string testTimeout = _currentSettings.TestTimeout?.Trim() ?? OptionTestTimeoutDefaultValue;
                return Utils.IsValidTestTimeout(testTimeout) ? testTimeout : OptionTestTimeoutDefaultValue;
            }
        }


        public const string OptionIsolationLevel = "Isolation level";
        public const string OptionIsolationLevelDescription =
            "Minimum isolation of the tests by means of separate test host processes (TE.ProcessHost.exe); if the 'IsolationLevel' metadata of a test requests a tighter isolation, the metadata wins. " +
            "No effect if tests are run in process (option '" + OptionRunInProcess + "') or are being debugged: TE.exe can not start further test host processes then, so the switch is not passed.\n" +
            TaefIsolationLevelConverter.Default + ": do not pass the switch (TAEF's default is one test host process per test DLL, i.e. '" + TaefIsolationLevelConverter.Module + "')\n" +
            TaefIsolationLevelConverter.Test + ", " + TaefIsolationLevelConverter.Method + ": a new test host process for each test\n" +
            TaefIsolationLevelConverter.Class + ": a new test host process for each test class\n" +
            TaefIsolationLevelConverter.Module + ": a new test host process for each test DLL\n" +
            "TE.exe switch: " + TaefConstants.IsolationLevelOption + "<level> (only if tests are neither run in process nor debugged)";
        public const TaefIsolationLevel OptionIsolationLevelDefaultValue = TaefIsolationLevel.Default;

        public virtual TaefIsolationLevel IsolationLevel => _currentSettings.IsolationLevel ?? OptionIsolationLevelDefaultValue;


        public const string OptionRunInProcess = "Run tests in process";
        public const string OptionRunInProcessDescription =
            "If true, tests are executed within TE.exe itself instead of a separate test host process. " +
            "This makes the tests' own console output (e.g. printf or std::cout) visible, but a crashing test aborts the remaining tests of its test DLL, " +
            "the options '" + OptionTestTimeout + "' and '" + OptionIsolationLevel + "' have no effect, and repetitions (option '" + OptionNrOfTestRepetitions + "') " +
            "repeat each test within the same process. " +
            "Tests are always run in process while being debugged.\n" +
            "TE.exe switch: " + TaefConstants.InProcOption;
        public const bool OptionRunInProcessDefaultValue = false;

        public virtual bool RunInProcess => _currentSettings.RunInProcess ?? OptionRunInProcessDefaultValue;

        #endregion

        #region TestExecutionOptionsPage

        public const string OptionDebuggerKind = "Debugger engine";
        public const string OptionDebuggerKindDescription =
                DebuggerKindConverter.VsTestFramework + ": the VsTest framework starts TE.exe under the debugger; also works without the Visual Studio extension (e.g. NuGet package only). " +
                "TE.exe's console output is not available then, so the results are read from a WTT log (" + TaefConstants.EnableWttLoggingOption + ") after TE.exe has finished: " +
                "results are reported only then, test output contains WEX logging only (no printf or std::cout output, option '" + OptionPrintTestOutput + "' has no effect), " +
                "test durations are not measured, and if TE.exe crashes, the results are read from the incomplete log it wrote while the tests were running\n" +
                DebuggerKindConverter.Native + ": the adapter starts TE.exe and the Visual Studio extension attaches its native debugger engine; results and output are reported as without debugger (default). " +
                "Requires the Visual Studio extension (not available with the NuGet package only)\n" +
                DebuggerKindConverter.ManagedAndNative + ": Same as '" + DebuggerKindConverter.Native + "', but allows to also debug into managed code\n" +
                "Note that tests are always run in process (" + TaefConstants.InProcOption + ", with " + TaefConstants.DisableTimeoutsOption + ") while being debugged.";
        public const DebuggerKind OptionDebuggerKindDefaultValue = DebuggerKind.Native;

        public virtual DebuggerKind DebuggerKind => _currentSettings.DebuggerKind ?? OptionDebuggerKindDefaultValue;


        public const string OptionAdditionalPdbs = "Additional PDBs";
        public const string OptionAdditionalPdbsDescription =
            "Files matching the provided file patterns are scanned for additional source locations. This can be useful if the PDBs containing the necessary information can not be found by scanning the test DLLs.\n" +
            "File part of each pattern may contain '*' and '?'; patterns are separated by ';'. Example: " + PlaceholderReplacer.TestDllDirPlaceholder + "\\pdbs\\*.pdb\n" + PlaceholderReplacer.AdditionalPdbsPlaceholders;
        public const string OptionAdditionalPdbsDefaultValue = "";

        public virtual string AdditionalPdbs => _currentSettings.AdditionalPdbs ?? OptionAdditionalPdbsDefaultValue;
        public IEnumerable<string> GetAdditionalPdbs(string testDll)
            => Utils.SplitAdditionalPdbs(AdditionalPdbs)
                .Select(p => _placeholderReplacer.ReplaceAdditionalPdbsPlaceholders(p, testDll));


        public const string OptionWorkingDir = "Working directory";
        public const string OptionWorkingDirDescription =
            "If non-empty, will set the working directory of TE.exe for test discovery and execution (default: " + PlaceholderReplacer.DescriptionOfTestDllDirPlaceHolder + ").\nExample: " + PlaceholderReplacer.SolutionDirPlaceholder + "\\MyTestDir\n" + PlaceholderReplacer.WorkingDirPlaceholders;
        public const string OptionWorkingDirDefaultValue = PlaceholderReplacer.TestDllDirPlaceholder;

        public virtual string WorkingDir => string.IsNullOrWhiteSpace(_currentSettings.WorkingDir)
            ? OptionWorkingDirDefaultValue
            : _currentSettings.WorkingDir;

        public string GetWorkingDirForExecution(string testDll, string testDirectory, int threadId)
        {
            return _placeholderReplacer.ReplaceWorkingDirPlaceholdersForExecution(WorkingDir, testDll, testDirectory, threadId);
        }

        public string GetWorkingDirForDiscovery(string testDll)
        {
            return _placeholderReplacer.ReplaceWorkingDirPlaceholdersForDiscovery(WorkingDir, testDll);
        }

        public const string OptionPathExtension = "PATH extension";
        public const string OptionPathExtensionDescription =
            "If non-empty, the content will be added in front of the PATH variable of the TE.exe processes (and thus of the tests) used for test discovery and execution.\nExample: C:\\MyBins;" + PlaceholderReplacer.TestDllDirPlaceholder + "\\MyOtherBins\n" + PlaceholderReplacer.PathExtensionPlaceholders;
        public const string OptionPathExtensionDefaultValue = "";

        public virtual string PathExtension => _currentSettings.PathExtension ?? OptionPathExtensionDefaultValue;

        public string GetPathExtension(string testDll)
            => _placeholderReplacer.ReplacePathExtensionPlaceholders(PathExtension, testDll);


        public const string OptionEnvironmentVariables = "Environment variables";
        public const string OptionEnvironmentVariablesDescription = "Allows to provide environment variables which will be added to the environment of TE.exe (and thus of the tests). Environment variables are separated by '"
                                                                    + EnvironmentVariablesParser.Separator
                                                                    + "'.\nExample: MyVar=MyValue" + EnvironmentVariablesParser.Separator + "MyDir=" + PlaceholderReplacer.TestDirPlaceholder
                                                                    + "\n" + PlaceholderReplacer.EnvironmentPlaceholders;
        public const string OptionEnvironmentVariablesDefaultValue = "";

        public virtual string EnvironmentVariables =>
            _currentSettings.EnvironmentVariables ?? OptionEnvironmentVariablesDefaultValue;

        public IDictionary<string, string> GetEnvironmentVariablesForDiscovery(string testDll)
            => EnvironmentVariablesParser.ParseEnvironmentVariablesString(
                _placeholderReplacer.ReplaceEnvironmentVariablesPlaceholdersForDiscovery(EnvironmentVariables, testDll));

        public IDictionary<string, string> GetEnvironmentVariablesForExecution(string testDll, string testDirectory, int threadId)
            => EnvironmentVariablesParser.ParseEnvironmentVariablesString(
                _placeholderReplacer.ReplaceEnvironmentVariablesPlaceholdersForExecution(EnvironmentVariables, testDll, testDirectory, threadId));


        public const string OptionAdditionalTestExecutionParam = "Additional TE.exe arguments";
        public const string OptionAdditionalTestExecutionParamDescription =
            "Additional arguments passed to TE.exe for test discovery and test execution, e.g. runtime parameters (/p:\"Name=Value\") or /runas:<context>. " +
            "They are inserted before the switches added by the adapter, so e.g. a " + TaefConstants.SelectOption + " query given here restricts test discovery. " +
            "Note that a quote must directly follow the colon of a switch (/p:\"Name=Value with spaces\", not \"/p:Name=Value with spaces\").\n" +
            PlaceholderReplacer.AdditionalTestExecutionParamPlaceholders;
        public const string OptionAdditionalTestExecutionParamDefaultValue = "";

        public virtual string AdditionalTestExecutionParam => _currentSettings.AdditionalTestExecutionParam ?? OptionAdditionalTestExecutionParamDefaultValue;

        public string GetUserParametersForExecution(string testDll, string testDirectory, int threadId)
            => _placeholderReplacer.ReplaceAdditionalTestExecutionParamPlaceholdersForExecution(
                AdditionalTestExecutionParam, testDll, testDirectory, threadId);

        public string GetUserParametersForDiscovery(string testDll)
            => _placeholderReplacer.ReplaceAdditionalTestExecutionParamPlaceholdersForDiscovery(
                AdditionalTestExecutionParam, testDll);


        public const string OptionBatchForTestSetup = "Test setup batch file";
        public const string OptionBatchForTestSetupDefaultValue = "";

        public const string OptionBatchForTestSetupDescription =
            "Batch file to be executed before test execution. If tests are executed in parallel, the batch file will be executed once per thread. " + PlaceholderReplacer.BatchesPlaceholders;

        public virtual string BatchForTestSetup => _currentSettings.BatchForTestSetup ?? OptionBatchForTestSetupDefaultValue;

        public string GetBatchForTestSetup(string testDirectory, int threadId)
            => _placeholderReplacer.ReplaceSetupBatchPlaceholders(BatchForTestSetup, testDirectory, threadId);


        public const string OptionBatchForTestTeardown = "Test teardown batch file";
        public const string OptionBatchForTestTeardownDescription =
            "Batch file to be executed after test execution. If tests are executed in parallel, the batch file will be executed once per thread. " + PlaceholderReplacer.BatchesPlaceholders;
        public const string OptionBatchForTestTeardownDefaultValue = "";

        public virtual string BatchForTestTeardown => _currentSettings.BatchForTestTeardown ?? OptionBatchForTestTeardownDefaultValue;

        public string GetBatchForTestTeardown(string testDirectory, int threadId)
            => _placeholderReplacer.ReplaceTeardownBatchPlaceholders(BatchForTestTeardown, testDirectory,
                threadId);


        public const string OptionKillProcessesOnCancel = "Kill processes on cancel";
        public const string OptionKillProcessesOnCancelDescription =
            "If true, running TE.exe processes (including their test host processes) are actively killed if the test execution is canceled. Note that killing a test process might have all kinds of side effects; in particular, cleanup methods of the tests will not be run.";
        public const bool OptionKillProcessesOnCancelDefaultValue = false;

        public virtual bool KillProcessesOnCancel => _currentSettings.KillProcessesOnCancel ?? OptionKillProcessesOnCancelDefaultValue;


        public const string OptionParallelTestExecution = "Parallel test execution";
        public const string OptionParallelTestExecutionDescription =
            "Parallel test execution is achieved by means of different threads, each of which is assigned a number of tests to be executed. The threads will then sequentially invoke TE.exe for the necessary test DLLs to produce the according test results.";
        public const bool OptionParallelTestExecutionDefaultValue = false;

        public virtual bool ParallelTestExecution => _currentSettings.ParallelTestExecution ?? OptionParallelTestExecutionDefaultValue;


        public const string OptionMaxNrOfThreads = "Maximum number of threads";
        public const string OptionMaxNrOfThreadsDescription =
            "Maximum number of threads to be used for test execution (0: one thread for each processor).";
        public const int OptionMaxNrOfThreadsDefaultValue = 0;

        public virtual int MaxNrOfThreads
        {
            get
            {
                int result = _currentSettings.MaxNrOfThreads ?? OptionMaxNrOfThreadsDefaultValue;
                if (result <= 0)
                {
                    result = Environment.ProcessorCount;
                }
                return result;
            }
        }

        public const string OptionMissingTestsReportMode = "Behavior for missing test results";
        public const string OptionMissingTestsReportModeDescription =
            "If a test can not be run (e.g. because a dependency has been removed since discovery without VS noticing), this option allows to configure how that test will be reported to the VS test framework." +
            "\nDefault: " + MissingTestsReportModeConverter.ReportAsNotFound;
        public const MissingTestsReportMode OptionMissingTestsReportModeDefaultValue = MissingTestsReportMode.ReportAsNotFound;

        public virtual MissingTestsReportMode MissingTestsReportMode =>
            _currentSettings.MissingTestsReportMode ?? OptionMissingTestsReportModeDefaultValue;

        #endregion

        #region TestDiscoveryOptionsPage

        public const string OptionTestDiscoveryRegex = "Regex for test discovery";
        public const string OptionTestDiscoveryRegexDescription =
            "If non-empty, only DLLs whose full path matches this regex will be considered as TAEF test DLLs; the DLLs are then not checked for TAEF test metadata. " +
            "A DLL is always considered a TAEF test DLL if a file <DLL path>" + TaefConstants.IndicatorFileExtension + " exists.";
        public const string OptionTestDiscoveryRegexDefaultValue = "";

        public virtual string TestDiscoveryRegex => _currentSettings.TestDiscoveryRegex ?? OptionTestDiscoveryRegexDefaultValue;


        public const string OptionTestDiscoveryTimeoutInSeconds = "Test discovery timeout in s";
        public const string OptionTestDiscoveryTimeoutInSecondsDescription =
            "Number of seconds after which test discovery (TE.exe " + TaefConstants.ListPropertiesOption + ") of a test DLL will be assumed to have failed. 0: Infinite timeout";
        public const int OptionTestDiscoveryTimeoutInSecondsDefaultValue = 30;

        public virtual int TestDiscoveryTimeoutInSeconds {
            get
            {
                int timeout = _currentSettings.TestDiscoveryTimeoutInSeconds ?? OptionTestDiscoveryTimeoutInSecondsDefaultValue;
                if (timeout < 0)
                    timeout = OptionTestDiscoveryTimeoutInSecondsDefaultValue;

                return timeout == 0 ? int.MaxValue : timeout;
            }
        }


        public const string TraitsRegexesPairSeparator = "//||//";
        public const string TraitsRegexesRegexSeparator = "///";
        public const string TraitsRegexesTraitSeparator = ",";
        public const string OptionTraitsDescription = "Allows to override/add traits for testcases matching a regex (the regexes are matched against the TAEF names of the tests, e.g. MyNamespace::MyClass::MyTest#0). Traits are build up in 3 phases: 1st, traits are assigned to tests according to the '" + OptionTraitsRegexesBefore + "' option. 2nd, the tests' TAEF metadata (module, class and test properties such as Owner or Priority) are added as traits, overriding traits from phase 1 with new values. 3rd, the '" + OptionTraitsRegexesAfter + "' option is evaluated, again in an overriding manner.\nSyntax: "
                                                 + TraitsRegexesRegexSeparator +
                                                 " separates the regex from the traits, the trait's name and value are separated by "
                                                 + TraitsRegexesTraitSeparator +
                                                 " and each pair of regex and trait is separated by "
                                                 + TraitsRegexesPairSeparator + ".\nExample: " +
                                                 @"MyClass::.*"
                                                 + TraitsRegexesRegexSeparator + "Type"
                                                 + TraitsRegexesTraitSeparator + "Small"
                                                 + TraitsRegexesPairSeparator +
                                                 @"MyClass2::.*|MyClass3::.*"
                                                 + TraitsRegexesRegexSeparator + "Type"
                                                 + TraitsRegexesTraitSeparator + "Medium";
        public const string OptionTraitsRegexesDefaultValue = "";

        public const string OptionTraitsRegexesBefore = "Before test discovery";

        public virtual List<RegexTraitPair> TraitsRegexesBefore
        {
            get
            {
                string option = _currentSettings.TraitsRegexesBefore ?? OptionTraitsRegexesDefaultValue;
                return RegexTraitParser.ParseTraitsRegexesString(option);
            }
        }

        public const string OptionTraitsRegexesAfter = "After test discovery";

        public virtual List<RegexTraitPair> TraitsRegexesAfter
        {
            get
            {
                string option = _currentSettings.TraitsRegexesAfter ?? OptionTraitsRegexesDefaultValue;
                return RegexTraitParser.ParseTraitsRegexesString(option);
            }
        }


        public const string OptionParseSymbolInformation = "Parse symbol information";
        public const string OptionParseSymbolInformationDescription =
            "Parse the debug symbols (PDB) of test DLLs to find the source locations of the tests. Setting this to false will speed up test discovery, but tests will not have source location information.";
        public const bool OptionParseSymbolInformationDefaultValue = true;

        public virtual bool ParseSymbolInformation => _currentSettings.ParseSymbolInformation ?? OptionParseSymbolInformationDefaultValue;


        #endregion

        #region Internal properties

        public virtual string DebuggingNamedPipeId => _currentSettings.DebuggingNamedPipeId;
        public virtual string SolutionDir => _solutionDir ?? _currentSettings.SolutionDir;

        #endregion

    }

}
