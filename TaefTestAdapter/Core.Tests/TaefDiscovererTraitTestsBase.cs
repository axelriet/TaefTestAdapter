// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestCases;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter
{
    /// <summary>
    /// Tests of the traits of the tests of Tests_taef.dll: TAEF metadata (module &lt; class &lt; method &lt; data row
    /// properties, see SampleTests\Tests\TraitsTests.cpp) and the traits of the options 'Before/After test discovery'.
    /// Traits resulting from the module properties of Tests_taef.dll (Architecture, Component, Owner=ModuleOwner) are
    /// ignored by <see cref="AssertFindsTestWithTraits"/> unless they are expected explicitly.
    /// </summary>
    [TestClass]
    public abstract class TaefDiscovererTraitTestsBase : TestsBase
    {
        protected abstract string SampleTestToUse { get; }

        private static readonly Trait[] NoTraits = new Trait[0];

        private IList<TestCase> _testCases;

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsAllAmountsOfTraits()
        {
            var traits = new List<Trait>();
            for (int i = 1; i <= 8; i++)
            {
                traits.Add(new Trait($"Trait{i}", $"Equals{i}"));
                AssertFindsTestWithTraits($"TaefSamples::Traits::With{i}Traits", traits.ToArray());
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_AllTestsHaveModuleTraits()
        {
            string architecture = TestResources.GetSampleConfiguration(SampleTestToUse)?.GetArchitecture();
            IList<TestCase> testCases = GetTestCases();

            testCases.Should().HaveCount(TestResources.NrOfTests);
            testCases.Should().OnlyContain(tc => tc.Traits.Count(t => t.Name == "Architecture" && t.Value == architecture) == 1);
            testCases.Should().OnlyContain(tc => tc.Traits.Count(t => t.Name == "Component" && t.Value == "SampleTests") == 1);
            testCases.Should().OnlyContain(tc => tc.Traits.Count(t => t.Name == "Owner") == 1);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_InternalTaefPropertiesAreNoTraits()
        {
            IList<TestCase> testCases = GetTestCases();

            testCases.SelectMany(tc => tc.Traits).Should().NotContain(t =>
                t.Name == "TaefTestType" || t.Name == "DataSource" || t.Name == "Metadata:Index" || t.Name == "Description" || t.Name.StartsWith("Data:"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndOneTrait()
        {
            AssertFindsTestWithTraits("TaefSamples::ClassWithFixtures::AddPassesWithTraits", new Trait("Type", "Small"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndTwoTraits()
        {
            AssertFindsTestWithTraits("TaefSamples::ClassWithFixtures::AddPassesWithTraits2", new Trait("Type", "Small"), new Trait("Author", "Alice"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndThreeTraits()
        {
            AssertFindsTestWithTraits("TaefSamples::ClassWithFixtures::AddPassesWithTraits3", new Trait("Type", "Small"), new Trait("Author", "Alice"), new Trait("TestCategory", "Integration"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTemplateTestWithOneTrait()
        {
            Trait[] traits = { new Trait("Author", "Bob") };
            foreach (string templateClass in TemplateClasses)
            {
                AssertFindsTestWithTraits($"{templateClass}::CanIterate", traits);
                AssertFindsTestWithTraits($"{templateClass}::CanDefeatMath", NoTraits);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTemplateTestWithTwoTraits()
        {
            Trait[] traits = { new Trait("Author", "Dave"), new Trait("TestCategory", "Integration") };
            foreach (string templateClass in TemplateClasses)
            {
                AssertFindsTestWithTraits($"{templateClass}::TwoTraits", traits);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTemplateTestWithThreeTraits()
        {
            Trait[] traits = { new Trait("Author", "Dave"), new Trait("TestCategory", "Integration"), new Trait("Class", "Simple") };
            foreach (string templateClass in TemplateClasses)
            {
                AssertFindsTestWithTraits($"{templateClass}::ThreeTraits", traits);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithOneTrait()
        {
            foreach (string row in TableRows)
            {
                AssertFindsTestWithTraits($"TaefSamples::TableDataTests::SimpleTraits#{row}", new Trait("Type", "Small"));
                AssertFindsTestWithTraits($"TaefSamples::TableDataTests::Simple#{row}", NoTraits);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithTwoTraits()
        {
            foreach (string row in TableRows)
            {
                AssertFindsTestWithTraits($"TaefSamples::TableDataTests::SimpleTraits2#{row}", new Trait("Type", "Small"), new Trait("Author", "Alice"));
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithThreeTraits()
        {
            foreach (string row in TableRows)
            {
                AssertFindsTestWithTraits($"TaefSamples::TableDataTests::SimpleTraits3#{row}",
                    new Trait("Type", "Medium"), new Trait("Author", "Carol"), new Trait("TestCategory", "Integration"));
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_DataRowPropertiesOverrideOtherProperties()
        {
            AssertFindsTestWithTraits(TestResources.TestNames.RowWithQuote, new Trait("Priority", "1"));
            AssertFindsTestWithTraits(TestResources.TestNames.RowWithColons, new Trait("Owner", "RowOwner"));
            AssertFindsTestWithTraits("TaefSamples::NamedRows::SpecialCharacters#with#hash", NoTraits);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTestWithUmlauts()
        {
            Trait[] traits = { new Trait("Träit1", "Völue1a Völue1b"), new Trait("Träit2", "Völue2") };
            AssertFindsTestWithTraits("TaefSamples::Ümlautß::Träits", traits);
            AssertFindsTestWithTraits("TaefSamples::Ümlautß::Täst", NoTraits);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestWithUmlauts()
        {
            Trait[] traits = { new Trait("Träit1", "Völue1a Völue1b"), new Trait("Träit2", "Völue2") };
            AssertFindsTestWithTraits("TaefSamples::DataDrivenTästs::Träits#metadataSet0", traits);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTestWithSetupAndUmlauts()
        {
            Trait[] traits = { new Trait("Träit1", "Völue1a Völue1b"), new Trait("Träit2", "Völue2") };
            AssertFindsTestWithTraits("TaefSamples::Nämespace::KlässWithSetüp::Träits", traits);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_FindsTestWithTwoEqualTraits()
        {
            // TE.exe joins the values of a property declared twice
            AssertFindsTestWithTraits("TaefSamples::Traits::WithEqualTraits", new Trait("Author", "Alice Bob"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_ClassPropertiesAreInheritedAndOverridden()
        {
            AssertFindsTestWithTraits("TaefSamples::ClassAndMethodProperties::InheritsClassProperties",
                new Trait("Owner", "ClassOwner"), new Trait("Category", "ClassCategory"), new Trait("ClassTrait", "ClassValue"));
            AssertFindsTestWithTraits("TaefSamples::ClassAndMethodProperties::OverridesClassProperties",
                new Trait("Owner", "MethodOwner"), new Trait("Category", "MethodCategory"), new Trait("ClassTrait", "ClassValue"));
            AssertFindsTestWithTraits("TaefSamples::ClassAndMethodProperties::WithMultipleValues",
                new Trait("Owner", "ClassOwner"), new Trait("Category", "Smoke Nightly"), new Trait("ClassTrait", "ClassValue"));
            AssertFindsTestWithTraits("TaefSamples::ClassAndMethodProperties::WithCustomPropertiesAndFails",
                new Trait("Owner", "ClassOwner"), new Trait("Category", "ClassCategory"), new Trait("ClassTrait", "ClassValue"),
                new Trait("BugId", "12345"), new Trait("Area", "Area with spaces"));
            AssertFindsTestWithTraits("TaefSamples::ClassAndMethodProperties::WithPriority2AndFails",
                new Trait("Owner", "ClassOwner"), new Trait("Category", "ClassCategory"), new Trait("ClassTrait", "ClassValue"), new Trait("Priority", "2"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SampleTests_IgnoredTestsHaveIgnoreTrait()
        {
            AssertFindsTestWithTraits(TestResources.TestNames.IgnoredTest,
                new Trait("Owner", "ClassOwner"), new Trait("Category", "ClassCategory"), new Trait("ClassTrait", "ClassValue"),
                new Trait("Ignore", "true"), new Trait("Priority", "1"));
            AssertFindsTestWithTraits(TestResources.TestNames.IgnoredPassing, new Trait("Ignore", "true"));
            AssertFindsTestWithTraits(TestResources.TestNames.IgnoredFailing, new Trait("Ignore", "true"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexBeforeFromOptions_TaefTraitWins()
        {
            string testname = "TaefSamples::Traits::WithEqualTraits";
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new RegexTraitPair(Regex.Escape(testname), "Author", "Foo").Yield().ToList());

            AssertFindsTestWithTraits(testname, new Trait("Author", "Alice Bob"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexBeforeFromOptionsThreeEqualTraits_TaefTraitWins()
        {
            string testname = "TaefSamples::Traits::WithEqualTraits";
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(
                new List<RegexTraitPair>
                {
                    new RegexTraitPair(Regex.Escape(testname), "Author", "Foo"),
                    new RegexTraitPair(Regex.Escape(testname), "Author", "Bar"),
                    new RegexTraitPair(Regex.Escape(testname), "Author", "Baz")
                });

            AssertFindsTestWithTraits(testname, new Trait("Author", "Alice Bob"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexAfterFromOptionsOneEqualTrait_FindsTestWithOneEqualTrait()
        {
            string testname = "TaefSamples::Traits::WithEqualTraits";
            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(
                new List<RegexTraitPair>
                {
                    new RegexTraitPair(Regex.Escape(testname), "Author", "Foo")
                });

            AssertFindsTestWithTraits(testname, new Trait("Author", "Foo"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexAfterFromOptionsTwoEqualTraits_FindsTestWithTwoEqualTraits()
        {
            string testname = "TaefSamples::Traits::WithEqualTraits";
            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(
                new List<RegexTraitPair>
                {
                    new RegexTraitPair(Regex.Escape(testname), "Author", "Foo"),
                    new RegexTraitPair(Regex.Escape(testname), "Author", "Bar")
                });

            AssertFindsTestWithTraits(testname, new Trait("Author", "Foo"), new Trait("Author", "Bar"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexBeforeFromOptionsTwoEqualTraits_FindsTestWithTaefAndTwoEqualTraits()
        {
            string testname = "TaefSamples::Traits::WithEqualTraits";
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(
                new List<RegexTraitPair>
                {
                    new RegexTraitPair(Regex.Escape(testname), "Author2", "Foo"),
                    new RegexTraitPair(Regex.Escape(testname), "Author2", "Bar")
                });

            AssertFindsTestWithTraits(testname, new Trait("Author", "Alice Bob"), new Trait("Author2", "Foo"), new Trait("Author2", "Bar"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexBeforeFromOptions_AddsTraitIfNotAlreadyExisting()
        {
            string testname = TestResources.TestNames.SimpleDataRow0;
            AssertFindsTestWithTraits(testname, NoTraits);

            // the regexes are matched against the TAEF names
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new RegexTraitPair(Regex.Escape(testname), "Type", "SomeNewType").Yield().ToList());
            _testCases = null;

            AssertFindsTestWithTraits(testname, new Trait("Type", "SomeNewType"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexBeforeFromOptions_TraitFromOptionsIsOverridenByTraitFromTest()
        {
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new RegexTraitPair(Regex.Escape("TaefSamples::TestMath::AddPassesWithTraits"), "Type", "SomeNewType").Yield().ToList());

            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPassesWithTraits", new Trait("Type", "Medium"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_BothRegexesFromOptions_BeforeTraitIsOverridenByAfterTrait()
        {
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new RegexTraitPair(Regex.Escape("TaefSamples::TestMath::AddPasses"), "Type", "BeforeType").Yield().ToList());
            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new RegexTraitPair(Regex.Escape("TaefSamples::TestMath::AddPasses"), "Type", "AfterType").Yield().ToList());

            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPasses", new Trait("Type", "AfterType"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexAfterFromOptions_AfterTraitOverridesTraitFromTest()
        {
            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPassesWithTraits", new Trait("Type", "Medium"));

            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new RegexTraitPair(Regex.Escape("TaefSamples::TestMath::AddPassesWithTraits"), "Type", "SomeNewType").Yield().ToList());
            _testCases = null;

            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPassesWithTraits", new Trait("Type", "SomeNewType"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexAfterFromOptions_AfterTraitOverridesModuleTrait()
        {
            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new RegexTraitPair("^TaefSamples::TestMath::", "Owner", "NewOwner").Yield().ToList());

            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPasses", new Trait("Owner", "NewOwner"));
            AssertFindsTestWithTraits("TaefSamples::ClassWithFixtures::AddPasses", NoTraits);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexAfterFromOptions_AddsTraitIfNotAlreadyExisting()
        {
            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPasses", NoTraits);

            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new RegexTraitPair(Regex.Escape("TaefSamples::TestMath::AddPasses"), "Type", "SomeNewType").Yield().ToList());
            _testCases = null;

            AssertFindsTestWithTraits("TaefSamples::TestMath::AddPasses", new Trait("Type", "SomeNewType"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_RegexButNoSourceLocation_TraitsAreAdded()
        {
            using (SampleCopy sample = SampleCopy.Create(SampleTestToUse))
            {
                EmbeddedPdbPath.Break(sample.TestDll);
                File.Delete(sample.Pdb);
                MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new RegexTraitPair(Regex.Escape("TaefSamples::TestMath::AddPasses"), "Type", "SomeNewType").Yield().ToList());

                List<TestCase> tests = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(sample.TestDll).ToList();

                tests.Should().HaveCount(TestResources.NrOfTests);
                tests.Should().OnlyContain(tc => tc.LineNumber == 0);
                AssertHasTraits(tests.Single(tc => tc.FullyQualifiedName == "TaefSamples::TestMath::AddPasses"), new Trait("Type", "SomeNewType"));
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("PDB of the test DLL could not be found"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_SelectInAdditionalTeArguments_OnlySelectedTestsAreFound()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/select:\"@Priority=1\"");

            List<TestCase> tests = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(SampleTestToUse).ToList();

            // TE.exe lists data source errors regardless of the selection
            tests.Where(tc => tc.FullyQualifiedName != TestResources.TestNames.MissingDataSource)
                .Should().NotBeEmpty()
                .And.OnlyContain(tc => tc.Traits.Any(t => t.Name == "Priority" && t.Value == "1"));
            tests.Select(tc => tc.FullyQualifiedName).Should().Contain(new[]
            {
                "TaefSamples::ClassAndMethodProperties::WithPriority1", TestResources.TestNames.IgnoredTest, TestResources.TestNames.RowWithQuote
            });
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_InvalidSelectInAdditionalTeArguments_NoTestsAreFound()
        {
            var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            discoverer.GetTestsFromTestDll(SampleTestToUse).Should().NotBeEmpty();

            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/select:\"@Name='TaefSamples::TestMath::*\"");
            discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            IList<TestCase> tests = discoverer.GetTestsFromTestDll(SampleTestToUse);

            tests.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("TE.exe returned with exit code") && s.Contains("0x06000000"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void GetTestsFromTestDll_UnknownSwitchInAdditionalTeArguments_WarningIsLoggedAndTestsAreFound()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/doesNotExist:\"foo\"");

            IList<TestCase> tests = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(SampleTestToUse);

            tests.Should().HaveCount(TestResources.NrOfTests);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Unknown command line argument 'foo'"))), Times.Once);
        }

        #region Helpers

        private static readonly string[] TemplateClasses =
        {
            "TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>",
            "TaefSamples::TemplateTests<class std::array<int,3> >",
            "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >"
        };

        private static readonly string[] TableRows = { "0", "1", "2", "Underscore" };

        private IList<TestCase> GetTestCases()
        {
            if (_testCases == null)
            {
                SampleTestToUse.AsFileInfo()
                    .Should().Exist("building the SampleTests solution produces that test DLL");

                var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
                _testCases = discoverer.GetTestsFromTestDll(SampleTestToUse);

                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.DebugError(It.IsAny<string>()), Times.Never);
                _testCases.Should().NotBeEmpty();
            }
            return _testCases;
        }

        /// <summary>
        /// Asserts that the test named <paramref name="name"/> has exactly the traits <paramref name="traits"/>, ignoring
        /// the traits resulting from the module properties of Tests_taef.dll (Architecture, Component and Owner=ModuleOwner)
        /// unless they are contained in <paramref name="traits"/>.
        /// </summary>
        private void AssertFindsTestWithTraits(string name, params Trait[] traits)
        {
            TestCase testCase = GetTestCases().SingleOrDefault(tc => tc.FullyQualifiedName == name);
            testCase.Should().NotBeNull($"Test should exist: {name}");

            AssertHasTraits(testCase, traits);
        }

        private static void AssertHasTraits(TestCase testCase, params Trait[] traits)
        {
            IEnumerable<Trait> actualTraits = testCase.Traits
                .Where(t => traits.Any(e => e.Name == t.Name) || !IsModuleTrait(t));

            actualTraits.Select(t => t.ToString()).Should().BeEquivalentTo(traits.Select(t => t.ToString()),
                $"test {testCase.FullyQualifiedName} should have these traits (actual traits: {string.Join(", ", testCase.Traits)})");
        }

        private static bool IsModuleTrait(Trait trait)
        {
            return trait.Name == "Architecture" || trait.Name == "Component" || (trait.Name == "Owner" && trait.Value == "ModuleOwner");
        }

        #endregion

    }

}
