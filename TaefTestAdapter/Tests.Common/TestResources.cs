// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using TaefTestAdapter.TestAdapter.Framework;

namespace TaefTestAdapter.Tests.Common
{
    /// <summary>
    /// The architecture/configuration combinations the sample test DLLs are built for
    /// (output folders <c>Debug</c>, <c>Release</c>, <c>Debug-x64</c> and <c>Release-x64</c>).
    /// </summary>
    public enum SampleConfiguration { DebugX86, ReleaseX86, DebugX64, ReleaseX64 }

    /// <summary>
    /// Locations of all files the tests need (sample test DLLs, helper executables, test data, TE.exe, vstest.console.exe)
    /// and facts about the sample test DLLs (numbers of tests and outcomes).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Locations are computed from build information embedded into this assembly at build time (enlistment root, OutRoot,
    /// output folder and configuration, see Tests.Common.csproj), so they do neither depend on the current directory nor on
    /// the default output folder: tests of an isolated build (<c>/p:OutRoot=...</c>) use the helper executables of that build.
    /// </para>
    /// <para>
    /// Sample test DLLs are taken from the folder given by environment variable <see cref="SamplesDirEnvVariable"/>
    /// (a folder containing <c>Debug</c>, <c>Release</c>, <c>Debug-x64</c> and <c>Release-x64</c>), else from
    /// <c>$(OutRoot)binaries\SampleTests\</c> if the samples have been built into the OutRoot of this build, else from
    /// <c>&lt;enlistment&gt;\out\binaries\SampleTests\</c>.
    /// </para>
    /// <para>
    /// The expected numbers are those of the sample DLLs of <c>SampleTests\SampleTests.sln</c> as documented in
    /// <c>SampleTests\README.md</c>; they are identical for all four configurations unless noted otherwise.
    /// </para>
    /// </remarks>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class TestResources
    {
        #region Build information and folders

        /// <summary>Environment variable overriding the root folder of the sample test DLLs (<see cref="SampleTestsBuildDir"/>).</summary>
        public const string SamplesDirEnvVariable = "TAEF_ADAPTER_SAMPLES_DIR";

        /// <summary>Environment variable overriding the path of vstest.console.exe (<see cref="GetVsTestConsolePath()"/>).</summary>
        public const string VsTestConsoleEnvVariable = "TAEF_ADAPTER_VSTEST_CONSOLE";

        /// <summary>Environment variable overriding the TAEF runtime folder (containing x86\TE.exe, x64\TE.exe, ...).</summary>
        public const string TaefRuntimesDirEnvVariable = "TAEF_ADAPTER_TAEF_RUNTIMES_DIR";

        private const string MetadataPrefix = "TaefTestAdapter.Tests.";

#if DEBUG
        public const string BuildConfig = "Debug";
#else
        public const string BuildConfig = "Release";
#endif

        private static readonly Lazy<IDictionary<string, string>> BuildInfo = new Lazy<IDictionary<string, string>>(() =>
            typeof(TestResources).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key.StartsWith(MetadataPrefix, StringComparison.Ordinal))
                .ToDictionary(a => a.Key.Substring(MetadataPrefix.Length), a => a.Value));

        private static string GetBuildInfo(string key)
        {
            if (!BuildInfo.Value.TryGetValue(key, out string value))
                throw new InvalidOperationException($"Build information '{MetadataPrefix}{key}' is missing in assembly {typeof(TestResources).Assembly.Location} - was Tests.Common built with Tests.Common.csproj?");
            return value;
        }

