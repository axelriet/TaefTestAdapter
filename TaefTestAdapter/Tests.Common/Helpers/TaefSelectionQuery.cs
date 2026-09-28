// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaefTestAdapter.TestCases;

namespace TaefTestAdapter.Tests.Common.Helpers
{
    /// <summary>
    /// Evaluates a TAEF selection query (the value of TE.exe's <c>/select:"&lt;query&gt;"</c>) for tests as TE.exe does, so
    /// that tests can check which tests a command line created by the adapter makes TE.exe run. Supported is the part of
    /// the selection language used by the adapter (behaviour verified with TE.exe 10.104k):
    /// <list type="bullet">
    /// <item>Terms <c>@&lt;property&gt;=&lt;value&gt;</c>, where the value is quoted with single quotes (a single quote
    /// within the value is doubled) or unquoted (up to the next blank or parenthesis). The property is <c>Name</c> (the
    /// TAEF name of the test), <c>Data:&lt;name&gt;</c> (a data value of the test's data row, e.g. <c>Data:Index</c>, also
    /// of a class-level row) or a TAEF property (metadata) of the test. A test without the property does not match.</item>
    /// <item>Values are compared ignoring case; <c>*</c> matches any number of characters, <c>?</c> exactly one
    /// character (TAEF has no escape for them; <c>?</c> also matches <c>*</c>). TE.exe compares numeric data values
    /// numerically (e.g. <c>@Data:Index=07</c> matches index 7), which is not modelled: values are compared as
    /// text.</item>
    /// <item>Operators <c>not</c>, <c>and</c> and <c>or</c> (in this order of precedence, ignoring case) and
    /// parentheses.</item>
    /// </list>
    /// Other syntax (e.g. comparisons with <c>&lt;</c>, <c>&gt;</c> or <c>~</c>) makes <see cref="Parse"/> throw.
    /// </summary>
    public sealed class TaefSelectionQuery
    {
        private const string SelectOptionStart = "/select:\"";

        private readonly Func<Func<string, string>, bool> _isMatch;

        /// <summary>The query as passed to <see cref="Parse"/>.</summary>
        public string Query { get; }

        private TaefSelectionQuery(string query, Func<Func<string, string>, bool> isMatch)
        {
            Query = query;
            _isMatch = isMatch;
        }

        /// <exception cref="FormatException">The query is invalid or uses unsupported syntax.</exception>
        public static TaefSelectionQuery Parse(string query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var parser = new Parser(query);
            Func<Func<string, string>, bool> isMatch = parser.ParseQuery();
            return new TaefSelectionQuery(query, isMatch);
        }

        /// <returns>
        /// The query of the last <c>/select:"&lt;query&gt;"</c> option of the TE.exe command line (arguments)
        /// <paramref name="commandLine"/> (i.e. the one TE.exe uses), or null if there is none (TE.exe then runs all tests).
        /// </returns>
        public static TaefSelectionQuery FromCommandLine(string commandLine)
        {
            if (commandLine == null)
                throw new ArgumentNullException(nameof(commandLine));

            int start = commandLine.LastIndexOf(SelectOptionStart, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return null;
            start += SelectOptionStart.Length;
            int end = commandLine.IndexOf('"', start);
            if (end < 0)
                throw new FormatException($"Unterminated /select option in command line: {commandLine}");

            return Parse(commandLine.Substring(start, end - start));
        }

        /// <param name="getPropertyValue">
        /// Returns the value of a property of the test (<c>Name</c>, <c>Data:&lt;name&gt;</c> or a TAEF property; the name
        /// is to be compared ignoring case), or null if the test does not have the property.
        /// </param>
        public bool IsMatch(Func<string, string> getPropertyValue)
        {
            if (getPropertyValue == null)
                throw new ArgumentNullException(nameof(getPropertyValue));

            return _isMatch(getPropertyValue);
        }

        /// <returns>True if the query matches the test <paramref name="descriptor"/> (as listed by <c>/listProperties</c>).</returns>
        public bool IsMatch(TestCaseDescriptor descriptor)
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            return IsMatch(property => GetPropertyValue(descriptor, property));
        }

