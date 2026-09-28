// This file has been modified for TAEF support.

using System;
using System.Linq;

namespace TaefTestAdapter.Model
{
    /// <summary>
    /// The number of tests of the test's TAEF class and of its test DLL, as found by test discovery. When tests are run,
    /// they allow selecting all tests of a class (<c>@Name='&lt;class&gt;::*'</c>) or of a test DLL (no selection at all)
    /// instead of naming each test, which keeps the TE.exe command lines short. The serialization is
    /// <c>&lt;tests in class&gt;:&lt;tests in test DLL&gt;</c>.
    /// </summary>
    public class TestCaseMetaDataProperty : TestProperty
    {
        /// <summary>Id of the property (e.g. of the corresponding VS test property).</summary>
        public static readonly string Id = $"{typeof(TestCaseMetaDataProperty).FullName}";

        /// <summary>Label of the property (e.g. of the corresponding VS test property).</summary>
        public const string Label = "Test case meta data";

        /// <summary>The number of tests of the test's TAEF class (see <see cref="Helpers.TaefNames.GetClassName"/>).</summary>
        public int NrOfTestCasesInClass { get; }

        /// <summary>The number of tests of the test's test DLL.</summary>
        public int NrOfTestCasesInTestDll { get; }

        /// <summary>Creates the property from its values.</summary>
        public TestCaseMetaDataProperty(int nrOfTestCasesInClass, int nrOfTestCasesInTestDll)
            : this($"{nrOfTestCasesInClass}:{nrOfTestCasesInTestDll}")
        {
        }

        /// <summary>Creates the property from its serialization <c>&lt;tests in class&gt;:&lt;tests in test DLL&gt;</c>.</summary>
        /// <exception cref="ArgumentException">If <paramref name="serialization"/> does not consist of two numbers.</exception>
        public TestCaseMetaDataProperty(string serialization) : base(serialization)
        {
            int[] values = serialization.Split(':').Select(int.Parse).ToArray();
            if (values.Length != 2)
                throw new ArgumentException(serialization, nameof(serialization));
            NrOfTestCasesInClass = values[0];
            NrOfTestCasesInTestDll = values[1];
        }
    }
}
