// This file has been modified for TAEF support.

using TaefTestAdapter.Common;

namespace TaefTestAdapter.ProcessExecution.Contracts
{
    /// <summary>
    /// Creates process executors for debugged and not debugged processes.
    /// </summary>
    public interface IDebuggedProcessExecutorFactory : IProcessExecutorFactory
    {
        /// <summary>Creates an executor which lets the test framework launch the process under the debugger.</summary>
        IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, ILogger logger);

        /// <summary>Creates an executor which starts the process and attaches the debugger with <paramref name="debuggerEngine"/>.</summary>
        IDebuggedProcessExecutor CreateNativeDebuggingExecutor(DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger);
    }
}
