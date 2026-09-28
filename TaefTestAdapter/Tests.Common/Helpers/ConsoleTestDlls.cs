// This file has been modified for TAEF support.

using System;

namespace TaefTestAdapter.Tests.Common.Helpers
{
    /// <summary>
    /// The sample test DLLs of the generated end-to-end tests (VsPackage.Tests.Generated\TAEF_Console.csv, column
    /// TestFile).
    /// </summary>
    public static class ConsoleTestDlls
    {
        /// <returns>
        /// The sample test DLL of a key: SampleTests (Tests_taef.dll, Debug x86), SampleTestsX64 (Tests_taef.dll, Release x64),
        /// HardCrashingSampleTests (CrashingTests_taef.dll, Debug x86), HardCrashingSampleTestsX64 (CrashingTests_taef.dll,
        /// Release x64), LoadTests (LoadTests_taef.dll, Release x86), LongRunningTests (LongRunningTests_taef.dll, Release x86),
        /// DllTests (DllTests_taef.dll, Release x86).
        /// </returns>
        public static string GetTestDll(string key)
        {
            switch (key)
            {
                case "SampleTests":
                    return TestResources.Tests_DebugX86;
                case "SampleTestsX64":
                    return TestResources.Tests_ReleaseX64;
                case "HardCrashingSampleTests":
                    return TestResources.CrashingTests_DebugX86;
                case "HardCrashingSampleTestsX64":
                    return TestResources.CrashingTests_ReleaseX64;
                case "LoadTests":
                    return TestResources.LoadTests_ReleaseX86;
                case "LongRunningTests":
                    return TestResources.LongRunningTests_ReleaseX86;
                case "DllTests":
                    return TestResources.DllTests_ReleaseX86;
                default:
                    throw new ArgumentException("Unknown test DLL key: " + key, nameof(key));
            }
        }
    }
}
