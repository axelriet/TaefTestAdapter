// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Settings;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Launches a process (TE.exe) via the VsTest framework with the debugger attached. The output of the process is not
    /// available. The environment variables passed in are not modified (the path extension is added to a copy of them).
    /// </summary>
    public class FrameworkDebuggedProcessExecutor : IDebuggedProcessExecutor
    {
        private readonly IFrameworkHandle _frameworkHandle;
        private readonly bool _printTestOutput;
        private readonly ILogger _logger;
        
        private int? _processId;

        public FrameworkDebuggedProcessExecutor(IFrameworkHandle handle, bool printTestOutput, ILogger logger)
        {
            _frameworkHandle = handle;
            _printTestOutput = printTestOutput;
            _logger = logger;
        }

        public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension, IDictionary<string, string> environmentVariables,
            Action<string> reportOutputLine)
        {
            if (reportOutputLine != null)
            {
                throw new ArgumentException(nameof(reportOutputLine));
            }
            if (_processId.HasValue)
            {
                throw new InvalidOperationException();
            }

            // a new dictionary: the caller's one is not modified (it is e.g. used for several TE.exe processes)
            IDictionary<string, string> environmentVariablesToSet = ProcessEnvironment.GetEnvironmentVariablesToSet(pathExtension, environmentVariables, _logger);

            _logger.DebugInfo($"Attaching debugger to '{command}' via {DebuggerKind.VsTestFramework} engine");
            if (_printTestOutput)
            {
                _logger.DebugInfo(
                    $"Note that due to restrictions of the VsTest framework, the output of TE.exe can not be displayed in the test console when debugging tests. Use option '{SettingsWrapper.OptionDebuggerKind}' to overcome this problem.");
            }

            _processId = _frameworkHandle.LaunchProcessWithDebuggerAttached(command, workingDir, parameters, environmentVariablesToSet);

            ProcessWaiter waiter;
            using (var process = Process.GetProcessById(_processId.Value))
            {
                waiter = new ProcessWaiter(process);
                waiter.WaitForExit();
            }

            _logger.DebugInfo($"Process '{command}' returned with exit code {waiter.ProcessExitCode}");
            return waiter.ProcessExitCode;
        }

        /// <summary>Kills the launched process together with all its child processes.</summary>
        public void Cancel()
        {
            if (_processId.HasValue)
            {
                ProcessUtils.KillProcessTree(_processId.Value, _logger);
            }
        }

    }

}