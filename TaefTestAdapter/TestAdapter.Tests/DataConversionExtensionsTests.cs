// This file has been added for TAEF support.

using System;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Model;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;
using VsTestProperty = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestProperty;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using VsTestResultMessage = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Tests of the conversions between the adapter's model (test names = TAEF names) and the VsTest framework's model
    /// (fully qualified names with '.' as scope separator, display name and property TaefName = TAEF name).
    /// </summary>
    [TestClass]
    public class DataConversionExtensionsTests
    {
        private const string TestDll = @"C:\Tests\MyTests_taef.dll";

        private static TestCase CreateTestCase(string taefName, string codeFile = @"C:\Tests\MyTests.cpp", int line = 42)
        {
            var testCase = new TestCase(taefName, TestDll, taefName, codeFile, line);
            testCase.Traits.Add(new Trait("Owner", "Me"));
            testCase.Traits.Add(new Trait("Category", "Smoke Nightly"));
            testCase.Properties.Add(new TestCaseMetaDataProperty(3, 17));
            return testCase;
        }

        #region ToVsTestCase / ToTestCase

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsTestCase_SimpleTest_IsConvertedCorrectly()
        {
            TestCase testCase = CreateTestCase("Ns::MyClass::MyMethod");

            VsTestCase vsTestCase = testCase.ToVsTestCase();

            vsTestCase.FullyQualifiedName.Should().Be("Ns.MyClass.MyMethod");
            vsTestCase.DisplayName.Should().Be("Ns::MyClass::MyMethod");
            vsTestCase.GetTaefName().Should().Be("Ns::MyClass::MyMethod");
            vsTestCase.GetPropertyValue(DataConversionExtensions.TaefNameProperty).Should().Be("Ns::MyClass::MyMethod");
            vsTestCase.Source.Should().Be(TestDll);
            vsTestCase.ExecutorUri.Should().Be(TestExecutor.ExecutorUri);
            vsTestCase.ExecutorUri.ToString().Should().BeEquivalentTo("executor://TestAdapterForTaef/v1");
            vsTestCase.CodeFilePath.Should().Be(@"C:\Tests\MyTests.cpp");
            vsTestCase.LineNumber.Should().Be(42);
            vsTestCase.Traits.Select(t => $"{t.Name}={t.Value}").Should().BeEquivalentTo("Owner=Me", "Category=Smoke Nightly");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsTestCase_TaefNames_FullyQualifiedNamesUseDotsOutsideOfTemplatesAndDataRows()
        {
            AssertVsName("TaefSamples::TestMath::AddPasses", "TaefSamples.TestMath.AddPasses");
            AssertVsName("TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_Named_Named::Test", "TaefSamples.Namespace_1.Namespace_2_Nested.Namespace_Named_Named.Test");
            AssertVsName(TestNames.SimpleDataRow0, "TaefSamples.TableDataTests.Simple#0");
            AssertVsName(TestNames.LightweightDataRow0, "TaefSamples.LightweightDataTests.SingleValue#metadataSet0");
            AssertVsName(TestNames.RowWithColons, "TaefSamples.NamedRows.SpecialCharacters#with::colons [x]");
            AssertVsName(TestNames.RowWithQuote, "TaefSamples.NamedRows.SpecialCharacters#with'quote");
            AssertVsName("TaefSamples::NamedRows::SpecialCharacters#with#hash", "TaefSamples.NamedRows.SpecialCharacters#with#hash");
            AssertVsName(TestNames.MissingDataSource, "TaefSamples.MissingDataSource.Test#error");
            AssertVsName(TestNames.TemplateTest, "TaefSamples.TemplateTests<class std::vector<int,class std::allocator<int> > >.CanIterate");
            AssertVsName("TaefSamples::ClassTemplates::NumberTemplateTests<signed char>::CanHoldLargeSum", "TaefSamples.ClassTemplates.NumberTemplateTests<signed char>.CanHoldLargeSum");
            AssertVsName(TestNames.AnonymousNamespaceTest, "TaefSamples.`anonymous-namespace'.Namespace_Anon.Test");
            AssertVsName(TestNames.UmlautTest, "TaefSamples.Ümlautß.Täst");
            AssertVsName("TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前", "TaefSamples.NamedRows.SpecialCharacters#Ünïcødé 名前");
            // class level data (the class row is part of the class)
            AssertVsName("ClassData#metadataSet0::M1", "ClassData#metadataSet0.M1");
            AssertVsName("Ns::ClassData#metadataSet1::M1#metadataSet2", "Ns.ClassData#metadataSet1.M1#metadataSet2");
            // global function
            AssertVsName("GlobalTest", "GlobalTest");
        }

        private static void AssertVsName(string taefName, string expectedVsName)
        {
            VsTestCase vsTestCase = CreateTestCase(taefName).ToVsTestCase();

            vsTestCase.FullyQualifiedName.Should().Be(expectedVsName, $"VS name of {taefName}");
            vsTestCase.DisplayName.Should().Be(taefName);
            vsTestCase.GetTaefName().Should().Be(taefName);
            vsTestCase.ToTestCase().FullyQualifiedName.Should().Be(taefName, "round trip");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_VsTestCase_RoundTripKeepsEverything()
        {
            TestCase testCase = CreateTestCase(TestNames.RowWithColons);

            TestCase roundTripped = testCase.ToVsTestCase().ToTestCase();

            roundTripped.Should().Be(testCase);
            roundTripped.FullyQualifiedName.Should().Be(TestNames.RowWithColons);
            roundTripped.DisplayName.Should().Be(TestNames.RowWithColons);
            roundTripped.Source.Should().Be(TestDll);
            roundTripped.CodeFilePath.Should().Be(testCase.CodeFilePath);
            roundTripped.LineNumber.Should().Be(testCase.LineNumber);
            roundTripped.Traits.Select(t => $"{t.Name}={t.Value}").Should().BeEquivalentTo("Owner=Me", "Category=Smoke Nightly");
            TestCaseMetaDataProperty metaData = roundTripped.Properties.OfType<TestCaseMetaDataProperty>().Single();
            metaData.NrOfTestCasesInClass.Should().Be(3);
            metaData.NrOfTestCasesInTestDll.Should().Be(17);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_TestWithDataRowIndex_RoundTripKeepsDataRowIndex()
        {
            TestCase testCase = CreateTestCase("W::Rows#a*");
            testCase.Properties.Add(new TestCaseDataRowIndexProperty(4));

            VsTestCase vsTestCase = testCase.ToVsTestCase();
            vsTestCase.GetPropertyValue(VsTestProperty.Find(TestCaseDataRowIndexProperty.Id)).Should().Be("4");
            TestCase roundTripped = vsTestCase.ToTestCase();

            roundTripped.Properties.OfType<TestCaseDataRowIndexProperty>().Single().Index.Should().Be(4);
            roundTripped.Properties.OfType<TestCaseMetaDataProperty>().Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_TestWithoutDataRowIndex_HasNoDataRowIndex()
        {
            TestCase roundTripped = CreateTestCase("W::Light#metadataSet0").ToVsTestCase().ToTestCase();

            roundTripped.Properties.OfType<TestCaseDataRowIndexProperty>().Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_InvalidDataRowIndex_IsIgnored()
        {
            VsTestCase vsTestCase = CreateTestCase("W::Rows#a*").ToVsTestCase();
            VsTestProperty dataRowIndexProperty = VsTestProperty.Find(TestCaseDataRowIndexProperty.Id);
            dataRowIndexProperty.Should().NotBeNull("the property is registered by DataConversionExtensions");

            foreach (string invalid in new[] { "", "abc", "-1", "07", " 1" })
            {
                vsTestCase.SetPropertyValue(dataRowIndexProperty, invalid);
                vsTestCase.ToTestCase().Properties.OfType<TestCaseDataRowIndexProperty>().Should().BeEmpty($"'{invalid}' is no valid index");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_VsTestCaseWithoutTaefNameProperty_TaefNameIsTakenFromDisplayName()
        {
            var vsTestCase = new VsTestCase("Ns.MyClass.MyMethod#row::x", TestExecutor.ExecutorUri, TestDll)
            {
                DisplayName = "Ns::MyClass::MyMethod#row::x"
            };

            vsTestCase.GetTaefName().Should().Be("Ns::MyClass::MyMethod#row::x");
            TestCase testCase = vsTestCase.ToTestCase();

            testCase.FullyQualifiedName.Should().Be("Ns::MyClass::MyMethod#row::x");
            testCase.DisplayName.Should().Be("Ns::MyClass::MyMethod#row::x");
            testCase.Properties.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTestCase_TaefNamePropertyIsEmpty_TaefNameIsTakenFromDisplayName()
        {
            VsTestCase vsTestCase = CreateTestCase("A::B").ToVsTestCase();
            vsTestCase.SetPropertyValue(DataConversionExtensions.TaefNameProperty, "");

            vsTestCase.GetTaefName().Should().Be("A::B");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TaefNameProperty_IsRegisteredStringPropertyOfTestCases()
        {
            // (accessing the property registers it)
            VsTestProperty registeredProperty = DataConversionExtensions.TaefNameProperty;
            VsTestProperty property = VsTestProperty.Find(DataConversionExtensions.TaefNamePropertyId);

            property.Should().NotBeNull();
            property.Should().BeSameAs(registeredProperty);
            property.Id.Should().Be("TaefTestAdapter.TaefName");
            property.GetValueType().Should().Be(typeof(string));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsTestCase_SameTestInDifferentDlls_DifferentIds()
        {
            var testCase1 = new TestCase("A::B", @"C:\Tests\First_taef.dll", "A::B", "", 0);
            var testCase2 = new TestCase("A::B", @"C:\Tests\Second_taef.dll", "A::B", "", 0);

            testCase1.ToVsTestCase().Id.Should().NotBe(testCase2.ToVsTestCase().Id);
            testCase1.ToVsTestCase().Id.Should().Be(new TestCase("A::B", @"C:\Tests\First_taef.dll", "A::B", "", 0).ToVsTestCase().Id);
        }

        #endregion

        #region ToVsTestResult

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsTestResult_FailedResultWithOutput_IsConvertedCorrectly()
        {
            var result = new TestResult(CreateTestCase("Ns::MyClass::MyMethod"))
            {
                Outcome = Model.TestOutcome.Failed,
                ComputerName = "MyComputer",
                DisplayName = "Ns::MyClass::MyMethod",
                Duration = TimeSpan.FromMilliseconds(1234),
                ErrorMessage = "Verify: AreEqual(1, 2) - Values (1, 2)",
                ErrorStackTrace = @"at Ns::MyClass::MyMethod in C:\Tests\MyTests.cpp:line 43",
                Output = "Some output" + Environment.NewLine + "More output"
            };

            VsTestResult vsResult = result.ToVsTestResult();

            vsResult.TestCase.FullyQualifiedName.Should().Be("Ns.MyClass.MyMethod");
            vsResult.TestCase.GetTaefName().Should().Be("Ns::MyClass::MyMethod");
            vsResult.Outcome.Should().Be(VsTestOutcome.Failed);
            vsResult.ComputerName.Should().Be("MyComputer");
            vsResult.DisplayName.Should().Be("Ns::MyClass::MyMethod");
            vsResult.Duration.Should().Be(TimeSpan.FromMilliseconds(1234));
            vsResult.ErrorMessage.Should().Be("Verify: AreEqual(1, 2) - Values (1, 2)");
            vsResult.ErrorStackTrace.Should().Be(@"at Ns::MyClass::MyMethod in C:\Tests\MyTests.cpp:line 43");
            vsResult.Messages.Should().ContainSingle();
            vsResult.Messages[0].Category.Should().Be(VsTestResultMessage.StandardOutCategory);
            vsResult.Messages[0].Text.Should().Be("Some output" + Environment.NewLine + "More output");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsTestResult_ResultWithoutOutput_HasNoMessages()
        {
            var result = new TestResult(CreateTestCase("A::B")) { Outcome = Model.TestOutcome.Passed, ComputerName = "MyComputer" };
            result.ToVsTestResult().Messages.Should().BeEmpty();

            result.Output = "";
            result.ToVsTestResult().Messages.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsTestOutcome_AllOutcomes_AreMappedCorrectly()
        {
            Model.TestOutcome.Passed.ToVsTestOutcome().Should().Be(VsTestOutcome.Passed);
            Model.TestOutcome.Failed.ToVsTestOutcome().Should().Be(VsTestOutcome.Failed);
            Model.TestOutcome.Skipped.ToVsTestOutcome().Should().Be(VsTestOutcome.Skipped);
            Model.TestOutcome.None.ToVsTestOutcome().Should().Be(VsTestOutcome.None);
            Model.TestOutcome.NotFound.ToVsTestOutcome().Should().Be(VsTestOutcome.NotFound);

            foreach (Model.TestOutcome outcome in Enum.GetValues(typeof(Model.TestOutcome)))
            {
                Action action = () => outcome.ToVsTestOutcome();
                action.Should().NotThrow($"outcome {outcome}");
            }
            Action invalid = () => ((Model.TestOutcome)4711).ToVsTestOutcome();
            invalid.Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetSeverityAndGetTestMessageLevel_AllLevels_AreMappedBothWays()
        {
            TestMessageLevel.Informational.GetSeverity().Should().Be(Severity.Info);
            TestMessageLevel.Warning.GetSeverity().Should().Be(Severity.Warning);
            TestMessageLevel.Error.GetSeverity().Should().Be(Severity.Error);

            foreach (Severity severity in Enum.GetValues(typeof(Severity)))
            {
                severity.GetTestMessageLevel().GetSeverity().Should().Be(severity);
            }
        }

        #endregion

    }
}
