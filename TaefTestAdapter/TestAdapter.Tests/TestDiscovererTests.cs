// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;

namespace TaefTestAdapter.TestAdapter
{

    /// <summary>
    /// Tests of <see cref="TestDiscoverer"/>: discovery of copies of the sample test DLLs with TE.exe, skipping of
    /// DLLs which are no TAEF test DLLs, failures of TE.exe (simulated with helper executables and a fake TE.exe set
    /// with option TeExecutable), the file origin check, and settings provided by the VsTest framework or by the
    /// fallback settings environment variable.
    /// </summary>
    [TestClass]
    public class TestDiscovererTests : TestAdapterTestsBase
    {
        private readonly Mock<IDiscoveryContext> _mockDiscoveryContext = new Mock<IDiscoveryContext>();
        private readonly Mock<ITestCaseDiscoverySink> _mockDiscoverySink = new Mock<ITestCaseDiscoverySink>();
        private readonly List<VsTestCase> _discoveredTestCases = new List<VsTestCase>();

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            _mockDiscoverySink
                .Setup(s => s.SendTestCase(It.IsAny<VsTestCase>()))
                .Callback((VsTestCase testCase) =>
                {
                    lock (_discoveredTestCases)
                        _discoveredTestCases.Add(testCase);
                });
        }

        [TestCleanup]
        public override void TearDown()
        {
            base.TearDown();

            _mockDiscoveryContext.Reset();
            _mockDiscoverySink.Reset();
            _discoveredTestCases.Clear();
        }

        private void DiscoverTests(params string[] testDlls)
        {
            var discoverer = new TestDiscoverer(TestEnvironment.Logger, TestEnvironment.Options);
            discoverer.DiscoverTests(testDlls, _mockDiscoveryContext.Object, MockVsLogger.Object, _mockDiscoverySink.Object);
        }

