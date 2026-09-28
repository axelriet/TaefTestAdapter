// This file has been modified for TAEF support.

using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Runs the tests of <see cref="TestExecutorTestsBase"/> with parallel test execution: the tests are split among
    /// <see cref="NrOfThreads"/> threads (a fixed number to make the splitting of the tests deterministic), each of which
    /// runs its own TE.exe processes.
    /// </summary>
    [TestClass]
    public class TestExecutorParallelTests : TestExecutorTestsBase
    {
        private const int NrOfThreads = 4;

        public TestExecutorParallelTests() : base(true, NrOfThreads) { }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_ParallelTestExecution_SpeedsUpTestExecution()
        {
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(false);

            string testDll = CopySample(TestResources.LongRunningTests_ReleaseX86).TestDll;
            Stopwatch stopwatch = Stopwatch.StartNew();
            RunTests(testDll);
            stopwatch.Stop();
            long sequentialDuration = stopwatch.ElapsedMilliseconds;
            CheckMockInvocations(1, 1, 0, 0, 0);

            ClearRecordedResults();
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(true);

            testDll = CopySample(TestResources.LongRunningTests_ReleaseX86).TestDll;
            stopwatch.Restart();
            RunTests(testDll);
            stopwatch.Stop();
            long parallelDuration = stopwatch.ElapsedMilliseconds;
            CheckMockInvocations(1, 1, 0, 0, 0);

            // 2 tests of 2 s each: sequentially at least 4 s, in parallel (2 TE.exe processes) at least 2 s and about 2 s faster
            sequentialDuration.Should().BeGreaterOrEqualTo(2 * TestResources.LongRunningTestDurationInMs);
            parallelDuration.Should().BeGreaterOrEqualTo(TestResources.LongRunningTestDurationInMs);
            parallelDuration.Should().BeLessThan(sequentialDuration - TestResources.LongRunningTestDurationInMs / 2);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_ParallelTestExecution_TestsAreSplitAmongThreads()
        {
            SetupSampleTestsSettings();

            RunTests(CopySample(TestResources.Tests_DebugX86).TestDll);

            CheckMockInvocations(TestResources.NrOfTestsReportedAsPassed, TestResources.NrOfTestsReportedAsFailed,
                TestResources.NrOfTestsReportedAsNone, TestResources.NrOfTestsReportedAsSkipped, 0);
            MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s == $"Executing tests on {NrOfThreads} threads")), Times.Once);
        }

        /// <summary>
        /// Each thread runs its own TE.exe: a crash in process only affects the tests of the crashing test's thread which
        /// are run after the crash (which tests these are depends on how the tests are split among the threads).
        /// </summary>
        [TestMethod]
        [TestCategory(Integration)]
        public override void RunTests_CrashingTestsRunInProcess_TestsAfterCrashAreNotRun()
        {
            MockOptions.Setup(o => o.RunInProcess).Returns(true);

            RunTests(CopySample(TestResources.CrashingTests_DebugX64).TestDll);

            GetRecordedResults().Should().HaveCount(TestResources.NrOfCrashingTests);
            GetRecordedResult(TestNames.CrashingAddFailsBeforeCrash).Outcome.Should().Be(VsTestOutcome.Failed);
            GetRecordedResult(TestNames.CrashingAddPassesBeforeCrash).Outcome.Should().Be(VsTestOutcome.Passed);
            GetRecordedResult(TestNames.CrashingTheCrash).Outcome.Should().Be(VsTestOutcome.Failed);
            foreach (VsTestResult result in GetRecordedResults().Where(r => r.Outcome == VsTestOutcome.Skipped))
            {
                result.ErrorMessage.Should().Contain("reason is probably a crash of test TaefSamples::Crashing::");
            }
            GetRecordedResults().Count(r => r.Outcome == VsTestOutcome.Passed).Should().BeGreaterOrEqualTo(1);
            GetRecordedResults().Count(r => r.Outcome == VsTestOutcome.Failed).Should().BeGreaterOrEqualTo(2);
        }

    }

}
