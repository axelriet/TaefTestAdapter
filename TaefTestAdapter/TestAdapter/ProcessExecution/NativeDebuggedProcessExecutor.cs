// This file has been modified by Microsoft on 8/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Starts a process (TE.exe) suspended, attaches the Visual Studio debugger to it (via <see cref="IDebuggerAttacher"/>),
    /// and resumes it. If the debugger can not be attached, the process is terminated without having run: it has been
    /// started for debugging (TE.exe e.g. with <c>/breakOnError</c> and <c>/disableTimeouts</c>), and would crash or hang
    /// without a debugger. Standard output and standard error of the process are redirected into one pipe and decoded as
    /// UTF-8; the process runs in a job object together with its descendants (see <see cref="RedirectedProcess"/>).
    /// </summary>
    public class NativeDebuggedProcessExecutor : IDebuggedProcessExecutor
    {
        public const int ExecutionFailed = int.MaxValue;

        private readonly IDebuggerAttacher _debuggerAttacher;
        private readonly DebuggerEngine _debuggerEngine;
        private readonly bool _printTestOutput;
        private readonly ILogger _logger;
        private readonly TimeSpan _outputGracePeriod;

        private readonly object _lock = new object();
        private RedirectedProcess _process;
        private bool _cancelRequested;

        public NativeDebuggedProcessExecutor(IDebuggerAttacher debuggerAttacher, DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger)
            : this(debuggerAttacher, debuggerEngine, printTestOutput, logger, RedirectedProcess.DefaultOutputGracePeriod)
        {
        }

        /// <param name="debuggerAttacher">Attaches the debugger.</param>
        /// <param name="debuggerEngine">The debugger engine to be used.</param>
        /// <param name="printTestOutput">If true, the output of the process is logged.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="outputGracePeriod">How long to wait for the end of the output after the process has exited
        /// (see <see cref="RedirectedProcess.DefaultOutputGracePeriod"/>).</param>
        public NativeDebuggedProcessExecutor(IDebuggerAttacher debuggerAttacher, DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger,
            TimeSpan outputGracePeriod)
        {
            _debuggerAttacher = debuggerAttacher ?? throw new ArgumentNullException(nameof(debuggerAttacher));
            _debuggerEngine = debuggerEngine;
            _printTestOutput = printTestOutput;
            _logger = logger;
            _outputGracePeriod = outputGracePeriod;
        }

        /// <returns>The exit code of the process, or <see cref="ExecutionFailed"/> if the process could not be started or
        /// the debugger could not be attached (the reason has been logged as an error).</returns>
        public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension, IDictionary<string, string> environmentVariables, Action<string> reportOutputLine)
        {
            try
            {
                IDictionary<string, string> environmentVariablesToSet = ProcessEnvironment.GetEnvironmentVariablesToSet(pathExtension, environmentVariables, _logger);
                using (RedirectedProcess process = RedirectedProcess.StartSuspended(command, parameters, workingDir, environmentVariablesToSet, false, _logger))
                {
                    try
                    {
                        int exitCode = ExecuteCommandBlocking(process, command, parameters, reportOutputLine);
                        if (exitCode != ExecutionFailed)
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
            catch (Win32Exception ex)
            {
                string nativeErrorMessage = new Win32Exception(ex.NativeErrorCode).Message;
                _logger.LogError($"{ex.Message} ({ex.NativeErrorCode}: {nativeErrorMessage})");
                return ExecutionFailed;
            }
        }

        private int ExecuteCommandBlocking(RedirectedProcess process, string command, string parameters, Action<string> reportOutputLine)
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
                return ExecutionFailed;
            }

            _logger.DebugInfo($"Attaching debugger to '{command}' via {_debuggerEngine} engine");
            if (!_debuggerAttacher.AttachDebugger(process.ProcessId, _debuggerEngine))
            {
                // The (still suspended) process must never run without debugger, e.g. TE.exe with /breakOnError would
                // crash at the first failing test (or pop up a JIT debugger dialog), and with /disableTimeouts, a hanging
                // test would hang forever.
                process.Kill();
                _logger.LogError($"Could not attach debugger to process {process.ProcessId}, so it has been terminated without running any tests. Command: \"{command}\" {parameters}");
                return ExecutionFailed;
            }

            if (_printTestOutput)
            {
                DotNetProcessExecutor.LogStartOfOutput(_logger, command, parameters);
            }

            int exitCode = process.ResumeAndWaitForExit(
                line =>
                {
                    reportOutputLine?.Invoke(line);
                    if (_printTestOutput)
                    {
                        _logger.LogInfo(line);
                    }
                },
                _outputGracePeriod);

            if (_printTestOutput)
            {
                DotNetProcessExecutor.LogEndOfOutput(_logger);
            }

            return exitCode;
        }

        /// <summary>Kills the started process together with all its descendants.</summary>
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
    }
}
