// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.Tests.Common.Helpers;

namespace TaefTestAdapter.Tests.Common
{
    /// <summary>
    /// Creates test data: test cases discovered from the real sample test DLLs (with the settings of the
    /// <see cref="TestEnvironment"/> at the time of first access; results are cached per instance), and dummy test cases
    /// and results with TAEF names (<c>Namespace::Class::Method[#row]</c>).
    /// </summary>
    public class TestDataCreator
    {
        /// <summary>Path of the (non-existing) test DLL of dummy test cases.</summary>
        public const string DummyTestDll = "c:\\ff.dll";

        private readonly TestEnvironment _testEnvironment;
        private readonly IDictionary<string, List<TestCase>> _testCasesOfTestDlls = new Dictionary<string, List<TestCase>>(StringComparer.OrdinalIgnoreCase);

        public TestDataCreator(TestEnvironment testEnvironment)
        {
            _testEnvironment = testEnvironment;
        }

        #region Test cases of the sample test DLLs

        private List<TestCase> _allTestCasesExceptLoadTests;
        /// <summary>
        /// All test cases of <see cref="AllTestCasesOfSampleTests"/>, <see cref="AllTestCasesOfHardCrashingTests"/> and
        /// <see cref="AllTestCasesOfLongRunningTests"/> (x86 DLLs).
        /// </summary>
        public List<TestCase> AllTestCasesExceptLoadTests
        {
            get
            {
                if (_allTestCasesExceptLoadTests == null)
                {
                    _allTestCasesExceptLoadTests = new List<TestCase>();
                    _allTestCasesExceptLoadTests.AddRange(AllTestCasesOfSampleTests);
                    _allTestCasesExceptLoadTests.AddRange(AllTestCasesOfHardCrashingTests);
                    _allTestCasesExceptLoadTests.AddRange(AllTestCasesOfLongRunningTests);
                }
                return _allTestCasesExceptLoadTests;
            }
        }

        /// <summary>The <see cref="TestResources.NrOfTests"/> test cases of Tests_taef.dll (Debug, x86), including the ignored ones.</summary>
        public List<TestCase> AllTestCasesOfSampleTests => GetTestCasesOfTestDll(TestResources.Tests_DebugX86);

        /// <summary>The <see cref="TestResources.NrOfCrashingTests"/> test cases of CrashingTests_taef.dll (Debug, x86).</summary>
        public List<TestCase> AllTestCasesOfHardCrashingTests => GetTestCasesOfTestDll(TestResources.CrashingTests_DebugX86);

        /// <summary>The <see cref="TestResources.NrOfLongRunningTests"/> test cases of LongRunningTests_taef.dll (Release, x86).</summary>
        public List<TestCase> AllTestCasesOfLongRunningTests => GetTestCasesOfTestDll(TestResources.LongRunningTests_ReleaseX86);

        /// <returns>
        /// The test cases of <paramref name="testDll"/> as discovered by <see cref="TaefDiscoverer.GetTestsFromTestDll"/>
        /// with the settings and logger of the <see cref="TestEnvironment"/> (discovered once per instance and DLL).
        /// </returns>
        public List<TestCase> GetTestCasesOfTestDll(string testDll)
        {
            string fullPath = Path.GetFullPath(testDll);
            if (!_testCasesOfTestDlls.TryGetValue(fullPath, out List<TestCase> testCases))
            {
                var discoverer = new TaefDiscoverer(_testEnvironment.Logger, _testEnvironment.Options);
                testCases = discoverer.GetTestsFromTestDll(fullPath).ToList();
                _testCasesOfTestDlls.Add(fullPath, testCases);
            }
            return testCases;
        }

        /// <returns>
        /// All test cases of <see cref="AllTestCasesExceptLoadTests"/> whose TAEF name (FullyQualifiedName) contains one of
        /// <paramref name="qualifiedNames"/> (e.g. "TaefSamples::Crashing::LongRunning", "TaefSamples::LongRunningTests::Test2").
        /// </returns>
        public List<TestCase> GetTestCases(params string[] qualifiedNames)
        {
            return AllTestCasesExceptLoadTests.Where(
                testCase => qualifiedNames.Any(
                    qualifiedName => testCase.FullyQualifiedName.Contains(qualifiedName)))
                    .ToList();
        }

        #endregion

        #region Dummy test cases and results

        /// <returns>A test case without source location, traits and properties (FullyQualifiedName = DisplayName = <paramref name="name"/>).</returns>
        public TestCase ToTestCase(string name, string testDll, string sourceFile = "")
        {
            return new TestCase(name, testDll, name, sourceFile, 0);
        }

        /// <returns>A test case of <see cref="DummyTestDll"/> without source location, traits and properties.</returns>
        public TestCase ToTestCase(string name)
        {
            return ToTestCase(name, DummyTestDll);
        }

        /// <returns>A test case of <see cref="DummyTestDll"/> with the given traits.</returns>
        public TestCase ToTestCase(string name, params Trait[] traits)
        {
            TestCase testCase = ToTestCase(name);
            testCase.Traits.AddRange(traits);
            return testCase;
        }

