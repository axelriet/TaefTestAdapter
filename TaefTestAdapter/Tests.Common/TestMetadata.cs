// This file has been modified for TAEF support.

using System;
using TaefTestAdapter.TestAdapter.Framework;

namespace TaefTestAdapter.Tests.Common
{
    public static class TestMetadata
    {
        /// <summary>If true, golden files of end-to-end tests are overwritten by the actual results.</summary>
        public const bool OverwriteTestResults = false;

        /// <summary>
        /// The Visual Studio version whose vstest.console.exe is used by end-to-end tests if available
        /// (see <see cref="TestResources.GetVsTestConsolePath()"/>).
        /// </summary>
        public const VsVersion VersionUnderTest = VsVersion.VS2026;

        /// <summary>
        /// Test categories. <see cref="Unit"/> tests neither need built sample DLLs nor start processes (except where
        /// noted); <see cref="Integration"/> tests use the sample test DLLs and TE.exe; <see cref="EndToEnd"/> tests run
        /// vstest.console.exe; <see cref="Load"/> tests measure performance with LoadTests_taef.dll.
        /// </summary>
        public static class TestCategories
        {
            public const string Unit = "Unit";
            public const string Integration = "Integration";
            public const string EndToEnd = "End to end";
            public const string Ui = "UI";
            public const string Load = "Load";
        }

        public static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(25);
        public static readonly int ToleranceInMs = (int)Math.Ceiling(Tolerance.TotalMilliseconds);
    }
}
