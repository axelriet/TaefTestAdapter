// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.IO;
using System.Collections.Generic;

namespace TaefTestAdapter.ProcessExecution.Contracts
{
    /// <summary>
    /// Executes a process and reports its output line by line.
    /// </summary>
    public interface IProcessExecutor
    {
        /// <summary>
        /// Executes <paramref name="command"/> with <paramref name="parameters"/> in <paramref name="workingDir"/>, with
        /// <paramref name="pathExtension"/> added to the PATH and <paramref name="environmentVariables"/> set, and
        /// reports each line of its output to <paramref name="reportOutputLine"/>.
        /// </summary>
        /// <returns>The exit code of the process.</returns>
        int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension,  IDictionary<string, string> environmentVariables, Action<string> reportOutputLine);
        /// <summary>Cancels the execution (kills the process and the processes started by it).</summary>
        void Cancel();
    }

    /// <summary>
    /// Extension methods of <see cref="IProcessExecutor"/>.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class IProcessExecutorExtensions
    {
        /// <summary>Executes batch file <paramref name="batchFile"/> with cmd.exe.</summary>
        /// <exception cref="FileNotFoundException">If <paramref name="batchFile"/> does not exist.</exception>
        public static int ExecuteBatchFileBlocking(this IProcessExecutor executor, string batchFile, string parameters, string workingDir, string pathExtension, Action<string> reportOutputLine)
        {
            if (!File.Exists(batchFile))
            {
                throw new FileNotFoundException("File not found", batchFile);
            }

            string command = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            return executor.ExecuteCommandBlocking(command, $"/C \"{batchFile}\" {parameters}", workingDir, pathExtension, new Dictionary<string, string>(),
                reportOutputLine);
        }
    }

}