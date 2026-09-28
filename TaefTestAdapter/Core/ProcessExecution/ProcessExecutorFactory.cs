// This file has been modified for TAEF support.

using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution.Contracts;

namespace TaefTestAdapter.ProcessExecution
{
    /// <summary>
    /// Creates process executors for processes which are not debugged.
    /// </summary>
    public class ProcessExecutorFactory : IProcessExecutorFactory
    {
        public IProcessExecutor CreateExecutor(bool printTestOutput, ILogger logger)
        {
            return new DotNetProcessExecutor(printTestOutput, logger);
        }
    }
}
