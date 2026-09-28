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
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Tests of the environment of TE.exe while debugging with the VsTest framework's debugger:
    /// <see cref="Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IFrameworkHandle.LaunchProcessWithDebuggerAttached"/>
    /// is mocked to record the environment variables and to start a dummy process instead of TE.exe.
    /// </summary>
    [TestClass]
    public class TestExecutorFrameworkDebuggingEnvironmentTests : TestAdapterTestsBase
    {
        private const string PathExtension = @"C:\TaefAdapterTestPathExtension";

        private readonly List<(string Arguments, IDictionary<string, string> Environment)> _launches = new List<(string, IDictionary<string, string>)>();
        private readonly List<Process> _launchedProcesses = new List<Process>();

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            MockOptions.Setup(o => o.DebuggingNamedPipeId).Returns((string)null);
            MockRunContext.Setup(c => c.IsBeingDebugged).Returns(true);

            MockFrameworkHandle
                .Setup(h => h.LaunchProcessWithDebuggerAttached(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>()))
                .Returns((string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables) =>
                {
                    Process process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/C \"exit 0\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    lock (_launches)
                    {
                        _launches.Add((arguments, new Dictionary<string, string>(environmentVariables)));
                        _launchedProcesses.Add(process);
                    }
                    return process.Id;
                });
        }

        [TestCleanup]
        public override void TearDown()
        {
            _launchedProcesses.ForEach(p => p.Dispose());
            base.TearDown();
        }

        private TestExecutor CreateExecutor()
        {
            return new TestExecutor(TestEnvironment.Logger, TestEnvironment.Options, new Mock<IDebuggerAttacher>().Object);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_PathExtensionAndSeveralCommandLines_NoWarningAboutPathVariable()
        {
            MockOptions.Setup(o => o.PathExtension).Returns(PathExtension);
            string testDll = CopySample(TestResources.LoadTests_ReleaseX86).TestDll;
            // every other test: the selection does not fit into one command line
            string[] testNames = TestDataCreator.GetTestCasesOfTestDll(testDll)
                .Select(tc => tc.FullyQualifiedName)
                .Where((name, index) => index % 2 == 0)
                .ToArray();
            IList<VsTestCase> testCases = ToVsTestCases(TestDataCreator.GetTestCasesOfTestDll(testDll), testNames);

            CreateExecutor().RunTests(testCases, MockRunContext.Object, MockFrameworkHandle.Object);

            _launches.Should().HaveCountGreaterOrEqualTo(2);
            _launches.Should().OnlyContain(l => l.Environment.Count == 1 && l.Environment["PATH"].StartsWith(PathExtension + ";"));
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Both a path extension and a PATH environment variable"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_PathExtensionAndUserPathVariableWithOtherCase_PathExtensionWins()
        {
            MockOptions.Setup(o => o.PathExtension).Returns(PathExtension);
            // discovered before the variable is set: discovery would log the warning, too
            List<Model.TestCase> discoveredTestCases = TestDataCreator.GetTestCasesOfTestDll(CopySample(TestResources.DllTests_ReleaseX86).TestDll);
            IList<VsTestCase> testCases = ToVsTestCases(discoveredTestCases, discoveredTestCases.Select(tc => tc.FullyQualifiedName).ToArray());
            MockOptions.Setup(o => o.EnvironmentVariables).Returns(@"Path=C:\user\path");

            CreateExecutor().RunTests(testCases, MockRunContext.Object, MockFrameworkHandle.Object);

            _launches.Should().ContainSingle();
            _launches[0].Environment.Keys.Should().Equal("PATH");
            _launches[0].Environment["PATH"].Should().StartWith(PathExtension + ";");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Both a path extension and a PATH environment variable"))), Times.Once);
        }
    }
}
