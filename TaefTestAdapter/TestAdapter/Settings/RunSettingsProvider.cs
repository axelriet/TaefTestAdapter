// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System.ComponentModel.Composition;
using System.Xml;
using System.Xml.XPath;

namespace TaefTestAdapter.TestAdapter.Settings
{
    /// <summary>
    /// Settings provider of the test platform which loads the <c>&lt;TaefTestAdapterSettings&gt;</c> node of the run
    /// settings.
    /// </summary>
    [Export(typeof(ISettingsProvider))]
    [SettingsName(TaefConstants.SettingsName)]
    public class RunSettingsProvider : ISettingsProvider
    {
        // virtual for mocking
        public virtual RunSettingsContainer SettingsContainer { get; private set; }

        /// <summary>
        /// Why the adapter's settings could not be loaded (<see cref="SettingsContainer"/> then contains default settings);
        /// null if they have been loaded (or if there were none). Logged by <see cref="CommonFunctions.CreateEnvironment"/>
        /// since there is no logger when the test platform calls <see cref="Load"/>.
        /// </summary>
        // virtual for mocking
        public virtual string LoadError { get; private set; }

        public string Name { get; private set; } = TaefConstants.SettingsName;

        public void Load(XmlReader reader)
        {
            // the provider might be reused for further runs (e.g. by a test host kept alive by Visual Studio)
            LoadError = null;

            var document = new XPathDocument(reader);
            var navigator = document.CreateNavigator();
            RunSettingsContainer container = null;
            try
            {
                if (navigator.MoveToChild(TaefConstants.SettingsName, ""))
                    container = RunSettingsContainer.LoadFromXml(navigator);
            }
            catch (InvalidRunSettingsException e)
            {
                // the message of a schema violation does not tell which element or value is invalid, its inner exception does
                string innerMessage = e.InnerException?.Message;
                LoadError = string.IsNullOrEmpty(innerMessage) || e.Message.Contains(innerMessage)
                    ? e.Message
                    : $"{e.Message}: {innerMessage}";
            }
            SettingsContainer = container ?? new RunSettingsContainer();
        }

    }

}