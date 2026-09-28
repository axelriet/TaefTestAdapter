// This file has been added for TAEF support.

using System.Collections.Generic;
using System.ComponentModel;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.Settings
{
    /// <summary>
    /// Value of TE.exe's <c>/isolationLevel</c> switch (minimum isolation of the test host processes).
    /// <see cref="Default"/> means that the switch is not passed. <see cref="Test"/> and <see cref="Method"/> are
    /// synonyms in TAEF.
    /// </summary>
    [TypeConverter(typeof(TaefIsolationLevelConverter))]
    public enum TaefIsolationLevel { Default, Test, Method, Class, Module }

    /// <summary>
    /// Extension methods of <see cref="TaefIsolationLevel"/>.
    /// </summary>
    public static class TaefIsolationLevelExtensions
    {
        private static readonly TaefIsolationLevelConverter Converter = new TaefIsolationLevelConverter();

        public static string ToReadableString(this TaefIsolationLevel isolationLevel)
        {
            return Converter.ConvertToString(isolationLevel);
        }

        /// <returns>The value to be passed to TE.exe's <c>/isolationLevel:</c> switch, or null for <see cref="TaefIsolationLevel.Default"/>.</returns>
        public static string ToTeValue(this TaefIsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case TaefIsolationLevel.Test:
                case TaefIsolationLevel.Method:
                case TaefIsolationLevel.Class:
                case TaefIsolationLevel.Module:
                    return isolationLevel.ToString();
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Display strings of <see cref="TaefIsolationLevel"/>.
    /// </summary>
    public class TaefIsolationLevelConverter : EnumConverterBase<TaefIsolationLevel>
    {
        public const string Default = "TAEF default";
        public const string Test = "Test";
        public const string Method = "Method";
        public const string Class = "Class";
        public const string Module = "Module";

        public TaefIsolationLevelConverter() : base(new Dictionary<TaefIsolationLevel, string>
        {
            { TaefIsolationLevel.Default, Default },
            { TaefIsolationLevel.Test, Test },
            { TaefIsolationLevel.Method, Method },
            { TaefIsolationLevel.Class, Class },
            { TaefIsolationLevel.Module, Module },
        }) {}
    }

}
