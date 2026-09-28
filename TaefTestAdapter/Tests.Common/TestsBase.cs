// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using TaefTestAdapter.Common;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace TaefTestAdapter.Tests.Common
{
    /// <summary>
    /// Base class of most test classes: provides a mocked logger, mocked settings (<see cref="MockOptions"/>, all options
    /// return their default values, see <see cref="SetupOptions"/>), a mocked framework reporter and a
    /// <see cref="TestDataCreator"/>. A new instance is created for each test method (MSTest), and all mocks are set up
    /// again before each test.
    /// </summary>
    public abstract class TestsBase
    {
        private class DummyProcessFactory : ProcessExecutorFactory, IDebuggedProcessExecutorFactory
        {
            public IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, ILogger logger)
            {
                throw new NotImplementedException();
            }

            public IDebuggedProcessExecutor CreateNativeDebuggingExecutor(DebuggerEngine engine, bool printTestOutput, ILogger logger)
            {
                throw new NotImplementedException();
            }
        }

        protected readonly Mock<ILogger> MockLogger;
        protected readonly Mock<SettingsWrapper> MockOptions;
        protected readonly Mock<ITestFrameworkReporter> MockFrameworkReporter;

        protected readonly TestDataCreator TestDataCreator;

        protected readonly TestEnvironment TestEnvironment;

        /// <summary>A factory creating <see cref="DotNetProcessExecutor"/>s; the debugging executors are not implemented.</summary>
        protected IDebuggedProcessExecutorFactory ProcessExecutorFactory => new DummyProcessFactory();

        protected TestsBase()
        {
            MockLogger = new Mock<ILogger>();
            MockLogger.Setup(l => l.GetMessages(It.IsAny<Severity[]>())).Returns(new List<string>());

            var mockSettingsContainer = new Mock<ITaefTestAdapterSettingsContainer>();
            var mockRunSettings = new Mock<RunSettings>();
            mockSettingsContainer.Setup(c => c.SolutionSettings).Returns(mockRunSettings.Object);
            MockOptions = new Mock<SettingsWrapper>(mockSettingsContainer.Object, Path.GetFullPath(TestResources.SampleTestsSolutionDir));

            MockFrameworkReporter = new Mock<ITestFrameworkReporter>();

            TestEnvironment = new TestEnvironment(MockOptions.Object, MockLogger.Object);
            TestDataCreator = new TestDataCreator(TestEnvironment);
        }


        [TestInitialize]
        public virtual void SetUp()
        {
            SetupOptions(MockOptions, MockLogger.Object);
        }

        /// <summary>
        /// Sets up all settings of <paramref name="mockOptions"/> to return what <see cref="SettingsWrapper"/> returns for
        /// the default values of the options (<c>SettingsWrapper.Option*DefaultValue</c>), with these exceptions:
        /// <see cref="SettingsWrapper.TimestampMode"/> is <see cref="TimestampMode.DoNotPrintTimestamp"/>,
        /// <see cref="SettingsWrapper.SeverityMode"/> is <see cref="SeverityMode.PrintSeverity"/>,
        /// <see cref="SettingsWrapper.DebuggerKind"/> is <see cref="DebuggerKind.Native"/>,
        /// <see cref="SettingsWrapper.DebuggingNamedPipeId"/> is a new GUID and <see cref="SettingsWrapper.SolutionDir"/> is
        /// the sample tests' solution dir (the value passed to the mock's constructor). Tests can override single settings
        /// with <c>MockOptions.Setup(o => o.Xyz).Returns(...)</c> after <see cref="SetUp"/> has run.
        /// </summary>
        public static void SetupOptions(Mock<SettingsWrapper> mockOptions, ILogger logger)
        {
            mockOptions.Object.HelperFilesCache = new HelperFilesCache(logger);
            mockOptions.Object.EnvironmentVariablesParser = new EnvironmentVariablesParser(logger);
            mockOptions.Object.RegexTraitParser = new RegexTraitParser(logger);

            mockOptions.Setup(o => o.CheckCorrectUsage(It.IsAny<string>())).Callback(() => { });
            mockOptions.Setup(o => o.Clone()).Returns(mockOptions.Object);

            // General
            mockOptions.Setup(o => o.PrintTestOutput).Returns(SettingsWrapper.OptionPrintTestOutputDefaultValue);
            mockOptions.Setup(o => o.OutputMode).Returns(SettingsWrapper.OptionOutputModeDefaultValue);
            mockOptions.Setup(o => o.TimestampMode).Returns(TimestampMode.DoNotPrintTimestamp);
            mockOptions.Setup(o => o.SeverityMode).Returns(SeverityMode.PrintSeverity);
            mockOptions.Setup(o => o.SummaryMode).Returns(SettingsWrapper.OptionSummaryModeDefaultValue);
            mockOptions.Setup(o => o.PrefixOutputWithTaef).Returns(SettingsWrapper.OptionPrefixOutputWithTaefDefaultValue);
            mockOptions.Setup(o => o.SkipOriginCheck).Returns(SettingsWrapper.OptionSkipOriginCheckDefaultValue);

            // TAEF
            mockOptions.Setup(o => o.TeExecutable).Returns(SettingsWrapper.OptionTeExecutableDefaultValue);
            mockOptions.Setup(o => o.RunIgnoredTests).Returns(SettingsWrapper.OptionRunIgnoredTestsDefaultValue);
            mockOptions.Setup(o => o.BreakOnError).Returns(SettingsWrapper.OptionBreakOnErrorDefaultValue);
            mockOptions.Setup(o => o.NrOfTestRepetitions).Returns(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue);
            mockOptions.Setup(o => o.TestTimeout).Returns(SettingsWrapper.OptionTestTimeoutDefaultValue);
            mockOptions.Setup(o => o.IsolationLevel).Returns(SettingsWrapper.OptionIsolationLevelDefaultValue);
            mockOptions.Setup(o => o.RunInProcess).Returns(SettingsWrapper.OptionRunInProcessDefaultValue);

            // Test execution
            mockOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.Native);
            mockOptions.Setup(o => o.AdditionalPdbs).Returns(SettingsWrapper.OptionAdditionalPdbsDefaultValue);
            mockOptions.Setup(o => o.WorkingDir).Returns(SettingsWrapper.OptionWorkingDirDefaultValue);
            mockOptions.Setup(o => o.PathExtension).Returns(SettingsWrapper.OptionPathExtensionDefaultValue);
            mockOptions.Setup(o => o.EnvironmentVariables).Returns(SettingsWrapper.OptionEnvironmentVariablesDefaultValue);
            mockOptions.Setup(o => o.AdditionalTestExecutionParam)
                .Returns(SettingsWrapper.OptionAdditionalTestExecutionParamDefaultValue);
            mockOptions.Setup(o => o.BatchForTestSetup).Returns(SettingsWrapper.OptionBatchForTestSetupDefaultValue);
            mockOptions.Setup(o => o.BatchForTestTeardown).Returns(SettingsWrapper.OptionBatchForTestTeardownDefaultValue);
            mockOptions.Setup(o => o.KillProcessesOnCancel).Returns(SettingsWrapper.OptionKillProcessesOnCancelDefaultValue);
            mockOptions.Setup(o => o.ParallelTestExecution)
                .Returns(SettingsWrapper.OptionParallelTestExecutionDefaultValue);
            // SettingsWrapper maps the default value 0 to the number of processors
            mockOptions.Setup(o => o.MaxNrOfThreads).Returns(Environment.ProcessorCount);
            mockOptions.Setup(o => o.MissingTestsReportMode)
                .Returns(SettingsWrapper.OptionMissingTestsReportModeDefaultValue);

            // Test discovery
            mockOptions.Setup(o => o.TestDiscoveryRegex).Returns(SettingsWrapper.OptionTestDiscoveryRegexDefaultValue);
            mockOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds)
                .Returns(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue);
            mockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new List<RegexTraitPair>());
            mockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new List<RegexTraitPair>());
            mockOptions.Setup(o => o.ParseSymbolInformation).Returns(SettingsWrapper.OptionParseSymbolInformationDefaultValue);

            // internal
            mockOptions.Setup(o => o.DebuggingNamedPipeId).Returns(Guid.NewGuid().ToString());
            mockOptions.Setup(o => o.SolutionDir).CallBase();
        }

        /// <summary>
        /// Sets up <see cref="MockOptions"/> like SampleTests.taef.runsettings does for Tests_taef.dll (runtime parameter
        /// <c>/p:"TestDirectory=$(TestDir)"</c>, working directory <c>$(SolutionDir)</c> (= SampleTests folder), environment variable
        /// <c>MYENVVAR=MyValue</c>), so that TaefSamples::RuntimeParameterTests::TestDirectoryIsSet, TaefSamples::WorkingDir::IsSolutionDirectory and
        /// TaefSamples::EnvironmentVariable::IsSet pass (see <see cref="TestResources.NrOfTestsNeedingSampleSettings"/>). Batch files and the
        /// helper file parameter of HelperFileTests_taef.dll are not set up.
        /// </summary>
        protected void SetupSampleTestsSettings()
        {
            MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TestDirectory=" + PlaceholderReplacer.TestDirPlaceholder + "\"");
            MockOptions.Setup(o => o.WorkingDir).Returns(PlaceholderReplacer.SolutionDirPlaceholder);
            MockOptions.Setup(o => o.EnvironmentVariables).Returns("MYENVVAR=MyValue");
        }

        [TestCleanup]
        public virtual void TearDown()
        {
            MockLogger.Reset();
            MockOptions.Reset();
            MockFrameworkReporter.Reset();
        }

    }

}
