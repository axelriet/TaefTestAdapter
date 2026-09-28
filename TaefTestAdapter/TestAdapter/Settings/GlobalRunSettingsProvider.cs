// This file has been modified for TAEF support.

using System.ComponentModel.Composition;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter.TestAdapter.Settings
{

    /// <summary>
    /// Holds the settings of the Visual Studio options (MEF component shared by the VS package and the test adapter).
    /// </summary>
    [Export(typeof(IGlobalRunSettings))]
    [Export(typeof(IGlobalRunSettingsInternal))]
    public class GlobalRunSettingsProvider : IGlobalRunSettingsInternal
    {
        /// <summary>
        /// The settings of the Visual Studio options.
        /// </summary>
        public RunSettings RunSettings { get; set; } = new RunSettings();
    }

}