        private static string WithTrailingSeparator(string directory)
        {
            directory = Path.GetFullPath(directory);
            return directory.EndsWith(@"\", StringComparison.Ordinal) ? directory : directory + @"\";
        }

        /// <summary>Root folder of the enlistment (with trailing backslash), e.g. <c>C:\src\TAEF-Test-Adapter\</c>.</summary>
        public static string EnlistmentRoot { get; } = WithTrailingSeparator(GetBuildInfo("EnlistmentRoot"));

        /// <summary>Root folder of all build outputs of this build (with trailing backslash), default <c>&lt;enlistment&gt;\out\</c>.</summary>
        public static string OutRoot { get; } = WithTrailingSeparator(GetBuildInfo("OutRoot"));

        /// <summary>Folder of <c>TaefTestAdapter.sln</c>.</summary>
        public static string AdapterSolutionDir { get; } = EnlistmentRoot + @"TaefTestAdapter\";

        /// <summary>Folder of <c>SampleTests.sln</c> (the "solution dir" of the sample tests, used for <c>$(SolutionDir)</c>).</summary>
        public static string SampleTestsSolutionDir { get; } = EnlistmentRoot + @"SampleTests\";

        public static string SampleTestsSolutionFile { get; } = SampleTestsSolutionDir + "SampleTests.sln";

        /// <summary>
        /// Build output folder of the adapter's projects for the current configuration, e.g.
        /// <c>&lt;OutRoot&gt;binaries\TaefTestAdapter\Debug\</c> (each project has its own sub folder).
        /// </summary>
        public static string TaefTestAdapterBuildDir { get; } =
            WithTrailingSeparator(Path.GetDirectoryName(GetBuildInfo("TestsCommonOutDir").TrimEnd('\\')));

        /// <summary>
        /// Root folder of the sample test DLLs, containing <c>Debug</c>, <c>Release</c>, <c>Debug-x64</c> and
        /// <c>Release-x64</c> (see class comment for how it is determined).
        /// </summary>
        public static string SampleTestsBuildDir => LazySampleTestsBuildDir.Value;
        private static readonly Lazy<string> LazySampleTestsBuildDir = new Lazy<string>(FindSampleTestsBuildDir);

        /// <summary>Folder containing the test data files of Tests.Common (<c>Resources\TestData</c>).</summary>
        public static string TestdataDir => LazyTestdataDir.Value;
        private static readonly Lazy<string> LazyTestdataDir = new Lazy<string>(FindTestdataDir);

        /// <summary>Folder containing TE.exe outputs of the sample DLLs captured for parser tests (see README.md in that folder).</summary>
        public static string TaefOutputDir => TestdataDir + @"TaefOutput\";

        /// <summary>Output folder of the TestAdapter project, usable as vstest.console /TestAdapterPath.</summary>
        public static string TestAdapterDir => TaefTestAdapterBuildDir + @"TestAdapter\";

        /// <summary>Output folder of the Packaging project (VSIX, nupkg and the adapter's DLLs).</summary>
        public static string PackagingDir => TaefTestAdapterBuildDir + @"Packaging\";

        private static string FindSampleTestsBuildDir()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable(SamplesDirEnvVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return WithTrailingSeparator(fromEnvironment.Trim().Trim('"'));

            var candidates = new[]
            {
                OutRoot + @"binaries\SampleTests\",
                EnlistmentRoot + @"out\binaries\SampleTests\"
            };
            return candidates.FirstOrDefault(ContainsSampleTests) ?? candidates.Last();
        }

        private static bool ContainsSampleTests(string directory)
        {
            return GetConfigurationDirectoryNames().Any(d => File.Exists(Path.Combine(directory, d, TestsDll)));
        }

        private static string FindTestdataDir()
        {
            string assemblyDir = Path.GetDirectoryName(typeof(TestResources).Assembly.Location) ?? "";
            var candidates = new[]
            {
                Path.Combine(assemblyDir, @"Resources\TestData\"),
                TaefTestAdapterBuildDir + @"Tests.Common\Resources\TestData\",
                AdapterSolutionDir + @"Tests.Common\Resources\TestData\"
            };
            return WithTrailingSeparator(candidates.FirstOrDefault(Directory.Exists) ?? candidates.First());
        }

        #endregion

        #region Helper executables (built by TaefTestAdapter.sln)

        /// <summary>.NET exe printing "Waiting for 10 seconds", 10 dots and "done." within 10 s (e.g. a slow fake TE.exe).</summary>
        public static string TenSecondsWaiter => TaefTestAdapterBuildDir + @"TenSecondsWaiter\TenSecondsWaiter.exe";
        /// <summary>Native exe printing "Test output before crashing" and crashing with an access violation (0xC0000005).</summary>
        public static string AlwaysCrashingExe => TaefTestAdapterBuildDir + @"CrashingExe\CrashingExe.exe";
        /// <summary>Native exe printing "Test output before exiting with 4711" and returning 4711.</summary>
        public static string AlwaysFailingExe => TaefTestAdapterBuildDir + @"FailingExe\FailingExe.exe";
        /// <summary>Native exe writing "SemaphoreExe.sem" into its working directory and returning 143.</summary>
        public static string SemaphoreExe => TaefTestAdapterBuildDir + @"SemaphoreExe\SemaphoreExe.exe";

        public const int AlwaysFailingExeExitCode = 4711;
        public const int SemaphoreExeExitCode = 143;
        public const string SemaphoreExeSemaphoreFile = "SemaphoreExe.sem";

        #endregion

        #region Sample test DLLs

        public const string TestsDll = "Tests_taef.dll";
        public const string CrashingTestsDll = "CrashingTests_taef.dll";
        public const string LoadTestsDll = "LoadTests_taef.dll";
        public const string LongRunningTestsDll = "LongRunningTests_taef.dll";
        public const string DllTestsDll = "DllTests_taef.dll";
        /// <summary>Plain native DLL (no TAEF test DLL) DllTests_taef.dll depends on.</summary>
        public const string DllProjectDll = "DllProject.dll";
        public const string LeakCheckTestsDll = "LeakCheckTests_taef.dll";
        public const string HelperFileTestsDll = "HelperFileTests_taef.dll";
        public const string ClrTestsDll = "ClrTests_taef.dll";
        /// <summary>Managed DLL (no TAEF test DLL) ClrTests_taef.dll depends on.</summary>
        public const string ClrDotNetLibProjectDll = "ClrDotNetLibProject.dll";

        /// <summary>All sample TAEF test DLLs (file names).</summary>
        public static readonly IReadOnlyList<string> AllSampleTestDlls = new[]
        {
            TestsDll, CrashingTestsDll, LoadTestsDll, LongRunningTestsDll, DllTestsDll, LeakCheckTestsDll, HelperFileTestsDll, ClrTestsDll
        };

        /// <summary>DLLs in the sample output folders which are no TAEF test DLLs (discovery must skip them silently).</summary>
        public static readonly IReadOnlyList<string> NonTaefSampleDlls = new[] { DllProjectDll, ClrDotNetLibProjectDll };

        public static readonly IReadOnlyList<SampleConfiguration> AllSampleConfigurations = new[]
        {
            SampleConfiguration.DebugX86, SampleConfiguration.ReleaseX86, SampleConfiguration.DebugX64, SampleConfiguration.ReleaseX64
        };

        /// <returns>Name of the output folder of the configuration: Debug, Release, Debug-x64 or Release-x64.</returns>
        public static string GetConfigurationDirectoryName(this SampleConfiguration configuration)
        {
            switch (configuration)
            {
                case SampleConfiguration.DebugX86: return "Debug";
                case SampleConfiguration.ReleaseX86: return "Release";
                case SampleConfiguration.DebugX64: return "Debug-x64";
                case SampleConfiguration.ReleaseX64: return "Release-x64";
                default: throw new ArgumentOutOfRangeException(nameof(configuration), configuration, null);
            }
        }

        private static IEnumerable<string> GetConfigurationDirectoryNames()
            => new[] { "Debug", "Release", "Debug-x64", "Release-x64" };

        /// <returns>"x86" or "x64" (the TAEF architecture names as used by TaefLocator).</returns>
        public static string GetArchitecture(this SampleConfiguration configuration)
            => configuration == SampleConfiguration.DebugX86 || configuration == SampleConfiguration.ReleaseX86 ? "x86" : "x64";

        public static bool IsDebug(this SampleConfiguration configuration)
            => configuration == SampleConfiguration.DebugX86 || configuration == SampleConfiguration.DebugX64;

        /// <returns>The folder containing the sample DLLs of <paramref name="configuration"/> (with trailing backslash).</returns>
        public static string GetSampleDirectory(SampleConfiguration configuration)
            => SampleTestsBuildDir + configuration.GetConfigurationDirectoryName() + @"\";

        /// <returns>Full path of the sample file <paramref name="fileName"/> (e.g. <see cref="TestsDll"/>) of <paramref name="configuration"/>.</returns>
        public static string GetSampleDll(string fileName, SampleConfiguration configuration)
            => GetSampleDirectory(configuration) + fileName;

        /// <returns>The configuration of a sample DLL path (derived from its folder name), or null.</returns>
        public static SampleConfiguration? GetSampleConfiguration(string sampleDll)
        {
            string folder = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(sampleDll)));
            foreach (SampleConfiguration configuration in AllSampleConfigurations)
            {
                if (string.Equals(folder, configuration.GetConfigurationDirectoryName(), StringComparison.OrdinalIgnoreCase))
                    return configuration;
            }
            return null;
        }

