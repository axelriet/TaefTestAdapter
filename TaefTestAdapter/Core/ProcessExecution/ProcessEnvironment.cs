// This file has been added for TAEF support.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;

namespace TaefTestAdapter.ProcessExecution
{
    /// <summary>
    /// Computes the environment of the processes started by the process executors (usually TE.exe): the environment of the
    /// current process, overridden by the environment variables configured by the user, and with the path extension
    /// prepended to <c>PATH</c>.
    /// </summary>
    public static class ProcessEnvironment
    {
        public const string PathVariableName = "PATH";

        /// <summary>
        /// Returns the environment variables which have to be set for a process in addition to (or instead of) the ones
        /// it inherits from the current process: the variables of <paramref name="environmentVariables"/>, and
        /// <c>PATH=&lt;pathExtension&gt;;%PATH%</c> if <paramref name="pathExtension"/> is not empty. In the latter case the
        /// path extension wins: a PATH variable of the user is ignored (with a warning if it is not empty), independent of
        /// the case of its name (<c>PATH</c>, <c>Path</c>, ...; names of environment variables are case-insensitive on
        /// Windows). <paramref name="environmentVariables"/> is not modified.
        /// </summary>
        /// <returns>A new dictionary (with the default, case-sensitive comparer, like the one passed in).</returns>
        public static IDictionary<string, string> GetEnvironmentVariablesToSet(string pathExtension, IDictionary<string, string> environmentVariables, ILogger logger)
        {
            var result = new Dictionary<string, string>();
            bool extendPath = !string.IsNullOrEmpty(pathExtension);
            if (environmentVariables != null)
            {
                foreach (KeyValuePair<string, string> variable in environmentVariables)
                {
                    if (extendPath && IsPathVariable(variable.Key))
                    {
                        if (!string.IsNullOrEmpty(variable.Value))
                            logger?.LogWarning($"Both a path extension and a PATH environment variable (named '{variable.Key}') have been provided! The PATH environment variable will be ignored.");
                        continue;
                    }
                    result[variable.Key] = variable.Value;
                }
            }

            if (extendPath)
                result[PathVariableName] = Utils.GetExtendedPath(pathExtension);

            return result;
        }

        /// <returns>True if <paramref name="name"/> is the name of the PATH environment variable (in any case).</returns>
        public static bool IsPathVariable(string name)
        {
            return string.Equals(name, PathVariableName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates the environment block of a process (as expected by <c>CreateProcess</c> with flag
        /// <c>CREATE_UNICODE_ENVIRONMENT</c>): the environment of the current process, overridden by
        /// <paramref name="variablesToSet"/> (names are compared case-insensitively). Entries are sorted by name and
        /// terminated by <c>\0</c>; the block is terminated by an additional <c>\0</c> (which is <em>not</em> part of the
        /// returned string, but is appended by e.g. <see cref="System.Runtime.InteropServices.Marshal.StringToHGlobalUni"/>).
        /// </summary>
        public static string CreateEnvironmentBlock(IDictionary<string, string> variablesToSet)
        {
            var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                environment[(string)variable.Key] = (string)variable.Value;
            }
            if (variablesToSet != null)
            {
                foreach (KeyValuePair<string, string> variable in variablesToSet)
                {
                    environment[variable.Key] = variable.Value;
                }
            }

            var block = new StringBuilder();
            foreach (KeyValuePair<string, string> variable in environment.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
            {
                block.Append(variable.Key).Append('=').Append(variable.Value).Append('\0');
            }
            if (block.Length == 0)
                block.Append('\0');

            return block.ToString();
        }
    }
}
