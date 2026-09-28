// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.XPath;
using FluentAssertions;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using TaefTestAdapter.VsPackage.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

// ReSharper disable PossibleNullReferenceException

namespace TaefTestAdapter.VsPackage.Settings
{

    /// <summary>
    /// Tests of the merging of the adapter's settings in Visual Studio: settings of the user's .runsettings file win over
    /// the settings of the solution settings file (&lt;solution&gt;.taef.runsettings), which win over the VS options
    /// (the "global" settings); internal and VS-only settings are always taken from the VS options.
    /// </summary>
    [TestClass]
    public class RunSettingsServiceTests : TestsBase
    {
        private const string GlobalWorkingDir = "GlobalWorkingDir";
        private const string SolutionSolutionWorkingDir = "SolutionSolutionWorkingDir";
        private const string SolutionProject1WorkingDir = "SolutionProject1WorkingDir";
        private const string SolutionProject2WorkingDir = "SolutionProject2";
        private const string UserSolutionWorkingDir = "UserSolutionWorkingDir";
        private const string UserProject1WorkingDir = "UserProject1WorkingDir";
        private const string UserProject3WorkingDir = "UserProject3WorkingDir";

        /// <summary>ProjectRegex of the project settings of <see cref="TestResources.UserTestSettings"/>.</summary>
        private const string UserProjectRegex = @"LoadTests_taef\.dll|CrashingTests_taef\.dll";

        private readonly List<string> _messages = new List<string>();
        private Mock<ILogger> _mockVsLogger;
        private TemporaryDirectory _tempDir;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            _messages.Clear();
            _mockVsLogger = new Mock<ILogger>();
            _mockVsLogger
                .Setup(l => l.Log(It.IsAny<MessageLevel>(), It.IsAny<string>()))
                .Callback<MessageLevel, string>((level, message) => _messages.Add($"{level}: {message}"));

            _tempDir = new TemporaryDirectory();
        }

        [TestCleanup]
        public override void TearDown()
        {
            _tempDir?.Dispose();
            base.TearDown();
        }

