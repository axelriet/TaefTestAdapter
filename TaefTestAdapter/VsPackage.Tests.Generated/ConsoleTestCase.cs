// This file has been added for TAEF support.

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// An end-to-end test case of <see cref="ConsoleDllTests"/>, i.e. a row of TAEF_Console.csv (see TAEF_Console.pictmodel
    /// for the meaning of the values).
    /// </summary>
    public class ConsoleTestCase
    {
        public const string NoFilter = "none";
        public const string NoSettings = "false";
        public const string DefaultValue = "default";
        public const string DefaultIsolationLevel = "Default";

        /// <summary>Key of a sample test DLL, see <see cref="Tests.Common.Helpers.ConsoleTestDlls.GetTestDll"/>.</summary>
        public string TestFile { get; set; }

        /// <summary>false (no settings), true (Project.runsettings), Solution or SolutionProject.</summary>
        public string Settings { get; set; } = NoSettings;

        /// <summary>vstest.console test case filter, or "none"; a leading * marks a filter with a non-existing property.</summary>
        public string TestCaseFilter { get; set; } = NoFilter;

        public bool EnableCodeCoverage { get; set; }
        public bool InIsolation { get; set; }

        /// <summary>If true, setting RunInProcess is set to true (else, it is not set).</summary>
        public bool RunInProcess { get; set; }

        /// <summary>true or false (setting RunIgnoredTests is overridden), or "default" (not overridden).</summary>
        public string RunIgnoredTests { get; set; } = DefaultValue;

        /// <summary>A value of setting IsolationLevel, or "Default" (not overridden).</summary>
        public string IsolationLevel { get; set; } = DefaultIsolationLevel;

        /// <summary>Value of setting NrOfTestRepetitions (1: not overridden).</summary>
        public int NrOfTestRepetitions { get; set; } = 1;

        /// <returns>True if one of the adapter settings is overridden by this test case.</returns>
        public bool OverridesSettings =>
            RunInProcess || RunIgnoredTests != DefaultValue || IsolationLevel != DefaultIsolationLevel || NrOfTestRepetitions != 1;

        public override string ToString()
        {
            return $"{TestFile}, settings: {Settings}, filter: {TestCaseFilter}, coverage: {EnableCodeCoverage}, isolation: {InIsolation}, " +
                   $"in process: {RunInProcess}, ignored tests: {RunIgnoredTests}, isolation level: {IsolationLevel}, repetitions: {NrOfTestRepetitions}";
        }
    }
}
