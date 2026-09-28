// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.Helpers;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Runs the tests of <see cref="TestExecutorSequentialTests"/> while "debugging" with the native debugger: the
    /// adapter starts TE.exe suspended, asks the VS package to attach the debugger (<see cref="IDebuggerAttacher"/>,
    /// mocked) and resumes TE.exe, which runs the tests in process (<c>/inproc</c>). TE.exe's output is available.
    /// </summary>
    [TestClass]
    // ReSharper disable once InconsistentNaming
    public class TestExecutorSequentialTests_NativeDebugging : TestExecutorSequentialTests
    {
        private readonly List<(int ProcessId, DebuggerEngine Engine, string Image)> _attachedProcesses = new List<(int, DebuggerEngine, string)>();

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.Native);

            MockRunContext.Setup(c => c.IsBeingDebugged).Returns(true);
            SetupDebuggerAttacher(true);
        }

        private void SetupDebuggerAttacher(bool result)
        {
            MockDebuggerAttacher.Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns((int processId, DebuggerEngine engine) =>
                {
                    // the process is suspended while the debugger is attached
                    string image = ParentProcessUtils.GetProcessImageFileName(processId);
                    lock (_attachedProcesses)
                    {
                        _attachedProcesses.Add((processId, engine, image));
                    }
                    return result;
                });
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_NativeDebugging_DebuggerIsAttachedToTe()
        {
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 1, 1);

            _attachedProcesses.Should().ContainSingle();
            _attachedProcesses[0].Engine.Should().Be(DebuggerEngine.Native);
            Path.GetFileName(_attachedProcesses[0].Image).Should().BeEquivalentTo(TaefConstants.TeExecutableName);
            _attachedProcesses[0].Image.Should().ContainEquivalentOf(@"\x86\");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_ManagedAndNativeDebugging_DebuggerIsAttachedWithManagedAndNativeEngine()
        {
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.ManagedAndNative);

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX64).TestDll, 1, 1);

            _attachedProcesses.Should().ContainSingle();
            _attachedProcesses[0].Engine.Should().Be(DebuggerEngine.ManagedAndNative);
            _attachedProcesses[0].Image.Should().ContainEquivalentOf(@"\x64\");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_DebuggerCanNotBeAttached_ErrorIsLoggedAndTestsAreNotRun()
        {
            SetupDebuggerAttacher(false);

            // TE.exe started for debugging (/inproc /disableTimeouts) is never run without debugger
            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll, 0, 0, nrOfNotFoundTests: TestResources.NrOfDllTests, checkNoErrorsLogged: false);

            _attachedProcesses.Should().ContainSingle();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.StartsWith("Could not attach debugger to process"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("terminated abnormally"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_DebuggerCanNotBeAttachedWithBreakOnError_TestsAreNotReportedAsCrashed()
        {
            // TE.exe with /breakOnError and without debugger would terminate with STATUS_BREAKPOINT at the first failure
            MockOptions.Setup(o => o.BreakOnError).Returns(true);
            SetupDebuggerAttacher(false);

            RunAndVerifyTests(CopySample(TestResources.DllTests_ReleaseX64).TestDll, 0, 0, nrOfNotFoundTests: TestResources.NrOfDllTests, checkNoErrorsLogged: false);

            GetRecordedResults().Should().OnlyContain(r => r.ErrorMessage == null || !r.ErrorMessage.Contains(StreamingTaefOutputParser.CrashText));
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("terminated abnormally"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_NativeDebuggingWithoutVsPackage_ErrorIsLoggedAndNoTestsAreRun()
        {
            // no VS package (e.g. adapter installed via NuGet), or the package could not open its debugger attacher service
            // (the package then does not publish the pipe id)
            MockOptions.Setup(o => o.DebuggingNamedPipeId).Returns((string)null);

            RunTests(CopySample(TestResources.DllTests_ReleaseX86).TestDll);

            CheckMockInvocations(0, 0, 0, 0, 0);
            _attachedProcesses.Should().BeEmpty();
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("is only possible if the")
                && s.Contains("NuGet") && s.Contains("could not start the service")
                && s.Contains(SettingsWrapper.OptionDebuggerKind) && s.Contains("<DebuggerKind>VsTestFramework</DebuggerKind>"))), Times.Once);
        }

    }
}
