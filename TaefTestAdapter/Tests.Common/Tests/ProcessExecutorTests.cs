// This file has been modified for TAEF support.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution.Contracts;
using Moq;

namespace TaefTestAdapter.Tests.Common.Tests
{
    /// <summary>
    /// Test bodies shared by the tests of the <see cref="IProcessExecutor"/> implementations (derived classes set
    /// <see cref="ProcessExecutor"/> and call the bodies from their [TestMethod]s).
    /// </summary>
    public abstract class ProcessExecutorTests
    {
        protected Mock<ILogger> MockLogger { get; } = new Mock<ILogger>();
        protected IProcessExecutor ProcessExecutor { get; set; }

        public virtual void Teardown()
        {
            MockLogger.Reset();
        }

        protected void Test_ExecuteProcessBlocking_PingLocalHost()
        {
            List<string> output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(
                Path.Combine(Environment.SystemDirectory, "ping.exe"),
                "localhost",
                "",
                null,
                new Dictionary<string, string>(),
                s => output.Add(s));

            exitCode.Should().Be(0);
            output.Should().Contain(s => s.Contains("Ping"));
            output.Should().HaveCountGreaterOrEqualTo(11);
            output.Should().HaveCountLessOrEqualTo(12);
        }

        /// <summary>
        /// Runs TE.exe (x86) with DllTests_taef.dll (Debug, x86): 1 passing and 1 failing test, TE.exe exit code 1.
        /// </summary>
        protected void Test_ExecuteProcessBlocking_SampleTests()
        {
            string testDll = TestResources.DllTests_DebugX86;
            List<string> output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking(
                TestResources.GetTeExecutable(SampleConfiguration.DebugX86),
                $"\"{testDll}\" {TaefConstants.UnicodeOutputFalseOption} {TaefConstants.ColoredConsoleOutputFalseOption}",
                Path.GetDirectoryName(testDll),
                null,
                new Dictionary<string, string>(),
                s => output.Add(s));

            exitCode.Should().Be(1);
            output.Should().Contain(s => s.StartsWith("Test Authoring and Execution Framework v"));
            output.Should().Contain("StartGroup: " + TestResources.TestNames.DllTestsPassing);
            output.Should().Contain("EndGroup: " + TestResources.TestNames.DllTestsPassing + " [Passed]");
            output.Should().Contain("StartGroup: " + TestResources.TestNames.DllTestsFailing);
            output.Should().Contain(s => s.StartsWith("Error: Verify: AreEqual(1, ReturnZero()) - Values (1, 0) [File: ") && s.EndsWith(", Function: TaefSamples::Failing::InvokeFunction, Line: 24]"));
            output.Should().Contain("EndGroup: " + TestResources.TestNames.DllTestsFailing + " [Failed]");
            output.Should().Contain("Summary: Total=2, Passed=1, Failed=1, Blocked=0, Not Run=0, Skipped=0");
        }

        /// <summary>
        /// Runs TE.exe (x64) with the non-ASCII tests of Tests_taef.dll (Debug, x64): TE.exe's UTF-8 output must be decoded
        /// correctly.
        /// </summary>
        protected void Test_ExecuteProcessBlocking_SampleTestsWithUnicodeOutput()
        {
            string testDll = TestResources.Tests_DebugX64;
            List<string> output = new List<string>();
            ProcessExecutor.ExecuteCommandBlocking(
                TestResources.GetTeExecutable(SampleConfiguration.DebugX64),
                $"\"{testDll}\" {TaefConstants.UnicodeOutputFalseOption} {TaefConstants.ColoredConsoleOutputFalseOption} {TaefConstants.GetSelectOption(TaefConstants.GetNameSelectionTerm("TaefSamples::Ümlautß::*"))}",
                Path.GetDirectoryName(testDll),
                null,
                new Dictionary<string, string>(),
                s => output.Add(s));

            output.Should().Contain("StartGroup: TaefSamples::Ümlautß::Täst");
            output.Should().Contain("EndGroup: TaefSamples::Ümlautß::Täst [Failed]");
            output.Should().Contain("EndGroup: TaefSamples::Ümlautß::Träits [Passed]");
        }

        protected void Test_WithSimpleCommand_ReturnsOutputOfCommand()
        {
            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking("cmd.exe", "/C \"echo 2\"", ".", "", new Dictionary<string, string>(), line => output.Add(line));

            exitCode.Should().Be(0);
            output.Should().ContainSingle();
            output.Should().HaveElementAt(0, "2");
        }

        protected void Test_IgnoresIfProcessReturnsErrorCode_DoesNotThrow()
        {
            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking("cmd.exe", "/C \"echo 2 & exit /b 7\"", ".", "", new Dictionary<string, string>(), line => output.Add(line));

            exitCode.Should().Be(7);
            output.Should().Contain(s => s.Trim() == "2");
        }

        protected void Test_WithEnvSetting_EnvVariableIsSet()
        {
            string envVarName = "MyVar";
            string envVarValue = "MyValue";

            Environment.GetEnvironmentVariable(envVarName).Should().BeNull();

            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking("cmd.exe", "/C \"set\"", ".", "", new Dictionary<string, string> {{ envVarName, envVarValue}}, line => output.Add(line));

            exitCode.Should().Be(0);
            output.Should().Contain($"{envVarName}={envVarValue}");
            Environment.GetEnvironmentVariable(envVarName).Should().BeNull();
        }

        protected void Test_WithOverridingEnvSetting_EnvVariableHasNewValue()
        {
            string newValue = "NewValue";

            var envVar = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .First(v => !string.IsNullOrWhiteSpace(v.Value?.ToString()) && v.Value.ToString() != newValue);
            string valueBeforeChange = envVar.Value.ToString();

            var output = new List<string>();
            int exitCode = ProcessExecutor.ExecuteCommandBlocking("cmd.exe", "/C \"set\"", ".", "", new Dictionary<string, string> {{ envVar.Key.ToString(), newValue}}, line => output.Add(line));

            exitCode.Should().Be(0);
            output.Should().Contain($"{envVar.Key}={newValue}");
            Environment.GetEnvironmentVariable(envVar.Key.ToString()).Should().Be(valueBeforeChange);
        }

    }
}
