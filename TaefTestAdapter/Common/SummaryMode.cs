// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.ComponentModel;

namespace TaefTestAdapter.Common
{
    /// <summary>
    /// Whether a summary of warnings and errors is printed after test discovery and execution (option SummaryMode).
    /// </summary>
    [TypeConverter(typeof(SummaryModeConverter))]
    public enum SummaryMode
    {
        Never,
        Error,
        WarningOrError
    }

    /// <summary>
    /// Display strings of <see cref="SummaryMode"/>.
    /// </summary>
    public class SummaryModeConverter : EnumConverterBase<SummaryMode>
    {
        public const string Never = "Never";
        public const string Error = "If errors occured";
        public const string WarningOrError = "If warnings or errors occured";

        public SummaryModeConverter() : base(new Dictionary<SummaryMode, string>
        {
            { SummaryMode.Never, Never},
            { SummaryMode.Error, Error},
            { SummaryMode.WarningOrError, WarningOrError},
        }) {}
    }

}