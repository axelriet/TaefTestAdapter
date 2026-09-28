// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// Parses the output of <c>TE.exe &lt;test DLL&gt; /listProperties</c> line by line and reports a
    /// <see cref="TestCaseDescriptor"/> as soon as a test is completely listed (i.e. when the next test, class or DLL
    /// starts, or on <see cref="Flush"/>).
    /// <para>
    /// The listing is structured by indentation (spaces only): banner and <c>Error:</c>/<c>Warning:</c> lines at column 0,
    /// the test DLL at 8, classes at 12 and tests at 16 characters. Fixtures (<c>Setup: F</c>, <c>Teardown: F</c>),
    /// properties (<c>Property[Name] =  Value</c>, two spaces after <c>=</c>) and data values (<c>Data[Name] = Value</c>)
    /// are indented by 16 (module, i.e. before the first class of a DLL), 20 (class) and 24 (test) characters. A data
    /// source error is listed as pseudo test <c>&lt;name&gt;#error [Blocked]</c> followed by the error message, indented
    /// by 20 characters for methods (test line at 16) and 16 characters for data-driven classes (class line at 12, the
    /// class is not followed by any tests). The trailing <c>Summary of ...</c> sections repeat errors indented by 4
    /// characters and are ignored.
    /// </para>
    /// <para>
    /// TE.exe prints property and data values as they are, so a value containing line breaks (e.g. a multi-line
    /// <c>Description</c>) continues on the following lines with arbitrary indentation, which can look like any other line
    /// of the listing. TE.exe always ends a block of properties or data values with an empty line, so the lines are
    /// interpreted as follows:
    /// <list type="bullet">
    /// <item>A line directly following a <c>Property[...]</c> or <c>Data[...]</c> line (or a continuation of its value)
    /// continues that value (joined by <c>\n</c>) unless it is the next property, data value or fixture of the same
    /// scope (i.e., with the same indentation).</item>
    /// <item>Lines which can nevertheless not belong to the listing (e.g. continuations of a value after an empty line
    /// within the value) are ignored and collected in <see cref="UnexpectedLines"/>: a line indented by 8 characters
    /// must be a rooted path (the test DLL, which TE.exe prints as absolute path), a line indented by 16 characters
    /// within a class must be a test of that class (<c>&lt;class&gt;::...</c>) or a data source error pseudo test, and
    /// unindented <c>Error:</c>/<c>Warning:</c> lines and summary headers must follow an empty line, the banner or
    /// another such message.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class StreamingListPropertiesParser
    {
        public const string BannerPrefix = "Test Authoring and Execution Framework";
        public const string ErrorPrefix = "Error: ";
        public const string WarningPrefix = "Warning: ";
        public const string SummaryPrefix = "Summary";

        /// <summary>Separates the lines of a property or data value spanning several lines.</summary>
        public const string ValueLineSeparator = "\n";

        public const int TestDllIndentation = 8;
        public const int ClassIndentation = 12;
        public const int TestIndentation = 16;
        public const int ModuleItemIndentation = 16;
        public const int ClassItemIndentation = 20;
        public const int TestItemIndentation = 24;
        private const int SummaryItemIndentation = 4;

        private static readonly Regex BannerRegex = new Regex(@"^Test Authoring and Execution Framework v(?<version>\S+) for (?<arch>\S+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex SummaryHeaderRegex = new Regex(@"^(?:Summary of .+:|Summary: Total=\d+.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RootedPathRegex = new Regex(@"^(?:[A-Za-z]:[\\/]|\\\\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex DataSourceErrorRegex = new Regex(@"^(?<name>.+#error) \[Blocked\]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex FixtureRegex = new Regex(@"^(?<kind>Setup|Teardown): (?<name>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PropertyRegex = new Regex(@"^Property\[(?<name>.+?)\] =(?: {1,2}(?<value>.*))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex DataRegex = new Regex(@"^Data\[(?<name>.+?)\] =(?: (?<value>.*))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Arguments of <see cref="TestCaseDescriptorCreated"/>.
        /// </summary>
        public class TestCaseDescriptorCreatedEventArgs : EventArgs
        {
            /// <summary>
            /// The test.
            /// </summary>
            public TestCaseDescriptor TestCaseDescriptor { get; set; }
        }

        /// <summary>Raised for every test (including data source error pseudo tests) in order of the listing.</summary>
        public event EventHandler<TestCaseDescriptorCreatedEventArgs> TestCaseDescriptorCreated;

        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private readonly List<string> _unexpectedLines = new List<string>();

        // properties, data and fixtures of the current module and class
        private readonly ScopeItems _moduleItems = new ScopeItems(TaefScope.Module);
        private readonly ScopeItems _classItems = new ScopeItems(TaefScope.Class);

        private TaefScope? _currentScope; // null: outside of a test DLL block
        private string _currentTestDll;
        private string _currentClass;
        private PendingTest _pendingTest;
        private int _errorMessageIndentation = -1; // indentation of the error message expected after a '#error [Blocked]' line
        private bool _isInSummary;

        // Error and warning messages may span several lines (continued with less than 4 spaces of indentation)
        private List<string> _continuedMessages; // list holding the message the next line may continue, or null
        private bool _isContinuationIgnored; // continuation of a message repeated in the summary

        // the property or data value the next line continues unless it is the next item of the scope, or null
        private ContinuedValue _continuedValue;

        // whether an unindented Error:/Warning: line or summary header can be a message of TE.exe (i.e. whether the
        // previous line was empty, the banner or such a message), and not a continuation of a value
        private bool _isTeMessageExpected = true;

        /// <summary>The TE.exe version from the banner (e.g. <c>10.104k</c>), or null if no banner has been parsed.</summary>
        public string TeVersion { get; private set; }

        /// <summary>The TE.exe architecture from the banner (e.g. <c>x64</c>), or null if no banner has been parsed.</summary>
        public string TeArchitecture { get; private set; }

        /// <summary>
        /// The <c>Error: ...</c> messages printed by TE.exe (e.g. <c>Error: TAEF: [HRESULT 0x80004005] Failed to load '...'. (The file
        /// was not recognized to be a TAEF test.)</c>), without their repetitions in the trailing summary. Messages spanning
        /// several lines are joined by <see cref="Environment.NewLine"/>.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>The <c>Warning: ...</c> messages printed by TE.exe, without their repetitions in the summary.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Non-empty lines which could not be interpreted (e.g. continuations of property values containing empty lines,
        /// see <see cref="StreamingListPropertiesParser"/>).
        /// </summary>
        public IReadOnlyList<string> UnexpectedLines => _unexpectedLines;

        /// <summary>The number of test DLL lines parsed (a DLL with module-level data is listed once per data row).</summary>
        public int NrOfTestDlls { get; private set; }

        public void ReportLine(string line)
        {
            if (line == null)
                return;

            line = line.TrimEnd('\r', '\n');
            if (line.Trim().Length == 0)
            {
                _continuedMessages = null;
                _isContinuationIgnored = false;
                _continuedValue = null;
                _isTeMessageExpected = true;
                return;
            }

            int indentation = 0;
            while (indentation < line.Length && line[indentation] == ' ')
                indentation++;
            string text = line.Substring(indentation);

            if (_continuedValue != null)
            {
                if (indentation != _continuedValue.Indentation || !IsItemLine(text))
                {
                    _continuedValue.Append(line);
                    return;
                }
                _continuedValue = null;
            }

            if (_continuedMessages != null || _isContinuationIgnored)
            {
                if (IsMessageContinuation(line, indentation))
                {
                    if (_continuedMessages != null)
                    {
                        int index = _continuedMessages.Count - 1;
                        _continuedMessages[index] = _continuedMessages[index] + Environment.NewLine + line;
                    }
                    return;
                }
                _continuedMessages = null;
                _isContinuationIgnored = false;
            }

            // set again by messages of TE.exe (see HandleUnindentedLine)
            bool isTeMessageExpected = _isTeMessageExpected;
            _isTeMessageExpected = false;

            if (_errorMessageIndentation >= 0)
            {
                if (indentation == _errorMessageIndentation && _pendingTest != null)
                {
                    _pendingTest.AppendErrorMessage(text);
                    return;
                }
                _errorMessageIndentation = -1;
            }

            switch (indentation)
            {
                case 0:
                    HandleUnindentedLine(line, isTeMessageExpected);
                    break;
                case SummaryItemIndentation when _isInSummary:
                    _isContinuationIgnored = true;
                    _isTeMessageExpected = true;
                    break;
                case TestDllIndentation when RootedPathRegex.IsMatch(text):
                    HandleTestDllLine(text);
                    break;
                case ClassIndentation when _currentScope != null:
                    HandleClassLine(text);
                    break;
                case TestIndentation when _currentScope == TaefScope.Module:
                    HandleItemLine(_moduleItems, text, line, indentation);
                    break;
                case TestIndentation when _currentScope != null && IsTestOfCurrentClass(text):
                    HandleTestLine(text);
                    break;
                case ClassItemIndentation when _currentScope == TaefScope.Class:
                    HandleItemLine(_classItems, text, line, indentation);
                    break;
                case ClassItemIndentation when _currentScope == TaefScope.Test && _pendingTest != null && _pendingTest.IsClassDataSourceError:
                    HandleItemLine(_pendingTest.Items, text, line, indentation);
                    break;
                case TestItemIndentation when _currentScope == TaefScope.Test && _pendingTest != null && !_pendingTest.IsClassDataSourceError:
                    HandleItemLine(_pendingTest.Items, text, line, indentation);
                    break;
                default:
                    _unexpectedLines.Add(line);
                    break;
            }
        }

        /// <summary>Reports the last test of the listing; to be called after the last line has been reported.</summary>
        public void Flush()
        {
            ReportPendingTest();
            _errorMessageIndentation = -1;
            _continuedValue = null;
            _continuedMessages = null;
            _isContinuationIgnored = false;
        }

        private void HandleUnindentedLine(string line, bool isTeMessageExpected)
        {
            Match bannerMatch = BannerRegex.Match(line);
            if (bannerMatch.Success)
            {
                ReportPendingTest();
                TeVersion = bannerMatch.Groups["version"].Value;
                TeArchitecture = bannerMatch.Groups["arch"].Value;
                _isInSummary = false;
                _isTeMessageExpected = true;
                return;
            }

            // otherwise, the line is probably the continuation of a value after an empty line within the value
            if (isTeMessageExpected)
            {
                if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                {
                    _errors.Add(line);
                    _continuedMessages = _errors;
                    _isTeMessageExpected = true;
                    return;
                }
                if (line.StartsWith(WarningPrefix, StringComparison.Ordinal))
                {
                    _warnings.Add(line);
                    _continuedMessages = _warnings;
                    _isTeMessageExpected = true;
                    return;
                }
                if (SummaryHeaderRegex.IsMatch(line))
                {
                    ReportPendingTest();
                    _currentScope = null;
                    _isInSummary = true;
                    _isTeMessageExpected = true;
                    return;
                }
            }

            _unexpectedLines.Add(line);
        }

        private static bool IsMessageContinuation(string line, int indentation)
        {
            if (indentation > 0)
                return indentation < SummaryItemIndentation;

            return !BannerRegex.IsMatch(line)
                && !line.StartsWith(ErrorPrefix, StringComparison.Ordinal)
                && !line.StartsWith(WarningPrefix, StringComparison.Ordinal)
                && !line.StartsWith(SummaryPrefix, StringComparison.Ordinal);
        }

        /// <returns>True if <paramref name="text"/> (without indentation) is a property, data value or fixture.</returns>
        private static bool IsItemLine(string text)
        {
            return PropertyRegex.IsMatch(text) || DataRegex.IsMatch(text) || FixtureRegex.IsMatch(text);
        }

        /// <returns>
        /// True if <paramref name="text"/> (a line indented like a test, without indentation) is a test of the current
        /// class (<c>&lt;class&gt;::...</c>) or a data source error pseudo test.
        /// </returns>
        private bool IsTestOfCurrentClass(string text)
        {
            if (DataSourceErrorRegex.IsMatch(text))
                return true;

            string classPrefix = _currentClass + TaefConstants.ScopeSeparator;
            return _currentClass != null
                && text.Length > classPrefix.Length
                && text.StartsWith(classPrefix, StringComparison.Ordinal);
        }

        private void HandleTestDllLine(string text)
        {
            ReportPendingTest();

            NrOfTestDlls++;
            _currentTestDll = text;
            _currentClass = null;
            _currentScope = TaefScope.Module;
            _moduleItems.Clear();
            _classItems.Clear();
            _isInSummary = false;
        }

        private void HandleClassLine(string text)
        {
            ReportPendingTest();

            _classItems.Clear();
            Match errorMatch = DataSourceErrorRegex.Match(text);
            if (errorMatch.Success)
            {
                // the data source of a data-driven class could not be loaded: the class is reported as pseudo test
                string name = errorMatch.Groups["name"].Value;
                _currentClass = name;
                _pendingTest = new PendingTest(name, name, isDataSourceError: true, isClassDataSourceError: true);
                _currentScope = TaefScope.Test;
                _errorMessageIndentation = TestIndentation;
                return;
            }

            _currentClass = text;
            _currentScope = TaefScope.Class;
        }

        private void HandleTestLine(string text)
        {
            ReportPendingTest();

            Match errorMatch = DataSourceErrorRegex.Match(text);
            if (errorMatch.Success)
            {
                _pendingTest = new PendingTest(errorMatch.Groups["name"].Value, _currentClass, isDataSourceError: true, isClassDataSourceError: false);
                _errorMessageIndentation = ClassItemIndentation;
            }
            else
            {
                _pendingTest = new PendingTest(text, _currentClass, isDataSourceError: false, isClassDataSourceError: false);
            }
            _currentScope = TaefScope.Test;
        }

        private void HandleItemLine(ScopeItems items, string text, string line, int indentation)
        {
            Match match = PropertyRegex.Match(text);
            if (match.Success)
            {
                items.Properties.Add(new TaefProperty(match.Groups["name"].Value, match.Groups["value"].Value));
                _continuedValue = new ContinuedValue(items.Properties, indentation);
                return;
            }

            match = DataRegex.Match(text);
            if (match.Success)
            {
                items.Data.Add(new TaefProperty(match.Groups["name"].Value, match.Groups["value"].Value));
                _continuedValue = new ContinuedValue(items.Data, indentation);
                return;
            }

            match = FixtureRegex.Match(text);
            if (match.Success)
            {
                var kind = match.Groups["kind"].Value == "Setup" ? TaefFixtureKind.Setup : TaefFixtureKind.Teardown;
                items.Fixtures.Add(new TaefFixture(items.Scope, kind, match.Groups["name"].Value));
                return;
            }

            _unexpectedLines.Add(line);
        }

        private void ReportPendingTest()
        {
            if (_pendingTest == null)
                return;

            PendingTest test = _pendingTest;
            _pendingTest = null;

            // class properties and data of a data-driven class with data source error are listed with the pseudo test
            ScopeItems classItems = test.IsClassDataSourceError ? test.Items : _classItems;
            ScopeItems testItems = test.IsClassDataSourceError ? null : test.Items;

            var fixtures = new List<TaefFixture>(_moduleItems.Fixtures);
            fixtures.AddRange(classItems.Fixtures);
            if (testItems != null)
                fixtures.AddRange(testItems.Fixtures);

            var descriptor = new TestCaseDescriptor(
                test.Name,
                test.ClassName,
                _currentTestDll,
                Merge(_moduleItems.Properties, classItems.Properties, testItems?.Properties),
                Merge(_moduleItems.Data, classItems.Data, testItems?.Data),
                fixtures,
                test.IsDataSourceError,
                test.ErrorMessage,
                test.IsClassDataSourceError);

            TestCaseDescriptorCreated?.Invoke(this, new TestCaseDescriptorCreatedEventArgs { TestCaseDescriptor = descriptor });
        }

        /// <summary>Merges properties of the scopes, where properties of inner scopes override those of outer ones (ignoring case).</summary>
        private static List<TaefProperty> Merge(params IList<TaefProperty>[] scopes)
        {
            var result = new List<TaefProperty>();
            var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (IList<TaefProperty> scope in scopes)
            {
                if (scope == null)
                    continue;

                foreach (TaefProperty property in scope)
                {
                    if (indexes.TryGetValue(property.Name, out int index))
                    {
                        result[index] = property;
                    }
                    else
                    {
                        indexes.Add(property.Name, result.Count);
                        result.Add(property);
                    }
                }
            }
            return result;
        }

        private class ScopeItems
        {
            public TaefScope Scope { get; }
            public List<TaefProperty> Properties { get; } = new List<TaefProperty>();
            public List<TaefProperty> Data { get; } = new List<TaefProperty>();
            public List<TaefFixture> Fixtures { get; } = new List<TaefFixture>();

            public ScopeItems(TaefScope scope)
            {
                Scope = scope;
            }

            public void Clear()
            {
                Properties.Clear();
                Data.Clear();
                Fixtures.Clear();
            }
        }

        /// <summary>The last property or data value of a scope, which may be continued by the following lines.</summary>
        private class ContinuedValue
        {
            private readonly List<TaefProperty> _items;
            private readonly int _index;

            /// <summary>Indentation of the line of the value (and of the other items of its scope).</summary>
            public int Indentation { get; }

            public ContinuedValue(List<TaefProperty> items, int indentation)
            {
                _items = items;
                _index = items.Count - 1;
                Indentation = indentation;
            }

            public void Append(string line)
            {
                TaefProperty item = _items[_index];
                _items[_index] = new TaefProperty(item.Name, item.Value + ValueLineSeparator + line);
            }
        }

        private class PendingTest
        {
            public string Name { get; }
            public string ClassName { get; }
            public bool IsDataSourceError { get; }
            public bool IsClassDataSourceError { get; }
            public string ErrorMessage { get; private set; }
            public ScopeItems Items { get; }

            public PendingTest(string name, string className, bool isDataSourceError, bool isClassDataSourceError)
            {
                Name = name;
                ClassName = className;
                IsDataSourceError = isDataSourceError;
                IsClassDataSourceError = isClassDataSourceError;
                Items = new ScopeItems(isClassDataSourceError ? TaefScope.Class : TaefScope.Test);
            }

            public void AppendErrorMessage(string text)
            {
                ErrorMessage = ErrorMessage == null ? text : ErrorMessage + Environment.NewLine + text;
            }
        }

    }

}
