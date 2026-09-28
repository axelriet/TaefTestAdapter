// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.Scheduling
{
    /// <summary>
    /// Tests of <see cref="TestDurationSerializer"/>: durations are stored in <c>&lt;testdll&gt;.taef.testdurations</c>
    /// (test DLLs are dummy files in a temporary directory).
    /// </summary>
    [TestClass]
    public class TestDurationSerializerTests : TestsBase
    {
        private TemporaryDirectory _directory;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        private string CreateTestDll(string name = "Tests_taef.dll") => _directory.CreateFile(name, "dummy");

        private static string GetDurationsFile(string testDll) => testDll + ".taef.testdurations";

        [TestMethod]
        [TestCategory(Unit)]
        public void DurationsExtension_IsTaefSpecific()
        {
            TaefConstants.DurationsExtension.Should().Be(".taef.testdurations");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_SimpleTests_DurationsAreWrittenAndReadCorrectly()
        {
            string testDll = CreateTestDll();
            var testResults = new List<TestResult>
            {
                TestDataCreator.ToTestResult("TestClass1::Test1", TestOutcome.Passed, 3, testDll),
                TestDataCreator.ToTestResult("TestClass1::SkippedTest", TestOutcome.Skipped, 1, testDll)
            };

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(testResults);

            GetDurationsFile(testDll).AsFileInfo().Should().Exist();

            IDictionary<TestCase, int> durations = serializer.ReadTestDurations(testResults.Select(tr => tr.TestCase));
            durations.Should().HaveCount(1);
            durations.Should().ContainKey(testResults[0].TestCase);
            durations[testResults[0].TestCase].Should().Be(3);
            durations.Should().NotContainKey(testResults[1].TestCase);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_OnlyPassedAndFailedTests_AreRecorded()
        {
            string testDll = CreateTestDll();
            var testResults = new List<TestResult>
            {
                TestDataCreator.ToTestResult("A::Passed", TestOutcome.Passed, 1, testDll),
                TestDataCreator.ToTestResult("A::Failed", TestOutcome.Failed, 2, testDll),
                TestDataCreator.ToTestResult("A::Skipped", TestOutcome.Skipped, 3, testDll),
                TestDataCreator.ToTestResult("A::None", TestOutcome.None, 4, testDll),
                TestDataCreator.ToTestResult("A::NotFound", TestOutcome.NotFound, 5, testDll)
            };

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(testResults);

            serializer.ReadTestDurations(testResults.Select(tr => tr.TestCase)).Keys.Select(tc => tc.FullyQualifiedName)
                .Should().BeEquivalentTo("A::Passed", "A::Failed");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_SameTestsInDifferentTestDlls_DurationsAreWrittenAndReadCorrectly()
        {
            string testDll = CreateTestDll();
            string testDll2 = CreateTestDll("Other_taef.dll");
            var testResults = new List<TestResult>
            {
                TestDataCreator.ToTestResult("TestClass1::Test1", TestOutcome.Passed, 3, testDll),
                TestDataCreator.ToTestResult("TestClass1::Test1", TestOutcome.Failed, 4, testDll2)
            };

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(testResults);

            GetDurationsFile(testDll).AsFileInfo().Should().Exist();
            GetDurationsFile(testDll2).AsFileInfo().Should().Exist();

            IDictionary<TestCase, int> durations = serializer.ReadTestDurations(testResults.Select(tr => tr.TestCase));
            durations.Should().HaveCount(2);
            durations[testResults[0].TestCase].Should().Be(3);
            durations[testResults[1].TestCase].Should().Be(4);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_SingleTest_DurationIsUpdatedCorrectly()
        {
            string testDll = CreateTestDll();
            var testResults = new List<TestResult>
            {
                TestDataCreator.ToTestResult("TestClass1::Test1", TestOutcome.Passed, 3, testDll)
            };

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(testResults);
            IDictionary<TestCase, int> durations = serializer.ReadTestDurations(testResults.Select(tr => tr.TestCase));
            durations[testResults[0].TestCase].Should().Be(3);

            testResults[0].Duration = TimeSpan.FromMilliseconds(4);
            serializer.UpdateTestDurations(testResults);
            durations = serializer.ReadTestDurations(testResults.Select(tr => tr.TestCase));
            durations.Should().HaveCount(1);
            durations[testResults[0].TestCase].Should().Be(4);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_OtherTestsOfTestDll_AreKept()
        {
            string testDll = CreateTestDll();
            var serializer = new TestDurationSerializer();
            TestResult first = TestDataCreator.ToTestResult("A::First", TestOutcome.Passed, 7, testDll);
            TestResult second = TestDataCreator.ToTestResult("A::Second", TestOutcome.Failed, 9, testDll);

            serializer.UpdateTestDurations(first.Yield());
            serializer.UpdateTestDurations(second.Yield());

            IDictionary<TestCase, int> durations = serializer.ReadTestDurations(new[] { first.TestCase, second.TestCase });
            durations[first.TestCase].Should().Be(7);
            durations[second.TestCase].Should().Be(9);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_RepeatedTest_LastDurationIsRecorded()
        {
            string testDll = CreateTestDll();
            var testResults = new List<TestResult>
            {
                TestDataCreator.ToTestResult("A::Repeated", TestOutcome.Passed, 3, testDll),
                TestDataCreator.ToTestResult("A::Repeated", TestOutcome.Failed, 5, testDll)
            };

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(testResults);

            serializer.ReadTestDurations(testResults[0].TestCase.Yield()).Single().Value.Should().Be(5);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_FractionsOfMilliseconds_AreRoundedUp()
        {
            string testDll = CreateTestDll();
            TestResult result = TestDataCreator.ToTestResult("A::Short", TestOutcome.Passed, 0, testDll);
            result.Duration = TimeSpan.FromTicks(2500);

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(result.Yield());

            serializer.ReadTestDurations(result.TestCase.Yield())[result.TestCase].Should().Be(1);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_TaefNamesWithSpecialCharacters_AreStoredAndReadCorrectly()
        {
            string testDll = CreateTestDll();
            string[] names =
            {
                "TaefSamples::NamedRows::SpecialCharacters#with'quote", "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]",
                "TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate", "TaefSamples::Ümlautß::Täst", "Q::T#say \"hi\" & <bye>",
                @"TaefSamples::NamedRows::SpecialCharacters#back\slash"
            };
            List<TestResult> testResults = names.Select((n, i) => TestDataCreator.ToTestResult(n, TestOutcome.Passed, i + 1, testDll)).ToList();

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(testResults);

            IDictionary<TestCase, int> durations = serializer.ReadTestDurations(testResults.Select(tr => tr.TestCase));
            durations.Should().HaveCount(names.Length);
            for (int i = 0; i < names.Length; i++)
                durations[testResults[i].TestCase].Should().Be(i + 1, names[i]);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_FileFormat_ContainsFullPathOfTestDllAndDurations()
        {
            string testDll = CreateTestDll();
            var serializer = new TestDurationSerializer();

            serializer.UpdateTestDurations(TestDataCreator.ToTestResult("A::B", TestOutcome.Passed, 42, testDll).Yield());

            XDocument document = XDocument.Load(GetDurationsFile(testDll));
            document.Root.Name.LocalName.Should().Be("TaefTestDurations");
            document.Root.Element("TestDll").Value.Should().Be(Path.GetFullPath(testDll));
            XElement duration = document.Root.Element("TestDurations").Elements().Single();
            duration.Attribute("Test").Value.Should().Be("A::B");
            duration.Attribute("Duration").Value.Should().Be("42");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReadTestDurations_NoDurationFile_EmptyDictionary()
        {
            string testDll = CreateTestDll();

            IDictionary<TestCase, int> durations = new TestDurationSerializer().ReadTestDurations(TestDataCreator.ToTestCase("TestClass1::Test1", testDll).Yield());

            durations.Should().NotBeNull();
            durations.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReadTestDurations_DurationFileWithoutCurrentTest_EmptyDictionary()
        {
            string testDll = CreateTestDll();
            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(TestDataCreator.ToTestResult("TestClass1::Test1", TestOutcome.Passed, 3, testDll).Yield());

            IDictionary<TestCase, int> durations = serializer.ReadTestDurations(TestDataCreator.ToTestCase("TestClass1::Test2", testDll).Yield());

            durations.Should().NotBeNull();
            durations.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReadTestDurations_BrokenDurationFile_ThrowsInvalidTestDurationsException()
        {
            string testDll = CreateTestDll();
            File.WriteAllText(GetDurationsFile(testDll), "<TaefTestDurations><TestDll>broken");

            new Action(() => new TestDurationSerializer().ReadTestDurations(TestDataCreator.ToTestCase("A::B", testDll).Yield()))
                .Should().Throw<InvalidTestDurationsException>().Which.Message.Should().Contain(GetDurationsFile(testDll));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReadTestDurations_DurationFileNotMatchingSchema_ThrowsInvalidTestDurationsException()
        {
            string testDll = CreateTestDll();
            File.WriteAllText(GetDurationsFile(testDll), "<?xml version=\"1.0\"?><TaefTestDurations><Unexpected /></TaefTestDurations>");

            new Action(() => new TestDurationSerializer().ReadTestDurations(TestDataCreator.ToTestCase("A::B", testDll).Yield()))
                .Should().Throw<InvalidTestDurationsException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void UpdateTestDurations_BrokenDurationFile_IsReplaced()
        {
            string testDll = CreateTestDll();
            File.WriteAllText(GetDurationsFile(testDll), "broken");
            TestResult result = TestDataCreator.ToTestResult("A::B", TestOutcome.Passed, 11, testDll);

            var serializer = new TestDurationSerializer();
            serializer.UpdateTestDurations(result.Yield());

            serializer.ReadTestDurations(result.TestCase.Yield())[result.TestCase].Should().Be(11);
        }

    }

}
