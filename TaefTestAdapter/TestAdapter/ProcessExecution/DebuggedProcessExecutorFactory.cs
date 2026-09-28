// This file has been modified for TAEF support.

using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Creates process executors for debugged and not debugged processes.
    /// </summary>
    public class DebuggedProcessExecutorFactory : ProcessExecutorFactory, IDebuggedProcessExecutorFactory
    {
        private readonly IFrameworkHandle _frameworkHandle;
        private readonly IDebuggerAttacher _debuggerAttacher;

        public DebuggedProcessExecutorFactory(IFrameworkHandle frameworkHandle, IDebuggerAttacher debuggerAttacher)
        {
            _frameworkHandle = frameworkHandle;
            _debuggerAttacher = debuggerAttacher;
        }

        public IDebuggedProcessExecutor CreateNativeDebuggingExecutor(DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger)
        {
            return new NativeDebuggedProcessExecutor(_debuggerAttacher, debuggerEngine, printTestOutput, logger);
        }

        public IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, ILogger logger)
        {
            return new FrameworkDebuggedProcessExecutor(_frameworkHandle, printTestOutput, logger);
        }
    }
}