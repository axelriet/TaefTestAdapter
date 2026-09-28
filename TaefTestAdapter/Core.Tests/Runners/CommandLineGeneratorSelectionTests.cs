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
    /// Checks that TE.exe runs exactly the tests to be run with the command lines <see cref="CommandLineGenerator"/>
    /// creates for tests discovered from the verbatim <c>/listProperties</c> output of a probe DLL
    /// (<see cref="ProbeTaefOutputs"/>): data rows whose names contain the wildcard characters <c>*</c> and <c>?</c>, and
    /// named rows of data-driven classes within a namespace. The tests TE.exe runs are determined by evaluating the
    /// <c>/select</c> queries as TE.exe does (<see cref="TaefSelectionQuery"/>).
    /// </summary>
    [TestClass]
    public class CommandLineGeneratorSelectionTests : TestsBase
    {
        private const string WildcardRowA = "ProbeNs::Wild::Rows#a*";
        private const string RowAb = "ProbeNs::Wild::Rows#ab";
        private const string WildcardRowAc = "ProbeNs::Wild::Rows#a?c";
        private const string RowAbc = "ProbeNs::Wild::Rows#abc";

        private const int LengthOfTeExecutable = 80;

        private IList<TestCaseDescriptor> _descriptors;
        private IList<TestCase> _testCases;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);

            _descriptors = ProbeTaefOutputs.GetDescriptors();
            _testCases = ProbeTaefOutputs.GetTestCases(MockOptions.Object, MockLogger.Object);
            _testCases.Should().HaveCount(_descriptors.Count);
        }

        /// <summary>
        /// The names of all tests of the probe DLL except for data source error pseudo tests (which TE.exe always runs) and
        /// tests whose names only differ in case (TE.exe's selection can not tell them apart).
        /// </summary>
        private IEnumerable<string> TestNamesDistinguishableByTe => _descriptors
            .Where(d => !d.IsDataSourceError)
            .Select(d => d.Name)
            .Where(n => _descriptors.Count(d => string.Equals(d.Name, n, StringComparison.OrdinalIgnoreCase)) == 1);

        /// <returns>
        /// The names of the tests TE.exe runs with the command lines created for the tests <paramref name="testNames"/>
        /// (in order of the command lines, a test run by several command lines is contained several times), without
        /// data source error pseudo tests (which TE.exe always runs).
        /// </returns>
        private List<string> GetTestsRunByTe(IEnumerable<string> testNames)
        {
            List<TestCase> testCasesToRun = testNames.Select(n => _testCases.Single(tc => tc.FullyQualifiedName == n)).ToList();
            var generator = new CommandLineGenerator(testCasesToRun, ProbeTaefOutputs.ProbeDll, LengthOfTeExecutable, "", false, MockOptions.Object);

            var testsRun = new List<string>();
            foreach (CommandLineGenerator.Args args in generator.GetCommandLines())
            {
                TaefSelectionQuery query = TaefSelectionQuery.FromCommandLine(args.CommandLine);
                testsRun.AddRange(_descriptors
                    .Where(d => !d.IsDataSourceError && (query == null || query.IsMatch(d)))
                    .Select(d => d.Name));
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
        public void GetCommandLines_DataRowWithAsteriskInName_TeRunsOnlyThatRow()
        {
            // "@Name='ProbeNs::Wild::Rows#a*'" alone would also run rows ab, a?c and abc
            AssertTeRunsExactly(WildcardRowA);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DataRowWithQuestionMarkInName_TeRunsOnlyThatRow()
        {
            // "@Name='ProbeNs::Wild::Rows#a?c'" alone would also run row abc
            AssertTeRunsExactly(WildcardRowAc);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RowsMatchedByNamesOfOtherRows_TeRunsOnlyTheseRows()
        {
            AssertTeRunsExactly(RowAb);
            AssertTeRunsExactly(RowAbc);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_SeveralRowsWithWildcardCharactersInNames_TeRunsExactlyThem()
        {
            AssertTeRunsExactly(WildcardRowA, RowAbc);
            AssertTeRunsExactly(WildcardRowAc, RowAb);
            AssertTeRunsExactly(WildcardRowA, RowAb, WildcardRowAc);
            AssertTeRunsExactly(WildcardRowA, RowAb, WildcardRowAc, RowAbc);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllTestsButOneRowWithWildcardCharacterInName_TeRunsExactlyThem()
        {
            AssertTeRunsExactly(TestNamesDistinguishableByTe.Where(n => n != WildcardRowA).ToArray());
            AssertTeRunsExactly(TestNamesDistinguishableByTe.Where(n => n != RowAbc).ToArray());
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_EachSingleTestOfProbeDll_TeRunsOnlyThatTest()
        {
            foreach (string testName in TestNamesDistinguishableByTe)
            {
                AssertTeRunsExactly(testName);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllTestsOfNamedRowsOfDataDrivenClasses_TeRunsExactlyThem()
        {
            AssertTeRunsExactly("ProbeNs::ClassDataBlocked#one::A", "ProbeNs::ClassDataBlocked#one::B");
            AssertTeRunsExactly(TestNamesDistinguishableByTe.Where(n => n.StartsWith("ProbeNs::ClassData", StringComparison.Ordinal)).ToArray());
            AssertTeRunsExactly("GlobalDataBlocked#one::A", "GlobalDataBlocked#two::A", "ProbeNs::ClassLightBlocked#metadataSet1::A");
        }
    }
}
