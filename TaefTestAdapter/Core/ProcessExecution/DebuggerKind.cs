// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.ComponentModel;
using TaefTestAdapter.Common;

// ReSharper disable NotResolvedInText

namespace TaefTestAdapter.ProcessExecution
{
    /// <summary>
    /// How tests are debugged (option DebuggerKind).
    /// </summary>
    [TypeConverter(typeof(DebuggerKindConverter))]
    public enum DebuggerKind { VsTestFramework, Native, ManagedAndNative }

    /// <summary>
    /// Extension methods of <see cref="DebuggerKind"/>.
    /// </summary>
    public static class DebuggerKindExtensions
    {
        private static readonly DebuggerKindConverter Converter = new DebuggerKindConverter();

        public static string ToReadableString(this DebuggerKind debuggerKind)
        {
            return Converter.ConvertToString(debuggerKind);
        }
    }

    /// <summary>
    /// Display strings of <see cref="DebuggerKind"/>.
    /// </summary>
    public class DebuggerKindConverter : EnumConverterBase<DebuggerKind>
    {
        public const string VsTestFramework = "VsTest framework";
        public const string Native = "Native";
        public const string ManagedAndNative = "Managed and native";

        public DebuggerKindConverter() : base(new Dictionary<DebuggerKind, string>
        {
            { DebuggerKind.VsTestFramework, VsTestFramework},
            { DebuggerKind.Native, Native},
            { DebuggerKind.ManagedAndNative, ManagedAndNative},
        }) {}

    }

}