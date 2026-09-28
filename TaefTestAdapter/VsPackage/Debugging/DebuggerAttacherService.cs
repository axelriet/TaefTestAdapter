// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using TaefTestAdapter.Common;
using System.ServiceModel;
using TaefTestAdapter.TestAdapter.ProcessExecution;

namespace TaefTestAdapter.VsPackage.Debugging
{
    /// <summary>
    /// Implements IDebuggerAttacherService to expose Visual Studio interfaces for the out-of-process adapter.
    /// Requests are processed one at a time on a thread pool thread (independent of the thread which has opened
    /// the service host); the <see cref="IDebuggerAttacher"/> is responsible for switching to the UI thread.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single, UseSynchronizationContext = false)]
    public sealed class DebuggerAttacherService : IDebuggerAttacherService
    {
        private readonly IDebuggerAttacher _debuggerAttacher;
        private readonly ILogger _logger;

        public DebuggerAttacherService(IDebuggerAttacher debuggerAttacher, ILogger logger)
        {
            _debuggerAttacher = debuggerAttacher;
            _logger = logger;
        }

        public void AttachDebugger(int processId, DebuggerEngine debuggerEngine)
        {
            bool success = false;
            try
            {
                success = _debuggerAttacher.AttachDebugger(processId, debuggerEngine);
            }
            catch (Exception e)
            {
                ThrowFaultException($"Could not attach debugger to process {processId} because of exception on the server side:{Environment.NewLine}{e}");
            }
            if (!success)
            {
                ThrowFaultException($"Could not attach debugger to process {processId} for unknown reasons");
            }
        }

        private void ThrowFaultException(string message)
        {
            throw new FaultException<DebuggerAttacherServiceFault>(new DebuggerAttacherServiceFault(message));
        }
    }

    /// <summary>
    /// Host of the debugger attacher service (WCF, named pipe).
    /// </summary>
    public class DebuggerAttacherServiceHost : ServiceHost
    {
        /// <summary>
        /// Constructs the host for DebuggerAttacherService.
        /// </summary>
        /// <param name="id">Identifier of the service end-point</param>
        public DebuggerAttacherServiceHost(string id, IDebuggerAttacher debuggerAttacher, ILogger logger) :
            base(new DebuggerAttacherService(debuggerAttacher, logger), new Uri[] {
                DebuggerAttacherServiceConfiguration.ConstructPipeUri(id)
            })
        {
            AddServiceEndpoint(typeof(IDebuggerAttacherService), new NetNamedPipeBinding(), DebuggerAttacherServiceConfiguration.InterfaceAddress);
        }
    }
}