        // Tests_taef.dll: main sample (C++17, no TAEF exports)
        public static string Tests_DebugX86 => GetSampleDll(TestsDll, SampleConfiguration.DebugX86);
        public static string Tests_ReleaseX86 => GetSampleDll(TestsDll, SampleConfiguration.ReleaseX86);
        public static string Tests_DebugX64 => GetSampleDll(TestsDll, SampleConfiguration.DebugX64);
        public static string Tests_ReleaseX64 => GetSampleDll(TestsDll, SampleConfiguration.ReleaseX64);

        public static string CrashingTests_DebugX86 => GetSampleDll(CrashingTestsDll, SampleConfiguration.DebugX86);
        public static string CrashingTests_ReleaseX86 => GetSampleDll(CrashingTestsDll, SampleConfiguration.ReleaseX86);
        public static string CrashingTests_DebugX64 => GetSampleDll(CrashingTestsDll, SampleConfiguration.DebugX64);
        public static string CrashingTests_ReleaseX64 => GetSampleDll(CrashingTestsDll, SampleConfiguration.ReleaseX64);

        public static string LoadTests_DebugX86 => GetSampleDll(LoadTestsDll, SampleConfiguration.DebugX86);
        public static string LoadTests_ReleaseX86 => GetSampleDll(LoadTestsDll, SampleConfiguration.ReleaseX86);
        public static string LoadTests_DebugX64 => GetSampleDll(LoadTestsDll, SampleConfiguration.DebugX64);
        public static string LoadTests_ReleaseX64 => GetSampleDll(LoadTestsDll, SampleConfiguration.ReleaseX64);

