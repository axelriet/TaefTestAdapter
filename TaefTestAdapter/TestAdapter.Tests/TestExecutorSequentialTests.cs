// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FluentAssertions;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Runs the tests of <see cref="TestExecutorTestsBase"/> sequentially (one thread), plus cancellation tests.
    /// </summary>
    [TestClass]
    public class TestExecutorSequentialTests : TestExecutorTestsBase
    {
        private const int WaitBeforeCancelInMs = 1000;

        public TestExecutorSequentialTests() : base(false, 1) { }

        /// <summary>
        /// TaefSamples::Crashing::LongRunning (CrashingTests_taef.dll) and TaefSamples::LongRunningTests::Test2 (LongRunningTests_taef.dll) run for
        /// 2 s each (two TE.exe processes); execution is canceled after 1 s. The currently running TE.exe is not killed,
        /// so the first test completes, but the second one is not started anymore.
        /// </summary>
        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_CancelingExecutor_StopsTestExecution()
        {
            long elapsedMs = RunCancelingTests(false);

            // 1st test is completed, 2nd test is not started (which would take another 2 s plus the start of TE.exe)
            elapsedMs.Should().BeGreaterOrEqualTo(TestResources.LongRunningTestDurationInMs);
            elapsedMs.Should().BeLessThan(2 * TestResources.LongRunningTestDurationInMs);
            GetRecordedResults().Should().NotContain(r => r.TestCase.DisplayName == TestNames.LongRunningTest2 && r.Outcome == VsTestOutcome.Failed);
        }

        /// <summary>
        /// As <see cref="RunTests_CancelingExecutor_StopsTestExecution"/>, but the running TE.exe (and its test host)
        /// is killed on cancel, so the first test does not complete.
        /// </summary>
        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_CancelingExecutorAndKillProcesses_StopsTestExecutionFaster()
        {
            long elapsedMs = RunCancelingTests(true);

            elapsedMs.Should().BeGreaterOrEqualTo(WaitBeforeCancelInMs);
            elapsedMs.Should().BeLessThan(TestResources.LongRunningTestDurationInMs);
            GetRecordedResults().Should().NotContain(r => r.Outcome == VsTestOutcome.Passed || r.Outcome == VsTestOutcome.Failed);
        }

        private long RunCancelingTests(bool killProcesses)
        {
            MockOptions.Setup(o => o.KillProcessesOnCancel).Returns(killProcesses);
            var testCasesToRun = new List<VsTestCase>();
            testCasesToRun.AddRange(GetVsTestCases(CopySample(TestResources.CrashingTests_DebugX86).TestDll, TestNames.CrashingLongRunning));
            testCasesToRun.AddRange(GetVsTestCases(CopySample(TestResources.LongRunningTests_ReleaseX86).TestDll, TestNames.LongRunningTest2));

            var executor = CreateExecutor();
            var canceller = new Thread(() =>
            {
                Thread.Sleep(WaitBeforeCancelInMs);
                executor.Cancel();
            });

            var stopwatch = Stopwatch.StartNew();
            canceller.Start();
            executor.RunTests(testCasesToRun, MockRunContext.Object, MockFrameworkHandle.Object);
            stopwatch.Stop();

            canceller.Join();
            MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s == "Test execution canceled.")), Times.Once);
            return stopwatch.ElapsedMilliseconds;
        }

    }
}
