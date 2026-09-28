// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Text;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution.Contracts;

namespace TaefTestAdapter.ProcessExecution
{

    /// <summary>
    /// Executes a process (usually TE.exe) with redirected output (see <see cref="RedirectedProcess"/>). Standard output
    /// and standard error share one pipe, i.e. their lines keep their order (under <c>TE.exe /inproc</c>, standard error
    /// carries the test's own <c>std::cerr</c> output). The output is decoded as UTF-8 (TE.exe writes UTF-8 if its output
    /// is redirected and switch <c>/unicodeOutput:false</c> is passed); lines are reported one at a time (never
    /// concurrently). Once the process has exited, processes started by it which keep the output pipe open (e.g. helper
    /// processes left behind by tests) are not waited for. <see cref="Cancel"/> kills the process together with all its
    /// descendants (e.g. TE.exe together with its test host processes TE.ProcessHost.exe).
    /// </summary>
    public class DotNetProcessExecutor : IProcessExecutor
    {
        /// <summary>Encoding of the output of the processes (UTF-8 without BOM).</summary>
        public static readonly Encoding OutputEncoding = RedirectedProcess.OutputEncoding;

        private readonly bool _printTestOutput;
        private readonly ILogger _logger;
        private readonly TimeSpan _outputGracePeriod;

        private readonly object _lock = new object();
        private RedirectedProcess _process;
        private bool _cancelRequested;

        public static void LogStartOfOutput(ILogger logger, string command, string parameters)
        {
            logger.LogInfo(
                ">>>>>>>>>>>>>>> Output of command '\"" + command + "\" " + parameters + "'");
        }

        public static void LogEndOfOutput(ILogger logger)
        {
            logger.LogInfo("<<<<<<<<<<<<<<< End of Output");
        }

        public DotNetProcessExecutor(bool printTestOutput, ILogger logger)
            : this(printTestOutput, logger, RedirectedProcess.DefaultOutputGracePeriod)
        {
        }

        /// <param name="printTestOutput">If true, the output of the process is logged.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="outputGracePeriod">How long to wait for the end of the output after the process has exited
        /// (see <see cref="RedirectedProcess.DefaultOutputGracePeriod"/>).</param>
        public DotNetProcessExecutor(bool printTestOutput, ILogger logger, TimeSpan outputGracePeriod)
        {
            _printTestOutput = printTestOutput;
            _logger = logger;
            _outputGracePeriod = outputGracePeriod;
        }

        /// <param name="command">The executable to be started.</param>
        /// <param name="parameters">The complete argument string (passed as is, i.e. it must already be quoted properly).</param>
        /// <param name="workingDir">The working directory of the process.</param>
        /// <param name="pathExtension">Prepended to the PATH environment variable of the process (if not empty); wins over
        /// a PATH variable of <paramref name="environmentVariables"/> (see <see cref="ProcessEnvironment.GetEnvironmentVariablesToSet"/>).</param>
        /// <param name="environmentVariables">Additional environment variables of the process (not modified).</param>
        /// <param name="reportOutputLine">Receives each line of output (may be null).</param>
        /// <returns>The exit code of the process.</returns>
        /// <exception cref="System.ComponentModel.Win32Exception">If the process can not be started.</exception>
        public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension, IDictionary<string, string> environmentVariables,
            Action<string> reportOutputLine)
        {
            IDictionary<string, string> environmentVariablesToSet = ProcessEnvironment.GetEnvironmentVariablesToSet(pathExtension, environmentVariables, _logger);

            void ReportOutputLine(string line)
            {
                reportOutputLine?.Invoke(line);
                if (_printTestOutput)
                {
                    _logger.LogInfo(line);
                }
            }

            if (_printTestOutput)
            {
                LogStartOfOutput(_logger, command, parameters);
            }

            using (RedirectedProcess process = RedirectedProcess.StartSuspended(command, parameters, workingDir, environmentVariablesToSet, true, _logger))
            {
                try
                {
                    OnProcessStarted(process);

                    int exitCode = process.ResumeAndWaitForExit(ReportOutputLine, _outputGracePeriod);

                    if (_printTestOutput)
                    {
                        LogEndOfOutput(_logger);
                    }

                    _logger.DebugInfo($"Process '{command}' returned with exit code {exitCode}");
                    return exitCode;
                }
                finally
                {
                    lock (_lock)
                    {
                        _process = null;
                    }
                }
            }
        }

        /// <summary>
        /// Kills the process started by <see cref="ExecuteCommandBlocking"/> together with all its descendants (even those
        /// whose parent has already terminated). If the process has not been started yet, it is killed as soon as it has
        /// been created (i.e., before it has been resumed).
        /// </summary>
        public void Cancel()
        {
            RedirectedProcess process;
            lock (_lock)
            {
                _cancelRequested = true;
                process = _process;
            }
            process?.Kill();
        }

        private void OnProcessStarted(RedirectedProcess process)
        {
            bool cancelRequested;
            lock (_lock)
            {
                _process = process;
                cancelRequested = _cancelRequested;
            }

            if (cancelRequested)
            {
                _logger.DebugInfo($"Execution has been canceled while process {process.ProcessId} was being started, killing it");
                process.Kill();
            }
        }

    }

}
