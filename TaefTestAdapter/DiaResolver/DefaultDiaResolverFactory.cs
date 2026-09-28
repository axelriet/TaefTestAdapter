// This file has been modified for TAEF support.

using TaefTestAdapter.Common;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>
    /// Creates <see cref="DiaResolver"/>s.
    /// </summary>
    public class DefaultDiaResolverFactory : IDiaResolverFactory
    {
        /// <summary>
        /// The instance.
        /// </summary>
        public static IDiaResolverFactory Instance { get; } = new DefaultDiaResolverFactory();

        public IDiaResolver Create(string binary, string pdb, ILogger logger)
        {
            return new DiaResolver(binary, pdb, logger);
        }
    }
}