// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// A test as listed by <c>TE.exe &lt;test DLL&gt; /listProperties</c> (see <see cref="StreamingListPropertiesParser"/>).
    /// </summary>
    public class TestCaseDescriptor
    {
        private static readonly IReadOnlyList<TaefProperty> NoProperties = new TaefProperty[0];
        private static readonly IReadOnlyList<TaefFixture> NoFixtures = new TaefFixture[0];

        /// <summary>
        /// The TAEF name of the test, e.g. <c>Ns::Class::Method</c> or <c>Ns::Class::Method#metadataSet0</c> (see
        /// <see cref="Helpers.TaefNames"/>). Data source error pseudo tests are named <c>...#error</c> (without the
        /// <c>[Blocked]</c> printed by TE.exe).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The class of the test as listed by TE.exe (e.g. <c>Ns::Class</c> or <c>Ns::Class#metadataSet0</c> for a
        /// data-driven class). For a data source error of a data-driven class, this is the name of the pseudo test.
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// The test DLL as listed by TE.exe (full path; followed by <c>#metadataSet&lt;n&gt;</c> if the module has
        /// lightweight data), or null if the listing did not contain it.
        /// </summary>
        public string TestDll { get; }

        /// <summary>
        /// The TAEF metadata of the test: module properties overridden by class properties overridden by test (method
        /// or data row) properties, compared ignoring case. Multi-valued properties are listed by TE.exe as one value
        /// (values joined by spaces).
        /// </summary>
        public IReadOnlyList<TaefProperty> Properties { get; }

        /// <summary>The values of the data row(s) of the test (module &lt; class &lt; test), e.g. <c>Color=Red</c>.</summary>
        public IReadOnlyList<TaefProperty> Data { get; }

        /// <summary>The setup and teardown methods of the module, the class and the test, in this order.</summary>
        public IReadOnlyList<TaefFixture> Fixtures { get; }

        /// <summary>
        /// For a data source error pseudo test (TE.exe lists it as <c>... #error [Blocked]</c>), the error message
        /// printed by TE.exe (e.g. <c>[HRESULT: 0x80070002] Failed to find the data source: ...</c>); null otherwise.
        /// </summary>
        public string DataSourceErrorMessage { get; }

        /// <summary>True if this is a pseudo test for a data source error (see <see cref="DataSourceErrorMessage"/>).</summary>
        public bool IsDataSourceError { get; }

        /// <summary>
        /// True if this is a pseudo test for the data source error of a data-driven <em>class</em> (TE.exe lists the class
        /// as <c>Ns::Class#error [Blocked]</c> without any tests, and executes a pseudo test <c>Ns::Class#error</c>).
        /// </summary>
        public bool IsClassDataSourceError { get; }

        /// <summary>Same as <see cref="Name"/>.</summary>
        public string FullyQualifiedName => Name;

        /// <summary>Same as <see cref="Name"/>.</summary>
        public string DisplayName => Name;

        public TestCaseDescriptor(string name, string className, string testDll = null,
            IEnumerable<TaefProperty> properties = null, IEnumerable<TaefProperty> data = null,
            IEnumerable<TaefFixture> fixtures = null,
            bool isDataSourceError = false, string dataSourceErrorMessage = null, bool isClassDataSourceError = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ClassName = className ?? "";
            TestDll = testDll;
            Properties = properties?.ToList().AsReadOnly() ?? NoProperties;
            Data = data?.ToList().AsReadOnly() ?? NoProperties;
            Fixtures = fixtures?.ToList().AsReadOnly() ?? NoFixtures;
            IsDataSourceError = isDataSourceError || isClassDataSourceError;
            IsClassDataSourceError = isClassDataSourceError;
            DataSourceErrorMessage = dataSourceErrorMessage;
        }

        /// <returns>The value of the property named <paramref name="name"/> (ignoring case), or null.</returns>
        public string GetProperty(string name)
        {
            return Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public override string ToString()
        {
            return Name;
        }

    }

}
