// This file has been modified for TAEF support.

using TaefTestAdapter.Settings;

namespace TaefTestAdapter.TestAdapter.Settings
{

    /// <summary>
    /// The settings of the Visual Studio options, as updated by the Visual Studio package.
    /// </summary>
    public interface IGlobalRunSettingsInternal : IGlobalRunSettings
    {
        /// <summary>The settings of the Visual Studio options.</summary>
        new RunSettings RunSettings { get; set; }
    }

}