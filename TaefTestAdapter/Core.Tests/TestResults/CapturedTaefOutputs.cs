// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestCases;
using TaefTestAdapter.Tests.Common;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Access to the TE.exe outputs captured from the sample test DLLs (Tests.Common\Resources\TestData\TaefOutput, see the
    /// README.md there) and to the test cases they belong to.
    /// </summary>
    public static class CapturedTaefOutputs
    {
        /// <summary>The test DLL the outputs of Tests_taef.dll have been captured with (Debug, x64).</summary>
        public const string TestsDll = @"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\Tests_taef.dll";

        public const string TestsListProperties = "Tests_taef.dll.listProperties.txt";
        public const string TestsRun = "Tests_taef.dll.run.txt";
        public const string TestsRunInProcess = "Tests_taef.dll.runInProcess.txt";
        public const string TestsRunWithoutSettings = "Tests_taef.dll.runWithoutSettings.txt";
        public const string TestsRunIgnored = "Tests_taef.dll.runIgnored.txt";
        public const string TestsRunSelected = "Tests_taef.dll.runSelected.txt";
        public const string TestsRunNoMatch = "Tests_taef.dll.runNoMatch.txt";
        public const string CrashingTestsRun = "CrashingTests_taef.dll.run.txt";
        public const string CrashingTestsRunInProcess = "CrashingTests_taef.dll.runInProcess.txt";
        public const string LeakCheckTestsRun = "LeakCheckTests_taef.dll.run.txt";
        public const string DllTestsRun = "DllTests_taef.dll.run.txt";
        public const string DllTestsRunLoop = "DllTests_taef.dll.runLoop.txt";
        public const string DllTestsRunWithoutDependency = "DllTests_taef.dll.runWithoutDependency.txt";
        public const string LongRunningTestsRunTimeout = "LongRunningTests_taef.dll.runTimeout.txt";
        public const string ErrorSelectSyntaxError = "Error.SelectSyntaxError.txt";
        public const string ErrorNoMatchingTests = "Error.NoMatchingTests.txt";
        public const string ErrorNoTestFiles = "Error.NoTestFiles.txt";
        public const string ErrorNotATestDll = "Error.NotATestDll.txt";

        public static string[] ReadLines(string fileName) => TestResources.ReadTaefOutputLines(fileName);

        /// <returns>
        /// All <see cref="TestResources.NrOfTests"/> test cases of Tests_taef.dll as the adapter creates them from the
        /// captured <c>/listProperties</c> output (traits incl. Ignore, meta data; no source locations, i.e.
        /// <paramref name="settings"/> must return false for ParseSymbolInformation).
        /// </returns>
        public static IList<TestCase> GetTestCasesOfTestsDll(SettingsWrapper settings, ILogger logger)
        {
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser().ParseListPropertiesOutput(ReadLines(TestsListProperties));
            return new TestCaseFactory(TestsDll, logger, settings, null, null).CreateTestCasesFromDescriptors(descriptors);
        }

        /// <returns>Test cases (without meta data) of <paramref name="testDll"/> for the given TAEF names.</returns>
        public static IList<TestCase> ToTestCases(string testDll, params string[] names)
            => names.Select(n => new TestCase(n, testDll, n, "", 0)).ToList();
    }

}