        /// <returns>True if <paramref name="value"/> matches the TAEF <paramref name="pattern"/> (see <see cref="TaefSelectionQuery"/>).</returns>
        public static bool IsWildcardMatch(string pattern, string value)
        {
            var regex = new StringBuilder("^");
            foreach (char c in pattern)
            {
                switch (c)
                {
                    case '*':
                        regex.Append(".*");
                        break;
                    case '?':
                        regex.Append('.');
                        break;
                    default:
                        regex.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }
            regex.Append('$');
            return Regex.IsMatch(value, regex.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        public override string ToString() => Query;

        private static string GetPropertyValue(TestCaseDescriptor descriptor, string property)
        {
            const string dataPrefix = "Data:";
            if (string.Equals(property, "Name", StringComparison.OrdinalIgnoreCase))
                return descriptor.Name;
            if (property.StartsWith(dataPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string dataName = property.Substring(dataPrefix.Length);
                return descriptor.Data.LastOrDefault(d => string.Equals(d.Name, dataName, StringComparison.OrdinalIgnoreCase))?.Value;
            }
            return descriptor.GetProperty(property);
        }

        /// <summary>Recursive descent parser: query := or; or := and ('or' and)*; and := not ('and' not)*; not := 'not' not | primary.</summary>
        private class Parser
        {
            private readonly string _query;
            private int _position;

            public Parser(string query)
            {
                _query = query;
            }

            public Func<Func<string, string>, bool> ParseQuery()
            {
                Func<Func<string, string>, bool> result = ParseOr();
                SkipBlanks();
                if (_position < _query.Length)
                    throw Error("unexpected text");
                return result;
            }

            private Func<Func<string, string>, bool> ParseOr()
            {
                var operands = new List<Func<Func<string, string>, bool>> { ParseAnd() };
                while (TryReadKeyword("or"))
                    operands.Add(ParseAnd());
                return operands.Count == 1 ? operands[0] : test => operands.Any(o => o(test));
            }

            private Func<Func<string, string>, bool> ParseAnd()
            {
                var operands = new List<Func<Func<string, string>, bool>> { ParseNot() };
                while (TryReadKeyword("and"))
                    operands.Add(ParseNot());
                return operands.Count == 1 ? operands[0] : test => operands.All(o => o(test));
            }

            private Func<Func<string, string>, bool> ParseNot()
            {
                if (TryReadKeyword("not"))
                {
                    Func<Func<string, string>, bool> operand = ParseNot();
                    return test => !operand(test);
                }
                return ParsePrimary();
            }

            private Func<Func<string, string>, bool> ParsePrimary()
            {
                SkipBlanks();
                if (_position >= _query.Length)
                    throw Error("term expected");

                if (_query[_position] == '(')
                {
                    _position++;
                    Func<Func<string, string>, bool> result = ParseOr();
                    SkipBlanks();
                    if (_position >= _query.Length || _query[_position] != ')')
                        throw Error("')' expected");
                    _position++;
                    return result;
                }

                if (_query[_position] != '@')
                    throw Error("'@' or '(' expected");
                _position++;

                int nameStart = _position;
                while (_position < _query.Length && (char.IsLetterOrDigit(_query[_position]) || _query[_position] == ':' || _query[_position] == '_' || _query[_position] == '.'))
                    _position++;
                string property = _query.Substring(nameStart, _position - nameStart);
                if (property.Length == 0)
                    throw Error("property name expected");

                SkipBlanks();
                if (_position >= _query.Length || _query[_position] != '=')
                    throw Error("'=' expected (other comparisons are not supported)");
                _position++;
                SkipBlanks();

                string pattern = ReadValue();
                return test =>
                {
                    string value = test(property);
                    return value != null && IsWildcardMatch(pattern, value);
                };
            }

            private string ReadValue()
            {
                if (_position < _query.Length && _query[_position] == '\'')
                {
                    var value = new StringBuilder();
                    _position++;
                    while (true)
                    {
                        if (_position >= _query.Length)
                            throw Error("unterminated value");
                        char c = _query[_position++];
                        if (c == '\'')
                        {
                            if (_position < _query.Length && _query[_position] == '\'')
                            {
                                value.Append('\'');
                                _position++;
                                continue;
                            }
                            return value.ToString();
                        }
                        value.Append(c);
                    }
                }

                int start = _position;
                while (_position < _query.Length && !char.IsWhiteSpace(_query[_position]) && _query[_position] != '(' && _query[_position] != ')')
                    _position++;
                if (_position == start)
                    throw Error("value expected");
                return _query.Substring(start, _position - start);
            }

            private bool TryReadKeyword(string keyword)
            {
                SkipBlanks();
                if (string.Compare(_query, _position, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0)
                    return false;
                int end = _position + keyword.Length;
                if (end < _query.Length && !char.IsWhiteSpace(_query[end]) && _query[end] != '(' && _query[end] != '@')
                    return false;
                _position = end;
                return true;
            }

            private void SkipBlanks()
            {
                while (_position < _query.Length && char.IsWhiteSpace(_query[_position]))
                    _position++;
            }

            private FormatException Error(string message)
            {
                return new FormatException($"Invalid or unsupported selection query ({message} at position {_position}): {_query}");
            }
        }
    }
}
