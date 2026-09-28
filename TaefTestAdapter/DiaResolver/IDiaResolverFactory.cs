// This file has been modified for TAEF support.

using TaefTestAdapter.Common;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>
    /// Creates resolvers which read symbols of binaries from their PDBs.
    /// </summary>
    public interface IDiaResolverFactory
    {
        /// <summary>Creates a resolver for <paramref name="binary"/>, reading symbols from <paramref name="pdb"/>.</summary>
        IDiaResolver Create(string binary, string pdb, ILogger logger);
    }
}