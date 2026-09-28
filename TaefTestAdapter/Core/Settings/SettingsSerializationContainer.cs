// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.Xml.Serialization;

namespace TaefTestAdapter.Settings
{

    /// <summary>
    /// XML serialization of the <c>&lt;TaefTestAdapterSettings&gt;</c> node of a settings file.
    /// </summary>
    [XmlRoot(TaefConstants.SettingsName)]
    public class SettingsSerializationContainer
    {
        /// <summary>
        /// The <c>&lt;SolutionSettings&gt;</c> node.
        /// </summary>
        public SingleSettings SolutionSettings { get; set; } = new SingleSettings();

        /// <summary>
        /// The <c>&lt;Settings&gt;</c> nodes of the <c>&lt;ProjectSettings&gt;</c> node.
        /// </summary>
        [XmlArray("ProjectSettings")]
        [XmlArrayItem("Settings")]
        public SettingsList SettingsList { get; set; } = new SettingsList();

        /// <summary>
        /// Creates an empty container (for deserialization).
        /// </summary>
        public SettingsSerializationContainer() { }

        /// <summary>
        /// Creates the serialization of <paramref name="container"/>.
        /// </summary>
        public SettingsSerializationContainer(ITaefTestAdapterSettingsContainer container)
        {
            SolutionSettings.Settings = container.SolutionSettings;
            SettingsList.AddRange(container.ProjectSettings);
        }

    }

    /// <summary>
    /// A list of project settings.
    /// </summary>
    public class SettingsList : List<RunSettings> { }

    /// <summary>
    /// A node containing one <c>&lt;Settings&gt;</c> node.
    /// </summary>
    public class SingleSettings
    {
        /// <summary>
        /// Empty settings unless deserialized from an XML element: elements <c>&lt;SolutionSettings&gt;</c> and
        /// <c>&lt;Settings&gt;</c> are optional (see TaefTestAdapterSettings.xsd), e.g. a settings file might only contain
        /// project settings.
        /// </summary>
        public RunSettings Settings { get; set; } = new RunSettings();
    }

}