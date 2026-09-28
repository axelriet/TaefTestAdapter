// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Runs all sample test DLLs of a configuration (except LoadTests_taef.dll) in parallel with TE.exe (copies of the DLLs
    /// in temporary directories) and checks the outcomes documented in SampleTests\README.md.
    /// </summary>
    [TestClass]
    public class ParallelTestRunnerIntegrationTests : TestsBase
    {
        private static readonly string[] SampleTestDllsExceptLoadTests =
        {
            TestResources.TestsDll, TestResources.CrashingTestsDll, TestResources.LongRunningTestsDll, TestResources.DllTestsDll,
            TestResources.LeakCheckTestsDll, TestResources.HelperFileTestsDll, TestResources.ClrTestsDll
        };

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_AllSampleTestDllsDebugX64_OutcomesAsDocumented()
        {
            DoRunAllSampleTestDlls(SampleConfiguration.DebugX64);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_AllSampleTestDllsReleaseX86_OutcomesAsDocumented()
        {
            DoRunAllSampleTestDlls(SampleConfiguration.ReleaseX86);
        }

        private void DoRunAllSampleTestDlls(SampleConfiguration configuration)
        {
            SetupSampleTestsSettings();
            // HelperFileTests_taef.dll needs the value of its helper file; the other test DLLs ignore the runtime parameter
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TestDirectory=$(TestDir)\" /p:\"TheTarget=$(TheTarget)\"");
            MockOptions.Setup(o => o.ParallelTestExecution).Returns(true);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(4);

            var copies = new List<SampleCopy>();
            try
            {
                copies.AddRange(SampleTestDllsExceptLoadTests.Select(dll => SampleCopy.Create(TestResources.GetSampleDll(dll, configuration))));
                List<TestCase> testCases = copies.SelectMany(c => TestDataCreator.GetTestCasesOfTestDll(c.TestDll)).ToList();
                const int nrOfTests = TestResources.NrOfAllSampleTests - TestResources.NrOfLoadTests;
                testCases.Should().HaveCount(nrOfTests);
                var reporter = new FakeFrameworkReporter();
                var factory = new RecordingProcessExecutorFactory(new ProcessExecutorFactory());

                new ParallelTestRunner(reporter, MockLogger.Object, MockOptions.Object, new SchedulingAnalyzer(MockLogger.Object))
                    .RunTests(testCases, false, factory);

                // every thread runs each test DLL with the TE.exe of the configuration's architecture
                factory.Executions.Should().OnlyContain(e => e.Command == TestResources.GetTeExecutable(configuration));
                factory.Executions.Select(e => e.Parameters.Substring(0, e.Parameters.IndexOf(".dll\"") + 5)).Distinct()
                    .Should().HaveCount(SampleTestDllsExceptLoadTests.Length);
                MockLogger.Verify(l => l.LogInfo("Executing tests on 4 threads"), Times.Once);

                int passingLeakCheckTests = configuration.IsDebug() ? TestResources.NrOfPassingLeakCheckTestsDebug : TestResources.NrOfPassingLeakCheckTestsRelease;
                int failingLeakCheckTests = configuration.IsDebug() ? TestResources.NrOfFailingLeakCheckTestsDebug : TestResources.NrOfFailingLeakCheckTestsRelease;
                int expectedPassed = TestResources.NrOfTestsReportedAsPassed + TestResources.NrOfCrashingTestsPassing + 1 /* LongRunning */
                                     + 1 /* DllTests */ + passingLeakCheckTests + TestResources.NrOfHelperFileTests + 1 /* ClrTests */;
                int expectedFailed = TestResources.NrOfTestsReportedAsFailed + TestResources.NrOfCrashingTestsFailing + TestResources.NrOfCrashingTestsCrashing
                                     + 1 /* LongRunning */ + 1 /* DllTests */ + failingLeakCheckTests + 1 /* ClrTests */;

                reporter.ReportedTestResults.Should().HaveCount(nrOfTests);
                reporter.ReportedTestResults.Select(tr => tr.TestCase).Should().OnlyHaveUniqueItems();
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(expectedPassed);
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(expectedFailed);
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.Skipped).Should().Be(TestResources.NrOfTestsReportedAsSkipped);
                reporter.ReportedTestResults.Count(tr => tr.Outcome == TestOutcome.None).Should().Be(TestResources.NrOfTestsReportedAsNone);
                reporter.ReportedTestResults.Single(tr => tr.TestCase.FullyQualifiedName == TestResources.TestNames.HelperFileTheTargetIsSet)
                    .Outcome.Should().Be(TestOutcome.Passed);
                reporter.ReportedTestResults.Single(tr => tr.TestCase.FullyQualifiedName == TestResources.TestNames.ClrTestsPass)
                    .Outcome.Should().Be(TestOutcome.Passed);

                // results of all threads are recorded in the durations files
                foreach (SampleCopy copy in copies)
                {
                    new TestDurationSerializer().ReadTestDurations(testCases.Where(tc => tc.Source == copy.TestDll))
                        .Should().NotBeEmpty(copy.TestDll);
                }
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                copies.ForEach(c => c.Dispose());
            }
        }

    }

}
