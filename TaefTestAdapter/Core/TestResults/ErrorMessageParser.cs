// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TaefTestAdapter.TestResults
{

    /// <summary>
    /// Creates the error message and the stack trace of a test result from the lines TE.exe printed for the test:
    /// <list type="bullet">
    /// <item><c>Error: &lt;message&gt;[ [File: &lt;file&gt;, Function: &lt;function&gt;, Line: &lt;line&gt;]]</c> (e.g. failed
    /// <c>VERIFY_*</c> macros, <c>Log::Error()</c>, exceptions, crashes, fixture failures)</item>
    /// <item><c>TestSkipped: &lt;message&gt;</c>, <c>TestBlocked: &lt;message&gt;</c>, <c>TestFailed: &lt;message&gt;</c>,
    /// <c>TestNotRun: &lt;message&gt;</c> (results set explicitly, e.g. by <c>Log::Result()</c>, or by TAEF)</item>
    /// </list>
    /// Details:
    /// <list type="bullet">
    /// <item>TE.exe prints a message containing line breaks (e.g. <c>VERIFY_ARE_EQUAL</c> of strings containing new lines)
    /// as several lines, of which only the first one has the <c>Error: </c> prefix and only the last one has the source
    /// information suffix. Such an error is recognized if the line with the suffix follows within
    /// <see cref="MaxNrOfContinuationLines"/> lines, and no line in between starts like a line of TE.exe (e.g.
    /// <c>Error: </c>, <c>Verify: </c>, <c>StartGroup: </c>). The lines following an error without source information
    /// can not be told apart from other output (e.g. of <c>Log::Comment()</c>), so only its first line is part of the
    /// message (the complete text is part of the test's output).</item>
    /// <item>With <c>/inproc</c>, output the test printed without a trailing new line (e.g. with <c>printf()</c>) is glued
    /// to TE.exe's next line. Recognized at the end of such a line are errors with source information (see
    /// <see cref="GluedErrorWithSourceInformationRegex"/>), errors of TAEF itself and of exceptions caught by TAEF (see
    /// <see cref="GluedTaefErrorRegex"/>), and result messages (see <see cref="GluedResultMessageRegex"/>). Other glued
    /// errors can not be told apart from ordinary output (e.g. a comment containing <c>Error: </c>).</item>
    /// <item>All other lines are ignored.</item>
    /// </list>
    /// The error message consists of the messages without their source information (numbered <c>#1 - ...</c> if there
    /// are several of them); the stack trace contains one entry <c>at &lt;function&gt; in &lt;file&gt;:line &lt;line&gt;</c> for
    /// each error with source information (which Visual Studio displays as clickable links; <c>&lt;file name&gt;:&lt;line&gt;</c>
    /// is used instead of an unknown function).
    /// </summary>
    public class ErrorMessageParser
    {
        public const string ErrorPrefix = "Error: ";

        /// <summary>Line printed by TE.exe option <c>/stackTraceOnError</c> before the frames of the call stack of an error.</summary>
        public const string CallSiteLine = ErrorPrefix + "Call Site";

        /// <summary>Maximum number of lines a message containing line breaks is searched for its source information.</summary>
        public const int MaxNrOfContinuationLines = 100;

        private static readonly Regex ErrorWithSourceInformationRegex = new Regex(
            @"^Error: (?<message>.*) \[File: (?<file>.+?), Function: (?<function>.*?), Line: (?<line>\d+)\]$",
            RegexOptions.Compiled);

        /// <summary>The last line of an error message containing line breaks.</summary>
        private static readonly Regex SourceInformationSuffixRegex = new Regex(
            @"^(?<message>.*) \[File: (?<file>.+?), Function: (?<function>.*?), Line: (?<line>\d+)\]$",
            RegexOptions.Compiled);

        /// <summary>
        /// An error with source information at the end of a line which starts with other output: with <c>/inproc</c>, output
        /// the test printed without a trailing new line (e.g. with <c>printf()</c>) is glued to TE.exe's next line.
        /// </summary>
        private static readonly Regex GluedErrorWithSourceInformationRegex = new Regex(
            @"Error: (?<message>.*) \[File: (?<file>.+?), Function: (?<function>.*?), Line: (?<line>\d+)\]$",
            RegexOptions.Compiled);

        /// <summary>
        /// An error printed by TAEF (e.g. for an exception it caught) glued to the end of other output (see
        /// <see cref="GluedErrorWithSourceInformationRegex"/>); only TAEF's fixed message forms are recognized.
        /// </summary>
        private static readonly Regex GluedTaefErrorRegex = new Regex(
            @"(?<=.)Error: (?<message>(?:Caught (?:std::exception: |an unidentified C\+\+ exception|WEX::Common::Exception: |Platform::Exception)|TAEF: ).*)$",
            RegexOptions.Compiled);

        private static readonly Regex ResultMessageRegex = new Regex(
            @"^Test(?<result>Skipped|Blocked|Failed|NotRun): (?<message>.*)$",
            RegexOptions.Compiled);

        /// <summary>A result message glued to the end of other output (see <see cref="GluedErrorWithSourceInformationRegex"/>).</summary>
        private static readonly Regex GluedResultMessageRegex = new Regex(
            @"(?<=.)Test(?<result>Skipped|Blocked|Failed|NotRun): (?<message>.*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Starts of lines TE.exe prints itself: such a line is never the continuation of a message containing line
        /// breaks, and glued messages are not searched within it.
        /// </summary>
        private static readonly string[] TaefLinePrefixes =
        {
            ErrorPrefix, "Warning: ", "Verify: ", "StartGroup: ", "EndGroup: ", "TestSkipped: ", "TestBlocked: ",
            "TestFailed: ", "TestNotRun: ", "TAEF: ", "Property: TAEF: "
        };

        private class Message
        {
            public string Text { get; set; }
            public string File { get; set; }
            public string Function { get; set; }
            public string Line { get; set; }

            public bool HasSourceInformation => !string.IsNullOrEmpty(File) && !string.IsNullOrEmpty(Line);
        }

        public string ErrorMessage { get; private set; } = "";
        public string ErrorStackTrace { get; private set; } = "";

        /// <summary>Number of messages found (errors and result messages).</summary>
        public int NrOfMessages => _messages.Count;

        private readonly IList<Message> _messages;
        private readonly string _codeFilePathOfTest;

        /// <param name="lines">Lines printed by TE.exe for a test (e.g. the lines between its StartGroup and EndGroup).</param>
        /// <param name="codeFilePathOfTest">
        /// Source file of the test (optional); used to resolve relative file names of the source information (which
        /// are the result of compiling without option <c>/FC</c>).
        /// </param>
        public ErrorMessageParser(IEnumerable<string> lines, string codeFilePathOfTest = null)
        {
            _messages = ParseMessages(lines ?? Enumerable.Empty<string>());
            _codeFilePathOfTest = codeFilePathOfTest;
        }

        public ErrorMessageParser(string consoleOutput, string codeFilePathOfTest = null)
            : this(SplitIntoLines(consoleOutput), codeFilePathOfTest)
        {
        }

        public void Parse()
        {
            if (_messages.Count == 0)
            {
                ErrorMessage = "";
                ErrorStackTrace = "";
                return;
            }

            bool numberMessages = _messages.Count > 1;
            var errorMessages = new List<string>();
            var stackTrace = new List<string>();
            for (int i = 0; i < _messages.Count; i++)
            {
                Message message = _messages[i];
                string messageReference = numberMessages ? $"#{i + 1} - " : "";

                errorMessages.Add(messageReference + message.Text);
                if (message.HasSourceInformation)
                {
                    string file = ResolveFile(message.File);
                    string label = string.IsNullOrEmpty(message.Function)
                        ? $"{GetFileNameForLabel(file)}:{message.Line}"
                        : message.Function;
                    stackTrace.Add(CreateStackTraceEntry(messageReference + label, file, message.Line));
                }
            }

            ErrorMessage = string.Join("\n", errorMessages);
            ErrorStackTrace = string.Join("", stackTrace);
        }

        /// <returns>A stack trace entry <c>at &lt;label&gt; in &lt;file&gt;:line &lt;line&gt;</c> (terminated by a new line).</returns>
        public static string CreateStackTraceEntry(string label, string fullFileName, string lineNumber)
        {
            return $"at {label} in {fullFileName}:line {lineNumber}{Environment.NewLine}";
        }

        /// <returns>True if <paramref name="line"/> is a line which is interpreted by this class (error or result message).</returns>
        public static bool IsMessageLine(string line)
        {
            return line != null
                && ((line.StartsWith(ErrorPrefix, StringComparison.Ordinal) && line != CallSiteLine)
                    || ResultMessageRegex.IsMatch(line));
        }

        /// <returns>
        /// The message of an <c>Error:</c> line without prefix and source information, or null if <paramref name="line"/>
        /// is not an error line.
        /// </returns>
        public static string GetErrorText(string line)
        {
            if (line == null || !line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                return null;

            Match match = ErrorWithSourceInformationRegex.Match(line);
            return match.Success ? match.Groups["message"].Value : line.Substring(ErrorPrefix.Length);
        }

        /// <returns>
        /// The <c>Error:</c> lines of <paramref name="lines"/> (except <see cref="CallSiteLine"/>); an error message containing
        /// line breaks (see <see cref="ErrorMessageParser"/>) is returned as one string containing its lines (joined by
        /// <see cref="Environment.NewLine"/>).
        /// </returns>
        public static IList<string> GetErrors(IEnumerable<string> lines)
        {
            IList<string> linesList = lines as IList<string> ?? (lines ?? Enumerable.Empty<string>()).ToList();
            var errors = new List<string>();
            for (int i = 0; i < linesList.Count; i++)
            {
                string line = linesList[i];
                if (line == null || line == CallSiteLine || !line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                    continue;

                int lastLine = ErrorWithSourceInformationRegex.IsMatch(line) ? i : FindLastLineOfMultiLineError(linesList, i, out _);
                if (lastLine < 0)
                    lastLine = i;
                errors.Add(string.Join(Environment.NewLine, linesList.Skip(i).Take(lastLine - i + 1)));
                i = lastLine;
            }
            return errors;
        }

        private static IList<Message> ParseMessages(IEnumerable<string> lines)
        {
            IList<string> linesList = lines as IList<string> ?? lines.ToList();
            var messages = new List<Message>();
            for (int i = 0; i < linesList.Count; i++)
            {
                string line = linesList[i];
                if (line == null || line == CallSiteLine)
                    continue;

                Match match;
                if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                {
                    match = ErrorWithSourceInformationRegex.Match(line);
                    if (match.Success)
                    {
                        messages.Add(CreateMessage(match.Groups["message"].Value, match));
                        continue;
                    }

                    int lastLine = FindLastLineOfMultiLineError(linesList, i, out match);
                    if (lastLine > i)
                    {
                        var text = new StringBuilder(line.Substring(ErrorPrefix.Length));
                        for (int j = i + 1; j < lastLine; j++)
                            text.Append('\n').Append(linesList[j]);
                        text.Append('\n').Append(match.Groups["message"].Value);

                        messages.Add(CreateMessage(text.ToString(), match));
                        i = lastLine;
                        continue;
                    }

                    messages.Add(new Message { Text = line.Substring(ErrorPrefix.Length) });
                    continue;
                }

                match = GluedErrorWithSourceInformationRegex.Match(line);
                if (match.Success)
                {
                    messages.Add(CreateMessage(match.Groups["message"].Value, match));
                    continue;
                }

                match = ResultMessageRegex.Match(line);
                if (!match.Success && !StartsLikeTaefLine(line))
                {
                    Match gluedError = GluedTaefErrorRegex.Match(line);
                    if (gluedError.Success)
                    {
                        messages.Add(new Message { Text = gluedError.Groups["message"].Value });
                        continue;
                    }
                    match = GluedResultMessageRegex.Match(line);
                }
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["message"].Value))
                {
                    messages.Add(new Message { Text = match.Groups["message"].Value });
                }
            }
            return messages;
        }

        private static Message CreateMessage(string text, Match sourceInformation)
        {
            return new Message
            {
                Text = text,
                File = sourceInformation.Groups["file"].Value,
                Function = sourceInformation.Groups["function"].Value,
                Line = sourceInformation.Groups["line"].Value
            };
        }

        /// <summary>
        /// Searches the last line of an error message containing line breaks which starts with the <c>Error:</c> line
        /// <paramref name="lines"/>[<paramref name="firstLine"/>] (which has no source information).
        /// </summary>
        /// <returns>The index of the line with the source information (matched by <paramref name="sourceInformation"/>), or -1.</returns>
        private static int FindLastLineOfMultiLineError(IList<string> lines, int firstLine, out Match sourceInformation)
        {
            sourceInformation = null;
            int end = Math.Min(lines.Count - 1, firstLine + MaxNrOfContinuationLines);
            for (int i = firstLine + 1; i <= end; i++)
            {
                string line = lines[i];
                if (line == null || StartsLikeTaefLine(line))
                    return -1;

                Match match = SourceInformationSuffixRegex.Match(line);
                if (match.Success)
                {
                    // an error glued to other output is an error of its own
                    if (GluedErrorWithSourceInformationRegex.IsMatch(line))
                        return -1;

                    sourceInformation = match;
                    return i;
                }
            }
            return -1;
        }

        private static bool StartsLikeTaefLine(string line)
        {
            return TaefLinePrefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal));
        }

        /// <returns>The part of <paramref name="file"/> after the last directory separator (never throws, unlike <see cref="Path.GetFileName"/>).</returns>
        private static string GetFileNameForLabel(string file)
        {
            int index = file.LastIndexOfAny(new[] { '\\', '/' });
            return index >= 0 ? file.Substring(index + 1) : file;
        }

        private string ResolveFile(string file)
        {
            try
            {
                if (Path.IsPathRooted(file) || string.IsNullOrEmpty(_codeFilePathOfTest) || !Path.IsPathRooted(_codeFilePathOfTest))
                    return file;

                // file names are relative to the compiler's working directory (usually the project directory), which is
                // most likely a parent directory of the test's source file
                for (string directory = Path.GetDirectoryName(_codeFilePathOfTest);
                    !string.IsNullOrEmpty(directory);
                    directory = Path.GetDirectoryName(directory))
                {
                    string candidate = Path.GetFullPath(Path.Combine(directory, file));
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception)
            {
                // invalid path - use file name as is
            }
            return file;
        }

        private static IEnumerable<string> SplitIntoLines(string text)
        {
            return string.IsNullOrEmpty(text)
                ? Enumerable.Empty<string>()
                : text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

    }

}
