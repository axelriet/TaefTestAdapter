// This file has been added for TAEF support.

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;

namespace TaefTestAdapter.Scheduling
{
    /// <summary>
    /// Tests of <see cref="SchedulingAnalyzer"/>, which compares the durations the tests were expected to run (from the
    /// <c>*.taef.testdurations</c> files) with their actual durations.
    /// </summary>
    [TestClass]
    public class SchedulingAnalyzerTests : TestsBase
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void AddDurations_SameTestTwice_SecondDurationIsRejected()
        {
            var analyzer = new SchedulingAnalyzer(MockLogger.Object);
            TestCase testCase = TestDataCreator.ToTestCase("A::B");

            analyzer.AddExpectedDuration(testCase, 3).Should().BeTrue();
            analyzer.AddExpectedDuration(testCase, 4).Should().BeFalse();
            analyzer.AddActualDuration(testCase, 5).Should().BeTrue();
            analyzer.AddActualDuration(testCase, 6).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void PrintStatisticsToDebugOutput_NoDurations_NothingToReport()
        {
            var analyzer = new SchedulingAnalyzer(MockLogger.Object);
            analyzer.AddActualDuration(TestDataCreator.ToTestCase("A::B"), 5);

            analyzer.PrintStatisticsToDebugOutput();

            MockLogger.Verify(l => l.DebugInfo("# of expected test case durations: 0"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("# of actual test case durations: 1"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Nothing to report."), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void PrintStatisticsToDebugOutput_Durations_DifferencesAreReported()
        {
            var analyzer = new SchedulingAnalyzer(MockLogger.Object);
            TestCase test1 = TestDataCreator.ToTestCase("A::One");
            TestCase test2 = TestDataCreator.ToTestCase("A::Two");
            analyzer.AddExpectedDuration(test1, 10);
            analyzer.AddExpectedDuration(test2, 20);
            analyzer.AddActualDuration(test1, 14);
            analyzer.AddActualDuration(test2, 20);

            analyzer.PrintStatisticsToDebugOutput();

            MockLogger.Verify(l => l.DebugInfo("2 expected durations have been found in actual durations"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Avg difference between expected and actual duration: -2.0ms"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Standard deviation: 2.0ms"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Test A::One: Expected 10ms, actual 14ms"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Test A::Two: Expected 20ms, actual 20ms"), Times.Once);
        }
    }
}
