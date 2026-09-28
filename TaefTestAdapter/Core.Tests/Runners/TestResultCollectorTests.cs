// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Tests of <see cref="TestResultCollector"/>: results of tests which have not been reported while TE.exe was
    /// running (from console output which has not been parsed yet, as crash suspects, or according to option
    /// <see cref="Settings.SettingsWrapper.MissingTestsReportMode"/>).
    /// </summary>
    [TestClass]
    public class TestResultCollectorTests : TestsBase
    {
        private const string TestDll = @"C:\tests\Collector_taef.dll";

        private static readonly List<TestCase> TestCases = new List<TestCase>
        {
            new TestCase("A::X", TestDll, "A::X", @"C:\src\a.cpp", 10),
            new TestCase("A::Y", TestDll, "A::Y", @"C:\src\a.cpp", 20),
            new TestCase("B::Z", TestDll, "B::Z", "", 0)
        };

        private List<TestResult> Collect(IList<string> consoleOutput = null, TestCase crashedTestCase = null)
        {
            return new TestResultCollector(MockLogger.Object, "[T0] ", MockOptions.Object)
                .CollectTestResults(TestCases, consoleOutput ?? new List<string>(), crashedTestCase);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_DefaultMode_MissingTestsAreReportedAsNotFound()
        {
            MockOptions.Object.MissingTestsReportMode.Should().Be(MissingTestsReportMode.ReportAsNotFound);

            List<TestResult> results = Collect();

            results.Should().HaveCount(3);
            results.Should().OnlyContain(tr => tr.Outcome == TestOutcome.NotFound);
            results.Select(tr => tr.TestCase).Should().Equal(TestCases);
            foreach (TestResult result in results)
            {
                result.ErrorMessage.Should().StartWith("Test case has not been run - possible causes:");
                result.ErrorMessage.Should().Contain("- TE.exe could not be started or failed to run the test DLL (see the Tests output window)");
                result.ErrorStackTrace.Should().BeNull();
                result.DisplayName.Should().Be(result.TestCase.DisplayName);
            }
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.StartsWith("[T0] 3 test cases seem to not have been run"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_ReportModes_MissingTestsAreReportedAccordingly()
        {
            var expectations = new Dictionary<MissingTestsReportMode, TestOutcome>
            {
                { MissingTestsReportMode.ReportAsFailed, TestOutcome.Failed },
                { MissingTestsReportMode.ReportAsSkipped, TestOutcome.Skipped },
                { MissingTestsReportMode.ReportAsNotFound, TestOutcome.NotFound }
            };
            foreach (KeyValuePair<MissingTestsReportMode, TestOutcome> expectation in expectations)
            {
                MockOptions.Setup(o => o.MissingTestsReportMode).Returns(expectation.Key);

                List<TestResult> results = Collect();

                results.Should().HaveCount(3, expectation.Key.ToString());
                results.Should().OnlyContain(tr => tr.Outcome == expectation.Value, expectation.Key.ToString());
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_DoNotReport_NoResults()
        {
            MockOptions.Setup(o => o.MissingTestsReportMode).Returns(MissingTestsReportMode.DoNotReport);

            Collect().Should().BeEmpty();
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("3 test cases seem to not have been run"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_CrashedTest_RemainingTestsAreSkippedAsCrashSuspects()
        {
            // the crashed test itself has already been reported by the parser
            List<TestResult> results = new TestResultCollector(MockLogger.Object, "", MockOptions.Object)
                .CollectTestResults(TestCases.Skip(1), new List<string>(), TestCases[1]);

            results.Should().HaveCount(2);
            results.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Skipped);
            results.Should().OnlyContain(tr => tr.ErrorMessage == "reason is probably a crash of test A::Y");
            results.Should().OnlyContain(tr => tr.ErrorStackTrace == ErrorMessageParser.CreateStackTraceEntry("crash suspect", @"C:\src\a.cpp", "20"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_CrashedTestWithoutSourceLocation_NoStackTrace()
        {
            List<TestResult> results = Collect(crashedTestCase: TestCases[2]);

            results.Should().HaveCount(3);
            results.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Skipped && tr.ErrorStackTrace == null);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_ConsoleOutput_ResultsAreParsedFromOutput()
        {
            List<string> consoleOutput = RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("A::Y", "Failed"), ("B::Z", "NotRun"));

            List<TestResult> results = Collect(consoleOutput);

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal("A::X Passed", "A::Y Failed", "B::Z None");
            TestResultAssertions.AssertTestResultIsFailure(results[1], "Verify: AreEqual(1, 2) - Values (1, 2)");
            MockLogger.Verify(l => l.DebugInfo("[T0] Collected 3 test results from console output"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_ConsoleOutputWithSomeResults_MissingTestsAreReported()
        {
            List<string> consoleOutput = RunnerTestData.CreateTeOutput(("A::X", "Passed"));

            List<TestResult> results = Collect(consoleOutput);

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal("A::X Passed", "A::Y NotFound", "B::Z NotFound");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_ConsoleOutputEndingWithinGroup_CrashIsDetectedFromOutput()
        {
            var consoleOutput = new List<string>
            {
                "Test Authoring and Execution Framework v10.104k for x64",
                "",
                "StartGroup: A::X",
                "EndGroup: A::X [Passed]",
                "",
                "StartGroup: A::Y",
                "About to crash"
            };

            List<TestResult> results = Collect(consoleOutput);

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal("A::X Passed", "A::Y Failed", "B::Z Skipped");
            results[1].ErrorMessage.Should().StartWith(StreamingTaefOutputParser.CrashText);
            results[2].ErrorMessage.Should().Be("reason is probably a crash of test A::Y");
            results[2].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("crash suspect", @"C:\src\a.cpp", "20"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CollectTestResults_NoTests_NoResults()
        {
            new TestResultCollector(MockLogger.Object, "", MockOptions.Object)
                .CollectTestResults(new TestCase[0], new List<string>(), null)
                .Should().BeEmpty();
            MockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
        }

    }

}
