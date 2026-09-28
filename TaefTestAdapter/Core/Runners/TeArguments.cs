// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter.Runners
{

    /// <summary>
    /// Helpers for passing file paths and test selections to TE.exe.
    /// <para>
    /// <b>Paths:</b> TE.exe (10.104k, x86 and x64) mangles non-ASCII characters within test file arguments and within the
    /// value of <c>/logFile</c> (it interprets their UTF-8 bytes as ANSI characters, e.g. <c>ü</c> becomes <c>Ã¼</c>):
    /// it reports an existing test DLL as not existing (exit code <c>0x05000000</c>), and it writes WTT logs into a
    /// different, newly created folder. Other paths (TE.exe itself, TE.exe's working directory, values of <c>/p:</c>) are
    /// not affected, and TE.exe resolves relative paths against its working directory correctly. Therefore, the adapter
    /// passes such paths as ASCII-only alternatives if they contain non-ASCII characters: relative to TE.exe's working
    /// directory, or as 8.3 short paths (see <see cref="GetTestDllArgument(string,string,ILogger)"/> and
    /// <see cref="CreateWttLogFile(string,string,string,ILogger)"/>).
    /// </para>
    /// <para>
    /// <b>Selection:</b> TE.exe evaluates only one selection: a <c>/select:&lt;query&gt;</c> replaces any earlier
    /// <c>/select</c> or <c>/name</c> (with warning <c>Multiple /select or /name options have been specified</c>), while a
    /// <c>/name:&lt;name&gt;</c> (short for <c>/select:"@Name='&lt;name&gt;'"</c>) is ignored if a selection has been
    /// specified before. <see cref="ExtractSelection"/> finds the selection of the user's additional TE.exe arguments, so
    /// that it can be combined with the adapter's selection (see <see cref="CommandLineGenerator"/>).
    /// </para>
    /// </summary>
    public static class TeArguments
    {
        /// <summary>A WTT log file TE.exe is asked to write (see <see cref="CreateWttLogFile(string,string,string,ILogger)"/>).</summary>
        public class WttLogFile
        {
            /// <summary>Full path of the log file (to be read and deleted by the adapter).</summary>
            public string File { get; }

            /// <summary>The path passed to TE.exe (switch <c>/logFile</c>); consists of ASCII characters if possible.</summary>
            public string Argument { get; }

            public WttLogFile(string file, string argument)
            {
                File = file ?? throw new ArgumentNullException(nameof(file));
                Argument = argument ?? throw new ArgumentNullException(nameof(argument));
            }

            public override string ToString() => File;
        }

        public const string WttLogFilePrefix = "TaefTestAdapter_";
        public const string WttLogFileExtension = ".wtl";

        private const string SelectSwitch = "select";
        private const string NameSwitch = "name";
        private const string OutputFolderSwitch = "outputFolder";

        #region Paths

        /// <returns>True if <paramref name="value"/> consists of ASCII characters only (null counts as ASCII).</returns>
        public static bool IsAscii(string value)
        {
            return value == null || value.All(c => c <= 0x7F);
        }

        /// <returns>
        /// The 8.3 short form of the existing file or folder <paramref name="path"/> (Windows API <c>GetShortPathNameW</c>),
        /// or null if it can not be determined. Note that the result is the (unchanged) long path for path components
        /// without short names (e.g. if 8.3 names are disabled for the volume).
        /// </returns>
        public static string GetShortPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            try
            {
                uint length = NativeMethods.GetShortPathNameW(path, null, 0);
                if (length == 0)
                    return null;

                var buffer = new StringBuilder((int)length);
                uint result = NativeMethods.GetShortPathNameW(path, buffer, length);
                return result == 0 || result >= length ? null : buffer.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the path of the test DLL to be passed to TE.exe (unquoted): the full path of <paramref name="testDll"/>
        /// if it consists of ASCII characters only. Otherwise (TE.exe can not open test files whose path contains non-ASCII
        /// characters), the file name if <paramref name="workingDir"/> (the working directory of TE.exe) is the folder of the
        /// test DLL (the default, <c>$(TestDllDir)</c>) and the file name consists of ASCII characters, else the 8.3 short
        /// path of the test DLL if it consists of ASCII characters. If none of these is possible, an error explaining the
        /// problem is logged and the full path is returned (TE.exe will then report that it could not find the test DLL).
        /// <para>
        /// The returned path must only be used for the TE.exe command line: the adapter reports everything else (e.g. the
        /// sources of test cases) with the original path.
        /// </para>
        /// </summary>
        /// <param name="testDll">The test DLL (relative paths are made absolute).</param>
        /// <param name="workingDir">Working directory of TE.exe (may be null).</param>
        /// <param name="logger">Receives the error if no ASCII path can be found (may be null).</param>
        public static string GetTestDllArgument(string testDll, string workingDir, ILogger logger)
        {
            return GetTestDllArgument(testDll, workingDir, logger, GetShortPath);
        }

        /// <summary>
        /// See <see cref="GetTestDllArgument(string,string,ILogger)"/>; <paramref name="getShortPath"/> determines 8.3 short
        /// paths (see <see cref="GetShortPath"/>, for tests).
        /// </summary>
        public static string GetTestDllArgument(string testDll, string workingDir, ILogger logger, Func<string, string> getShortPath)
        {
            if (testDll == null)
                throw new ArgumentNullException(nameof(testDll));
            if (getShortPath == null)
                throw new ArgumentNullException(nameof(getShortPath));

            string fullPath = GetFullPath(testDll);
            if (IsAscii(fullPath))
                return fullPath;

            string fileName = Path.GetFileName(fullPath);
            if (!string.IsNullOrEmpty(fileName) && IsAscii(fileName) && IsSameDirectory(Path.GetDirectoryName(fullPath), workingDir))
                return fileName;

            string shortPath = getShortPath(fullPath);
            if (!string.IsNullOrEmpty(shortPath) && IsAscii(shortPath))
                return shortPath;

            logger?.LogError(
                $"The path of test DLL '{fullPath}' contains non-ASCII characters, which TE.exe can not handle (it will report that the test DLL does not exist), " +
                "and no alternative path consisting of ASCII characters only could be found (the test DLL has no 8.3 short path, " +
                $"and TE.exe's working directory '{workingDir}' is not the folder of the test DLL). The tests of this DLL can not be discovered or run. " +
                "Move the test DLL (e.g. the build output folder) to a folder whose path consists of ASCII characters only, " +
                $"or set option '{SettingsWrapper.OptionWorkingDir}' to '{PlaceholderReplacer.TestDllDirPlaceholder}' (if the file name of the test DLL consists of ASCII characters), " +
                "or enable 8.3 file names for the volume and recreate the folders (see 'fsutil 8dot3name').");
            return fullPath;
        }

        /// <summary>
        /// Chooses a new WTT log file for TE.exe (<c>TaefTestAdapter_&lt;guid&gt;.wtl</c>) in <paramref name="tempDir"/>,
        /// such that the path passed to TE.exe consists of ASCII characters (TE.exe would write the log to a different folder
        /// otherwise): the full path if it consists of ASCII characters, else the path within the 8.3 short path of
        /// <paramref name="tempDir"/> if it consists of ASCII characters, else the file name only (i.e., the log is written
        /// into TE.exe's working directory <paramref name="workingDir"/>) unless <paramref name="userParameters"/> contain
        /// <c>/outputFolder</c> (TE.exe would resolve the file name against that folder). If none of these is possible, an
        /// error explaining the problem is logged and the full path is used.
        /// </summary>
        /// <param name="tempDir">Folder for the log file (usually <see cref="Path.GetTempPath"/>).</param>
        /// <param name="workingDir">Working directory of TE.exe (may be null).</param>
        /// <param name="userParameters">The user's additional TE.exe arguments (may be null).</param>
        /// <param name="logger">Receives the error if no ASCII path can be found (may be null).</param>
        public static WttLogFile CreateWttLogFile(string tempDir, string workingDir, string userParameters, ILogger logger)
        {
            return CreateWttLogFile(tempDir, workingDir, userParameters, logger, GetShortPath);
        }

        /// <summary>
        /// See <see cref="CreateWttLogFile(string,string,string,ILogger)"/>; <paramref name="getShortPath"/> determines 8.3
        /// short paths (see <see cref="GetShortPath"/>, for tests).
        /// </summary>
        public static WttLogFile CreateWttLogFile(string tempDir, string workingDir, string userParameters, ILogger logger, Func<string, string> getShortPath)
        {
            if (string.IsNullOrWhiteSpace(tempDir))
                throw new ArgumentException("Temp directory must not be empty", nameof(tempDir));
            if (getShortPath == null)
                throw new ArgumentNullException(nameof(getShortPath));

            string fileName = $"{WttLogFilePrefix}{Guid.NewGuid():N}{WttLogFileExtension}";
            string file = Path.Combine(tempDir, fileName);
            if (IsAscii(file))
                return new WttLogFile(file, file);

            // the log file does not exist yet, so only the folder can be shortened
            string shortTempDir = getShortPath(tempDir);
            if (!string.IsNullOrEmpty(shortTempDir) && IsAscii(shortTempDir))
                return new WttLogFile(file, Path.Combine(shortTempDir, fileName));

            if (!string.IsNullOrWhiteSpace(workingDir) && !ContainsSwitch(userParameters, OutputFolderSwitch))
                return new WttLogFile(Path.Combine(GetFullPath(workingDir), fileName), fileName);

            logger?.LogError(
                $"The folder for temporary files '{tempDir}' contains non-ASCII characters, which TE.exe can not handle when writing its WTT log " +
                $"(it has no 8.3 short path, and the log can not be written to TE.exe's working directory '{workingDir}', e.g. since option '{SettingsWrapper.OptionAdditionalTestExecutionParam}' " +
                "contains /outputFolder). Test results will not be available while debugging. " +
                $"Set environment variable TMP to a folder whose path consists of ASCII characters only, or set option '{SettingsWrapper.OptionDebuggerKind}' to '{DebuggerKindConverter.Native}'.");
            return new WttLogFile(file, file);
        }

        private static bool IsSameDirectory(string directory, string otherDirectory)
        {
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(otherDirectory))
                return false;

            return string.Equals(NormalizeDirectory(directory), NormalizeDirectory(otherDirectory), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectory(string directory)
        {
            return GetFullPath(directory.Trim().Trim('"')).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string GetFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern uint GetShortPathNameW(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);
        }

        #endregion

        #region Selection

        /// <summary>
        /// Removes all test selections (<c>/select:&lt;query&gt;</c> and <c>/name:&lt;name&gt;</c>, also with <c>-</c>
        /// instead of <c>/</c> and in any case) from the TE.exe arguments <paramref name="arguments"/>, and returns the
        /// selection query TE.exe would have used: the query of the last <c>/select</c>, or <c>@Name='&lt;name&gt;'</c> for
        /// the first <c>/name</c> if there is no <c>/select</c> before it (TE.exe ignores a <c>/name</c> following a selection,
        /// see class comment). An empty query (<c>/select:""</c>) selects all tests. Switches without value (e.g.
        /// <c>/select:</c>) and quoted arguments (e.g. <c>"/select:..."</c>) are no selections for TE.exe (it treats them as
        /// test file expressions), so they are kept.
        /// </summary>
        /// <param name="arguments">TE.exe arguments (may be null).</param>
        /// <param name="argumentsWithoutSelection">
        /// <paramref name="arguments"/> without the selection switches (trimmed; switches are separated by single spaces if
        /// switches have been removed).
        /// </param>
        /// <returns>The selection query, or null if TE.exe would run all tests.</returns>
        public static string ExtractSelection(string arguments, out string argumentsWithoutSelection)
        {
            arguments = arguments ?? "";
            List<Token> tokens = Tokenize(arguments).ToList();

            string query = null;
            bool isSelectionFound = false;
            var remainingTokens = new List<string>();
            foreach (Token token in tokens)
            {
                if (token.IsSwitch(SelectSwitch))
                {
                    query = token.Value;
                    isSelectionFound = true;
                }
                else if (token.IsSwitch(NameSwitch))
                {
                    if (string.IsNullOrEmpty(query))
                        query = $"@Name='{token.Value}'";
                    isSelectionFound = true;
                }
                else
                {
                    remainingTokens.Add(arguments.Substring(token.Start, token.End - token.Start));
                }
            }

            argumentsWithoutSelection = isSelectionFound ? string.Join(" ", remainingTokens) : arguments.Trim();
            return string.IsNullOrEmpty(query) ? null : query;
        }

        /// <returns>True if <paramref name="arguments"/> contain the switch <c>/&lt;switchName&gt;:&lt;value&gt;</c>.</returns>
        public static bool ContainsSwitch(string arguments, string switchName)
        {
            return Tokenize(arguments ?? "").Any(t => t.IsSwitch(switchName));
        }

        private class Token
        {
            public int Start { get; }
            public int End { get; }
            /// <summary>Name of the switch (without <c>/</c> and <c>:</c>) if the token is a switch with value, else null.</summary>
            public string SwitchName { get; }
            /// <summary>Value of the switch (without surrounding quotes).</summary>
            public string Value { get; }

            public Token(int start, int end, string switchName = null, string value = null)
            {
                Start = start;
                End = end;
                SwitchName = switchName;
                Value = value;
            }

            public bool IsSwitch(string switchName)
                => SwitchName != null && string.Equals(SwitchName, switchName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Splits TE.exe arguments like TE.exe does: arguments are separated by white space; the value of a switch
        /// (<c>/name:value</c> or <c>-name:value</c>) is quoted if a double quote follows the colon directly (and then
        /// extends to the next double quote); an argument starting with a double quote extends to the next double quote.
        /// </summary>
        private static IEnumerable<Token> Tokenize(string arguments)
        {
            int length = arguments.Length;
            int i = 0;
            while (true)
            {
                while (i < length && char.IsWhiteSpace(arguments[i]))
                    i++;
                if (i >= length)
                    yield break;

                int start = i;
                char first = arguments[i];
                if (first == '"')
                {
                    int closingQuote = arguments.IndexOf('"', i + 1);
                    i = SkipToWhiteSpace(arguments, closingQuote < 0 ? length : closingQuote + 1);
                    yield return new Token(start, i);
                    continue;
                }

                if (first == '/' || first == '-')
                {
                    int colon = i + 1;
                    while (colon < length && arguments[colon] != ':' && arguments[colon] != '"' && !char.IsWhiteSpace(arguments[colon]))
                        colon++;
                    if (colon < length && arguments[colon] == ':' && colon > i + 1)
                    {
                        string switchName = arguments.Substring(i + 1, colon - i - 1);
                        int valueStart = colon + 1;
                        if (valueStart < length && arguments[valueStart] == '"')
                        {
                            int closingQuote = arguments.IndexOf('"', valueStart + 1);
                            int valueEnd = closingQuote < 0 ? length : closingQuote;
                            string value = arguments.Substring(valueStart + 1, valueEnd - valueStart - 1);
                            i = SkipToWhiteSpace(arguments, closingQuote < 0 ? length : closingQuote + 1);
                            yield return new Token(start, i, switchName, value);
                            continue;
                        }

                        i = SkipToWhiteSpace(arguments, valueStart);
                        yield return i > valueStart
                            ? new Token(start, i, switchName, arguments.Substring(valueStart, i - valueStart))
                            : new Token(start, i);
                        continue;
                    }
                }

                i = SkipToWhiteSpace(arguments, i);
                yield return new Token(start, i);
            }
        }

        private static int SkipToWhiteSpace(string arguments, int index)
        {
            while (index < arguments.Length && !char.IsWhiteSpace(arguments[index]))
                index++;
            return index;
        }

        #endregion

    }

}
