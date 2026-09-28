// This file has been modified by Microsoft on 9/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using Microsoft.Win32.SafeHandles;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestCases;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter
{
    [TestClass]
    public class TaefDiscovererTests : TestsBase
    {

        #region TAEF test DLL detection (IsTaefTestDll)

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WithRegexFromOptions_MatchesCorrectly()
        {
            AssertIsTaefTestDll("SomeWeirdExpression", true, "Some.*Expression");
            AssertIsTaefTestDll("SomeWeirdOtherThing", false, "Some.*Expression");
            AssertIsTaefTestDll("MyTaefTests.dll", false, "Some.*Expression");
            AssertIsTaefTestDll(@"C:\out\Debug\MyTests_taef.dll", true, @"_taef\.dll$");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WithUnparsableRegexFromOptions_ProducesErrorMessage()
        {
            bool result = TaefDiscoverer.IsTaefTestDll("my.dll", "d[ddd[", TestEnvironment.Logger);

            result.Should().BeFalse();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("'d[ddd['"))), Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WithSlowRegex_TimesOutAndProducesErrorMessage()
        {
            // regex from https://stackoverflow.com/questions/9687596/slow-regex-performance
            string slowRegex = "\"(([^\\\\\"]*)(\\\\.)?)*\"";

            var stopwatch = Stopwatch.StartNew();
            bool result = TaefDiscoverer.IsTaefTestDll(
                "\"This is an unterminated string and takes FOREVER to match",
                slowRegex,
                TestEnvironment.Logger);
            stopwatch.Stop();

            result.Should().BeFalse();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains($"'{slowRegex}'") && s.Contains("timed out"))), Times.Exactly(1));
            stopwatch.Elapsed.Should().BeGreaterOrEqualTo(TaefDiscoverer.RegexTimeout - TestMetadata.Tolerance);
            // generous upper bound (the regex would run for ages without timeout)
            stopwatch.Elapsed.Should().BeLessThan(TaefDiscoverer.RegexTimeout + TimeSpan.FromSeconds(5));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WithIndicatorFile_IsRecognizedAsTestDll()
        {
            using (var directory = new TemporaryDirectory())
            {
                string plainDll = SyntheticPeFile.PlainDll().WriteTo(directory.GetPath("SomeWeirdName.dll"));
                TaefDiscoverer.IsTaefTestDll(plainDll, "", TestEnvironment.Logger).Should().BeFalse();

                File.Create(plainDll + TaefConstants.IndicatorFileExtension).Dispose();

                TaefDiscoverer.IsTaefTestDll(plainDll, "", TestEnvironment.Logger).Should().BeTrue();
                TaefDiscoverer.IsTaefTestDll(plainDll, "DoesNotMatch", TestEnvironment.Logger).Should().BeTrue("the indicator file wins over the regex");
                TaefConstants.IndicatorFileExtension.Should().Be(".is_taef_test");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WithoutRegex_TaefMetadataIsChecked()
        {
            using (var directory = new TemporaryDirectory())
            {
                TaefDiscoverer.IsTaefTestDll(SyntheticPeFile.TaefTestDll().WriteTo(directory.GetPath("Taef64.dll")), "", TestEnvironment.Logger).Should().BeTrue();
                TaefDiscoverer.IsTaefTestDll(SyntheticPeFile.TaefTestDll(SyntheticPeFile.MachineX86).WriteTo(directory.GetPath("Taef32.dll")), "", TestEnvironment.Logger).Should().BeTrue();
                TaefDiscoverer.IsTaefTestDll(SyntheticPeFile.PlainDll().WriteTo(directory.GetPath("Plain.dll")), "", TestEnvironment.Logger).Should().BeFalse();
                TaefDiscoverer.IsTaefTestDll(directory.CreateFile("Text.dll", "not a dll"), "", TestEnvironment.Logger).Should().BeFalse();
                TaefDiscoverer.IsTaefTestDll(directory.GetPath("DoesNotExist.dll"), null, TestEnvironment.Logger).Should().BeFalse();
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WithRegex_TaefMetadataIsNotChecked()
        {
            using (var directory = new TemporaryDirectory())
            {
                string taefDll = SyntheticPeFile.TaefTestDll().WriteTo(directory.GetPath("Taef.dll"));
                string plainDll = SyntheticPeFile.PlainDll().WriteTo(directory.GetPath("Plain_taef.dll"));

                TaefDiscoverer.IsTaefTestDll(taefDll, @"_taef\.dll$", TestEnvironment.Logger).Should().BeFalse();
                TaefDiscoverer.IsTaefTestDll(plainDll, @"_taef\.dll$", TestEnvironment.Logger).Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void IsTaefTestDll_SampleDlls_AreRecognizedAndOtherDllsAreNot()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                foreach (string dll in TestResources.AllSampleTestDlls)
                {
                    TaefDiscoverer.IsTaefTestDll(TestResources.GetSampleDll(dll, configuration), "", TestEnvironment.Logger)
                        .Should().BeTrue($"{dll} ({configuration}) is a TAEF test DLL");
                }
                foreach (string dll in TestResources.NonTaefSampleDlls)
                {
                    TaefDiscoverer.IsTaefTestDll(TestResources.GetSampleDll(dll, configuration), "", TestEnvironment.Logger)
                        .Should().BeFalse($"{dll} ({configuration}) is no TAEF test DLL");
                }
            }
        }

        #endregion

        #region File origin (VerifyTestDllTrust)

        [TestMethod]
        [TestCategory(Unit)]
        public void VerifyTestDllTrust_LocalFile_IsTrusted()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = SyntheticPeFile.TaefTestDll().WriteTo(directory.GetPath("Local_taef.dll"));

                TaefDiscoverer.VerifyTestDllTrust(testDll, MockOptions.Object, MockLogger.Object).Should().BeTrue();
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VerifyTestDllTrust_DownloadedFile_IsNotTrustedUnlessOriginCheckIsSkipped()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = SyntheticPeFile.TaefTestDll().WriteTo(directory.GetPath("Downloaded_taef.dll"));
                MarkAsDownloaded(testDll);

                TaefDiscoverer.VerifyTestDllTrust(testDll, MockOptions.Object, MockLogger.Object).Should().BeFalse();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains(testDll) && s.Contains("another computer"))), Times.Once);

                MockOptions.Setup(o => o.SkipOriginCheck).Returns(true);
                TaefDiscoverer.VerifyTestDllTrust(testDll, MockOptions.Object, MockLogger.Object).Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestsFromTestDll_DownloadedTestDll_TeIsNotStarted()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                string testDll = SyntheticPeFile.TaefTestDll().WriteTo(Path.Combine(fakeTe.Directory, "Downloaded_taef.dll"));
                MarkAsDownloaded(testDll);

                new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDll(testDll).Should().BeEmpty();

                fakeTe.HasBeenRun.Should().BeFalse();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains(testDll))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void VerifyTestDllTrust_SampleDllsAndHelperExecutables_AreTrusted()
        {
            var files = TestResources.AllSampleConfigurations
                .SelectMany(c => new[] { TestResources.GetSampleDll(TestResources.TestsDll, c), TestResources.GetSampleDll(TestResources.CrashingTestsDll, c) })
                .Concat(new[] { TestResources.SemaphoreExe, TestResources.AlwaysCrashingExe, TestResources.AlwaysFailingExe });

            foreach (string file in files)
            {
                string fullPath = Path.GetFullPath(file);
                fullPath.AsFileInfo().Should().Exist();
                TaefDiscoverer.VerifyTestDllTrust(fullPath, MockOptions.Object, MockLogger.Object)
                    .Should().BeTrue($"'{fullPath}' is built by us");
            }
        }

        #endregion

        #region Discovery with a fake TE.exe

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestsFromTestDll_TaefTestDll_TestsListedByTeAreReturned()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                string testDll = SyntheticPeFile.TaefTestDll().WriteTo(Path.Combine(fakeTe.Directory, TestResources.TestsDll));

                IList<TestCase> testCases = new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDll(testDll);

                testCases.Should().HaveCount(TestResources.NrOfTests);
                testCases.Should().OnlyContain(tc => tc.Source == testDll && tc.DisplayName == tc.FullyQualifiedName);
                fakeTe.GetArguments().Should().StartWith($"\"{testDll}\" /listProperties /runIgnoredTests");
                MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains($"Found {TestResources.NrOfTests} tests in test DLL {testDll}"))), Times.Once);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestsFromTestDll_NoTaefTestDll_TeIsNotStarted()
        {
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                string plainDll = SyntheticPeFile.PlainDll().WriteTo(Path.Combine(fakeTe.Directory, "Plain.dll"));

                IList<TestCase> testCases = new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDll(plainDll);

                testCases.Should().BeEmpty();
                fakeTe.HasBeenRun.Should().BeFalse();
                MockLogger.Verify(l => l.LogInfo(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DiscoverTests_SeveralFiles_TestsOfTaefTestDllsAreReported()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            // the fake TE.exe prints the listing of Tests_taef.dll for every test DLL
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                var testDlls = new[]
                {
                    SyntheticPeFile.TaefTestDll().WriteTo(Path.Combine(fakeTe.Directory, "A_taef.dll")),
                    SyntheticPeFile.TaefTestDll(SyntheticPeFile.MachineX86).WriteTo(Path.Combine(fakeTe.Directory, "B_taef.dll")),
                    SyntheticPeFile.PlainDll().WriteTo(Path.Combine(fakeTe.Directory, "Plain.dll")),
                    Path.Combine(fakeTe.Directory, "DoesNotExist.dll")
                };
                var reporter = new FakeFrameworkReporter();

                new TaefDiscoverer(MockLogger.Object, MockOptions.Object).DiscoverTests(testDlls, reporter);

                reporter.ReportedTestCasesFound.Should().HaveCount(2 * TestResources.NrOfTests);
                reporter.ReportedTestCasesFound.Select(tc => tc.Source).Distinct().Should().BeEquivalentTo(testDlls[0], testDlls[1]);
                MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.StartsWith($"Found {TestResources.NrOfTests} tests in test DLL"))), Times.Exactly(2));
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestsFromTestDlls_SeveralDlls_AllTestsAreReturned()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                var testDlls = Enumerable.Range(0, 5)
                    .Select(i => SyntheticPeFile.TaefTestDll().WriteTo(Path.Combine(fakeTe.Directory, $"Test{i}_taef.dll")))
                    .ToList();

                IList<TestCase> testCases = new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDlls(testDlls);

                testCases.Should().HaveCount(testDlls.Count * TestResources.NrOfTests);
                testCases.GroupBy(tc => tc.Source).Should().OnlyContain(g => g.Count() == TestResources.NrOfTests);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestsFromTestDlls_Canceled_NoTestsAreReturnedAndTeIsNotStarted()
        {
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                string testDll = SyntheticPeFile.TaefTestDll().WriteTo(Path.Combine(fakeTe.Directory, "A_taef.dll"));

                IList<TestCase> testCases = new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDlls(new[] { testDll }, () => true);

                testCases.Should().BeEmpty();
                fakeTe.HasBeenRun.Should().BeFalse();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DiscoverTests_NoFiles_NothingIsReported()
        {
            var reporter = new FakeFrameworkReporter();

            new TaefDiscoverer(MockLogger.Object, MockOptions.Object).DiscoverTests(new string[0], reporter);

            reporter.ReportedTestCasesFound.Should().BeEmpty();
        }

        #endregion

        #region Discovery of the sample DLLs with TE.exe

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTestsDebugX86_FindsTestsWithLocation()
        {
            FindTests(TestResources.Tests_DebugX86);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTestsReleaseX86_FindsTestsWithLocation()
        {
            FindTests(TestResources.Tests_ReleaseX86);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTestsDebugX64_FindsTestsWithLocation()
        {
            FindTests(TestResources.Tests_DebugX64);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTestsReleaseX64_FindsTestsWithLocation()
        {
            FindTests(TestResources.Tests_ReleaseX64);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_ExternallyLinkedX86TestDll_FindsTestsWithLocation()
        {
            FindExternallyLinkedTests(TestResources.DllTests_ReleaseX86);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_ExternallyLinkedX64TestDll_FindsTestsWithLocation()
        {
            FindExternallyLinkedTests(TestResources.DllTests_ReleaseX64);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_WithPathExtension_FindsTests()
        {
            string baseDir = TestDataCreator.PreparePathExtensionTest();
            try
            {
                string testDll = TestDataCreator.GetPathExtensionTestDll(baseDir);
                MockOptions.Setup(o => o.PathExtension).Returns(PlaceholderReplacer.TestDllDirPlaceholder + @"\..\dll");

                var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
                IList<TestCase> testCases = discoverer.GetTestsFromTestDll(testDll);

                testCases.Should().HaveCount(TestResources.NrOfDllTests);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                Utils.DeleteDirectory(baseDir).Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_WithoutPathExtension_FindsTestsSinceTeListsStatically()
        {
            // unlike running the tests, listing them does not load the test DLL (and its dependencies)
            string baseDir = TestDataCreator.PreparePathExtensionTest();
            try
            {
                string testDll = TestDataCreator.GetPathExtensionTestDll(baseDir);

                var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
                IList<TestCase> testCases = discoverer.GetTestsFromTestDll(testDll);

                testCases.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(TestResources.TestNames.DllTestsPassing, TestResources.TestNames.DllTestsFailing);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                Utils.DeleteDirectory(baseDir).Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_FindsDataDrivenTests()
        {
            AssertFindsTest(TestResources.TestNames.SimpleDataRow0, @"Tests\DataDrivenTests.cpp", 57);
            AssertFindsTest(TestResources.TestNames.LightweightDataRow0, @"Tests\DataDrivenTests.cpp", 93);
            AssertFindsTest("TaefSamples::LightweightDataTests::TwoValues#metadataSet3", @"Tests\DataDrivenTests.cpp", 100);
            AssertFindsTest(TestResources.TestNames.RowWithQuote, @"Tests\DataDrivenTests.cpp", 152);
            AssertFindsTest(TestResources.TestNames.RowWithColons, @"Tests\DataDrivenTests.cpp", 152);
            AssertFindsTest("TaefSamples::NamedRows::SpecialCharacters#with#hash", @"Tests\DataDrivenTests.cpp", 152);
            AssertFindsTest(TestResources.TestNames.MissingDataSource, @"Tests\DataDrivenTests.cpp", 171);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_FindsTemplateTests()
        {
            AssertFindsTest(TestResources.TestNames.TemplateTest, @"Tests\ClassTemplateTests.cpp", 57);
            AssertFindsTest("TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>::CanDefeatMath", @"Tests\ClassTemplateTests.cpp", 39);
            AssertFindsTest("TaefSamples::ClassTemplates::NumberTemplateTests<signed char>::CanHoldLargeSum", @"Tests\ClassTemplateTests.cpp", 93);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_FindsTestsInNamespaces()
        {
            AssertFindsTest(TestResources.TestNames.AnonymousNamespaceTest, @"Tests\NamespaceTests.cpp", 70);
            AssertFindsTest("TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test", @"Tests\NamespaceTests.cpp", 82);
            AssertFindsTest("TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep::Fails", @"Tests\NamespaceTests.cpp", 42);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_FindsTestsWithUmlauts()
        {
            AssertFindsTest(TestResources.TestNames.UmlautTest, @"Tests\UmlautTests.cpp", 15);
            AssertFindsTest("TaefSamples::Nämespace::KlässWithSetüp::Täst", @"Tests\UmlautTests.cpp", 47);
            AssertFindsTest("TaefSamples::DataDrivenTästs::Täst#metadataSet0", @"Tests\UmlautTests.cpp", 82);
            AssertFindsTest("TaefSamples::ÜmlautTemplateTests<class TaefSamples::ImplementationA>::Täst", @"Tests\UmlautTests.cpp", 121);
            AssertFindsTest("TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前", @"Tests\DataDrivenTests.cpp", 152);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_TestsOfClassesWhichAreSuffixesOfEachOtherHaveCorrectLocations()
        {
            AssertFindsTest("TaefSamples::abcd::t", @"Tests\BasicTests.cpp", 121);
            AssertFindsTest("TaefSamples::bbcd::t", @"Tests\BasicTests.cpp", 131);
            AssertFindsTest("TaefSamples::bcd::t", @"Tests\BasicTests.cpp", 141);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_IgnoredTestsAreFoundWithIgnoreTrait()
        {
            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(TestResources.Tests_DebugX86);

            testCases.Where(TaefConstants.IsIgnored).Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(
                TestResources.TestNames.IgnoredTest, TestResources.TestNames.IgnoredPassing, TestResources.TestNames.IgnoredFailing);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_SampleTests_MetaDataPropertiesAreConsistent()
        {
            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(TestResources.Tests_DebugX64);

            foreach (var testCasesOfClass in testCases.GroupBy(tc => TaefNames.GetClassName(tc.FullyQualifiedName)))
            {
                foreach (TestCase testCase in testCasesOfClass)
                {
                    TestCaseMetaDataProperty metaData = testCase.Properties.OfType<TestCaseMetaDataProperty>().Single();
                    metaData.NrOfTestCasesInClass.Should().Be(testCasesOfClass.Count(), testCase.FullyQualifiedName);
                    metaData.NrOfTestCasesInTestDll.Should().Be(TestResources.NrOfTests);
                }
            }
            testCases.Where(tc => tc.FullyQualifiedName.StartsWith("TaefSamples::TableDataTests::"))
                .Select(tc => tc.Properties.OfType<TestCaseMetaDataProperty>().Single().NrOfTestCasesInClass)
                .Should().OnlyContain(n => n == 16);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_AdditionalTeArgumentsWithSelect_RestrictDiscovery()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/select:\"@Name='TaefSamples::TestMath::*'\"");

            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(TestResources.Tests_DebugX86);

            // TE.exe lists data source errors regardless of the selection
            testCases.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(
                TestResources.TestNames.TestMathAddFails, TestResources.TestNames.TestMathAddPasses, TestResources.TestNames.TestMathAddPassesWithTraits,
                TestResources.TestNames.MissingDataSource);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_AdditionalTeArgumentsWithInvalidSelect_NoTestsAndErrorIsLogged()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/select:\"@Name='TaefSamples::TestMath::*\"");

            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDll(TestResources.Tests_DebugX86);

            testCases.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("0x06000000") && s.Contains(TestResources.Tests_DebugX86))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_ParseSymbolInformation_DiaResolverIsCreated()
        {
            var mockFactory = new Mock<IDiaResolverFactory>();
            var mockResolver = new Mock<IDiaResolver>();
            mockFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>())).Returns(mockResolver.Object);
            mockResolver.Setup(r => r.GetFunctions(It.IsAny<string>())).Returns(new List<SourceFileLocation>());
            mockResolver.Setup(r => r.FindFunctions(It.IsAny<string>())).Returns(new List<SourceFileLocation>());
            mockResolver.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>())).Returns(new Dictionary<string, SourceFileLocation>());

            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options, new ProcessExecutorFactory(), mockFactory.Object)
                .GetTestsFromTestDll(TestResources.Tests_DebugX86);

            testCases.Should().HaveCount(TestResources.NrOfTests);
            mockFactory.Verify(f => f.Create(TestResources.Tests_DebugX86, It.IsAny<string>(), It.IsAny<ILogger>()), Times.AtLeastOnce);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_DoNotParseSymbolInformation_DiaIsNotInvoked()
        {
            var mockFactory = new Mock<IDiaResolverFactory>();
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);

            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options, new ProcessExecutorFactory(), mockFactory.Object)
                .GetTestsFromTestDll(TestResources.Tests_DebugX86);

            mockFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
            testCases.Should().HaveCount(TestResources.NrOfTests);
            foreach (TestCase testCase in testCases)
            {
                testCase.CodeFilePath.Should().Be("");
                testCase.LineNumber.Should().Be(0);
                testCase.Source.Should().Be(TestResources.Tests_DebugX86);
                testCase.DisplayName.Should().NotBeNullOrEmpty();
                testCase.FullyQualifiedName.Should().Be(testCase.DisplayName);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_LoadTests_AllTestsAreFoundWithLocation()
        {
            var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            IList<TestCase> testCases = discoverer.GetTestsFromTestDll(TestResources.LoadTests_ReleaseX86);

            testCases.Should().HaveCount(TestResources.NrOfLoadTests);
            var names = new HashSet<string>(testCases.Select(tc => tc.FullyQualifiedName));
            for (int i = 0; i < TestResources.NrOfLoadTests; i++)
            {
                string name = TestResources.TestNames.LoadTest(i);
                names.Contains(name).Should().BeTrue($"Test not found: {name}");
            }
            testCases.Should().OnlyContain(tc => tc.CodeFilePath.EndsWith(@"LoadTests\LoadTests.cpp", StringComparison.OrdinalIgnoreCase) && tc.LineNumber > 0);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetTestsFromTestDll_NonTaefSampleDlls_NoTestsAndNoErrors()
        {
            var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);

            foreach (string dll in TestResources.NonTaefSampleDlls)
            {
                discoverer.GetTestsFromTestDll(TestResources.GetSampleDll(dll, SampleConfiguration.DebugX86)).Should().BeEmpty();
            }
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_AllFilesOfSampleFolder_AllSampleTestsAreFound()
        {
            string[] files = Directory.GetFiles(TestResources.GetSampleDirectory(SampleConfiguration.ReleaseX64));
            var reporter = new FakeFrameworkReporter();

            new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).DiscoverTests(files, reporter);

            reporter.ReportedTestCasesFound.Should().HaveCount(TestResources.NrOfAllSampleTests);
            reporter.ReportedTestCasesFound.Select(tc => Path.GetFileName(tc.Source)).Distinct().Should().BeEquivalentTo(TestResources.AllSampleTestDlls);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Load)]
        public void GetTestsFromTestDll_LoadTests_AreFoundInReasonableTime()
        {
            if (CiSupport.IsRunningOnBuildServer)
            {
                Assert.Inconclusive("Skipping test since it is unstable on the build server");
            }

            var stopwatch = Stopwatch.StartNew();
            var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            IList<TestCase> testCases = discoverer.GetTestsFromTestDll(TestResources.LoadTests_ReleaseX86);
            stopwatch.Stop();
            var actualDuration = stopwatch.Elapsed;

            testCases.Should().HaveCount(TestResources.NrOfLoadTests);
            int testParsingDurationInMs = CiSupport.GetWeightedDuration(testCases.Count); // 1 ms per test (listing, symbol lookup and processing)
            int overheadInMs = CiSupport.GetWeightedDuration(5000); // TE.exe start-up, loading the PDB
            var maxDuration = TimeSpan.FromMilliseconds(testParsingDurationInMs + overheadInMs);

            actualDuration.Should().BeLessThan(maxDuration);
        }

        [TestMethod]
        [TestCategory(Load)]
        public void GetTestsFromTestDlls_AllSampleDllsOfAllConfigurations_AreFoundInReasonableTime()
        {
            if (CiSupport.IsRunningOnBuildServer)
            {
                Assert.Inconclusive("Skipping test since it is unstable on the build server");
            }

            var testDlls = TestResources.AllSampleConfigurations
                .SelectMany(c => TestResources.AllSampleTestDlls.Select(dll => TestResources.GetSampleDll(dll, c)))
                .ToList();

            var stopwatch = Stopwatch.StartNew();
            IList<TestCase> testCases = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options).GetTestsFromTestDlls(testDlls);
            stopwatch.Stop();

            testCases.Should().HaveCount(4 * TestResources.NrOfAllSampleTests);
            // Discovery with one thread pool task per test DLL took about 20 s for these DLLs (thread pool starvation)
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(CiSupport.GetWeightedDuration(15000)));
        }

        #endregion

        #region Helpers

        private void AssertIsTaefTestDll(string dll, bool isTaefTestDll, string regex = "")
        {
            TaefDiscoverer.IsTaefTestDll(dll, regex, TestEnvironment.Logger)
                .Should()
                .Be(isTaefTestDll);
        }

        private IList<TestCase> FindTests(string testDll)
        {
            var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            IList<TestCase> testCases = discoverer.GetTestsFromTestDll(testDll);

            testCases.Should().HaveCount(TestResources.NrOfTests);
            testCases.Should().OnlyContain(tc => tc.Source == testDll);
            testCases.Should().OnlyContain(tc => tc.DisplayName == tc.FullyQualifiedName);
            testCases.Should().OnlyContain(tc => !string.IsNullOrEmpty(tc.CodeFilePath) && tc.LineNumber > 0);

            TestCase testCase = testCases.Single(tc => tc.FullyQualifiedName == "TaefSamples::ClassWithFixtures::AddFails");
            testCase.DisplayName.Should().Be("TaefSamples::ClassWithFixtures::AddFails");
            testCase.CodeFilePath.Should().EndWithEquivalent(@"SampleTests\Tests\FixtureTests.cpp");
            testCase.LineNumber.Should().Be(51);

            testCase = testCases.Single(tc => tc.FullyQualifiedName == "TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>::CanDefeatMath");
            testCase.CodeFilePath.Should().EndWithEquivalent(@"SampleTests\Tests\ClassTemplateTests.cpp");
            testCase.LineNumber.Should().Be(39);

            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            return testCases;
        }

        private void FindExternallyLinkedTests(string testDll)
        {
            var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            IList<TestCase> testCases = discoverer.GetTestsFromTestDll(testDll);

            testCases.Should().HaveCount(TestResources.NrOfDllTests);

            string expectedCodeFilePath = Path.GetFullPath($@"{TestResources.SampleTestsSolutionDir}DllDependentProject\DllTests.cpp");
            TestCase passing = testCases.Single(tc => tc.FullyQualifiedName == TestResources.TestNames.DllTestsPassing);
            passing.DisplayName.Should().Be(TestResources.TestNames.DllTestsPassing);
            passing.CodeFilePath.Should().BeEquivalentTo(expectedCodeFilePath);
            passing.LineNumber.Should().Be(13);

            TestCase failing = testCases.Single(tc => tc.FullyQualifiedName == TestResources.TestNames.DllTestsFailing);
            failing.DisplayName.Should().Be(TestResources.TestNames.DllTestsFailing);
            failing.CodeFilePath.Should().BeEquivalentTo(expectedCodeFilePath);
            failing.LineNumber.Should().Be(23);
        }

        private readonly Dictionary<string, IList<TestCase>> _testCasesOfSampleTests = new Dictionary<string, IList<TestCase>>();

        private void AssertFindsTest(string name, string file, int line)
        {
            if (!_testCasesOfSampleTests.TryGetValue(TestResources.Tests_DebugX86, out IList<TestCase> tests))
            {
                TestResources.Tests_DebugX86.AsFileInfo()
                    .Should().Exist("building the SampleTests solution produces that test DLL");

                var discoverer = new TaefDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
                tests = discoverer.GetTestsFromTestDll(TestResources.Tests_DebugX86);
                _testCasesOfSampleTests.Add(TestResources.Tests_DebugX86, tests);

                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.DebugError(It.IsAny<string>()), Times.Never);
                tests.Should().NotBeEmpty();
            }

            TestCase testCase = tests.Single(t => t.FullyQualifiedName == name);
            testCase.DisplayName.Should().Be(name);
            testCase.CodeFilePath.Should().EndWithEquivalent(@"SampleTests\" + file, name);
            testCase.LineNumber.Should().Be(line, name);
        }

        /// <summary>Marks a file as downloaded from the internet (Zone.Identifier stream with ZoneId=3, "Mark of the Web").</summary>
        private static void MarkAsDownloaded(string file)
        {
            using (SafeFileHandle handle = NativeMethods.CreateFile(file + ":Zone.Identifier", NativeMethods.GenericWrite, 0, IntPtr.Zero, NativeMethods.CreateAlways, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                    throw new IOException($"Could not create Zone.Identifier stream of '{file}'", Marshal.GetHRForLastWin32Error());

                using (var stream = new FileStream(handle, FileAccess.Write))
                {
                    byte[] content = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3\r\n");
                    stream.Write(content, 0, content.Length);
                }
            }
        }

        private static class NativeMethods
        {
            public const uint GenericWrite = 0x40000000;
            public const uint CreateAlways = 2;

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
                uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
        }

        #endregion

    }

}
