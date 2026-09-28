// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Scheduling;
using TaefTestAdapter.Settings;
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
    /// Tests of <see cref="SequentialTestRunner"/> running the sample test DLLs with TE.exe. The sample DLLs are copied to
    /// temporary directories (test runs write <c>*.taef.testdurations</c> files next to the test DLLs).
    /// </summary>
    [TestClass]
    public class SequentialTestRunnerIntegrationTests : TestsBase
    {
        private TemporaryDirectory _testDirectory;
        private FakeFrameworkReporter _reporter;
        private RecordingProcessExecutorFactory _factory;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _testDirectory = new TemporaryDirectory();
            _reporter = new FakeFrameworkReporter();
            _factory = new RecordingProcessExecutorFactory(new ProcessExecutorFactory());
        }

        [TestCleanup]
        public override void TearDown()
        {
            _testDirectory.Dispose();
            base.TearDown();
        }

        private SequentialTestRunner CreateRunner(SettingsWrapper settings = null, ITestFrameworkReporter reporter = null)
        {
            return new SequentialTestRunner("", 0, _testDirectory.Path, reporter ?? _reporter, MockLogger.Object, settings ?? MockOptions.Object,
                new SchedulingAnalyzer(MockLogger.Object));
        }

        private int Count(TestOutcome outcome) => _reporter.ReportedTestResults.Count(tr => tr.Outcome == outcome);

        private TestResult ResultOf(string testName)
            => _reporter.ReportedTestResults.Single(tr => tr.TestCase.FullyQualifiedName == testName);

        private void VerifyNoErrorsLogged()
        {
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        #region Tests_taef.dll

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_AllTestsOfSampleTestsWithSampleSettings_OutcomesAsDocumented()
        {
            SetupSampleTestsSettings();
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_DebugX86))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);
                testCases.Should().HaveCount(TestResources.NrOfTests);

                CreateRunner().RunTests(testCases, false, _factory);

                RecordedExecution execution = _factory.Executions.Single();
                execution.Command.Should().Be(TestResources.TeExecutableX86);
                execution.Parameters.Should().Be($"\"{sample.TestDll}\" /p:\"TestDirectory={_testDirectory.Path}\" {TaefConstants.OutputFormatOptions}");
                execution.WorkingDir.Should().Be(MockOptions.Object.SolutionDir);

                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfTests);
                _reporter.ReportedTestResults.Select(tr => tr.TestCase).Should().OnlyHaveUniqueItems();
                Count(TestOutcome.Passed).Should().Be(TestResources.NrOfTestsReportedAsPassed);
                Count(TestOutcome.Failed).Should().Be(TestResources.NrOfTestsReportedAsFailed);
                Count(TestOutcome.Skipped).Should().Be(TestResources.NrOfTestsReportedAsSkipped);
                Count(TestOutcome.None).Should().Be(TestResources.NrOfTestsReportedAsNone);
                _reporter.ReportedTestCasesStarted.Should().HaveCount(TestResources.NrOfTests);

                ResultOf(TestResources.TestNames.IgnoredPassing).ErrorMessage.Should().Be(TaefConstants.IgnoredTestMessage);
                ResultOf(TestResources.TestNames.BlockedByTest).ErrorMessage.Should().Be("Blocked: Blocked by the test: a prerequisite is missing");
                TestResult failed = ResultOf(TestResources.TestNames.TestMathAddFails);
                TestResultAssertions.AssertTestResultIsFailure(failed, "Verify: AreEqual(1000, Add(10, 10)) - Values (1000, 20)");
                failed.ErrorStackTrace.Should().MatchRegex(@"^at TaefSamples::TestMath::AddFails in .*\\SampleTests\\Tests\\BasicTests\.cpp:line \d+\r\n$");
                ResultOf("TaefSamples::Ümlautß::Träits").Output.Should().Be("Ümlaut output: äöü ÄÖÜ ß 名前 ✓");

                IDictionary<TestCase, int> durations = new TestDurationSerializer().ReadTestDurations(testCases);
                durations.Should().HaveCount(TestResources.NrOfTestsReportedAsPassed + TestResources.NrOfTestsReportedAsFailed);

                VerifyNoErrorsLogged();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TestsNeedingSampleSettingsWithoutSettings_TestsFail()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_DebugX64))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll)
                    .Where(tc => tc.FullyQualifiedName == TestResources.TestNames.TestDirectoryIsSet
                                 || tc.FullyQualifiedName == TestResources.TestNames.WorkingDirIsSolutionDirectory
                                 || tc.FullyQualifiedName == TestResources.TestNames.EnvironmentVariableIsSet)
                    .ToList();
                testCases.Should().HaveCount(TestResources.NrOfTestsNeedingSampleSettings);

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().Command.Should().Be(TestResources.TeExecutableX64);
                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfTestsNeedingSampleSettings);
                _reporter.ReportedTestResults.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Failed);
                ResultOf(TestResources.TestNames.EnvironmentVariableIsSet).ErrorMessage
                    .Should().Contain("environment variable MYENVVAR is not set");
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TestsNeedingSampleSettingsWithSettings_TestsPass()
        {
            SetupSampleTestsSettings();
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_DebugX64))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll)
                    .Where(tc => tc.FullyQualifiedName == TestResources.TestNames.TestDirectoryIsSet
                                 || tc.FullyQualifiedName == TestResources.TestNames.WorkingDirIsSolutionDirectory
                                 || tc.FullyQualifiedName == TestResources.TestNames.EnvironmentVariableIsSet)
                    .ToList();

                CreateRunner().RunTests(testCases, false, _factory);

                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfTestsNeedingSampleSettings);
                _reporter.ReportedTestResults.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Passed);
                VerifyNoErrorsLogged();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_WorkingDirNotSet_TestFails()
        {
            DoRunWorkingDirTest(null, null, TestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_WorkingDirSetForSolution_TestPasses()
        {
            DoRunWorkingDirTest(PlaceholderReplacer.SolutionDirPlaceholder, null, TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_WorkingDirSetForProject_TestPasses()
        {
            DoRunWorkingDirTest("foo", PlaceholderReplacer.SolutionDirPlaceholder, TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_EnvironmentVariableSetForSolution_TestPasses()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_ReleaseX86))
            {
                TestCase testCase = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll)
                    .Single(tc => tc.FullyQualifiedName == TestResources.TestNames.EnvironmentVariableIsSet);
                SettingsWrapper settings = CreateSettings(null, null, "MYENVVAR=MyValue");

                CreateRunner(settings).RunTests(testCase.Yield(), false, _factory);

                _reporter.ReportedTestResults.Single().Outcome.Should().Be(TestOutcome.Passed);
                VerifyNoErrorsLogged();
            }
        }

        private void DoRunWorkingDirTest(string solutionWorkingDir, string projectWorkingDir, TestOutcome expectedOutcome)
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_ReleaseX86))
            {
                TestCase testCase = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll)
                    .Single(tc => tc.FullyQualifiedName == TestResources.TestNames.WorkingDirIsSolutionDirectory);
                SettingsWrapper settings = CreateSettings(solutionWorkingDir, projectWorkingDir);

                CreateRunner(settings).RunTests(testCase.Yield(), false, _factory);

                _reporter.ReportedTestResults.Single().Outcome.Should().Be(expectedOutcome);
                _factory.Executions.Single().WorkingDir.Should().Be(
                    solutionWorkingDir == null ? sample.Directory : Path.GetFullPath(TestResources.SampleTestsSolutionDir));
                VerifyNoErrorsLogged();
            }
        }

        /// <returns>Real settings with the given solution and project (any test DLL) settings.</returns>
        private SettingsWrapper CreateSettings(string solutionWorkingDir, string projectWorkingDir, string environmentVariables = null)
        {
            var mockContainer = new Mock<ITaefTestAdapterSettingsContainer>();

            var solutionSettings = new RunSettings { WorkingDir = solutionWorkingDir, EnvironmentVariables = environmentVariables };
            mockContainer.Setup(c => c.SolutionSettings).Returns(solutionSettings);
            if (projectWorkingDir != null)
            {
                mockContainer
                    .Setup(c => c.GetSettingsForTestDll(It.IsAny<string>()))
                    .Returns(new RunSettings { ProjectRegex = ".*", WorkingDir = projectWorkingDir });
            }

            return new SettingsWrapper(mockContainer.Object, Path.GetFullPath(TestResources.SampleTestsSolutionDir))
            {
                RegexTraitParser = new RegexTraitParser(MockLogger.Object),
                EnvironmentVariablesParser = new EnvironmentVariablesParser(MockLogger.Object),
                HelperFilesCache = new HelperFilesCache(MockLogger.Object)
            };
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_IgnoredTests_AreOnlyRunWithOptionRunIgnoredTests()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_DebugX64))
            {
                List<TestCase> ignoredTestCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll).Where(TaefConstants.IsIgnored).ToList();
                ignoredTestCases.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(
                    TestResources.TestNames.IgnoredTest, TestResources.TestNames.IgnoredPassing, TestResources.TestNames.IgnoredFailing);

                CreateRunner().RunTests(ignoredTestCases, false, _factory);

                _factory.Executions.Should().BeEmpty();
                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfIgnoredTests);
                _reporter.ReportedTestResults.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Skipped && tr.ErrorMessage == TaefConstants.IgnoredTestMessage);

                MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);
                _reporter = new FakeFrameworkReporter();

                CreateRunner().RunTests(ignoredTestCases, false, _factory);

                _factory.Executions.Single().Parameters.Should().Contain(" /runIgnoredTests /select:\"");
                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfIgnoredTests, "TaefSamples::MissingDataSource::Test#error is run by TE.exe as well, but not reported");
                Count(TestOutcome.Passed).Should().Be(TestResources.NrOfPassingIgnoredTests);
                Count(TestOutcome.Failed).Should().Be(TestResources.NrOfFailingIgnoredTests);
            }
        }

        #endregion

        #region Other sample test DLLs

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_CrashingTests_CrashingTestsFailAndTheOthersAreRun()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.CrashingTests_DebugX86))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);
                testCases.Should().HaveCount(TestResources.NrOfCrashingTests);

                CreateRunner().RunTests(testCases, false, _factory);

                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfCrashingTests);
                Count(TestOutcome.Passed).Should().Be(TestResources.NrOfCrashingTestsPassing);
                Count(TestOutcome.Failed).Should().Be(TestResources.NrOfCrashingTestsFailing + TestResources.NrOfCrashingTestsCrashing);
                foreach (string crashingTest in new[] { TestResources.TestNames.CrashingTheCrash, TestResources.TestNames.CrashingTheAbort, TestResources.TestNames.CrashingTheStackOverflow })
                {
                    ResultOf(crashingTest).ErrorMessage.Should().Contain("The test host process was unexpectedly terminated with exit code", crashingTest);
                }
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("terminated abnormally"))), Times.Never);
                VerifyNoErrorsLogged();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_CrashingTestsInProcess_CrashedTestFailsAndFollowingTestsAreCrashSuspects()
        {
            MockOptions.Setup(o => o.RunInProcess).Returns(true);
            using (SampleCopy sample = SampleCopy.Create(TestResources.CrashingTests_DebugX64))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().Parameters.Should().EndWith(" /inproc");
                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfCrashingTests);
                Count(TestOutcome.Passed).Should().Be(TestResources.NrOfCrashingTestsPassingInProcess);
                Count(TestOutcome.Failed).Should().Be(TestResources.NrOfCrashingTestsFailingInProcess + 1);
                Count(TestOutcome.Skipped).Should().Be(TestResources.NrOfCrashingTestsNotStartedInProcess);

                TestResult crashed = ResultOf(TestResources.TestNames.CrashingTheCrash);
                crashed.ErrorMessage.Should().StartWith(StreamingTaefOutputParser.CrashText + " (TE.exe terminated with exit code 0xC0000005)");
                crashed.ErrorMessage.Should().Contain("About to dereference a null pointer");
                ResultOf(TestResources.TestNames.CrashingAddPassesAfterAllCrashes).ErrorMessage
                    .Should().Be("reason is probably a crash of test " + TestResources.TestNames.CrashingTheCrash);
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                    s.Contains($"TE.exe terminated abnormally with exit code 0xC0000005 while running test '{TestResources.TestNames.CrashingTheCrash}'"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_DllDependencyNotFound_TestsAreBlocked()
        {
            string baseDir = TestDataCreator.PreparePathExtensionTest();
            try
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(TestDataCreator.GetPathExtensionTestDll(baseDir));
                testCases.Should().HaveCount(TestResources.NrOfDllTests);

                CreateRunner().RunTests(testCases, false, _factory);

                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfDllTests);
                foreach (TestResult result in _reporter.ReportedTestResults)
                {
                    result.Outcome.Should().Be(TestOutcome.Failed);
                    result.ErrorMessage.Should().StartWith("Blocked: TAEF: [HRESULT: 0x8007007E] A failure occurred while preparing to run tests in 'DllTests_taef.dll'.");
                }
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("A failure occurred while preparing to run tests"))), Times.Once);
            }
            finally
            {
                Utils.DeleteDirectory(baseDir);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_DllDependencyFoundViaPathExtension_TestsAreRun()
        {
            string baseDir = TestDataCreator.PreparePathExtensionTest();
            try
            {
                MockOptions.Setup(o => o.PathExtension).Returns(TestDataCreator.GetPathExtensionDllDir(baseDir));
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(TestDataCreator.GetPathExtensionTestDll(baseDir));

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().PathExtension.Should().Be(TestDataCreator.GetPathExtensionDllDir(baseDir));
                ResultOf(TestResources.TestNames.DllTestsPassing).Outcome.Should().Be(TestOutcome.Passed);
                TestResultAssertions.AssertTestResultIsFailure(ResultOf(TestResources.TestNames.DllTestsFailing), "Verify: AreEqual(1, ReturnZero()) - Values (1, 0)");
                VerifyNoErrorsLogged();
            }
            finally
            {
                Utils.DeleteDirectory(baseDir);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_Repetitions_EveryTestHasSeveralResults()
        {
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(3);
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_DebugX64))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().Parameters.Should().EndWith(" /testmode:Loop /Loop:3 /LoopTest:1");
                _reporter.ReportedTestResults.Should().HaveCount(3 * TestResources.NrOfDllTests);
                Count(TestOutcome.Passed).Should().Be(3);
                Count(TestOutcome.Failed).Should().Be(3);
                VerifyNoErrorsLogged();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TestTimeout_LongRunningTestsAreAborted()
        {
            MockOptions.Setup(o => o.TestTimeout).Returns("0:0:1");
            using (SampleCopy sample = SampleCopy.Create(TestResources.LongRunningTests_ReleaseX64))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().Parameters.Should().EndWith(" /testTimeout:0:0:1");
                _reporter.ReportedTestResults.Should().HaveCount(TestResources.NrOfLongRunningTests);
                foreach (TestResult result in _reporter.ReportedTestResults)
                {
                    result.Outcome.Should().Be(TestOutcome.Failed);
                    result.ErrorMessage.Should().Contain("TAEF: The user-specified test timeout has expired.");
                }
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_HelperFile_PlaceholdersAreReplacedWithValuesOfHelperFile()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TheTarget=$(TheTarget)\"");
            using (SampleCopy sample = SampleCopy.Create(TestResources.HelperFileTests_DebugX86))
            {
                File.Exists(sample.TestDll + HelperFilesCache.HelperFileEnding).Should().BeTrue();
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().Parameters.Should().Contain("/p:\"TheTarget=HelperFileTests_taef.dll\"");
                _reporter.ReportedTestResults.Single().Outcome.Should().Be(TestOutcome.Passed);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_HelperFileMissing_TestFails()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TheTarget=$(TheTarget)\"");
            using (SampleCopy sample = SampleCopy.Create(TestResources.HelperFileTests_DebugX86, copyDependencies: false))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                CreateRunner().RunTests(testCases, false, _factory);

                _reporter.ReportedTestResults.Single().Outcome.Should().Be(TestOutcome.Failed);
            }
        }

        #endregion

        #region Repetitions in process, user selections, non-ASCII paths

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_RepetitionsInProcess_EveryTestHasSeveralResults()
        {
            // /Loop:3 would block the second and third loop with /inproc; /LoopTest:3 repeats each test in the same test host
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(3);
            MockOptions.Setup(o => o.RunInProcess).Returns(true);
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_DebugX64))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                CreateRunner().RunTests(testCases, false, _factory);

                _factory.Executions.Single().Parameters.Should().EndWith(" /testmode:Loop /Loop:1 /LoopTest:3 /inproc");
                _reporter.ReportedTestResults.Should().HaveCount(3 * TestResources.NrOfDllTests);
                Count(TestOutcome.Passed).Should().Be(3);
                Count(TestOutcome.Failed).Should().Be(3);
                _reporter.ReportedTestResults.Should().OnlyContain(tr => tr.ErrorMessage == null || !tr.ErrorMessage.Contains(StreamingTaefOutputParser.BlockedPrefix));
                VerifyNoErrorsLogged();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_UserSelectionAndAllDiscoveredTestsOfClass_TestsExcludedByUserAreNotRun()
        {
            // the user's selection hides TaefSamples::TestMath::AddFails from discovery
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns($"/select:\"not @Name='{TestResources.TestNames.TestMathAddFails}'\"");
            using (SampleCopy sample = SampleCopy.Create(TestResources.Tests_DebugX64))
            {
                List<TestCase> discoveredTestCases = new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDll(sample.TestDll).ToList();
                List<TestCase> testCases = discoveredTestCases.Where(tc => tc.FullyQualifiedName.StartsWith("TaefSamples::TestMath::")).ToList();
                testCases.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(
                    TestResources.TestNames.TestMathAddPasses, TestResources.TestNames.TestMathAddPassesWithTraits);

                CreateRunner().RunTests(testCases, false, _factory);

                RecordedExecution execution = _factory.Executions.Single();
                execution.Parameters.Should().Contain($" /select:\"(not @Name='{TestResources.TestNames.TestMathAddFails}') and (");
                execution.Output.Should().NotContain(l => l.StartsWith("StartGroup: " + TestResources.TestNames.TestMathAddFails));
                execution.Output.Should().NotContain(l => l.Contains("Multiple /select or /name options"));
                _reporter.ReportedTestResults.Should().HaveCount(2).And.OnlyContain(tr => tr.Outcome == TestOutcome.Passed);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
                VerifyNoErrorsLogged();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TestDllsInNonAsciiFolders_TestsAreRun()
        {
            // TE.exe can not open test DLLs whose path contains non-ASCII characters
            foreach ((string sampleDll, string folder) in new[]
            {
                (TestResources.DllTests_DebugX64, "dirü"),
                (TestResources.DllTests_DebugX64, "dir名"),
                (TestResources.DllTests_ReleaseX86, "x86ä")
            })
            {
                using (SampleCopy sample = SampleCopy.Create(sampleDll))
                {
                    string testDll = CopyWithDependencies(sample, folder);
                    List<TestCase> allTestCases = RunnerTestData.CreateTestCases(testDll, TestResources.TestNames.DllTestsPassing, TestResources.TestNames.DllTestsFailing);
                    List<TestCase> subset = RunnerTestData.CreateTestCases(testDll, new[] { TestResources.TestNames.DllTestsFailing },
                        new[] { TestResources.TestNames.DllTestsPassing, TestResources.TestNames.DllTestsFailing });

                    // default working directory $(TestDllDir): the file name is passed; else the 8.3 short path
                    foreach (string workingDir in new[] { "$(TestDllDir)", "$(TestDir)" })
                    {
                        MockOptions.Setup(o => o.WorkingDir).Returns(workingDir);
                        foreach (List<TestCase> testCases in new[] { allTestCases, subset })
                        {
                            _reporter = new FakeFrameworkReporter();

                            CreateRunner().RunTests(testCases, false, _factory);

                            string context = $"{folder}, {workingDir}: {_factory.Executions.Last()}";
                            _factory.Executions.Last().ExitCode.Should().Be(1, context);
                            _reporter.ReportedTestResults.Should().HaveCount(testCases.Count, context);
                            _reporter.ReportedTestResults.Select(tr => tr.TestCase.Source).Should().OnlyContain(s => s == testDll, context);
                            TestResultAssertions.AssertTestResultIsFailure(ResultOf(TestResources.TestNames.DllTestsFailing), "Verify: AreEqual(1, ReturnZero()) - Values (1, 0)");
                            if (testCases == allTestCases)
                                ResultOf(TestResources.TestNames.DllTestsPassing).Outcome.Should().Be(TestOutcome.Passed, context);
                        }
                    }
                    VerifyNoErrorsLogged();
                }
            }
        }

        /// <returns>The copy of the test DLL (and its dependencies) in sub folder <paramref name="folder"/>.</returns>
        private static string CopyWithDependencies(SampleCopy sample, string folder)
        {
            string targetDir = sample.GetPath(folder);
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sample.Directory))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
            return Path.Combine(targetDir, Path.GetFileName(sample.TestDll));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_DebuggingWithVsTestFrameworkAndNonAsciiTempDir_ResultsAreReadFromWttLog()
        {
            // TE.exe would write the WTT log to a different (mangled) folder if its path contained non-ASCII characters
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            var factory = new OutputlessProcessLauncherFactory();
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_DebugX64))
            {
                string tempDir = sample.GetPath("Temp_Jürgen_名前");
                Directory.CreateDirectory(tempDir);
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(sample.TestDll);

                string originalTmp = Environment.GetEnvironmentVariable("TMP");
                string originalTemp = Environment.GetEnvironmentVariable("TEMP");
                try
                {
                    Environment.SetEnvironmentVariable("TMP", tempDir);
                    Environment.SetEnvironmentVariable("TEMP", tempDir);

                    CreateRunner().RunTests(testCases, true, factory);
                }
                finally
                {
                    Environment.SetEnvironmentVariable("TMP", originalTmp);
                    Environment.SetEnvironmentVariable("TEMP", originalTemp);
                }

                string parameters = factory.Parameters.Single();
                parameters.Should().Contain(" /inproc /disableTimeouts /enableWttLogging /logFile:\"");
                string logFileArgument = System.Text.RegularExpressions.Regex.Match(parameters, "/logFile:\"([^\"]+)\"").Groups[1].Value;
                TeArguments.IsAscii(logFileArgument).Should().BeTrue(logFileArgument);

                ResultOf(TestResources.TestNames.DllTestsPassing).Outcome.Should().Be(TestOutcome.Passed);
                TestResultAssertions.AssertTestResultIsFailure(ResultOf(TestResources.TestNames.DllTestsFailing), "Verify: AreEqual(1, ReturnZero()) - Values (1, 0)");
                Directory.GetFileSystemEntries(tempDir).Should().BeEmpty("the WTT log has been deleted");
                Directory.GetFileSystemEntries(sample.Directory, "*.wtl").Should().BeEmpty("the WTT log has been deleted");
                Directory.GetDirectories(sample.Directory).Should().ContainSingle("TE.exe must not have created a folder with a mangled name");
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
                VerifyNoErrorsLogged();
            }
        }

        /// <summary>
        /// Launches processes as the VsTest framework's debugger does for the adapter (without debugger, and without
        /// providing the output).
        /// </summary>
        private class OutputlessProcessLauncherFactory : IDebuggedProcessExecutorFactory
        {
            public List<string> Parameters { get; } = new List<string>();

            public IProcessExecutor CreateExecutor(bool printTestOutput, ILogger logger) => throw new NotSupportedException();

            public IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, ILogger logger) => new Executor(this);

            public IDebuggedProcessExecutor CreateNativeDebuggingExecutor(DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger)
                => throw new NotSupportedException();

            private class Executor : IDebuggedProcessExecutor
            {
                private readonly OutputlessProcessLauncherFactory _factory;

                public Executor(OutputlessProcessLauncherFactory factory)
                {
                    _factory = factory;
                }

                public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension,
                    IDictionary<string, string> environmentVariables, Action<string> reportOutputLine)
                {
                    reportOutputLine.Should().BeNull("the output is not available when debugging with the VsTest framework");
                    _factory.Parameters.Add(parameters);
                    var startInfo = new ProcessStartInfo(command, parameters)
                    {
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process process = Process.Start(startInfo))
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        process.OutputDataReceived += (sender, args) => { };
                        process.ErrorDataReceived += (sender, args) => { };
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                        return process.ExitCode;
                    }
                }

                public void Cancel()
                {
                }
            }
        }

        #endregion

        #region Cancellation

        [TestMethod]
        [TestCategory(Integration)]
        public void Cancel_WithoutKillingProcesses_RunningTeFinishesAndNoFurtherTeIsStarted()
        {
            DoRunCancelingTests(killProcesses: false);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Cancel_KillingProcesses_TeAndTestHostAreKilledAndNoFurtherTeIsStarted()
        {
            DoRunCancelingTests(killProcesses: true);
        }

        private void DoRunCancelingTests(bool killProcesses)
        {
            MockOptions.Setup(o => o.KillProcessesOnCancel).Returns(killProcesses);
            using (SampleCopy longRunningTests = SampleCopy.Create(TestResources.LongRunningTests_ReleaseX86))
            using (SampleCopy dllTests = SampleCopy.Create(TestResources.DllTests_ReleaseX86))
            {
                List<TestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(longRunningTests.TestDll)
                    .Concat(TestDataCreator.GetTestCasesOfTestDll(dllTests.TestDll))
                    .ToList();
                testCases.Should().HaveCount(TestResources.NrOfLongRunningTests + TestResources.NrOfDllTests);

                var firstTestStarted = new ManualResetEventSlim();
                var reporter = new Mock<ITestFrameworkReporter>();
                reporter.Setup(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>())).Callback(() => firstTestStarted.Set());
                SequentialTestRunner runner = CreateRunner(reporter: reporter.Object);
                var thread = new Thread(() => runner.RunTests(testCases, false, _factory));

                thread.Start();
                firstTestStarted.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("TaefSamples::LongRunningTests::Test1 should have been started");
                IList<int> teProcesses = ProcessTree.GetTeProcessIds();
                IList<int> testHostProcesses = ProcessTree.GetTestHostProcessIds(teProcesses);
                teProcesses.Should().ContainSingle();
                testHostProcesses.Should().ContainSingle();

                Stopwatch stopwatch = Stopwatch.StartNew();
                runner.Cancel();
                thread.Join(TimeSpan.FromSeconds(60)).Should().BeTrue();
                stopwatch.Stop();

                _factory.Executions.Should().ContainSingle("DllTests_taef.dll must not be run after canceling");
                RecordedExecution execution = _factory.Executions.Single();
                execution.IsCanceled.Should().Be(killProcesses);
                if (killProcesses)
                {
                    // TE.exe has been killed while running Test1 (it would have needed about 2 more seconds per test)
                    execution.Output.Should().NotContain(l => l.StartsWith("Summary: "));
                    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
                }
                else
                {
                    // TE.exe has run both tests (the output has not been parsed anymore after canceling)
                    execution.Output.Should().Contain("EndGroup: " + TestResources.TestNames.LongRunningTest2 + " [Failed]");
                    execution.Output.Should().Contain(l => l.StartsWith("Summary: "));
                    execution.ExitCode.Should().Be(1);
                }
                ProcessTree.HasExited(teProcesses.Single()).Should().BeTrue();
                ProcessTree.HasExited(testHostProcesses.Single()).Should().BeTrue();
                reporter.Verify(r => r.ReportTestResults(It.Is<IEnumerable<TestResult>>(trs => trs.Any())), Times.Never);
                VerifyNoErrorsLogged();
            }
        }

        #endregion

    }

}