        #region Service registration

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor__InstanceHasCorrectName()
        {
            new RunSettingsService(null).Name.Should().Be(TaefConstants.SettingsName);
            TaefConstants.SettingsName.Should().Be("TaefTestAdapterSettings");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunSettingsService_Attributes_ExportedAsRunSettingsServiceOfTaefSettings()
        {
            Type serviceType = typeof(RunSettingsService);

            serviceType.GetCustomAttributes<ExportAttribute>()
                .Should().Contain(e => e.ContractType == typeof(IRunSettingsService));
            serviceType.GetCustomAttribute<SettingsNameAttribute>()
                .SettingsName.Should().Be(TaefConstants.SettingsName);
            serviceType.GetConstructors().Single().GetCustomAttribute<ImportingConstructorAttribute>()
                .Should().NotBeNull();
        }

        #endregion

        #region Files of Tests.Common

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_UserSettingsWithoutRunSettingsNode_Warning()
        {
            RunSettingsService service = SetupRunSettingsService(TestResources.XmlFileBroken);

            var xml = new XmlDocument();
            xml.Load(TestResources.UserTestSettingsWithoutRunSettingsNode);

            service.AddRunSettings(xml, new Mock<IRunSettingsConfigurationInfo>().Object, _mockVsLogger.Object);

            _mockVsLogger.Verify(l => l.Log(It.Is<MessageLevel>(ml => ml == MessageLevel.Warning), It.Is<string>(s => s.Contains("does not contain a RunSettings node"))),
                Times.Exactly(1));
            _messages.Should().ContainSingle();
            xml.GetElementsByTagName(TaefConstants.SettingsName).Count.Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_BrokenSolutionSettings_ErrorAndUserAndGlobalSettingsAreMerged()
        {
            RunSettingsService service = SetupRunSettingsService(TestResources.XmlFileBroken);

            var xml = new XmlDocument();
            xml.Load(TestResources.UserTestSettings);

            service.AddRunSettings(xml, new Mock<IRunSettingsConfigurationInfo>().Object, _mockVsLogger.Object);

            // from global settings
            AssertContainsSetting(xml, "AdditionalTestExecutionParam", "Global");
            AssertContainsSetting(xml, "NrOfTestRepetitions", "1");
            // from user settings (win over global settings)
            AssertContainsSetting(xml, "RunIgnoredTests", "true");
            AssertContainsSetting(xml, "MaxNrOfThreads", "3");
            AssertContainsSetting(xml, "TestTimeout", "0:0:3");
            AssertContainsSetting(xml, "TraitsRegexesBefore", "User///A,B");
            // only from VS options
            AssertContainsSetting(xml, "SkipOriginCheck", "false");

            _mockVsLogger.Verify(l => l.Log(It.Is<MessageLevel>(ml => ml == MessageLevel.Error), It.Is<string>(s => s.Contains("could not be parsed") && s.Contains(TestResources.XmlFileBroken))),
                Times.Exactly(1));
            _messages.Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_UserAndSolutionSettings_UserSettingsBeatSolutionSettingsBeatGlobalSettings()
        {
            RunSettingsService service = SetupRunSettingsService(TestResources.SolutionTestSettings);

            var xml = new XmlDocument();
            xml.Load(TestResources.UserTestSettings);

            IXPathNavigable result = service.AddRunSettings(xml, new Mock<IRunSettingsConfigurationInfo>().Object, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            RunSettingsContainer container = LoadMergedSettings(result);

            RunSettings solutionSettings = container.SolutionSettings;
            solutionSettings.TraitsRegexesBefore.Should().Be("User///A,B"); // user and solution file
            solutionSettings.RunIgnoredTests.Should().BeTrue(); // user file
            solutionSettings.MaxNrOfThreads.Should().Be(3); // user file and global
            solutionSettings.TestTimeout.Should().Be("0:0:3"); // user file
            solutionSettings.NrOfTestRepetitions.Should().Be(2); // solution file and global
            solutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class); // solution file
            solutionSettings.BatchForTestSetup.Should().Be("Solution"); // solution file
            solutionSettings.AdditionalTestExecutionParam.Should().Be("Global"); // global
            solutionSettings.SkipOriginCheck.Should().BeFalse(); // user file (ignored) and global

            RunSettings projectSettings = container.ProjectSettings.Should().ContainSingle().Which;
            projectSettings.ProjectRegex.Should().Be(UserProjectRegex);
            projectSettings.MaxNrOfThreads.Should().Be(4); // project settings of user file
            projectSettings.TraitsRegexesBefore.Should().Be("User///A,B"); // from merged solution settings
            projectSettings.NrOfTestRepetitions.Should().Be(2);
            projectSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
            projectSettings.AdditionalTestExecutionParam.Should().Be("Global");
            projectSettings.SkipOriginCheck.Should().BeFalse();

            container.GetSettingsForTestDll(@"C:\foo\LoadTests_taef.dll").Should().BeSameAs(projectSettings);
            container.GetSettingsForTestDll(@"C:\foo\Tests_taef.dll").Should().BeNull();
        }

        #endregion

        #region TAEF settings

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_TaefSettings_MergedWithPrecedenceUserSolutionGlobal()
        {
            string solutionFile = _tempDir.CreateFile("MySolution.taef.runsettings", WrapIntoRunSettings(
                "<SolutionSettings><Settings>" +
                "<TestTimeout>2:00</TestTimeout><TeExecutable>$(SolutionDir)tools\\TAEF</TeExecutable><NrOfTestRepetitions>4</NrOfTestRepetitions>" +
                "</Settings></SolutionSettings>"));
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), solutionFile);

            XmlDocument userSettings = CreateRunSettingsDocument(
                "<SolutionSettings><Settings>" +
                "<RunIgnoredTests>true</RunIgnoredTests><TestTimeout>1:00</TestTimeout>" +
                "</Settings></SolutionSettings>" +
                "<ProjectSettings><Settings ProjectRegex=\".*Other_taef\\.dll\"><IsolationLevel>Module</IsolationLevel></Settings></ProjectSettings>",
                "<RunConfiguration><TargetPlatform>x64</TargetPlatform></RunConfiguration>");

            IXPathNavigable result = service.AddRunSettings(userSettings, null, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            XPathNavigator navigator = result.CreateNavigator();
            navigator.Select($"/RunSettings/{TaefConstants.SettingsName}").Count.Should().Be(1);
            navigator.SelectSingleNode("/RunSettings/RunConfiguration/TargetPlatform")?.Value.Should().Be("x64", "other settings must be kept");

            RunSettingsContainer container = LoadMergedSettings(result);
            RunSettings solutionSettings = container.SolutionSettings;
            solutionSettings.TestTimeout.Should().Be("1:00"); // user file wins
            solutionSettings.RunIgnoredTests.Should().BeTrue(); // user file
            solutionSettings.TeExecutable.Should().Be(@"$(SolutionDir)tools\TAEF"); // solution file beats VS options
            solutionSettings.NrOfTestRepetitions.Should().Be(4); // solution file beats VS options
            solutionSettings.RunInProcess.Should().BeTrue(); // VS options
            solutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class); // VS options
            solutionSettings.BreakOnError.Should().BeTrue(); // VS options

            RunSettings projectSettings = container.ProjectSettings.Should().ContainSingle().Which;
            projectSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Module); // own value
            projectSettings.TestTimeout.Should().Be("1:00"); // from user's solution settings
            projectSettings.TeExecutable.Should().Be(@"$(SolutionDir)tools\TAEF"); // from solution file
            projectSettings.RunInProcess.Should().BeTrue(); // from VS options
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_InternalAndVsOnlySettingsInSettingsFiles_AreTakenFromVsOptions()
        {
            const string internalSettings =
                "<SkipOriginCheck>true</SkipOriginCheck><DebuggingNamedPipeId>evil</DebuggingNamedPipeId>" +
                "<SolutionDir>C:\\Evil</SolutionDir><PlatformName>Evil</PlatformName><ConfigurationName>Evil</ConfigurationName>";
            string solutionFile = _tempDir.CreateFile("MySolution.taef.runsettings", WrapIntoRunSettings(
                $"<SolutionSettings><Settings>{internalSettings}</Settings></SolutionSettings>" +
                $"<ProjectSettings><Settings ProjectRegex=\"Solution\">{internalSettings}</Settings></ProjectSettings>"));
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), solutionFile);

