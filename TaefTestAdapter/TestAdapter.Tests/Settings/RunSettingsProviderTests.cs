// This file has been modified for TAEF support.

using System.IO;
using System.Xml;
using System.Xml.XPath;
using FluentAssertions;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Settings
{

    [TestClass]
    public class RunSettingsProviderTests
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor__InstanceHasCorrectName()
        {
            new RunSettingsProvider().Name.Should().Be(TaefConstants.SettingsName);
            new RunSettingsProvider().Name.Should().Be("TaefTestAdapterSettings");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_SolutionSettings_SettingsAreMerged()
        {
            var provider = new RunSettingsProvider();
            provider.SettingsContainer.Should().BeNull();

            var settingsDoc = new XmlDocument();
            settingsDoc.Load(TestResources.ProviderDeliveredTestSettings);
            XPathNavigator navigator = settingsDoc.CreateNavigator();

            navigator.MoveToChild(Constants.RunSettingsName, "").Should().BeTrue();
            navigator.MoveToChild(TaefConstants.SettingsName, "").Should().BeTrue();
            navigator.MoveToChild("SolutionSettings", "").Should().BeTrue();
            navigator.MoveToRoot();

            navigator.MoveToRoot();
            navigator.MoveToChild(Constants.RunSettingsName, "").Should().BeTrue();
            navigator.MoveToChild(TaefConstants.SettingsName, "").Should().BeTrue();
            provider.Load(navigator.ReadSubtree());

            provider.SettingsContainer.Should().NotBeNull();
            provider.SettingsContainer.SolutionSettings.BatchForTestSetup.Should().Be("Solution");
            provider.SettingsContainer.SolutionSettings.NrOfTestRepetitions.Should().Be(2);
            provider.SettingsContainer.SolutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
            provider.SettingsContainer.SolutionSettings.TraitsRegexesBefore.Should().Be("Solution///A,B");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_InvalidSettings_DefaultSettingsAreUsed()
        {
            var provider = new RunSettingsProvider();

            provider.Load(CreateReader($"<{TaefConstants.SettingsName}><SolutionSettings><Settings><TestTimeout>0:99</TestTimeout></Settings></SolutionSettings><ProjectSettings/></{TaefConstants.SettingsName}>"));

            provider.SettingsContainer.Should().NotBeNull();
            provider.SettingsContainer.SolutionSettings.TestTimeout.Should().BeNull();
            provider.SettingsContainer.ProjectSettings.Should().BeEmpty();
            provider.LoadError.Should().Contain("TestTimeout").And.Contain("0:99");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_SchemaViolation_LoadErrorNamesInvalidElement()
        {
            var provider = new RunSettingsProvider();

            provider.Load(CreateReader($"<{TaefConstants.SettingsName}><SolutionSettings><Settings><UnknownSetting>true</UnknownSetting></Settings></SolutionSettings></{TaefConstants.SettingsName}>"));

            provider.SettingsContainer.ProjectSettings.Should().BeEmpty();
            provider.LoadError.Should().Contain("UnknownSetting");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_ProjectSettingsWithoutProjectRegex_LoadErrorNamesProjectRegex()
        {
            var provider = new RunSettingsProvider();

            provider.Load(CreateReader($"<{TaefConstants.SettingsName}><ProjectSettings><Settings><RunInProcess>true</RunInProcess></Settings></ProjectSettings></{TaefConstants.SettingsName}>"));

            provider.SettingsContainer.ProjectSettings.Should().BeEmpty();
            provider.LoadError.Should().Contain("ProjectRegex");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_ValidSettings_NoLoadError()
        {
            var provider = new RunSettingsProvider();

            provider.Load(CreateReader($"<{TaefConstants.SettingsName}><ProjectSettings><Settings ProjectRegex=\".*\"><RunInProcess>true</RunInProcess></Settings></ProjectSettings></{TaefConstants.SettingsName}>"));

            provider.LoadError.Should().BeNull();
            provider.SettingsContainer.ProjectSettings.Should().ContainSingle().Which.RunInProcess.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_ValidSettingsAfterInvalidSettings_LoadErrorIsReset()
        {
            // a test host kept alive by Visual Studio might reuse the provider for further runs
            var provider = new RunSettingsProvider();
            provider.Load(CreateReader($"<{TaefConstants.SettingsName}><SolutionSettings><Settings><TestTimeout>0:99</TestTimeout></Settings></SolutionSettings></{TaefConstants.SettingsName}>"));
            provider.LoadError.Should().NotBeNull();

            provider.Load(CreateReader($"<{TaefConstants.SettingsName}><SolutionSettings><Settings><TestTimeout>0:59</TestTimeout></Settings></SolutionSettings></{TaefConstants.SettingsName}>"));

            provider.LoadError.Should().BeNull();
            provider.SettingsContainer.SolutionSettings.TestTimeout.Should().Be("0:59");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Load_OtherSettings_DefaultSettingsAreUsed()
        {
            var provider = new RunSettingsProvider();

            // settings of another adapter (here: of the adapter shipped with TAEF) are ignored
            provider.Load(CreateReader("<TaefAdapterSettings><SolutionSettings><Settings><UnknownSetting>true</UnknownSetting></Settings></SolutionSettings></TaefAdapterSettings>"));

            provider.SettingsContainer.Should().NotBeNull();
            provider.SettingsContainer.ProjectSettings.Should().BeEmpty();
        }

        private static XmlReader CreateReader(string xml)
        {
            return XmlReader.Create(new StringReader(xml));
        }

    }

}
