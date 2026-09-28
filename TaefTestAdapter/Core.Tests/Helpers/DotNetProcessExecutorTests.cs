// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using TaefTestAdapter.Tests.Common.Tests;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Helpers
{
    [TestClass]
    public class DotNetProcessExecutorTests : ProcessExecutorTests
    {
        [TestInitialize]
        public void Setup()
        {
            ProcessExecutor = new DotNetProcessExecutor(false, MockLogger.Object);
        }

        [TestCleanup]
        public override void Teardown()
        {
            base.Teardown();
        }

        private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

        #region Shared test bodies

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

        #region Encoding, output, PATH

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_Utf8Output_IsDecodedCorrectly()
        {
            DotNetProcessExecutor.OutputEncoding.WebName.Should().Be("utf-8");
            DotNetProcessExecutor.OutputEncoding.GetPreamble().Should().BeEmpty();

            using (var directory = new TemporaryDirectory())
            {
                // TE.exe writes UTF-8 (without BOM) if its output is redirected and /unicodeOutput:false is passed
                const string text = "StartGroup: TaefSamples::Ümlautß::Täst 名前 ✓";
                string file = directory.CreateFile("utf8.txt", text + "\r\nsecond line\r\n");

                var output = new List<string>();
                int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, $"/C type \"{file}\"", directory.Path, null, new Dictionary<string, string>(), output.Add);

                exitCode.Should().Be(0);
                output.Should().Equal(text, "second line");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_OutputOnStandardError_IsReportedAsWell()
        {
            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo to stdout& echo to stderr 1>&2\"", ".", null, new Dictionary<string, string>(), output.Add);

            exitCode.Should().Be(0);
            output.Select(l => l.Trim()).Should().Equal("to stdout", "to stderr");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_AlternatingOutputOnStandardOutputAndError_OrderIsKept()
        {
            // Standard output and standard error share one pipe: e.g. the std::cerr output of a test run with
            // TE.exe /inproc must end up within the output of that test (with two pipes read by two threads, the lines
            // of the two streams were reported in arbitrary order).
            const int nrOfLoops = 300;
            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, $"/C \"for /L %i in (1,1,{nrOfLoops}) do @(echo out %i& echo err %i 1>&2)\"", ".", null,
                new Dictionary<string, string>(), output.Add);

            exitCode.Should().Be(0);
            IEnumerable<string> expectedOutput = Enumerable.Range(1, nrOfLoops).SelectMany(i => new[] { $"out {i}", $"err {i}" });
            output.Select(l => l.Trim()).Should().Equal(expectedOutput);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExecuteCommandBlocking_InProcessTestWritesToStandardError_LineIsReportedWithinGroupOfTest()
        {
            string testDll = TestResources.Tests_DebugX86;
            var output = new List<string>();

            // TaefSamples::OutputHandling::OutputOfPassingTest writes a line to std::cerr (and TE.exe /inproc writes the test's output
            // to its own standard output and standard error)
            ProcessExecutor.ExecuteCommandBlocking(
                TestResources.GetTeExecutable(SampleConfiguration.DebugX86),
                $"\"{testDll}\" {TaefConstants.OutputFormatOptions} {TaefConstants.InProcOption} {TaefConstants.GetSelectOption(TaefConstants.GetNameSelectionTerm("TaefSamples::OutputHandling::*"))}",
                Path.GetDirectoryName(testDll), null, new Dictionary<string, string>(), output.Add);

            const string testName = "TaefSamples::OutputHandling::OutputOfPassingTest";
            int startOfGroup = output.IndexOf("StartGroup: " + testName);
            int endOfGroup = output.IndexOf("EndGroup: " + testName + " [Passed]");
            int standardErrorLine = output.IndexOf("std::cerr output (visible with /inproc only)");
            startOfGroup.Should().BeGreaterThan(0);
            endOfGroup.Should().BeGreaterThan(startOfGroup);
            standardErrorLine.Should().BeGreaterThan(output.IndexOf("printf output (visible with /inproc only)"));
            standardErrorLine.Should().BeInRange(startOfGroup + 1, endOfGroup - 1);
            output.Count(l => l.Contains("std::cerr output")).Should().Be(1);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_DifferentLineBreaks_AreSplitLikeReadLine()
        {
            using (var directory = new TemporaryDirectory())
            {
                string file = directory.CreateFile("linebreaks.txt", "a\r\nb\nc\rd\r\n\r\nlast");
                var output = new List<string>();

                ProcessExecutor.ExecuteCommandBlocking(Cmd, $"/C type \"{file}\"", directory.Path, null, new Dictionary<string, string>(), output.Add);

                output.Should().Equal("a", "b", "c", "d", "", "last");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_ReportOutputLineThrows_ExceptionIsRethrownAndProcessIsKilled()
        {
            ISet<int> pingProcessesBefore = GetPingProcesses();
            var exception = new InvalidOperationException("output can not be processed");
            Stopwatch stopwatch = Stopwatch.StartNew();

            // ping.exe runs for about 30 s
            ProcessExecutor.Invoking(e => e.ExecuteCommandBlocking(Cmd, "/C \"echo first line& ping -n 30 127.0.0.1\"", ".", null,
                    new Dictionary<string, string>(), line => throw exception))
                .Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(exception);

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
            GetNewPingProcesses(pingProcessesBefore).Should().BeEmpty("ping.exe must have been killed");
        }

        #endregion

        #region Processes keeping the output open

        /// <summary>
        /// cmd.exe starts ping.exe in the background and exits: ping.exe inherits cmd.exe's handles (incl. the output pipe,
        /// independent of the redirection of ping's output), like e.g. a helper process started by a test with
        /// CreateProcess(bInheritHandles=TRUE), system() or _spawn.
        /// </summary>
        private const string CommandLeavingPingRunning = "/C \"start /b ping -n 30 127.0.0.1 >NUL & echo cmd.exe exits\"";

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_ChildProcessKeepsOutputOpen_ReturnsAfterGracePeriodAndLogsWarning()
        {
            var executor = new DotNetProcessExecutor(false, MockLogger.Object, TimeSpan.FromSeconds(1));
            ISet<int> pingProcessesBefore = GetPingProcesses();
            var output = new List<string>();
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                int exitCode = executor.ExecuteCommandBlocking(Cmd, CommandLeavingPingRunning, ".", null, new Dictionary<string, string>(), output.Add);
                stopwatch.Stop();

                exitCode.Should().Be(0);
                output.Select(l => l.Trim()).Should().Contain("cmd.exe exits");
                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "ping.exe runs for about 30 s, but is not waited for");
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("has exited, but its output is still being kept open by processes started by it which are still running: ")
                    && s.Contains("PING (") && !s.Contains("conhost"))), Times.Once);

                // processes left behind by a process which has exited normally are not killed
                GetNewPingProcesses(pingProcessesBefore).Should().ContainSingle();
            }
            finally
            {
                KillNewPingProcesses(pingProcessesBefore);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_ProcessHasExitedButChildProcessKeepsOutputOpen_ChildProcessIsKilledAndExecutionReturns()
        {
            // without the grace period, the executor would wait for ping.exe
            var executor = new DotNetProcessExecutor(false, MockLogger.Object, TimeSpan.FromMinutes(5));
            ISet<int> pingProcessesBefore = GetPingProcesses();
            var cmdHasExited = new ManualResetEventSlim();
            try
            {
                Task<int> execution = Task.Factory.StartNew(() => executor.ExecuteCommandBlocking(Cmd, CommandLeavingPingRunning, ".", null,
                    new Dictionary<string, string>(), line =>
                    {
                        if (line.Contains("cmd.exe exits"))
                            cmdHasExited.Set();
                    }), TaskCreationOptions.LongRunning);
                cmdHasExited.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue();
                ProcessTree.WaitFor(() => GetNewPingProcesses(pingProcessesBefore).Count == 1, 10000).Should().BeTrue();
                // ping.exe's parent (cmd.exe) has exited, i.e. ping.exe can not be found via the process tree of cmd.exe
                Thread.Sleep(500);
                execution.IsCompleted.Should().BeFalse();

                Stopwatch stopwatch = Stopwatch.StartNew();
                executor.Cancel();

                execution.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the executor must not wait for the killed ping.exe");
                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
                ProcessTree.WaitFor(() => GetNewPingProcesses(pingProcessesBefore).Count == 0, 5000).Should().BeTrue("ping.exe must have been killed");
                MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("Could not kill"))), Times.Never);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                KillNewPingProcesses(pingProcessesBefore);
            }
        }

        /// <returns>Ids of the running ping.exe processes.</returns>
        private static ISet<int> GetPingProcesses()
        {
            var result = new HashSet<int>();
            foreach (Process process in Process.GetProcessesByName("PING"))
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited)
                            result.Add(process.Id);
                    }
                    catch (Exception)
                    {
                        // process has exited meanwhile, or can not be accessed
                    }
                }
            }
            return result;
        }

        /// <returns>Ids of the running ping.exe processes which are not contained in <paramref name="pingProcessesBefore"/>.</returns>
        private static IList<int> GetNewPingProcesses(ISet<int> pingProcessesBefore)
        {
            return GetPingProcesses().Where(id => !pingProcessesBefore.Contains(id)).ToList();
        }

        private static void KillNewPingProcesses(ISet<int> pingProcessesBefore)
        {
            foreach (int processId in GetNewPingProcesses(pingProcessesBefore))
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        process.Kill();
                    }
                }
                catch (Exception)
                {
                    // has exited meanwhile
                }
            }
        }

        #endregion

        #region Environment, working directory, logging, errors

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_PathExtensionAndUserPathVariableWithAnyCase_PathExtensionWinsWithWarning()
        {
            const string pathExtension = @"C:\my\path\extension";
            foreach (string name in new[] { "PATH", "Path", "path" })
            {
                MockLogger.Reset();
                var environmentVariables = new Dictionary<string, string> { { name, @"C:\user\path" } };
                var output = new List<string>();

                ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo %PATH%\"", ".", pathExtension, environmentVariables, output.Add);

                output.Should().ContainSingle().Which.Should().StartWith(pathExtension + ";", name);
                output.Single().Should().NotContain(@"C:\user\path", name);
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Both a path extension and a PATH environment variable") && s.Contains($"'{name}'"))), Times.Once, name);
                environmentVariables.Should().Equal(new Dictionary<string, string> { { name, @"C:\user\path" } });
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_UserPathVariableWithoutPathExtension_UserPathIsUsed()
        {
            var output = new List<string>();

            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo %PATH%\"", ".", null,
                new Dictionary<string, string> { { "Path", @"C:\user\path" } }, output.Add);

            output.Should().Equal(@"C:\user\path");
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_EnvironmentVariablesAndPathExtension_DictionaryIsNotModified()
        {
            var environmentVariables = new Dictionary<string, string> { { "TAEF_ADAPTER_TEST_VAR", "value" } };
            var output = new List<string>();

            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo %TAEF_ADAPTER_TEST_VAR%\"", ".", @"C:\my\path\extension", environmentVariables, output.Add);

            output.Should().Equal("value");
            environmentVariables.Should().Equal(new Dictionary<string, string> { { "TAEF_ADAPTER_TEST_VAR", "value" } });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_NoOutputReceiver_ProcessIsRunAnyway()
        {
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo 2 & exit /b 3\"", ".", null, null, null);

            exitCode.Should().Be(3);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_PathExtension_IsPrependedToPath()
        {
            const string pathExtension = @"C:\my\path\extension";
            var output = new List<string>();

            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo %PATH%\"", ".", pathExtension, new Dictionary<string, string>(), output.Add);

            output.Should().ContainSingle().Which.Should().StartWith(pathExtension + ";");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_WorkingDirectory_IsSet()
        {
            using (var directory = new TemporaryDirectory())
            {
                directory.CreateFile("MarkerFileOfWorkingDirectory.txt");
                var output = new List<string>();

                ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"dir /b\"", directory.Path, null, new Dictionary<string, string>(), output.Add);

                output.Should().ContainSingle().Which.Should().Be("MarkerFileOfWorkingDirectory.txt");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_PrintTestOutput_OutputIsLoggedWithStartAndEndMarkers()
        {
            var logger = new Mock<TaefTestAdapter.Common.ILogger>();
            var executor = new DotNetProcessExecutor(true, logger.Object);

            executor.ExecuteCommandBlocking(Cmd, "/C \"echo 2\"", ".", null, new Dictionary<string, string>(), null);

            logger.Verify(l => l.LogInfo($">>>>>>>>>>>>>>> Output of command '\"{Cmd}\" /C \"echo 2\"'"), Times.Once);
            logger.Verify(l => l.LogInfo("2"), Times.Once);
            logger.Verify(l => l.LogInfo("<<<<<<<<<<<<<<< End of Output"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_NoPrintTestOutput_OutputIsNotLogged()
        {
            ProcessExecutor.ExecuteCommandBlocking(Cmd, "/C \"echo 2\"", ".", null, new Dictionary<string, string>(), null);

            MockLogger.Verify(l => l.LogInfo(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_ExecutableDoesNotExist_Throws()
        {
            new Action(() => ProcessExecutor.ExecuteCommandBlocking(@"C:\does\not\exist\TE.exe", "", ".", null, new Dictionary<string, string>(), null))
                .Should().Throw<System.ComponentModel.Win32Exception>();
        }

        #endregion

        #region Cancel

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_WhileProcessIsRunning_ProcessAndItsChildrenAreKilled()
        {
            var executor = new DotNetProcessExecutor(false, MockLogger.Object);
            var firstLineReceived = new ManualResetEventSlim();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // cmd.exe starts ping.exe, which runs for about 60 s
            Task<int> execution = Task.Run(() => executor.ExecuteCommandBlocking(Cmd, "/C \"ping -n 60 127.0.0.1\"", ".", null,
                new Dictionary<string, string>(), line => firstLineReceived.Set()));
            firstLineReceived.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
            IList<int> cmdProcesses = ProcessTree.GetChildProcessIds(Process.GetCurrentProcess().Id, "cmd")
                .Where(id => ProcessTree.GetChildProcessIds(id, "ping").Any())
                .ToList();
            cmdProcesses.Should().ContainSingle();
            IList<int> pingProcesses = ProcessTree.GetChildProcessIds(cmdProcesses.Single(), "ping");

            executor.Cancel();

            execution.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue("the process tree should have been killed");
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(40));
            execution.Result.Should().NotBe(0);
            ProcessTree.HasExited(cmdProcesses.Single()).Should().BeTrue();
            ProcessTree.HasExited(pingProcesses.Single()).Should().BeTrue("the child process must be killed as well");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_BeforeProcessIsStarted_ProcessIsKilledRightAfterStart()
        {
            var executor = new DotNetProcessExecutor(false, MockLogger.Object);
            executor.Cancel();

            Stopwatch stopwatch = Stopwatch.StartNew();
            int exitCode = executor.ExecuteCommandBlocking(Cmd, "/C \"ping -n 60 127.0.0.1\"", ".", null, new Dictionary<string, string>(), null);
            stopwatch.Stop();

            exitCode.Should().NotBe(0);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(40));
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains("Execution has been canceled while process") && s.EndsWith("was being started, killing it"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Cancel_AfterProcessHasExited_DoesNothing()
        {
            var executor = new DotNetProcessExecutor(false, MockLogger.Object);
            executor.ExecuteCommandBlocking(Cmd, "/C \"echo 2\"", ".", null, new Dictionary<string, string>(), null).Should().Be(0);

            executor.Invoking(e => e.Cancel()).Should().NotThrow();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Cancel_WhileTeIsRunningTest_TeAndTestHostAreKilled()
        {
            string testDll = TestResources.LongRunningTests_ReleaseX64;
            var executor = new DotNetProcessExecutor(false, MockLogger.Object);
            var testStarted = new ManualResetEventSlim();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // 10 loops of 2 tests running 2 s each
            Task<int> execution = Task.Run(() => executor.ExecuteCommandBlocking(
                TestResources.GetTeExecutable(SampleConfiguration.ReleaseX64),
                $"\"{testDll}\" {TaefConstants.OutputFormatOptions} {TaefConstants.GetLoopOptions(10)}",
                Path.GetDirectoryName(testDll), null, new Dictionary<string, string>(),
                line =>
                {
                    if (line.StartsWith("StartGroup: "))
                        testStarted.Set();
                }));
            testStarted.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
            IList<int> teProcesses = ProcessTree.GetTeProcessIds();
            teProcesses.Should().ContainSingle();
            IList<int> testHostProcesses = ProcessTree.GetTestHostProcessIds(teProcesses);
            testHostProcesses.Should().ContainSingle();

            executor.Cancel();

            execution.Wait(TimeSpan.FromSeconds(20)).Should().BeTrue("TE.exe and its test host should have been killed");
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
            ProcessTree.HasExited(teProcesses.Single()).Should().BeTrue();
            ProcessTree.HasExited(testHostProcesses.Single()).Should().BeTrue("TE.ProcessHost.exe keeps running if only TE.exe is killed");
        }

        #endregion

    }

}
