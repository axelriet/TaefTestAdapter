// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.ProcessExecution;
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
    /// With TE.exe <c>/inproc</c>, the test's output to standard error (e.g. <c>std::cerr</c>) is written to TE.exe's
    /// standard error: it must be part of the output of the test which has written it (and of no other test).
    /// </summary>
    [TestClass]
    public class SequentialTestRunnerStandardErrorTests : TestsBase
    {
        private const string StandardErrorLine = "std::cerr output (visible with /inproc only)";
        private const string TestWritingToStandardError = "TaefSamples::OutputHandling::OutputOfPassingTest";

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_InProcessTestsWritingToStandardError_OutputIsAttributedToTheRightTest()
        {
            MockOptions.Setup(o => o.RunInProcess).Returns(true);
            // several runs: the order of the lines of standard output and standard error was not deterministic before
            for (int run = 0; run < 3; run++)
            {
                using (var testDirectory = new TemporaryDirectory())
                using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_DebugX86))
                {
                    var reporter = new FakeFrameworkReporter();
                    List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll)
                        .Where(tc => tc.FullyQualifiedName.StartsWith("TaefSamples::OutputHandling::"))
                        .ToList();
                    testCases.Should().HaveCountGreaterThan(1);

                    new SequentialTestRunner("", 0, testDirectory.Path, reporter, MockLogger.Object, MockOptions.Object, new SchedulingAnalyzer(MockLogger.Object))
                        .RunTests(testCases, false, new RecordingProcessExecutorFactory(new ProcessExecutorFactory()));

                    reporter.ReportedTestResults.Should().HaveCount(testCases.Count);
                    TestResult result = reporter.ReportedTestResults.Single(tr => tr.TestCase.FullyQualifiedName == TestWritingToStandardError);
                    result.Outcome.Should().Be(TestOutcome.Passed);
                    result.Output.Should().Contain("printf output (visible with /inproc only)");
                    result.Output.Should().Contain(StandardErrorLine, $"run {run}");
                    result.Output.IndexOf(StandardErrorLine).Should().BeGreaterThan(result.Output.IndexOf("printf output (visible with /inproc only)"));
                    reporter.ReportedTestResults
                        .Where(tr => tr.TestCase.FullyQualifiedName != TestWritingToStandardError)
                        .Should().OnlyContain(tr => tr.Output == null || !tr.Output.Contains(StandardErrorLine), $"run {run}");
                }
            }
        }
    }
}
