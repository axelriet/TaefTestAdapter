// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Tests of <see cref="TestExecutor"/> concerning its settings: settings provided by the VsTest framework (run
    /// settings of the run context) and option TeExecutable.
    /// </summary>
    [TestClass]
    public class TestExecutorSettingsTests : TestAdapterTestsBase
    {
        private readonly Mock<IDebuggerAttacher> _mockDebuggerAttacher = new Mock<IDebuggerAttacher>();

        private void RunTests(IEnumerable<VsTestCase> testCases, TestExecutor executor = null)
        {
            executor = executor ?? new TestExecutor(TestEnvironment.Logger, TestEnvironment.Options, _mockDebuggerAttacher.Object);
            executor.RunTests(testCases, MockRunContext.Object, MockFrameworkHandle.Object);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_SettingsFromRunContext_AreUsed()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            IList<VsTestCase> testCases = ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll),
                TestNames.TestDirectoryIsSet, TestNames.WorkingDirIsSolutionDirectory, TestNames.EnvironmentVariableIsSet);
            // the settings of SampleTests.taef.runsettings; $(SolutionDir) is the solution directory of the run context
            Mock<IRunSettings> mockRunSettings = CreateRunSettings(CreateSettingsXml(
                "<AdditionalTestExecutionParam>/p:\"TestDirectory=$(TestDir)\"</AdditionalTestExecutionParam>" +
                "<WorkingDir>$(SolutionDir)</WorkingDir>" +
                "<EnvironmentVariables>MYENVVAR=MyValue</EnvironmentVariables>" +
                "<PrefixOutputWithTaef>true</PrefixOutputWithTaef>"));
            MockRunContext.Setup(c => c.RunSettings).Returns(mockRunSettings.Object);

            // settings and logger are created from the run context
            RunTests(testCases, new TestExecutor());

            GetRecordedResults().Should().HaveCount(3);
            GetRecordedResults().Should().OnlyContain(r => r.Outcome == VsTestOutcome.Passed);
            MockFrameworkHandle.Verify(h => h.SendMessage(TestMessageLevel.Informational, It.Is<string>(s => s.StartsWith("[TAEF] Test execution completed"))), Times.Once);
            MockFrameworkHandle.Verify(h => h.SendMessage(TestMessageLevel.Error, It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_SettingsFromRunContextWithoutSampleSettings_TestsFail()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            IList<VsTestCase> testCases = ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll),
                TestNames.TestDirectoryIsSet, TestNames.WorkingDirIsSolutionDirectory, TestNames.EnvironmentVariableIsSet);
            MockRunContext.Setup(c => c.RunSettings).Returns(CreateRunSettings(CreateSettingsXml("")).Object);

            RunTests(testCases, new TestExecutor());

            GetRecordedResults().Should().HaveCount(3);
            GetRecordedResults().Should().OnlyContain(r => r.Outcome == VsTestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TeExecutableIsFolderOfTaefRuntimes_TeOfMatchingArchitectureIsUsed()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            IList<VsTestCase> testCases = ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll), TestNames.DllTestsPassing, TestNames.DllTestsFailing);
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.TaefRuntimesDir);

            RunTests(testCases);

            GetRecordedResults().Select(r => r.Outcome).Should().BeEquivalentTo(VsTestOutcome.Passed, VsTestOutcome.Failed);
            string expectedTe = Path.Combine(TestResources.TaefRuntimesDir, "x86", TaefConstants.TeExecutableName);
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains($"with '{expectedTe}'"))), Times.AtLeastOnce);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TeExecutableDoesNotExist_WarningIsLoggedAndTeIsFoundAutomatically()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX64).TestDll;
            IList<VsTestCase> testCases = ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll), TestNames.DllTestsPassing, TestNames.DllTestsFailing);
            string notExistingTe = $@"C:\DoesNotExist_{Guid.NewGuid():N}\TE.exe";
            MockOptions.Setup(o => o.TeExecutable).Returns(notExistingTe);

            RunTests(testCases);

            GetRecordedResults().Select(r => r.Outcome).Should().BeEquivalentTo(VsTestOutcome.Passed, VsTestOutcome.Failed);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains(SettingsWrapper.OptionTeExecutable) && s.Contains(notExistingTe))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_TeExecutableFails_TestsAreReportedAsFailedAndProblemIsLogged()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            IList<VsTestCase> testCases = ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll), TestNames.DllTestsPassing, TestNames.DllTestsFailing);
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.AlwaysFailingExe);

            RunTests(testCases);

            // the executable prints some output and terminates with a non-TAEF exit code without having started a test, which
            // can not be told apart from TE.exe crashing in a fixture outside of a test: the tests must not end up green or
            // merely "not found" (see StreamingTaefOutputParser.TerminatedOutsideOfTest)
            GetRecordedResults().Should().HaveCount(2);
            GetRecordedResults().Should().OnlyContain(r => r.Outcome == VsTestOutcome.Failed
                && r.ErrorMessage.Contains("terminated abnormally") && r.ErrorMessage.Contains("0x00001267")
                && r.ErrorMessage.Contains("Test output before exiting with 4711"));
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("TE.exe terminated abnormally") && s.Contains("0x00001267"))), Times.Once);
        }

    }
}
