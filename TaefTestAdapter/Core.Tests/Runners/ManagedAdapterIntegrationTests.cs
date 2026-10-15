// This file has been added for TAEF support.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Runners
{
    [TestClass]
    public class ManagedAdapterIntegrationTests : TestsBase
    {
        private const string BasicClass = "TaefSampleTests.Managed.BasicTests";
        private const string DataClass = "TaefSampleTests.Managed.DataTests";

        [TestMethod]
        [TestCategory(Integration)]
        public void ManagedFramework_DiscoverySelectionAndExecutionUseProductionPaths()
        {
            VerifyManagedRuntime("net48");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ManagedNet8_DiscoverySelectionAndExecutionUseProductionPaths()
        {
            VerifyManagedRuntime("net8.0-windows");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ManagedNet10_DiscoverySelectionAndExecutionUseProductionPaths()
        {
            VerifyManagedRuntime("net10.0-windows");
        }

        private void VerifyManagedRuntime(string framework)
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.GetManagedSampleDll(framework, "Debug")))
            {
                var factory = new RecordingProcessExecutorFactory(new ProcessExecutorFactory());
                IList<TestCase> tests = new TaefDiscoverer(MockLogger.Object, MockOptions.Object, factory)
                    .GetTestsFromTestDll(sample.TestDll);

                tests.Should().HaveCount(12, framework);
                tests.Should().OnlyContain(t => t.IsManaged && t.Source == sample.TestDll);
                tests.Should().OnlyContain(t => t.LineNumber > 0 && t.CodeFilePath.EndsWith("ManagedTests.cs"));
                tests.Select(t => t.FullyQualifiedName).Should().OnlyHaveUniqueItems();
                tests.Should().Contain(t => t.FullyQualifiedName == "GlobalManagedTests.GlobalPassingTest");
                tests.Should().Contain(t => t.FullyQualifiedName == DataClass + ".TableData#row.with.dot");
                tests.Should().Contain(t => t.FullyQualifiedName == DataClass + ".TableData#row::with::colons");
                TestCase passing = tests.Single(t => t.FullyQualifiedName == BasicClass + ".PassingTest");
                passing.Traits.Should().Contain(t => t.Name == "ModuleProperty" && t.Value == "ManagedModule");
                passing.Traits.Should().Contain(t => t.Name == "ClassProperty" && t.Value == "ManagedClass");
                passing.Traits.Should().Contain(t => t.Name == "Owner" && t.Value == "ManagedTests");
                tests.Single(t => t.FullyQualifiedName == BasicClass + ".AsyncPassingTest").Traits
                    .Should().NotContain(t => t.Name.StartsWith("System.Runtime.CompilerServices."));
                tests.Where(t => t.TaefClassName == BasicClass).Should().OnlyContain(t =>
                    t.Properties.OfType<TestCaseMetaDataProperty>().Single().NrOfTestCasesInClass == 6);
                tests.Where(t => t.TaefClassName == DataClass).Should().OnlyContain(t =>
                    t.Properties.OfType<TestCaseMetaDataProperty>().Single().NrOfTestCasesInClass == 5);
                factory.Executions.Should().ContainSingle().Which.ExitCode.Should().Be(0);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);

                List<TestCase> selected = tests.Where(t => t.TaefClassName == DataClass).ToList();
                selected.Should().HaveCount(5);
                FakeFrameworkReporter selectedReporter = Run(selected, sample.Directory, factory);
                RecordedExecution selectedExecution = factory.Executions.Last();
                selectedExecution.ExitCode.Should().Be(0, string.Join("\n", selectedExecution.Output));
                selectedExecution.Parameters.Should().Contain("(@Name='" + DataClass + ".*' and not @Name='" + DataClass + ".*.*')");
                selectedExecution.Parameters.Should().Contain("@Name='" + DataClass + ".TableData#row.with.dot'");
                selectedReporter.ReportedTestResults.Select(r => r.TestCase.FullyQualifiedName)
                    .Should().BeEquivalentTo(selected.Select(t => t.FullyQualifiedName));
                selectedReporter.ReportedTestResults.Should().OnlyContain(r => r.Outcome == TestOutcome.Passed);

                FakeFrameworkReporter allReporter = Run(tests, sample.Directory, factory);
                RecordedExecution allExecution = factory.Executions.Last();
                allExecution.ExitCode.Should().HaveValue();
                allExecution.Parameters.Should().NotContain("/select:");
                allReporter.ReportedTestResults.Should().HaveCount(12);
                allReporter.ReportedTestResults.Select(r => r.TestCase.FullyQualifiedName).Should().OnlyHaveUniqueItems();
                allReporter.ReportedTestResults.Count(r => r.Outcome == TestOutcome.Passed).Should().Be(9);
                allReporter.ReportedTestResults.Count(r => r.Outcome == TestOutcome.Failed).Should().Be(2);
                allReporter.ReportedTestResults.Count(r => r.Outcome == TestOutcome.Skipped).Should().Be(1);
                allReporter.ReportedTestResults.Single(r => r.TestCase.FullyQualifiedName == BasicClass + ".IgnoredTest")
                    .ErrorMessage.Should().Be(TaefConstants.IgnoredTestMessage);
                TestResult failure = allReporter.ReportedTestResults.Single(r => r.TestCase.FullyQualifiedName == BasicClass + ".FailingTest");
                failure.ErrorMessage.Should().Contain("Managed failure for adapter validation");
                failure.ErrorStackTrace.Should().Contain("BasicTests.FailingTest()");
                TestResult asyncFailure = allReporter.ReportedTestResults.Single(r => r.TestCase.FullyQualifiedName == BasicClass + ".AsyncFailingTest");
                asyncFailure.ErrorMessage.Should().Contain("Managed async failure");
                asyncFailure.ErrorStackTrace.Should().NotBeNullOrEmpty();
                if (framework != "net48")
                {
                    allExecution.EnvironmentVariables.Should().ContainKey(ManagedRuntimeResolver.CoreClrVariable);
                    failure.ErrorStackTrace.Should().Contain("ManagedTests.cs:line ");
                    asyncFailure.ErrorStackTrace.Should().Contain("ManagedTests.cs:line ");
                }

                IList<TestResult> parsed = new TaefOutputParser(tests, allExecution.Output, MockLogger.Object).GetTestResults();
                foreach (TestResult result in parsed)
                {
                    TestResult streamed = allReporter.ReportedTestResults.Single(r => r.TestCase.Equals(result.TestCase));
                    result.Outcome.Should().Be(streamed.Outcome);
                    result.ErrorMessage.Should().Be(streamed.ErrorMessage);
                    result.ErrorStackTrace.Should().Be(streamed.ErrorStackTrace);
                    result.Output.Should().Be(streamed.Output);
                }
                parsed.Should().HaveCount(11, "the adapter reports the ignored test without running it in TE.exe");
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        private FakeFrameworkReporter Run(IEnumerable<TestCase> tests, string testDirectory, RecordingProcessExecutorFactory factory)
        {
            var reporter = new FakeFrameworkReporter();
            var runner = new SequentialTestRunner("", 0, testDirectory, reporter, MockLogger.Object, MockOptions.Object,
                new SchedulingAnalyzer(MockLogger.Object));
            runner.RunTests(tests, false, factory);
            return reporter;
        }
    }
}
