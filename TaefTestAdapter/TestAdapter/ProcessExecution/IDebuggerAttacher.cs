// This file has been modified for TAEF support.

using TaefTestAdapter.Common;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Attaches the Visual Studio debugger to a process.
    /// </summary>
    public interface IDebuggerAttacher
    {
        /// <returns>True if the debugger has been attached to process <paramref name="processId"/>.</returns>
        bool AttachDebugger(int processId, DebuggerEngine debuggerEngine);
    }
}