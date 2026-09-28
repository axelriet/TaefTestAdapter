// This file has been modified for TAEF support.

using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;


namespace TaefTestAdapter.TestAdapter.Settings
{
    /// <summary>
    /// Tests of settings helper files (<c>&lt;test DLL&gt;.taef_settings_helper</c>, <c>Key=Value</c> pairs separated by
    /// <c>::TAEF::</c>, making <c>$(Key)</c> available as placeholder): HelperFileTests_taef.dll's post-build step writes
    /// such a file with <c>TheTarget=HelperFileTests_taef.dll</c>; its test only passes if TE.exe is passed
    /// <c>/p:"TheTarget=HelperFileTests_taef.dll"</c>.
    /// </summary>
    [TestClass]
    public class HelperFileTests : TestAdapterTestsBase
    {
        private const string TheTargetParameter = "/p:\"TheTarget=$(TheTarget)\"";

        private readonly Mock<IDebuggerAttacher> _mockDebuggerAttacher = new Mock<IDebuggerAttacher>();

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            _mockDebuggerAttacher.Reset();
            _mockDebuggerAttacher.Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>())).Returns(true);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void HelperFileTests_AdditionalParamsAreNotProvided_TestFails()
        {
            RunHelperFileTestsDll(CopySample(TestResources.HelperFileTests_ReleaseX86).TestDll);

            VerifyResult(TestOutcome.Failed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void HelperFileTests_AdditionalParamsAreProvided_TestSucceeds()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(TheTargetParameter);

            RunHelperFileTestsDll(CopySample(TestResources.HelperFileTests_ReleaseX86).TestDll);

            VerifyResult(TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void HelperFileTests_WorkingDirIsSetFromHelperFile_TestSucceeds()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(TheTargetParameter);
            MockOptions.Setup(o => o.WorkingDir).Returns("$(TheWorkingDirectory)");

            RunHelperFileTestsDll(CopySample(TestResources.HelperFileTests_ReleaseX64).TestDll);

            VerifyResult(TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void HelperFileTests_HelperFileWithOtherValue_TestFails()
        {
            SampleCopy copy = CopySample(TestResources.HelperFileTests_ReleaseX86, copyDependencies: false);
            File.WriteAllText(HelperFilesCache.GetHelperFile(copy.TestDll),
                $"SolutionDir={TestResources.SampleTestsSolutionDir}{HelperFilesCache.SettingsSeparator}TheTarget=SomethingElse.dll");
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(TheTargetParameter);

            RunHelperFileTestsDll(copy.TestDll);

            TestResult result = VerifyResult(TestOutcome.Failed);
            result.ErrorMessage.Should().Contain("SomethingElse.dll");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void HelperFileTests_HelperFileWithSeveralValues_AllPlaceholdersAreReplaced()
        {
            SampleCopy copy = CopySample(TestResources.HelperFileTests_ReleaseX86, copyDependencies: false);
            File.WriteAllText(HelperFilesCache.GetHelperFile(copy.TestDll),
                $"Prefix=HelperFile{HelperFilesCache.SettingsSeparator}Suffix=Tests_taef.dll{HelperFilesCache.SettingsSeparator}Dir={copy.Directory}");
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TheTarget=$(Prefix)$(Suffix)\"");
            MockOptions.Setup(o => o.WorkingDir).Returns("$(Dir)");

            RunHelperFileTestsDll(copy.TestDll);

            VerifyResult(TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void HelperFileTests_NoHelperFile_PlaceholderIsNotReplacedAndTestFails()
        {
            SampleCopy copy = CopySample(TestResources.HelperFileTests_ReleaseX86, copyDependencies: false);
            File.Exists(HelperFilesCache.GetHelperFile(copy.TestDll)).Should().BeFalse();
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(TheTargetParameter);

            RunHelperFileTestsDll(copy.TestDll);

            TestResult result = VerifyResult(TestOutcome.Failed);
            result.ErrorMessage.Should().Contain("$(TheTarget)");
        }

        private void RunHelperFileTestsDll(string testDll)
        {
            var executor = new TestExecutor(TestEnvironment.Logger, TestEnvironment.Options, _mockDebuggerAttacher.Object);
            executor.RunTests(testDll.Yield(), MockRunContext.Object, MockFrameworkHandle.Object);
        }

        private TestResult VerifyResult(TestOutcome expectedOutcome)
        {
            TestResult result = GetRecordedResults().Single();
            result.TestCase.DisplayName.Should().Be(TestNames.HelperFileTheTargetIsSet);
            result.Outcome.Should().Be(expectedOutcome, result.ErrorMessage);
            return result;
        }
    }
}
