// This file has been modified for TAEF support.

using System;

namespace TaefTestAdapter.TestAdapter.ProcessExecution
{
    /// <summary>
    /// Message of the test adapter asking the Visual Studio package to attach the debugger to a process, and its answer.
    /// </summary>
    [Serializable]
    public class AttachDebuggerMessage
    {
        public int ProcessId { get; set; }
        public bool DebuggerAttachedSuccessfully { get; set; } = false;
        public string ErrorMessage { get; set; } = null;
    }
}