        /// <summary>Uses the fake TE.exe (printing <paramref name="outputFile"/> of the captured TE.exe outputs) for test discovery.</summary>
        private void SetupFakeTeExecutable(string outputFile, int exitCode = 0)
        {
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.FakeTeExecutable);
            MockOptions.Setup(o => o.EnvironmentVariables).Returns(
                $"{TestResources.FakeTeOutputEnvVariable}={TestResources.GetTaefOutputFile(outputFile)}" +
                $"{SettingsWrapper.TraitsRegexesPairSeparator}{TestResources.FakeTeExitCodeEnvVariable}={exitCode}");
        }

        private VsTestCase GetDiscoveredTestCase(string taefName)
        {
            return _discoveredTestCases.Single(tc => tc.DisplayName == taefName);
        }

        #region Discovery of TAEF test DLLs

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_WithDefaultSettings_RegistersFoundTestsAtDiscoverySink()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86, "MyTests.dll").TestDll;

            DiscoverTests(testDll);

            _discoveredTestCases.Should().HaveCount(TestResources.NrOfTests);
            _discoveredTestCases.Should().OnlyContain(tc => tc.Source == testDll && tc.ExecutorUri == TestExecutor.ExecutorUri);

            VsTestCase testCase = GetDiscoveredTestCase(TestNames.TestMathAddPassesWithTraits);
            testCase.FullyQualifiedName.Should().Be("TaefSamples.TestMath.AddPassesWithTraits");
            testCase.GetTaefName().Should().Be(TestNames.TestMathAddPassesWithTraits);
            testCase.CodeFilePath.Should().EndWithEquivalent(@"SampleTests\Tests\BasicTests.cpp");
            testCase.LineNumber.Should().BeGreaterThan(0);
            testCase.Traits.Should().Contain(t => t.Name == "Type" && t.Value == "Medium");
            testCase.Traits.Should().Contain(t => t.Name == "Owner" && t.Value == "ModuleOwner");
            testCase.Traits.Should().NotContain(t => t.Name == "TaefTestType");

            GetDiscoveredTestCase(TestNames.RowWithColons).FullyQualifiedName.Should().Be("TaefSamples.NamedRows.SpecialCharacters#with::colons [x]");
            GetDiscoveredTestCase(TestNames.UmlautTest).FullyQualifiedName.Should().Be("TaefSamples.Ümlautß.Täst");
            GetDiscoveredTestCase(TestNames.IgnoredPassing).Traits.Should().Contain(t => t.Name == TaefConstants.IgnoreProperty && t.Value == "true");
            GetDiscoveredTestCase(TestNames.MissingDataSource).FullyQualifiedName.Should().Be("TaefSamples.MissingDataSource.Test#error");

            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s == $"Found {TestResources.NrOfTests} tests in test DLL {testDll}")), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_TestDllsOfBothArchitectures_AllTestsAreFound()
        {
            string x86TestDll = CopySample(TestResources.DllTests_DebugX86).TestDll;
            string x64TestDll = CopySample(TestResources.LongRunningTests_ReleaseX64).TestDll;

            DiscoverTests(x86TestDll, x64TestDll);

            _discoveredTestCases.Should().HaveCount(TestResources.NrOfDllTests + TestResources.NrOfLongRunningTests);
            _discoveredTestCases.Where(tc => tc.Source == x86TestDll).Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo(TestNames.DllTestsPassing, TestNames.DllTestsFailing);
            _discoveredTestCases.Where(tc => tc.Source == x64TestDll).Select(tc => tc.DisplayName)
                .Should().BeEquivalentTo(TestNames.LongRunningTest1, TestNames.LongRunningTest2);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_WithCustomNonMatchingRegex_DoesNotFindTests()
        {
            string testDll = CopySample(TestResources.Tests_DebugX86, "MyTests.dll").TestDll;
            MockOptions.Setup(o => o.TestDiscoveryRegex).Returns("NoMatchAtAll");

            DiscoverTests(testDll);

            _discoveredTestCases.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_WithCustomMatchingRegex_FindsTests()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX64, "MyTests.dll").TestDll;
            MockOptions.Setup(o => o.TestDiscoveryRegex).Returns(@".*\\MyTests\.dll$");

            DiscoverTests(testDll);

            _discoveredTestCases.Should().HaveCount(TestResources.NrOfDllTests);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_SelectInAdditionalTestExecutionParam_RestrictsDiscoveredTests()
        {
            string testDll = CopySample(TestResources.Tests_DebugX64).TestDll;
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/select:\"@Name='TaefSamples::TestMath::*'\"");

            DiscoverTests(testDll);

            // TE.exe always lists (and runs) the pseudo test of a missing data source, whatever is selected
            _discoveredTestCases.Select(tc => tc.DisplayName).Should().BeEquivalentTo(
                TestNames.TestMathAddFails, TestNames.TestMathAddPasses, TestNames.TestMathAddPassesWithTraits, TestNames.MissingDataSource);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_NonTaefDlls_AreSkippedSilentlyWithoutStartingTe()
        {
            SampleCopy copy = CopySample(TestResources.ClrTests_ReleaseX86);
            TemporaryDirectory directory = CreateTemporaryDirectory();
            string textFile = directory.CreateFile("NotADll.dll", "This is not a DLL");
            string nativeDll = TestResources.DllTestsDll_DebugX64;
            string managedDll = Path.Combine(copy.Directory, TestResources.ClrDotNetLibProjectDll);
            string adapterDll = typeof(TestDiscoverer).Assembly.Location;
            string notExistingDll = directory.GetPath("DoesNotExist.dll");
            // if TE.exe was started for any of the DLLs, the fake TE.exe would report tests
            SetupFakeTeExecutable("Tests_taef.dll.listProperties.txt");

            DiscoverTests(textFile, nativeDll, managedDll, adapterDll, notExistingDll);

            _discoveredTestCases.Should().BeEmpty();
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_DllWithIndicatorFile_IsTreatedAsTaefTestDll()
        {
            TemporaryDirectory directory = CreateTemporaryDirectory();
            string dll = directory.GetPath(TestResources.DllProjectDll);
            File.Copy(TestResources.DllTestsDll_DebugX64, dll);
            directory.CreateFile(TestResources.DllProjectDll + TaefConstants.IndicatorFileExtension);
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            SetupFakeTeExecutable("Tests_taef.dll.listProperties.txt");

            DiscoverTests(dll);

            _discoveredTestCases.Should().HaveCount(TestResources.NrOfTests);
            _discoveredTestCases.Should().OnlyContain(tc => tc.Source == dll && tc.CodeFilePath == "" && tc.LineNumber == 0);
        }

        #endregion

        #region Failures of TE.exe

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_TeFailsWithSpecialExitCode_ErrorWithExplanationIsLogged()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            SetupFakeTeExecutable("Error.NoTestFiles.txt", TaefConstants.ExitCodeNoTestFiles);

            DiscoverTests(testDll);

            _discoveredTestCases.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s =>
                s.Contains($"Could not list the tests of test DLL '{testDll}'")
                && s.Contains("(0x05000000)")
                && s.Contains(TaefConstants.GetExitCodeDescription(TaefConstants.ExitCodeNoTestFiles))
                && s.Contains("Error: TAEF: None of the specified test files were found."))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_CrashingTe_CrashIsLogged()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.AlwaysCrashingExe);

            using (new CrashDialogSuppressor())
            {
                DiscoverTests(testDll);
            }

            _discoveredTestCases.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Could not list the tests of test DLL") && s.Contains("(0xC0000005)"))),
                Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_FailingTe_ExitCodeIsLogged()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.AlwaysFailingExe);

            DiscoverTests(testDll);

            _discoveredTestCases.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains($"TE.exe returned with exit code {TestResources.AlwaysFailingExeExitCode}"))),
                Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_TeDoesNotTerminate_DiscoveryIsCanceledAfterTimeout()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.TenSecondsWaiter);
            MockOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns(1);

            Stopwatch stopwatch = Stopwatch.StartNew();
            DiscoverTests(testDll);
            stopwatch.Stop();

            _discoveredTestCases.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.StartsWith($"Test discovery was cancelled after 1s for test DLL '{testDll}'"))), Times.Once);
            // TenSecondsWaiter runs for 10 s
            stopwatch.ElapsedMilliseconds.Should().BeInRange(1000, 8000);
        }

        #endregion

        #region Origin check

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_TrustedTestDll_TeIsRun()
        {
            SampleCopy copy = CopySample(TestResources.DllTests_ReleaseX86);
            // SemaphoreExe writes a file into its working directory (the test DLL's directory) and returns 143
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.SemaphoreExe);
            string semaphoreFile = copy.GetPath(TestResources.SemaphoreExeSemaphoreFile);

            DiscoverTests(copy.TestDll);

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains($"TE.exe returned with exit code {TestResources.SemaphoreExeExitCode}"))),
                Times.Once);
            semaphoreFile.AsFileInfo().Should().Exist("TE.exe should have been run");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_UntrustedTestDll_TeIsNotRun()
        {
            SampleCopy copy = CopySample(TestResources.DllTests_ReleaseX86);
            MarkUntrusted(copy.TestDll);
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.SemaphoreExe);
            string semaphoreFile = copy.GetPath(TestResources.SemaphoreExeSemaphoreFile);

            DiscoverTests(copy.TestDll);

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("was blocked to help protect"))), Times.Once);
            semaphoreFile.AsFileInfo().Should().NotExist("TE.exe should not have been run");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_UntrustedTestDllWithSkipOriginCheck_TeIsRun()
        {
            SampleCopy copy = CopySample(TestResources.DllTests_ReleaseX86);
            MarkUntrusted(copy.TestDll);
            MockOptions.Setup(o => o.TeExecutable).Returns(TestResources.SemaphoreExe);
            MockOptions.Setup(o => o.SkipOriginCheck).Returns(true);
            string semaphoreFile = copy.GetPath(TestResources.SemaphoreExeSemaphoreFile);

            DiscoverTests(copy.TestDll);

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains($"TE.exe returned with exit code {TestResources.SemaphoreExeExitCode}"))),
                Times.Once);
            semaphoreFile.AsFileInfo().Should().Exist("TE.exe should have been run");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains(SettingsWrapper.OptionSkipOriginCheck))), Times.Once);
        }

        private static void MarkUntrusted(string path)
        {
            using (var handle = NativeMethods.CreateFileW(path + ":Zone.Identifier", NativeMethods.GENERIC_WRITE, 0, IntPtr.Zero,
                NativeMethods.CREATE_NEW, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                using (var stream = new FileStream(handle, FileAccess.Write))
                {
                    var data = Encoding.ASCII.GetBytes("[ZoneTransfer]\r\nZoneId=3");
                    stream.Write(data, 0, data.Length);
                }
            }
        }

        #endregion

        #region Settings provided by the VsTest framework

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_SettingsFromRunSettings_AreUsed()
        {
            string myTestDll = CopySample(TestResources.DllTests_ReleaseX86, "MyTests.dll").TestDll;
            string otherTestDll = CopySample(TestResources.LongRunningTests_ReleaseX86, "OtherTests.dll").TestDll;
            Mock<IRunSettings> mockRunSettings = CreateRunSettings(CreateSettingsXml(@"<TestDiscoveryRegex>.*\\MyTests\.dll$</TestDiscoveryRegex><PrefixOutputWithTaef>true</PrefixOutputWithTaef>"));
            _mockDiscoveryContext.Setup(c => c.RunSettings).Returns(mockRunSettings.Object);

            // settings and logger are created from the run settings
            new TestDiscoverer().DiscoverTests(new[] { myTestDll, otherTestDll }, _mockDiscoveryContext.Object, MockVsLogger.Object, _mockDiscoverySink.Object);

            _discoveredTestCases.Should().HaveCount(TestResources.NrOfDllTests);
            _discoveredTestCases.Should().OnlyContain(tc => tc.Source == myTestDll);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Informational, It.Is<string>(s => s.StartsWith("[TAEF] ") && s.Contains($"Found {TestResources.NrOfDllTests} tests in test DLL"))), Times.Once);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Error, It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void DiscoverTests_FallbackSettingsFromEnvironmentVariable_AreUsed()
        {
            string myTestDll = CopySample(TestResources.DllTests_ReleaseX86, "MyTests.dll").TestDll;
            string otherTestDll = CopySample(TestResources.LongRunningTests_ReleaseX86, "OtherTests.dll").TestDll;
            TemporaryDirectory directory = CreateTemporaryDirectory();
            string settingsFile = directory.CreateFile("Fallback.runsettings",
                $"<?xml version=\"1.0\" encoding=\"utf-8\"?><RunSettings>{CreateSettingsXml(@"<TestDiscoveryRegex>.*\\OtherTests\.dll$</TestDiscoveryRegex>")}</RunSettings>");
            // the VsTest framework does not provide the adapter's settings (e.g. because the adapter is not registered with it)
            var mockRunSettings = new Mock<IRunSettings>(MockBehavior.Strict);
            _mockDiscoveryContext.Setup(c => c.RunSettings).Returns(mockRunSettings.Object);

            CommonFunctionsTests.RunWithEnvVariable(CommonFunctions.TaefSettingsEnvVariable, settingsFile, () =>
                new TestDiscoverer().DiscoverTests(new[] { myTestDll, otherTestDll }, _mockDiscoveryContext.Object, MockVsLogger.Object, _mockDiscoverySink.Object));

            _discoveredTestCases.Should().HaveCount(TestResources.NrOfLongRunningTests);
            _discoveredTestCases.Should().OnlyContain(tc => tc.Source == otherTestDll);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Informational, It.Is<string>(s => s.Contains($"Using fallback settings from file '{settingsFile}'"))), Times.Once);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Error, It.IsAny<string>()), Times.Never);
        }

        #endregion

    }

    /// <summary>
    /// Prevents Windows Error Reporting dialogs for crashing child processes while it is not disposed (the error mode
    /// of the current process is inherited by processes it starts).
    /// </summary>
    internal sealed class CrashDialogSuppressor : IDisposable
    {
        private const uint SEM_FAILCRITICALERRORS = 0x0001;
        private const uint SEM_NOGPFAULTERRORBOX = 0x0002;

        private readonly uint _formerErrorMode;

        public CrashDialogSuppressor()
        {
            _formerErrorMode = NativeMethods.SetErrorMode(0);
            NativeMethods.SetErrorMode(_formerErrorMode | SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
        }

        public void Dispose()
        {
            NativeMethods.SetErrorMode(_formerErrorMode);
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class NativeMethods
    {
        public const int GENERIC_WRITE = 1073741824;
        public const int CREATE_NEW = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(
            [MarshalAs(UnmanagedType.LPWStr)] string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll")]
        public static extern uint SetErrorMode(uint uMode);
    }

}
