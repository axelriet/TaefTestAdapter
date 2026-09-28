// This file has been modified for TAEF support.

using TaefTestAdapter.Settings;

namespace TaefTestAdapter.TestAdapter.Settings
{

    /// <summary>
    /// The settings of the Visual Studio options.
    /// </summary>
    public interface IGlobalRunSettings
    {
        /// <summary>The settings of the Visual Studio options.</summary>
        RunSettings RunSettings { get; }
    }

}