            XmlDocument userSettings = CreateRunSettingsDocument(
                $"<SolutionSettings><Settings>{internalSettings}</Settings></SolutionSettings>" +
                $"<ProjectSettings><Settings ProjectRegex=\"User\">{internalSettings}</Settings></ProjectSettings>");

            IXPathNavigable result = service.AddRunSettings(userSettings, null, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            RunSettingsContainer container = LoadMergedSettings(result);
            container.ProjectSettings.Should().HaveCount(2);
            foreach (RunSettings settings in new[] { container.SolutionSettings }.Concat(container.ProjectSettings))
            {
                settings.SkipOriginCheck.Should().BeFalse();
                settings.DebuggingNamedPipeId.Should().Be("pipe-from-vs");
                settings.SolutionDir.Should().Be(@"C:\Solution");
                settings.PlatformName.Should().Be("x64");
                settings.ConfigurationName.Should().Be("Debug");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_InvalidUserSettings_ErrorAndReplacedByMergedSettings()
        {
            string solutionFile = _tempDir.CreateFile("MySolution.taef.runsettings", WrapIntoRunSettings(
                "<SolutionSettings><Settings><NrOfTestRepetitions>4</NrOfTestRepetitions></Settings></SolutionSettings>"));
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), solutionFile);

            // 60 minutes is not a valid TAEF timeout (minutes are 0..59)
            XmlDocument userSettings = CreateRunSettingsDocument(
                "<SolutionSettings><Settings><TestTimeout>0:60</TestTimeout></Settings></SolutionSettings>");

            IXPathNavigable result = service.AddRunSettings(userSettings, null, _mockVsLogger.Object);

            _messages.Should().ContainSingle().Which.Should().StartWith("Error: Invalid run settings")
                .And.Contain($"{TaefConstants.SettingsName} is ignored").And.Contain("TestTimeout").And.Contain("0:60");
            XPathNavigator navigator = result.CreateNavigator();
            navigator.Select($"//{TaefConstants.SettingsName}").Count.Should().Be(1);

            RunSettingsContainer container = LoadMergedSettings(result);
            container.SolutionSettings.TestTimeout.Should().Be("0:05"); // from VS options
            container.SolutionSettings.NrOfTestRepetitions.Should().Be(4); // from solution file
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_UserSettingsWithUnknownSetting_Error()
        {
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), null);