        public static string LongRunningTests_DebugX86 => GetSampleDll(LongRunningTestsDll, SampleConfiguration.DebugX86);
        public static string LongRunningTests_ReleaseX86 => GetSampleDll(LongRunningTestsDll, SampleConfiguration.ReleaseX86);
        public static string LongRunningTests_DebugX64 => GetSampleDll(LongRunningTestsDll, SampleConfiguration.DebugX64);
        public static string LongRunningTests_ReleaseX64 => GetSampleDll(LongRunningTestsDll, SampleConfiguration.ReleaseX64);

        public static string DllTests_DebugX86 => GetSampleDll(DllTestsDll, SampleConfiguration.DebugX86);
        public static string DllTests_ReleaseX86 => GetSampleDll(DllTestsDll, SampleConfiguration.ReleaseX86);
        public static string DllTests_DebugX64 => GetSampleDll(DllTestsDll, SampleConfiguration.DebugX64);
        public static string DllTests_ReleaseX64 => GetSampleDll(DllTestsDll, SampleConfiguration.ReleaseX64);
        public static string DllTestsDll_DebugX86 => GetSampleDll(DllProjectDll, SampleConfiguration.DebugX86);
        public static string DllTestsDll_ReleaseX86 => GetSampleDll(DllProjectDll, SampleConfiguration.ReleaseX86);
        public static string DllTestsDll_DebugX64 => GetSampleDll(DllProjectDll, SampleConfiguration.DebugX64);
        public static string DllTestsDll_ReleaseX64 => GetSampleDll(DllProjectDll, SampleConfiguration.ReleaseX64);

        public static string LeakCheckTests_DebugX86 => GetSampleDll(LeakCheckTestsDll, SampleConfiguration.DebugX86);
        public static string LeakCheckTests_ReleaseX86 => GetSampleDll(LeakCheckTestsDll, SampleConfiguration.ReleaseX86);
        public static string LeakCheckTests_DebugX64 => GetSampleDll(LeakCheckTestsDll, SampleConfiguration.DebugX64);
        public static string LeakCheckTests_ReleaseX64 => GetSampleDll(LeakCheckTestsDll, SampleConfiguration.ReleaseX64);

        public static string HelperFileTests_DebugX86 => GetSampleDll(HelperFileTestsDll, SampleConfiguration.DebugX86);
        public static string HelperFileTests_ReleaseX86 => GetSampleDll(HelperFileTestsDll, SampleConfiguration.ReleaseX86);
        public static string HelperFileTests_DebugX64 => GetSampleDll(HelperFileTestsDll, SampleConfiguration.DebugX64);
        public static string HelperFileTests_ReleaseX64 => GetSampleDll(HelperFileTestsDll, SampleConfiguration.ReleaseX64);
        /// <summary>Old (misspelled) name of <see cref="HelperFileTests_ReleaseX86"/>.</summary>
        public static string HelperFilesTests_ReleaseX86 => HelperFileTests_ReleaseX86;

        public static string ClrTests_DebugX86 => GetSampleDll(ClrTestsDll, SampleConfiguration.DebugX86);
        public static string ClrTests_ReleaseX86 => GetSampleDll(ClrTestsDll, SampleConfiguration.ReleaseX86);
        public static string ClrTests_DebugX64 => GetSampleDll(ClrTestsDll, SampleConfiguration.DebugX64);
        public static string ClrTests_ReleaseX64 => GetSampleDll(ClrTestsDll, SampleConfiguration.ReleaseX64);

        /// <summary>Files next to a sample test DLL which it needs at runtime (copied by <see cref="SampleCopy"/>).</summary>
        public static IReadOnlyList<string> GetRuntimeDependencies(string sampleDllFileName)
        {
            switch (Path.GetFileName(sampleDllFileName)?.ToLowerInvariant())
            {
                case "dlltests_taef.dll":
                    return new[] { DllProjectDll };
                case "clrtests_taef.dll":
                    return new[] { ClrDotNetLibProjectDll };
                default:
                    return new string[0];
            }
        }

        #endregion

        #region Facts about the sample test DLLs (identical for all configurations unless noted otherwise)

