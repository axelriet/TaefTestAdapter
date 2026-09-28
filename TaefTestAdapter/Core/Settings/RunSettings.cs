// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System.Xml.Serialization;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;

namespace TaefTestAdapter.Settings
{

    /// <summary>
    /// The settings of a <c>&lt;Settings&gt;</c> node of a settings file (XML serializable); settings which are not set
    /// are not serialized.
    /// </summary>
    public class RunSettings : ITaefTestAdapterSettings
    {
        public RunSettings() : this(null) {}

        public RunSettings(string projectRegex)
        {
            ProjectRegex = projectRegex;
        }

        [XmlAttribute]
        public string ProjectRegex { get; set; }

        // General

        public virtual bool? PrintTestOutput { get; set; }
        public bool ShouldSerializePrintTestOutput() { return PrintTestOutput != null; }

        public virtual OutputMode? OutputMode { get; set; }
        public bool ShouldSerializeOutputMode() { return OutputMode != null; }

        public virtual TimestampMode? TimestampMode { get; set; }
        public bool ShouldSerializeTimestampMode() { return TimestampMode != null; }

        public virtual SeverityMode? SeverityMode { get; set; }
        public bool ShouldSerializeSeverityMode() { return SeverityMode != null; }

        public virtual SummaryMode? SummaryMode { get; set; }
        public bool ShouldSerializeSummaryMode() { return SummaryMode != null; }

        public virtual bool? PrefixOutputWithTaef { get; set; }
        public bool ShouldSerializePrefixOutputWithTaef() { return PrefixOutputWithTaef != null; }

        public bool? SkipOriginCheck { get; set; }
        public bool ShouldSerializeSkipOriginCheck() { return SkipOriginCheck != null; }

        // Test discovery

        public virtual string TestDiscoveryRegex { get; set; }
        public bool ShouldSerializeTestDiscoveryRegex() { return TestDiscoveryRegex != null; }

        public virtual int? TestDiscoveryTimeoutInSeconds { get; set; }
        public bool ShouldSerializeTestDiscoveryTimeoutInSeconds() { return TestDiscoveryTimeoutInSeconds != null; }

        public virtual bool? ParseSymbolInformation { get; set; }
        public bool ShouldSerializeParseSymbolInformation() { return ParseSymbolInformation != null; }

        public virtual string TraitsRegexesBefore { get; set; }
        public bool ShouldSerializeTraitsRegexesBefore() { return TraitsRegexesBefore != null; }

        public virtual string TraitsRegexesAfter { get; set; }
        public bool ShouldSerializeTraitsRegexesAfter() { return TraitsRegexesAfter != null; }

        // Test execution

        public virtual string AdditionalPdbs { get; set; }
        public bool ShouldSerializeAdditionalPdbs() { return AdditionalPdbs != null; }

        public virtual string WorkingDir { get; set; }
        public bool ShouldSerializeWorkingDir() { return WorkingDir != null; }

        public virtual string PathExtension { get; set; }
        public bool ShouldSerializePathExtension() { return PathExtension != null; }

        public virtual string EnvironmentVariables { get; set; }
        public bool ShouldSerializeEnvironmentVariables() { return EnvironmentVariables != null; }

        public virtual string AdditionalTestExecutionParam { get; set; }
        public bool ShouldSerializeAdditionalTestExecutionParam() { return AdditionalTestExecutionParam != null; }

        public virtual string BatchForTestSetup { get; set; }
        public bool ShouldSerializeBatchForTestSetup() { return BatchForTestSetup != null; }

        public virtual string BatchForTestTeardown { get; set; }
        public bool ShouldSerializeBatchForTestTeardown() { return BatchForTestTeardown != null; }

        public virtual bool? KillProcessesOnCancel { get; set; }
        public bool ShouldSerializeKillProcessesOnCancel() { return KillProcessesOnCancel != null; }

        public virtual DebuggerKind? DebuggerKind { get; set; }
        public bool ShouldSerializeDebuggerKind() { return DebuggerKind != null; }

        public virtual bool? ParallelTestExecution { get; set; }
        public bool ShouldSerializeParallelTestExecution() { return ParallelTestExecution != null; }

        public virtual int? MaxNrOfThreads { get; set; }
        public bool ShouldSerializeMaxNrOfThreads() { return MaxNrOfThreads != null; }

        public MissingTestsReportMode? MissingTestsReportMode { get; set; }
        public bool ShouldSerializeMissingTestsReportMode() { return MissingTestsReportMode != null; }

        // TAEF

        public virtual string TeExecutable { get; set; }
        public bool ShouldSerializeTeExecutable() { return TeExecutable != null; }

        public virtual bool? RunIgnoredTests { get; set; }
        public bool ShouldSerializeRunIgnoredTests() { return RunIgnoredTests != null; }

        public virtual bool? BreakOnError { get; set; }
        public bool ShouldSerializeBreakOnError() { return BreakOnError != null; }

        public virtual int? NrOfTestRepetitions { get; set; }
        public bool ShouldSerializeNrOfTestRepetitions() { return NrOfTestRepetitions != null; }

        public virtual string TestTimeout { get; set; }
        public bool ShouldSerializeTestTimeout() { return TestTimeout != null; }

        public virtual TaefIsolationLevel? IsolationLevel { get; set; }
        public bool ShouldSerializeIsolationLevel() { return IsolationLevel != null; }

        public virtual bool? RunInProcess { get; set; }
        public bool ShouldSerializeRunInProcess() { return RunInProcess != null; }

        // internal

        public virtual string DebuggingNamedPipeId { get; set; }
        public bool ShouldSerializeDebuggingNamedPipeId() { return DebuggingNamedPipeId != null; }

        public virtual string SolutionDir { get; set; }
        public bool ShouldSerializeSolutionDir() { return SolutionDir != null; }

        public virtual string PlatformName { get; set; }
        public bool ShouldSerializePlatformName() { return PlatformName != null; }

        public virtual string ConfigurationName { get; set; }
        public bool ShouldSerializeConfigurationName() { return ConfigurationName != null; }

    }

}
