// This file has been modified for TAEF support.

using TaefTestAdapter.Common;

namespace TaefTestAdapter.ProcessExecution.Contracts
{
    /// <summary>
    /// Creates process executors.
    /// </summary>
    public interface IProcessExecutorFactory
    {
        /// <summary>Creates an executor for processes which are not debugged.</summary>
        IProcessExecutor CreateExecutor(bool printTestOutput, ILogger logger);
    }
}