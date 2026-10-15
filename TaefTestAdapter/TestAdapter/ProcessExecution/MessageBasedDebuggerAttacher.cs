// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Attaches the debugger by asking the debugger attacher service of the Visual Studio package via a named pipe.
    /// </summary>
    public class MessageBasedDebuggerAttacher : IDebuggerAttacher
    {
        private static readonly TimeSpan AttachDebuggerTimeout = TimeSpan.FromSeconds(10);

        private readonly ILogger _logger;
        private readonly string _debuggingNamedPipeId;
        private readonly TimeSpan _timeout;

        public MessageBasedDebuggerAttacher(string debuggingNamedPipeId, TimeSpan timeout, ILogger logger)
        {
            _debuggingNamedPipeId = debuggingNamedPipeId;
            _timeout = timeout;
            _logger = logger;
        }

        public MessageBasedDebuggerAttacher(string debuggingNamedPipeId, ILogger logger) : this(debuggingNamedPipeId, AttachDebuggerTimeout, logger)
        {
        }

        public bool AttachDebugger(int processId, DebuggerEngine debuggerEngine)
        {
            // This assembly also runs in modern .NET VSTest hosts. The existing service is hosted by the framework
            // Visual Studio package; keep its framework-only WCF client in a separate process in those hosts.
            return RequiresFrameworkBridge
                ? AttachUsingFrameworkBridge(processId, debuggerEngine)
                : AttachUsingWcf(processId, debuggerEngine);
        }

        /// <summary>Framework WCF is available in framework hosts; modern hosts use the companion executable.</summary>
        protected virtual bool RequiresFrameworkBridge => typeof(object).Assembly.GetName().Name != "mscorlib";

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool AttachUsingWcf(int processId, DebuggerEngine debuggerEngine)
        {
            try
            {
                var stopWatch = Stopwatch.StartNew();

                var proxy = DebuggerAttacherServiceConfiguration.CreateProxy(_debuggingNamedPipeId, _timeout);
                using (var client = new DebuggerAttacherServiceProxyWrapper(proxy))
                {
                    client.Service.AttachDebugger(processId, debuggerEngine);
                    stopWatch.Stop();
                    _logger.DebugInfo($"Debugger attached to process {processId}, took {stopWatch.ElapsedMilliseconds} ms");
                    return true;
                }
            }
            catch (FaultException<DebuggerAttacherServiceFault> serviceFault)
            {
                var errorMessage = serviceFault.Detail.Message;
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = $"Could not attach debugger to process {processId}, no error message available";

                errorMessage += $"{Environment.NewLine}There might be more information on the problem in Visual Studio's ActivityLog.xml (see e.g. https://blogs.msdn.microsoft.com/visualstudio/2010/02/24/troubleshooting-extensions-with-the-activity-log/)";

                _logger.LogError(errorMessage);
            }
            catch (Exception e)
            {
                _logger.LogError($"Could not attach debugger to process {processId}:{Environment.NewLine}{e}");
            }
            return false;
        }

        private bool AttachUsingFrameworkBridge(int processId, DebuggerEngine debuggerEngine)
        {
            string bridge = Path.Combine(Path.GetDirectoryName(typeof(MessageBasedDebuggerAttacher).Assembly.Location),
                "TaefTestAdapter.DebuggerBridge.exe");
            try
            {
                var startInfo = new ProcessStartInfo(bridge)
                {
                    Arguments = $"{processId} {(int) debuggerEngine} {_debuggingNamedPipeId} {(int) _timeout.TotalMilliseconds}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (var process = Process.Start(startInfo))
                {
                    var standardError = process.StandardError.ReadToEndAsync();
                    var standardOutput = process.StandardOutput.ReadToEndAsync();
                    if (!process.WaitForExit((int) _timeout.TotalMilliseconds + 5000))
                    {
                        process.Kill();
                        _logger.LogError($"Timed out waiting for the debugger bridge to attach to process {processId}.");
                        return false;
                    }
                    if (process.ExitCode != 0)
                    {
                        _logger.LogError($"Could not attach debugger to process {processId}: {standardError.GetAwaiter().GetResult().Trim()}");
                        return false;
                    }
                    _logger.DebugInfo($"Debugger attached to process {processId} through the framework bridge.");
                    return true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Could not start the debugger bridge '{bridge}' for process {processId}: {e.Message}");
                return false;
            }
        }
    }

}
