// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Reads a WTT log written by TE.exe (switches <c>/enableWttLogging /logFile:&lt;file&gt;</c>) and converts it into
    /// the console output TE.exe would have printed (as far as relevant for <see cref="StreamingTaefOutputParser"/>):
    /// test groups (<c>StartGroup:</c>, <c>EndGroup: ... [&lt;result&gt;]</c>), comments, <c>Verify:</c> lines,
    /// <c>Error: ... [File: ..., Function: , Line: ...]</c> lines (WTT logs do not contain the function), warnings, result
    /// messages (<c>TestSkipped:</c> etc.), and fixture failures outside of tests. Used if the console output of TE.exe
    /// is not available (i.e., if tests are debugged with the VsTest framework's debugger).
    /// <para>
    /// A WTT log is truncated if TE.exe terminated abnormally; everything up to the truncation is converted (so that a
    /// test which was running is reported as crashed).
    /// </para>
    /// </summary>
    public class WttLogParser
    {
        /// <summary>
        /// Appended to the WTT log file name to form the name of the file TE.exe's WTT logger writes while tests are
        /// running; TE.exe turns it into the WTT log only when it terminates normally.
        /// </summary>
        public const string TraceFileExtension = ".trace";

        private const string VerifyContext = "Verify";
        private const string TaefContext = "TAEF";
        private const string RootElement = "WTT-Logger";

        private readonly string _wttLogFile;
        private readonly ILogger _logger;

        public WttLogParser(string wttLogFile, ILogger logger)
        {
            _wttLogFile = wttLogFile ?? throw new ArgumentNullException(nameof(wttLogFile));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <returns>The console output lines equivalent to the WTT log (empty if the log does not exist or can not be read).
        /// If TE.exe has terminated abnormally, the WTT log does not exist; the file written while tests were running
        /// (see <see cref="TraceFileExtension"/>) is converted instead.</returns>
        public IList<string> GetConsoleOutput()
        {
            var lines = new List<string>();
            string traceFile = _wttLogFile + TraceFileExtension;
            bool isTraceFile = !File.Exists(_wttLogFile);
            if (isTraceFile)
            {
                if (!File.Exists(traceFile))
                {
                    _logger.DebugWarning($"WTT log file '{_wttLogFile}' does not exist");
                    return lines;
                }
                _logger.DebugWarning($"WTT log file '{_wttLogFile}' does not exist (TE.exe has probably terminated abnormally), converting '{traceFile}' written while tests were running");
            }

            string file = isTraceFile ? traceFile : _wttLogFile;
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = isTraceFile ? CreateTraceFileReader(stream) : new XmlTextReader(stream))
                {
                    // keep line breaks within attribute values (multi-line comments)
                    reader.Normalization = false;
                    reader.DtdProcessing = DtdProcessing.Prohibit;
                    reader.XmlResolver = null;
                    reader.WhitespaceHandling = WhitespaceHandling.None;

                    ConvertEntries(reader, lines);
                }
            }
            catch (XmlException e)
            {
                // TE.exe terminated abnormally
                _logger.DebugWarning($"WTT log file '{file}' could not be parsed completely (TE.exe has probably terminated abnormally): {e.Message}");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _logger.LogWarning($"Could not read WTT log file '{file}': {e.Message}");
            }

            return lines;
        }

        // The file written while tests are running contains the entries of the WTT log without XML declaration and root
        // element (and ends in the middle of an entry if TE.exe was terminated): wrap it into a (truncated) WTT log.
        private static XmlTextReader CreateTraceFileReader(Stream stream)
        {
            string content;
            using (var streamReader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: true))
            {
                content = streamReader.ReadToEnd();
            }

            content = Regex.Replace(content, @"^\s*(?:<\?[^>]*\?>\s*)*", "");
            if (!content.StartsWith("<" + RootElement, StringComparison.Ordinal))
                content = "<" + RootElement + ">" + Environment.NewLine + content;

            return new XmlTextReader(new StringReader(content));
        }

        private static void ConvertEntries(XmlTextReader reader, List<string> lines)
        {
            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element)
                return;

            int entryDepth = reader.Depth + 1;
            string currentTest = null;
            bool currentTestIsNotRun = false;

            reader.Read();
            while (!reader.EOF)
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Depth != entryDepth)
                {
                    reader.Read();
                    continue;
                }

                // reads the complete entry (the reader stays on its end); throws if the entry is truncated
                XElement entry;
                using (XmlReader entryReader = reader.ReadSubtree())
                {
                    entry = XElement.Load(entryReader);
                }

                ConvertEntry(entry, lines, ref currentTest, ref currentTestIsNotRun);

                // only move to the next node after the entry has been converted: this throws if the log is truncated
                // right after the entry (XNode.ReadFrom() would have lost the entry in that case)
                reader.Read();
            }
        }

        private static void ConvertEntry(XElement entry, List<string> lines, ref string currentTest, ref bool currentTestIsNotRun)
        {
            string userText = (string) entry.Attribute("UserText") ?? "";
            XElement data = entry.Element("Data");
            string context = (string) data?.Element("WexContext");
            XElement result = data?.Element("Result");
            bool isFixtureMarker = data != null && (data.Element("StartGroup") != null || data.Element("EndGroup") != null);

            switch (entry.Name.LocalName)
            {
                case "StartTest":
                    currentTest = (string) entry.Attribute("Title");
                    currentTestIsNotRun = false;
                    lines.Add("");
                    lines.Add(StreamingTaefOutputParser.StartGroupPrefix + currentTest);
                    break;

                case "EndTest":
                    string title = (string) entry.Attribute("Title") ?? currentTest;
                    lines.Add($"{StreamingTaefOutputParser.EndGroupPrefix}{title} [{GetTaefResult((string) entry.Attribute("Result"), currentTestIsNotRun)}]");
                    currentTest = null;
                    break;

                case "Msg":
                    if (isFixtureMarker || (result == null && userText.Length == 0))
                        break;
                    if (result != null)
                        AddResultMessage(lines, result.Value, userText);
                    else if (currentTest != null)
                        AddLines(lines, AddContext(context, userText));
                    break;

                case "Warn":
                    if (result != null)
                    {
                        if (currentTest != null && result.Value == "NotRun")
                            currentTestIsNotRun = true;
                        AddResultMessage(lines, result.Value, AddTaefContext(context, userText));
                    }
                    else
                    {
                        AddLines(lines, "Warning: " + AddContext(context, userText));
                    }
                    break;

                case "Error":
                    if (result != null)
                        AddResultMessage(lines, result.Value, AddTaefContext(context, userText));
                    else
                        AddLines(lines, "Error: " + AddContext(context, userText) + GetSourceInformation(entry));
                    break;
            }
        }

        private static string GetTaefResult(string wttResult, bool isNotRun)
        {
            switch (wttResult)
            {
                case "Pass":
                    return "Passed";
                case "Fail":
                    return "Failed";
                case "Skipped":
                    return "Skipped";
                case "Blocked":
                    // WTT logs report NotRun as Blocked
                    return isNotRun ? "NotRun" : "Blocked";
                default:
                    return string.IsNullOrEmpty(wttResult) ? "Failed" : wttResult;
            }
        }

        private static void AddResultMessage(List<string> lines, string result, string message)
        {
            // TE.exe does not print results which are logged by TAEF without a message (e.g. for tests blocked by a setup failure)
            if (string.IsNullOrEmpty(message) || message == TaefContext + ": ")
                return;
            AddLines(lines, $"Test{result}: {message}");
        }

        private static string AddContext(string context, string text)
        {
            return string.IsNullOrEmpty(context) ? text : $"{context}: {text}";
        }

        private static string AddTaefContext(string context, string text)
        {
            return context == TaefContext && !string.IsNullOrEmpty(text) ? $"{context}: {text}" : text;
        }

        private static string GetSourceInformation(XElement entry)
        {
            string file = (string) entry.Attribute("File");
            string line = (string) entry.Attribute("Line");
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(line) || !int.TryParse(line, out int lineNumber) || lineNumber < 0)
                return "";
            return $" [File: {file}, Function: , Line: {lineNumber}]";
        }

        private static void AddLines(List<string> lines, string text)
        {
            lines.AddRange(text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None));
        }

        /// <summary>
        /// Deletes the WTT log file (if it exists); problems are logged as debug warnings.
        /// </summary>
        /// <summary>
        /// Deletes the WTT log and the file written while tests were running (which is left behind if TE.exe has
        /// terminated abnormally, see <see cref="TraceFileExtension"/>).
        /// </summary>
        public static void DeleteLogFile(string wttLogFile, ILogger logger)
        {
            foreach (string file in new[] { wttLogFile, wttLogFile + TraceFileExtension })
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch (Exception e)
                {
                    logger.DebugWarning($"Could not delete WTT log file '{file}': {e.Message}");
                }
            }
        }

    }

}
