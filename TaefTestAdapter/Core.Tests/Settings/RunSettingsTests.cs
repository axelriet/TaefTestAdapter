// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Settings
{

    /// <summary>
    /// Tests of <see cref="RunSettings"/> (merging, XML serialization) and of the consistency of the settings'
    /// XSD and the documented default settings (Resources\AllTestSettings.taef.runsettings) with <see cref="SettingsWrapper"/>.
    /// </summary>
    [TestClass]
    public class RunSettingsTests
    {
        private static readonly string[] InternalSettings =
        {
            nameof(ITaefTestAdapterSettings.DebuggingNamedPipeId),
            nameof(ITaefTestAdapterSettings.SolutionDir),
            nameof(ITaefTestAdapterSettings.PlatformName),
            nameof(ITaefTestAdapterSettings.ConfigurationName)
        };

        private static IEnumerable<PropertyInfo> SettingsProperties => typeof(ITaefTestAdapterSettings)
            .GetProperties()
            .Where(p => p.Name != nameof(ITaefTestAdapterSettings.ProjectRegex));

        [TestMethod]
        [TestCategory(Unit)]
        public void GetUnsetValuesFrom_AllValuesUnset_AllValuesAreTaken()
        {
            RunSettings other = CreateRunSettingsWithAllValuesSet();
            var self = new RunSettings("myRegex");

            self.GetUnsetValuesFrom(other);

            foreach (PropertyInfo property in SettingsProperties)
            {
                property.GetValue(self).Should().Be(property.GetValue(other), property.Name);
            }
            self.ProjectRegex.Should().Be("myRegex");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetUnsetValuesFrom_AllValuesSet_NoValueIsOverridden()
        {
            RunSettings self = CreateRunSettingsWithAllValuesSet();
            RunSettings expected = CreateRunSettingsWithAllValuesSet();
            var other = new RunSettings
            {
                PrintTestOutput = false,
                TeExecutable = "other",
                RunIgnoredTests = false,
                TestTimeout = "1",
                IsolationLevel = TaefIsolationLevel.Default,
                RunInProcess = false,
                NrOfTestRepetitions = 1
            };

            self.GetUnsetValuesFrom(other);

            foreach (PropertyInfo property in SettingsProperties)
            {
                property.GetValue(self).Should().Be(property.GetValue(expected), property.Name);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Serialization_AllValuesSet_RoundTripAndXsdValidation()
        {
            var container = new Mock<ITaefTestAdapterSettingsContainer>();
            container.Setup(c => c.SolutionSettings).Returns(CreateRunSettingsWithAllValuesSet());
            container.Setup(c => c.ProjectSettings).Returns(new List<RunSettings> { new RunSettings(@".*\\My_taef\.dll$") { RunInProcess = true } });

            string xml = Serialize(new SettingsSerializationContainer(container.Object));

            ValidateAgainstXsd(xml);
            SettingsSerializationContainer deserialized = Deserialize(xml);
            foreach (PropertyInfo property in SettingsProperties)
            {
                property.GetValue(deserialized.SolutionSettings.Settings).Should().Be(property.GetValue(container.Object.SolutionSettings), property.Name);
            }
            deserialized.SettingsList.Should().ContainSingle();
            deserialized.SettingsList[0].ProjectRegex.Should().Be(@".*\\My_taef\.dll$");
            deserialized.SettingsList[0].RunInProcess.Should().BeTrue();
            deserialized.SettingsList[0].TeExecutable.Should().BeNull();

            XDocument document = XDocument.Parse(xml);
            document.Root.Should().NotBeNull();
            document.Root.Name.LocalName.Should().Be(TaefConstants.SettingsName);
            document.Descendants("TestTimeout").Single().Value.Should().Be("0:0:42");
            document.Descendants("IsolationLevel").Single().Value.Should().Be("Class");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Deserialization_NoSolutionSettings_SolutionSettingsAreEmpty()
        {
            // <SolutionSettings> and its <Settings> are optional: a settings file might only contain project settings
            string projectSettings = "<ProjectSettings><Settings ProjectRegex=\".*\"><RunInProcess>true</RunInProcess></Settings></ProjectSettings>";
            string[] xmls =
            {
                $"<{TaefConstants.SettingsName}>{projectSettings}</{TaefConstants.SettingsName}>",
                $"<{TaefConstants.SettingsName}><SolutionSettings />{projectSettings}</{TaefConstants.SettingsName}>",
            };
            foreach (string xml in xmls)
            {
                ValidateAgainstXsd(xml);
                SettingsSerializationContainer deserialized = Deserialize(xml);

                deserialized.SolutionSettings.Settings.Should().NotBeNull(xml);
                foreach (PropertyInfo property in SettingsProperties)
                {
                    property.GetValue(deserialized.SolutionSettings.Settings).Should().BeNull($"{property.Name} is not set in {xml}");
                }
                deserialized.SettingsList.Should().ContainSingle(xml).Which.RunInProcess.Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Serialization_UnsetValues_AreNotSerialized()
        {
            var container = new Mock<ITaefTestAdapterSettingsContainer>();
            container.Setup(c => c.SolutionSettings).Returns(new RunSettings { RunInProcess = true });
            container.Setup(c => c.ProjectSettings).Returns(new List<RunSettings>());

            string xml = Serialize(new SettingsSerializationContainer(container.Object));

            XDocument document = XDocument.Parse(xml);
            // ReSharper disable once PossibleNullReferenceException
            document.Descendants("Settings").Single().Elements().Select(e => e.Name.LocalName).Should().Equal("RunInProcess");
            ValidateAgainstXsd(xml);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Xsd_InvalidValues_AreRejected()
        {
            foreach (string invalidSettings in new[]
            {
                // (ranges of hours, minutes and seconds are checked by RunSettingsContainer, not by the XSD)
                "<TestTimeout>1:2:3:4</TestTimeout>",
                "<TestTimeout>0:0:1.12345678</TestTimeout>",
                "<TestTimeout>abc</TestTimeout>",
                "<IsolationLevel>Bogus</IsolationLevel>",
                "<NrOfTestRepetitions>0</NrOfTestRepetitions>",
                "<RunInProcess>maybe</RunInProcess>",
                // unknown elements
                "<UnknownSetting>true</UnknownSetting>"
            })
            {
                string xml = $"<{TaefConstants.SettingsName}><SolutionSettings><Settings>{invalidSettings}</Settings></SolutionSettings></{TaefConstants.SettingsName}>";
                Action validate = () => ValidateAgainstXsd(xml);
                validate.Should().Throw<XmlSchemaValidationException>(invalidSettings);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Xsd_ValidTaefValues_AreAccepted()
        {
            string xml = $"<{TaefConstants.SettingsName}><SolutionSettings><Settings>" +
                         "<TeExecutable>$(SolutionDir)\\TAEF</TeExecutable><TestTimeout>1.02:03:04.5</TestTimeout>" +
                         "<IsolationLevel>Module</IsolationLevel><RunIgnoredTests>true</RunIgnoredTests>" +
                         $"</Settings></SolutionSettings></{TaefConstants.SettingsName}>";

            ValidateAgainstXsd(xml);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllTestSettings_ContainsAllUserSettingsWithDefaultValues()
        {
            XElement settingsElement = XDocument.Load(TestResources.AllTestSettings).Descendants(TaefConstants.SettingsName).Single();
            ValidateAgainstXsd(settingsElement.ToString());

            RunSettings settings = Deserialize(settingsElement.ToString()).SolutionSettings.Settings;

            // all user settings are listed (except SkipOriginCheck, which is only mentioned in a comment: the VS extension ignores
            // it in settings files)
            foreach (PropertyInfo property in SettingsProperties.Where(p => !InternalSettings.Contains(p.Name) && p.Name != nameof(ITaefTestAdapterSettings.SkipOriginCheck)))
            {
                property.GetValue(settings).Should().NotBeNull($"{property.Name} should be contained in {TestResources.AllTestSettings}");
            }

            // with their default values, i.e. SettingsWrapper delivers the same values as for unset settings
            SettingsWrapper fromFile = CreateSettingsWrapper(settings);
            SettingsWrapper defaults = CreateSettingsWrapper(new RunSettings());
            foreach (PropertyInfo property in typeof(SettingsWrapper).GetProperties().Where(p => p.GetMethod != null && p.GetMethod.IsPublic))
            {
                object value = property.GetValue(fromFile);
                object defaultValue = property.GetValue(defaults);
                if (value is IEnumerable<RegexTraitPair> pairs)
                    pairs.Should().BeEmpty(property.Name);
                else
                    value.Should().Be(defaultValue, property.Name);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllTestSettings_DefaultValuesMatchConstantsOfSettingsWrapper()
        {
            XElement settingsElement = XDocument.Load(TestResources.AllTestSettings).Descendants("Settings").First();

            string Value(string name) => settingsElement.Element(name)?.Value;

            Value(nameof(RunSettings.WorkingDir)).Should().Be(SettingsWrapper.OptionWorkingDirDefaultValue);
            Value(nameof(RunSettings.TestDiscoveryTimeoutInSeconds)).Should().Be(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue.ToString());
            Value(nameof(RunSettings.NrOfTestRepetitions)).Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue.ToString());
            Value(nameof(RunSettings.IsolationLevel)).Should().Be(SettingsWrapper.OptionIsolationLevelDefaultValue.ToString());
            Value(nameof(RunSettings.TeExecutable)).Should().Be(SettingsWrapper.OptionTeExecutableDefaultValue);
            Value(nameof(RunSettings.TestTimeout)).Should().Be(SettingsWrapper.OptionTestTimeoutDefaultValue);
            Value(nameof(RunSettings.RunInProcess)).Should().Be("false");
            Value(nameof(RunSettings.RunIgnoredTests)).Should().Be("false");
            Value(nameof(RunSettings.DebuggerKind)).Should().Be(SettingsWrapper.OptionDebuggerKindDefaultValue.ToString());
            Value(nameof(RunSettings.MaxNrOfThreads)).Should().Be(SettingsWrapper.OptionMaxNrOfThreadsDefaultValue.ToString());
        }

        #region Helpers

        private static RunSettings CreateRunSettingsWithAllValuesSet()
        {
            return new RunSettings
            {
                PrintTestOutput = true,
                OutputMode = OutputMode.Verbose,
                TimestampMode = TimestampMode.PrintTimestamp,
                SeverityMode = SeverityMode.DoNotPrintSeverity,
                SummaryMode = SummaryMode.Error,
                PrefixOutputWithTaef = true,
                SkipOriginCheck = true,

                TestDiscoveryRegex = ".*Tests_taef\\.dll",
                TestDiscoveryTimeoutInSeconds = 42,
                ParseSymbolInformation = false,
                TraitsRegexesBefore = "A///B,C",
                TraitsRegexesAfter = "D///E,F",

                AdditionalPdbs = "$(TestDllDir)\\*.pdb",
                WorkingDir = "$(SolutionDir)",
                PathExtension = "C:\\bin",
                EnvironmentVariables = "A=B",
                AdditionalTestExecutionParam = "/p:\"A=B\"",
                BatchForTestSetup = "setup.bat",
                BatchForTestTeardown = "teardown.bat",
                KillProcessesOnCancel = true,
                DebuggerKind = DebuggerKind.ManagedAndNative,
                ParallelTestExecution = true,
                MaxNrOfThreads = 3,
                MissingTestsReportMode = MissingTestsReportMode.ReportAsFailed,

                TeExecutable = "C:\\TAEF\\TE.exe",
                RunIgnoredTests = true,
                BreakOnError = true,
                NrOfTestRepetitions = 3,
                TestTimeout = "0:0:42",
                IsolationLevel = TaefIsolationLevel.Class,
                RunInProcess = true,

                DebuggingNamedPipeId = "pipe",
                SolutionDir = "C:\\Solution",
                PlatformName = "x64",
                ConfigurationName = "Debug"
            };
        }

        private static SettingsWrapper CreateSettingsWrapper(RunSettings runSettings)
        {
            var container = new Mock<ITaefTestAdapterSettingsContainer>();
            container.Setup(c => c.SolutionSettings).Returns(runSettings);
            var logger = new Mock<ILogger>().Object;
            return new SettingsWrapper(container.Object)
            {
                RegexTraitParser = new RegexTraitParser(logger),
                EnvironmentVariablesParser = new EnvironmentVariablesParser(logger),
                HelperFilesCache = new HelperFilesCache(logger)
            };
        }

        private static string Serialize(SettingsSerializationContainer container)
        {
            var serializer = new XmlSerializer(typeof(SettingsSerializationContainer));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, container);
                return writer.ToString();
            }
        }

        private static SettingsSerializationContainer Deserialize(string xml)
        {
            var serializer = new XmlSerializer(typeof(SettingsSerializationContainer));
            using (var reader = new StringReader(xml))
            {
                return (SettingsSerializationContainer)serializer.Deserialize(reader);
            }
        }

        private static void ValidateAgainstXsd(string xml)
        {
            var schemaSet = new XmlSchemaSet();
            using (XmlReader schemaReader = XmlReader.Create(TestResources.SettingsXsd))
            {
                schemaSet.Add(null, schemaReader);
            }

            var settings = new XmlReaderSettings
            {
                Schemas = schemaSet,
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
            };
            settings.ValidationEventHandler += (sender, args) => throw args.Exception;

            // the serializer writes an XML declaration with encoding utf-16 into strings
            string content = xml.StartsWith("<?xml", StringComparison.Ordinal) ? xml.Substring(xml.IndexOf("?>", StringComparison.Ordinal) + 2) : xml;
            using (XmlReader reader = XmlReader.Create(new StringReader(content), settings))
            {
                while (reader.Read())
                {
                }
            }
        }

        #endregion

    }

}