        // Tests_taef.dll (TE.exe outcomes with the settings of SampleTests.taef.runsettings, see SampleTests\README.md)
        /// <summary>Number of tests discovered by the adapter (TE.exe /listProperties /runIgnoredTests), including the ignored ones.</summary>
        public const int NrOfTests = 141;
        /// <summary>Tests with metadata Ignore=true: TaefSamples::ClassAndMethodProperties::IgnoredTest, TaefSamples::IgnoredClass::IgnoredPassing, TaefSamples::IgnoredClass::IgnoredFailing.</summary>
        public const int NrOfIgnoredTests = 3;
        /// <summary>Tests run by TE.exe without /runIgnoredTests.</summary>
        public const int NrOfNotIgnoredTests = NrOfTests - NrOfIgnoredTests;
        public const int NrOfPassingTests = 69;
        public const int NrOfFailingTests = 62;
        public const int NrOfBlockedTests = 5;
        public const int NrOfSkippedTests = 1;
        public const int NrOfNotRunTests = 1;
        /// <summary>Outcomes of the ignored tests if they are run (/runIgnoredTests).</summary>
        public const int NrOfPassingIgnoredTests = 2;
        public const int NrOfFailingIgnoredTests = 1;
        /// <summary>
        /// Tests which only pass with the settings of SampleTests.taef.runsettings (TaefSamples::RuntimeParameterTests::TestDirectoryIsSet,
        /// TaefSamples::WorkingDir::IsSolutionDirectory, TaefSamples::EnvironmentVariable::IsSet): without them, TE.exe reports 66 Passed and 65 Failed.
        /// </summary>
        public const int NrOfTestsNeedingSampleSettings = 3;
        public const int NrOfPassingTestsWithoutSampleSettings = NrOfPassingTests - NrOfTestsNeedingSampleSettings;
        public const int NrOfFailingTestsWithoutSampleSettings = NrOfFailingTests + NrOfTestsNeedingSampleSettings;

        // Tests_taef.dll: outcomes as reported by the adapter (Blocked -> Failed, NotRun -> None, ignored tests -> Skipped
        // unless RunIgnoredTests), with the sample settings
        public const int NrOfTestsReportedAsPassed = NrOfPassingTests;
        public const int NrOfTestsReportedAsFailed = NrOfFailingTests + NrOfBlockedTests;
        public const int NrOfTestsReportedAsSkipped = NrOfSkippedTests + NrOfIgnoredTests;
        public const int NrOfTestsReportedAsNone = NrOfNotRunTests;

        // CrashingTests_taef.dll (class Crashing): 4 Passed, 2 Failed, 3 crashes (reported as Failed) out of process;
        // with /inproc TE.exe dies in TaefSamples::Crashing::TheCrash (1 Passed, 1 Failed, the crash, 6 tests never started)
        public const int NrOfCrashingTests = 9;
        public const int NrOfCrashingTestsPassing = 4;
        public const int NrOfCrashingTestsFailing = 2;
        public const int NrOfCrashingTestsCrashing = 3;
        public const int NrOfCrashingTestsPassingInProcess = 1;
        public const int NrOfCrashingTestsFailingInProcess = 1;
        public const int NrOfCrashingTestsNotStartedInProcess = 6;

        // LoadTests_taef.dll: TaefSamples::LoadTests::Test0 ... TaefSamples::LoadTests::Test4999, odd numbers pass
        public const int NrOfLoadTests = 5000;
        public const int NrOfPassingLoadTests = 2500;
        public const int NrOfFailingLoadTests = 2500;

        // LongRunningTests_taef.dll: TaefSamples::LongRunningTests::Test1 (passes), TaefSamples::LongRunningTests::Test2 (fails), 2 s each
        public const int NrOfLongRunningTests = 2;
        public const int LongRunningTestDurationInMs = 2000;

        // DllTests_taef.dll: TaefSamples::Passing::InvokeFunction, TaefSamples::Failing::InvokeFunction (Blocked if DllProject.dll can not be found)
        public const int NrOfDllTests = 2;

        // LeakCheckTests_taef.dll: TaefSamples::MemoryLeaks::{passing, failing, PassingAndLeaking, FailingAndLeaking}
        public const int NrOfLeakCheckTests = 4;
        public const int NrOfPassingLeakCheckTestsDebug = 1;
        public const int NrOfFailingLeakCheckTestsDebug = 3;
        public const int NrOfPassingLeakCheckTestsRelease = 2;
        public const int NrOfFailingLeakCheckTestsRelease = 2;

        // HelperFileTests_taef.dll: TaefSamples::HelperFileTests::TheTargetIsSet (passes only with /p:"TheTarget=$(TheTarget)")
        public const int NrOfHelperFileTests = 1;

        // ClrTests_taef.dll: TaefSamples::ClrTests::Pass, TaefSamples::ClrTests::Fail
        public const int NrOfClrTests = 2;

        /// <summary>Number of tests of all sample test DLLs of one configuration.</summary>
        public const int NrOfAllSampleTests = NrOfTests + NrOfCrashingTests + NrOfLoadTests + NrOfLongRunningTests + NrOfDllTests
                                              + NrOfLeakCheckTests + NrOfHelperFileTests + NrOfClrTests;

        #endregion

        #region Names of well-known sample tests

        /// <summary>Well known tests of the sample test DLLs (TAEF names).</summary>
        public static class TestNames
        {
            /// <summary>
            /// Root namespace of all test classes of the sample test DLLs: the TAEF name of every sample test starts with
            /// <c>TaefSamples::</c> (VS fully qualified name: <c>TaefSamples.</c>).
            /// </summary>
            public const string SampleNamespace = "TaefSamples";

