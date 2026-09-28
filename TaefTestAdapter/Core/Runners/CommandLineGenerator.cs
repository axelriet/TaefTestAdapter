// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter.Runners
{

    /// <summary>
    /// Creates the TE.exe command lines (arguments without the TE.exe path) for running a set of tests of one test DLL:
    /// <code>
    /// "&lt;dll&gt;" &lt;userParams&gt; /unicodeOutput:false /coloredConsoleOutput:false [/runIgnoredTests] [/breakOnError]
    ///     [/testmode:Loop /Loop:&lt;N&gt; /LoopTest:1 | /testmode:Loop /Loop:1 /LoopTest:&lt;N&gt;] [/testTimeout:&lt;timeout&gt;]
    ///     [/isolationLevel:&lt;level&gt;] [/inproc] [/disableTimeouts] [/enableWttLogging /logFile:"&lt;file&gt;"]
    ///     [/select:"[(&lt;userQuery&gt;) and (]&lt;query&gt;[)]"]
    /// </code>
    /// The test DLL is passed as absolute path, or as an alternative consisting of ASCII characters if its path contains
    /// non-ASCII characters (TE.exe can not open it otherwise, see <see cref="TeArguments.GetTestDllArgument(string,string,ILogger)"/>).
    /// <c>/breakOnError</c>, <c>/inproc</c> and <c>/disableTimeouts</c> are passed if the tests are being debugged
    /// (<c>/breakOnError</c> only if option <see cref="SettingsWrapper.BreakOnError"/> is set), <c>/inproc</c> also if
    /// option <see cref="SettingsWrapper.RunInProcess"/> is set; <c>/testTimeout</c> is not passed together with
    /// <c>/inproc</c> since TE.exe ignores timeouts then (and warns about that for each test), and neither is
    /// <c>/isolationLevel</c> since TE.exe can not start further test hosts then (and blocks the affected tests).
    /// Repetitions (option <see cref="SettingsWrapper.NrOfTestRepetitions"/>) are run as loops of all tests (each loop in
    /// new test hosts, see <see cref="TaefConstants.GetLoopOptions"/>), or, with <c>/inproc</c> (which does not allow
    /// further test hosts), by repeating each test in a row (see <see cref="TaefConstants.GetLoopTestOptions"/>).
    /// <para>
    /// The <c>/select</c> query is omitted if all tests of the test DLL are run; otherwise, it selects all tests of those
    /// classes whose tests are all run by a class term (see <see cref="TaefConstants.GetClassSelectionTerm"/>) and all
    /// other tests by name (see <see cref="TaefConstants.GetNameSelectionTerm"/>; names containing wildcard characters
    /// are narrowed down to the test's data row, see <see cref="GetTestSelectionTerm"/>). If the user parameters contain a
    /// selection (<c>/select</c> or <c>/name</c>, which restricts test discovery), it is removed from the user parameters
    /// and combined with the adapter's query (<c>(&lt;userQuery&gt;) and (&lt;query&gt;)</c>, see
    /// <see cref="TeArguments.ExtractSelection"/>): TE.exe only evaluates one selection, and a class term alone would also
    /// select the tests of the class which the user's selection excludes (the class's test counts only comprise the
    /// discovered tests). If the command line would get longer than <see cref="MaxCommandLength"/> (including the TE.exe
    /// path), the tests are split into several command lines.
    /// </para>
    /// <para>
    /// Tests with metadata <c>Ignore=true</c> (see <see cref="TaefConstants.IsIgnored"/>) are only run if option
    /// <see cref="SettingsWrapper.RunIgnoredTests"/> is set; otherwise they are not contained in any of the returned
    /// <see cref="Args.TestCases"/> (but they are taken into account when deciding whether all tests of a class or of the
    /// test DLL are run, since TE.exe does not run them anyway).
    /// </para>
    /// </summary>
    public class CommandLineGenerator
    {
        /// <summary>
        /// A TE.exe command line and the tests it runs.
        /// </summary>
        public class Args
        {
            /// <summary>The tests TE.exe is expected to run with this command line.</summary>
            public IList<TestCase> TestCases { get; }

            /// <summary>The complete TE.exe arguments, starting with the quoted path of the test DLL.</summary>
            public string CommandLine { get; }

            internal Args(IList<TestCase> testCases, string commandLine)
            {
                TestCases = testCases ?? new List<TestCase>();
                CommandLine = commandLine ?? "";
            }
        }

        private class SelectionTerm
        {
            public string Term { get; }
            public IList<TestCase> TestCases { get; }

            public SelectionTerm(string term, IList<TestCase> testCases)
            {
                Term = term;
                TestCases = testCases;
            }
        }

        /// <summary>
        /// Maximum length of a complete command line (quoted TE.exe path, a space, and the arguments).
        /// </summary>
        public const int MaxCommandLength = TaefConstants.MaxCommandLength;

        private readonly IList<TestCase> _testCasesToRun;
        private readonly string _testDll;
        private readonly int _lengthOfTeExecutable;
        private readonly string _userParameters;
        private readonly bool _isBeingDebugged;
        private readonly SettingsWrapper _settings;
        private readonly string _wttLogFile;
        private readonly string _workingDir;
        private readonly ILogger _logger;

        // the user parameters without /select and /name, and the selection query TE.exe would have used (null: none)
        private readonly string _userParametersWithoutSelection;
        private readonly string _userQuery;

        /// <param name="testCasesToRun">The tests to be run (all of them must belong to <paramref name="testDll"/>).</param>
        /// <param name="testDll">The test DLL.</param>
        /// <param name="lengthOfTeExecutable">Length of the path of the TE.exe which will run the tests.</param>
        /// <param name="userParameters">Additional TE.exe arguments (placeholders already replaced), may be empty.</param>
        /// <param name="isBeingDebugged">Whether the tests are going to be debugged.</param>
        /// <param name="settings">The settings (for the test DLL).</param>
        /// <param name="wttLogFile">
        /// If not null, TE.exe is asked to write a WTT log to this file (switches <c>/enableWttLogging /logFile:"..."</c>,
        /// placed before the <c>/select</c> query), e.g. because TE.exe's console output will not be available. The path
        /// is passed as is (see <see cref="TeArguments.CreateWttLogFile(string,string,string,ILogger)"/>).
        /// </param>
        /// <param name="workingDir">
        /// The working directory of TE.exe (may be null); if the path of the test DLL contains non-ASCII characters and the
        /// working directory is the test DLL's folder, the test DLL is passed by its file name.
        /// </param>
        /// <param name="logger">Receives an error if the test DLL can not be passed to TE.exe (may be null).</param>
        public CommandLineGenerator(IEnumerable<TestCase> testCasesToRun, string testDll, int lengthOfTeExecutable,
            string userParameters, bool isBeingDebugged, SettingsWrapper settings, string wttLogFile = null,
            string workingDir = null, ILogger logger = null)
        {
            if (testCasesToRun == null)
                throw new ArgumentNullException(nameof(testCasesToRun));
            if (string.IsNullOrWhiteSpace(testDll))
                throw new ArgumentException("Test DLL must not be empty", nameof(testDll));
            if (lengthOfTeExecutable < 0)
                throw new ArgumentOutOfRangeException(nameof(lengthOfTeExecutable));

            _testCasesToRun = testCasesToRun.Distinct().ToList();
            _testDll = testDll;
            _lengthOfTeExecutable = lengthOfTeExecutable;
            _userParameters = userParameters ?? throw new ArgumentNullException(nameof(userParameters));
            _isBeingDebugged = isBeingDebugged;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _wttLogFile = wttLogFile;
            _workingDir = workingDir;
            _logger = logger;

            _userQuery = TeArguments.ExtractSelection(_userParameters, out _userParametersWithoutSelection);
        }

        /// <returns>
        /// The command lines to run all tests (except ignored ones, see class comment); empty if there is no test to be
        /// run by TE.exe.
        /// </returns>
        public IEnumerable<Args> GetCommandLines()
        {
            List<TestCase> testCasesRunByTe = _settings.RunIgnoredTests
                ? _testCasesToRun.ToList()
                : _testCasesToRun.Where(tc => !TaefConstants.IsIgnored(tc)).ToList();
            if (testCasesRunByTe.Count == 0)
                return new List<Args>();

            string testDllArgument = TeArguments.GetTestDllArgument(_testDll, _workingDir, _logger);

            // TE.exe runs the tests selected by the user (if any)
            if (AllTestCasesOfTestDllAreRun())
                return new List<Args> { new Args(testCasesRunByTe, GetBaseCommandLine(testDllArgument, _userParameters)) };

            string baseCommandLine = GetBaseCommandLine(testDllArgument, _userParametersWithoutSelection);
            List<SelectionTerm> selectionTerms = GetSelectionTerms(testCasesRunByTe);
            return GetCommandLinesWithSelection(baseCommandLine, selectionTerms);
        }

        private string GetBaseCommandLine(string testDllArgument, string userParameters)
        {
            var commandLine = new StringBuilder();
            commandLine.Append('"').Append(testDllArgument).Append('"');

            userParameters = userParameters.Trim();
            if (userParameters.Length > 0)
                commandLine.Append(' ').Append(userParameters);

            commandLine.Append(' ').Append(TaefConstants.OutputFormatOptions);

            if (_settings.RunIgnoredTests)
                commandLine.Append(' ').Append(TaefConstants.RunIgnoredTestsOption);

            // without a debugger, TE.exe would crash (or ask for a JIT debugger) on the first error
            if (_settings.BreakOnError && _isBeingDebugged)
                commandLine.Append(' ').Append(TaefConstants.BreakOnErrorOption);

            // the debugger is attached to TE.exe, so the tests must be run within TE.exe
            bool runInProcess = _settings.RunInProcess || _isBeingDebugged;

            // TE.exe needs a new test host for each loop, which it can not start with /inproc: all tests of the second and
            // further loops would be blocked ("TAEF would need to start a second test host, but the /InProc switch is being used");
            // repeating each test within a single loop works in process (and while debugging)
            int nrOfRepetitions = _settings.NrOfTestRepetitions;
            if (nrOfRepetitions > 1)
                commandLine.Append(' ').Append(runInProcess
                    ? TaefConstants.GetLoopTestOptions(nrOfRepetitions)
                    : TaefConstants.GetLoopOptions(nrOfRepetitions));

            // TE.exe ignores timeouts when running tests in process (and prints a warning for each test)
            string testTimeout = _settings.TestTimeout;
            if (!string.IsNullOrWhiteSpace(testTimeout) && !runInProcess)
                commandLine.Append(' ').Append(TaefConstants.GetTestTimeoutOption(testTimeout));

            // with /inproc, the tests run within TE.exe, i.e. in a single test host: TE.exe blocks all tests which would need
            // another test host because of an isolation level ("TAEF would need to start a second test host, but the /InProc
            // switch is being used")
            string isolationLevelOption = TaefConstants.GetIsolationLevelOption(_settings.IsolationLevel);
            if (isolationLevelOption != null && !runInProcess)
                commandLine.Append(' ').Append(isolationLevelOption);

            if (runInProcess)
                commandLine.Append(' ').Append(TaefConstants.InProcOption);

            if (_isBeingDebugged)
                commandLine.Append(' ').Append(TaefConstants.DisableTimeoutsOption);

            if (!string.IsNullOrEmpty(_wttLogFile))
                commandLine.Append(' ').Append(TaefConstants.GetWttLoggingOptions(_wttLogFile));

            return commandLine.ToString();
        }

        private bool AllTestCasesOfTestDllAreRun()
        {
            TestCaseMetaDataProperty metaData = GetMetaData(_testCasesToRun.First());
            return metaData != null
                && _testCasesToRun.All(tc => GetMetaData(tc)?.NrOfTestCasesInTestDll == metaData.NrOfTestCasesInTestDll)
                && _testCasesToRun.Count == metaData.NrOfTestCasesInTestDll;
        }

        private List<SelectionTerm> GetSelectionTerms(List<TestCase> testCasesRunByTe)
        {
            var testCasesRunByTeSet = new HashSet<TestCase>(testCasesRunByTe);
            var selectionTerms = new List<SelectionTerm>();

            // all tests to run (including ignored ones not run by TE.exe) grouped by class, in order of appearance
            var classNames = new List<string>();
            var testCasesOfClasses = new Dictionary<string, List<TestCase>>();
            foreach (TestCase testCase in _testCasesToRun)
            {
                string className = TaefNames.GetClassName(testCase.FullyQualifiedName);
                if (!testCasesOfClasses.TryGetValue(className, out List<TestCase> testCasesOfClass))
                {
                    testCasesOfClasses.Add(className, testCasesOfClass = new List<TestCase>());
                    classNames.Add(className);
                }
                testCasesOfClass.Add(testCase);
            }

            foreach (string className in classNames)
            {
                List<TestCase> testCasesOfClass = testCasesOfClasses[className];
                List<TestCase> testCasesOfClassRunByTe = testCasesOfClass.Where(testCasesRunByTeSet.Contains).ToList();
                if (testCasesOfClassRunByTe.Count == 0)
                    continue;

                List<TestCase> testCasesSelectedByName;
                if (CanBeSelectedByClass(className, testCasesOfClass))
                {
                    // data rows whose names contain "::" are not matched by the class term
                    testCasesSelectedByName = testCasesOfClassRunByTe.Where(tc => IsMatchedByNestedClassPattern(className, tc)).ToList();
                    List<TestCase> testCasesSelectedByClass = testCasesOfClassRunByTe.Except(testCasesSelectedByName).ToList();
                    if (testCasesSelectedByClass.Count > 0)
                        selectionTerms.Add(new SelectionTerm(TaefConstants.GetClassSelectionTerm(className), testCasesSelectedByClass));
                }
                else
                {
                    testCasesSelectedByName = testCasesOfClassRunByTe;
                }

                selectionTerms.AddRange(testCasesSelectedByName.Select(tc =>
                    new SelectionTerm(GetTestSelectionTerm(tc), new List<TestCase> { tc })));
            }

            return selectionTerms;
        }

        /// <returns>
        /// The term selecting the test by its name (see <see cref="TaefConstants.GetNameSelectionTerm"/>). TAEF has no
        /// escape for the wildcards <c>*</c> and <c>?</c>, so a name containing them (e.g. in a data row name or a template
        /// argument) or characters which have to be replaced by <c>?</c> (see <see cref="TaefConstants.ToSelectablePattern"/>)
        /// is a pattern which may match other tests as well. Such a pattern is narrowed down: <c>*</c> is replaced by
        /// <c>?</c> (which also matches the character <c>*</c>, but only a single character), and if the test's data row
        /// index is known (<see cref="TestCaseDataRowIndexProperty"/>), the term also requires it:
        /// <c>(@Name='&lt;pattern&gt;' and @Data:Index=&lt;n&gt;)</c>. Tests whose names still match the pattern (e.g. tests
        /// of other rows of a data-driven class whose methods are data-driven as well, which may have the same index, or
        /// instantiations of a class template differing in a single character) might be run as well.
        /// </returns>
        private static string GetTestSelectionTerm(TestCase testCase)
        {
            string name = testCase.FullyQualifiedName;
            if (!IsPattern(name))
                return TaefConstants.GetNameSelectionTerm(name);

            string nameTerm = TaefConstants.GetNameSelectionTerm(
                name.Replace(TaefConstants.SelectAnyCharactersWildcard, TaefConstants.SelectAnyCharacterWildcard));
            TestCaseDataRowIndexProperty dataRowIndex = testCase.Properties.OfType<TestCaseDataRowIndexProperty>().FirstOrDefault();
            return dataRowIndex == null
                ? nameTerm
                : $"({nameTerm}{TaefConstants.SelectionAndOperator}{TaefConstants.GetDataRowIndexSelectionTerm(dataRowIndex.Index)})";
        }

        /// <returns>
        /// True if <paramref name="name"/> contains wildcard characters or characters which have to be replaced by
        /// wildcards (see <see cref="TaefConstants.ToSelectablePattern"/>), i.e. if TE.exe would treat it as pattern.
        /// </returns>
        private static bool IsPattern(string name)
        {
            return name.IndexOf(TaefConstants.SelectAnyCharactersWildcard) >= 0
                || name.IndexOf(TaefConstants.SelectAnyCharacterWildcard) >= 0
                || TaefConstants.ToSelectablePattern(name) != name;
        }

        /// <returns>
        /// True if all tests of the class are to be run (according to the tests' meta data) and the class can safely be
        /// selected with a wildcard term (i.e., its name is not empty and contains no wildcard characters or characters
        /// which would have to be replaced by wildcards).
        /// </returns>
        private static bool CanBeSelectedByClass(string className, List<TestCase> testCasesOfClass)
        {
            if (string.IsNullOrEmpty(className) || IsPattern(className))
                return false;

            string classPrefix = className + TaefConstants.ScopeSeparator;
            if (!testCasesOfClass.All(tc => tc.FullyQualifiedName.StartsWith(classPrefix, StringComparison.Ordinal)))
                return false;

            TestCaseMetaDataProperty metaData = GetMetaData(testCasesOfClass.First());
            return metaData != null
                && testCasesOfClass.All(tc => GetMetaData(tc)?.NrOfTestCasesInClass == metaData.NrOfTestCasesInClass)
                && testCasesOfClass.Count == metaData.NrOfTestCasesInClass;
        }

        private static bool IsMatchedByNestedClassPattern(string className, TestCase testCase)
        {
            int startIndex = className.Length + TaefConstants.ScopeSeparator.Length;
            return testCase.FullyQualifiedName.IndexOf(TaefConstants.ScopeSeparator, startIndex, StringComparison.Ordinal) >= 0;
        }

        private IEnumerable<Args> GetCommandLinesWithSelection(string baseCommandLine, List<SelectionTerm> selectionTerms)
        {
            // the tests to be run have passed the user's selection during discovery, but class terms might select more tests
            string selectOptionStart = _userQuery == null
                ? $" {TaefConstants.SelectOption}\""
                : $" {TaefConstants.SelectOption}\"({_userQuery}){TaefConstants.SelectionAndOperator}(";
            string selectOptionEnd = _userQuery == null ? "\"" : ")\"";
            string commandLineStart = baseCommandLine + selectOptionStart;

            // the process is started with the command line "<TE.exe>" <arguments>
            int lengthOfFixedParts = _lengthOfTeExecutable + 3 + commandLineStart.Length + selectOptionEnd.Length;
            int maxQueryLength = MaxCommandLength - lengthOfFixedParts;

            var commandLines = new List<Args>();
            var query = new StringBuilder();
            var testCases = new List<TestCase>();
            foreach (SelectionTerm selectionTerm in selectionTerms)
            {
                int additionalLength = selectionTerm.Term.Length + (query.Length == 0 ? 0 : TaefConstants.SelectionOrOperator.Length);
                if (query.Length > 0 && query.Length + additionalLength > maxQueryLength)
                {
                    commandLines.Add(new Args(testCases, commandLineStart + query + selectOptionEnd));
                    query.Clear();
                    testCases = new List<TestCase>();
                }

                // a term which does not even fit into an empty query is passed anyway (TE.exe can then not be started,
                // which will be reported)
                if (query.Length > 0)
                    query.Append(TaefConstants.SelectionOrOperator);
                query.Append(selectionTerm.Term);
                testCases.AddRange(selectionTerm.TestCases);
            }
            if (query.Length > 0)
                commandLines.Add(new Args(testCases, commandLineStart + query + selectOptionEnd));

            return commandLines;
        }

        private static TestCaseMetaDataProperty GetMetaData(TestCase testCase)
        {
            return testCase.Properties.OfType<TestCaseMetaDataProperty>().FirstOrDefault();
        }

    }

}
