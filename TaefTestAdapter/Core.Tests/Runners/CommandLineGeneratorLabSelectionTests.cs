// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.TestCases;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Checks which tests TE.exe runs with the command lines <see cref="CommandLineGenerator"/> creates for tests discovered
    /// from the verbatim <c>/listProperties</c> output of a lab DLL (<see cref="LabTaefOutputs"/>) whose test names contain
    /// the wildcard characters <c>*</c> and <c>?</c> and double quotes (data rows of methods and classes, class template
    /// instantiations). The <c>/select</c> queries are evaluated as TE.exe does (<see cref="TaefSelectionQuery"/>); ignored
    /// tests are run (option RunIgnoredTests) since the evaluation does not take metadata <c>Ignore</c> into account.
    /// </summary>
    [TestClass]
    public class CommandLineGeneratorLabSelectionTests : TestsBase
    {
        private const int LengthOfTeExecutable = 80;

        /// <summary>
        /// Rows of a data-driven method of a data-driven class, both with row names containing wildcard characters: the
        /// term (<c>@Name='WildNs::ClassRows#c?::N#n?' and @Data:Index=0</c>) also matches the row with the same index of
        /// the other class row (<c>WildNs::ClassRows#cd::N#n*</c>), since TE.exe only knows the index of the method's row.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> KnownAdditionalTests = new Dictionary<string, string>
        {
            { "WildNs::ClassRows#c*::N#n*", "WildNs::ClassRows#cd::N#n*" },
            { "WildNs::ClassRows#c*::N#nn", "WildNs::ClassRows#cd::N#nn" }
        };

        private IList<TestCaseDescriptor> _descriptors;
        private IList<TestCase> _testCases;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);

            _descriptors = new ListPropertiesParser().ParseListPropertiesOutput(LabTaefOutputs.ListProperties);
            _testCases = new TestCaseFactory(LabTaefOutputs.LabDll, MockLogger.Object, MockOptions.Object, null, null)
                .CreateTestCasesFromDescriptors(_descriptors);
            _testCases.Should().HaveCount(LabTaefOutputs.NrOfTests);
        }

        private List<string> GetTestsRunByTe(IEnumerable<string> testNames)
        {
            List<TestCase> testCasesToRun = testNames.Select(n => _testCases.Single(tc => tc.FullyQualifiedName == n)).ToList();
            var generator = new CommandLineGenerator(testCasesToRun, LabTaefOutputs.LabDll, LengthOfTeExecutable, "", false, MockOptions.Object);

            var testsRun = new List<string>();
            foreach (CommandLineGenerator.Args args in generator.GetCommandLines())
            {
                TaefSelectionQuery query = TaefSelectionQuery.FromCommandLine(args.CommandLine);
                testsRun.AddRange(_descriptors.Where(d => query == null || query.IsMatch(d)).Select(d => d.Name));
            }
            return testsRun;
        }

        private void AssertTeRunsExactly(params string[] testNames)
        {
            List<string> testsRun = GetTestsRunByTe(testNames);

            testsRun.Should().OnlyHaveUniqueItems("TE.exe must not run a test twice");
            testsRun.Should().BeEquivalentTo(testNames, $"TE.exe must run exactly the selected tests {string.Join(", ", testNames)}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_EachSingleTestOfLabDll_TeRunsOnlyThatTest()
        {
            // TE.exe matches names ignoring case, so the tests IgnoreValues::V_TRUE, V_true and V_tRuE can not be told apart
            IEnumerable<string> testNames = _descriptors
                .Select(d => d.Name)
                .Where(n => _descriptors.Count(d => string.Equals(d.Name, n, StringComparison.OrdinalIgnoreCase)) == 1)
                .Where(n => !KnownAdditionalTests.ContainsKey(n));
            foreach (string testName in testNames)
            {
                AssertTeRunsExactly(testName);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RowsWithWildcardsAndDoubleQuotes_TeRunsExactlyThem()
        {
            AssertTeRunsExactly("WildNs::Wild::Rows#a*", "WildNs::Wild::Rows#a?c");
            AssertTeRunsExactly("WildNs::Wild::Rows#q\"x", "WildNs::Wild::Rows#q'x");
            AssertTeRunsExactly("WildNs::Wild::Rows#a*", "WildNs::ClassRows#c*::M", "WildNs::ClassRows#cd::N#n*", "TT<int *>::M");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllTestsButOneWithWildcardInName_TeRunsExactlyThem()
        {
            foreach (string excluded in new[] { "WildNs::Wild::Rows#a*", "WildNs::Wild::Rows#ab", "TT<int *>::M", "TT<int * *>::M", "WildNs::ClassRows#cd::M" })
            {
                AssertTeRunsExactly(_descriptors.Select(d => d.Name).Where(n => n != excluded).ToArray());
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RowsOfDataDrivenMethodOfClassRowWithWildcard_TeAlsoRunsSameRowOfOtherClassRow()
        {
            // known limitation (see KnownAdditionalTests)
            foreach (KeyValuePair<string, string> pair in KnownAdditionalTests)
            {
                GetTestsRunByTe(new[] { pair.Key }).Should().BeEquivalentTo(pair.Key, pair.Value);
            }
        }
    }
}