            XmlDocument userSettings = CreateRunSettingsDocument(
                "<SolutionSettings><Settings><UnknownSetting>true</UnknownSetting></Settings></SolutionSettings>");

            IXPathNavigable result = service.AddRunSettings(userSettings, null, _mockVsLogger.Object);

            // the message of the schema violation names the invalid element
            _messages.Should().ContainSingle().Which.Should().StartWith("Error: Invalid run settings")
                .And.Contain("UnknownSetting");
            result.CreateNavigator().Select($"//{TaefConstants.SettingsName}").Count.Should().Be(1);
            LoadMergedSettings(result).SolutionSettings.RunInProcess.Should().BeTrue("the VS options must be used");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_InvalidSolutionSettings_ErrorAndUserAndGlobalSettingsAreMerged()
        {
            string solutionFile = _tempDir.CreateFile("MySolution.taef.runsettings", WrapIntoRunSettings(
                "<SolutionSettings><Settings><IsolationLevel>Bogus</IsolationLevel></Settings></SolutionSettings>"));
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), solutionFile);

            XmlDocument userSettings = CreateRunSettingsDocument(
                "<SolutionSettings><Settings><TestTimeout>1:00</TestTimeout></Settings></SolutionSettings>");

            IXPathNavigable result = service.AddRunSettings(userSettings, null, _mockVsLogger.Object);

            _messages.Should().ContainSingle()
                .Which.Should().StartWith("Error: Solution test settings file could not be parsed").And.Contain(solutionFile)
                .And.Contain("Bogus");
            RunSettingsContainer container = LoadMergedSettings(result);
            container.SolutionSettings.TestTimeout.Should().Be("1:00");
            container.SolutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_SettingsFilesWithProjectSettingsOnly_ProjectSettingsAreMerged()
        {
            // <SolutionSettings> is optional
            string solutionFile = _tempDir.CreateFile("MySolution.taef.runsettings", WrapIntoRunSettings(
                "<ProjectSettings><Settings ProjectRegex=\"Solution_taef\\.dll\"><TestTimeout>0:02</TestTimeout></Settings></ProjectSettings>"));
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), solutionFile);
            XmlDocument userSettings = CreateRunSettingsDocument(
                "<ProjectSettings><Settings ProjectRegex=\"User_taef\\.dll\"><TestTimeout>0:03</TestTimeout></Settings></ProjectSettings>");

            IXPathNavigable result = service.AddRunSettings(userSettings, null, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            RunSettingsContainer container = LoadMergedSettings(result);
            container.SolutionSettings.TestTimeout.Should().Be("0:05"); // from VS options
            container.ProjectSettings.Should().HaveCount(2);
            container.GetSettingsForTestDll(@"C:\User_taef.dll").TestTimeout.Should().Be("0:03");
            container.GetSettingsForTestDll(@"C:\Solution_taef.dll").TestTimeout.Should().Be("0:02");
            container.GetSettingsForTestDll(@"C:\Solution_taef.dll").RunInProcess.Should().BeTrue(); // from VS options
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_SolutionSettingsWithInvalidValue_ErrorNamesSettingAndValue()
        {
            string solutionFile = _tempDir.CreateFile("MySolution.taef.runsettings", WrapIntoRunSettings(
                "<SolutionSettings><Settings><TestTimeout>0:00:90</TestTimeout></Settings></SolutionSettings>"));
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), solutionFile);

            IXPathNavigable result = service.AddRunSettings(CreateRunSettingsDocument(""), null, _mockVsLogger.Object);

            _messages.Should().ContainSingle()
                .Which.Should().StartWith("Error: Solution test settings file could not be parsed").And.Contain(solutionFile)
                .And.Contain("Invalid TestTimeout: 0:00:90");
            LoadMergedSettings(result).SolutionSettings.TestTimeout.Should().Be("0:05"); // from VS options
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_InvalidSolutionSettingsWithoutInnerException_ErrorWithoutStackTrace()
        {
            // e.g. settings the schema does not catch (but the adapter does) might be reported without inner exception
            var exception = new InvalidRunSettingsException($"Invalid {TaefConstants.SettingsName}: project settings need a ProjectRegex");
            var service = new RunSettingsServiceWithFailingSolutionSettingsFile(CreateGlobalRunSettings(), exception);

            IXPathNavigable result = service.AddRunSettings(CreateRunSettingsDocument(""), null, _mockVsLogger.Object);

            _messages.Should().ContainSingle()
                .Which.Should().StartWith("Error: Solution test settings file could not be parsed")
                .And.EndWith($"Error message: {exception.Message}");
            LoadMergedSettings(result).SolutionSettings.TestTimeout.Should().Be("0:05"); // from VS options
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_SolutionSettingsFileWithoutRunSettingsNode_Warning()
        {
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), TestResources.UserTestSettingsWithoutRunSettingsNode);

            IXPathNavigable result = service.AddRunSettings(CreateRunSettingsDocument(""), null, _mockVsLogger.Object);

            _messages.Should().ContainSingle()
                .Which.Should().StartWith("Warning: Solution test settings file found").And.Contain("does not contain RunSettings node");
            LoadMergedSettings(result).SolutionSettings.TestTimeout.Should().Be("0:05");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_NoSolutionOpen_VsOptionsAreUsed()
        {
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), null);

            var document = new XmlDocument();
            document.LoadXml("<RunSettings><RunConfiguration /></RunSettings>");
            IXPathNavigable result = service.AddRunSettings(document, null, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            XPathNavigator navigator = result.CreateNavigator();
            navigator.Select("/RunSettings/RunConfiguration").Count.Should().Be(1);
            navigator.Select($"/RunSettings/{TaefConstants.SettingsName}").Count.Should().Be(1);

            RunSettingsContainer container = LoadMergedSettings(result);
            container.ProjectSettings.Should().BeEmpty();
            RunSettings settings = container.SolutionSettings;
            settings.TeExecutable.Should().Be(@"C:\Global\TE.exe");
            settings.RunInProcess.Should().BeTrue();
            settings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
            settings.TestTimeout.Should().Be("0:05");
            settings.NrOfTestRepetitions.Should().Be(3);
            settings.RunIgnoredTests.Should().BeFalse();
            settings.BreakOnError.Should().BeTrue();
            settings.DebuggingNamedPipeId.Should().Be("pipe-from-vs");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_SolutionSettingsFileDoesNotExist_NoMessages()
        {
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), _tempDir.GetPath("NotExisting.taef.runsettings"));

            IXPathNavigable result = service.AddRunSettings(CreateRunSettingsDocument(""), null, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            LoadMergedSettings(result).SolutionSettings.TeExecutable.Should().Be(@"C:\Global\TE.exe");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_AllTestSettingsFile_AllSettingsAreKept()
        {
            // the documented settings file (all settings with their default values) is a valid user settings file
            var service = new RunSettingsServiceUnderTest(CreateGlobalRunSettings(), null);

            var document = new XmlDocument();
            document.Load(TestResources.AllTestSettings);
            IXPathNavigable result = service.AddRunSettings(document, null, _mockVsLogger.Object);

            _messages.Should().BeEmpty();
            RunSettings settings = LoadMergedSettings(result).SolutionSettings;
            settings.TestTimeout.Should().Be(SettingsWrapper.OptionTestTimeoutDefaultValue);
            settings.NrOfTestRepetitions.Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue);
            settings.RunInProcess.Should().Be(SettingsWrapper.OptionRunInProcessDefaultValue);
            settings.IsolationLevel.Should().Be(SettingsWrapper.OptionIsolationLevelDefaultValue);
            settings.RunIgnoredTests.Should().Be(SettingsWrapper.OptionRunIgnoredTestsDefaultValue);
            settings.BreakOnError.Should().Be(SettingsWrapper.OptionBreakOnErrorDefaultValue);
            settings.DebuggingNamedPipeId.Should().Be("pipe-from-vs");
        }

        #endregion

        #region Working dir matrix (solution/project settings of user file and solution file)

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_CompleteSettings_BasicChecks()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, SolutionProject1WorkingDir, SolutionProject2WorkingDir,
                UserSolutionWorkingDir, UserProject1WorkingDir, UserProject3WorkingDir);

            resultingContainer.Should().NotBeNull();
            resultingContainer.SolutionSettings.Should().NotBeNull();
            resultingContainer.ProjectSettings.Should().HaveCount(3);

            resultingContainer.GetSettingsForTestDll("project1").Should().NotBeNull();
            resultingContainer.GetSettingsForTestDll("project2").Should().NotBeNull();
            resultingContainer.GetSettingsForTestDll("project3").Should().NotBeNull();
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_CompleteSettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, SolutionProject1WorkingDir, SolutionProject2WorkingDir,
                UserSolutionWorkingDir, UserProject1WorkingDir, UserProject3WorkingDir);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(SolutionProject2WorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserProject3WorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_NoSettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, null, null,
                null, null, null);

            resultingContainer.GetSettingsForTestDll("project1").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("project2").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("project3").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_EmptySettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, "", "",
                null, "", "");

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(GlobalWorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(GlobalWorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(GlobalWorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_CompleteSolutionSettingsNoProjectSettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, SolutionProject1WorkingDir, SolutionProject2WorkingDir,
                null, null, null);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(SolutionProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(SolutionProject2WorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_NoSolutionSettingsCompleteProjectSettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, null, null,
                UserSolutionWorkingDir, UserProject1WorkingDir, UserProject3WorkingDir);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserProject3WorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_CompleteSolutionSettingsEmptyProjectSettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, SolutionProject1WorkingDir, SolutionProject2WorkingDir,
                null, "", "");

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(SolutionProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(SolutionProject2WorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(SolutionSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_EmptySolutionSettingsCompleteProjectSettings_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, "", "",
                UserSolutionWorkingDir, UserProject1WorkingDir, UserProject3WorkingDir);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserProject3WorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings1_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, null, "",
                UserSolutionWorkingDir, "", null);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings2_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, SolutionProject1WorkingDir, null,
                null, "", UserProject3WorkingDir);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(SolutionProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserProject3WorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings3_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, "", SolutionProject2WorkingDir,
                null, UserProject1WorkingDir, null);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(SolutionProject2WorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings4_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, SolutionProject1WorkingDir, "",
                UserSolutionWorkingDir, null, "");

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(SolutionProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings5_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                SolutionSolutionWorkingDir, "", SolutionProject2WorkingDir,
                UserSolutionWorkingDir, null, UserProject3WorkingDir);

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(SolutionProject2WorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserProject3WorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings6_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, "", null,
                null, UserProject1WorkingDir, "");

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").Should().BeNull();
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(GlobalWorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AddRunSettings_MixedSettings7_()
        {
            var resultingContainer = SetupFinalRunSettingsContainer(
                null, null, SolutionProject2WorkingDir,
                UserSolutionWorkingDir, UserProject1WorkingDir, "");

            resultingContainer.GetSettingsForTestDll("project1").WorkingDir.Should().Be(UserProject1WorkingDir);
            resultingContainer.GetSettingsForTestDll("project2").WorkingDir.Should().Be(SolutionProject2WorkingDir);
            resultingContainer.GetSettingsForTestDll("project3").WorkingDir.Should().Be(UserSolutionWorkingDir);
            resultingContainer.GetSettingsForTestDll("not_matched").Should().BeNull();

            CheckSkipOriginCheck(resultingContainer);
        }

        #endregion

        #region Helpers

        private class GlobalRunSettings : IGlobalRunSettings
        {
            public RunSettings RunSettings { get; set; }
        }

        private class RunSettingsServiceWithFailingSolutionSettingsFile : RunSettingsService
        {
            private readonly Exception _exception;

            internal RunSettingsServiceWithFailingSolutionSettingsFile(IGlobalRunSettings globalRunSettings, Exception exception)
                : base(globalRunSettings)
            {
                _exception = exception;
            }

            protected override string GetSolutionSettingsXmlFile() => throw _exception;
        }

        /// <returns>VS options as the VS package would provide them (internal settings included).</returns>
        private static IGlobalRunSettings CreateGlobalRunSettings()
        {
            return new GlobalRunSettings
            {
                RunSettings = new RunSettings
                {
                    TeExecutable = @"C:\Global\TE.exe",
                    RunInProcess = true,
                    IsolationLevel = TaefIsolationLevel.Class,
                    TestTimeout = "0:05",
                    NrOfTestRepetitions = 3,
                    RunIgnoredTests = false,
                    BreakOnError = true,
                    SkipOriginCheck = false,
                    DebuggingNamedPipeId = "pipe-from-vs",
                    SolutionDir = @"C:\Solution",
                    PlatformName = "x64",
                    ConfigurationName = "Debug",
                }
            };
        }

        private RunSettingsService SetupRunSettingsService(string solutionRunSettingsFile)
        {
            var globalRunSettings = new RunSettings
            {
                AdditionalTestExecutionParam = "Global",
                NrOfTestRepetitions = 1,
                MaxNrOfThreads = 1,
                TraitsRegexesBefore = "Global",
                SkipOriginCheck = false
            };

            var mockGlobalRunSettings = new Mock<IGlobalRunSettings>();
            mockGlobalRunSettings.Setup(grs => grs.RunSettings).Returns(globalRunSettings);

            return new RunSettingsServiceUnderTest(mockGlobalRunSettings.Object, solutionRunSettingsFile);
        }

        private static string WrapIntoRunSettings(string adapterSettingsContent, string otherSettings = "")
        {
            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?><RunSettings>{otherSettings}" +
                   $"<{TaefConstants.SettingsName}>{adapterSettingsContent}</{TaefConstants.SettingsName}></RunSettings>";
        }

        private static XmlDocument CreateRunSettingsDocument(string adapterSettingsContent, string otherSettings = "")
        {
            var document = new XmlDocument();
            document.LoadXml(WrapIntoRunSettings(adapterSettingsContent, otherSettings));
            return document;
        }

        private static RunSettingsContainer LoadMergedSettings(IXPathNavigable mergedRunSettings)
        {
            XPathNavigator navigator = mergedRunSettings.CreateNavigator();
            navigator.MoveToRoot();
            navigator.MoveToChild(Constants.RunSettingsName, "").Should().BeTrue();
            navigator.MoveToChild(TaefConstants.SettingsName, "").Should().BeTrue();
            return RunSettingsContainer.LoadFromXml(navigator);
        }

        private void AssertContainsSetting(XmlDocument xml, string nodeName, string value)
        {
            XmlNode solutionSettingsNode = xml.GetElementsByTagName("SolutionSettings").Item(0);
            XmlNodeList list = solutionSettingsNode.SelectNodes($"Settings/{nodeName}");

#pragma warning disable CollectionShouldContainSingle // Simplify Assertion
            list.Should().HaveCount(1, $"node {nodeName} should exist only once. XML Document:{Environment.NewLine}{ToFormattedString(xml, 4)}");
#pragma warning restore CollectionShouldContainSingle // Simplify Assertion

            XmlNode node = list.Item(0);
            node.Should().NotBeNull();
            // ReSharper disable once PossibleNullReferenceException
            node.InnerText.Should().BeEquivalentTo(value);
        }

        private static string ToFormattedString(XmlDocument xml, int indentation)
        {
            StringWriter sw;
            using (var xw = new XmlTextWriter(sw = new StringWriter()))
            {
                xw.Formatting = Formatting.Indented;
                xw.Indentation = indentation;
                xml.WriteContentTo(xw);
                return sw.ToString();
            }
        }

        private RunSettingsContainer SetupFinalRunSettingsContainer(
            string solutionSolutionWorkingDir, string solutionProject1WorkingDir, string solutionProject2WorkingDir,
            string userSolutionWorkingDir, string userProject1WorkingDir, string userProject3WorkingDir)
        {
            var globalSettings = new RunSettings { ProjectRegex = null, WorkingDir = GlobalWorkingDir, SkipOriginCheck = false};
            var mockGlobalRunSettings = new Mock<IGlobalRunSettings>();
            mockGlobalRunSettings.Setup(grs => grs.RunSettings).Returns(globalSettings);

            var solutionSettingsContainer = SetupSettingsContainer(solutionSolutionWorkingDir, solutionProject1WorkingDir, solutionProject2WorkingDir, null);
            var solutionSettingsNavigator = EmbedSettingsIntoRunSettings(solutionSettingsContainer);
            var solutionSettingsFile = SerializeSolutionSettings(solutionSettingsNavigator);

            var userSettingsContainer = SetupSettingsContainer(userSolutionWorkingDir, userProject1WorkingDir, null, userProject3WorkingDir);
            var userSettingsNavigator = EmbedSettingsIntoRunSettings(userSettingsContainer);

            var serviceUnderTest = new RunSettingsServiceUnderTest(mockGlobalRunSettings.Object, solutionSettingsFile);
            IXPathNavigable navigable = serviceUnderTest.AddRunSettings(userSettingsNavigator,
                new Mock<IRunSettingsConfigurationInfo>().Object, _mockVsLogger.Object);

            _messages.Should().BeEmpty();

            var navigator = navigable.CreateNavigator();
            navigator.MoveToChild(Constants.RunSettingsName, "");
            navigator.MoveToChild(TaefConstants.SettingsName, "");

            return RunSettingsContainer.LoadFromXml(navigator);
        }

        private RunSettingsContainer SetupSettingsContainer(string solutionWorkingDir,
            string project1WorkingDir, string project2WorkingDir, string project3WorkingDir)
        {
            var settingsContainer = new RunSettingsContainer(new RunSettings
            {
                ProjectRegex = null,
                WorkingDir = solutionWorkingDir,
                SkipOriginCheck = true
            });

            AddProjectSettings(settingsContainer, "project1", project1WorkingDir);
            AddProjectSettings(settingsContainer, "project2", project2WorkingDir);
            AddProjectSettings(settingsContainer, "project3", project3WorkingDir);

            return settingsContainer;
        }

        private static void AddProjectSettings(RunSettingsContainer settingsContainer, string project, string workingDir)
        {
            if (workingDir == null)
                return;

            settingsContainer.ProjectSettings.Add(new RunSettings
            {
                ProjectRegex = project,
                WorkingDir = workingDir == "" ? null : workingDir,
                SkipOriginCheck = true
            });
        }

        private static XPathNavigator EmbedSettingsIntoRunSettings(RunSettingsContainer settingsContainer)
        {
            var settingsDocument = new XmlDocument();
            XmlDeclaration xmlDeclaration = settingsDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = settingsDocument.DocumentElement;
            settingsDocument.InsertBefore(xmlDeclaration, root);

            XmlElement runSettingsNode = settingsDocument.CreateElement("", Constants.RunSettingsName, "");
            settingsDocument.AppendChild(runSettingsNode);

            var settingsNavigator = settingsDocument.CreateNavigator();
            settingsNavigator.MoveToChild(Constants.RunSettingsName, "");
            settingsNavigator.AppendChild(settingsContainer.ToXml().CreateNavigator());
            settingsNavigator.MoveToRoot();

            return settingsNavigator;
        }

        private string SerializeSolutionSettings(XPathNavigator settingsNavigator)
        {
            string settingsFile = _tempDir.GetPath($"Solution{Guid.NewGuid():N}.taef.runsettings");

            var document = new XmlDocument();
            document.LoadXml(settingsNavigator.OuterXml);
            document.Save(settingsFile);

            return settingsFile;
        }

        private void CheckSkipOriginCheck(RunSettingsContainer runSettingsContainer)
        {
            runSettingsContainer.SolutionSettings.SkipOriginCheck.Should().BeFalse();
            foreach (RunSettings projectSettings in runSettingsContainer.ProjectSettings)
            {
                projectSettings.SkipOriginCheck.Should().BeFalse();
            }
#pragma warning disable NullConditionalAssertion // Code Smell
            runSettingsContainer.GetSettingsForTestDll("project1")?.SkipOriginCheck.Should().BeFalse();
            runSettingsContainer.GetSettingsForTestDll("project2")?.SkipOriginCheck.Should().BeFalse();
            runSettingsContainer.GetSettingsForTestDll("project3")?.SkipOriginCheck.Should().BeFalse();
            runSettingsContainer.GetSettingsForTestDll("not_matched")?.SkipOriginCheck.Should().BeFalse();
#pragma warning restore NullConditionalAssertion // Code Smell
        }

        #endregion

    }

}
