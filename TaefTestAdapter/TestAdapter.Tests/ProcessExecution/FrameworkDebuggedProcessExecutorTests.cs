// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Tests of <see cref="FrameworkDebuggedProcessExecutor"/> with a mocked <see cref="IFrameworkHandle"/>, which
    /// records the environment passed to it and starts a dummy process.
    /// </summary>
    [TestClass]
    public class FrameworkDebuggedProcessExecutorTests
    {
        private const string PathExtension = @"C:\TaefAdapterTestPathExtension";

        private readonly Mock<ILogger> _mockLogger = new Mock<ILogger>();
        private readonly Mock<IFrameworkHandle> _mockFrameworkHandle = new Mock<IFrameworkHandle>();
        private readonly List<IDictionary<string, string>> _passedEnvironments = new List<IDictionary<string, string>>();
        // kept open until the end of the test: the executor accesses the process by its id
        private readonly List<Process> _launchedProcesses = new List<Process>();

        [TestInitialize]
        public void Setup()
        {
            _mockFrameworkHandle
                .Setup(h => h.LaunchProcessWithDebuggerAttached(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                .Returns((string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables) =>
                {
                    // copy: the dictionary must not be changed afterwards
                    _passedEnvironments.Add(new Dictionary<string, string>(environmentVariables));
                    Process process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/C \"exit 0\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    _launchedProcesses.Add(process);
                    return process.Id;
                });
        }

        [TestCleanup]
        public void TearDown()
        {
            _launchedProcesses.ForEach(p => p.Dispose());
        }

        private int Execute(IDictionary<string, string> environmentVariables, string pathExtension = PathExtension)
        {
            var executor = new FrameworkDebuggedProcessExecutor(_mockFrameworkHandle.Object, false, _mockLogger.Object);
            return executor.ExecuteCommandBlocking(@"C:\TAEF\x64\TE.exe", "\"test.dll\" /inproc", ".", pathExtension, environmentVariables, null);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_PathExtension_PassedDictionaryIsNotModified()
        {
            var environmentVariables = new Dictionary<string, string> { { "MYVAR", "MyValue" } };

            Execute(environmentVariables);

            environmentVariables.Should().Equal(new Dictionary<string, string> { { "MYVAR", "MyValue" } });
            IDictionary<string, string> passedEnvironment = _passedEnvironments.Single();
            passedEnvironment.Should().HaveCount(2);
            passedEnvironment["MYVAR"].Should().Be("MyValue");
            passedEnvironment["PATH"].Should().Be($"{PathExtension};{Environment.GetEnvironmentVariable("PATH")}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_SameDictionaryForSeveralProcesses_NoWarningAboutPathVariable()
        {
            // e.g. SequentialTestRunner, which passes the environment variables of a test DLL for all its command lines
            var environmentVariables = new Dictionary<string, string>();

            Execute(environmentVariables);
            Execute(environmentVariables);
            Execute(environmentVariables);

            environmentVariables.Should().BeEmpty();
            _passedEnvironments.Should().HaveCount(3);
            _passedEnvironments.Should().OnlyContain(e => e.Count == 1 && e["PATH"].StartsWith(PathExtension + ";"));
            _mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_PathExtensionAndUserPathVariableWithOtherCase_PathExtensionWinsWithWarning()
        {
            var environmentVariables = new Dictionary<string, string> { { "Path", @"C:\user\path" } };

            Execute(environmentVariables);

            IDictionary<string, string> passedEnvironment = _passedEnvironments.Single();
            passedEnvironment.Keys.Should().Equal("PATH");
            passedEnvironment["PATH"].Should().StartWith(PathExtension + ";");
            _mockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Both a path extension and a PATH environment variable (named 'Path')"))), Times.Once);
            environmentVariables.Should().Equal(new Dictionary<string, string> { { "Path", @"C:\user\path" } });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteCommandBlocking_NoPathExtension_UserVariablesArePassedAsIs()
        {
            var environmentVariables = new Dictionary<string, string> { { "Path", @"C:\user\path" }, { "MYVAR", "MyValue" } };

            int exitCode = Execute(environmentVariables, pathExtension: "");

            exitCode.Should().Be(0);
            _passedEnvironments.Single().Should().Equal(environmentVariables);
            _mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }
    }
}
