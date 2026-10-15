// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.Scheduling
{
    internal static class ManagedExecutionGroups
    {
        private sealed class GroupKey : IEquatable<GroupKey>
        {
            private readonly string _source;
            private readonly string _class;
            private readonly string _group;

            public GroupKey(TestCase test, string group)
            {
                _source = test.Source;
                _class = test.TaefClassName;
                _group = group;
            }

            public bool Equals(GroupKey other)
            {
                return other != null && StringComparer.OrdinalIgnoreCase.Equals(_source, other._source)
                    && _class == other._class && _group == other._group;
            }

            public override bool Equals(object obj) => Equals(obj as GroupKey);

            public override int GetHashCode()
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(_source) ^ _class.GetHashCode() ^ _group.GetHashCode();
            }
        }

        public static bool HasGroups(IEnumerable<TestCase> tests) => tests.Any(t => GetName(t) != null);

        /// <summary>Groups selected members without adding unselected prerequisites; TAEF still applies their dependency rules.</summary>
        public static List<List<TestCase>> GetUnits(IEnumerable<TestCase> tests)
        {
            var units = new List<List<TestCase>>();
            var groups = new Dictionary<GroupKey, List<TestCase>>();
            foreach (TestCase test in tests)
            {
                string name = GetName(test);
                if (name == null)
                {
                    units.Add(new List<TestCase> { test });
                    continue;
                }
                var key = new GroupKey(test, name);
                if (!groups.TryGetValue(key, out List<TestCase> members))
                {
                    members = new List<TestCase>();
                    groups.Add(key, members);
                    units.Add(members);
                }
                members.Add(test);
            }
            return units;
        }

        private static string GetName(TestCase test)
        {
            string group = test.IsManaged ? test.Properties.OfType<TestCaseExecutionGroupProperty>().FirstOrDefault()?.Name
                ?? test.Traits.LastOrDefault(t => string.Equals(t.Name, "ExecutionGroup", StringComparison.OrdinalIgnoreCase))?.Value : null;
            return string.IsNullOrEmpty(group) ? null : group;
        }
    }
}