            // Tests_taef.dll
            public const string TestMathAddFails = "TaefSamples::TestMath::AddFails";
            public const string TestMathAddPasses = "TaefSamples::TestMath::AddPasses";
            public const string TestMathAddPassesWithTraits = "TaefSamples::TestMath::AddPassesWithTraits";
            public const string TestDirectoryIsSet = "TaefSamples::RuntimeParameterTests::TestDirectoryIsSet";
            public const string WorkingDirIsSolutionDirectory = "TaefSamples::WorkingDir::IsSolutionDirectory";
            public const string EnvironmentVariableIsSet = "TaefSamples::EnvironmentVariable::IsSet";
            public const string SkippedByTest = "TaefSamples::ExplicitResults::SkippedByTest";
            public const string BlockedByTest = "TaefSamples::ExplicitResults::BlockedByTest";
            public const string NotRunByTest = "TaefSamples::ExplicitResults::NotRunByTest";
            public const string IgnoredTest = "TaefSamples::ClassAndMethodProperties::IgnoredTest";
            public const string IgnoredPassing = "TaefSamples::IgnoredClass::IgnoredPassing";
            public const string IgnoredFailing = "TaefSamples::IgnoredClass::IgnoredFailing";
            public const string MissingDataSource = "TaefSamples::MissingDataSource::Test#error";
            public const string SimpleDataRow0 = "TaefSamples::TableDataTests::Simple#0";
            public const string LightweightDataRow0 = "TaefSamples::LightweightDataTests::SingleValue#metadataSet0";
            public const string RowWithQuote = "TaefSamples::NamedRows::SpecialCharacters#with'quote";
            public const string RowWithColons = "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]";
            public const string TemplateTest = "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate";
            public const string AnonymousNamespaceTest = "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test";
            public const string UmlautTest = "TaefSamples::Ümlautß::Täst";

            // CrashingTests_taef.dll
            public const string CrashingAddFailsBeforeCrash = "TaefSamples::Crashing::AddFailsBeforeCrash";
            public const string CrashingAddPassesBeforeCrash = "TaefSamples::Crashing::AddPassesBeforeCrash";
            public const string CrashingTheCrash = "TaefSamples::Crashing::TheCrash";
            public const string CrashingAddFailsAfterCrash = "TaefSamples::Crashing::AddFailsAfterCrash";
            public const string CrashingAddPassesAfterCrash = "TaefSamples::Crashing::AddPassesAfterCrash";
            public const string CrashingLongRunning = "TaefSamples::Crashing::LongRunning";
            public const string CrashingTheAbort = "TaefSamples::Crashing::TheAbort";
            public const string CrashingTheStackOverflow = "TaefSamples::Crashing::TheStackOverflow";
            public const string CrashingAddPassesAfterAllCrashes = "TaefSamples::Crashing::AddPassesAfterAllCrashes";

            // LongRunningTests_taef.dll
            public const string LongRunningTest1 = "TaefSamples::LongRunningTests::Test1";
            public const string LongRunningTest2 = "TaefSamples::LongRunningTests::Test2";

            // DllTests_taef.dll
            public const string DllTestsPassing = "TaefSamples::Passing::InvokeFunction";
            public const string DllTestsFailing = "TaefSamples::Failing::InvokeFunction";

            // LeakCheckTests_taef.dll
            public const string LeakCheckPassing = "TaefSamples::MemoryLeaks::Passing";
            public const string LeakCheckFailing = "TaefSamples::MemoryLeaks::Failing";
            public const string LeakCheckPassingAndLeaking = "TaefSamples::MemoryLeaks::PassingAndLeaking";
            public const string LeakCheckFailingAndLeaking = "TaefSamples::MemoryLeaks::FailingAndLeaking";

            // HelperFileTests_taef.dll
            public const string HelperFileTheTargetIsSet = "TaefSamples::HelperFileTests::TheTargetIsSet";

            // ClrTests_taef.dll
            public const string ClrTestsPass = "TaefSamples::ClrTests::Pass";
            public const string ClrTestsFail = "TaefSamples::ClrTests::Fail";

            /// <returns>Name of test <paramref name="i"/> (0..4999) of LoadTests_taef.dll.</returns>
            public static string LoadTest(int i) => $"TaefSamples::LoadTests::Test{i}";
        }

        #endregion

        #region Settings and test data files

        /// <summary>Batch files of the samples, relative to <see cref="SampleTestsSolutionDir"/> (use as "$(SolutionDir)" + ...).</summary>
        public const string SucceedingBatch = @"Tests\Returns0.bat";
        public const string FailingBatch = @"Tests\Returns1.bat";

