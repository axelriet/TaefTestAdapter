// This file has been added for TAEF support.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// A fake TE.exe (batch file <c>TE.cmd</c>, to be used as value of option TeExecutable) in a temporary directory,
    /// together with an (empty) fake test DLL <see cref="TestDll"/>. When run, the fake TE.exe records its arguments,
    /// working directory, PATH and the value of environment variable <see cref="EnvironmentVariableName"/> in files of the
    /// temporary directory, prints a listing (e.g. a captured TE.exe output), optionally sleeps, and exits with the given
    /// exit code. Only cmd.exe (and ping.exe for sleeping) are needed, no built binaries.
    /// </summary>
    internal sealed class FakeTe : IDisposable
    {
        public const string EnvironmentVariableName = "TAEF_FAKE_TE_TESTVARIABLE";

        private readonly TemporaryDirectory _directory = new TemporaryDirectory();

        public string Directory => _directory.Path;

        /// <summary>The fake TE.exe (a batch file).</summary>
        public string TeExecutable => _directory.GetPath("TE.cmd");

        /// <summary>An empty file named like a TAEF test DLL (it is not a PE file).</summary>
        public string TestDll => _directory.GetPath("Fake_taef.dll");

        /// <returns>The arguments TE.cmd has been called with, or null if it has not been run.</returns>
        public string GetArguments() => ReadRecordedValue("arguments.txt");

        /// <returns>The working directory of the last run of TE.cmd, or null if it has not been run.</returns>
        public string GetWorkingDirectory() => ReadRecordedValue("workingdir.txt");

        /// <returns>The value of the PATH variable of the last run of TE.cmd, or null if it has not been run.</returns>
        public string GetPath() => ReadRecordedValue("path.txt");

        /// <returns>The value of <see cref="EnvironmentVariableName"/> of the last run of TE.cmd, or null if it has not been run.</returns>
        public string GetEnvironmentVariableValue() => ReadRecordedValue("environment.txt");

        public bool HasBeenRun => File.Exists(_directory.GetPath("arguments.txt"));

        private FakeTe()
        {
        }

        /// <param name="listing">Output to be printed (lines), or null.</param>
        /// <param name="exitCode">Exit code of the fake TE.exe.</param>
        /// <param name="sleepSeconds">Seconds to wait before printing the output.</param>
        public static FakeTe Create(string[] listing = null, int exitCode = 0, int sleepSeconds = 0)
        {
            var fakeTe = new FakeTe();
            try
            {
                File.WriteAllBytes(fakeTe.TestDll, new byte[0]);
                if (listing != null)
                    File.WriteAllText(fakeTe._directory.GetPath("listing.txt"), string.Join("\r\n", listing) + "\r\n", new UTF8Encoding(false));

                var script = new StringBuilder();
                script.AppendLine("@echo off");
                script.AppendLine("> \"%~dp0arguments.txt\" echo(%*");
                script.AppendLine("> \"%~dp0workingdir.txt\" echo(%CD%");
                script.AppendLine("> \"%~dp0path.txt\" echo(%PATH%");
                script.AppendLine($"> \"%~dp0environment.txt\" echo(%{EnvironmentVariableName}%");
                if (sleepSeconds > 0)
                    script.AppendLine($"ping -n {(sleepSeconds + 1).ToString(CultureInfo.InvariantCulture)} 127.0.0.1 > nul");
                script.AppendLine("if exist \"%~dp0listing.txt\" type \"%~dp0listing.txt\"");
                script.AppendLine($"exit /b {exitCode.ToString(CultureInfo.InvariantCulture)}");
                File.WriteAllText(fakeTe.TeExecutable, script.ToString(), Encoding.ASCII);

                return fakeTe;
            }
            catch
            {
                fakeTe.Dispose();
                throw;
            }
        }

        /// <summary>Creates a fake TE.exe printing a captured TE.exe output (see <see cref="TestResources.TaefOutputDir"/>).</summary>
        public static FakeTe CreateFromCapturedOutput(string capturedOutputFile, int exitCode = 0)
        {
            return Create(TestResources.ReadTaefOutputLines(capturedOutputFile), exitCode);
        }

        private string ReadRecordedValue(string file)
        {
            string path = _directory.GetPath(file);
            if (!File.Exists(path))
                return null;

            string value = File.ReadAllText(path, Encoding.Default).TrimEnd('\r', '\n');
            // "echo(%VAR%" prints "%VAR%" if the variable is not set
            return value.StartsWith("%", StringComparison.Ordinal) && value.EndsWith("%", StringComparison.Ordinal) && value.Length > 1
                ? ""
                : value;
        }

        public void Dispose()
        {
            _directory.Dispose();
        }
    }

}
