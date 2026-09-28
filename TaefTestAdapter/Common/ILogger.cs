// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.ComponentModel;

namespace TaefTestAdapter.Common
{

    /// <summary>The severity of a logged message.</summary>
    public enum Severity { Info, Warning, Error }

    /// <summary>The amount of output of the adapter (option OutputMode); messages of a higher level are not logged.</summary>
    public enum OutputMode { None = 0, Info = 10, Debug = 20, Verbose = 30 }

    /// <summary>Whether a timestamp is added to the adapter's output (option TimestampMode).</summary>
    [TypeConverter(typeof(TimestampModeConverter))]
    public enum TimestampMode { Automatic, PrintTimestamp, DoNotPrintTimestamp }

    /// <summary>Whether the severity is added to the adapter's output (option SeverityMode).</summary>
    [TypeConverter(typeof(SeverityModeConverter))]
    public enum SeverityMode { Automatic, PrintSeverity, DoNotPrintSeverity }

    /// <summary>
    /// Logger of the adapter. Messages are logged depending on the <see cref="OutputMode"/>: Log* methods from
    /// <see cref="OutputMode.Info"/>, Debug* methods from <see cref="OutputMode.Debug"/>, Verbose* methods from
    /// <see cref="OutputMode.Verbose"/>.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Logs an informational message.</summary>
        void LogInfo(string message);
        /// <summary>Logs a warning.</summary>
        void LogWarning(string message);
        /// <summary>Logs an error.</summary>
        void LogError(string message);
        /// <summary>Logs an informational message if the output mode is at least <see cref="OutputMode.Debug"/>.</summary>
        void DebugInfo(string message);
        /// <summary>Logs a warning if the output mode is at least <see cref="OutputMode.Debug"/>.</summary>
        void DebugWarning(string message);
        /// <summary>Logs an error if the output mode is at least <see cref="OutputMode.Debug"/>.</summary>
        void DebugError(string message);
        /// <summary>Logs an informational message if the output mode is <see cref="OutputMode.Verbose"/>.</summary>
        void VerboseInfo(string message);

        /// <returns>The messages logged so far with one of <paramref name="severities"/> (e.g. for the summary of errors).</returns>
        IList<string> GetMessages(params Severity[] severities);

    }

    /// <summary>
    /// Display strings of <see cref="TimestampMode"/>.
    /// </summary>
    public class TimestampModeConverter : EnumConverterBase<TimestampMode>
    {
        public const string Automatic = "Automatic";
        public const string PrintTimeStamp = "Print timestamp";
        public const string DoNotPrintTimeStamp = "Do not print timestamp";

        public TimestampModeConverter() : base(new Dictionary<TimestampMode, string>
        {
            {TimestampMode.Automatic, Automatic},
            {TimestampMode.PrintTimestamp, PrintTimeStamp},
            {TimestampMode.DoNotPrintTimestamp, DoNotPrintTimeStamp}
        }){}
    }

    /// <summary>
    /// Display strings of <see cref="SeverityMode"/>.
    /// </summary>
    public class SeverityModeConverter : EnumConverterBase<SeverityMode>
    {
        public const string Automatic = "Automatic";
        public const string PrintSeverity = "Print severity";
        public const string DoNotPrintSeverity = "Do not print severity";

        public SeverityModeConverter() : base(new Dictionary<SeverityMode, string>
        {
            {SeverityMode.Automatic, Automatic},
            {SeverityMode.PrintSeverity, PrintSeverity},
            {SeverityMode.DoNotPrintSeverity, DoNotPrintSeverity}
        }){}
    }

}