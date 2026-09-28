// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Framework;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Unit tests of <see cref="SequentialTestRunner"/>: TE.exe is replaced by a <see cref="FakeProcessExecutorFactory"/>
    /// (option TeExecutable points to an existing dummy file), test DLLs are dummy paths in a temporary directory.
    /// See <see cref="SequentialTestRunnerIntegrationTests"/> for tests running the sample test DLLs with TE.exe.
    /// </summary>
    [TestClass]
    public class SequentialTestRunnerTests : TestsBase
    {
        private TemporaryDirectory _directory;
        private FakeProcessExecutorFactory _factory;
        private FakeFrameworkReporter _reporter;

        private string TestDll => _directory.GetPath("Fake_taef.dll");
        private string OtherTestDll => _directory.GetPath("Other_taef.dll");
        private string TestDir => _directory.GetPath("TestDir");
        private static string TeExecutable => Path.GetFullPath(TestResources.FakeTeExecutable);

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
            Directory.CreateDirectory(TestDir);
            _factory = new FakeProcessExecutorFactory();
            _reporter = new FakeFrameworkReporter();
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.FakeTeExecutable);
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        private SequentialTestRunner CreateRunner(ITestFrameworkReporter reporter = null, int threadId = 0, string threadName = "")
        {
            return new SequentialTestRunner(threadName, threadId, TestDir, reporter ?? _reporter, MockLogger.Object, MockOptions.Object,
                new SchedulingAnalyzer(MockLogger.Object));
        }

        private void RunTests(IEnumerable<TestCase> testCases, bool isBeingDebugged = false)
        {
            CreateRunner().RunTests(testCases, isBeingDebugged, _factory);
        }

        private TestResult ResultOf(string testName)
            => _reporter.ReportedTestResults.Single(tr => tr.TestCase.FullyQualifiedName == testName);

        private string BaseArguments(string testDll) => $"\"{testDll}\" {TaefConstants.OutputFormatOptions}";

        #region Command lines and settings

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_AllTestsOfTestDll_TeIsRunOnceWithoutSelection()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y", "B::Z");
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("A::Y", "Failed"), ("B::Z", "Skipped")), 1);

            RunTests(testCases);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Kind.Should().Be(RecordedExecution.KindNormal);
            execution.Command.Should().Be(TeExecutable);
            execution.Parameters.Should().Be(BaseArguments(TestDll));
            execution.WorkingDir.Should().Be(_directory.Path, "the default working directory is $(TestDllDir)");
            execution.PathExtension.Should().BeNullOrEmpty();
            execution.EnvironmentVariables.Should().BeEmpty();
            execution.IsOutputRequested.Should().BeTrue();
            execution.PrintTestOutput.Should().BeFalse();

            _reporter.ReportedTestCasesStarted.Select(tc => tc.FullyQualifiedName).Should().Equal("A::X", "A::Y", "B::Z");
            _reporter.ReportedTestResults.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}")
                .Should().Equal("A::X Passed", "A::Y Failed", "B::Z Skipped");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf("A::Y"), "Verify: AreEqual(1, 2) - Values (1, 2)");
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_SubsetOfTests_SelectionIsPassed()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, new[] { "A::X", "B::Z" }, new[] { "A::X", "A::Y", "B::Z" });
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("B::Z", "Passed")));

            RunTests(testCases);

            _factory.Executions.Single().Parameters.Should().Be(BaseArguments(TestDll) + " /select:\"@Name='A::X' or (@Name='B::*' and not @Name='B::*::*')\"");
            _reporter.ReportedTestResults.Should().HaveCount(2).And.OnlyContain(tr => tr.Outcome == TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TwoTestDlls_TeIsRunForEachOfThem()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X")
                .Concat(RunnerTestData.CreateTestCases(OtherTestDll, "B::Y"))
                .ToList();
            _factory.Behavior = e => new FakeProcessBehavior(e.Parameters.Contains(TestDll)
                ? RunnerTestData.CreateTeOutput(("A::X", "Passed"))
                : RunnerTestData.CreateTeOutput(("B::Y", "Failed")), 0);

            RunTests(testCases);

            _factory.Executions.Select(e => e.Parameters).Should().Equal(BaseArguments(TestDll), BaseArguments(OtherTestDll));
            _reporter.ReportedTestResults.Select(tr => $"{tr.TestCase.Source} {tr.TestCase.FullyQualifiedName} {tr.Outcome}")
                .Should().Equal($"{TestDll} A::X Passed", $"{OtherTestDll} B::Y Failed");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_PlaceholdersInSettings_AreReplaced()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TestDirectory=$(TestDir)\" /p:\"ThreadId=$(ThreadId)\" /p:\"Dll=$(TestDll)\"");
            MockOptions.Setup(o => o.WorkingDir).Returns("$(SolutionDir)");
            MockOptions.Setup(o => o.EnvironmentVariables).Returns("MYENVVAR=MyValue" + SettingsWrapperSeparator + "MYDIR=$(TestDir)");
            MockOptions.Setup(o => o.PathExtension).Returns("$(TestDllDir)\\dll");
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X");

            CreateRunner(threadId: 3).RunTests(testCases, false, _factory);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Parameters.Should().Be($"\"{TestDll}\" /p:\"TestDirectory={TestDir}\" /p:\"ThreadId=3\" /p:\"Dll={TestDll}\" {TaefConstants.OutputFormatOptions}");
            execution.WorkingDir.Should().Be(MockOptions.Object.SolutionDir);
            execution.EnvironmentVariables.Should().Contain("MYENVVAR", "MyValue").And.Contain("MYDIR", TestDir);
            execution.PathExtension.Should().Be(_directory.Path + "\\dll");
        }

        private const string SettingsWrapperSeparator = Settings.SettingsWrapper.TraitsRegexesPairSeparator;

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_PathExtensionAndPathVariable_PathVariableIsIgnoredWithWarning()
        {
            MockOptions.Setup(o => o.EnvironmentVariables).Returns("PATH=C:\\foo");
            MockOptions.Setup(o => o.PathExtension).Returns("C:\\bar");

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"));

            RecordedExecution execution = _factory.Executions.Single();
            execution.PathExtension.Should().Be("C:\\bar");
            execution.EnvironmentVariables.Should().NotContainKey("PATH");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Both a path extension and a PATH environment variable have been provided"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_PrintTestOutput_OutputIsPrintedUnlessTestsAreRunInParallel()
        {
            MockOptions.Setup(o => o.PrintTestOutput).Returns(true);
            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"));
            _factory.Executions.Last().PrintTestOutput.Should().BeTrue();

            MockOptions.Setup(o => o.ParallelTestExecution).Returns(true);
            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"));
            _factory.Executions.Last().PrintTestOutput.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_ManyTests_SeveralTeInvocations()
        {
            List<string> allNames = Enumerable.Range(0, 1000)
                .SelectMany(i => new[] { $"SomeNamespace::SomeRatherLongTestClassName{i:D4}::SomeRatherLongTestMethodName", $"SomeNamespace::SomeRatherLongTestClassName{i:D4}::Other" })
                .ToList();
            List<string> namesToRun = allNames.Where((n, i) => i % 2 == 0).ToList();
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, namesToRun, allNames);
            // each invocation "runs" all tests, the parser picks the ones of its command line
            List<string> output = RunnerTestData.CreateTeOutput(namesToRun.Select(n => (n, "Passed")).ToArray());
            _factory.Behavior = e => new FakeProcessBehavior(output);

            RunTests(testCases);

            IList<RecordedExecution> executions = _factory.Executions;
            executions.Should().HaveCount(3);
            executions.Should().OnlyContain(e => e.CommandLineLength <= TaefConstants.MaxCommandLength);
            _reporter.ReportedTestResults.Select(tr => tr.TestCase.FullyQualifiedName).OrderBy(n => n, StringComparer.Ordinal)
                .Should().Equal(namesToRun.OrderBy(n => n, StringComparer.Ordinal));
            _reporter.ReportedTestResults.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Passed);
        }

        #endregion

        #region Ignored tests

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_IgnoredTests_AreReportedAsSkippedWithoutBeingRun()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, new[] { "A::X", "A::Y", "B::Z" }, new[] { "A::X", "A::Y", "B::Z" }, new[] { "A::Y" });
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("B::Z", "Passed")));

            RunTests(testCases);

            _factory.Executions.Single().Parameters.Should().Be(BaseArguments(TestDll), "TE.exe does not run ignored tests");
            TestResult ignored = ResultOf("A::Y");
            ignored.Outcome.Should().Be(TestOutcome.Skipped);
            ignored.ErrorMessage.Should().Be(TaefConstants.IgnoredTestMessage);
            ignored.ErrorMessage.Should().Be("Test is marked Ignore=true - enable option 'Also run ignored tests' to run it.");
            _reporter.ReportedTestResults.First().Should().BeSameAs(ignored, "ignored tests are reported before TE.exe is started");
            _reporter.ReportedTestCasesStarted.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo("A::X", "A::Y", "B::Z");
            _reporter.ReportedTestResults.Should().HaveCount(3);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_OnlyIgnoredTests_TeIsNotStarted()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, new[] { "A::Y" }, new[] { "A::X", "A::Y" }, new[] { "A::Y" });

            RunTests(testCases);

            _factory.Executions.Should().BeEmpty();
            ResultOf("A::Y").Outcome.Should().Be(TestOutcome.Skipped);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_IgnoredTestsWithOptionRunIgnoredTests_AreRun()
        {
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, new[] { "A::Y" }, new[] { "A::X", "A::Y" }, new[] { "A::Y" });
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::Y", "Failed")));

            RunTests(testCases);

            _factory.Executions.Single().Parameters.Should().Be(BaseArguments(TestDll) + " /runIgnoredTests /select:\"@Name='A::Y'\"");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf("A::Y"));
        }

        #endregion

        #region Missing results, crashes, exit codes

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TestNotReportedByTe_IsReportedAccordingToMissingTestsReportMode()
        {
            var expectedOutcomes = new Dictionary<MissingTestsReportMode, TestOutcome?>
            {
                { MissingTestsReportMode.ReportAsNotFound, TestOutcome.NotFound },
                { MissingTestsReportMode.ReportAsFailed, TestOutcome.Failed },
                { MissingTestsReportMode.ReportAsSkipped, TestOutcome.Skipped },
                { MissingTestsReportMode.DoNotReport, null }
            };
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed")));

            foreach (KeyValuePair<MissingTestsReportMode, TestOutcome?> pair in expectedOutcomes)
            {
                _reporter = new FakeFrameworkReporter();
                MockOptions.Setup(o => o.MissingTestsReportMode).Returns(pair.Key);

                RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y"));

                ResultOf("A::X").Outcome.Should().Be(TestOutcome.Passed);
                TestResult missing = _reporter.ReportedTestResults.SingleOrDefault(tr => tr.TestCase.FullyQualifiedName == "A::Y");
                if (pair.Value.HasValue)
                {
                    missing.Should().NotBeNull(pair.Key.ToString());
                    missing.Outcome.Should().Be(pair.Value.Value);
                    missing.ErrorMessage.Should().StartWith("Test case has not been run - possible causes:");
                }
                else
                {
                    missing.Should().BeNull(pair.Key.ToString());
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TeCrashesWhileRunningTest_CrashedTestFailsAndRemainingTestsAreSkippedAsCrashSuspects()
        {
            var testCases = new List<TestCase>
            {
                new TestCase("A::X", TestDll, "A::X", @"C:\src\a.cpp", 10),
                new TestCase("A::Y", TestDll, "A::Y", @"C:\src\a.cpp", 20),
                new TestCase("B::Z", TestDll, "B::Z", @"C:\src\b.cpp", 30)
            };
            testCases.ForEach(tc => tc.Properties.Add(new Model.TestCaseMetaDataProperty(tc.FullyQualifiedName == "B::Z" ? 1 : 2, 3)));
            List<string> output = RunnerTestData.CreateTeOutput(("A::X", "Passed"));
            output.RemoveAt(output.Count - 1);
            output.AddRange(new[] { "", "StartGroup: A::Y", "About to crash" });
            _factory.Behavior = e => new FakeProcessBehavior(output, unchecked((int)0xC0000005));

            RunTests(testCases);

            ResultOf("A::X").Outcome.Should().Be(TestOutcome.Passed);
            TestResultAssertions.AssertTestResultIsFailure(ResultOf("A::Y"),
                StreamingTaefOutputParser.CrashText + " (TE.exe terminated with exit code 0xC0000005)\nTest output:\n\nAbout to crash");
            TestResult suspect = ResultOf("B::Z");
            suspect.Outcome.Should().Be(TestOutcome.Skipped);
            suspect.ErrorMessage.Should().Be("reason is probably a crash of test A::Y");
            suspect.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("crash suspect", @"C:\src\a.cpp", "20"));
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.Contains("TE.exe terminated abnormally with exit code 0xC0000005 while running test 'A::Y'"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TeTerminatesAbnormallyOutsideOfTests_TestsWithoutResultFailAndErrorIsLogged()
        {
            // e.g. a class setup crashing TE.exe while running in process
            List<string> output = RunnerTestData.CreateTeOutput(("A::X", "Passed"));
            output.RemoveAt(output.Count - 1);
            output.Add("B class setup about to crash");
            _factory.Behavior = e => new FakeProcessBehavior(output, unchecked((int)0xC0000005));

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X", "B::Y", "B::Z"));

            ResultOf("A::X").Outcome.Should().Be(TestOutcome.Passed);
            foreach (string testName in new[] { "B::Y", "B::Z" })
            {
                TestResultAssertions.AssertTestResultIsFailure(ResultOf(testName),
                    StreamingTaefOutputParser.TerminatedOutsideOfTestText + " with exit code 0xC0000005 outside of a test (probably in a setup or cleanup fixture running in process)" +
                    "\nLast output of TE.exe:\n\nB class setup about to crash");
            }
            _reporter.ReportedTestResults.Should().HaveCount(3, "no further results (e.g. 'not found') must be reported");
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains($"Test DLL '{TestDll}': TE.exe terminated abnormally with exit code 0xC0000005")
                                                                 && s.Contains("2 test(s) have not been run and are reported as failed"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("terminated abnormally"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_TeCrashesWhileRunningTestNotPartOfRun_CrashIsAttributedToItsGroup()
        {
            // e.g. a test TE.exe runs although it has not been selected (as happened for data rows with wildcards in their names)
            List<string> output = RunnerTestData.CreateTeOutput(("W::Rows#c*", "Passed"));
            output.RemoveAt(output.Count - 1);
            output.AddRange(new[] { "", "StartGroup: W::Rows#cboom", "about to crash" });
            _factory.Behavior = e => new FakeProcessBehavior(output, unchecked((int)0xC0000005));

            RunTests(RunnerTestData.CreateTestCases(TestDll, new[] { "W::Rows#c*", "W::Rows#zz" }, new[] { "W::Rows#c*", "W::Rows#cboom", "W::Rows#zz", "W::After" }));

            ResultOf("W::Rows#c*").Outcome.Should().Be(TestOutcome.Passed);
            TestResultAssertions.AssertTestResultIsFailure(ResultOf("W::Rows#zz"),
                StreamingTaefOutputParser.TerminatedOutsideOfTestText + " with exit code 0xC0000005 outside of a test, within group 'W::Rows#cboom' " +
                "(a log group of a setup or cleanup fixture running in process, or a test which is not part of the tests run)" +
                "\nLast output of TE.exe:\n\nStartGroup: W::Rows#cboom\nabout to crash");
            _reporter.ReportedTestResults.Should().HaveCount(2);
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("1 test(s) have not been run"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_OutputWithoutSummary_AbnormalTerminationIsLogged()
        {
            List<string> output = RunnerTestData.CreateTeOutput(("A::X", "Passed"));
            output.RemoveAt(output.Count - 1);
            _factory.Behavior = e => new FakeProcessBehavior(output, 42);

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"));

            ResultOf("A::X").Outcome.Should().Be(TestOutcome.Passed);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.EndsWith("TE.exe terminated abnormally with exit code 0x0000002A"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_SpecialExitCodes_ErrorWithExplanationIsLogged()
        {
            var outputs = new Dictionary<int, string[]>
            {
                { TaefConstants.ExitCodeStartupError, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorSelectSyntaxError) },
                { TaefConstants.ExitCodeNoTestsExecuted, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorNoMatchingTests) },
                { TaefConstants.ExitCodeNoTestFiles, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorNoTestFiles) },
                {
                    TaefConstants.ExitCodeLoggerInitializationFailed, new[]
                    {
                        "Test Authoring and Execution Framework v10.104k for x64",
                        "",
                        "[HRESULT 0x8007007E] Failed to initialize the logger. (Failed to delay-load WTTLog.dll.)"
                    }
                }
            };

            foreach (KeyValuePair<int, string[]> pair in outputs)
            {
                MockLogger.Invocations.Clear();
                _reporter = new FakeFrameworkReporter();
                _factory.Behavior = e => new FakeProcessBehavior(pair.Value, pair.Key);

                RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"));

                string description = TaefConstants.GetExitCodeDescription(pair.Key);
                MockLogger.Verify(l => l.LogError(It.Is<string>(s =>
                    s.StartsWith($"Test DLL '{TestDll}': TE.exe returned exit code 0x{pair.Key:X8}: {description}."))), Times.Once, description);
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("terminated abnormally"))), Times.Never);
                ResultOf("A::X").Outcome.Should().Be(TestOutcome.NotFound);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_ExecutorThrows_ErrorIsLoggedAndTestsAreReportedAsNotFound()
        {
            _factory.Behavior = e => new FakeProcessBehavior { Exception = new InvalidOperationException("The system cannot find the file specified") };

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"));

            MockLogger.Verify(l => l.LogError($"Failed to run '{TeExecutable}': The system cannot find the file specified"), Times.Once);
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("execute the following command") && s.Contains($"\"{TeExecutable}\" {BaseArguments(TestDll)}"))), Times.Once);
            ResultOf("A::X").Outcome.Should().Be(TestOutcome.NotFound);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogExecutionError_LogsExceptionAndHowToReproduceTheProblem()
        {
            var logger = new Mock<ILogger>();
            var exception = new AggregateException(new InvalidOperationException("inner problem"));

            SequentialTestRunner.LogExecutionError(logger.Object, @"C:\te\TE.exe", @"C:\work", "\"C:\\a.dll\" /x", exception, "[T1] ");

            logger.Verify(l => l.LogError($"[T1] Failed to run 'C:\\te\\TE.exe': {exception.Message}"), Times.Once);
            logger.Verify(l => l.DebugError(It.Is<string>(s => s.StartsWith("[T1] Exception:") && s.Contains("inner problem"))), Times.Once);
            logger.Verify(l => l.LogError("[T1] " + Strings.Instance.TroubleShootingLink), Times.Once);
            logger.Verify(l => l.LogError(It.Is<string>(s =>
                s.StartsWith("[T1] In particular: launch a command prompt, change into directory 'C:\\work'")
                && s.EndsWith("\"C:\\te\\TE.exe\" \"C:\\a.dll\" /x"))), Times.Once);
        }

        #endregion

        #region Durations

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_Results_DurationsOfPassedAndFailedTestsAreRecorded()
        {
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("A::Y", "Failed"), ("B::Z", "Skipped")));
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y", "B::Z");

            RunTests(testCases);

            File.Exists(TestDll + TaefConstants.DurationsExtension).Should().BeTrue();
            IDictionary<TestCase, int> durations = new TestDurationSerializer().ReadTestDurations(testCases);
            durations.Keys.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo("A::X", "A::Y");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebugged_DurationsAreNotRecorded()
        {
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed")));

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"), isBeingDebugged: true);

            ResultOf("A::X").Outcome.Should().Be(TestOutcome.Passed);
            File.Exists(TestDll + TaefConstants.DurationsExtension).Should().BeFalse();
        }

        #endregion

        #region Debugging

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebuggedWithNativeDebugger_TestsAreRunInProcessWithDebugger()
        {
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.Native);
            MockOptions.Setup(o => o.BreakOnError).Returns(true);
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Failed")));

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"), isBeingDebugged: true);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Kind.Should().Be(RecordedExecution.KindNativeDebugger);
            execution.DebuggerEngine.Should().Be(DebuggerEngine.Native);
            execution.IsOutputRequested.Should().BeTrue();
            execution.Parameters.Should().Be(BaseArguments(TestDll) + " /breakOnError /inproc /disableTimeouts");
            ResultOf("A::X").Outcome.Should().Be(TestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebuggedWithManagedAndNativeDebugger_DebuggerEngineIsPassed()
        {
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.ManagedAndNative);

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"), isBeingDebugged: true);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Kind.Should().Be(RecordedExecution.KindNativeDebugger);
            execution.DebuggerEngine.Should().Be(DebuggerEngine.ManagedAndNative);
        }

        private static string GetWttLogFile(string parameters)
        {
            System.Text.RegularExpressions.Match match = Regex.Match(parameters, "/logFile:\"([^\"]+)\"");
            match.Success.Should().BeTrue(parameters);
            return match.Groups[1].Value;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebuggedWithVsTestFramework_ResultsAreReadFromWttLog()
        {
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            string wttLogFile = null;
            _factory.Behavior = e => new FakeProcessBehavior
            {
                // TE.exe writes the WTT log while the output is not available
                Output = RunnerTestData.CreateTeOutput(("A::X", "Failed"), ("A::Y", "Failed")),
                BeforeOutput = e2 =>
                {
                    wttLogFile = GetWttLogFile(e2.Parameters);
                    File.WriteAllText(wttLogFile, @"<?xml version=""1.0"" encoding=""UTF-16"" ?>
<WTT-Logger>
<StartTest Title=""A::X"" TUID="""" ></StartTest>
<EndTest Title=""A::X"" TUID="""" Result=""Pass"" Repro="""" ></EndTest>
<StartTest Title=""A::Y"" TUID="""" ></StartTest>
<Error File=""C:\src\a.cpp"" Line=""42"" ErrCode=""0x0"" ErrType="""" ErrorText=""Error 0x00000000"" UserText=""AreEqual(1, 2) - Values (1, 2)"" >
<Data><WexContext><![CDATA[Verify]]></WexContext></Data>
</Error>
<EndTest Title=""A::Y"" TUID="""" Result=""Fail"" Repro="""" ></EndTest>
</WTT-Logger>
", Encoding.Unicode);
                }
            };

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y"), isBeingDebugged: true);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Kind.Should().Be(RecordedExecution.KindFrameworkDebugger);
            execution.IsOutputRequested.Should().BeFalse("the VsTest framework's debugger does not provide the output");
            execution.PrintTestOutput.Should().BeFalse();
            execution.Parameters.Should().Be(BaseArguments(TestDll) + $" /inproc /disableTimeouts /enableWttLogging /logFile:\"{wttLogFile}\"");
            Path.GetDirectoryName(wttLogFile).Should().Be(Path.GetTempPath().TrimEnd('\\'));

            TestResultAssertions.AssertTestResultIsPassed(ResultOf("A::X"));
            TestResultAssertions.AssertTestResultIsFailure(ResultOf("A::Y"), "Verify: AreEqual(1, 2) - Values (1, 2)");
            ResultOf("A::Y").ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("a.cpp:42", @"C:\src\a.cpp", "42"));
            File.Exists(wttLogFile).Should().BeFalse("the WTT log is deleted after it has been read");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebuggedWithVsTestFrameworkAndTeCrashed_ResultsAreReadFromTraceFileWhichIsDeleted()
        {
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            string wttLogFile = null;
            _factory.Behavior = e => new FakeProcessBehavior
            {
                ExitCode = unchecked((int)0xC0000005),
                // TE.exe crashed while A::Y was running: only the file written while the tests were running exists
                BeforeOutput = e2 =>
                {
                    wttLogFile = GetWttLogFile(e2.Parameters);
                    File.WriteAllText(wttLogFile + WttLogParser.TraceFileExtension, @"<StartTest Title=""A::X"" TUID="""" ></StartTest>
<EndTest Title=""A::X"" TUID="""" Result=""Pass"" Repro="""" ></EndTest>
<StartTest Title=""A::Y"" TUID="""" ></StartTest>
<Msg UserText=""about to crash"" ><Data></Data></Msg>
<Msg UserText=""trunc", Encoding.Unicode);
                }
            };

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y"), isBeingDebugged: true);

            TestResultAssertions.AssertTestResultIsPassed(ResultOf("A::X"));
            ResultOf("A::Y").Outcome.Should().Be(TestOutcome.Failed);
            ResultOf("A::Y").ErrorMessage.Should().StartWith(StreamingTaefOutputParser.CrashText)
                .And.Contain("about to crash", "the output of the crashed test up to the crash is part of the message");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("did not write a WTT log"))), Times.Never);
            File.Exists(wttLogFile).Should().BeFalse();
            File.Exists(wttLogFile + WttLogParser.TraceFileExtension).Should().BeFalse("the file left behind by the crashed TE.exe is deleted, too");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebuggedWithVsTestFrameworkAndNoWttLog_WarningAndMissingResults()
        {
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            _factory.Behavior = e => new FakeProcessBehavior(new string[0], unchecked((int)0xC0000005));

            RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"), isBeingDebugged: true);

            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.Contains("TE.exe (exit code 0xC0000005) did not write a WTT log, so no test results are available")
                && s.Contains(Settings.SettingsWrapper.OptionDebuggerKind))), Times.Once);
            ResultOf("A::X").Outcome.Should().Be(TestOutcome.NotFound);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_BeingDebuggedWithVsTestFrameworkAndNonAsciiTempDir_WttLogPathPassedToTeIsAscii()
        {
            // e.g. %TEMP% of a user named Jürgen: TE.exe would write the WTT log to a different (mangled) folder
            string nonAsciiTempDir = _directory.GetPath("Temp_Jürgen_名前");
            Directory.CreateDirectory(nonAsciiTempDir);
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            string wttLogArgument = null;
            _factory.Behavior = e => new FakeProcessBehavior
            {
                BeforeOutput = e2 =>
                {
                    // TE.exe resolves a relative log file against its working directory
                    wttLogArgument = GetWttLogFile(e2.Parameters);
                    string wttLogFile = Path.IsPathRooted(wttLogArgument) ? wttLogArgument : Path.Combine(e2.WorkingDir, wttLogArgument);
                    File.WriteAllText(wttLogFile, @"<?xml version=""1.0"" encoding=""UTF-16"" ?>
<WTT-Logger>
<StartTest Title=""A::X"" TUID="""" ></StartTest>
<EndTest Title=""A::X"" TUID="""" Result=""Pass"" Repro="""" ></EndTest>
</WTT-Logger>
", Encoding.Unicode);
                }
            };

            string originalTmp = Environment.GetEnvironmentVariable("TMP");
            string originalTemp = Environment.GetEnvironmentVariable("TEMP");
            try
            {
                Environment.SetEnvironmentVariable("TMP", nonAsciiTempDir);
                Environment.SetEnvironmentVariable("TEMP", nonAsciiTempDir);
                Path.GetTempPath().Should().StartWith(nonAsciiTempDir);

                RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"), isBeingDebugged: true);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", originalTmp);
                Environment.SetEnvironmentVariable("TEMP", originalTemp);
            }

            wttLogArgument.Should().NotBeNull();
            TeArguments.IsAscii(wttLogArgument).Should().BeTrue(wttLogArgument);
            TestResultAssertions.AssertTestResultIsPassed(ResultOf("A::X"));
            Directory.GetFiles(nonAsciiTempDir).Should().BeEmpty("the WTT log is deleted after it has been read");
            Directory.GetFiles(_directory.Path, "*.wtl").Should().BeEmpty("the WTT log is deleted after it has been read");
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Test DLLs with non-ASCII paths

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_NonAsciiTestDllPath_TestDllIsPassedAsAsciiPath()
        {
            // TE.exe can not open test DLLs whose path contains non-ASCII characters
            string testDll = _directory.CreateFile(Path.Combine("dirü名", "Fake_taef.dll"));
            List<TestCase> testCases = RunnerTestData.CreateTestCases(testDll, new[] { "A::X" }, new[] { "A::X", "A::Y" });
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed")));

            // the default working directory is the folder of the test DLL: the file name is passed
            RunTests(testCases);

            RecordedExecution execution = _factory.Executions.Single();
            execution.WorkingDir.Should().Be(Path.GetDirectoryName(testDll));
            execution.Parameters.Should().Be($"\"Fake_taef.dll\" {TaefConstants.OutputFormatOptions} /select:\"@Name='A::X'\"");
            ResultOf("A::X").Outcome.Should().Be(TestOutcome.Passed);
            ResultOf("A::X").TestCase.Source.Should().Be(testDll);

            // another working directory: the 8.3 short path is passed (if 8.3 names are enabled for the volume)
            MockOptions.Setup(o => o.WorkingDir).Returns("$(SolutionDir)");
            RunTests(testCases);

            string shortPath = TeArguments.GetShortPath(testDll);
            if (shortPath == null || !TeArguments.IsAscii(shortPath))
                Assert.Inconclusive($"8.3 file names seem to be disabled for the volume of '{testDll}'");
            _factory.Executions.Last().Parameters.Should().StartWith($"\"{shortPath}\" ");
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Cancellation

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_WhileTeIsRunningWithoutKillingProcesses_NoFurtherTeIsStartedAndProcessIsNotKilled()
        {
            DoCancelWhileTeIsRunning(killProcessesOnCancel: false);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_WhileTeIsRunningAndKillingProcesses_NoFurtherTeIsStartedAndProcessIsKilled()
        {
            DoCancelWhileTeIsRunning(killProcessesOnCancel: true);
        }

        private void DoCancelWhileTeIsRunning(bool killProcessesOnCancel)
        {
            MockOptions.Setup(o => o.KillProcessesOnCancel).Returns(killProcessesOnCancel);
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X")
                .Concat(RunnerTestData.CreateTestCases(OtherTestDll, "B::Y"))
                .ToList();
            SequentialTestRunner runner = CreateRunner();
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("B::Y", "Passed")))
            {
                AfterOutput = e2 => runner.Cancel()
            };

            runner.RunTests(testCases, false, _factory);

            RecordedExecution execution = _factory.Executions.Single();
            execution.Parameters.Should().StartWith($"\"{TestDll}\"");
            execution.IsCanceled.Should().Be(killProcessesOnCancel);
            _reporter.ReportedTestResults.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal("A::X");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunTests_ReporterCancelsTestRun_NoFurtherTeIsStarted()
        {
            var reporter = new Mock<ITestFrameworkReporter>();
            reporter.Setup(r => r.ReportTestResults(It.IsAny<IEnumerable<TestResult>>()))
                .Throws(new TestRunCanceledException("Test run has been canceled", new Exception("canceled by VS")));
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X")
                .Concat(RunnerTestData.CreateTestCases(OtherTestDll, "B::Y"))
                .ToList();
            _factory.Behavior = e => new FakeProcessBehavior(RunnerTestData.CreateTeOutput(("A::X", "Passed"), ("B::Y", "Passed")));

            CreateRunner(reporter.Object).RunTests(testCases, false, _factory);

            _factory.Executions.Should().ContainSingle();
            reporter.Verify(r => r.ReportTestResults(It.Is<IEnumerable<TestResult>>(trs => trs.Any())), Times.Once);
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains("Execution has been canceled: canceled by VS"))), Times.AtLeastOnce);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_BeforeRunTests_NoTeIsStarted()
        {
            SequentialTestRunner runner = CreateRunner();
            runner.Cancel();

            runner.RunTests(RunnerTestData.CreateTestCases(TestDll, "A::X"), false, _factory);

            _factory.Executions.Should().BeEmpty();
            _reporter.ReportedTestResults.Should().BeEmpty();
        }

        #endregion

    }

}
