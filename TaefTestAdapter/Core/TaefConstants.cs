// This file has been modified by Microsoft on 9/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter
{

    /// <summary>
    /// Names, file extensions, TE.exe switches and TE.exe exit codes used by the Test Adapter for TAEF.
    /// </summary>
    public static class TaefConstants
    {
        #region Product identity and files

        /// <summary>Name of the runsettings element holding the adapter's settings.</summary>
        public const string SettingsName = "TaefTestAdapterSettings";

        /// <summary>Extension of solution settings files (<c>&lt;SolutionName&gt;.taef.runsettings</c>).</summary>
        public const string SettingsExtension = ".taef.runsettings";

        /// <summary>Extension of test duration files (<c>&lt;testdll&gt;.taef.testdurations</c>).</summary>
        public const string DurationsExtension = ".taef.testdurations";

        /// <summary>
        /// If a file <c>&lt;testdll&gt;.is_taef_test</c> exists, the DLL is treated as a TAEF test DLL
        /// without any further checks.
        /// </summary>
        public const string IndicatorFileExtension = ".is_taef_test";

        /// <summary>Text of the output prefix (printed as <c>[TAEF]</c>, see option PrefixOutputWithTaef).</summary>
        public const string OutputPrefix = "TAEF";

        /// <summary>Extension of the test sources handled by the adapter.</summary>
        public const string TestDllExtension = ".dll";

        /// <summary>File name of the TAEF test runner.</summary>
        public const string TeExecutableName = "TE.exe";

        /// <summary>File name of TAEF's out-of-process test host.</summary>
        public const string TeProcessHostName = "TE.ProcessHost.exe";

        #endregion

        #region TE.exe switches

        public const string ListOption = "/list";
        public const string ListPropertiesOption = "/listProperties";
        public const string RunIgnoredTestsOption = "/runIgnoredTests";
        public const string UnicodeOutputFalseOption = "/unicodeOutput:false";
        public const string ColoredConsoleOutputFalseOption = "/coloredConsoleOutput:false";
        public const string BreakOnErrorOption = "/breakOnError";
        public const string InProcOption = "/inproc";
        public const string DisableTimeoutsOption = "/disableTimeouts";
        public const string TestModeLoopOption = "/testmode:Loop";
        /// <summary>Followed by the number of loops, e.g. <c>/Loop:3</c>.</summary>
        public const string LoopOption = "/Loop:";
        public const string LoopTestOneOption = "/LoopTest:1";
        /// <summary>Followed by the number of repetitions of each test within a loop, e.g. <c>/LoopTest:3</c>.</summary>
        public const string LoopTestOption = "/LoopTest:";
        /// <summary>Followed by the timeout, e.g. <c>/testTimeout:0:05</c>.</summary>
        public const string TestTimeoutOption = "/testTimeout:";
        /// <summary>Followed by the level, e.g. <c>/isolationLevel:Class</c>.</summary>
        public const string IsolationLevelOption = "/isolationLevel:";
        /// <summary>
        /// Followed by the quoted query, e.g. <c>/select:"@Name='A' or @Name='B'"</c>. The quote must come
        /// right after the colon - quoting the whole argument makes TE.exe run all tests.
        /// </summary>
        public const string SelectOption = "/select:";

        public const string EnableWttLoggingOption = "/enableWttLogging";
        /// <summary>Followed by the quoted log file, e.g. <c>/logFile:"C:\tmp\x.wtl"</c>; requires <see cref="EnableWttLoggingOption"/>.</summary>
        public const string LogFileOption = "/logFile:";

        /// <summary>Switches passed to every TE.exe invocation to get parsable UTF-8 output.</summary>
        public const string OutputFormatOptions = UnicodeOutputFalseOption + " " + ColoredConsoleOutputFalseOption;

        /// <summary>Joins the terms of a <c>/select</c> query.</summary>
        public const string SelectionOrOperator = " or ";

        /// <summary>Combines the user's <c>/select</c> query with the adapter's query (both in parentheses).</summary>
        public const string SelectionAndOperator = " and ";

        /// <summary>
        /// Maximum length of a TE.exe command line (including the TE.exe path and the test DLL path). Windows allows
        /// 32,767 characters; TE.exe itself imposes no limit.
        /// </summary>
        public const int MaxCommandLength = 30000;

        /// <returns>
        /// <c>/testmode:Loop /Loop:&lt;n&gt; /LoopTest:1</c>: runs all tests <paramref name="nrOfRepetitions"/> times, each
        /// loop with new test hosts (i.e., fixtures are run for each loop). Not usable with <see cref="InProcOption"/>
        /// (TE.exe blocks all tests of the second and further loops since it can not start another test host), see
        /// <see cref="GetLoopTestOptions"/>.
        /// </returns>
        public static string GetLoopOptions(int nrOfRepetitions)
        {
            return $"{TestModeLoopOption} {LoopOption}{nrOfRepetitions} {LoopTestOneOption}";
        }

        /// <returns>
        /// <c>/testmode:Loop /Loop:1 /LoopTest:&lt;n&gt;</c>: runs each test <paramref name="nrOfRepetitions"/> times in a
        /// row within the same test host (i.e., class and module fixtures are run only once); usable with
        /// <see cref="InProcOption"/>.
        /// </returns>
        public static string GetLoopTestOptions(int nrOfRepetitions)
        {
            return $"{TestModeLoopOption} {LoopOption}1 {LoopTestOption}{nrOfRepetitions}";
        }

        /// <returns><c>/testTimeout:&lt;timeout&gt;</c></returns>
        public static string GetTestTimeoutOption(string testTimeout)
        {
            return TestTimeoutOption + testTimeout;
        }

        /// <returns><c>/isolationLevel:&lt;level&gt;</c>, or null for <see cref="TaefIsolationLevel.Default"/></returns>
        public static string GetIsolationLevelOption(TaefIsolationLevel isolationLevel)
        {
            string value = isolationLevel.ToTeValue();
            return value == null ? null : IsolationLevelOption + value;
        }

        /// <returns><c>/enableWttLogging /logFile:"&lt;file&gt;"</c> (makes TE.exe write a WTT log to the given file)</returns>
        public static string GetWttLoggingOptions(string wttLogFile)
        {
            return $"{EnableWttLoggingOption} {LogFileOption}\"{wttLogFile}\"";
        }

        /// <returns><c>/select:"&lt;query&gt;"</c> (quote directly after the colon)</returns>
        public static string GetSelectOption(string query)
        {
            return $"{SelectOption}\"{query}\"";
        }

        #endregion

        #region Test selection

        /// <summary>
        /// Wildcard matching exactly one character (one UTF-8 byte) within a <c>/select</c> query value; it replaces the
        /// characters of a test name which can not be passed within <c>/select:"..."</c> (see <see cref="ToSelectablePattern"/>).
        /// </summary>
        public const char SelectAnyCharacterWildcard = '?';

        /// <summary>Wildcard matching any number of characters within a <c>/select</c> query value.</summary>
        public const char SelectAnyCharactersWildcard = '*';

        /// <returns>
        /// A pattern which can be used within the quoted value of a <c>/select:"..."</c> query and which matches
        /// <paramref name="name"/> (and possibly a few similar names): a double quote would terminate the argument and
        /// can not be escaped, so it is replaced (as are control characters) by the wildcard
        /// <see cref="SelectAnyCharacterWildcard"/>. Single quotes are not escaped by this method (see
        /// <see cref="GetNameSelectionTerm"/>).
        /// </returns>
        public static string ToSelectablePattern(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            char[] result = null;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '"' || char.IsControl(c))
                {
                    if (result == null)
                        result = name.ToCharArray();
                    result[i] = SelectAnyCharacterWildcard;
                }
            }
            return result == null ? name : new string(result);
        }

        /// <returns>
        /// A selection term <c>@Name='&lt;pattern&gt;'</c> matching the TAEF test name (or name pattern)
        /// <paramref name="namePattern"/>: characters which can not be passed to TE.exe are replaced by wildcards
        /// (<see cref="ToSelectablePattern"/>), single quotes are doubled. Note that TE.exe matches names
        /// case-insensitively and that <c>*</c> and <c>?</c> are wildcards.
        /// </returns>
        public static string GetNameSelectionTerm(string namePattern)
        {
            return $"@Name='{Helpers.TaefNames.EscapeForSelect(ToSelectablePattern(namePattern))}'";
        }

        /// <returns>
        /// A selection term matching all tests of the TAEF class <paramref name="className"/>, i.e. all names
        /// <c>&lt;className&gt;::&lt;method&gt;[#&lt;row&gt;]</c>, but not the tests of nested classes
        /// (<c>&lt;className&gt;::&lt;Nested&gt;::&lt;method&gt;</c>):
        /// <c>(@Name='&lt;className&gt;::*' and not @Name='&lt;className&gt;::*::*')</c>. Note that this term does not match
        /// data rows whose row name contains <c>::</c>; they have to be selected by name.
        /// </returns>
        public static string GetClassSelectionTerm(string className)
        {
            if (className == null)
                throw new ArgumentNullException(nameof(className));

            string classPrefix = className + ScopeSeparator;
            string allMembers = GetNameSelectionTerm(classPrefix + SelectAnyCharactersWildcard);
            string nestedMembers = GetNameSelectionTerm(classPrefix + SelectAnyCharactersWildcard + ScopeSeparator + SelectAnyCharactersWildcard);
            return $"({allMembers} and not {nestedMembers})";
        }

        /// <returns>
        /// A selection term <c>@Data:Index=&lt;index&gt;</c> matching the tests whose data row has the index
        /// <paramref name="dataRowIndex"/> (see <see cref="TestCaseDataRowIndexProperty"/>).
        /// </returns>
        public static string GetDataRowIndexSelectionTerm(int dataRowIndex)
        {
            if (dataRowIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(dataRowIndex), dataRowIndex, "The data row index must not be negative");

            return $"@{DataPropertyPrefix}{DataRowIndexName}={dataRowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        /// <returns>
        /// True if the test has the trait <c>Ignore</c> with a value TE.exe treats as ignored, i.e. <c>1</c> or <c>true</c>
        /// (ignoring case, but not surrounding blanks; TE.exe 10.104k runs tests with values like <c>2</c>, <c>01</c>,
        /// <c>" true"</c> or <c>yes</c>). The trait name is compared ignoring case.
        /// </returns>
        public static bool IsIgnored(TestCase testCase)
        {
            if (testCase == null)
                throw new ArgumentNullException(nameof(testCase));

            return testCase.Traits.Any(t =>
                string.Equals(t.Name, IgnoreProperty, StringComparison.OrdinalIgnoreCase) &&
                IsIgnoreValue(t.Value));
        }

        /// <returns>True if TE.exe treats a test with metadata <c>Ignore=<paramref name="value"/></c> as ignored (see <see cref="IsIgnored"/>).</returns>
        public static bool IsIgnoreValue(string value)
        {
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Test names and metadata

        /// <summary>Separates namespaces, class and method in TAEF test names.</summary>
        public const string ScopeSeparator = "::";

        /// <summary>Separates the method part of a TAEF test name from the data row suffix (e.g. <c>#metadataSet0</c>).</summary>
        public const char DataRowSeparator = '#';

        /// <summary>Suffix of the pseudo test TE.exe reports if the data source of a data-driven test can not be loaded.</summary>
        public const string DataSourceErrorSuffix = "#error";

        /// <summary>TAEF metadata marking a test as ignored (value <c>true</c> or <c>1</c>, see <see cref="IsIgnored"/>).</summary>
        public const string IgnoreProperty = "Ignore";

        /// <summary>
        /// Name of the data value holding the index of a table data row (listed as <c>Data[Index] = &lt;n&gt;</c>, selectable
        /// with <c>@Data:Index=&lt;n&gt;</c>).
        /// </summary>
        public const string DataRowIndexName = "Index";

        /// <summary>
        /// TAEF properties which are not reported as traits (in addition to all properties starting with
        /// <see cref="DataPropertyPrefix"/>).
        /// </summary>
        public static readonly IReadOnlyList<string> PropertiesNotReportedAsTraits = Array.AsReadOnly(new[]
        {
            "TaefTestType",
            "Metadata:Index",
            "DataSource",
            "Description"
        });

        /// <summary>Prefix of properties holding the values of a data row.</summary>
        public const string DataPropertyPrefix = "Data:";

        /// <summary>Message of the result reported for tests with metadata <c>Ignore=true</c> which are not run.</summary>
        public const string IgnoredTestMessage =
            "Test is marked Ignore=true - enable option '" + SettingsWrapper.OptionRunIgnoredTests + "' to run it.";

        #endregion

        #region TE.exe exit codes

        // Apart from these, the exit code of TE.exe is the number of Failed + Blocked + NotRun tests
        // (or the crash code if a test crashes TE.exe while running in process).

        /// <summary>The TAEF logger could not be initialized (e.g. WTTLog.dll missing).</summary>
        public const int ExitCodeLoggerInitializationFailed = 0x03000000;

        /// <summary>No test files were found or specified.</summary>
        public const int ExitCodeNoTestFiles = 0x05000000;

        /// <summary>Startup error, e.g. invalid <c>/select</c> syntax.</summary>
        public const int ExitCodeStartupError = 0x06000000;

        /// <summary>No test cases were executed (nothing matched the selection, or not a TAEF test DLL).</summary>
        public const int ExitCodeNoTestsExecuted = 0x07000000;

        /// <summary>The session timeout (<c>/sessionTimeout</c>) expired.</summary>
        public const int ExitCodeSessionTimeout = 0x08000000;

        /// <summary>
        /// Returns an explanation for the special exit codes of TE.exe, or null if <paramref name="exitCode"/> is
        /// not one of them (i.e. it is the number of non-passing tests or a crash code).
        /// </summary>
        public static string GetExitCodeDescription(int exitCode)
        {
            switch (exitCode)
            {
                case ExitCodeLoggerInitializationFailed:
                    return "TE.exe could not initialize its logger (is the TAEF installation complete, e.g. is WTTLog.dll next to TE.exe?)";
                case ExitCodeNoTestFiles:
                    return "TE.exe did not find any test files";
                case ExitCodeStartupError:
                    return "TE.exe failed to start, e.g. because of an invalid command line switch or an invalid /select query";
                case ExitCodeNoTestsExecuted:
                    return "TE.exe did not execute any tests (no test matched the selection, or the file does not contain TAEF tests)";
                case ExitCodeSessionTimeout:
                    return "the TE.exe session timeout expired";
                default:
                    return null;
            }
        }

        #endregion

    }

}
