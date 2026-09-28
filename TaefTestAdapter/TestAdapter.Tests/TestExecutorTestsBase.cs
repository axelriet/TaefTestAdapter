// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using VsTestResultMessage = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Tests of <see cref="TestExecutor"/> running the sample test DLLs with TE.exe. The tests are run by several derived
    /// classes: sequentially, in parallel, and while being debugged (with the VsTest framework's debugger and with the
    /// native debugger); the expectations take the differences into account (see <see cref="IsTestOutputAvailable"/>
    /// and <see cref="IsRunInProcess"/>). All tests run copies of the sample DLLs (test execution writes test
    /// durations files next to the test DLLs).
    /// </summary>
    public abstract class TestExecutorTestsBase : TestAdapterTestsBase
    {
        protected readonly Mock<IDebuggerAttacher> MockDebuggerAttacher = new Mock<IDebuggerAttacher>();

        private readonly bool _parallelTestExecution;
        private readonly int _maxNrOfThreads;

        protected TestExecutorTestsBase(bool parallelTestExecution, int maxNrOfThreads)
        {
            _parallelTestExecution = parallelTestExecution;
            _maxNrOfThreads = maxNrOfThreads;
        }

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            MockOptions.Setup(o => o.ParallelTestExecution).Returns(_parallelTestExecution);
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(_maxNrOfThreads);

            MockDebuggerAttacher.Reset();
            MockDebuggerAttacher.Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>())).Returns(true);
        }

        #region Properties of the test run (overridden by the derived classes' setups)

        protected bool IsBeingDebugged => MockRunContext.Object.IsBeingDebugged;

        /// <summary>TE.exe's console output is not available if the tests are debugged with the VsTest framework's debugger (results are then taken from a WTT log).</summary>
        protected bool IsTestOutputAvailable => !IsBeingDebugged || MockOptions.Object.DebuggerKind > DebuggerKind.VsTestFramework;

        /// <summary>Tests are run within TE.exe (<c>/inproc</c>) if they are debugged or option RunInProcess is set.</summary>
        protected bool IsRunInProcess => IsBeingDebugged || MockOptions.Object.RunInProcess;

        protected bool IsRunInParallel => MockOptions.Object.ParallelTestExecution && !IsBeingDebugged;

        #endregion

        #region Helpers

        protected TestExecutor CreateExecutor()
        {
            return new TestExecutor(TestEnvironment.Logger, TestEnvironment.Options, MockDebuggerAttacher.Object);
        }

        /// <summary>Runs all tests of <paramref name="testDlls"/> (<see cref="TestExecutor.RunTests(IEnumerable{string}, Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IRunContext, Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IFrameworkHandle)"/>).</summary>
        protected void RunTests(params string[] testDlls)
        {
            CreateExecutor().RunTests(testDlls, MockRunContext.Object, MockFrameworkHandle.Object);
        }

        /// <summary>Runs the given tests (<see cref="TestExecutor.RunTests(IEnumerable{VsTestCase}, Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IRunContext, Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IFrameworkHandle)"/>).</summary>
        protected void RunTests(IEnumerable<VsTestCase> testCases)
        {
            CreateExecutor().RunTests(testCases, MockRunContext.Object, MockFrameworkHandle.Object);
        }

        /// <returns>The VS test cases of the given tests of <paramref name="testDll"/> (as discovered by the adapter).</returns>
        protected IList<VsTestCase> GetVsTestCases(string testDll, params string[] taefNames)
        {
            return ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll), taefNames);
        }

        protected void ClearRecordedResults()
        {
            MockFrameworkHandle.Invocations.Clear();
        }

        protected void RunAndVerifySingleTest(VsTestCase testCase, VsTestOutcome expectedOutcome)
        {
            RunTests(testCase.Yield());

            foreach (VsTestOutcome outcome in Enum.GetValues(typeof(VsTestOutcome)))
            {
                MockFrameworkHandle.Verify(h => h.RecordEnd(It.IsAny<VsTestCase>(), It.Is<VsTestOutcome>(to => to == outcome)),
                    Times.Exactly(outcome == expectedOutcome ? 1 : 0), $"outcome {outcome}");
            }
        }

        protected void RunAndVerifyTests(string testDll, int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests = 0,
            int nrOfSkippedTests = 0, int nrOfNotFoundTests = 0, bool checkNoErrorsLogged = true)
        {
            RunAndVerifyTests(new[] { testDll }, nrOfPassedTests, nrOfFailedTests, nrOfUnexecutedTests, nrOfSkippedTests, nrOfNotFoundTests, checkNoErrorsLogged);
        }

        protected void RunAndVerifyTests(string[] testDlls, int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests = 0,
            int nrOfSkippedTests = 0, int nrOfNotFoundTests = 0, bool checkNoErrorsLogged = true)
        {
            RunTests(testDlls);

            if (checkNoErrorsLogged)
                CheckNoErrorsLogged();

            CheckMockInvocations(nrOfPassedTests, nrOfFailedTests, nrOfUnexecutedTests, nrOfSkippedTests, nrOfNotFoundTests);
        }

        protected void CheckNoErrorsLogged()
        {
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.DebugError(It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Checks the numbers of results (and of RecordEnd calls) per outcome; <paramref name="nrOfUnexecutedTests"/> are
        /// results with outcome None (TAEF result NotRun).
        /// </summary>
        protected virtual void CheckMockInvocations(int nrOfPassedTests, int nrOfFailedTests, int nrOfUnexecutedTests, int nrOfSkippedTests, int nrOfNotFoundTests)
        {
            var expectedNumbers = new Dictionary<VsTestOutcome, int>
            {
                { VsTestOutcome.Passed, nrOfPassedTests },
                { VsTestOutcome.Failed, nrOfFailedTests },
                { VsTestOutcome.None, nrOfUnexecutedTests },
                { VsTestOutcome.Skipped, nrOfSkippedTests },
                { VsTestOutcome.NotFound, nrOfNotFoundTests }
            };

            IList<VsTestResult> results = GetRecordedResults();
            string actualNumbers = string.Join(", ", expectedNumbers.Keys.Select(o => $"{o}: {results.Count(r => r.Outcome == o)}"));
            foreach (KeyValuePair<VsTestOutcome, int> expected in expectedNumbers)
            {
                results.Count(r => r.Outcome == expected.Key).Should().Be(expected.Value, $"number of results with outcome {expected.Key} (actual numbers: {actualNumbers})");
                MockFrameworkHandle.Verify(h => h.RecordEnd(It.IsAny<VsTestCase>(), It.Is<VsTestOutcome>(to => to == expected.Key)),
                    Times.Exactly(expected.Value), $"RecordEnd() with outcome {expected.Key}");
            }
        }

        protected static bool HasStandardOutput(VsTestResult result, string text)
        {
            return result.Messages.Any(m => m.Category == VsTestResultMessage.StandardOutCategory && m.Text.Contains(text));
        }

        #endregion

        #region Settings affecting single tests

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_TestDirectoryViaUserParams_IsPassedViaCommandLineArg()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86).TestDll;
            VsTestCase testCase = GetVsTestCases(testDll, TestNames.TestDirectoryIsSet).Single();

            RunAndVerifySingleTest(testCase, VsTestOutcome.Failed);

            ClearRecordedResults();
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TestDirectory=" + PlaceholderReplacer.TestDirPlaceholder + "\"");

            RunAndVerifySingleTest(testCase, VsTestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WorkingDir_IsSetCorrectly()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86).TestDll;
            VsTestCase testCase = GetVsTestCases(testDll, TestNames.WorkingDirIsSolutionDirectory).Single();

            MockOptions.Setup(o => o.WorkingDir).Returns(PlaceholderReplacer.TestDllDirPlaceholder);
            RunAndVerifySingleTest(testCase, VsTestOutcome.Failed);

            ClearRecordedResults();
            MockOptions.Setup(o => o.WorkingDir).Returns(PlaceholderReplacer.SolutionDirPlaceholder);

            RunAndVerifySingleTest(testCase, VsTestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_EnvironmentVariables_AreSetCorrectly()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86).TestDll;
            VsTestCase testCase = GetVsTestCases(testDll, TestNames.EnvironmentVariableIsSet).Single();

            RunAndVerifySingleTest(testCase, VsTestOutcome.Failed);

            ClearRecordedResults();
            MockOptions.Setup(o => o.EnvironmentVariables).Returns("MYENVVAR=MyValue");

            RunAndVerifySingleTest(testCase, VsTestOutcome.Passed);
        }

        #endregion

        #region Complete test DLLs

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExternallyLinkedX86Tests_CorrectTestResults()
        {
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExternallyLinkedX86TestsInDebugMode_CorrectTestResults()
        {
            // for at least having the debug messaging code executed once
            MockOptions.Setup(o => o.OutputMode).Returns(OutputMode.Verbose);

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExternallyLinkedX64Tests_CorrectTestResults()
        {
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX64).TestDll, 1, 1);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_StaticallyLinkedX86Tests_CorrectTestResults()
        {
            SetupSampleTestsSettings();
            // let's print the test output
            MockOptions.Setup(o => o.PrintTestOutput).Returns(true);

            RunAndVerifyTests(CopySample(TestResources.Tests_DebugX86).TestDll,
                TestResources.NrOfTestsReportedAsPassed, TestResources.NrOfTestsReportedAsFailed,
                TestResources.NrOfTestsReportedAsNone, TestResources.NrOfTestsReportedAsSkipped);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_StaticallyLinkedX64TestsWithoutSampleSettings_CorrectTestResults()
        {
            RunAndVerifyTests(CopySample(TestResources.Tests_ReleaseX64).TestDll,
                TestResources.NrOfPassingTestsWithoutSampleSettings,
                TestResources.NrOfFailingTestsWithoutSampleSettings + TestResources.NrOfBlockedTests,
                TestResources.NrOfTestsReportedAsNone, TestResources.NrOfTestsReportedAsSkipped);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_TestDllsOfDifferentArchitectures_EachIsRunWithMatchingTeExecutable()
        {
            string x86TestDll = CopySample(TestResources.DllTests_DebugX86).TestDll;
            string x64TestDll = CopySample(TestResources.DllTests_DebugX64).TestDll;

            RunAndVerifyTests(new[] { x86TestDll, x64TestDll }, 2, 2);

            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains($"Running tests of test DLL '{x86TestDll}' with '") && s.EndsWith(@"\x86\TE.exe'", StringComparison.OrdinalIgnoreCase))),
                Times.AtLeastOnce());
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains($"Running tests of test DLL '{x64TestDll}' with '") && s.EndsWith(@"\x64\TE.exe'", StringComparison.OrdinalIgnoreCase))),
                Times.AtLeastOnce());
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_PrintTestOutput_OutputIsPrintedAtMostOnce()
        {
            MockOptions.Setup(o => o.PrintTestOutput).Returns(true);
            MockOptions.Setup(o => o.OutputMode).Returns(OutputMode.Info);

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX64).TestDll, 1, 1);

            // TE.exe's output is not printed if tests are run in parallel, and it is not available when debugging with the VsTest framework
            int nrOfExpectedLines = IsTestOutputAvailable && !IsRunInParallel ? 1 : 0;

            MockLogger.Verify(l => l.LogInfo(It.Is<string>(line => line == "StartGroup: " + TestNames.DllTestsPassing)), Times.Exactly(nrOfExpectedLines));
            MockLogger.Verify(l => l.LogInfo(It.Is<string>(line => line.StartsWith(">>>>>>>>>>>>>>> Output of command"))), Times.Exactly(nrOfExpectedLines));
            MockLogger.Verify(l => l.LogInfo(It.Is<string>(line => line.StartsWith("<<<<<<<<<<<<<<< End of Output"))), Times.Exactly(nrOfExpectedLines));
        }

        #endregion

        #region Crashing tests

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_HardCrashingX86Tests_CorrectTestResults()
        {
            RunTests(CopySample(TestResources.CrashingTests_DebugX86).TestDll);

            CheckCrashingTestsResults();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_CrashingX64Tests_CorrectTestResults()
        {
            RunTests(CopySample(TestResources.CrashingTests_ReleaseX64).TestDll);

            CheckCrashingTestsResults();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_CrashingTestsRunInProcess_TestsAfterCrashAreNotRun()
        {
            MockOptions.Setup(o => o.RunInProcess).Returns(true);

            RunTests(CopySample(TestResources.CrashingTests_DebugX64).TestDll);

            CheckCrashingTestsResults();
        }

        /// <summary>
        /// Checks the results of running all tests of CrashingTests_taef.dll: out of process, TE.exe reports crashes as
        /// failures and continues with the next test; in process, TE.exe dies in TaefSamples::Crashing::TheCrash (the remaining tests
        /// are reported as skipped). When debugging with the VsTest framework, tests also run in process, and the results
        /// are read from the incomplete WTT log TE.exe wrote until it died (the same results as in process).
        /// </summary>
        protected virtual void CheckCrashingTestsResults()
        {
            VsTestResult crashResult = GetRecordedResult(TestNames.CrashingTheCrash);
            crashResult.Outcome.Should().Be(VsTestOutcome.Failed);

            if (IsRunInProcess)
            {
                CheckMockInvocations(TestResources.NrOfCrashingTestsPassingInProcess, TestResources.NrOfCrashingTestsFailingInProcess + 1, 0,
                    TestResources.NrOfCrashingTestsNotStartedInProcess, 0);
                crashResult.ErrorMessage.Should().Contain(StreamingTaefOutputParser.CrashText);
                foreach (VsTestResult result in GetRecordedResults().Where(r => r.Outcome == VsTestOutcome.Skipped))
                {
                    result.ErrorMessage.Should().Contain($"reason is probably a crash of test {TestNames.CrashingTheCrash}");
                }
            }
            else
            {
                CheckMockInvocations(TestResources.NrOfCrashingTestsPassing, TestResources.NrOfCrashingTestsFailing + TestResources.NrOfCrashingTestsCrashing, 0, 0, 0);
                crashResult.ErrorMessage.Should().Contain("0xC0000005");
                GetRecordedResult(TestNames.CrashingTheStackOverflow).ErrorMessage.Should().Contain("0xC00000FD");
                GetRecordedResult(TestNames.CrashingAddPassesAfterAllCrashes).Outcome.Should().Be(VsTestOutcome.Passed);
            }
        }

        #endregion

        #region Batches and PATH extension

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithSetupAndTeardownBatchesWhereTeardownFails_LogsWarning()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns($"$(SolutionDir){TestResources.SucceedingBatch}");
            MockOptions.Setup(o => o.BatchForTestTeardown).Returns($"$(SolutionDir){TestResources.FailingBatch}");

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);

            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.AtLeastOnce());
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithSetupAndTeardownBatchesWhereSetupFails_LogsWarning()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns($"$(SolutionDir){TestResources.FailingBatch}");
            MockOptions.Setup(o => o.BatchForTestTeardown).Returns($"$(SolutionDir){TestResources.SucceedingBatch}");

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);

            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.AtLeastOnce());
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithoutBatches_NoLogging()
        {
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);

            MockLogger.Verify(l => l.LogInfo(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup))),
                Times.Never);
            MockLogger.Verify(l => l.LogInfo(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
            MockLogger.Verify(l => l.LogWarning(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
            MockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestTeardown))),
                Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithNonexistingSetupBatch_LogsError()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns("some_nonexisting_file");

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1, checkNoErrorsLogged: false);

            MockLogger.Verify(l => l.LogError(
                It.Is<string>(s => s.Contains(PreparingTestRunner.TestSetup.ToLower()))),
                Times.AtLeastOnce());
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithPathExtension_ExecutionOk()
        {
            string baseDir = TestDataCreator.PreparePathExtensionTest();
            try
            {
                string targetDll = TestDataCreator.GetPathExtensionTestDll(baseDir);
                MockOptions.Setup(o => o.PathExtension).Returns(PlaceholderReplacer.TestDllDirPlaceholder + @"\..\dll");

                RunAndVerifyTests(targetDll, 1, 1);
            }
            finally
            {
                Utils.DeleteDirectory(baseDir).Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_WithoutPathExtension_TestsAreBlocked()
        {
            string baseDir = TestDataCreator.PreparePathExtensionTest();
            try
            {
                string targetDll = TestDataCreator.GetPathExtensionTestDll(baseDir);

                // TE.exe can not load the test DLL (DllProject.dll is missing) and reports the tests as blocked
                RunAndVerifyTests(targetDll, 0, TestResources.NrOfDllTests);

                foreach (VsTestResult result in GetRecordedResults())
                {
                    result.ErrorMessage.Should().Contain(StreamingTaefOutputParser.BlockedPrefix);
                }
            }
            finally
            {
                Utils.DeleteDirectory(baseDir).Should().BeTrue();
            }
        }

        #endregion

        #region Outcomes, messages and output

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ExplicitResults_AreMappedToVsOutcomes()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll,
                TestNames.SkippedByTest, TestNames.BlockedByTest, TestNames.NotRunByTest, "TaefSamples::ExplicitResults::FailedByLogResult");

            RunTests(testCases);

            CheckMockInvocations(0, 2, 1, 1, 0);

            VsTestResult blocked = GetRecordedResult(TestNames.BlockedByTest);
            blocked.Outcome.Should().Be(VsTestOutcome.Failed);
            blocked.ErrorMessage.Trim().Should().StartWith(StreamingTaefOutputParser.BlockedPrefix + "Blocked by the test: a prerequisite is missing");

            GetRecordedResult(TestNames.NotRunByTest).Outcome.Should().Be(VsTestOutcome.None);
            GetRecordedResult(TestNames.SkippedByTest).ErrorMessage.Should().Contain("Skipped by the test");
            GetRecordedResult("TaefSamples::ExplicitResults::FailedByLogResult").ErrorMessage.Should().Contain("Failed set by the test");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_FailingTest_ErrorMessageAndStackTraceAreReported()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll, TestNames.TestMathAddFails);

            RunTests(testCases);

            VsTestResult result = GetRecordedResult(TestNames.TestMathAddFails);
            result.Outcome.Should().Be(VsTestOutcome.Failed);
            result.ErrorMessage.Should().Contain("Verify: AreEqual(1000, Add(10, 10)) - Values (1000, 20)");
            result.ErrorMessage.Should().NotContain("[File:");
            result.ErrorStackTrace.Should().MatchRegex(@"at .* in .*\\SampleTests\\Tests\\BasicTests\.cpp:line 22");
            result.TestCase.CodeFilePath.Should().EndWithEquivalent(@"SampleTests\Tests\BasicTests.cpp");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_OutputOfTest_IsReportedAsStandardOutput()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll, "TaefSamples::OutputHandling::OutputOfPassingTest");

            RunTests(testCases);

            VsTestResult result = GetRecordedResult("TaefSamples::OutputHandling::OutputOfPassingTest");
            result.Outcome.Should().Be(VsTestOutcome.Passed);
            HasStandardOutput(result, "Log::Comment output").Should().BeTrue();
            HasStandardOutput(result, "MyContext: Log::Comment output with a context").Should().BeTrue();
            HasStandardOutput(result, "Warning: Log::Warning output (does not fail the test)").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_ConsoleOutputOfTest_IsOnlyAvailableIfRunInProcess()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll, "TaefSamples::OutputHandling::Output_ManyLinesWithNewlines");

            RunTests(testCases);

            VsTestResult result = GetRecordedResult("TaefSamples::OutputHandling::Output_ManyLinesWithNewlines");
            result.Outcome.Should().Be(VsTestOutcome.Failed);
            HasStandardOutput(result, "before test 1").Should().BeTrue();
            HasStandardOutput(result, "std::cout before test (visible with /inproc only)").Should().Be(IsRunInProcess && IsTestOutputAvailable);

            ClearRecordedResults();
            MockOptions.Setup(o => o.RunInProcess).Returns(true);

            RunTests(testCases);

            result = GetRecordedResult("TaefSamples::OutputHandling::Output_ManyLinesWithNewlines");
            result.Outcome.Should().Be(VsTestOutcome.Failed);
            HasStandardOutput(result, "std::cout before test (visible with /inproc only)").Should().Be(IsTestOutputAvailable);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_NrOfTestRepetitions_EachRepetitionIsReported()
        {
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(2);

            // in process (and while debugging), each test is repeated within a single loop (/Loop:1 /LoopTest:2), since TE.exe
            // can not start a new test host for each loop
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 2, 2);

            GetRecordedResults().Should().OnlyContain(r => r.ErrorMessage == null || !r.ErrorMessage.Contains(StreamingTaefOutputParser.BlockedPrefix));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_IsolationLevel_TestsAreRunNormally()
        {
            // a test host per test (out of process); not passed to TE.exe if tests are run in process (TE.exe would block the tests)
            MockOptions.Setup(o => o.IsolationLevel).Returns(TaefIsolationLevel.Test);

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);

            GetRecordedResults().Should().OnlyContain(r => r.ErrorMessage == null || !r.ErrorMessage.Contains(StreamingTaefOutputParser.BlockedPrefix));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_NrOfTestRepetitionsAndRunInProcess_EachRepetitionIsReported()
        {
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(2);
            MockOptions.Setup(o => o.RunInProcess).Returns(true);

            // /Loop:2 would block the tests of the second loop ("TAEF would need to start a second test host, but the /InProc
            // switch is being used"), /LoopTest:2 repeats each test in the same test host
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX64).TestDll, 2, 2);

            GetRecordedResults().Should().OnlyContain(r => r.ErrorMessage == null || !r.ErrorMessage.Contains(StreamingTaefOutputParser.BlockedPrefix));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_TestTimeout_TestsAreAbortedUnlessRunInProcess()
        {
            MockOptions.Setup(o => o.TestTimeout).Returns("0:0:1");

            RunTests(CopySample(TestResources.LongRunningTests_ReleaseX86).TestDll);

            // TE.exe does not support timeouts for tests run in process (and while debugging)
            if (IsRunInProcess)
            {
                CheckMockInvocations(1, 1, 0, 0, 0);
            }
            else
            {
                CheckMockInvocations(0, 2, 0, 0, 0);
                GetRecordedResult(TestNames.LongRunningTest1).Duration.Should().BeLessThan(TimeSpan.FromMilliseconds(TestResources.LongRunningTestDurationInMs));
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_TestNotContainedInTestDll_IsReportedAccordingToMissingTestsReportMode()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            VsTestCase testCase = TestDataCreator.ToTestCase("TaefSamples::Passing::DoesNotExist", testDll).ToVsTestCase();

            var expectedNumbers = new Dictionary<MissingTestsReportMode, int[]>
            {
                { MissingTestsReportMode.DoNotReport, new[] { 0, 0, 0 } },
                { MissingTestsReportMode.ReportAsFailed, new[] { 1, 0, 0 } },
                { MissingTestsReportMode.ReportAsSkipped, new[] { 0, 1, 0 } },
                { MissingTestsReportMode.ReportAsNotFound, new[] { 0, 0, 1 } }
            };
            foreach (KeyValuePair<MissingTestsReportMode, int[]> expected in expectedNumbers)
            {
                ClearRecordedResults();
                MockLogger.Invocations.Clear();
                MockOptions.Setup(o => o.MissingTestsReportMode).Returns(expected.Key);

                RunTests(testCase.Yield());

                CheckMockInvocations(0, expected.Value[0], 0, expected.Value[1], expected.Value[2]);
                // TE.exe's exit code 0x07000000: no test matched the selection
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("0x07000000"))), Times.Once, $"mode {expected.Key}");
            }
        }

        #endregion

        #region Ignored tests

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_IgnoredTests_AreReportedAsSkippedWithoutRunningTe()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll, TestNames.IgnoredTest, TestNames.IgnoredPassing, TestNames.IgnoredFailing);
            // TE.exe must not be started: a TE.exe which fails would be reported
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.AlwaysFailingExe);

            RunTests(testCases);

            CheckMockInvocations(0, 0, 0, TestResources.NrOfIgnoredTests, 0);
            foreach (VsTestResult result in GetRecordedResults())
            {
                result.ErrorMessage.Should().Contain(TaefConstants.IgnoredTestMessage);
            }
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            CheckNoErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_IgnoredTestsWithRunIgnoredTests_AreRun()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll, TestNames.IgnoredTest, TestNames.IgnoredPassing, TestNames.IgnoredFailing);
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);

            RunTests(testCases);

            CheckMockInvocations(TestResources.NrOfPassingIgnoredTests, TestResources.NrOfFailingIgnoredTests, 0, 0, 0);
            GetRecordedResult(TestNames.IgnoredFailing).Outcome.Should().Be(VsTestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_AllTestsWithRunIgnoredTests_IgnoredTestsAreRun()
        {
            SetupSampleTestsSettings();
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);

            RunAndVerifyTests(CopySample(TestResources.Tests_DebugX64).TestDll,
                TestResources.NrOfPassingTests + TestResources.NrOfPassingIgnoredTests,
                TestResources.NrOfFailingTests + TestResources.NrOfBlockedTests + TestResources.NrOfFailingIgnoredTests,
                TestResources.NrOfNotRunTests, TestResources.NrOfSkippedTests);
        }

        #endregion

        #region Test case filters

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_FilterByFullyQualifiedName_OnlyMatchingTestsAreRun()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            // the VS name uses '.' as scope separator (except within the data row name)
            SetupTestCaseFilter("FullyQualifiedName=TaefSamples.TestMath.AddPasses");

            RunTests(testDll);

            CheckMockInvocations(1, 0, 0, 0, 0);
            GetRecordedResult(TestNames.TestMathAddPasses).TestCase.FullyQualifiedName.Should().Be("TaefSamples.TestMath.AddPasses");

            ClearRecordedResults();
            SetupTestCaseFilter("FullyQualifiedName=TaefSamples.NamedRows.SpecialCharacters#with::colons [x]");

            RunTests(testDll);

            CheckMockInvocations(0, 1, 0, 0, 0);
            GetRecordedResult(TestNames.RowWithColons).Outcome.Should().Be(VsTestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_FilterByDisplayName_OnlyMatchingTestsAreRun()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            // the display name is the TAEF name
            SetupTestCaseFilter("DisplayName~TaefSamples::NamedRows::SpecialCharacters#with");

            RunTests(testDll);

            // with space, with'quote, with#hash, with::colons [x]
            CheckMockInvocations(2, 2, 0, 0, 0);
            GetRecordedResult(TestNames.RowWithQuote).Outcome.Should().Be(VsTestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_FilterByTrait_OnlyMatchingTestsAreRun()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            // trait names are not case sensitive; IgnoredTest is ignored and thus reported as skipped
            SetupTestCaseFilter("owner=ClassOwner");

            RunTests(testDll);

            CheckMockInvocations(3, 2, 0, 1, 0);
            GetRecordedResult(TestNames.IgnoredTest).Outcome.Should().Be(VsTestOutcome.Skipped);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_FilterByTraitAndName_OnlyMatchingTestsAreRun()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            SetupTestCaseFilter("Priority=1&FullyQualifiedName~NamedRows.");

            RunTests(testDll);

            CheckMockInvocations(0, 1, 0, 0, 0);
            GetRecordedResult(TestNames.RowWithQuote).Outcome.Should().Be(VsTestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_InvalidFilter_NoTestsAreRun()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            SetupTestCaseFilter("NoSuchProperty=Foo");

            RunTests(testDll);

            CheckMockInvocations(0, 0, 0, 0, 0);
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Test case filter is invalid"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_FilterWithVsTestCases_OnlyMatchingTestsAreRun()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            IList<VsTestCase> testCases = GetVsTestCases(testDll, TestNames.DllTestsPassing, TestNames.DllTestsFailing);
            SetupTestCaseFilter("FullyQualifiedName!=TaefSamples.Failing.InvokeFunction");

            RunTests(testCases);

            CheckMockInvocations(1, 0, 0, 0, 0);
        }

        #endregion

        #region Test sources

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_VsTestCasesWithoutTaefNameProperty_TaefNameIsTakenFromDisplayName()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            // e.g. created by a VsTest framework version which does not transfer the adapter's properties
            List<VsTestCase> testCases = GetVsTestCases(testDll, TestNames.DllTestsPassing, TestNames.DllTestsFailing)
                .Select(tc => new VsTestCase(tc.FullyQualifiedName, tc.ExecutorUri, tc.Source) { DisplayName = tc.DisplayName })
                .ToList();
            testCases.Should().OnlyContain(tc => tc.GetPropertyValue(DataConversionExtensions.TaefNameProperty) == null);

            RunTests(testCases);

            CheckMockInvocations(1, 1, 0, 0, 0);
            GetRecordedResult(TestNames.DllTestsPassing).Outcome.Should().Be(VsTestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public virtual void RunTests_SourcesContainNonTaefDlls_OnlyTaefTestsAreRun()
        {
            SampleCopy copy = CopySample(TestResources.DllTests_ReleaseX86);
            string dependency = Path.Combine(copy.Directory, TestResources.DllProjectDll);
            string managedDll = typeof(TestExecutor).Assembly.Location;

            RunAndVerifyTests(new[] { dependency, copy.TestDll, managedDll }, 1, 1);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        #endregion

    }

}
