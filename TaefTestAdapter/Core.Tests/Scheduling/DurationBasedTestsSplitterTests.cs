// This file has been modified for TAEF support.

using System.Collections.Generic;
using System;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Scheduling
{
    [TestClass]
    public class DurationBasedTestsSplitterTests : TestsBase
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_SimpleCase_TestsAreDistributedCorrectly()
        {
            IDictionary<Model.TestCase, int> durations = new Dictionary<Model.TestCase, int>
            {
                { TestDataCreator.ToTestCase("TestClass::ShortTest1"), 1 },
                { TestDataCreator.ToTestCase("TestClass::ShortTest2"), 1 },
                { TestDataCreator.ToTestCase("TestClass::LongTest"), 3 },
                { TestDataCreator.ToTestCase("TestClass::ShortTest3"), 1 }
            };

            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);

            ITestsSplitter splitter = new DurationBasedTestsSplitter(durations, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().ContainSingle();
            result[0][0].FullyQualifiedName.Should().Be("TestClass::LongTest");
            result[1].Should().HaveCount(3);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_SimpleCaseWithThreeThreads_TestsAreDistributedCorrectly()
        {
            IDictionary<Model.TestCase, int> durations = new Dictionary<Model.TestCase, int>
            {
                { TestDataCreator.ToTestCase("TestClass::ShortTest1"), 1 },
                { TestDataCreator.ToTestCase("TestClass::ShortTest2"), 1 },
                { TestDataCreator.ToTestCase("TestClass::LongTest"), 3 },
                { TestDataCreator.ToTestCase("TestClass::ShortTest3"), 1 }
            };

            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(3);

            ITestsSplitter splitter = new DurationBasedTestsSplitter(durations, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(3);
            result[0].Should().ContainSingle();
            result[0][0].FullyQualifiedName.Should().Be("TestClass::LongTest");
            result[1].Should().HaveCount(2);
            result[2].Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_AsymmetricCase_TestsAreDistributedCorrectly()
        {
            IDictionary<Model.TestCase, int> durations = new Dictionary<Model.TestCase, int>
            {
                { TestDataCreator.ToTestCase("TestClass::ShortTest1"), 1 },
                { TestDataCreator.ToTestCase("TestClass::LongTest"), 5 }
            };

            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(3);

            ITestsSplitter splitter = new DurationBasedTestsSplitter(durations, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().ContainSingle();
            result[0][0].FullyQualifiedName.Should().Be("TestClass::LongTest");
            result[1].Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_RandomTestDurations_TestsAreDistributedCorrectly()
        {
            // fixed seeds: the test must be deterministic
            ExecuteRandomDurationsTest(5000, 1000, 8, 4711);
            ExecuteRandomDurationsTest(5000, 500, 7, 42);
            ExecuteRandomDurationsTest(50, 100000, 8, 1234);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_SingleThread_AllTestsInOneList()
        {
            IDictionary<Model.TestCase, int> durations = new Dictionary<Model.TestCase, int>
            {
                { TestDataCreator.ToTestCase("TestClass::ShortTest"), 1 },
                { TestDataCreator.ToTestCase("TestClass::LongTest"), 3 }
            };

            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(1);

            List<List<Model.TestCase>> result = new DurationBasedTestsSplitter(durations, TestEnvironment.Options).SplitTestcases();

            result.Should().ContainSingle();
            result[0].Select(tc => tc.FullyQualifiedName).Should().Equal("TestClass::LongTest", "TestClass::ShortTest");
        }


        private void ExecuteRandomDurationsTest(int nrOfTests, int maxRandomDuration, int nrOfThreads, int seed)
        {
            IDictionary<Model.TestCase, int> durations = CreateRandomTestResults(nrOfTests, maxRandomDuration, seed);

            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(nrOfThreads);

            ITestsSplitter splitter = new DurationBasedTestsSplitter(durations, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(nrOfThreads);
            result.Select(l => l.Count).Sum().Should().Be(nrOfTests);

            int sumOfAllDurations = durations.Select(kvp => kvp.Value).Sum();
            int maxDuration = durations.Select(kvp => kvp.Value).Max();

            int targetDuration = sumOfAllDurations / nrOfThreads;

            HashSet<Model.TestCase> foundTestcases = new HashSet<Model.TestCase>();
            foreach (List<Model.TestCase> testcases in result)
            {
                int sum = testcases.Select(tc => durations[tc]).Sum();
                sum.Should().BeLessThan(targetDuration + maxDuration / 2);
                sum.Should().BeGreaterThan(targetDuration - maxDuration / 2);

                foundTestcases.UnionWith(testcases);
            }

            foundTestcases.Should().HaveCount(nrOfTests);
        }

        private IDictionary<Model.TestCase, int> CreateRandomTestResults(int nr, int maxDuration, int seed)
        {
            IDictionary<Model.TestCase, int> durations = new Dictionary<Model.TestCase, int>();
            Random random = new Random(seed);
            for (int i = 0; i < nr; i++)
            {
                durations.Add(TestDataCreator.ToTestCase("TestClass::Test" + i), random.Next(1, maxDuration));
            }
            return durations;
        }

    }

}