// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Runs the tests of <see cref="TestExecutorSequentialTests"/> while "debugging" with the VsTest framework's debugger:
    /// <see cref="Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter.IFrameworkHandle.LaunchProcessWithDebuggerAttached"/>
    /// is mocked to start the process without debugger (and without access to its output, as the VsTest framework does).
    /// The adapter runs TE.exe with <c>/inproc</c> and takes the results from a WTT log written by TE.exe.
    /// </summary>
    [TestClass]
    // ReSharper disable once InconsistentNaming
    public class TestExecutorSequentialTests_FrameworkDebugging : TestExecutorSequentialTests
    {
        private class LaunchedProcess
        {
            public string FilePath;
            public string WorkingDirectory;
            public string Arguments;
            public Process Process;
        }

        private readonly List<LaunchedProcess> _launchedProcesses = new List<LaunchedProcess>();

        /// <summary>If true, a dummy process is launched instead of the requested one (e.g. TE.exe with /breakOnError, which must not be run without a debugger).</summary>
        private bool _launchDummyProcess;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);

            MockRunContext.Setup(c => c.IsBeingDebugged).Returns(true);
            SetUpMockFrameworkHandle();
        }

        [TestCleanup]
        public override void TearDown()
        {
            lock (_launchedProcesses)
            {
                foreach (LaunchedProcess launchedProcess in _launchedProcesses)
                {
                    try
                    {
                        if (!launchedProcess.Process.HasExited)
                            ProcessUtils.KillProcessTree(launchedProcess.Process.Id, MockLogger.Object);
                    }
                    catch (InvalidOperationException)
                    {
                        // process has not been started
                    }
                    launchedProcess.Process.Dispose();
                }
                _launchedProcesses.Clear();
            }

            base.TearDown();
        }

        private void SetUpMockFrameworkHandle()
        {
            MockFrameworkHandle.Setup(
                    h => h.LaunchProcessWithDebuggerAttached(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<IDictionary<string, string>>()))
                .Returns((string filePath,
                    string workingDirectory,
                    string arguments,
                    IDictionary<string, string> environmentVariables) =>
                {
                    ProcessStartInfo processStartInfo = _launchDummyProcess
                        ? new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/C \"exit 0\"")
                        : new ProcessStartInfo(filePath, arguments);
                    processStartInfo.WorkingDirectory = workingDirectory;
                    processStartInfo.UseShellExecute = false;
                    processStartInfo.CreateNoWindow = true;
                    // the output is not available to the adapter (and must not end up on the test console)
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    foreach (var kvp in environmentVariables)
                    {
                        processStartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }

                    var process = new Process { StartInfo = processStartInfo };
                    process.OutputDataReceived += (sender, args) => { };
                    process.ErrorDataReceived += (sender, args) => { };
                    lock (_launchedProcesses)
                    {
                        _launchedProcesses.Add(new LaunchedProcess { FilePath = filePath, WorkingDirectory = workingDirectory, Arguments = arguments, Process = process });
                    }
                    if (!process.Start())
                        throw new Exception("Process could not be started: " + filePath);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    return process.Id;
                });
        }

        private IList<LaunchedProcess> GetLaunchedProcesses()
        {
            lock (_launchedProcesses)
            {
                return _launchedProcesses.ToList();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_DebuggingWithVsTestFramework_TeIsLaunchedInProcessWithWttLog()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            // the VsTest framework's debugger does not need the VS package
            MockOptions.Setup(o => o.DebuggingNamedPipeId).Returns((string)null);

            RunAndVerifyTests(testDll, 1, 1);

            LaunchedProcess launchedProcess = GetLaunchedProcesses().Single();
            launchedProcess.FilePath.Should().EndWithEquivalent(@"\x86\TE.exe");
            launchedProcess.WorkingDirectory.Should().Be(Path.GetDirectoryName(testDll));
            launchedProcess.Arguments.Should().StartWith($"\"{testDll}\" ");
            launchedProcess.Arguments.Should().Contain(" " + TaefConstants.InProcOption);
            launchedProcess.Arguments.Should().Contain(" " + TaefConstants.DisableTimeoutsOption);
            launchedProcess.Arguments.Should().Contain(" " + TaefConstants.EnableWttLoggingOption);
            launchedProcess.Arguments.Should().NotContain(TaefConstants.BreakOnErrorOption);

            // the WTT log has been deleted
            System.Text.RegularExpressions.Match logFile = Regex.Match(launchedProcess.Arguments, Regex.Escape(TaefConstants.LogFileOption) + "\"(?<file>[^\"]+)\"");
            logFile.Success.Should().BeTrue();
            File.Exists(logFile.Groups["file"].Value).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_BreakOnError_TeIsLaunchedWithBreakOnErrorSwitch()
        {
            string testDll = CopySample(TestResources.DllTests_ReleaseX86).TestDll;
            MockOptions.Setup(o => o.BreakOnError).Returns(true);
            // TE.exe with /breakOnError must not run without debugger
            _launchDummyProcess = true;

            RunTests(testDll);

            GetLaunchedProcesses().Single().Arguments.Should().Contain(" " + TaefConstants.BreakOnErrorOption);
            // the dummy process does not write a WTT log
            CheckMockInvocations(0, 0, 0, 0, TestResources.NrOfDllTests);
        }

        // TE.exe dies in TaefSamples::Crashing::TheCrash before completing its WTT log: the results are read from the incomplete log
        // it wrote while the tests were running, so option MissingTestsReportMode does not apply to these tests

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_CrashingX64Tests_DoNotReport_ResultsFromIncompleteWttLog()
        {
            RunCrashingTestsAndCheckResultsFromIncompleteWttLog(TestResources.CrashingTests_ReleaseX64, MissingTestsReportMode.DoNotReport);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_CrashingX64Tests_ReportAsFailed_ResultsFromIncompleteWttLog()
        {
            RunCrashingTestsAndCheckResultsFromIncompleteWttLog(TestResources.CrashingTests_ReleaseX64, MissingTestsReportMode.ReportAsFailed);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_CrashingX86Tests_ReportAsSkipped_ResultsFromIncompleteWttLog()
        {
            RunCrashingTestsAndCheckResultsFromIncompleteWttLog(TestResources.CrashingTests_ReleaseX86, MissingTestsReportMode.ReportAsSkipped);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void RunTests_HardCrashingX86Tests_ReportAsNotFound_ResultsFromIncompleteWttLog()
        {
            RunCrashingTestsAndCheckResultsFromIncompleteWttLog(TestResources.CrashingTests_DebugX86, MissingTestsReportMode.ReportAsNotFound);
        }

        private void RunCrashingTestsAndCheckResultsFromIncompleteWttLog(string crashingTestsDll, MissingTestsReportMode missingTestsReportMode)
        {
            MockOptions.Setup(o => o.MissingTestsReportMode).Returns(missingTestsReportMode);

            RunTests(CopySample(crashingTestsDll).TestDll);

            CheckCrashingTestsResults();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("did not write a WTT log"))), Times.Never);
            System.Text.RegularExpressions.Match logFile = Regex.Match(GetLaunchedProcesses().Single().Arguments, Regex.Escape(TaefConstants.LogFileOption) + "\"(?<file>[^\"]+)\"");
            logFile.Success.Should().BeTrue();
            File.Exists(logFile.Groups["file"].Value + WttLogParser.TraceFileExtension).Should().BeFalse("the incomplete WTT log is deleted after it has been read");
        }

    }
}
