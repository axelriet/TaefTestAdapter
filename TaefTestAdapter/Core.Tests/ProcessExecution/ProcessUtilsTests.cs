// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Runners;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Common
{
    [TestClass]
    public class ProcessUtilsTests
    {
        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();

        private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

        private static Process StartProcess(string arguments)
        {
            return Process.Start(new ProcessStartInfo(Cmd, arguments) { UseShellExecute = false, CreateNoWindow = true });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void KillProcessTree_ProcessWithChildProcess_BothAreKilled()
        {
            using (Process cmd = StartProcess("/C \"ping -n 30 127.0.0.1 >NUL\""))
            {
                IList<int> pingProcesses = new List<int>();
                ProcessTree.WaitFor(() => (pingProcesses = ProcessTree.GetChildProcessIds(cmd.Id, "PING")).Any(), 10000).Should().BeTrue();

                ProcessUtils.KillProcessTree(cmd.Id, _mockLogger.Object);

                cmd.WaitForExit(5000).Should().BeTrue();
                ProcessTree.HasExited(pingProcesses.Single()).Should().BeTrue();
                _mockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void KillProcessTree_ProcessHasExitedButIsStillOpen_NoWarningIsLogged()
        {
            // the process executors keep a handle of the process they have started: the process can still be opened, but
            // TerminateProcess fails with ERROR_ACCESS_DENIED
            using (Process cmd = StartProcess("/C \"exit 0\""))
            {
                cmd.WaitForExit(10000).Should().BeTrue();

                ProcessUtils.KillProcessTree(cmd.Id, _mockLogger.Object);

                _mockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
                _mockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains($"Process {cmd.Id}") && s.EndsWith("has already exited"))), Times.Once);
            }
        }
    }
}
