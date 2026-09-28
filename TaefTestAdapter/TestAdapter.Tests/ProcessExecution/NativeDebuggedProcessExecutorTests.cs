// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.TestAdapter.Helpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using TaefTestAdapter.Tests.Common.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Tests of <see cref="NativeDebuggedProcessExecutor"/> (with a mocked <see cref="IDebuggerAttacher"/>): the
    /// process is started suspended, the debugger is attached, and the process' output is decoded as UTF-8.
    /// </summary>
    [TestClass]
    public class NativeDebuggedProcessExecutorTests : ProcessExecutorTests
    {
        private readonly Mock<IDebuggerAttacher> _mockDebuggerAttacher = new Mock<IDebuggerAttacher>();
        private readonly List<(int ProcessId, DebuggerEngine Engine, string Image)> _attachedProcesses = new List<(int, DebuggerEngine, string)>();

        private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

        [TestInitialize]
        public void Setup()
        {
            SetupDebuggerAttacher(true);
            ProcessExecutor = new NativeDebuggedProcessExecutor(_mockDebuggerAttacher.Object, DebuggerEngine.Native, true, MockLogger.Object);
        }

        private void SetupDebuggerAttacher(bool result)
        {
            _mockDebuggerAttacher.Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns((int processId, DebuggerEngine engine) =>
                {
                    _attachedProcesses.Add((processId, engine, ParentProcessUtils.GetProcessImageFileName(processId)));
                    return result;
                });
        }

        [TestCleanup]
        public override void Teardown()
        {
            base.Teardown();
            _mockDebuggerAttacher.Reset();
            _attachedProcesses.Clear();
        }

        #region Common process executor tests

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_PingLocalHost()
        {
            Test_ExecuteProcessBlocking_PingLocalHost();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExecuteProcessBlocking_SampleTests()
        {
            Test_ExecuteProcessBlocking_SampleTests();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExecuteProcessBlocking_SampleTestsWithUnicodeOutput()
        {
            Test_ExecuteProcessBlocking_SampleTestsWithUnicodeOutput();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_WithSimpleCommand_ReturnsOutputOfCommand()
        {
            Test_WithSimpleCommand_ReturnsOutputOfCommand();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_IgnoresIfProcessReturnsErrorCode_DoesNotThrow()
        {
            Test_IgnoresIfProcessReturnsErrorCode_DoesNotThrow();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_SetEnvVariable_EnvVariableIsSet()
        {
            Test_WithEnvSetting_EnvVariableIsSet();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_SetExistingEnvVariable_EnvVariableIsOverridden()
        {
            Test_WithOverridingEnvSetting_EnvVariableHasNewValue();
        }

        #endregion

        #region Debugger attachment

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_NativeEngine_DebuggerIsAttachedToStartedProcess()
        {
            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo 2\"", ".", "", new Dictionary<string, string>(), output.Add);

            exitCode.Should().Be(0);
            output.Should().Equal("2");
            _attachedProcesses.Should().ContainSingle();
            _attachedProcesses[0].Engine.Should().Be(DebuggerEngine.Native);
            _attachedProcesses[0].Image.Should().BeEquivalentTo(Cmd);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_ManagedAndNativeEngine_DebuggerIsAttachedWithThatEngine()
        {
            ProcessExecutor = new NativeDebuggedProcessExecutor(_mockDebuggerAttacher.Object, DebuggerEngine.ManagedAndNative, false, MockLogger.Object);

            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"exit 0\"", ".", "", new Dictionary<string, string>(), null);

            _attachedProcesses.Should().ContainSingle();
            _attachedProcesses[0].Engine.Should().Be(DebuggerEngine.ManagedAndNative);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_DebuggerCanNotBeAttached_ErrorIsLoggedAndProcessIsTerminatedWithoutRunning()
        {
            // e.g. TE.exe with /breakOnError would crash at the first failing test if it was run without debugger
            SetupDebuggerAttacher(false);
            using (var directory = new TemporaryDirectory())
            {
                string markerFile = Path.Combine(directory.Path, "marker.txt");
                var output = new List<string>();

                int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, $"/C \"echo 2& echo ran > \"{markerFile}\"& exit /b 3\"", directory.Path, "",
                    new Dictionary<string, string>(), output.Add);

                exitCode.Should().Be(NativeDebuggedProcessExecutor.ExecutionFailed);
                output.Should().BeEmpty();
                File.Exists(markerFile).Should().BeFalse("the process must not have run");
                _attachedProcesses.Should().ContainSingle();
                IsProcessRunning(_attachedProcesses[0].ProcessId).Should().BeFalse();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.StartsWith($"Could not attach debugger to process {_attachedProcesses[0].ProcessId}")
                    && s.Contains("terminated without running any tests"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_BeforeProcessIsStarted_ProcessIsNotRunAndDebuggerIsNotAttached()
        {
            ProcessExecutor.Cancel();

            int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo 2\"", ".", "", new Dictionary<string, string>(), null);

            exitCode.Should().Be(NativeDebuggedProcessExecutor.ExecutionFailed);
            _attachedProcesses.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_PrintTestOutput_OutputIsLogged()
        {
            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo foo\"", ".", "", new Dictionary<string, string>(), null);

            MockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.StartsWith(">>>>>>>>>>>>>>> Output of command"))), Times.Once);
            MockLogger.Verify(l => l.LogInfo("foo"), Times.Once);
            MockLogger.Verify(l => l.LogInfo("<<<<<<<<<<<<<<< End of Output"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_NotExistingCommand_ErrorIsLogged()
        {
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(@"C:\DoesNotExist\TE.exe", "", ".", "", new Dictionary<string, string>(), null);

            exitCode.Should().Be(NativeDebuggedProcessExecutor.ExecutionFailed);
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Could not create process"))), Times.Once);
            _attachedProcesses.Should().BeEmpty();
        }

        #endregion

        #region Output, environment, working directory

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_Utf8Output_IsDecodedCorrectly()
        {
            // the fake TE.exe prints a captured TE.exe output (UTF-8)
            var output = new List<string>();
            var environment = new Dictionary<string, string>
            {
                { TestResources.FakeTeOutputEnvVariable, TestResources.GetTaefOutputFile("Tests_taef.dll.run.txt") }
            };

            int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, $"/C \"\"{TestResources.FakeTeExecutable}\"\"", ".", "", environment, output.Add);

            exitCode.Should().Be(0);
            output.Should().Contain("StartGroup: " + TestResources.TestNames.UmlautTest);
            output.Should().Contain("EndGroup: TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前 [Failed]");
            output.Should().HaveCount(TestResources.ReadTaefOutputLines("Tests_taef.dll.run.txt").Length);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_NonAsciiEnvironmentVariable_IsPassedCorrectly()
        {
            const string value = "Wert_äöüß€_名前";
            var output = new List<string>();

            // output of cmd.exe is UTF-8 with code page 65001
            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"chcp 65001 >NUL & set TAEF_ADAPTER_TEST_VAR\"", ".", "",
                new Dictionary<string, string> { { "TAEF_ADAPTER_TEST_VAR", value } }, output.Add);

            output.Should().Contain("TAEF_ADAPTER_TEST_VAR=" + value);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_WorkingDirAndPathExtension_AreSet()
        {
            string workingDir = Path.GetTempPath().TrimEnd('\\');
            const string pathExtension = @"C:\TaefAdapterTestPathExtension";
            var output = new List<string>();

            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"cd & echo %PATH%\"", workingDir, pathExtension, new Dictionary<string, string>(), output.Add);

            output.Should().HaveCount(2);
            output[0].Should().BeEquivalentTo(workingDir);
            output[1].Should().Contain(pathExtension);
            output[1].Should().Contain(Environment.GetEnvironmentVariable("PATH")?.Split(';')[0]);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_PathExtensionAndUserPathVariableWithOtherCase_PathExtensionWinsWithWarning()
        {
            const string pathExtension = @"C:\TaefAdapterTestPathExtension";
            var environmentVariables = new Dictionary<string, string> { { "Path", @"C:\user\path" } };
            var output = new List<string>();

            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo %PATH%\"", ".", pathExtension, environmentVariables, output.Add);

            output.Should().ContainSingle().Which.Should().StartWith(pathExtension + ";");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Both a path extension and a PATH environment variable (named 'Path')"))), Times.Once);
            environmentVariables.Should().Equal(new Dictionary<string, string> { { "Path", @"C:\user\path" } });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_AlternatingOutputOnStandardOutputAndError_OrderIsKept()
        {
            const int nrOfLoops = 100;
            var output = new List<string>();

            ProcessExecutor.ExecuteCommandBlocking(Cmd, $"/C \"for /L %i in (1,1,{nrOfLoops}) do @(echo out %i& echo err %i 1>&2)\"", ".", "",
                new Dictionary<string, string>(), output.Add);

            output.Select(l => l.Trim()).Should().Equal(Enumerable.Range(1, nrOfLoops).SelectMany(i => new[] { $"out {i}", $"err {i}" }));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteProcessBlocking_ChildProcessKeepsOutputOpen_ReturnsAfterGracePeriod()
        {
            ProcessExecutor = new NativeDebuggedProcessExecutor(_mockDebuggerAttacher.Object, DebuggerEngine.Native, false, MockLogger.Object, TimeSpan.FromSeconds(1));
            var output = new List<string>();
            var stopwatch = Stopwatch.StartNew();
            int pingProcessId = 0;
            try
            {
                // ping.exe inherits the output pipe and runs for about 30 s after cmd.exe has exited
                int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"start /b ping -n 30 127.0.0.1 >NUL & echo cmd.exe exits\"", ".", "",
                    new Dictionary<string, string>(), output.Add);
                stopwatch.Stop();

                exitCode.Should().Be(0);
                output.Select(l => l.Trim()).Should().Contain("cmd.exe exits");
                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("has exited, but its output is still being kept open"))), Times.Once);
                string warning = (string)MockLogger.Invocations.Single(i => i.Method.Name == nameof(ILogger.LogWarning)).Arguments[0];
                pingProcessId = int.Parse(System.Text.RegularExpressions.Regex.Match(warning, @"PING \((\d+)\)").Groups[1].Value);
            }
            finally
            {
                if (pingProcessId != 0 && IsProcessRunning(pingProcessId))
                {
                    using (Process ping = Process.GetProcessById(pingProcessId))
                        ping.Kill();
                }
            }
        }

        #endregion

        #region Cancellation

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_ProcessWithChildProcess_WholeProcessTreeIsKilled()
        {
            // cmd.exe starts ping.exe, which runs for about 30 s and keeps the output pipe open: the executor only returns
            // before if ping.exe is killed, too
            var firstLineReceived = new ManualResetEventSlim();
            var output = new List<string>();
            var stopwatch = Stopwatch.StartNew();
            Task<int> execution = Task.Factory.StartNew(() => ProcessExecutor.ExecuteCommandBlocking(
                Cmd, "/C \"ping -n 30 127.0.0.1\"", ".", "", new Dictionary<string, string>(),
                line =>
                {
                    lock (output) output.Add(line);
                    firstLineReceived.Set();
                }), TaskCreationOptions.LongRunning);

            firstLineReceived.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue("ping.exe should produce output");
            ProcessExecutor.Cancel();

            execution.Wait(TimeSpan.FromSeconds(15)).Should().BeTrue("the executor should return once the process tree has been killed");
            stopwatch.Stop();
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(25));
            _attachedProcesses.Should().ContainSingle();
            IsProcessRunning(_attachedProcesses[0].ProcessId).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_NoProcessStarted_DoesNotThrow()
        {
            Action cancel = () => ProcessExecutor.Cancel();
            cancel.Should().NotThrow();
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        #endregion

    }

}
