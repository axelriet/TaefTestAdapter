// This file has been modified for TAEF support.

using System.Collections.Generic;

namespace TaefTestAdapter.Model
{
    /// <summary>
    /// A test of a TAEF test DLL (a test method, or a data row of a data-driven test).
    /// </summary>
    public class TestCase
    {
        /// <summary>Full path of the test DLL.</summary>
        public string Source { get; }

        /// <summary>The TAEF name of the test, e.g. <c>Ns::Class::Method#metadataSet0</c>.</summary>
        public string FullyQualifiedName { get; }
        /// <summary>The name shown to the user (the TAEF name).</summary>
        public string DisplayName { get; }

        /// <summary>The source file of the test method, or null if unknown.</summary>
        public string CodeFilePath { get; }
        /// <summary>The line of the test method in <see cref="CodeFilePath"/>, or 0 if unknown.</summary>
        public int LineNumber { get; }

        /// <summary>The traits of the test (from TAEF metadata and trait regexes).</summary>
        public List<Trait> Traits { get; } = new List<Trait>();
        /// <summary>Further properties of the test, e.g. <see cref="TestCaseMetaDataProperty"/>.</summary>
        public List<TestProperty> Properties { get; } = new List<TestProperty>();

        /// <summary>Creates a test.</summary>
        public TestCase(string fullyQualifiedName, string source, string displayName, string codeFilePath, int lineNumber)
        {
            FullyQualifiedName = fullyQualifiedName;
            Source = source;
            DisplayName = displayName;
            CodeFilePath = codeFilePath;
            LineNumber = lineNumber;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TestCase other))
                return false;

            return FullyQualifiedName == other.FullyQualifiedName && Source == other.Source;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + FullyQualifiedName.GetHashCode();
            hash = hash * 31 + Source.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return DisplayName;
        }

    }

}