// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Framework
{
    [TestClass]
    public class VsTestFrameworkReporterTests : TestAdapterTestsBase
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestResults_InVisualStudio_ErrorMessageStartsWithNewline()
        {
            DoTestBeginOfErrorMessage(true, Environment.NewLine);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestResults_FromVsTestConsole_ErrorMessageStartsInSameline()
        {
            DoTestBeginOfErrorMessage(false, "");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestResults_FromVsTestConsole_StacktraceEndsWithoutNewline()
        {
            var errorStacktrace = "My stack trace";
            var result = new TestResult(TestDataCreator.ToTestCase("MyClass::MyTestCase"))
            {
                Outcome = TestOutcome.Failed,
                ErrorStackTrace = errorStacktrace + Environment.NewLine,
                ComputerName = "My Computer"
            };

            var reporter = new VsTestFrameworkReporter(MockFrameworkHandle.Object, false, MockLogger.Object);
            reporter.ReportTestResults(result.Yield());

            MockFrameworkHandle.Verify(h => h.RecordResult(
                It.Is<VsTestResult>(tr => tr.ErrorStackTrace.Equals(errorStacktrace))),
                Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestResults_InVisualStudio_MessageOfSkippedTestStartsWithNewlineButNotOfPassedTest()
        {
            var skipped = new TestResult(TestDataCreator.ToTestCase("MyClass::Skipped")) { Outcome = TestOutcome.Skipped, ErrorMessage = "skipped", ComputerName = "MyComputer" };
            var passed = new TestResult(TestDataCreator.ToTestCase("MyClass::Passed")) { Outcome = TestOutcome.Passed, ErrorMessage = "passed", ComputerName = "MyComputer" };

            var reporter = new VsTestFrameworkReporter(MockFrameworkHandle.Object, true, MockLogger.Object);
            reporter.ReportTestResults(new[] { skipped, passed });

            MockFrameworkHandle.Verify(h => h.RecordResult(It.Is<VsTestResult>(tr => tr.ErrorMessage == Environment.NewLine + "skipped" && tr.Outcome == VsTestOutcome.Skipped)), Times.Once);
            MockFrameworkHandle.Verify(h => h.RecordResult(It.Is<VsTestResult>(tr => tr.ErrorMessage == "passed" && tr.Outcome == VsTestOutcome.Passed)), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestResults_Result_IsRecordedAndEnded()
        {
            var result = new TestResult(TestDataCreator.ToTestCase("Ns::MyClass::MyTest#metadataSet0"))
            {
                Outcome = TestOutcome.Failed,
                Output = "the output",
                ComputerName = "MyComputer"
            };

            var reporter = new VsTestFrameworkReporter(MockFrameworkHandle.Object, false, MockLogger.Object);
            reporter.ReportTestResults(result.Yield());

            MockFrameworkHandle.Verify(h => h.RecordResult(It.Is<VsTestResult>(tr =>
                tr.TestCase.FullyQualifiedName == "Ns.MyClass.MyTest#metadataSet0"
                && tr.Outcome == VsTestOutcome.Failed
                && tr.Messages.Single().Text == "the output")), Times.Once);
            MockFrameworkHandle.Verify(h => h.RecordEnd(
                It.Is<VsTestCase>(tc => tc.DisplayName == "Ns::MyClass::MyTest#metadataSet0"),
                VsTestOutcome.Failed), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestsStarted_TestCases_AreRecordedAsStarted()
        {
            var reporter = new VsTestFrameworkReporter(MockFrameworkHandle.Object, false, MockLogger.Object);
            reporter.ReportTestsStarted(new[] { TestDataCreator.ToTestCase("A::B"), TestDataCreator.ToTestCase("A::C") });

            MockFrameworkHandle.Verify(h => h.RecordStart(It.Is<VsTestCase>(tc => tc.FullyQualifiedName == "A.B" && tc.DisplayName == "A::B")), Times.Once);
            MockFrameworkHandle.Verify(h => h.RecordStart(It.Is<VsTestCase>(tc => tc.FullyQualifiedName == "A.C")), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestsFound_TestCases_AreSentToDiscoverySink()
        {
            var mockSink = new Mock<ITestCaseDiscoverySink>();
            var testCase = TestDataCreator.ToTestCase("Ns::A::B", new Trait("Owner", "Me"));

            var reporter = new VsTestFrameworkReporter(mockSink.Object, MockLogger.Object);
            reporter.ReportTestsFound(testCase.Yield());

            mockSink.Verify(s => s.SendTestCase(It.Is<VsTestCase>(tc =>
                tc.FullyQualifiedName == "Ns.A.B"
                && tc.DisplayName == "Ns::A::B"
                && tc.GetTaefName() == "Ns::A::B"
                && tc.Source == testCase.Source
                && tc.Traits.Single().Name == "Owner")), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportTestResults_CalledFromSeveralThreads_FrameworkHandleIsNeverCalledConcurrently()
        {
            const int nrOfThreads = 8;
            const int nrOfResultsPerThread = 25;
            int nrOfConcurrentCalls = 0;
            int nrOfOverlappingCalls = 0;
            int nrOfRecordedResults = 0;
            MockFrameworkHandle.Setup(h => h.RecordResult(It.IsAny<VsTestResult>())).Callback(() =>
            {
                if (Interlocked.Increment(ref nrOfConcurrentCalls) > 1)
                    Interlocked.Increment(ref nrOfOverlappingCalls);
                Thread.SpinWait(1000);
                Interlocked.Increment(ref nrOfRecordedResults);
                Interlocked.Decrement(ref nrOfConcurrentCalls);
            });

            var reporter = new VsTestFrameworkReporter(MockFrameworkHandle.Object, false, MockLogger.Object);
            var tasks = new List<Task>();
            for (int i = 0; i < nrOfThreads; i++)
            {
                int thread = i;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    for (int j = 0; j < nrOfResultsPerThread; j++)
                    {
                        reporter.ReportTestResults(new TestResult(TestDataCreator.ToTestCase($"Thread{thread}::Test{j}")) { Outcome = TestOutcome.Passed, ComputerName = "MyComputer" }.Yield());
                    }
                }, TaskCreationOptions.LongRunning));
            }
            Task.WaitAll(tasks.ToArray());

            nrOfRecordedResults.Should().Be(nrOfThreads * nrOfResultsPerThread);
            nrOfOverlappingCalls.Should().Be(0);
        }

        private void DoTestBeginOfErrorMessage(bool inVisualStudio, string beginOfErrorMessage)
        {
            var errorMessage = "My error message";
            var result = new TestResult(TestDataCreator.ToTestCase("MyClass::MyTestCase"))
            {
                Outcome = TestOutcome.Failed,
                ErrorMessage = errorMessage,
                ComputerName = "My Computer"
            };

            var reporter = new VsTestFrameworkReporter(MockFrameworkHandle.Object, inVisualStudio, MockLogger.Object);
            reporter.ReportTestResults(result.Yield());

            MockFrameworkHandle.Verify(h => h.RecordResult(
                It.Is<VsTestResult>(tr => tr.ErrorMessage.Equals(beginOfErrorMessage + errorMessage))),
                Times.Exactly(1));
        }

    }

}
