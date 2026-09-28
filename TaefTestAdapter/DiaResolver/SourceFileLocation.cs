// This file has been modified for TAEF support.

namespace TaefTestAdapter.DiaResolver
{

    /// <summary>The source location of a symbol (function) found in a PDB.</summary>
    public class SourceFileLocation
    {
        /// <summary>The name of the symbol as stored in the PDB (e.g. <c>Ns::Class::Method</c>).</summary>
        public string Symbol { get; }

        /// <summary>The source file as stored in the PDB (usually a full path).</summary>
        public string Sourcefile { get; }

        public uint Line { get; }

        public SourceFileLocation(string symbol, string sourceFile, uint line)
        {
            Symbol = symbol;
            Sourcefile = sourceFile;
            Line = line;
        }

        public override string ToString()
        {
            return $"{Symbol} ({Sourcefile}:{Line})";
        }
    }

}