        /// <summary>Solution settings of the samples (found automatically by the adapter next to SampleTests.sln).</summary>
        public static string SampleTestsSolutionSettings => SampleTestsSolutionDir + "SampleTests.taef.runsettings";
        /// <summary>User settings without adapter settings.</summary>
        public static string SampleTestsNoSettings => SampleTestsSolutionDir + "No.runsettings";
        /// <summary>User settings with repetitions, parallel execution and isolation level (non-deterministic order of results).</summary>
        public static string SampleTestsNonDeterministicSettings => SampleTestsSolutionDir + "NonDeterministic.runsettings";
        /// <summary>All user settings with their default values (Resources\AllTestSettings.taef.runsettings of the adapter).</summary>
        public static string AllTestSettings => AdapterSolutionDir + @"Resources\AllTestSettings.taef.runsettings";
        /// <summary>The XSD of the adapter's settings.</summary>
        public static string SettingsXsd => AdapterSolutionDir + @"TestAdapter\TaefTestAdapterSettings.xsd";

        /// <summary>Broken XML file (e.g. an invalid settings file).</summary>
        public static string XmlFileBroken => TestdataDir + "Broken.xml";

        /// <summary>Solution settings: BatchForTestSetup "Solution", NrOfTestRepetitions 2, IsolationLevel Class, TraitsRegexesBefore "Solution///A,B".</summary>
        public static string SolutionTestSettings => TestdataDir + @"RunSettingsServiceTests\Solution.taef.runsettings";
        /// <summary>
        /// User settings: solution: RunIgnoredTests true, MaxNrOfThreads 3, TestTimeout 0:0:3, TraitsRegexesBefore "User///A,B",
        /// SkipOriginCheck true; one project (ProjectRegex "LoadTests_taef\.dll|CrashingTests_taef\.dll"): MaxNrOfThreads 4,
        /// SkipOriginCheck true.
        /// </summary>
        public static string UserTestSettings => TestdataDir + @"RunSettingsServiceTests\User.runsettings";
        /// <summary>An XML file without RunSettings node.</summary>
        public static string UserTestSettingsWithoutRunSettingsNode => TestdataDir + @"RunSettingsServiceTests\User_WithoutRunSettingsNode.runsettings";
        /// <summary>Same content as <see cref="SolutionTestSettings"/> (settings delivered by a settings provider).</summary>
        public static string ProviderDeliveredTestSettings => TestdataDir + @"RunSettingsServiceTests\Provider_delivered.runsettings";

        // fallback settings (TAEF_ADAPTER_FALLBACK_SETTINGS) of the generated end-to-end tests (see ConsoleDllTests.GetSettingsFile)
        public static string UserTestSettingsForGeneratedTests_Project => TestdataDir + "Project.runsettings";
        public static string UserTestSettingsForGeneratedTests_Solution => TestdataDir + "Solution.runsettings";
        public static string UserTestSettingsForGeneratedTests_SolutionProject => TestdataDir + "SolutionProject.runsettings";
        public static string UserTestSettingsForListingTests => TestdataDir + "ListTests.runsettings";

        /// <returns>Full path of a captured TE.exe output file (see Resources\TestData\TaefOutput\README.md).</returns>
        public static string GetTaefOutputFile(string fileName) => TaefOutputDir + fileName;

        /// <returns>The lines of a captured TE.exe output file (UTF-8).</returns>
        public static string[] ReadTaefOutputLines(string fileName)
            => File.ReadAllLines(GetTaefOutputFile(fileName), System.Text.Encoding.UTF8);

        #endregion

        #region TE.exe

        /// <summary>
        /// The folder containing the TAEF runtimes (x86\TE.exe, x64\TE.exe, arm64\TE.exe), e.g.
        /// <c>C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF\</c>: environment variable
        /// <see cref="TaefRuntimesDirEnvVariable"/>, else KitsRoot10 from the registry, else the default install location.
        /// </summary>
        public static string TaefRuntimesDir => LazyTaefRuntimesDir.Value;
        private static readonly Lazy<string> LazyTaefRuntimesDir = new Lazy<string>(FindTaefRuntimesDir);

        /// <returns>Full path of TE.exe for <paramref name="architecture"/> (x86, x64, arm64); the file might not exist.</returns>
        public static string GetTeExecutable(string architecture) => Path.Combine(TaefRuntimesDir, architecture, "TE.exe");

        /// <returns>Full path of the TE.exe matching the architecture of the sample DLLs of <paramref name="configuration"/>.</returns>
        public static string GetTeExecutable(SampleConfiguration configuration) => GetTeExecutable(configuration.GetArchitecture());

        public static string TeExecutableX86 => GetTeExecutable("x86");
        public static string TeExecutableX64 => GetTeExecutable("x64");

        /// <summary>
        /// A fake TE.exe (batch file, use it e.g. as value of option TeExecutable): ignores its arguments, prints the file
        /// given by environment variable <see cref="FakeTeOutputEnvVariable"/> (if set; e.g. a file of <see cref="TaefOutputDir"/>)
        /// and exits with the exit code given by <see cref="FakeTeExitCodeEnvVariable"/> (default 0). Pass the variables with
        /// option EnvironmentVariables (or set them for the test process).
        /// </summary>
        public static string FakeTeExecutable => TestdataDir + "FakeTe.cmd";
        public const string FakeTeOutputEnvVariable = "TAEF_FAKE_TE_OUTPUT";
        public const string FakeTeExitCodeEnvVariable = "TAEF_FAKE_TE_EXITCODE";

