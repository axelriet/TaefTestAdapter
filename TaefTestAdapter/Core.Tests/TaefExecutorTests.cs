// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;

namespace TaefTestAdapter
{
    [TestClass]
    public class TaefExecutorTests : TestsBase
    {

        #region Integration tests with the sample test DLLs

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_SequentialExecution_ProducesTestDurationsFiles()
        {
            AssertDurationsFilesAreCreated(false);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_ParallelExecution_ProducesTestDurationsFiles()
        {
            AssertDurationsFilesAreCreated(true);
        }

        private void AssertDurationsFilesAreCreated(bool parallelExecution)
        {
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(parallelExecution);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);
            SetupSampleTestsSettings();

            using (SampleCopy sampleTests = SampleCopy.Create(TestResources.Tests_DebugX86))
            using (SampleCopy crashingTests = SampleCopy.Create(TestResources.CrashingTests_DebugX86))
            {
                List<TestCase> sampleTestCases = TestDataCreator.GetTestCasesOfTestDll(sampleTests.TestDll);
                List<TestCase> crashingTestCases = TestDataCreator.GetTestCasesOfTestDll(crashingTests.TestDll);
                var reporter = new FakeFrameworkReporter();

                new TaefExecutor(TestEnvironment.Logger, TestEnvironment.Options, ProcessExecutorFactory)
                    .RunTests(sampleTestCases.Concat(crashingTestCases), reporter, false);

                reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfTests + TestResources.NrOfCrashingTests);
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Passed)
                    .Should().Be(TestResources.NrOfTestsReportedAsPassed + TestResources.NrOfCrashingTestsPassing);
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Failed)
                    .Should().Be(TestResources.NrOfTestsReportedAsFailed + TestResources.NrOfCrashingTestsFailing + TestResources.NrOfCrashingTestsCrashing);

                (sampleTests.TestDll + TaefConstants.DurationsExtension).AsFileInfo().Should().Exist("test execution should result in test durations");
                AssertDurationsOfPassedAndFailedTestsAreRecorded(sampleTestCases, reporter);

                (crashingTests.TestDll + TaefConstants.DurationsExtension).AsFileInfo().Should().Exist("test execution should result in test durations");
                AssertDurationsOfPassedAndFailedTestsAreRecorded(crashingTestCases, reporter);

                MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.StartsWith("Executing tests on 2 threads"))), parallelExecution ? Times.Once() : Times.Never());
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        private static void AssertDurationsOfPassedAndFailedTestsAreRecorded(IList<TestCase> testCases, FakeFrameworkReporter reporter)
        {
            IDictionary<TestCase, int> durations = new TestDurationSerializer().ReadTestDurations(testCases);
            List<TestCase> passedOrFailed = testCases
                .Where(tc => reporter.ReportedTestResults.Any(tr => tc.Equals(tr.TestCase) && (tr.Outcome == TestOutcome.Passed || tr.Outcome == TestOutcome.Failed)))
                .ToList();
            passedOrFailed.Should().NotBeEmpty();
            durations.Keys.Should().BeEquivalentTo(passedOrFailed);
        }

        [TestMethod]
        [TestCategory(Load)]
        public void RunTests_ManyLoadTests_AreRunWithSeveralTeInvocations()
        {
            using (SampleCopy loadTests = SampleCopy.Create(TestResources.LoadTests_ReleaseX64))
            {
                List<TestCase> allTestCases = TestDataCreator.GetTestCasesOfTestDll(loadTests.TestDll);
                allTestCases.Should().HaveCount(TestResources.NrOfLoadTests);
                List<TestCase> testCases = allTestCases.Where(tc => int.Parse(tc.FullyQualifiedName.Substring("TaefSamples::LoadTests::Test".Length)) < 3000).ToList();
                var reporter = new FakeFrameworkReporter();
                var factory = new RecordingProcessExecutorFactory(new ProcessExecution.ProcessExecutorFactory());

                new TaefExecutor(TestEnvironment.Logger, TestEnvironment.Options, factory).RunTests(testCases, reporter, false);

                factory.Executions.Should().HaveCountGreaterThan(1);
                factory.Executions.Should().OnlyContain(e => e.CommandLineLength <= TaefConstants.MaxCommandLength);
                reporter.ReportedTestResults.Should().HaveCount(3000);
                reporter.ReportedTestResults.Select(tr => tr.TestCase).Should().OnlyHaveUniqueItems();
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(1500);
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(1500);
            }
        }

        #endregion

        #region Unit tests with faked TE.exe

        private TemporaryDirectory _directory;
        private FakeProcessExecutorFactory _factory;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
            _factory = new FakeProcessExecutorFactory
            {
                Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::1", "Passed"), ("A::2", "Failed"), ("B::1", "Passed")))
            };
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        private List<TestCase> FakeTestCases() => RunnerTestData.CreateTestCases(_directory.GetPath("Fake_taef.dll"), "A::1", "A::2", "B::1");

        private void UseFakeTe()
        {
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.FakeTeExecutable);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_Sequentially_AllTestsAreRunWithOneTeInvocation()
        {
            UseFakeTe();
            var reporter = new FakeFrameworkReporter();

            new TaefExecutor(MockLogger.Object, MockOptions.Object, _factory).RunTests(FakeTestCases(), reporter, false);

            _factory.Executions.Should().ContainSingle().Which.Kind.Should().Be(RecordedExecution.KindNormal);
            reporter.ReportedTestResults.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal("A::1 Passed", "A::2 Failed", "B::1 Passed");
            MockLogger.Verify(l => l.LogInfo("Running 3 tests..."), Times.Once);
            MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.StartsWith("Executing tests on"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_InParallel_TestsAreRunOnSeveralThreads()
        {
            UseFakeTe();
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(true);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);
            var reporter = new FakeFrameworkReporter();

            new TaefExecutor(MockLogger.Object, MockOptions.Object, _factory).RunTests(FakeTestCases(), reporter, false);

            _factory.Executions.Should().HaveCount(2);
            reporter.ReportedTestResults.Should().HaveCount(3);
            MockLogger.Verify(l => l.LogInfo("Executing tests on 2 threads"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_ParallelExecutionWhileDebugging_TestsAreRunSequentiallyWithDebugger()
        {
            UseFakeTe();
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(true);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);
            var reporter = new FakeFrameworkReporter();

            new TaefExecutor(MockLogger.Object, MockOptions.Object, _factory).RunTests(FakeTestCases(), reporter, true);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Kind.Should().Be(RecordedExecution.KindNativeDebugger);
            execution.Parameters.Should().EndWith(" /inproc /disableTimeouts");
            reporter.ReportedTestResults.Should().HaveCount(3);
            MockLogger.Verify(l => l.DebugInfo("Parallel execution is selected in options, but tests are executed sequentially because debugger is attached."), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_BeforeRunTests_NoTestsAreRun()
        {
            UseFakeTe();
            var reporter = new FakeFrameworkReporter();
            var executor = new TaefExecutor(MockLogger.Object, MockOptions.Object, _factory);

            executor.Cancel();
            executor.RunTests(FakeTestCases(), reporter, false);

            _factory.Executions.Should().BeEmpty();
            reporter.ReportedTestResults.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TeNotFound_ErrorIsLoggedOnceAndTestsAreNotFound()
        {
            // ARM32 test DLLs are not supported by current TAEF versions, so there is no TE.exe for them
            if ((System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
                .Any(d => !string.IsNullOrWhiteSpace(d) && System.IO.File.Exists(System.IO.Path.Combine(d.Trim(), TaefConstants.TeExecutableName))))
                Assert.Inconclusive("A TE.exe is on the PATH, it would be used for ARM32 test DLLs");
            if (System.IO.File.Exists(TestResources.GetTeExecutable(TaefLocator.ArchitectureArm)))
                Assert.Inconclusive("The installed TAEF supports ARM32 test DLLs");
            string armDll = _directory.GetPath("Arm_taef.dll");
            System.IO.File.WriteAllBytes(armDll, CreatePeHeaderWithMachineType(0x01C4));
            List<TestCase> testCases = RunnerTestData.CreateTestCases(armDll, "A::1", "A::2");
            var reporter = new FakeFrameworkReporter();

            new TaefExecutor(MockLogger.Object, MockOptions.Object, _factory).RunTests(testCases, reporter, false);

            _factory.Executions.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.StartsWith("Could not find TE.exe for arm test DLLs"))), Times.Once);
            reporter.ReportedTestResults.Should().HaveCount(2).And.OnlyContain(tr => tr.Outcome == TestOutcome.NotFound);
        }

        /// <returns>A minimal PE file (DOS header, PE signature and COFF file header) with the given machine type.</returns>
        private static byte[] CreatePeHeaderWithMachineType(ushort machineType)
        {
            var bytes = new byte[512];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            const int peHeaderOffset = 0x80;
            System.BitConverter.GetBytes(peHeaderOffset).CopyTo(bytes, 0x3C);
            bytes[peHeaderOffset] = (byte)'P';
            bytes[peHeaderOffset + 1] = (byte)'E';
            System.BitConverter.GetBytes(machineType).CopyTo(bytes, peHeaderOffset + 4);
            return bytes;
        }

        #endregion

    }

}
