// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;

namespace TaefTestAdapter.Settings
{
    /*
    To add a new option, make the following changes:
    - add (nullable!) property to TaefTestAdapter.Settings.ITaefTestAdapterSettings
    - handle property in method TaefTestAdapter.Settings.TaefTestAdapterSettingsExtensions.GetUnsetValuesFrom()
    - add property and according constants to class TaefTestAdapter.Settings.SettingsWrapper
    - handle property serialization in class TaefTestAdapter.Settings.RunSettings
    - add Options UI integration to one of the classes in TaefTestAdapter.VsPackage.OptionsPages.*
    - handle property in method TaefTestAdapter.VsPackage.TaefTestAdapterPackage.GetRunSettingsFromOptionPages()
    - update schema in TestAdapter/TaefTestAdapterSettings.xsd
    - add new option to Resources/AllTestSettings.taef.runsettings
    - add default mock configuration in method TaefTestAdapter.Tests.Common.TestsBase.SetUp()
    */
    /// <summary>
    /// The settings of the adapter (see TestAdapter\TaefTestAdapterSettings.xsd and <see cref="SettingsWrapper"/> for
    /// their meaning and default values); null means "not set".
    /// </summary>
    public interface ITaefTestAdapterSettings
    {
        /// <summary>For project settings: the regex matching the full paths of the test DLLs they apply to.</summary>
        string ProjectRegex { get; set; }

        // General
        /// <summary>Setting PrintTestOutput.</summary>
        bool? PrintTestOutput { get; set; }
        /// <summary>Setting OutputMode.</summary>
        OutputMode? OutputMode { get; set; }
        /// <summary>Setting TimestampMode.</summary>
        TimestampMode? TimestampMode { get; set; }
        /// <summary>Setting SeverityMode.</summary>
        SeverityMode? SeverityMode { get; set; }
        /// <summary>Setting SummaryMode.</summary>
        SummaryMode? SummaryMode { get; set; }
        /// <summary>Setting PrefixOutputWithTaef.</summary>
        bool? PrefixOutputWithTaef { get; set; }
        /// <summary>Setting SkipOriginCheck.</summary>
        bool? SkipOriginCheck { get; set; }

        // Test discovery
        /// <summary>Setting TestDiscoveryRegex.</summary>
        string TestDiscoveryRegex { get; set; }
        /// <summary>Setting TestDiscoveryTimeoutInSeconds.</summary>
        int? TestDiscoveryTimeoutInSeconds { get; set; }
        /// <summary>Setting ParseSymbolInformation.</summary>
        bool? ParseSymbolInformation { get; set; }
        /// <summary>Setting TraitsRegexesBefore.</summary>
        string TraitsRegexesBefore { get; set; }
        /// <summary>Setting TraitsRegexesAfter.</summary>
        string TraitsRegexesAfter { get; set; }

        // Test execution
        /// <summary>Setting AdditionalPdbs.</summary>
        string AdditionalPdbs { get; set; }
        /// <summary>Setting WorkingDir.</summary>
        string WorkingDir { get; set; }
        /// <summary>Setting PathExtension.</summary>
        string PathExtension { get; set; }
        /// <summary>Setting EnvironmentVariables.</summary>
        string EnvironmentVariables { get; set; }
        /// <summary>Setting AdditionalTestExecutionParam.</summary>
        string AdditionalTestExecutionParam { get; set; }
        /// <summary>Setting BatchForTestSetup.</summary>
        string BatchForTestSetup { get; set; }
        /// <summary>Setting BatchForTestTeardown.</summary>
        string BatchForTestTeardown { get; set; }
        /// <summary>Setting KillProcessesOnCancel.</summary>
        bool? KillProcessesOnCancel { get; set; }
        /// <summary>Setting DebuggerKind.</summary>
        DebuggerKind? DebuggerKind { get; set; }
        /// <summary>Setting ParallelTestExecution.</summary>
        bool? ParallelTestExecution { get; set; }
        /// <summary>Setting MaxNrOfThreads.</summary>
        int? MaxNrOfThreads { get; set; }
        /// <summary>Setting MissingTestsReportMode.</summary>
        MissingTestsReportMode? MissingTestsReportMode { get; set; }

        // TAEF
        /// <summary>Setting TeExecutable.</summary>
        string TeExecutable { get; set; }
        /// <summary>Setting RunIgnoredTests.</summary>
        bool? RunIgnoredTests { get; set; }
        /// <summary>Setting BreakOnError.</summary>
        bool? BreakOnError { get; set; }
        /// <summary>Setting NrOfTestRepetitions.</summary>
        int? NrOfTestRepetitions { get; set; }
        /// <summary>Setting TestTimeout.</summary>
        string TestTimeout { get; set; }
        /// <summary>Setting IsolationLevel.</summary>
        TaefIsolationLevel? IsolationLevel { get; set; }
        /// <summary>Setting RunInProcess.</summary>
        bool? RunInProcess { get; set; }