        private static string FindTaefRuntimesDir()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable(TaefRuntimesDirEnvVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return WithTrailingSeparator(fromEnvironment.Trim().Trim('"'));

            var roots = new List<string>();
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots"))
                    {
                        if (key?.GetValue("KitsRoot10") is string root && !string.IsNullOrWhiteSpace(root))
                            roots.Add(root);
                    }
                }
                catch (Exception)
                {
                    // ignore: fall back to the default location
                }
            }
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            roots.Add(Path.Combine(programFilesX86, @"Windows Kits\10\"));

            var candidates = roots.Select(r => Path.Combine(r, @"Testing\Runtimes\TAEF\")).ToList();
            return WithTrailingSeparator(candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "x64", "TE.exe")) || File.Exists(Path.Combine(c, "x86", "TE.exe")))
                                         ?? candidates.Last());
        }

        #endregion

        #region vstest.console.exe

        private const string VsTestConsoleRelativePath = @"Common7\IDE\Extensions\TestPlatform\vstest.console.exe";

        private static readonly Lazy<IList<(VsVersion Version, string VsTestConsole)>> VsTestConsoles =
            new Lazy<IList<(VsVersion, string)>>(FindVsTestConsoles);

        /// <returns>
        /// Path of vstest.console.exe to be used by end-to-end tests: environment variable <see cref="VsTestConsoleEnvVariable"/>,
        /// else the one of the VS version under test (<see cref="TestMetadata.VersionUnderTest"/>), else the one of the newest
        /// installed VS 2026/2022; null if there is none.
        /// </returns>
        public static string GetVsTestConsolePath()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable(VsTestConsoleEnvVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment.Trim().Trim('"');

            return GetVsTestConsolePath(TestMetadata.VersionUnderTest)
                ?? VsTestConsoles.Value.OrderByDescending(v => v.Version).Select(v => v.VsTestConsole).FirstOrDefault();
        }

        /// <returns>Path of vstest.console.exe of an installed VS <paramref name="version"/> (VS2026, VS2022), or null.</returns>
        public static string GetVsTestConsolePath(VsVersion version)
        {
            return VsTestConsoles.Value.Where(v => v.Version == version).Select(v => v.VsTestConsole).FirstOrDefault();
        }

        private static IList<(VsVersion, string)> FindVsTestConsoles()
        {
            var result = new List<(VsVersion, string)>();
            foreach (string installationPath in FindVsInstallations())
            {
                string vsTestConsole = Path.Combine(installationPath, VsTestConsoleRelativePath);
                if (!File.Exists(vsTestConsole))
                    continue;

                VsVersion version = GetVsVersion(installationPath);
                if (version != VsVersion.Unknown && result.All(r => r.Item1 != version))
                    result.Add((version, vsTestConsole));
            }
            return result;
        }

        private static IEnumerable<string> FindVsInstallations()
        {
            var installations = new List<string>();

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string vswhere = Path.Combine(programFilesX86, @"Microsoft Visual Studio\Installer\vswhere.exe");
            if (File.Exists(vswhere))
            {
                try
                {
                    var startInfo = new ProcessStartInfo(vswhere, "-all -prerelease -products * -version [17.0,19.0) -sort -property installationPath")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (Process process = Process.Start(startInfo))
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(30000);
                        installations.AddRange(output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
                    }
                }
                catch (Exception)
                {
                    // ignore: fall back to known locations
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (string year in new[] { "18", "2022" })
            {
                foreach (string edition in new[] { "Enterprise", "Professional", "Community", "Preview", "BuildTools" })
                {
                    installations.Add(Path.Combine(programFiles, "Microsoft Visual Studio", year, edition));
                }
            }

            return installations.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static VsVersion GetVsVersion(string installationPath)
        {
            try
            {
                string devenv = Path.Combine(installationPath, @"Common7\IDE\devenv.exe");
                string file = File.Exists(devenv) ? devenv : Path.Combine(installationPath, VsTestConsoleRelativePath);
                int major = FileVersionInfo.GetVersionInfo(file).FileMajorPart;
                return Enum.IsDefined(typeof(VsVersion), major) ? (VsVersion)major : VsVersion.Unknown;
            }
            catch (Exception)
            {
                return VsVersion.Unknown;
            }
        }

        #endregion

        #region Misc

        public static string GetGoldenFileName(string typeName, string testCaseName, string fileExtension)
        {
            return typeName + "__" + testCaseName + fileExtension;
        }

        public static string NormalizePointerInfo(string text)
        {
            return Regex.Replace(text, "([0-9A-F]{8}){1,2} pointing to", "${MemoryLocation} pointing to");
        }

        #endregion

    }

}
