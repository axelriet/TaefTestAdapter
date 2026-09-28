// This file has been modified for TAEF support.

using System;
using System.Text;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace TaefTestAdapter.TestAdapter.Framework
{

    /// <summary>
    /// Logger sending the adapter's messages to the VsTest framework (i.e., to the Tests output window of Visual Studio
    /// or to the console of vstest.console.exe). Depending on the options, messages are prefixed with
    /// <c>[TAEF]</c> (see <see cref="TaefConstants.OutputPrefix"/>), a timestamp and the severity.
    /// </summary>
    public class VsTestFrameworkLogger : LoggerBase
    {
        private readonly IMessageLogger _logger;
        private readonly Func<TimestampMode> _timeStampMode;
        private readonly Func<SeverityMode> _severityMode;
        private readonly Func<bool> _prefixOutput;

        public VsTestFrameworkLogger(IMessageLogger logger, Func<OutputMode> outputMode, Func<TimestampMode> timestampMode, Func<SeverityMode> severityMode, Func<bool> prefixOutput)
            : base(outputMode)
        {
            _logger = logger;
            _timeStampMode = timestampMode;
            _severityMode = severityMode;
            _prefixOutput = prefixOutput;
        }


        public override void Log(Severity severity, string message)
        {
            TestMessageLevel level = severity.GetTestMessageLevel();

            var builder = new StringBuilder();
            AppendOutputPrefix(builder, level);
            builder.Append(message);

            var finalMessage = builder.ToString();
            if (string.IsNullOrWhiteSpace(finalMessage))
            {
                // The VsTest framework does not accept empty messages...
                // But it accepts an 'INVISIBLE SEPARATOR' (U+2063)  :-)
                finalMessage = "\u2063";
            }

            _logger.SendMessage(level, finalMessage);
            ReportFinalLogEntry(
                new LogEntry
                {
                    Severity = level.GetSeverity(),
                    Message = finalMessage
                });
        }

        private void AppendOutputPrefix(StringBuilder builder, TestMessageLevel level)
        {
            if (_prefixOutput())
            {
                builder.Append(TaefConstants.OutputPrefix);
            }

            var timestampMode = _timeStampMode();
            if (timestampMode == TimestampMode.PrintTimestamp ||
                timestampMode == TimestampMode.Automatic && !VsVersionUtils.VsVersion.PrintsTimeStampAndSeverity())
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(Utils.GetTimestamp());
            }

            var severityMode = _severityMode();
            if (level > TestMessageLevel.Informational &&
                (severityMode == SeverityMode.PrintSeverity ||
                 severityMode == SeverityMode.Automatic && !VsVersionUtils.VsVersion.PrintsTimeStampAndSeverity()))
            {
                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(GetSeverity(level));
            }

            if (builder.Length > 0)
            {
                builder.Insert(0, '[');
                builder.Append("] ");
            }
        }

        private string GetSeverity(TestMessageLevel level)
        {
            switch (level)
            {
                case TestMessageLevel.Informational: return "";
                case TestMessageLevel.Warning: return "Warning";
                case TestMessageLevel.Error: return "ERROR";
                default:
                    throw new InvalidOperationException($"Unknown literal {level}");
            }
        }
    }

}