        /// <returns>A test case of <see cref="DummyTestDll"/> with trait Ignore=true (i.e. TaefConstants.IsIgnored() is true).</returns>
        public TestCase ToIgnoredTestCase(string name)
        {
            return ToTestCase(name, new Trait(TaefConstants.IgnoreProperty, "true"));
        }

        public TestResult ToTestResult(string qualifiedTestCaseName, TestOutcome outcome, int duration, string testDll = DummyTestDll)
        {
            return new TestResult(ToTestCase(qualifiedTestCaseName, testDll))
            {
                Outcome = outcome,
                Duration = TimeSpan.FromMilliseconds(duration)
            };
        }

        /// <returns>
        /// Test cases of <see cref="DummyTestDll"/> for those names of <paramref name="allQualifiedNames"/> which are contained in
        /// <paramref name="qualifiedNamesToRun"/>, each with a <see cref="TestCaseMetaDataProperty"/> computed as the discoverer
        /// does: number of tests of the same TAEF class (<see cref="TaefNames.GetClassName"/>) among
        /// <paramref name="allQualifiedNames"/>, and the number of all tests.
        /// </returns>
        public IEnumerable<TestCase> CreateDummyTestCasesFull(string[] qualifiedNamesToRun, string[] allQualifiedNames)
        {
            return CreateDummyTestCasesFull(qualifiedNamesToRun, allQualifiedNames, new string[0]);
        }

        /// <summary>
        /// As <see cref="CreateDummyTestCasesFull(string[],string[])"/>; test cases whose names are contained in
        /// <paramref name="ignoredQualifiedNames"/> get the trait Ignore=true.
        /// </summary>
        public IEnumerable<TestCase> CreateDummyTestCasesFull(string[] qualifiedNamesToRun, string[] allQualifiedNames, string[] ignoredQualifiedNames)
        {
            var class2Count = allQualifiedNames
                .GroupBy(TaefNames.GetClassName)
                .ToDictionary(g => g.Key, g => g.Count());

            var testCases = new List<TestCase>();
            foreach (string qualifiedName in allQualifiedNames)
            {
                if (!qualifiedNamesToRun.Contains(qualifiedName))
                    continue;

                TestCase testCase = ignoredQualifiedNames.Contains(qualifiedName)
                    ? ToIgnoredTestCase(qualifiedName)
                    : ToTestCase(qualifiedName);
                testCase.Properties.Add(new TestCaseMetaDataProperty(class2Count[TaefNames.GetClassName(qualifiedName)], allQualifiedNames.Length));
                testCases.Add(testCase);
            }

            return testCases;
        }

        /// <returns>Test cases of <see cref="DummyTestDll"/> (with meta data) for all <paramref name="qualifiedNames"/>.</returns>
        public IEnumerable<TestCase> CreateDummyTestCases(params string[] qualifiedNames)
        {
            return CreateDummyTestCasesFull(qualifiedNames, qualifiedNames);
        }

        #endregion

        #region PATH extension

        /// <summary>
        /// Copies DllTests_taef.dll (Release, x86; with PDB) into <c>&lt;temp dir&gt;\testdll\</c> and its dependency
        /// DllProject.dll into <c>&lt;temp dir&gt;\dll\</c>: the tests can only be run if <c>&lt;temp dir&gt;\dll</c> is
        /// on the PATH (option PathExtension); otherwise TE.exe reports them as Blocked.
        /// </summary>
        /// <returns>The temp dir (delete it with <see cref="Utils.DeleteDirectory(string)"/>).</returns>
        public static string PreparePathExtensionTest()
        {
            string baseDir = Utils.GetTempDirectory();
            string testDllDir = Path.GetDirectoryName(GetPathExtensionTestDll(baseDir));
            string dllDir = Path.Combine(baseDir, "dll");

            // ReSharper disable AssignNullToNotNullAttribute
            Directory.CreateDirectory(testDllDir);
            Directory.CreateDirectory(dllDir);
            File.Copy(TestResources.DllTests_ReleaseX86, GetPathExtensionTestDll(baseDir));
            File.Copy(Path.ChangeExtension(TestResources.DllTests_ReleaseX86, ".pdb"), Path.Combine(testDllDir, Path.GetFileNameWithoutExtension(TestResources.DllTestsDll) + ".pdb"));
            File.Copy(TestResources.DllTestsDll_ReleaseX86, Path.Combine(dllDir, TestResources.DllProjectDll));
            // ReSharper restore AssignNullToNotNullAttribute

            return baseDir;
        }

        /// <returns>The copied DllTests_taef.dll within a folder created by <see cref="PreparePathExtensionTest"/>.</returns>
        public static string GetPathExtensionTestDll(string baseDir)
        {
            return Path.Combine(baseDir, "testdll", TestResources.DllTestsDll);
        }

        /// <returns>The folder (within a folder created by <see cref="PreparePathExtensionTest"/>) which must be added to the PATH.</returns>
        public static string GetPathExtensionDllDir(string baseDir)
        {
            return Path.Combine(baseDir, "dll");
        }

        #endregion

    }

}
