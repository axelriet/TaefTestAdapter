// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Framework;
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
    /// Tests of <see cref="PreparingTestRunner"/>: test setup/teardown batch files (run with cmd.exe), thread names, the
    /// test directory, and error handling. TE.exe is faked.
    /// </summary>
    [TestClass]
    public class PreparingTestRunnerTests : TestsBase
    {
        private TemporaryDirectory _directory;
        private FakeFrameworkReporter _reporter;
        private FakeProcessExecutorFactory _fakeTeFactory;

        private string TestDll => _directory.GetPath("Fake_taef.dll");

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
            _reporter = new FakeFrameworkReporter();
            _fakeTeFactory = new FakeProcessExecutorFactory
            {
                Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed")))
            };
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.FakeTeExecutable);
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        /// <summary>A factory running batch files with cmd.exe (like the real one), but faking TE.exe.</summary>
        private class BatchRunningFactory : TaefTestAdapter.ProcessExecution.Contracts.IDebuggedProcessExecutorFactory
        {
            private readonly FakeProcessExecutorFactory _fakeTeFactory;

            public BatchRunningFactory(FakeProcessExecutorFactory fakeTeFactory)
            {
                _fakeTeFactory = fakeTeFactory;
            }

            public TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor CreateExecutor(bool printTestOutput, Common.ILogger logger)
                => new SelectingExecutor(new DotNetProcessExecutor(printTestOutput, logger), _fakeTeFactory.CreateExecutor(printTestOutput, logger));

            public TaefTestAdapter.ProcessExecution.Contracts.IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, Common.ILogger logger)
                => _fakeTeFactory.CreateFrameworkDebuggingExecutor(printTestOutput, logger);

            public TaefTestAdapter.ProcessExecution.Contracts.IDebuggedProcessExecutor CreateNativeDebuggingExecutor(Common.DebuggerEngine debuggerEngine, bool printTestOutput, Common.ILogger logger)
                => _fakeTeFactory.CreateNativeDebuggingExecutor(debuggerEngine, printTestOutput, logger);

            private class SelectingExecutor : TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor
            {
                private readonly TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor _realExecutor;
                private readonly TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor _fakeExecutor;

                public SelectingExecutor(TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor realExecutor, TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor fakeExecutor)
                {
                    _realExecutor = realExecutor;
                    _fakeExecutor = fakeExecutor;
                }

                private TaefTestAdapter.ProcessExecution.Contracts.IProcessExecutor GetExecutor(string command)
                    => command.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase) ? _realExecutor : _fakeExecutor;

                public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension,
                    IDictionary<string, string> environmentVariables, Action<string> reportOutputLine)
                    => GetExecutor(command).ExecuteCommandBlocking(command, parameters, workingDir, pathExtension, environmentVariables, reportOutputLine);

                public void Cancel()
                {
                    _realExecutor.Cancel();
                    _fakeExecutor.Cancel();
                }
            }
        }

        private PreparingTestRunner CreateRunner(int threadId = -1, ITestFrameworkReporter reporter = null)
        {
            return new PreparingTestRunner(threadId, reporter ?? _reporter, MockLogger.Object, MockOptions.Object, new SchedulingAnalyzer(MockLogger.Object));
        }

        private void RunTests(PreparingTestRunner runner, IEnumerable<TestCase> testCases = null)
        {
            runner.RunTests(testCases ?? RunnerTestData.CreateTestCases(TestDll, "A::X"), false, new BatchRunningFactory(_fakeTeFactory));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_SucceedingSetupAndFailingTeardownBatches_BatchesAreRunAndExitCodesAreLogged()
        {
            MockOptions.Setup(o => o.BatchForTestSetup).Returns("$(SolutionDir)" + TestResources.SucceedingBatch);
            MockOptions.Setup(o => o.BatchForTestTeardown).Returns("$(SolutionDir)" + TestResources.FailingBatch);
            string setupBatch = MockOptions.Object.SolutionDir + TestResources.SucceedingBatch;
            string teardownBatch = MockOptions.Object.SolutionDir + TestResources.FailingBatch;
            File.Exists(setupBatch).Should().BeTrue();

            RunTests(CreateRunner());

            MockLogger.Verify(l => l.DebugInfo($"Successfully ran {PreparingTestRunner.TestSetup} batch '{setupBatch}'"), Times.Once);
            MockLogger.Verify(l => l.LogWarning($"{PreparingTestRunner.TestTeardown} batch returned exit code 1, executed command: '{teardownBatch}'"), Times.Once);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            _reporter.ReportedTestResults.Single().Outcome.Should().Be(TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BatchFiles_AreRunBeforeAndAfterTestsInSolutionDirWithPlaceholdersReplaced()
        {
            string markerFile = _directory.GetPath("marker.txt");
            string setupBatch = _directory.CreateFile("setup_$(ThreadId).bat".Replace("$(ThreadId)", "7"), $"@echo setup %CD%>>\"{markerFile}\"\r\n@exit 0\r\n");
            string teardownBatch = _directory.CreateFile("teardown.bat", $"@echo teardown %CD%>>\"{markerFile}\"\r\n@exit 0\r\n");
            MockOptions.Setup(o => o.BatchForTestSetup).Returns(_directory.GetPath("setup_$(ThreadId).bat"));
            MockOptions.Setup(o => o.BatchForTestTeardown).Returns(teardownBatch);
            _fakeTeFactory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed")))
            {
                BeforeOutput = e2 => File.AppendAllText(markerFile, "tests" + Environment.NewLine)
            };

            RunTests(CreateRunner(threadId: 7));

            string solutionDir = MockOptions.Object.SolutionDir.TrimEnd('\\');
            File.ReadAllLines(markerFile).Select(l => l.Trim()).Should().Equal($"setup {solutionDir}", "tests", $"teardown {solutionDir}");
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains($"Successfully ran {PreparingTestRunner.TestSetup} batch '{setupBatch}'"))), Times.Once);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BatchFileDoesNotExist_ErrorIsLoggedAndTestsAreRun()
        {
            string batch = _directory.GetPath("DoesNotExist.bat");
            MockOptions.Setup(o => o.BatchForTestSetup).Returns(batch);

            RunTests(CreateRunner());

            MockLogger.Verify(l => l.LogError($"Did not find test setup batch file: {batch}"), Times.Once);
            _reporter.ReportedTestResults.Single().Outcome.Should().Be(TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_NoBatchFiles_OnlyTeIsRun()
        {
            RunTests(CreateRunner());

            _fakeTeFactory.Executions.Should().ContainSingle();
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains("batch"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TestDirectory_IsCreatedForTheRunAndDeletedAfterwards()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TestDirectory=$(TestDir)\"");
            string testDirectory = null;
            bool testDirectoryExistedDuringRun = false;
            _fakeTeFactory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed")))
            {
                BeforeOutput = e2 =>
                {
                    testDirectory = System.Text.RegularExpressions.Regex.Match(e2.Parameters, "/p:\"TestDirectory=([^\"]+)\"").Groups[1].Value;
                    testDirectoryExistedDuringRun = Directory.Exists(testDirectory);
                }
            };

            RunTests(CreateRunner());

            testDirectory.Should().NotBeNullOrEmpty();
            testDirectoryExistedDuringRun.Should().BeTrue();
            Directory.Exists(testDirectory).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_ThreadId_IsPrefixOfLogMessagesAndPaddedToNumberOfThreads()
        {
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(12);
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"Thread=$(ThreadId)\"");

            RunTests(CreateRunner(threadId: 3));

            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.StartsWith("[T03] Execution took "))), Times.Once);
            _fakeTeFactory.Executions.Single().Parameters.Should().Contain("/p:\"Thread=3\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_NoThreadId_NoPrefixAndThreadIdZero()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"Thread=$(ThreadId)\"");

            RunTests(CreateRunner());

            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.StartsWith("Execution took "))), Times.Once);
            _fakeTeFactory.Executions.Single().Parameters.Should().Contain("/p:\"Thread=0\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_ExceptionWhileRunningTests_IsLoggedAsError()
        {
            var reporter = new Mock<ITestFrameworkReporter>();
            reporter.Setup(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>())).Throws(new InvalidOperationException("reporter is broken"));
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, new[] { "A::X" }, new[] { "A::X", "A::Y" }, new[] { "A::X" });

            RunTests(CreateRunner(reporter: reporter.Object), testCases);

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.StartsWith("Exception while running tests: ") && s.Contains("reporter is broken"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_BeforeRunTests_NoTeIsStarted()
        {
            PreparingTestRunner runner = CreateRunner();
            runner.Cancel();

            RunTests(runner);

            _fakeTeFactory.Executions.Should().BeEmpty();
        }

    }

}
