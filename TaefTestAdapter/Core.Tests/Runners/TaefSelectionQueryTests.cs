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

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Tests of the test helper <see cref="TaefSelectionQuery"/>: the expected tests are those TE.exe 10.104k lists for
    /// <c>TE.exe Probe_taef.dll /list /select:"&lt;query&gt;"</c> (see <see cref="ProbeTaefOutputs"/>), except for the
    /// data source error pseudo test <c>ProbeNs::ClassMissing#error</c>, which TE.exe lists and runs for every query.
    /// </summary>
    [TestClass]
    public class TaefSelectionQueryTests
    {
        private static readonly IList<TestCaseDescriptor> ProbeTests = ProbeTaefOutputs.GetDescriptors();

        private static IEnumerable<string> Select(string query)
        {
            TaefSelectionQuery selectionQuery = TaefSelectionQuery.Parse(query);
            return ProbeTests.Where(selectionQuery.IsMatch).Select(d => d.Name);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsMatch_NamePatterns_AsterisksAndQuestionMarksAreWildcardsAndCaseIsIgnored()
        {
            Select("@Name='ProbeNs::Wild::Rows#a*'").Should().Equal(
                "ProbeNs::Wild::Rows#a*", "ProbeNs::Wild::Rows#ab", "ProbeNs::Wild::Rows#a?c", "ProbeNs::Wild::Rows#abc");
            Select("@Name='ProbeNs::Wild::Rows#a?c'").Should().Equal("ProbeNs::Wild::Rows#a?c", "ProbeNs::Wild::Rows#abc");
            Select("@Name='probens::wild::rows#AB'").Should().Equal("ProbeNs::Wild::Rows#ab");
            Select("@Name='ProbeNs::CaseA::Test'").Should().Equal("ProbeNs::CaseA::Test", "ProbeNs::casea::Test");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsMatch_DataValues_AreValuesOfMethodAndClassRows()
        {
            string[] rowsWithIndex1 =
            {
                "ProbeNs::ClassData#two::First", "ProbeNs::ClassData#two::Second", "ProbeNs::Wild::Rows#ab",
                "ProbeNs::ClassDataBlocked#two::A", "ProbeNs::ClassDataBlocked#two::B", "GlobalDataBlocked#two::A",
                "ProbeNs::ClassDataVerifySetup#two::A"
            };
            Select("@Data:Index=1").Should().Equal(rowsWithIndex1);
            Select("@Data:Index='1'").Should().Equal(rowsWithIndex1);
            Select("@Data:V=3").Should().Equal("ProbeNs::Wild::Rows#a?c");
            Select("@DataSource='Table:Probe.xml#WildRows' and @Data:Index=3").Should().Equal("ProbeNs::Wild::Rows#abc");
        }

        /// <summary>The lists of <c>TE.exe CoreLab.dll /list /select:"&lt;query&gt;"</c> (see <see cref="LabTaefOutputs"/>).</summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void IsMatch_LabDll_QuestionMarkMatchesAsteriskAndDataIndexIsTheMethodRowsIndex()
        {
            IList<TestCaseDescriptor> labTests = new ListPropertiesParser().ParseListPropertiesOutput(LabTaefOutputs.ListProperties);
            IEnumerable<string> SelectLab(string query) => labTests.Where(TaefSelectionQuery.Parse(query).IsMatch).Select(d => d.Name);

            SelectLab("@Name='WildNs::Wild::Rows#a?'").Should().Equal("WildNs::Wild::Rows#a*", "WildNs::Wild::Rows#ab");
            SelectLab("@Name='WildNs::Wild::Rows#a?' and @Data:Index=0").Should().Equal("WildNs::Wild::Rows#a*");
            SelectLab("@Name='WildNs::Wild::Rows#q?x'").Should().Equal("WildNs::Wild::Rows#q\"x", "WildNs::Wild::Rows#qyx", "WildNs::Wild::Rows#q'x");
            SelectLab("@Name='WildNs::Wild::Rows#q?x' and @Data:Index=4").Should().Equal("WildNs::Wild::Rows#q\"x");
            SelectLab("@Name='WildNs::ClassRows#c?::M' and @Data:Index=0").Should().Equal("WildNs::ClassRows#c*::M");
            SelectLab("@Name='WildNs::ClassRows#c?::N#n?' and @Data:Index=0").Should().Equal("WildNs::ClassRows#c*::N#n*", "WildNs::ClassRows#cd::N#n*");
            SelectLab("@Name='TT<int *>::M'").Should().Equal("TT<int * *>::M", "TT<int *>::M");
            SelectLab("@Name='TT<int ?>::M'").Should().Equal("TT<int *>::M");
            SelectLab("@Name='WildNs::Wild::Light#metadataSet0' and @Data:Index=7").Should().Equal("WildNs::Wild::Light#metadataSet0");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsMatch_Operators_NotBindsStrongerThanAndWhichBindsStrongerThanOr()
        {
            Select("(@Name='ProbeNs::Wild::Rows#a*' and @Data:Index=0)").Should().Equal("ProbeNs::Wild::Rows#a*");
            Select("@Name='ProbeNs::Wild::Rows#a*' AND @Data:Index=0").Should().Equal("ProbeNs::Wild::Rows#a*");
            Select("@Name='ProbeNs::Wild::Rows#a*' and not @Name='ProbeNs::Wild::Rows#ab'").Should().Equal(
                "ProbeNs::Wild::Rows#a*", "ProbeNs::Wild::Rows#a?c", "ProbeNs::Wild::Rows#abc");
            Select("@Name='ProbeNs::Wild::Rows#a?c' and @Data:Index=2 or @Name='ProbeNs::Wild::Rows#ab'").Should().Equal(
                "ProbeNs::Wild::Rows#ab", "ProbeNs::Wild::Rows#a?c");
            Select("not @Name='ProbeNs::*' and @Data:Index=1").Should().Equal("GlobalDataBlocked#two::A");
            Select("@Name='ProbeNs::ClassData#one::First' or (@Name='GlobalDataBlocked#*' and not @Data:Index=0)").Should().Equal(
                "ProbeNs::ClassData#one::First", "GlobalDataBlocked#two::A");
            Select("(@Name='ProbeNs::ClassDataBlocked#one::*' and not @Name='ProbeNs::ClassDataBlocked#one::*::*')").Should().Equal(
                "ProbeNs::ClassDataBlocked#one::A", "ProbeNs::ClassDataBlocked#one::B");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsMatch_QuotedValues_DoubledQuotesAreSingleQuotes()
        {
            TaefSelectionQuery query = TaefSelectionQuery.Parse("@Name='TaefSamples::NamedRows::SpecialCharacters#with''quote'");

            query.IsMatch(p => p == "Name" ? "TaefSamples::NamedRows::SpecialCharacters#with'quote" : null).Should().BeTrue();
            query.IsMatch(p => p == "Name" ? "TaefSamples::NamedRows::SpecialCharacters#with''quote" : null).Should().BeFalse();
            query.IsMatch(p => null).Should().BeFalse("a test without the property does not match");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_UnsupportedOrInvalidQueries_Throw()
        {
            foreach (string query in new[] { "", "@Name", "@Name='a", "@Name>'a'", "(@Name='a'", "@Name='a' xor @Name='b'", "Name='a'" })
            {
                Action parsing = () => TaefSelectionQuery.Parse(query);
                parsing.Should().Throw<FormatException>(query);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FromCommandLine_CommandLines_LastSelectionIsUsed()
        {
            TaefSelectionQuery.FromCommandLine("\"C:\\tests\\My_taef.dll\" /unicodeOutput:false").Should().BeNull();
            TaefSelectionQuery.FromCommandLine("\"C:\\tests\\My_taef.dll\" /select:\"@Name='A::*'\" /inproc /select:\"@Name='B::X' or @Name='B::Y'\"")
                .Query.Should().Be("@Name='B::X' or @Name='B::Y'");

            Action parsing = () => TaefSelectionQuery.FromCommandLine("\"C:\\tests\\My_taef.dll\" /select:\"@Name='A::*'");
            parsing.Should().Throw<FormatException>();
        }
    }
}
