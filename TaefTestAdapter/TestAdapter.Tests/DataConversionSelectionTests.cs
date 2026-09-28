// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Runners;
using TaefTestAdapter.TestCases;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Checks that the conversion of discovered tests into VS test cases and back (as happens between discovery and
    /// execution) keeps everything the adapter needs to make TE.exe run exactly the selected tests, for the tests of a
    /// probe DLL with names TE.exe's selection can not tell apart by name alone (see <see cref="ProbeTaefOutputs"/>).
    /// </summary>
    [TestClass]
    public class DataConversionSelectionTests : TestsBase
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_DataRowsWithWildcardCharactersInNames_RoundTrippedTestCasesAreSelectedExactly()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            IList<TestCaseDescriptor> descriptors = ProbeTaefOutputs.GetDescriptors();
            List<TestCase> roundTripped = ProbeTaefOutputs.GetTestCases(MockOptions.Object, MockLogger.Object)
                .Select(tc => tc.ToVsTestCase().ToTestCase())
                .ToList();

            foreach (string rowName in new[] { "ProbeNs::Wild::Rows#a*", "ProbeNs::Wild::Rows#a?c", "ProbeNs::Wild::Rows#ab" })
            {
                TestCase testCase = roundTripped.Single(tc => tc.FullyQualifiedName == rowName);
                string commandLine = new CommandLineGenerator(new[] { testCase }, ProbeTaefOutputs.ProbeDll, 80, "", false, MockOptions.Object)
                    .GetCommandLines()
                    .Single()
                    .CommandLine;

                TaefSelectionQuery query = TaefSelectionQuery.FromCommandLine(commandLine);
                query.Should().NotBeNull(commandLine);
                descriptors.Where(query.IsMatch).Select(d => d.Name).Should().Equal(new[] { rowName },
                    $"TE.exe must only run {rowName} with command line {commandLine}");
            }
        }
    }
}
