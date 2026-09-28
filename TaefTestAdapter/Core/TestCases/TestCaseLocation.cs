// This file has been modified for TAEF support.

using TaefTestAdapter.DiaResolver;

namespace TaefTestAdapter.TestCases
{

    /// <summary>The source location of a test method (the location of the function in the PDB).</summary>
    public class TestCaseLocation : SourceFileLocation
    {
        public TestCaseLocation(string symbol, string sourceFile, uint line) : base(symbol, sourceFile, line)
        {
        }
    }

}
