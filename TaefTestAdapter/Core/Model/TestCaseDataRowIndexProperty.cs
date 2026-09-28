// This file has been added for TAEF support.

using System;
using System.Globalization;

namespace TaefTestAdapter.Model
{

    /// <summary>
    /// The index of the data row of a data-driven test, i.e. its data value <c>Index</c> as listed by
    /// <c>TE.exe /listProperties</c> (<c>Data[Index] = &lt;n&gt;</c>; the index of the row within the method's table data
    /// source, or within the class's if only the class is data-driven). Lightweight data rows have no such value (unless a
    /// data value is named <c>Index</c>). It allows selecting exactly that row with <c>@Data:Index=&lt;n&gt;</c> if the
    /// name of the test is a pattern for TE.exe (e.g. because the row name contains <c>*</c> or <c>?</c>).
    /// </summary>
    public class TestCaseDataRowIndexProperty : TestProperty
    {
        /// <summary>
        /// Id of the property (e.g. of the corresponding VS test property).
        /// </summary>
        public static readonly string Id = $"{typeof(TestCaseDataRowIndexProperty).FullName}";
        /// <summary>
        /// Label of the property (e.g. of the corresponding VS test property).
        /// </summary>
        public const string Label = "Data row index";

        /// <summary>The index (not negative).</summary>
        public int Index { get; }

        /// <summary>
        /// Creates the property for data row <paramref name="index"/>.
        /// </summary>
        public TestCaseDataRowIndexProperty(int index) : base(index.ToString(CultureInfo.InvariantCulture))
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The data row index must not be negative");
            Index = index;
        }

        /// <exception cref="ArgumentException">If <paramref name="serialization"/> is no valid serialization (see <see cref="TryParse"/>).</exception>
        public TestCaseDataRowIndexProperty(string serialization) : base(serialization)
        {
            if (!TryParseIndex(serialization, out int index))
                throw new ArgumentException($"Invalid data row index: '{serialization}'", nameof(serialization));
            Index = index;
        }

        /// <returns>
        /// The property for the data value <paramref name="value"/> (as listed by TE.exe, or a serialization), or null if
        /// it is not the canonical representation of a non-negative number (e.g. <c>07</c> or <c>abc</c>, which may be the
        /// value of a lightweight data value named <c>Index</c>): TE.exe might then compare the value differently, and a
        /// selection which does not match the test would make TE.exe not run it.
        /// </returns>
        public static TestCaseDataRowIndexProperty TryParse(string value)
        {
            return TryParseIndex(value, out int index) ? new TestCaseDataRowIndexProperty(index) : null;
        }

        private static bool TryParseIndex(string value, out int index)
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index)
                && index.ToString(CultureInfo.InvariantCulture) == value;
        }
    }

}
