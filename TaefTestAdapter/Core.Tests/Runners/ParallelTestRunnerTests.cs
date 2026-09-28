// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Tests of <see cref="ParallelTestRunner"/> (TE.exe is faked): splitting of the tests among threads (by number or by
    /// recorded durations), one TE.exe invocation per thread, and cancellation of all threads.
    /// </summary>
    [TestClass]
    public class ParallelTestRunnerTests : TestsBase
    {
        private static readonly string[] Names = { "A::1", "A::2", "B::1", "B::2" };

        private TemporaryDirectory _directory;
        private FakeFrameworkReporter _reporter;
        private FakeProcessExecutorFactory _factory;

        private string TestDll => _directory.GetPath("Parallel_taef.dll");

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
            _reporter = new FakeFrameworkReporter();
            _factory = new FakeProcessExecutorFactory
            {
                // every TE.exe invocation "runs" all tests, each parser picks the tests of its command line
                Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::1", "Passed"), ("A::2", "Failed"), ("B::1", "Passed"), ("B::2", "Skipped")))
            };
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.FakeTeExecutable);
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(true);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        private ParallelTestRunner CreateRunner()
            => new ParallelTestRunner(_reporter, MockLogger.Object, MockOptions.Object, new SchedulingAnalyzer(MockLogger.Object));

        /// <returns>The tests selected by name or by class term.</returns>
        private static IEnumerable<string> GetSelectedTests(RecordedExecution execution)
            => Names.Where(n => execution.Parameters.Contains($"@Name='{n}'")
                                || execution.Parameters.Contains(TaefConstants.GetClassSelectionTerm(Helpers.TaefNames.GetClassName(n))));

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TwoThreads_EachThreadRunsItsShareOfTestsWithItsOwnTe()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, Names);

            CreateRunner().RunTests(testCases, false, _factory);

            IList<RecordedExecution> executions = _factory.Executions;
            executions.Should().HaveCount(2);
            executions.Select(e => string.Join(",", GetSelectedTests(e))).Should().BeEquivalentTo("A::1,B::1", "A::2,B::2");
            executions.Should().OnlyContain(e => !e.PrintTestOutput, "output of parallel runs is not printed");

            _reporter.ReportedTestResults.Select(tr => tr.TestCase.FullyQualifiedName).Should().BeEquivalentTo(Names);
            _reporter.ReportedTestResults.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}")
                .Should().BeEquivalentTo("A::1 Passed", "A::2 Failed", "B::1 Passed", "B::2 Skipped");

            MockLogger.Verify(l => l.LogInfo("Executing tests on 2 threads"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Using splitter based on number of tests"), Times.Once);
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.StartsWith("[T0] Execution took "))), Times.Once);
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.StartsWith("[T1] Execution took "))), Times.Once);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_DurationsOfAllTestsAvailable_TestsAreSplitByDuration()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, Names);
            var durations = new Dictionary<string, int> { { "A::1", 10 }, { "A::2", 1 }, { "B::1", 1 }, { "B::2", 8 } };
            new TestDurationSerializer().UpdateTestDurations(testCases.Select(tc => new TestResult(tc)
            {
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(durations[tc.FullyQualifiedName])
            }));

            CreateRunner().RunTests(testCases, false, _factory);

            MockLogger.Verify(l => l.DebugInfo("Using splitter based on test durations"), Times.Once);
            _factory.Executions.Select(e => string.Join(",", GetSelectedTests(e).OrderBy(n => n))).Should().BeEquivalentTo("A::1", "A::2,B::1,B::2");
            _reporter.ReportedTestResults.Should().HaveCount(4);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_DurationsOfSomeTestsMissing_TestsAreSplitByNumber()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, Names);
            new TestDurationSerializer().UpdateTestDurations(testCases.Take(3).Select(tc => new TestResult(tc)
            {
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(5)
            }));

            CreateRunner().RunTests(testCases, false, _factory);

            MockLogger.Verify(l => l.DebugInfo("Using splitter based on number of tests"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_InvalidDurationsFile_WarningAndTestsAreSplitByNumber()
        {
            File.WriteAllText(TestDll + TaefConstants.DurationsExtension, "<TaefTestDurations><broken");
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, Names);

            CreateRunner().RunTests(testCases, false, _factory);

            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.StartsWith("Could not read test durations: "))), Times.Once);
            MockLogger.Verify(l => l.DebugInfo("Using splitter based on number of tests"), Times.Once);
            _reporter.ReportedTestResults.Should().HaveCount(4);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_MoreThreadsThanTests_OneThreadPerTest()
        {
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(8);
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, new[] { "A::1", "B::2" }, Names);

            CreateRunner().RunTests(testCases, false, _factory);

            MockLogger.Verify(l => l.LogInfo("Executing tests on 2 threads"), Times.Once);
            _factory.Executions.Select(e => string.Join(",", GetSelectedTests(e))).Should().BeEquivalentTo("A::1", "B::2");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_WhileTestsAreRunning_AllThreadsAreCanceled()
        {
            MockOptions.Setup(o => o.KillProcessesOnCancel).Returns(true);
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, Names);
            ParallelTestRunner runner = CreateRunner();
            var allTesRunning = new CountdownEvent(2);
            var canceled = new ManualResetEventSlim();
            int cancelCalls = 0;
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::1", "Passed"), ("A::2", "Failed"), ("B::1", "Passed"), ("B::2", "Skipped")))
            {
                // both (fake) TE.exe processes are running when the run is canceled
                BeforeOutput = e2 =>
                {
                    allTesRunning.Signal();
                    allTesRunning.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
                    if (Interlocked.Exchange(ref cancelCalls, 1) == 0)
                    {
                        runner.Cancel();
                        canceled.Set();
                    }
                    canceled.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
                }
            };

            runner.RunTests(testCases, false, _factory);

            _factory.Executions.Should().HaveCount(2).And.OnlyContain(e => e.IsCanceled);
            _reporter.ReportedTestResults.Should().BeEmpty();
        }

    }

}
