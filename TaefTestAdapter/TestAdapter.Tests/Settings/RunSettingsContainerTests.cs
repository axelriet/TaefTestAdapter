// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Settings
{
    [TestClass]
    public class RunSettingsContainerTests
    {
        private RunSettingsContainer _container;
        private RunSettings _solutionSettings;
        private RunSettings _projectSettings1;
        private RunSettings _projectSettings2;

        [TestInitialize]
        public void Setup()
        {
            _solutionSettings = new RunSettings
            {
                ProjectRegex = null,
                AdditionalTestExecutionParam = "solution"
            };

            _projectSettings1 = new RunSettings
            {
                ProjectRegex = @".*PerformanceTests_taef\.dll",
                AdditionalTestExecutionParam = "project1"
            };

            _projectSettings2 = new RunSettings
            {
                ProjectRegex = @".*UnitTests_taef\.dll",
                AdditionalTestExecutionParam = "project2"
            };

            _container = new RunSettingsContainer(_solutionSettings);
            _container.ProjectSettings.Add(_projectSettings1);
            _container.ProjectSettings.Add(_projectSettings2);
        }

        #region Settings for test DLLs and merging

        [TestMethod]
        [TestCategory(Unit)]
        public void GetSettingsForProject_InvalidProject_NullIsReturned()
        {
            var settings = _container.GetSettingsForTestDll("foo");
            settings.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetSettingsForProject_ValidProject_ProjectSettingsAreReturned()
        {
            var settings = _container.GetSettingsForTestDll(@"C:\Users\User\Desktop\MyPerformanceTests_taef.dll");
            settings.Should().Be(_projectSettings1);
            settings.AdditionalTestExecutionParam.Should().Be("project1");

            settings = _container.GetSettingsForTestDll(@"TheUnitTests_taef.dll");
            settings.Should().Be(_projectSettings2);
            settings.AdditionalTestExecutionParam.Should().Be("project2");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetUnsetValuesFrom__ResultsInCorrectlyMergedContainer()
        {
            var solutionSettings = new RunSettings
            {
                ProjectRegex = null,
                AdditionalTestExecutionParam = "foo",
                BatchForTestSetup = "solution",
                IsolationLevel = TaefIsolationLevel.Class
            };

            var projectSettings1 = new RunSettings
            {
                ProjectRegex = @".*PerformanceTests_taef\.dll",
                AdditionalTestExecutionParam = "foo",
                BatchForTestSetup = "project1"
            };

            var projectSettings2 = new RunSettings
            {
                ProjectRegex = @".*IntegrationTests_taef\.dll",
                AdditionalTestExecutionParam = "foo",
                BatchForTestSetup = "project2"
            };

            var container = new RunSettingsContainer(solutionSettings);
            container.ProjectSettings.Add(projectSettings1);
            container.ProjectSettings.Add(projectSettings2);

            _container.GetUnsetValuesFrom(container);

            _container.SolutionSettings.AdditionalTestExecutionParam.Should().Be("solution");
            _container.SolutionSettings.BatchForTestSetup.Should().Be("solution");
            _container.SolutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);

            _container.ProjectSettings.Should().HaveCount(3);
            _container.ProjectSettings[0].AdditionalTestExecutionParam.Should().Be("project1");
            _container.ProjectSettings[1].AdditionalTestExecutionParam.Should().Be("project2");
            _container.ProjectSettings[2].AdditionalTestExecutionParam.Should().Be("foo");
            _container.ProjectSettings[0].BatchForTestSetup.Should().Be("project1");
            _container.ProjectSettings[1].BatchForTestSetup.Should().BeNull();
            _container.ProjectSettings[2].BatchForTestSetup.Should().Be("project2");
        }

        #endregion

        #region Loading from XML

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_UserSettings_AreLoadedCorrectly()
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(TestResources.UserTestSettings);
            var navigator = xmlDocument.CreateNavigator();
            navigator.MoveToChild(Constants.RunSettingsName, "");
            navigator.MoveToChild(TaefConstants.SettingsName, "");

            var runSettingsContainer = RunSettingsContainer.LoadFromXml(navigator);

            runSettingsContainer.Should().NotBeNull();
            runSettingsContainer.SolutionSettings.Should().NotBeNull();
            runSettingsContainer.ProjectSettings.Should().ContainSingle();

            runSettingsContainer.SolutionSettings.MaxNrOfThreads.Should().Be(3);
            runSettingsContainer.SolutionSettings.RunIgnoredTests.Should().BeTrue();
            runSettingsContainer.SolutionSettings.TestTimeout.Should().Be("0:0:3");
            runSettingsContainer.ProjectSettings[0].MaxNrOfThreads.Should().Be(4);
            runSettingsContainer.ProjectSettings[0].ProjectRegex.Should().Be(@"LoadTests_taef\.dll|CrashingTests_taef\.dll");
            runSettingsContainer.SolutionSettings.TraitsRegexesBefore.Should().Be("User///A,B");
            runSettingsContainer.ProjectSettings[0].TraitsRegexesBefore.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_AllTestSettings_AllSettingsAreLoaded()
        {
            RunSettingsContainer container = LoadFromFile(TestResources.AllTestSettings);

            RunSettings settings = container.SolutionSettings;
            // SkipOriginCheck is only mentioned in a comment (the VS extension ignores it in settings files); the others are set by the adapter itself
            var notContained = new[] { nameof(RunSettings.ProjectRegex), nameof(RunSettings.SkipOriginCheck), nameof(RunSettings.DebuggingNamedPipeId),
                nameof(RunSettings.SolutionDir), nameof(RunSettings.PlatformName), nameof(RunSettings.ConfigurationName) };
            foreach (PropertyInfo property in typeof(RunSettings).GetProperties().Where(p => p.CanWrite && !notContained.Contains(p.Name)))
            {
                property.GetValue(settings).Should().NotBeNull($"setting {property.Name} is contained in {TestResources.AllTestSettings}");
            }

            settings.TeExecutable.Should().Be("");
            settings.RunIgnoredTests.Should().BeFalse();
            settings.BreakOnError.Should().BeFalse();
            settings.NrOfTestRepetitions.Should().Be(1);
            settings.TestTimeout.Should().Be("");
            settings.IsolationLevel.Should().Be(TaefIsolationLevel.Default);
            settings.RunInProcess.Should().BeFalse();
            settings.WorkingDir.Should().Be("$(TestDllDir)");
            settings.DebuggerKind.Should().Be(DebuggerKind.Native);
            settings.MissingTestsReportMode.Should().Be(MissingTestsReportMode.ReportAsNotFound);
            settings.PrefixOutputWithTaef.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_NewTaefSettings_AreLoadedCorrectly()
        {
            RunSettingsContainer container = LoadFromXml(
                @"<TeExecutable>$(SolutionDir)tools\TAEF</TeExecutable>
                  <RunIgnoredTests>true</RunIgnoredTests>
                  <TestTimeout>0:05</TestTimeout>
                  <IsolationLevel>Method</IsolationLevel>
                  <RunInProcess>true</RunInProcess>
                  <NrOfTestRepetitions>3</NrOfTestRepetitions>
                  <PrefixOutputWithTaef>true</PrefixOutputWithTaef>",
                @"<Settings ProjectRegex="".*\\Slow_taef\.dll$""><TestTimeout>1.02:03:04.5</TestTimeout><IsolationLevel>Module</IsolationLevel></Settings>");

            RunSettings settings = container.SolutionSettings;
            settings.TeExecutable.Should().Be(@"$(SolutionDir)tools\TAEF");
            settings.RunIgnoredTests.Should().BeTrue();
            settings.TestTimeout.Should().Be("0:05");
            settings.IsolationLevel.Should().Be(TaefIsolationLevel.Method);
            settings.RunInProcess.Should().BeTrue();
            settings.NrOfTestRepetitions.Should().Be(3);
            settings.PrefixOutputWithTaef.Should().BeTrue();

            container.ProjectSettings.Should().ContainSingle();
            container.ProjectSettings[0].TestTimeout.Should().Be("1.02:03:04.5");
            container.ProjectSettings[0].IsolationLevel.Should().Be(TaefIsolationLevel.Module);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_ValidTestTimeouts_AreAccepted()
        {
            foreach (string timeout in new[] { "", "0", "23", "0:05", "1:30:15", "0:0:1.5", "2.03:04:05", "0:0:0.1234567", " 0:0:30 " })
            {
                Action action = () => LoadFromXml($"<TestTimeout>{timeout}</TestTimeout>");
                action.Should().NotThrow($"'{timeout}' is a valid test timeout");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_InvalidTestTimeouts_AreRejected()
        {
            // TE.exe ignores such values (with a warning): hours > 23, minutes or seconds > 59, more than 7 fractional digits, no numbers
            foreach (string timeout in new[] { "24", "0:60", "0:90", "0:0:60", "abc", "1.24", "0:0:1.12345678", "-1", "0:05:" })
            {
                Action action = () => LoadFromXml($"<TestTimeout>{timeout}</TestTimeout>");
                action.Should().Throw<InvalidRunSettingsException>($"'{timeout}' is not a valid test timeout");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_IsolationLevels_AllLevelsAreAcceptedAndInvalidOnesRejected()
        {
            foreach (TaefIsolationLevel level in Enum.GetValues(typeof(TaefIsolationLevel)))
            {
                LoadFromXml($"<IsolationLevel>{level}</IsolationLevel>").SolutionSettings.IsolationLevel.Should().Be(level);
            }

            Action action = () => LoadFromXml("<IsolationLevel>Bogus</IsolationLevel>");
            action.Should().Throw<InvalidRunSettingsException>();
            action = () => LoadFromXml("<IsolationLevel>class</IsolationLevel>");
            action.Should().Throw<InvalidRunSettingsException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_NrOfTestRepetitionsBelowOne_IsRejected()
        {
            Action action = () => LoadFromXml("<NrOfTestRepetitions>0</NrOfTestRepetitions>");
            action.Should().Throw<InvalidRunSettingsException>();
            action = () => LoadFromXml("<NrOfTestRepetitions>-1</NrOfTestRepetitions>");
            action.Should().Throw<InvalidRunSettingsException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_UnknownSettings_AreRejected()
        {
            foreach (string element in new[] { "<UnknownSetting>true</UnknownSetting>", "<AnotherUnknownSetting>42</AnotherUnknownSetting>" })
            {
                Action action = () => LoadFromXml(element);
                action.Should().Throw<InvalidRunSettingsException>($"{element} is not a valid setting");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_InvalidRegexes_AreRejected()
        {
            Action action = () => LoadFromXml("", @"<Settings ProjectRegex=""(unclosed""><RunInProcess>true</RunInProcess></Settings>");
            action.Should().Throw<InvalidRunSettingsException>().WithMessage("*ProjectRegex*");

            action = () => LoadFromXml("<TestDiscoveryRegex>[z-a]</TestDiscoveryRegex>");
            action.Should().Throw<InvalidRunSettingsException>().WithMessage("*TestDiscoveryRegex*");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_ProjectSettingsWithoutProjectRegex_AreRejected()
        {
            Action action = () => LoadFromXml("", "<Settings><RunInProcess>true</RunInProcess></Settings>");
            action.Should().Throw<InvalidRunSettingsException>().WithMessage("*ProjectRegex*");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetSettingsForTestDll_ProjectSettingsWithoutProjectRegex_AreIgnored()
        {
            var container = new RunSettingsContainer();
            container.ProjectSettings.Add(new RunSettings { RunInProcess = true });

            container.GetSettingsForTestDll(@"C:\My_taef.dll").Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LoadFromXml_OtherRootElement_DefaultContainerIsReturned()
        {
            var document = new XmlDocument();
            document.LoadXml("<SomeOtherAdapterSettings><Settings /></SomeOtherAdapterSettings>");

            RunSettingsContainer container = RunSettingsContainer.LoadFromXml(document.CreateNavigator().SelectSingleNode("/*"));

            container.Should().NotBeNull();
            container.ProjectSettings.Should().BeEmpty();
            container.SolutionSettings.RunInProcess.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToXml_ContainerWithTaefSettings_RoundTripKeepsSettings()
        {
            var solutionSettings = new RunSettings
            {
                TeExecutable = @"C:\TAEF",
                RunIgnoredTests = true,
                TestTimeout = "0:0:30",
                IsolationLevel = TaefIsolationLevel.Test,
                RunInProcess = true,
                NrOfTestRepetitions = 2
            };
            var container = new RunSettingsContainer(solutionSettings);
            container.ProjectSettings.Add(new RunSettings(@".*\\My_taef\.dll$") { RunInProcess = false, IsolationLevel = TaefIsolationLevel.Module });

            XmlElement xml = container.ToXml();
            xml.Name.Should().Be(TaefConstants.SettingsName);
            RunSettingsContainer loaded = RunSettingsContainer.LoadFromXml(xml.CreateNavigator());

            loaded.SolutionSettings.TeExecutable.Should().Be(@"C:\TAEF");
            loaded.SolutionSettings.RunIgnoredTests.Should().BeTrue();
            loaded.SolutionSettings.TestTimeout.Should().Be("0:0:30");
            loaded.SolutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Test);
            loaded.SolutionSettings.RunInProcess.Should().BeTrue();
            loaded.SolutionSettings.NrOfTestRepetitions.Should().Be(2);
            loaded.SolutionSettings.BreakOnError.Should().BeNull();
            loaded.ProjectSettings.Should().ContainSingle();
            loaded.ProjectSettings[0].ProjectRegex.Should().Be(@".*\\My_taef\.dll$");
            loaded.ProjectSettings[0].RunInProcess.Should().BeFalse();
            loaded.ProjectSettings[0].IsolationLevel.Should().Be(TaefIsolationLevel.Module);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetUnsetValuesFrom_SettingsFileWithoutAdapterSettings_ReturnsFalse()
        {
            var container = new RunSettingsContainer();

            container.GetUnsetValuesFrom(TestResources.UserTestSettingsWithoutRunSettingsNode).Should().BeFalse();
            container.GetUnsetValuesFrom(TestResources.SolutionTestSettings).Should().BeTrue();

            container.SolutionSettings.BatchForTestSetup.Should().Be("Solution");
            container.SolutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
        }

        private static RunSettingsContainer LoadFromXml(string solutionSettings, string projectSettings = "")
        {
            string xml = $"<{TaefConstants.SettingsName}><SolutionSettings><Settings>{solutionSettings}</Settings></SolutionSettings><ProjectSettings>{projectSettings}</ProjectSettings></{TaefConstants.SettingsName}>";
            var document = new XPathDocument(new StringReader(xml));
            XPathNavigator navigator = document.CreateNavigator();
            navigator.MoveToChild(TaefConstants.SettingsName, "").Should().BeTrue();
            return RunSettingsContainer.LoadFromXml(navigator);
        }

        private static RunSettingsContainer LoadFromFile(string settingsFile)
        {
            var document = new XPathDocument(settingsFile);
            XPathNavigator navigator = document.CreateNavigator();
            navigator.MoveToChild(Constants.RunSettingsName, "").Should().BeTrue();
            navigator.MoveToChild(TaefConstants.SettingsName, "").Should().BeTrue();
            return RunSettingsContainer.LoadFromXml(navigator);
        }

        #endregion

    }
}
