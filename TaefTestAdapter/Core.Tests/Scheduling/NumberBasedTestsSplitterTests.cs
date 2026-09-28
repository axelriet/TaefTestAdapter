// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Scheduling
{
    [TestClass]
    public class NumberBasedTestsSplitterTests : TestsBase
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_SameNumberOfTestsAsThreads_TestsAreDistributedEvenly()
        {
            IEnumerable<Model.TestCase> testCasesWithCommonClass = TestDataCreator.CreateDummyTestCases("FooClass::BarTest", "FooClass::BazTest");
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);

            ITestsSplitter splitter = new NumberBasedTestsSplitter(testCasesWithCommonClass, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().ContainSingle();
            result[1].Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_MoreTestsThanThreads_TestsAreDistributedEvenly()
        {
            IEnumerable<Model.TestCase> testCasesWithCommonClass = TestDataCreator.CreateDummyTestCases("FooClass::BarTest", "FooClass::BazTest", "FooClass::FooTest");
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);

            ITestsSplitter splitter = new NumberBasedTestsSplitter(testCasesWithCommonClass, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().HaveCount(2);
            result[1].Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_ALotMoreTestsThanThreads_TestsAreDistributedEvenly()
        {
            List<Model.TestCase> testcases = new List<Model.TestCase>();
            for (int i = 0; i < 5002; i++)
            {
                testcases.Add(TestDataCreator.ToTestCase("TestClass::Test" + i));
            }
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(8);

            ITestsSplitter splitter = new NumberBasedTestsSplitter(testcases, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(8);
            result[0].Should().HaveCount(626);
            result[1].Should().HaveCount(626);
            result[2].Should().HaveCount(625);
            result[3].Should().HaveCount(625);
            result[4].Should().HaveCount(625);
            result[5].Should().HaveCount(625);
            result[6].Should().HaveCount(625);
            result[7].Should().HaveCount(625);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_OrderOfTests_TestsAreDistributedRoundRobin()
        {
            IEnumerable<Model.TestCase> testCases = TestDataCreator.CreateDummyTestCases("A::1", "A::2", "B::1", "B::2", "C::1");
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);

            List<List<Model.TestCase>> result = new NumberBasedTestsSplitter(testCases, TestEnvironment.Options).SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Select(tc => tc.FullyQualifiedName).Should().Equal("A::1", "B::1", "C::1");
            result[1].Select(tc => tc.FullyQualifiedName).Should().Equal("A::2", "B::2");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitTestcases_MoreThreadsThanTests_TestsAreDistributedEvenly()
        {
            IEnumerable<Model.TestCase> testCasesWithCommonClass = TestDataCreator.CreateDummyTestCases("FooClass::BarTest", "FooClass::BazTest");
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(8);

            ITestsSplitter splitter = new NumberBasedTestsSplitter(testCasesWithCommonClass, TestEnvironment.Options);
            List<List<Model.TestCase>> result = splitter.SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().ContainSingle();
            result[1].Should().ContainSingle();
        }

    }

}