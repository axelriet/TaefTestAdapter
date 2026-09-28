// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using Microsoft.VisualStudio.Shell;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.VsPackage.Helpers
{
    /// <summary>
    /// Logs to Visual Studio's activity log (which is written if Visual Studio is started with option /log).
    /// Can be used from any thread.
    /// </summary>
    public class ActivityLogLogger : LoggerBase
    {
        public ActivityLogLogger(Func<OutputMode> outputMode) : base(outputMode)
        {
        }

        public override void Log(Severity severity, string message)
        {
            string source = Strings.Instance.ExtensionName;

            bool logged;
            switch (severity)
            {
                case Severity.Info:
                    logged = ActivityLog.TryLogInformation(source, message);
                    break;
                case Severity.Warning:
                    logged = ActivityLog.TryLogWarning(source, message);
                    break;
                case Severity.Error:
                    logged = ActivityLog.TryLogError(source, message);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown enum literal: {severity}");
            }

            if (!logged)
            {
                Console.WriteLine($"{source}: {severity} - {message}");
            }
        }
    }

}
