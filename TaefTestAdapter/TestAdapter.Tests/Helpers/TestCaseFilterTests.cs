// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.TestAdapter.Fakes;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Helpers
{
    [TestClass]
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public class TestCaseFilterTests : TestAdapterTestsBase
    {
        private readonly Mock<ITestCaseFilterExpression> _mockFilterExpression = new Mock<ITestCaseFilterExpression>();
        private readonly ISet<string> _traitNames = new HashSet<string>();

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            MockRunContext.Setup(rc => rc.GetTestCaseFilter(
                    It.IsAny<IEnumerable<string>>(), It.IsAny<Func<string, TestProperty>>()))
                .Returns(_mockFilterExpression.Object);
        }

        [TestCleanup]
        public override void TearDown()
        {
            base.TearDown();

            _mockFilterExpression.Reset();
            _traitNames.Clear();
        }

        private List<TestCase> CreateVsTestCases(params string[] taefNames)
        {
            return TestDataCreator.CreateDummyTestCases(taefNames).Select(tc => tc.ToVsTestCase()).ToList();
        }

        private List<TestCase> Filter(IEnumerable<TestCase> testCases, string filter, params string[] traitNames)
        {
            SetupTestCaseFilter(filter);
            var filterObject = new TestCaseFilter(MockRunContext.Object, new HashSet<string>(traitNames), TestEnvironment.Logger);
            return filterObject.Filter(testCases).ToList();
        }

        #region Filter expressions (mocked)

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_NoFilterExpressionProvided_NoFiltering()
        {
            MockRunContext.Setup(rc => rc.GetTestCaseFilter(
                    It.IsAny<IEnumerable<string>>(), It.IsAny<Func<string, TestProperty>>()))
                .Returns((ITestCaseFilterExpression)null);
            IEnumerable<TestCase> testCases = CreateVsTestCases("Foo::Bar", "Foo::Baz");

            TestCaseFilter filter = new TestCaseFilter(MockRunContext.Object, _traitNames, TestEnvironment.Logger);
            IEnumerable<TestCase> filteredTestCases = filter.Filter(testCases).ToList();

            AssertAreEqual(testCases, filteredTestCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_ExpressionAcceptsAnything_NoFiltering()
        {
            _mockFilterExpression.Setup(e => e.MatchTestCase(It.IsAny<TestCase>(), It.IsAny<Func<string, object>>())).Returns(true);
            IEnumerable<TestCase> testCases = CreateVsTestCases("Foo::Bar", "Foo::Baz");

            TestCaseFilter filter = new TestCaseFilter(MockRunContext.Object, _traitNames, TestEnvironment.Logger);
            IEnumerable<TestCase> filteredTestCases = filter.Filter(testCases).ToList();

            AssertAreEqual(testCases, filteredTestCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_ExpressionMatchesNothing_EmptyResult()
        {
            _mockFilterExpression.Setup(e => e.MatchTestCase(It.IsAny<TestCase>(), It.IsAny<Func<string, object>>())).Returns(false);
            IEnumerable<TestCase> testCases = CreateVsTestCases("Foo::Bar", "Foo::Baz");

            TestCaseFilter filter = new TestCaseFilter(MockRunContext.Object, _traitNames, TestEnvironment.Logger);
            IEnumerable<TestCase> filteredTestCases = filter.Filter(testCases).ToList();

            AssertAreEqual(new List<TestCase>(), filteredTestCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_ExpressionMatchesDisplayName_CorrectFiltering()
        {
            List<TestCase> testCases = CreateVsTestCases("Foo::Bar", "Foo::Baz");
            _mockFilterExpression.Setup(e => e.MatchTestCase(It.Is<TestCase>(tc => tc == testCases[0]), It.Is<Func<string, object>>(f => f("DisplayName").ToString() == "Foo::Bar"))).Returns(true);

            TestCaseFilter filter = new TestCaseFilter(MockRunContext.Object, _traitNames, TestEnvironment.Logger);
            IEnumerable<TestCase> filteredTestCases = filter.Filter(testCases).ToList();

            AssertAreEqual(testCases[0].Yield(), filteredTestCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Matches_ExpressionMatchesDisplayName_CorrectFiltering()
        {
            List<TestCase> testCases = CreateVsTestCases("Foo::Bar");
            _mockFilterExpression.Setup(e => e.MatchTestCase(It.Is<TestCase>(tc => tc == testCases[0]), It.Is<Func<string, object>>(f => f("DisplayName").ToString() == "Foo::Bar"))).Returns(true);

            TestCaseFilter filter = new TestCaseFilter(MockRunContext.Object, _traitNames, TestEnvironment.Logger);
            bool matches = filter.Matches(testCases[0]);

            matches.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_Trait_CorrectFiltering()
        {
            List<TestCase> testCases = CreateVsTestCases("Foo::Bar", "Foo::Baz");
            testCases[0].Traits.Add(new Trait("MyTrait", "value1"));
            SetupFilterToAcceptTraitForTestCase(testCases[0], "MyTrait", "value1");
            testCases[1].Traits.Add(new Trait("MyTrait", "value2"));
            SetupFilterToAcceptTraitForTestCase(testCases[1], "MyTrait", "value2");
            _traitNames.Add("MyTrait");

            TestCaseFilter filter = new TestCaseFilter(MockRunContext.Object, _traitNames, TestEnvironment.Logger);
            IEnumerable<TestCase> filteredTestCases = filter.Filter(testCases).ToList();

            AssertAreEqual(testCases, filteredTestCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SetPropertyValue_Trait_CorrectValidation()
        {
            var testCase = CreateVsTestCases("Foo::Bar").Single();
            testCase.Traits.Add(new Trait("MyTrait", "value1"));

            //registers TestProperty objects for trait names
            // ReSharper disable once ObjectCreationAsStatement
            new TestCaseFilter(MockRunContext.Object, new HashSet<string>{"MyTrait"}, TestEnvironment.Logger);
            TestProperty property = TestProperty.Find("MyTrait");

            Action action = () => testCase.SetPropertyValue(property, "i");
            action.Should().NotThrow();

            action = () => testCase.SetPropertyValue(property, "_i");
            action.Should().NotThrow();

            action = () => testCase.SetPropertyValue(property, "äöüÄÖÜß$");
            action.Should().NotThrow();

            action = () => testCase.SetPropertyValue(property, "_äöüÄÖÜß$");
            action.Should().NotThrow();

            // since we are not at the beginning of the method name
            action = () => testCase.SetPropertyValue(property, "1");
            action.Should().NotThrow();

            action = () => testCase.SetPropertyValue(property, "_1");
            action.Should().NotThrow();

            action = () => testCase.SetPropertyValue(property, "_");
            action.Should().NotThrow();

            action = () => testCase.SetPropertyValue(property, "");
            action.Should().Throw<ArgumentException>().WithMessage("MyTrait");

            action = () => testCase.SetPropertyValue(property, "_(");
            action.Should().Throw<ArgumentException>().WithMessage("MyTrait");

            action = () => testCase.SetPropertyValue(property, "a(");
            action.Should().Throw<ArgumentException>().WithMessage("MyTrait");

            action = () => testCase.SetPropertyValue(property, "1(");
            action.Should().Throw<ArgumentException>().WithMessage("MyTrait");

            action = () => testCase.SetPropertyValue(property, "%");
            action.Should().Throw<ArgumentException>().WithMessage("MyTrait");

            action = () => testCase.SetPropertyValue(property, "+");
            action.Should().Throw<ArgumentException>().WithMessage("MyTrait");
        }

        #endregion

        #region Filter expressions as provided by the VsTest framework (FakeTestCaseFilterExpression)

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_FullyQualifiedName_IsTheNameWithDotsAsScopeSeparators()
        {
            List<TestCase> testCases = CreateVsTestCases("Ns::Class::Method", "Ns::Class::Other", "Ns::Class::Method#row::with::colons", "Other::Method");

            Filter(testCases, "FullyQualifiedName=Ns.Class.Method").Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo("Ns::Class::Method");
            Filter(testCases, "FullyQualifiedName=Ns.Class.Method#row::with::colons").Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo("Ns::Class::Method#row::with::colons");
            Filter(testCases, "FullyQualifiedName~Ns.Class.").Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo("Ns::Class::Method", "Ns::Class::Other", "Ns::Class::Method#row::with::colons");
            Filter(testCases, "FullyQualifiedName=Ns::Class::Method").Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_DisplayName_IsTheTaefName()
        {
            List<TestCase> testCases = CreateVsTestCases("Ns::Class::Method", "Ns::Class::Method#metadataSet0", "TaefSamples::Ümlautß::Täst");

            Filter(testCases, "DisplayName=Ns::Class::Method#metadataSet0").Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo("Ns::Class::Method#metadataSet0");
            Filter(testCases, "DisplayName~Ns::Class::Method").Should().HaveCount(2);
            Filter(testCases, "DisplayName=TaefSamples::Ümlautß::Täst").Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo("TaefSamples::Ümlautß::Täst");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_TraitNames_AreNotCaseSensitive()
        {
            List<TestCase> testCases = CreateVsTestCases("A::B", "A::C");
            testCases[0].Traits.Add(new Trait("Owner", "Me"));
            testCases[1].Traits.Add(new Trait("Owner", "You"));

            Filter(testCases, "owner=Me", "Owner").Select(tc => tc.DisplayName).Should().BeEquivalentTo("A::B");
            Filter(testCases, "OWNER!=Me", "Owner").Select(tc => tc.DisplayName).Should().BeEquivalentTo("A::C");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_TraitWithSeveralValues_MatchesIfOneValueMatches()
        {
            List<TestCase> testCases = CreateVsTestCases("A::B", "A::C", "A::D");
            testCases[0].Traits.Add(new Trait("Category", "Smoke"));
            testCases[0].Traits.Add(new Trait("Category", "Nightly"));
            testCases[1].Traits.Add(new Trait("Category", "Nightly"));
            // multi-valued TAEF metadata arrives as one space-joined trait
            testCases[2].Traits.Add(new Trait("Category", "Smoke Nightly"));

            Filter(testCases, "Category=Smoke", "Category").Select(tc => tc.DisplayName).Should().BeEquivalentTo("A::B");
            Filter(testCases, "Category=Nightly", "Category").Select(tc => tc.DisplayName).Should().BeEquivalentTo("A::B", "A::C");
            Filter(testCases, "Category=Smoke Nightly", "Category").Select(tc => tc.DisplayName).Should().BeEquivalentTo("A::D");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_TraitAndName_BothMustMatch()
        {
            List<TestCase> testCases = CreateVsTestCases("A::B", "A::C", "X::B");
            testCases[0].Traits.Add(new Trait("Priority", "1"));
            testCases[1].Traits.Add(new Trait("Priority", "2"));
            testCases[2].Traits.Add(new Trait("Priority", "1"));

            Filter(testCases, "Priority=1&FullyQualifiedName~A.", "Priority").Select(tc => tc.DisplayName).Should().BeEquivalentTo("A::B");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_SupportedProperties_ContainBasePropertiesAndTraits()
        {
            SetupTestCaseFilter("Owner=Me");
            var filter = new TestCaseFilter(MockRunContext.Object, new HashSet<string> { "Owner", "Priority" }, TestEnvironment.Logger);

            filter.Filter(CreateVsTestCases("A::B")).ToList();

            MockRunContext.Verify(rc => rc.GetTestCaseFilter(
                It.Is<IEnumerable<string>>(properties => new[] { "FullyQualifiedName", "DisplayName", "LineNumber", "CodeFilePath", "ExecutorUri", "Id", "Source", "Owner", "Priority" }
                    .All(p => properties.Contains(p))),
                It.Is<Func<string, TestProperty>>(provider => provider("FullyQualifiedName") == TestCaseProperties.FullyQualifiedName && provider("owner") != null && provider("Unknown") == null)));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_BasePropertiesOfTestCase_CanBeFilteredOn()
        {
            var testCase1 = new Model.TestCase("A::B", @"C:\Tests\First_taef.dll", "A::B", @"C:\Tests\First.cpp", 17).ToVsTestCase();
            var testCase2 = new Model.TestCase("A::B", @"C:\Tests\Second_taef.dll", "A::B", @"C:\Tests\Second.cpp", 42).ToVsTestCase();
            var testCases = new List<TestCase> { testCase1, testCase2 };

            Filter(testCases, @"Source~Second_taef").Should().Equal(testCase2);
            Filter(testCases, "LineNumber=17").Should().Equal(testCase1);
            Filter(testCases, @"CodeFilePath~First.cpp").Should().Equal(testCase1);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_TraitWithNameOfBaseProperty_WarningIsLoggedAndTraitIsIgnored()
        {
            List<TestCase> testCases = CreateVsTestCases("A::B");
            testCases[0].Traits.Add(new Trait("DisplayName", "Foo"));

            Filter(testCases, "DisplayName=Foo", "DisplayName").Should().BeEmpty();

            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Trait has same name as base test property") && s.Contains("DisplayName"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Filter_InvalidFilter_ErrorIsLoggedAndNoTestMatches()
        {
            List<TestCase> testCases = CreateVsTestCases("A::B");

            Filter(testCases, "UnknownProperty=Foo").Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.StartsWith("Test case filter is invalid, no tests will be run:") && s.Contains("UnknownProperty"))), Times.Once);

            var filter = new TestCaseFilter(MockRunContext.Object, new HashSet<string>(), TestEnvironment.Logger);
            filter.Matches(testCases[0]).Should().BeFalse();
        }

        #endregion

        private void SetupFilterToAcceptTraitForTestCase(TestCase testCase, string traitName, string traitValue)
        {
            _mockFilterExpression.Setup(e => e.MatchTestCase(It.Is<TestCase>(tc => tc == testCase), It.Is<Func<string, object>>(f => f(traitName).ToString() == traitValue))).Returns(true);
        }


        private static void AssertAreEqual(IEnumerable<TestCase> testCases1, IEnumerable<TestCase> testCases2)
        {
            testCases1.Should().HaveSameCount(testCases2);

            using (IEnumerator<TestCase> enumerator1 = testCases1.GetEnumerator())
            using (IEnumerator<TestCase> enumerator2 = testCases2.GetEnumerator())
            {
                while (enumerator1.MoveNext() && enumerator2.MoveNext())
                {
                    AssertAreEqual(enumerator1.Current, enumerator2.Current);
                }
            }
        }

        private static void AssertAreEqual(TestCase testCase1, TestCase testCase2)
        {
            testCase1.FullyQualifiedName.Should().BeEquivalentTo(testCase2.FullyQualifiedName);
            testCase1.DisplayName.Should().BeEquivalentTo(testCase2.DisplayName);
            testCase1.CodeFilePath.Should().BeEquivalentTo(testCase2.CodeFilePath);
            testCase1.Source.Should().BeEquivalentTo(testCase2.Source);
            testCase1.LineNumber.Should().Be(testCase2.LineNumber);
            testCase1.Id.Should().Be(testCase2.Id);
            testCase1.ExecutorUri.Should().BeEquivalentTo(testCase2.ExecutorUri);
            testCase1.Traits.Count().Should().Be(testCase2.Traits.Count());
        }

    }

}
