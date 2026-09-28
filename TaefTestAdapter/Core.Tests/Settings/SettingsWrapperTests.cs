// This file has been modified for TAEF support.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    [TestClass]
    public class SettingsWrapperTests : TestsBase
    {
        /// <summary>A test DLL path (the file does not need to exist).</summary>
        private const string TestDll = @"C:\TheSolution\out\Debug\My_taef.dll";
        private const string TestDllDir = @"C:\TheSolution\out\Debug";

        private Mock<RunSettings> MockXmlOptions { get; } = new Mock<RunSettings>();
        private SettingsWrapper TheOptions { get; set; }


        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            var containerMock = new Mock<ITaefTestAdapterSettingsContainer>();
            containerMock.Setup(c => c.SolutionSettings).Returns(MockXmlOptions.Object);
            containerMock.Setup(c => c.GetSettingsForTestDll(It.IsAny<string>())).Returns(MockXmlOptions.Object);
            TheOptions = new SettingsWrapper(containerMock.Object)
            {
                RegexTraitParser = new RegexTraitParser(TestEnvironment.Logger),
                EnvironmentVariablesParser = new EnvironmentVariablesParser(TestEnvironment.Logger),
                HelperFilesCache = new HelperFilesCache(TestEnvironment.Logger)
            };
        }

        [TestCleanup]
        public override void TearDown()
        {
            base.TearDown();

            MockXmlOptions.Reset();
        }

        #region TAEF options

        [TestMethod]
        [TestCategory(Unit)]
        public void NrOfTestRepetitions_InvalidValue_ReturnsDefaultValue()
        {
            MockXmlOptions.Setup(o => o.NrOfTestRepetitions).Returns(-2);
            TheOptions.NrOfTestRepetitions.Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue);

            // negative values are invalid
            MockXmlOptions.Setup(o => o.NrOfTestRepetitions).Returns(-1);
            TheOptions.NrOfTestRepetitions.Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue);

            MockXmlOptions.Setup(o => o.NrOfTestRepetitions).Returns(0);
            TheOptions.NrOfTestRepetitions.Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue);

            MockXmlOptions.Setup(o => o.NrOfTestRepetitions).Returns(SettingsWrapper.OptionNrOfTestRepetitionsMinValue);
            TheOptions.NrOfTestRepetitions.Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsMinValue);

            MockXmlOptions.Setup(o => o.NrOfTestRepetitions).Returns(4711);
            TheOptions.NrOfTestRepetitions.Should().Be(4711);

            MockXmlOptions.Setup(o => o.NrOfTestRepetitions).Returns((int?)null);
            TheOptions.NrOfTestRepetitions.Should().Be(1);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void OptionDescriptions_BehaviorInProcess_IsDocumented()
        {
            // if tests are run in process (option RunInProcess or debugging), CommandLineGenerator omits /testTimeout and
            // /isolationLevel (TE.exe would ignore or block them), and repeats each test within a single loop instead of
            // running several loops (each of which needs new test hosts)
            SettingsWrapper.OptionNrOfTestRepetitionsDescription.Should()
                .Contain($"'{SettingsWrapper.OptionRunInProcess}'").And.Contain("debugged").And.Contain("same process")
                .And.Contain(TaefConstants.TestModeLoopOption + " " + TaefConstants.LoopOption + "<n> " + TaefConstants.LoopTestOneOption)
                .And.Contain(TaefConstants.TestModeLoopOption + " " + TaefConstants.LoopOption + "1 /LoopTest:<n> if tests are run in process or are being debugged");
            SettingsWrapper.OptionTestTimeoutDescription.Should()
                .Contain($"'{SettingsWrapper.OptionRunInProcess}'").And.Contain("debugged").And.Contain(TaefConstants.DisableTimeoutsOption);
            SettingsWrapper.OptionIsolationLevelDescription.Should()
                .Contain($"'{SettingsWrapper.OptionRunInProcess}'").And.Contain("debugged").And.Contain("not passed");

            SettingsWrapper.OptionRunInProcessDescription.Should()
                .Contain($"'{SettingsWrapper.OptionTestTimeout}'")
                .And.Contain($"'{SettingsWrapper.OptionIsolationLevel}'")
                .And.Contain($"'{SettingsWrapper.OptionNrOfTestRepetitions}'")
                .And.Contain("always run in process while being debugged");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void OptionDebuggerKindDescription_DocumentsRestrictionsOfEngines()
        {
            // VsTestFramework: SequentialTestRunner reads the results from a WTT log after TE.exe has finished (no console
            // output); Native/ManagedAndNative: the debugger attacher service of the VS extension is needed (see TestExecutor)
            SettingsWrapper.OptionDebuggerKindDescription.Should()
                .Contain(DebuggerKindConverter.VsTestFramework + ": ")
                .And.Contain(DebuggerKindConverter.Native + ": ")
                .And.Contain(DebuggerKindConverter.ManagedAndNative + ": ")
                .And.Contain("WTT log").And.Contain(TaefConstants.EnableWttLoggingOption)
                .And.Contain("no printf").And.Contain($"'{SettingsWrapper.OptionPrintTestOutput}' has no effect")
                .And.Contain("durations are not measured").And.Contain("crashes")
                .And.Contain("Requires the Visual Studio extension").And.Contain("NuGet")
                .And.Contain(TaefConstants.InProcOption).And.Contain(TaefConstants.DisableTimeoutsOption);
            SettingsWrapper.OptionDebuggerKindDescription.Should()
                .NotContain("no test crash detection").And.NotContain("no restrictions");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void OptionSkipOriginCheckDescription__DocumentsWhereTheOptionCanBeSet()
        {
            // the VS extension ignores the option in settings files (see RunSettingsService), the test adapter itself does not
            SettingsWrapper.OptionSkipOriginCheckDescription.Should()
                .Contain("Visual Studio options").And.Contain("ignored")
                .And.Contain("vstest.console.exe").And.Contain("settings file");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TeExecutable__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.TeExecutable).Returns((string)null);
            TheOptions.TeExecutable.Should().Be(SettingsWrapper.OptionTeExecutableDefaultValue);
            TheOptions.TeExecutable.Should().BeEmpty();
            TheOptions.GetTeExecutable(TestDll).Should().BeEmpty();

            MockXmlOptions.Setup(o => o.TeExecutable).Returns(@"C:\TAEF\TE.exe");
            TheOptions.TeExecutable.Should().Be(@"C:\TAEF\TE.exe");
            TheOptions.GetTeExecutable(TestDll).Should().Be(@"C:\TAEF\TE.exe");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTeExecutable__PlaceholdersAreReplaced()
        {
            MockXmlOptions.Setup(o => o.SolutionDir).Returns(@"C:\TheSolution");
            MockXmlOptions.Setup(o => o.PlatformName).Returns("x64");
            MockXmlOptions.Setup(o => o.TeExecutable).Returns(@" $(SolutionDir)\tools\$(PlatformName)\TE.exe ");
            TheOptions.GetTeExecutable(TestDll).Should().Be(@"C:\TheSolution\tools\x64\TE.exe");

            MockXmlOptions.Setup(o => o.TeExecutable).Returns(@"$(TestDllDir)\..\TAEF");
            TheOptions.GetTeExecutable(TestDll).Should().Be(TestDllDir + @"\..\TAEF");

            MockXmlOptions.Setup(o => o.TeExecutable).Returns(@"%ProgramFiles(x86)%\Windows Kits\10\Testing\Runtimes\TAEF");
            TheOptions.GetTeExecutable(TestDll).Should().Be(
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\Windows Kits\10\Testing\Runtimes\TAEF");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunIgnoredTests__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.RunIgnoredTests).Returns((bool?)null);
            TheOptions.RunIgnoredTests.Should().Be(SettingsWrapper.OptionRunIgnoredTestsDefaultValue);
            TheOptions.RunIgnoredTests.Should().BeFalse();

            MockXmlOptions.Setup(o => o.RunIgnoredTests).Returns(!SettingsWrapper.OptionRunIgnoredTestsDefaultValue);
            TheOptions.RunIgnoredTests.Should().Be(!SettingsWrapper.OptionRunIgnoredTestsDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BreakOnError__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.BreakOnError).Returns((bool?)null);
            bool result = TheOptions.BreakOnError;
            result.Should().Be(SettingsWrapper.OptionBreakOnErrorDefaultValue);

            MockXmlOptions.Setup(o => o.BreakOnError).Returns(!SettingsWrapper.OptionBreakOnErrorDefaultValue);
            result = TheOptions.BreakOnError;
            result.Should().Be(!SettingsWrapper.OptionBreakOnErrorDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestTimeout_ValidValues_AreReturnedTrimmed()
        {
            MockXmlOptions.Setup(o => o.TestTimeout).Returns((string)null);
            TheOptions.TestTimeout.Should().Be(SettingsWrapper.OptionTestTimeoutDefaultValue);
            TheOptions.TestTimeout.Should().BeEmpty();

            foreach (string value in new[] { "0:0:3", "0:05", "1", "23:59:59", "1.02:00", "0:0:0.5", "0:0:1.1234567" })
            {
                MockXmlOptions.Setup(o => o.TestTimeout).Returns(value);
                TheOptions.TestTimeout.Should().Be(value);
            }

            MockXmlOptions.Setup(o => o.TestTimeout).Returns("  0:0:30 ");
            TheOptions.TestTimeout.Should().Be("0:0:30");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestTimeout_InvalidValues_ReturnDefault()
        {
            foreach (string value in new[] { "abc", "0:60", "24", "0:0:60", "1.24", "0:0:1.12345678", "-1", "0:5:", ":5", "   " })
            {
                MockXmlOptions.Setup(o => o.TestTimeout).Returns(value);
                TheOptions.TestTimeout.Should().Be(SettingsWrapper.OptionTestTimeoutDefaultValue, $"'{value}' is invalid");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsolationLevel__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.IsolationLevel).Returns((TaefIsolationLevel?)null);
            TheOptions.IsolationLevel.Should().Be(SettingsWrapper.OptionIsolationLevelDefaultValue);
            TheOptions.IsolationLevel.Should().Be(TaefIsolationLevel.Default);

            foreach (TaefIsolationLevel level in Enum.GetValues(typeof(TaefIsolationLevel)))
            {
                MockXmlOptions.Setup(o => o.IsolationLevel).Returns(level);
                TheOptions.IsolationLevel.Should().Be(level);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunInProcess__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.RunInProcess).Returns((bool?)null);
            TheOptions.RunInProcess.Should().Be(SettingsWrapper.OptionRunInProcessDefaultValue);
            TheOptions.RunInProcess.Should().BeFalse();

            MockXmlOptions.Setup(o => o.RunInProcess).Returns(true);
            TheOptions.RunInProcess.Should().BeTrue();
        }

        #endregion

        #region General, discovery and execution options

        [TestMethod]
        [TestCategory(Unit)]
        public void MaxNrOfThreads_InvalidValue_ReturnsDefaultValue()
        {
            MockXmlOptions.Setup(o => o.MaxNrOfThreads).Returns(-1);
            TheOptions.MaxNrOfThreads.Should().Be(Environment.ProcessorCount);

            MockXmlOptions.Setup(o => o.MaxNrOfThreads).Returns(0);
            TheOptions.MaxNrOfThreads.Should().Be(Environment.ProcessorCount);

            if (Environment.ProcessorCount > 1)
            {
                MockXmlOptions.Setup(o => o.MaxNrOfThreads).Returns(Environment.ProcessorCount - 1);
                TheOptions.MaxNrOfThreads.Should().Be(Environment.ProcessorCount - 1);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestDiscoveryTimeoutInSeconds__ZeroIsInfiniteAndNegativeValuesAreInvalid()
        {
            MockXmlOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns((int?)null);
            TheOptions.TestDiscoveryTimeoutInSeconds.Should().Be(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue);

            MockXmlOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns(0);
            TheOptions.TestDiscoveryTimeoutInSeconds.Should().Be(int.MaxValue);

            MockXmlOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns(-5);
            TheOptions.TestDiscoveryTimeoutInSeconds.Should().Be(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue);

            MockXmlOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns(42);
            TheOptions.TestDiscoveryTimeoutInSeconds.Should().Be(42);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AdditionalTestExecutionParam__PlaceholdersAreTreatedCorrectly()
        {
            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(PlaceholderReplacer.TestDirPlaceholder);
            string result = TheOptions.GetUserParametersForExecution(TestDll, "mydir", 0);
            result.Should().Be("mydir");

            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(PlaceholderReplacer.TestDirPlaceholder + " " + PlaceholderReplacer.TestDirPlaceholder);
            result = TheOptions.GetUserParametersForExecution(TestDll, "mydir", 0);
            result.Should().Be("mydir mydir");

            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(PlaceholderReplacer.TestDirPlaceholder.ToLower());
            result = TheOptions.GetUserParametersForExecution(TestDll, "mydir", 0);
            result.Should().Be(PlaceholderReplacer.TestDirPlaceholder.ToLower());

            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(PlaceholderReplacer.ThreadIdPlaceholder);
            result = TheOptions.GetUserParametersForExecution(TestDll, "mydir", 4711);
            result.Should().Be("4711");

            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(PlaceholderReplacer.TestDirPlaceholder + ", " + PlaceholderReplacer.ThreadIdPlaceholder);
            result = TheOptions.GetUserParametersForExecution(TestDll, "mydir", 4711);
            result.Should().Be("mydir, 4711");

            MockXmlOptions.Setup(o => o.SolutionDir).Returns(@"C:\\TheSolutionDir");
            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(PlaceholderReplacer.TestDirPlaceholder + ", " + PlaceholderReplacer.ThreadIdPlaceholder + ", " + PlaceholderReplacer.SolutionDirPlaceholder + ", " + PlaceholderReplacer.TestDllPlaceholder);
            result = TheOptions.GetUserParametersForExecution(TestDll, "mydir", 4711);
            result.Should().Be($@"mydir, 4711, C:\\TheSolutionDir, {TestDll}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetUserParametersForDiscovery__TestDirAndThreadIdAreRemoved()
        {
            MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns("/p:\"TestDirectory=$(TestDir)\" /p:\"Thread=$(ThreadId)\" /p:\"Dll=$(TestDll)\"");

            TheOptions.GetUserParametersForDiscovery(TestDll).Should().Be($"/p:\"TestDirectory=\" /p:\"Thread=\" /p:\"Dll={TestDll}\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void PrintTestOutput__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.PrintTestOutput).Returns((bool?)null);
            bool result = TheOptions.PrintTestOutput;
            result.Should().Be(SettingsWrapper.OptionPrintTestOutputDefaultValue);

            MockXmlOptions.Setup(o => o.PrintTestOutput).Returns(!SettingsWrapper.OptionPrintTestOutputDefaultValue);
            result = TheOptions.PrintTestOutput;
            result.Should().Be(!SettingsWrapper.OptionPrintTestOutputDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void PrefixOutputWithTaef__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.PrefixOutputWithTaef).Returns((bool?)null);
            TheOptions.PrefixOutputWithTaef.Should().Be(SettingsWrapper.OptionPrefixOutputWithTaefDefaultValue);

            MockXmlOptions.Setup(o => o.PrefixOutputWithTaef).Returns(!SettingsWrapper.OptionPrefixOutputWithTaefDefaultValue);
            TheOptions.PrefixOutputWithTaef.Should().Be(!SettingsWrapper.OptionPrefixOutputWithTaefDefaultValue);
            SettingsWrapper.OptionPrefixOutputWithTaef.Should().Contain("[TAEF]");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SkipOriginCheck__ReturnsValueOrDefault()
        {
            // not virtual in RunSettings (the VS extension takes the value from the VS options only)
            MockXmlOptions.Object.SkipOriginCheck = null;
            TheOptions.SkipOriginCheck.Should().Be(SettingsWrapper.OptionSkipOriginCheckDefaultValue);

            MockXmlOptions.Object.SkipOriginCheck = !SettingsWrapper.OptionSkipOriginCheckDefaultValue;
            TheOptions.SkipOriginCheck.Should().Be(!SettingsWrapper.OptionSkipOriginCheckDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void MissingTestsReportMode__ReturnsValueOrDefault()
        {
            MockXmlOptions.Object.MissingTestsReportMode = null;
            TheOptions.MissingTestsReportMode.Should().Be(SettingsWrapper.OptionMissingTestsReportModeDefaultValue);

            MockXmlOptions.Object.MissingTestsReportMode = MissingTestsReportMode.ReportAsFailed;
            TheOptions.MissingTestsReportMode.Should().Be(MissingTestsReportMode.ReportAsFailed);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseSymbolInformation__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.ParseSymbolInformation).Returns((bool?)null);
            bool result = TheOptions.ParseSymbolInformation;
            result.Should().Be(SettingsWrapper.OptionParseSymbolInformationDefaultValue);

            MockXmlOptions.Setup(o => o.ParseSymbolInformation).Returns(!SettingsWrapper.OptionParseSymbolInformationDefaultValue);
            result = TheOptions.ParseSymbolInformation;
            result.Should().Be(!SettingsWrapper.OptionParseSymbolInformationDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void OutputMode__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.OutputMode).Returns((OutputMode?)null);
            OutputMode result = TheOptions.OutputMode;
            result.Should().Be(SettingsWrapper.OptionOutputModeDefaultValue);

            MockXmlOptions.Setup(o => o.OutputMode).Returns(OutputMode.Verbose);
            result = TheOptions.OutputMode;
            result.Should().Be(OutputMode.Verbose);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebuggerKind__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.DebuggerKind).Returns((DebuggerKind?)null);
            TheOptions.DebuggerKind.Should().Be(SettingsWrapper.OptionDebuggerKindDefaultValue);
            TheOptions.DebuggerKind.Should().Be(DebuggerKind.Native);

            MockXmlOptions.Setup(o => o.DebuggerKind).Returns(DebuggerKind.VsTestFramework);
            TheOptions.DebuggerKind.Should().Be(DebuggerKind.VsTestFramework);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParallelTestExecution__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.ParallelTestExecution).Returns((bool?)null);
            bool result = TheOptions.ParallelTestExecution;
            result.Should().Be(SettingsWrapper.OptionParallelTestExecutionDefaultValue);

            MockXmlOptions.Setup(o => o.ParallelTestExecution).Returns(!SettingsWrapper.OptionParallelTestExecutionDefaultValue);
            result = TheOptions.ParallelTestExecution;
            result.Should().Be(!SettingsWrapper.OptionParallelTestExecutionDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void KillProcessesOnCancel__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.KillProcessesOnCancel).Returns((bool?)null);
            TheOptions.KillProcessesOnCancel.Should().Be(SettingsWrapper.OptionKillProcessesOnCancelDefaultValue);

            MockXmlOptions.Setup(o => o.KillProcessesOnCancel).Returns(!SettingsWrapper.OptionKillProcessesOnCancelDefaultValue);
            TheOptions.KillProcessesOnCancel.Should().Be(!SettingsWrapper.OptionKillProcessesOnCancelDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestDiscoveryRegex__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.TestDiscoveryRegex).Returns((string)null);
            string result = TheOptions.TestDiscoveryRegex;
            result.Should().Be(SettingsWrapper.OptionTestDiscoveryRegexDefaultValue);

            MockXmlOptions.Setup(o => o.TestDiscoveryRegex).Returns("FooBar");
            result = TheOptions.TestDiscoveryRegex;
            result.Should().Be("FooBar");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void PathExtension__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.PathExtension).Returns((string)null);
            string result = TheOptions.PathExtension;
            result.Should().Be(SettingsWrapper.OptionPathExtensionDefaultValue);

            MockXmlOptions.Setup(o => o.PathExtension).Returns("FooBar");
            result = TheOptions.PathExtension;
            result.Should().Be("FooBar");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetPathExtension__PlaceholderIsReplaced()
        {
            MockXmlOptions.Setup(o => o.PathExtension).Returns("Foo;" + PlaceholderReplacer.TestDllDirPlaceholder + ";Bar");
            string result = TheOptions.GetPathExtension(TestDll);

            result.Should().Be($"Foo;{TestDllDir};Bar");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetPathExtension__EnvVarPlaceholderIsReplaced()
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                var name = (string)variable.Key;
                string value = (string)variable.Value;
                MockXmlOptions.Setup(o => o.PathExtension).Returns($"Foo;%{name}%;Bar");
                string result = TheOptions.GetPathExtension(TestDll);

                result.Should().Be($"Foo;{value};Bar");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetPathExtension__PlatformAndConfigurationNamePlaceholdersAreReplaced()
        {
            MockXmlOptions.Setup(o => o.PlatformName).Returns("Debug");
            MockXmlOptions.Setup(o => o.ConfigurationName).Returns("x86");
            MockXmlOptions.Setup(o => o.PathExtension).Returns(
                $"P:{PlaceholderReplacer.PlatformNamePlaceholder}, C:{PlaceholderReplacer.ConfigurationNamePlaceholder}");

            string result = TheOptions.GetPathExtension(TestDll);

            result.Should().Be("P:Debug, C:x86");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BatchForTestTeardown__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.BatchForTestTeardown).Returns((string)null);
            string result = TheOptions.BatchForTestTeardown;
            result.Should().Be(SettingsWrapper.OptionBatchForTestTeardownDefaultValue);

            MockXmlOptions.Setup(o => o.BatchForTestTeardown).Returns("FooBar");
            result = TheOptions.BatchForTestTeardown;
            result.Should().Be("FooBar");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BatchForTestTeardown__EnvVarPlaceholderIsReplaced()
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                var name = (string)variable.Key;
                string value = (string)variable.Value;
                MockXmlOptions.Setup(o => o.BatchForTestTeardown).Returns($"Foo;%{name}%;Bar");
                string result = TheOptions.GetBatchForTestTeardown("TestDirectory", 4711);

                result.Should().Be($"Foo;{value};Bar");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BatchForTestSetup__ReturnsValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.BatchForTestSetup).Returns((string)null);
            string result = TheOptions.BatchForTestSetup;
            result.Should().Be(SettingsWrapper.OptionBatchForTestSetupDefaultValue);

            MockXmlOptions.Setup(o => o.BatchForTestSetup).Returns("FooBar");
            result = TheOptions.BatchForTestSetup;
            result.Should().Be("FooBar");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BatchForTestSetup__EnvVarPlaceholderIsReplaced()
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                var name = (string)variable.Key;
                string value = (string)variable.Value;
                MockXmlOptions.Setup(o => o.BatchForTestSetup).Returns($"Foo;%{name}%;Bar");
                string result = TheOptions.GetBatchForTestSetup("TestDirectory", 4711);

                result.Should().Be($"Foo;{value};Bar");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetBatchForTestSetup__TestDirThreadIdAndSolutionDirAreReplaced()
        {
            MockXmlOptions.Setup(o => o.SolutionDir).Returns(@"C:\TheSolution\");
            MockXmlOptions.Setup(o => o.BatchForTestSetup).Returns(@"$(SolutionDir)Tests\Setup.bat $(TestDir) $(ThreadId)");

            TheOptions.GetBatchForTestSetup(@"C:\testdir", 3).Should().Be(@"C:\TheSolution\Tests\Setup.bat C:\testdir 3");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWorkingDir__EnvVarPlaceholderIsReplaced()
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                var name = (string)variable.Key;
                string value = (string)variable.Value;
                MockXmlOptions.Setup(o => o.WorkingDir).Returns($"Foo;%{name}%;Bar");
                string result = TheOptions.GetWorkingDirForExecution(TestDll, "TestDirectory", 4711);

                result.Should().Be($"Foo;{value};Bar");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void WorkingDir_NotSet_IsTestDllDir()
        {
            SettingsWrapper.OptionWorkingDirDefaultValue.Should().Be(PlaceholderReplacer.TestDllDirPlaceholder);

            MockXmlOptions.Setup(o => o.WorkingDir).Returns((string)null);
            TheOptions.WorkingDir.Should().Be(PlaceholderReplacer.TestDllDirPlaceholder);

            MockXmlOptions.Setup(o => o.WorkingDir).Returns("  ");
            TheOptions.WorkingDir.Should().Be(PlaceholderReplacer.TestDllDirPlaceholder);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWorkingDirForExecution_null_TestDllDirIsReturned()
        {
            MockXmlOptions.Setup(o => o.WorkingDir).Returns((string)null);

            var result = TheOptions.GetWorkingDirForExecution(TestDll, "", 0);

            result.Should().Be(TestDllDir);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWorkingDirForExecution_EmptyString_TestDllDirIsReturned()
        {
            MockXmlOptions.Setup(o => o.WorkingDir).Returns("");

            var result = TheOptions.GetWorkingDirForExecution(TestDll, "", 0);

            result.Should().Be(TestDllDir);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWorkingDirForExecution_TestDirAndThreadId_AreReplaced()
        {
            MockXmlOptions.Setup(o => o.WorkingDir).Returns(@"$(TestDir)\$(ThreadId)");

            TheOptions.GetWorkingDirForExecution(TestDll, @"C:\testdir", 2).Should().Be(@"C:\testdir\2");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWorkingDirForDiscovery_null_TestDllDirIsReturned()
        {
            MockXmlOptions.Setup(o => o.WorkingDir).Returns((string) null);

            var result = TheOptions.GetWorkingDirForDiscovery(TestDll);

            result.Should().Be(TestDllDir);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWorkingDirForDiscovery_EmptyString_TestDllDirIsReturned()
        {
            MockXmlOptions.Setup(o => o.WorkingDir).Returns("");

            var result = TheOptions.GetWorkingDirForDiscovery(TestDll);

            result.Should().Be(TestDllDir);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetUserParams__EnvVarPlaceholderIsReplaced()
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                var name = (string)variable.Key;
                string value = (string)variable.Value;

                MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns($"Foo;%{name.ToLower()}%;Bar");
                string result = TheOptions.GetUserParametersForExecution(TestDll, "TestDirectory", 4711);
                result.Should().Be($"Foo;{value};Bar");

                MockXmlOptions.Setup(o => o.AdditionalTestExecutionParam).Returns($"Foo;%{name.ToUpper()}%;Bar");
                result = TheOptions.GetUserParametersForExecution(TestDll, "TestDirectory", 4711);
                result.Should().Be($"Foo;{value};Bar");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetAdditionalPdbs__EnvVarPlaceholderIsReplaced()
        {
            foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
            {
                var name = (string)variable.Key;
                string value = (string)variable.Value;

                MockXmlOptions.Setup(o => o.AdditionalPdbs).Returns($"Foo%{name}%;Bar");
                var result = TheOptions.GetAdditionalPdbs(TestDll).ToList();

                result.Should().HaveCount(2);
                result[0].Should().Be($"Foo{value}");
                result[1].Should().Be("Bar");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetAdditionalPdbs__TestDllPlaceholdersAreReplacedAndEntriesAreTrimmed()
        {
            MockXmlOptions.Setup(o => o.AdditionalPdbs).Returns(@" $(TestDllDir)\pdbs\*.pdb ;;$(TestDll).pdb");

            TheOptions.GetAdditionalPdbs(TestDll).Should().Equal(TestDllDir + @"\pdbs\*.pdb", TestDll + ".pdb");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TraitsRegexesBefore__ReturnsParsedValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.TraitsRegexesBefore).Returns((string)null);
            List<RegexTraitPair> result = TheOptions.TraitsRegexesBefore;
            result.Should().Equal(new List<RegexTraitPair>());

            MockXmlOptions.Setup(o => o.TraitsRegexesBefore).Returns("Foo///Bar,Baz");
            result = TheOptions.TraitsRegexesBefore;
            result.Should().ContainSingle();
            RegexTraitPair resultPair = result[0];
            resultPair.Regex.Should().Be("Foo");
            resultPair.Trait.Name.Should().Be("Bar");
            resultPair.Trait.Value.Should().Be("Baz");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TraitsRegexesAfter__ReturnsParsedValueOrDefault()
        {
            MockXmlOptions.Setup(o => o.TraitsRegexesAfter).Returns((string)null);
            List<RegexTraitPair> result = TheOptions.TraitsRegexesAfter;
            result.Should().Equal(new List<RegexTraitPair>());

            MockXmlOptions.Setup(o => o.TraitsRegexesAfter).Returns("Foo///Bar,Baz");
            result = TheOptions.TraitsRegexesAfter;
            result.Should().ContainSingle();
            RegexTraitPair resultPair = result[0];
            resultPair.Regex.Should().Be("Foo");
            resultPair.Trait.Name.Should().Be("Bar");
            resultPair.Trait.Value.Should().Be("Baz");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SolutionDir_SolutionDirGivenToConstructor_OverridesSettings()
        {
            var containerMock = new Mock<ITaefTestAdapterSettingsContainer>();
            containerMock.Setup(c => c.SolutionSettings).Returns(new RunSettings { SolutionDir = @"C:\FromSettings" });

            new SettingsWrapper(containerMock.Object).SolutionDir.Should().Be(@"C:\FromSettings");
            new SettingsWrapper(containerMock.Object, @"C:\FromConstructor").SolutionDir.Should().Be(@"C:\FromConstructor");
        }

        #endregion

        #region ToString()

        [TestMethod]
        [TestCategory(Unit)]
        public void ToString_PrintsCorrectly()
        {
            MockXmlOptions.Setup(s => s.TraitsRegexesBefore).Returns("Foo///Bar,Baz//||//Foo2///Bar2,Baz2");
            MockXmlOptions.Setup(s => s.BatchForTestSetup).Returns(@"C:\\myfolder\myfile.xml");
            MockXmlOptions.Setup(s => s.MaxNrOfThreads).Returns(1);
            MockXmlOptions.Setup(s => s.TestTimeout).Returns("0:0:30");
            MockXmlOptions.Setup(s => s.IsolationLevel).Returns(TaefIsolationLevel.Class);

            string optionsString = TheOptions.ToString();
            optionsString.Should().Contain("DebuggerKind: Native");
            optionsString.Should().Contain("PrintTestOutput: False");
            optionsString.Should().Contain("TestDiscoveryRegex: ''");
            optionsString.Should().Contain("WorkingDir: '$(TestDllDir)'");
            optionsString.Should().Contain("PathExtension: ''");
            optionsString.Should().Contain("TraitsRegexesBefore: {'Foo': (Bar,Baz), 'Foo2': (Bar2,Baz2)}");
            optionsString.Should().Contain("TraitsRegexesAfter: {}");
            optionsString.Should().Contain("ParseSymbolInformation: True");
            optionsString.Should().Contain("OutputMode: Info");
            optionsString.Should().Contain("TimestampMode: Automatic");
            optionsString.Should().Contain("SeverityMode: Automatic");
            optionsString.Should().Contain("SummaryMode: WarningOrError");
            optionsString.Should().Contain("PrefixOutputWithTaef: False");
            optionsString.Should().Contain("AdditionalTestExecutionParam: ''");
            optionsString.Should().Contain("BatchForTestSetup: 'C:\\\\myfolder\\myfile.xml'");
            optionsString.Should().Contain("BatchForTestTeardown: ''");
            optionsString.Should().Contain("ParallelTestExecution: False");
            optionsString.Should().Contain("MaxNrOfThreads: 1");
            optionsString.Should().Contain("BreakOnError: False");
            optionsString.Should().Contain("NrOfTestRepetitions: 1");
            optionsString.Should().Contain("TeExecutable: ''");
            optionsString.Should().Contain("RunIgnoredTests: False");
            optionsString.Should().Contain("TestTimeout: '0:0:30'");
            optionsString.Should().Contain("IsolationLevel: Class");
            optionsString.Should().Contain("RunInProcess: False");
            optionsString.Should().Contain("KillProcessesOnCancel: False");
            optionsString.Should().Contain("MissingTestsReportMode: ReportAsNotFound");
            optionsString.Should().Contain("TestDiscoveryTimeoutInSeconds: 30");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToString_InternalMembers_AreNotPrinted()
        {
            string optionsString = TheOptions.ToString();

            optionsString.Should().NotContain("HelperFilesCache").And.NotContain("RegexTraitParser").And.NotContain("DebuggingNamedPipeId");
        }

        #endregion

        #region Project settings

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_NewInstance_ShouldDeliverSolutionSettings()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings.WorkingDir.Should().Be("solution_dir");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_WithConfiguredProject_ShouldDeliverProjectSettings()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () =>
            {
                settings.WorkingDir.Should().Be("foo_dir");
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_WithConfiguredProject_ShouldSwitchBackToSolutionSettingsAfterExecution()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () => {});

            settings.WorkingDir.Should().Be("solution_dir");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_ActionThrows_ShouldSwitchBackToSolutionSettings()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings
                .Invoking(s => s.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () => throw new InvalidOperationException("boom")))
                .Should().Throw<InvalidOperationException>().WithMessage("boom");

            settings.WorkingDir.Should().Be("solution_dir");
            settings.ExecuteWithSettingsForTestDll("bar", MockLogger.Object, () => settings.WorkingDir.Should().Be("solution_dir"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_WithUnconfiguredProject_ShouldDeliverSolutionSettings()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings.ExecuteWithSettingsForTestDll("bar", MockLogger.Object, () =>
            {
                settings.WorkingDir.Should().Be("solution_dir");
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_NestedExecutionOfSameTestDll_ShouldDeliverProjectSettings()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () =>
            {
                settings.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () =>
                {
                    settings.WorkingDir.Should().Be("foo_dir");
                });

            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_NestedExecutionOfDifferentTestDlls_ShouldThrow()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo");

            settings
                .Invoking(s => s.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () =>
                {
                    s.ExecuteWithSettingsForTestDll("bar", MockLogger.Object, () => { });
                }))
                .Should().Throw<InvalidOperationException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Clone_WhileExecuting_ReturnsFreshSettingsWrapperInstance()
        {
            var settings = CreateSettingsWrapper("solution_dir", "foo", "bar");

            settings.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () =>
            {
                var settingsClone = settings.Clone();
                settingsClone.WorkingDir.Should().Be("solution_dir");
                settingsClone.ExecuteWithSettingsForTestDll("bar", MockLogger.Object, () =>
                {
                    settings.WorkingDir.Should().Be("foo_dir");
                    settingsClone.WorkingDir.Should().Be("bar_dir");
                });
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecuteWithSettingsForTestDll_ProjectSettingsOfTaefOptions_AreUsed()
        {
            var containerMock = new Mock<ITaefTestAdapterSettingsContainer>();
            containerMock.Setup(c => c.SolutionSettings).Returns(new RunSettings { RunInProcess = false, TestTimeout = "0:0:10", IsolationLevel = TaefIsolationLevel.Module });
            containerMock.Setup(c => c.GetSettingsForTestDll("foo")).Returns(new RunSettings("foo") { RunInProcess = true, TeExecutable = @"C:\TAEF\TE.exe", IsolationLevel = TaefIsolationLevel.Test });
            var settings = new SettingsWrapper(containerMock.Object)
            {
                RegexTraitParser = new RegexTraitParser(MockLogger.Object),
                EnvironmentVariablesParser = new EnvironmentVariablesParser(MockLogger.Object),
                HelperFilesCache = new HelperFilesCache(MockLogger.Object)
            };

            settings.ExecuteWithSettingsForTestDll("foo", MockLogger.Object, () =>
            {
                settings.RunInProcess.Should().BeTrue();
                settings.TeExecutable.Should().Be(@"C:\TAEF\TE.exe");
                settings.IsolationLevel.Should().Be(TaefIsolationLevel.Test);
                // project settings do not inherit unset values from the solution settings here (see RunSettingsContainer)
                settings.TestTimeout.Should().BeEmpty();
            });
            settings.RunInProcess.Should().BeFalse();
            settings.TestTimeout.Should().Be("0:0:10");
            settings.IsolationLevel.Should().Be(TaefIsolationLevel.Module);
        }

        #endregion

        #region Environment variables

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_Empty_ReturnsEmptyDictionary()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("");

            var envVars = TheOptions.GetEnvironmentVariablesForDiscovery(TestDll);
            envVars.Should().BeEmpty();

            envVars = TheOptions.GetEnvironmentVariablesForExecution(TestDll, "theTestDirectory", 42);
            envVars.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_SingleKeyValuePair_ReturnsDictionaryWithThatPair()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("MyVar=MyValue");

            var envVars = TheOptions.GetEnvironmentVariablesForDiscovery(TestDll);
            envVars.Should().HaveCount(1);
            envVars["MyVar"].Should().Be("MyValue");

            envVars = TheOptions.GetEnvironmentVariablesForExecution(TestDll, "theTestDirectory", 42);
            envVars.Should().HaveCount(1);
            envVars["MyVar"].Should().Be("MyValue");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_SinglePairWithEmptyValue_ReturnsDictionaryWithThatPair()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("MyVar=");

            var envVars = TheOptions.GetEnvironmentVariablesForDiscovery(TestDll);
            envVars.Should().HaveCount(1);
            envVars["MyVar"].Should().BeEmpty();

            envVars = TheOptions.GetEnvironmentVariablesForExecution(TestDll, "theTestDirectory", 42);
            envVars.Should().HaveCount(1);
            envVars["MyVar"].Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_SinglePairWithoutValue_ReturnsDictionaryWithThatPair()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("MyVar");

            var envVars = TheOptions.GetEnvironmentVariablesForDiscovery(TestDll);
            envVars.Should().BeEmpty();

            envVars = TheOptions.GetEnvironmentVariablesForExecution(TestDll, "theTestDirectory", 42);
            envVars.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_SinglePairWithEqualsSignInValue_ReturnsDictionaryWithThatPair()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("MyVar=A=B");

            var envVars = TheOptions.GetEnvironmentVariablesForDiscovery(TestDll);
            envVars.Should().HaveCount(1);
            envVars["MyVar"].Should().Be("A=B");

            envVars = TheOptions.GetEnvironmentVariablesForExecution(TestDll, "theTestDirectory", 42);
            envVars.Should().HaveCount(1);
            envVars["MyVar"].Should().Be("A=B");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_SeveralPairs_AreAllReturned()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("MyVar=A=B//||//MyOtherVar==//||//MyLastVar=Foo///||//");

            var envVars = TheOptions.GetEnvironmentVariablesForDiscovery(TestDll);
            envVars.Should().HaveCount(3);
            envVars["MyVar"].Should().Be("A=B");
            envVars["MyOtherVar"].Should().Be("=");
            envVars["MyLastVar"].Should().Be("Foo/");

            envVars = TheOptions.GetEnvironmentVariablesForExecution(TestDll, "theTestDirectory", 42);
            envVars.Should().HaveCount(3);
            envVars["MyVar"].Should().Be("A=B");
            envVars["MyOtherVar"].Should().Be("=");
            envVars["MyLastVar"].Should().Be("Foo/");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EnvironmentVariables_Placeholders_AreReplaced()
        {
            MockXmlOptions.Setup(o => o.EnvironmentVariables).Returns("Dir=$(TestDir)//||//Dll=$(TestDll)");

            TheOptions.GetEnvironmentVariablesForExecution(TestDll, @"C:\testdir", 42)
                .Should().Equal(new Dictionary<string, string> { { "Dir", @"C:\testdir" }, { "Dll", TestDll } });
            TheOptions.GetEnvironmentVariablesForDiscovery(TestDll)
                .Should().Equal(new Dictionary<string, string> { { "Dir", "" }, { "Dll", TestDll } });
        }

        #endregion

        private SettingsWrapper CreateSettingsWrapper(string solutionWorkdir, params string[] projects)
        {
            var containerMock = new Mock<ITaefTestAdapterSettingsContainer>();

            var solutionRunSettings = new RunSettings { WorkingDir = solutionWorkdir };
            containerMock.Setup(c => c.SolutionSettings).Returns(solutionRunSettings);

            foreach (string project in projects)
            {
                var projectRunSettings = new RunSettings { WorkingDir = $"{project}_dir" };
                containerMock.Setup(c => c.GetSettingsForTestDll(It.Is<string>(s => s == project))).Returns(projectRunSettings);
            }

            return new SettingsWrapper(containerMock.Object)
            {
                RegexTraitParser = new RegexTraitParser(MockLogger.Object),
                EnvironmentVariablesParser = new EnvironmentVariablesParser(MockLogger.Object),
                HelperFilesCache = new HelperFilesCache(MockLogger.Object)
            };
        }

    }

}
