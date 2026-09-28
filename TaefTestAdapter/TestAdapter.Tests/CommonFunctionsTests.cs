// This file has been modified for TAEF support.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.Framework;
using TaefTestAdapter.TestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter
{
    [TestClass]
    public class CommonFunctionsTests
    {
        private readonly Mock<IRunSettings> _mockRunSettings = new Mock<IRunSettings>(MockBehavior.Strict);
        private readonly Mock<IMessageLogger> _mockMessageLogger = new Mock<IMessageLogger>();

        [TestCleanup]
        public void TearDown()
        {
            _mockRunSettings.Reset();
            _mockMessageLogger.Reset();
        }

        /// <summary>
        /// Runs <paramref name="action"/> with the environment variable <paramref name="variable"/> of the current process
        /// set to <paramref name="value"/> (null: unset), and restores its value afterwards.
        /// </summary>
        internal static void RunWithEnvVariable(string variable, string value, Action action)
        {
            string formerValue = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, value);
                action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, formerValue);
            }
        }

        #region CreateEnvironment

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_RunSettingsThrow_LoggerIsNotNull()
        {
            RunWithEnvVariable(CommonFunctions.TaefSettingsEnvVariable, null, () =>
            {
                CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                    out ILogger logger, out SettingsWrapper settings);

                logger.Should().NotBeNull();
                settings.Should().NotBeNull();
                _mockMessageLogger.Verify(l => l.SendMessage(
                    It.Is<TestMessageLevel>(level => level == TestMessageLevel.Error),
                    It.Is<string>(s => s.Contains("Visual Studio test framework failed to provide settings"))));
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_RunSettingsThrowButFallbackSettingsAreConfigured_NoErrorIsLogged()
        {
            RunWithEnvVariable(CommonFunctions.TaefSettingsEnvVariable, TestResources.SolutionTestSettings, () =>
            {
                CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                    out _, out SettingsWrapper _);

                _mockMessageLogger.Verify(l => l.SendMessage(
                    It.Is<TestMessageLevel>(level => level == TestMessageLevel.Informational),
                    It.Is<string>(s => s.Contains("Visual Studio test framework failed to provide settings"))));
                _mockMessageLogger.Verify(l => l.SendMessage(
                    It.Is<TestMessageLevel>(level => level == TestMessageLevel.Error),
                    It.IsAny<string>()), Times.Never);
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_SettingsEnvVarIsSet_SettingsAreReceivedFromEnvVar()
        {
            RunWithEnvVariable(
                CommonFunctions.TaefSettingsEnvVariable, TestResources.SolutionTestSettings,
                () =>
                {
                    CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                        out _, out SettingsWrapper settings);

                    settings.Should().NotBeNull();
                    settings.BatchForTestSetup.Should().Be("Solution");
                    settings.NrOfTestRepetitions.Should().Be(2);
                    settings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
                    _mockMessageLogger.Verify(l => l.SendMessage(
                        It.Is<TestMessageLevel>(ml => ml == TestMessageLevel.Informational),
                        It.Is<string>(s => s.Contains($"Using fallback settings from file '{TestResources.SolutionTestSettings}'"))));
                });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_SettingsEnvVarIsNotSet_InfoIsLogged()
        {
            RunWithEnvVariable(
                CommonFunctions.TaefSettingsEnvVariable, null,
                () =>
                {
                    CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                        out _, out SettingsWrapper settings);

                    settings.Should().NotBeNull();
                    _mockMessageLogger.Verify(l => l.SendMessage(
                        It.Is<TestMessageLevel>(ml => ml == TestMessageLevel.Informational),
                        It.Is<string>(s => s.Contains($"No settings file provided through env variable {CommonFunctions.TaefSettingsEnvVariable}"))));
                    _mockMessageLogger.Verify(l => l.SendMessage(
                        It.Is<TestMessageLevel>(ml => ml == TestMessageLevel.Warning),
                        It.Is<string>(s => s.Contains("Using default settings"))));
                });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_SettingsEnvVarFileDoesNotExist_WarningIsLogged()
        {
            RunWithEnvVariable(
                CommonFunctions.TaefSettingsEnvVariable, "foobar",
                () =>
                {
                    CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                        out _, out SettingsWrapper settings);

                    settings.Should().NotBeNull();
                    _mockMessageLogger.Verify(l => l.SendMessage(
                        It.Is<TestMessageLevel>(ml => ml == TestMessageLevel.Warning),
                        It.Is<string>(s => s.Contains("does not exist"))));
                });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_SettingsEnvVarFileWithoutSettingsNode_WarningIsLogged()
        {
            RunWithEnvVariable(
                CommonFunctions.TaefSettingsEnvVariable, TestResources.UserTestSettingsWithoutRunSettingsNode,
                () =>
                {
                    CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                        out _, out SettingsWrapper settings);

                    settings.Should().NotBeNull();
                    _mockMessageLogger.Verify(l => l.SendMessage(
                        It.Is<TestMessageLevel>(ml => ml == TestMessageLevel.Warning),
                        It.Is<string>(s => s.Contains("could not be loaded"))));
                });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_InvalidSettingsEnvVarFile_ErrorIsLogged()
        {
            RunWithEnvVariable(
                CommonFunctions.TaefSettingsEnvVariable, TestResources.XmlFileBroken,
                () =>
                {
                    CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                        out _, out SettingsWrapper settings);

                    settings.Should().NotBeNull();
                    _mockMessageLogger.Verify(l => l.SendMessage(
                        It.Is<TestMessageLevel>(ml => ml == TestMessageLevel.Error),
                        It.Is<string>(s => s.Contains("an exception occurred while trying to read file"))));
                });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_SettingsFromProvider_SolutionAndProjectSettingsAreMerged()
        {
            var solutionSettings = new RunSettings { AdditionalTestExecutionParam = "/p:\"Solution=1\"", RunInProcess = true, TestTimeout = "0:0:30" };
            var projectSettings = new RunSettings(@".*\\MyTests_taef\.dll$") { AdditionalTestExecutionParam = "/p:\"Project=1\"" };
            var container = new RunSettingsContainer(solutionSettings);
            container.ProjectSettings.Add(projectSettings);
            var mockProvider = new Mock<RunSettingsProvider>();
            mockProvider.Setup(p => p.SettingsContainer).Returns(container);
            _mockRunSettings.Setup(rs => rs.GetSettings(TaefConstants.SettingsName)).Returns(mockProvider.Object);

            CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object,
                out ILogger logger, out SettingsWrapper settings, @"C:\MySolution");

            settings.SolutionDir.Should().Be(@"C:\MySolution");
            settings.AdditionalTestExecutionParam.Should().Be("/p:\"Solution=1\"");
            settings.RunInProcess.Should().BeTrue();
            settings.TestTimeout.Should().Be("0:0:30");
            // project settings get the unset values from the solution settings
            projectSettings.RunInProcess.Should().BeTrue();

            settings.ExecuteWithSettingsForTestDll(@"C:\MySolution\bin\MyTests_taef.dll", logger, () =>
            {
                settings.AdditionalTestExecutionParam.Should().Be("/p:\"Project=1\"");
                settings.RunInProcess.Should().BeTrue();
            });
            settings.ExecuteWithSettingsForTestDll(@"C:\MySolution\bin\OtherTests_taef.dll", logger, () =>
                settings.AdditionalTestExecutionParam.Should().Be("/p:\"Solution=1\""));

            _mockMessageLogger.Verify(l => l.SendMessage(It.IsAny<TestMessageLevel>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_ProviderCouldNotLoadSettings_ErrorIsLogged()
        {
            var mockProvider = new Mock<RunSettingsProvider>();
            mockProvider.Setup(p => p.SettingsContainer).Returns(new RunSettingsContainer());
            mockProvider.Setup(p => p.LoadError).Returns("Invalid TestTimeout: 0:90");
            _mockRunSettings.Setup(rs => rs.GetSettings(TaefConstants.SettingsName)).Returns(mockProvider.Object);

            CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object, out _, out SettingsWrapper settings);

            settings.Should().NotBeNull();
            _mockMessageLogger.Verify(l => l.SendMessage(TestMessageLevel.Error,
                It.Is<string>(s => s.Contains(TaefConstants.SettingsName) && s.Contains("default settings") && s.EndsWith("Invalid TestTimeout: 0:90"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateEnvironment_PrefixOutputWithTaef_MessagesArePrefixed()
        {
            var container = new RunSettingsContainer(new RunSettings { PrefixOutputWithTaef = true, SeverityMode = SeverityMode.PrintSeverity, TimestampMode = TimestampMode.DoNotPrintTimestamp });
            var mockProvider = new Mock<RunSettingsProvider>();
            mockProvider.Setup(p => p.SettingsContainer).Returns(container);
            _mockRunSettings.Setup(rs => rs.GetSettings(TaefConstants.SettingsName)).Returns(mockProvider.Object);

            CommonFunctions.CreateEnvironment(_mockRunSettings.Object, _mockMessageLogger.Object, out ILogger logger, out _);
            logger.LogInfo("an info");
            logger.LogWarning("a warning");

            _mockMessageLogger.Verify(l => l.SendMessage(TestMessageLevel.Informational, "[TAEF] an info"), Times.Once);
            _mockMessageLogger.Verify(l => l.SendMessage(TestMessageLevel.Warning, "[TAEF Warning] a warning"), Times.Once);
        }

        #endregion

        #region ReportErrors

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportErrors_WarningsAndErrors_SummaryIsLoggedAsError()
        {
            var logger = new FakeLogger(() => OutputMode.Info, false);
            logger.LogWarning("warning 1");
            logger.LogError("error 1");

            CommonFunctions.ReportErrors(logger, "test execution", OutputMode.Info, SummaryMode.WarningOrError);

            logger.Errors.Should().HaveCount(2);
            string summary = logger.Errors.Last();
            summary.Should().Contain("The following warnings and errors occurred during test execution (enable debug mode for more information):");
            summary.Should().Contain("warning 1");
            summary.Should().Contain("error 1");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportErrors_OnlyWarnings_SummaryIsLoggedAsWarning()
        {
            var logger = new FakeLogger(() => OutputMode.Debug, false);
            logger.LogWarning("warning 1");

            CommonFunctions.ReportErrors(logger, "test discovery", OutputMode.Debug, SummaryMode.WarningOrError);

            logger.Errors.Should().BeEmpty();
            logger.Warnings.Should().HaveCount(2);
            logger.Warnings.Last().Should().Contain("The following warnings and errors occurred during test discovery:");
            logger.Warnings.Last().Should().NotContain("enable debug mode");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportErrors_OnlyWarningsAndSummaryModeError_NoSummary()
        {
            var logger = new FakeLogger(() => OutputMode.Info, false);
            logger.LogWarning("warning 1");

            CommonFunctions.ReportErrors(logger, "test discovery", OutputMode.Info, SummaryMode.Error);

            logger.All.Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportErrors_SummaryModeNever_NoSummary()
        {
            var logger = new FakeLogger(() => OutputMode.Info, false);
            logger.LogError("error 1");

            CommonFunctions.ReportErrors(logger, "test discovery", OutputMode.Info, SummaryMode.Never);

            logger.All.Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportErrors_NoWarningsAndErrors_NoSummary()
        {
            var logger = new FakeLogger(() => OutputMode.Info, false);
            logger.LogInfo("info");

            CommonFunctions.ReportErrors(logger, "test discovery", OutputMode.Info, SummaryMode.WarningOrError);

            logger.All.Should().ContainSingle();
        }

        #endregion

        [TestMethod]
        [TestCategory(Unit)]
        public void LogVisualStudioVersion_SupportedOrUnknownVersion_NoWarningIsLogged()
        {
            var logger = new FakeLogger(() => OutputMode.Debug, false);

            CommonFunctions.LogVisualStudioVersion(logger);

            if (VsVersionUtils.VsVersion != VsVersion.Unknown && !VsVersionUtils.VsVersion.IsSupported())
                Assert.Inconclusive($"Tests are run with an unsupported Visual Studio version: {VsVersionUtils.VersionSource}");

            logger.Warnings.Should().BeEmpty();
            logger.Infos.Should().ContainSingle();
            logger.Infos.Single().Should().Contain(VsVersionUtils.VsVersion == VsVersion.Unknown
                ? "Could not identify the Visual Studio version"
                : $"Visual Studio version: {VsVersionUtils.VsVersion}");
        }

    }
}
