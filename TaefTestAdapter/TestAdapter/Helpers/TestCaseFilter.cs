// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace TaefTestAdapter.TestAdapter.Helpers
{

    /// <summary>
    /// Applies the test case filter of a test run (e.g. vstest.console.exe's <c>/TestCaseFilter</c>) to VS test cases
    /// (see <see cref="DataConversionExtensions.ToVsTestCase"/>). Supported properties are
    /// <list type="bullet">
    /// <item><c>FullyQualifiedName</c>: the VS name of the test, with <c>.</c> as scope separator (e.g.
    /// <c>FullyQualifiedName~Ns.Class.</c>),</item>
    /// <item><c>DisplayName</c>: the TAEF name of the test (e.g. <c>DisplayName=Ns::Class::Method#metadataSet0</c>),</item>
    /// <item><c>LineNumber</c>, <c>CodeFilePath</c>, <c>ExecutorUri</c>, <c>Id</c> and <c>Source</c>, and</item>
    /// <item>the names of all traits of the tests (i.e., TAEF metadata such as <c>Owner</c> or <c>Priority</c>, and
    /// traits added by the trait regexes); the names are not case sensitive, and a condition on a trait matches if one
    /// of the test's values of that trait matches.</item>
    /// </list>
    /// If the filter can not be parsed, an error is logged and no test matches.
    /// </summary>
    public class TestCaseFilter
    {
        private static readonly Regex TraitValueRegex = new Regex(@"^[\w$]+$", RegexOptions.Compiled);

        private readonly IRunContext _runContext;
        private readonly ILogger _logger;

        private readonly IDictionary<string, TestProperty> _testPropertiesMap = new Dictionary<string, TestProperty>(StringComparer.OrdinalIgnoreCase);
        private readonly IDictionary<string, TestProperty> _traitPropertiesMap = new Dictionary<string, TestProperty>(StringComparer.OrdinalIgnoreCase);

        private readonly ISet<string> _traitPropertyNames;
        private readonly ISet<string> _allPropertyNames;

        public TestCaseFilter(IRunContext runContext, ISet<string> traitNames, ILogger logger)
        {
            _runContext = runContext;
            _logger = logger;

            InitProperties(traitNames);

            _traitPropertyNames = new HashSet<string>(_traitPropertiesMap.Keys, StringComparer.OrdinalIgnoreCase);
            _allPropertyNames = new HashSet<string>(_testPropertiesMap.Keys.Union(_traitPropertyNames), StringComparer.OrdinalIgnoreCase);
        }

        /// <returns>The test cases matching the filter (all of them if there is no filter, none if the filter is invalid).</returns>
        public IEnumerable<TestCase> Filter(IEnumerable<TestCase> testCases)
        {
            if (!TryGetFilterExpression(out ITestCaseFilterExpression filterExpression))
                return Enumerable.Empty<TestCase>();

            return filterExpression == null ? testCases : testCases.Where(testCase => Matches(testCase, filterExpression));
        }

        /// <returns>True if <paramref name="testCase"/> matches the filter (or there is no filter).</returns>
        public bool Matches(TestCase testCase)
        {
            if (!TryGetFilterExpression(out ITestCaseFilterExpression filterExpression))
                return false;

            return filterExpression == null || Matches(testCase, filterExpression);
        }


        private void InitProperties(ISet<string> traitNames)
        {
            _testPropertiesMap[nameof(TestCaseProperties.FullyQualifiedName)] = TestCaseProperties.FullyQualifiedName;
            _testPropertiesMap[nameof(TestCaseProperties.DisplayName)] = TestCaseProperties.DisplayName;
            _testPropertiesMap[nameof(TestCaseProperties.LineNumber)] = TestCaseProperties.LineNumber;
            _testPropertiesMap[nameof(TestCaseProperties.CodeFilePath)] = TestCaseProperties.CodeFilePath;
            _testPropertiesMap[nameof(TestCaseProperties.ExecutorUri)] = TestCaseProperties.ExecutorUri;
            _testPropertiesMap[nameof(TestCaseProperties.Id)] = TestCaseProperties.Id;
            _testPropertiesMap[nameof(TestCaseProperties.Source)] = TestCaseProperties.Source;

            foreach (string traitName in traitNames)
            {
                if (_testPropertiesMap.Keys.Contains(traitName))
                {
                    _logger.LogWarning($"Trait has same name as base test property and will thus be ignored for test case filtering: {traitName}");
                    continue;
                }

                var traitTestProperty = TestProperty.Find(traitName) ??
                      TestProperty.Register(traitName, traitName, "", "", typeof(string),
                        ValidateTraitValue, TestPropertyAttributes.None, typeof(TestCase));
                _traitPropertiesMap[traitName] = traitTestProperty;
            }
        }

        private TestProperty PropertyProvider(string propertyName)
        {
            TestProperty testProperty;

            _testPropertiesMap.TryGetValue(propertyName, out testProperty);

            if (testProperty == null)
                _traitPropertiesMap.TryGetValue(propertyName, out testProperty);

            return testProperty;
        }

        private object PropertyValueProvider(TestCase currentTest, string propertyName)
        {
            if (_testPropertiesMap.TryGetValue(propertyName, out TestProperty testProperty))
                return currentTest.GetPropertyValue(testProperty);

            if (_traitPropertyNames.Contains(propertyName))
                return GetTraitValues(currentTest, propertyName);

            return null;
        }

        /// <returns>False if the filter can not be parsed (an error has been logged)</returns>
        private bool TryGetFilterExpression(out ITestCaseFilterExpression filterExpression)
        {
            try
            {
                filterExpression = _runContext.GetTestCaseFilter(_allPropertyNames, PropertyProvider);

                string message = filterExpression == null
                        ? "No test case filter provided"
                        : $"Test case filter: {filterExpression.TestCaseFilterValue}";
                _logger.DebugInfo(message);

                return true;
            }
            catch (TestPlatformFormatException e)
            {
                _logger.LogError($"Test case filter is invalid, no tests will be run: {e.Message}");
                filterExpression = null;
                return false;
            }
        }

        private object GetTraitValues(TestCase testCase, string traitName)
        {
            IList<string> traitValues = testCase.Traits
                .Where(t => string.Equals(t.Name, traitName, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.Value)
                .ToList();

            if (traitValues.Count > 1)
                return traitValues.ToArray();

            return traitValues.SingleOrDefault();
        }

        private bool Matches(TestCase testCase, ITestCaseFilterExpression filterExpression)
        {
            bool matches =
                filterExpression.MatchTestCase(testCase, propertyName => PropertyValueProvider(testCase, propertyName));

            string message = matches
                ? $"{testCase.DisplayName} matches {filterExpression.TestCaseFilterValue}"
                : $"{testCase.DisplayName} does not match {filterExpression.TestCaseFilterValue}";
            _logger.DebugInfo(message);

            return matches;
        }

        private bool ValidateTraitValue(object value)
        {
            if (!(value is string traitValue))
                return false;

            return TraitValueRegex.IsMatch(traitValue);
        }

    }

}
