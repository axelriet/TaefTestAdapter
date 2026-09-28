// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.ProcessExecution
{
    /// <summary>
    /// Tests of <see cref="RedirectedProcess"/> which are not covered by the tests of the process executors using it:
    /// the process is created suspended, and it runs in a job object which is killed if its owner does not wait for it.
    /// </summary>
    [TestClass]
    public class RedirectedProcessTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

        [TestMethod]
        [TestCategory(Unit)]
        public void StartSuspended_DisposedWithoutBeingResumed_ProcessIsKilledWithoutHavingRun()
        {
            using (var directory = new TemporaryDirectory())
            {
                string markerFile = Path.Combine(directory.Path, "marker.txt");
                int processId;
                using (RedirectedProcess process = RedirectedProcess.StartSuspended(Cmd, $"/C \"echo ran > \"{markerFile}\"\"", directory.Path,
                    new Dictionary<string, string>(), true, _mockLogger.Object))
                {
                    processId = process.ProcessId;
                    ProcessTree.HasExited(processId, 500).Should().BeFalse("the process is suspended");
                    // e.g. "Could not create process within job object"
                    _mockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
                }

                ProcessTree.HasExited(processId).Should().BeTrue();
                File.Exists(markerFile).Should().BeFalse("the process must not have run");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Kill_BeforeResume_ProcessDoesNotRunAndExitsWithCodeOne()
        {
            using (var directory = new TemporaryDirectory())
            {
                string markerFile = Path.Combine(directory.Path, "marker.txt");
                using (RedirectedProcess process = RedirectedProcess.StartSuspended(Cmd, $"/C \"echo ran > \"{markerFile}\"\"", directory.Path,
                    new Dictionary<string, string>(), true, _mockLogger.Object))
                {
                    process.Kill();
                    var output = new List<string>();

                    int exitCode = process.ResumeAndWaitForExit(output.Add, TimeSpan.FromSeconds(1));

                    exitCode.Should().Be(1);
                    output.Should().BeEmpty();
                }
                File.Exists(markerFile).Should().BeFalse("the process must not have run");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void StartSuspended_ExecutableDoesNotExist_ThrowsWin32Exception()
        {
            Action start = () => RedirectedProcess.StartSuspended(@"C:\does\not\exist\TE.exe", "", ".", null, true, _mockLogger.Object);

            start.Should().Throw<System.ComponentModel.Win32Exception>().Which.Message.Should().StartWith("Could not create process.");
        }

        /// <summary>
        /// If the process owning the job (e.g. the test host of the adapter) terminates, the job's processes (e.g. TE.exe
        /// and TE.ProcessHost.exe, which would otherwise keep running and keep the test DLL locked) are killed, too. Runs
        /// the process executor in a PowerShell process, which is killed while ping.exe is running.
        /// </summary>
        [TestMethod]
        [TestCategory(Integration)]
        public void ExecuteCommandBlocking_OwnerProcessIsKilled_ProcessesOfJobAreKilled()
        {
            string commonDll = typeof(ILogger).Assembly.Location;
            string coreDll = typeof(RedirectedProcess).Assembly.Location;
            string ping = Path.Combine(Environment.SystemDirectory, "PING.EXE");
            using (var directory = new TemporaryDirectory())
            {
                string script = directory.CreateFile("RunPing.ps1", $@"
$ErrorActionPreference = 'Stop'
Add-Type -Path '{commonDll}'
Add-Type -Path '{coreDll}'
Add-Type -ReferencedAssemblies '{commonDll}' -TypeDefinition @'
using System.Collections.Generic;
using TaefTestAdapter.Common;
public class NoLogger : ILogger
{{
    public void LogInfo(string message) {{ }}
    public void LogWarning(string message) {{ }}
    public void LogError(string message) {{ }}
    public void DebugInfo(string message) {{ }}
    public void DebugWarning(string message) {{ }}
    public void DebugError(string message) {{ }}
    public void VerboseInfo(string message) {{ }}
    public IList<string> GetMessages(params Severity[] severities) {{ return new List<string>(); }}
}}
'@
$executor = New-Object TaefTestAdapter.ProcessExecution.DotNetProcessExecutor($false, (New-Object NoLogger))
$executor.ExecuteCommandBlocking('{ping}', '-n 60 127.0.0.1', '.', $null, $null, $null)
");
                var startInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                    $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process powerShell = Process.Start(startInfo))
                {
                    IList<int> pingProcesses = new List<int>();
                    try
                    {
                        ProcessTree.WaitFor(() => (pingProcesses = ProcessTree.GetChildProcessIds(powerShell.Id, "PING")).Any(), 60000)
                            .Should().BeTrue("PowerShell should have started ping.exe");
                    }
                    finally
                    {
                        powerShell.Kill();
                        powerShell.WaitForExit(10000);
                    }

                    pingProcesses.Should().ContainSingle();
                    ProcessTree.HasExited(pingProcesses.Single(), 10000).Should().BeTrue("ping.exe must be killed together with its owner");
                }
            }
        }

    }
}