        // internal
        /// <summary>Setting DebuggingNamedPipeId (internal).</summary>
        string DebuggingNamedPipeId { get; set; }
        /// <summary>Setting SolutionDir (internal).</summary>
        string SolutionDir { get; set; }
        /// <summary>Setting PlatformName (internal).</summary>
        string PlatformName { get; set; }
        /// <summary>Setting ConfigurationName (internal).</summary>
        string ConfigurationName { get; set; }
    }

    /// <summary>
    /// Extension methods of <see cref="ITaefTestAdapterSettings"/>.
    /// </summary>
    public static class TaefTestAdapterSettingsExtensions
    {
        /// <summary>Sets all settings of <paramref name="self"/> which are not set to the values of <paramref name="other"/>.</summary>
        public static void GetUnsetValuesFrom(this ITaefTestAdapterSettings self, ITaefTestAdapterSettings other)
        {
            self.PrintTestOutput = self.PrintTestOutput ?? other.PrintTestOutput;
            self.OutputMode = self.OutputMode ?? other.OutputMode;
            self.TimestampMode = self.TimestampMode ?? other.TimestampMode;
            self.SeverityMode = self.SeverityMode ?? other.SeverityMode;
            self.SummaryMode = self.SummaryMode ?? other.SummaryMode;
            self.PrefixOutputWithTaef = self.PrefixOutputWithTaef ?? other.PrefixOutputWithTaef;
            self.SkipOriginCheck = self.SkipOriginCheck ?? other.SkipOriginCheck;

            self.TestDiscoveryRegex = self.TestDiscoveryRegex ?? other.TestDiscoveryRegex;
            self.TestDiscoveryTimeoutInSeconds = self.TestDiscoveryTimeoutInSeconds ?? other.TestDiscoveryTimeoutInSeconds;
            self.ParseSymbolInformation = self.ParseSymbolInformation ?? other.ParseSymbolInformation;
            self.TraitsRegexesBefore = self.TraitsRegexesBefore ?? other.TraitsRegexesBefore;
            self.TraitsRegexesAfter = self.TraitsRegexesAfter ?? other.TraitsRegexesAfter;

            self.AdditionalPdbs = self.AdditionalPdbs ?? other.AdditionalPdbs;
            self.WorkingDir = self.WorkingDir ?? other.WorkingDir;
            self.PathExtension = self.PathExtension ?? other.PathExtension;
            self.EnvironmentVariables = self.EnvironmentVariables ?? other.EnvironmentVariables;
            self.AdditionalTestExecutionParam = self.AdditionalTestExecutionParam ?? other.AdditionalTestExecutionParam;
            self.BatchForTestSetup = self.BatchForTestSetup ?? other.BatchForTestSetup;
            self.BatchForTestTeardown = self.BatchForTestTeardown ?? other.BatchForTestTeardown;
            self.KillProcessesOnCancel = self.KillProcessesOnCancel ?? other.KillProcessesOnCancel;
            self.DebuggerKind = self.DebuggerKind ?? other.DebuggerKind;
            self.ParallelTestExecution = self.ParallelTestExecution ?? other.ParallelTestExecution;
            self.MaxNrOfThreads = self.MaxNrOfThreads ?? other.MaxNrOfThreads;
            self.MissingTestsReportMode = self.MissingTestsReportMode ?? other.MissingTestsReportMode;

            self.TeExecutable = self.TeExecutable ?? other.TeExecutable;
            self.RunIgnoredTests = self.RunIgnoredTests ?? other.RunIgnoredTests;
            self.BreakOnError = self.BreakOnError ?? other.BreakOnError;
            self.NrOfTestRepetitions = self.NrOfTestRepetitions ?? other.NrOfTestRepetitions;
            self.TestTimeout = self.TestTimeout ?? other.TestTimeout;
            self.IsolationLevel = self.IsolationLevel ?? other.IsolationLevel;
            self.RunInProcess = self.RunInProcess ?? other.RunInProcess;

            self.DebuggingNamedPipeId = self.DebuggingNamedPipeId ?? other.DebuggingNamedPipeId;
            self.SolutionDir = self.SolutionDir ?? other.SolutionDir;
            self.PlatformName = self.PlatformName ?? other.PlatformName;
            self.ConfigurationName = self.ConfigurationName ?? other.ConfigurationName;
        }
    }

}
