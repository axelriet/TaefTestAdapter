// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace TaefTestAdapter.TestAdapter.Fakes
{
    /// <summary>
    /// A simplified version of the VsTest framework's test case filter (e.g. vstest.console.exe's <c>/TestCaseFilter</c>):
    /// conditions <c>&lt;property&gt;&lt;operator&gt;&lt;value&gt;</c> joined by <c>&amp;</c> (all must match), operators
    /// <c>=</c>, <c>!=</c>, <c>~</c> (contains) and <c>!~</c> (does not contain), comparisons ignore case. As the VsTest
    /// framework does, <see cref="Create"/> rejects filters with properties the adapter does not support
    /// (<see cref="TestPlatformFormatException"/>), and a condition on a property with several values (e.g. a trait with
    /// several values) matches if one of the values matches.
    /// </summary>
    public class FakeTestCaseFilterExpression : ITestCaseFilterExpression
    {
        private class Condition
        {
            public string PropertyName;
            public string Operator;
            public string Value;
        }

        private static readonly string[] Operators = { "!=", "!~", "=", "~" };

        private readonly IList<Condition> _conditions;

        public string TestCaseFilterValue { get; }

        /// <summary>The property names the adapter declared as supported when requesting the filter.</summary>
        public IList<string> SupportedProperties { get; }

        private FakeTestCaseFilterExpression(string filter, IList<Condition> conditions, IList<string> supportedProperties)
        {
            TestCaseFilterValue = filter;
            _conditions = conditions;
            SupportedProperties = supportedProperties;
        }

        /// <summary>
        /// Parses <paramref name="filter"/> and checks that all its properties are supported (as the VsTest framework does
        /// in <see cref="IRunContext.GetTestCaseFilter"/>).
        /// </summary>
        /// <exception cref="TestPlatformFormatException">If the filter can not be parsed or uses unsupported properties</exception>
        public static FakeTestCaseFilterExpression Create(string filter, IEnumerable<string> supportedProperties,
            Func<string, TestProperty> propertyProvider)
        {
            List<string> supported = supportedProperties.ToList();
            var conditions = new List<Condition>();
            foreach (string conditionString in filter.Split('&'))
            {
                Condition condition = ParseCondition(conditionString, filter);
                if (!supported.Contains(condition.PropertyName, StringComparer.OrdinalIgnoreCase) || propertyProvider(condition.PropertyName) == null)
                {
                    throw new TestPlatformFormatException(
                        $"No tests matched the filter because it contains one or more properties that are not valid ({condition.PropertyName}). Specify filter expression containing valid properties ({string.Join(", ", supported)}) and try again.",
                        filter);
                }
                conditions.Add(condition);
            }

            return new FakeTestCaseFilterExpression(filter, conditions, supported);
        }

        private static Condition ParseCondition(string conditionString, string filter)
        {
            foreach (string op in Operators)
            {
                int index = conditionString.IndexOf(op, StringComparison.Ordinal);
                if (index > 0)
                {
                    return new Condition
                    {
                        PropertyName = conditionString.Substring(0, index).Trim(),
                        Operator = op,
                        Value = conditionString.Substring(index + op.Length).Trim()
                    };
                }
            }
            throw new TestPlatformFormatException($"Incorrect format for TestCaseFilter: {conditionString}", filter);
        }

        public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
        {
            return _conditions.All(c => Matches(c, propertyValueProvider(c.PropertyName)));
        }

        private static bool Matches(Condition condition, object propertyValue)
        {
            string[] values = propertyValue is string[] array
                ? array
                : propertyValue == null ? new string[0] : new[] { propertyValue.ToString() };

            switch (condition.Operator)
            {
                case "=":
                    return values.Any(v => string.Equals(v, condition.Value, StringComparison.OrdinalIgnoreCase));
                case "!=":
                    return !values.Any(v => string.Equals(v, condition.Value, StringComparison.OrdinalIgnoreCase));
                case "~":
                    return values.Any(v => v.IndexOf(condition.Value, StringComparison.OrdinalIgnoreCase) >= 0);
                case "!~":
                    return !values.Any(v => v.IndexOf(condition.Value, StringComparison.OrdinalIgnoreCase) >= 0);
                default:
                    throw new InvalidOperationException($"Unknown operator {condition.Operator}");
            }
        }

        public override string ToString() => TestCaseFilterValue;
    }

}
