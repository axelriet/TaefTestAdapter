// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// Checks the generated end-to-end tests: ConsoleTests.cs must be up to date with ConsoleTests.tt and
    /// TAEF_Console.csv, the CSV file must be valid, and there must not be any golden files of removed tests.
    /// </summary>
    [TestClass]
    public class ConsoleTestsGenerationTests
    {
        /// <summary>
        /// Git for Windows can not create files whose (absolute) paths have more than 259 characters unless core.longpaths
        /// is enabled, and a failing file makes the whole checkout of a clone fail.
        /// </summary>
        private const int MaxPathLengthOfGit = 259;

        /// <summary>
        /// The golden files (named after the generated tests) have the longest paths of the enlistment; with this maximum,
        /// the enlistment can be cloned into folders with paths of up to 108 characters without enabling core.longpaths.
        /// </summary>
        private const int MaxLengthOfGoldenFilePath = 150;

        /// <summary>MaxMethodNameLength of ConsoleTests.tt.</summary>
        private const int MaxLengthOfTestName = 72;

        private static string ProjectDir => AbstractConsoleTests.GeneratedTestsProjectDir;
        private static string CsvFile => Path.Combine(ProjectDir, "TAEF_Console.csv");

        [TestMethod]
        [TestCategory(Integration)]
        public void ConsoleTests_GeneratedFile_IsUpToDate()
        {
            string textTransform = FindTextTransform();
            if (textTransform == null)
                Assert.Inconclusive("TextTransform.exe of Visual Studio has not been found");

            using (var tempDir = new TemporaryDirectory())
            {
                string generatedFile = tempDir.GetPath("ConsoleTests.cs");
                var startInfo = new ProcessStartInfo(textTransform, $@"-out ""{generatedFile}"" ""{Path.Combine(ProjectDir, "ConsoleTests.tt")}""")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    // ReSharper disable once PossibleNullReferenceException
                    string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    process.ExitCode.Should().Be(0, $"TextTransform.exe should transform ConsoleTests.tt. Output:{Environment.NewLine}{output}");
                }

                Normalize(File.ReadAllText(generatedFile, Encoding.UTF8))
                    .Should().Be(Normalize(File.ReadAllText(Path.Combine(ProjectDir, "ConsoleTests.cs"), Encoding.UTF8)),
                        "ConsoleTests.cs must be regenerated after changing ConsoleTests.tt or TAEF_Console.csv (see ConsoleTests.tt)");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TaefConsoleCsv_AllRows_AreValid()
        {
            IList<Dictionary<string, string>> rows = ReadCsv();

            rows.Should().NotBeEmpty();
            foreach (Dictionary<string, string> row in rows)
            {
                string because = $"row {string.Join(", ", row.Values)} should be valid";

                Action gettingTestDll = () => ConsoleDllTests.GetTestDll(row["TestFile"]);
                gettingTestDll.Should().NotThrow(because);
                Action gettingSettingsFile = () => ConsoleDllTests.GetSettingsFile(row["Settings"]);
                gettingSettingsFile.Should().NotThrow(because);
                ConsoleDllTests.GetSettingsFile(row["Settings"])?.AsFileInfo().Should().Exist(because);

                row["TestCaseFilter"].Should().NotBeNullOrWhiteSpace(because);
                bool.Parse(row["EnableCodeCoverage"]);
                bool.Parse(row["InIsolation"]);
                bool.Parse(row["RunInProcess"]);
                row["RunIgnoredTests"].Should().BeOneOf(new[] { ConsoleTestCase.DefaultValue, "true", "false" }, because);
                Enum.TryParse(row["IsolationLevel"], out TaefIsolationLevel _).Should().BeTrue(because);
                int.Parse(row["NrOfTestRepetitions"]).Should().BeGreaterOrEqualTo(1, because);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ConsoleDllTests_TestMethods_OnePerRowAndOneListingPerTestFile()
        {
            IList<Dictionary<string, string>> rows = ReadCsv();
            MethodInfo[] testMethods = GetTestMethods();

            testMethods.Where(m => !m.Name.StartsWith("List_TestsOf_")).Should().HaveCount(rows.Count);
            testMethods.Where(m => m.Name.StartsWith("List_TestsOf_")).Select(m => m.Name.Substring("List_TestsOf_".Length))
                .Should().BeEquivalentTo(rows.Select(r => r["TestFile"]).Distinct());
            testMethods.Should().OnlyContain(m => m.GetCustomAttributes<TestCategoryAttribute>().Any(c => c.TestCategories.Contains(EndToEnd)));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GoldenFiles_AllFiles_BelongToExistingTests()
        {
            var testNames = new HashSet<string>(GetTestMethods().Select(m => $"{nameof(ConsoleDllTests)}__{m.Name}"));

            string[] goldenFiles = Directory.GetFiles(Path.Combine(ProjectDir, "GoldenFiles"));
            goldenFiles.Should().NotBeEmpty();
            foreach (string goldenFile in goldenFiles)
            {
                string testName = Path.GetFileNameWithoutExtension(goldenFile);
                testNames.Should().Contain(testName, $"golden file {Path.GetFileName(goldenFile)} should belong to an existing test");
                Path.GetExtension(goldenFile).Should().Be(testName.Contains("__List_TestsOf_") ? ".txt" : ".xml");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GoldenFiles_PathsRelativeToEnlistmentRoot_AreShortEnoughForCloningWithoutLongPaths()
        {
            string goldenFilesDir = Path.Combine(ProjectDir, "GoldenFiles");
            goldenFilesDir.Should().StartWith(TestResources.EnlistmentRoot);
            string relativeGoldenFilesDir = goldenFilesDir.Substring(TestResources.EnlistmentRoot.Length);

            // the golden files of all tests (including those which do not have golden files yet)
            IEnumerable<string> goldenFileNames = GetTestMethods()
                .Select(m => TestResources.GetGoldenFileName(nameof(ConsoleDllTests), m.Name, m.Name.StartsWith("List_TestsOf_") ? ".txt" : ".xml"))
                .Concat(Directory.GetFiles(goldenFilesDir).Select(Path.GetFileName));
            foreach (string goldenFileName in goldenFileNames)
            {
                string relativePath = Path.Combine(relativeGoldenFilesDir, goldenFileName);
                relativePath.Length.Should().BeLessOrEqualTo(MaxLengthOfGoldenFilePath,
                    $"git can not check out {relativePath} into a clone with a path of more than {MaxPathLengthOfGit - 1 - relativePath.Length} characters " +
                    "unless core.longpaths is enabled (see MaxMethodNameLength in ConsoleTests.tt)");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ConsoleDllTests_TestMethodNames_AreShortened()
        {
            GetTestMethods().Select(m => m.Name).Should().OnlyContain(n => n.Length <= MaxLengthOfTestName,
                "ConsoleTests.tt shortens the names of the tests (they are part of the names of the golden files)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateSettingsFile_OverriddenSettings_AreSetInSolutionAndProjectSettings()
        {
            var testCase = new ConsoleTestCase
            {
                TestFile = "SampleTests",
                Settings = "SolutionProject",
                RunInProcess = true,
                RunIgnoredTests = "true",
                IsolationLevel = "Class",
                NrOfTestRepetitions = 2
            };

            using (var tempDir = new TemporaryDirectory())
            {
                string settingsFile = ConsoleDllTests.CreateSettingsFile(testCase, tempDir);

                settingsFile.Should().StartWith(tempDir.Path);
                var container = new RunSettingsContainer();
                container.GetUnsetValuesFrom(settingsFile).Should().BeTrue();

                RunSettings solutionSettings = container.SolutionSettings;
                solutionSettings.RunInProcess.Should().BeTrue();
                solutionSettings.RunIgnoredTests.Should().BeTrue();
                solutionSettings.IsolationLevel.Should().Be(TaefIsolationLevel.Class);
                solutionSettings.NrOfTestRepetitions.Should().Be(2);
                solutionSettings.EnvironmentVariables.Should().Be("MYENVVAR=MyValue", "the other settings of the file are kept");

                RunSettings projectSettings = container.ProjectSettings.Should().ContainSingle().Which;
                projectSettings.RunIgnoredTests.Should().BeTrue("the project's value is overridden");
                projectSettings.AdditionalTestExecutionParam.Should().Be("/p:\"TestDirectory=$(TestDir)\"");
                projectSettings.RunInProcess.Should().BeNull("the value of the solution settings applies");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateSettingsFile_NoOverriddenSettings_SettingsFileOfKey()
        {
            using (var tempDir = new TemporaryDirectory())
            {
                ConsoleDllTests.CreateSettingsFile(new ConsoleTestCase { TestFile = "SampleTests", Settings = "false" }, tempDir)
                    .Should().BeNull();
                ConsoleDllTests.CreateSettingsFile(new ConsoleTestCase { TestFile = "SampleTests", Settings = "true" }, tempDir)
                    .Should().Be(TestResources.UserTestSettingsForGeneratedTests_Project);

                string settingsFile = ConsoleDllTests.CreateSettingsFile(
                    new ConsoleTestCase { TestFile = "SampleTests", Settings = "false", NrOfTestRepetitions = 3 }, tempDir);
                var container = new RunSettingsContainer();
                container.GetUnsetValuesFrom(settingsFile).Should().BeTrue();
                container.SolutionSettings.NrOfTestRepetitions.Should().Be(3);
                container.SolutionSettings.RunInProcess.Should().BeNull();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArguments_AllOptions_QuotedArguments()
        {
            var testCase = new ConsoleTestCase
            {
                TestFile = "SampleTests",
                TestCaseFilter = "*NotExisting=Foo|DisplayName=TaefSamples::TestMath::AddPasses",
                EnableCodeCoverage = true,
                InIsolation = true
            };

            string arguments = ConsoleDllTests.GetArguments(testCase, @"C:\My Samples\Tests_taef.dll", @"C:\My Results");

            arguments.Should().Be(@" ""C:\My Samples\Tests_taef.dll"" /TestCaseFilter:""NotExisting=Foo|DisplayName=TaefSamples::TestMath::AddPasses"""
                                  + @" /EnableCodeCoverage /InIsolation /ResultsDirectory:""C:\My Results""");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArguments_NoFilter_NoFilterArgument()
        {
            string arguments = ConsoleDllTests.GetArguments(new ConsoleTestCase { TestFile = "DllTests" }, @"C:\DllTests_taef.dll", @"C:\Results");

            arguments.Should().Be(@" ""C:\DllTests_taef.dll"" /ResultsDirectory:""C:\Results""");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void CopySampleTestDll_DllTests_CopiedWithPdbAndDependencyIntoConfigurationFolder()
        {
            string originalDll = ConsoleDllTests.GetTestDll("DllTests");
            originalDll.AsFileInfo().Should().Exist("the sample test DLLs must have been built");

            using (var tempDir = new TemporaryDirectory())
            {
                string testDll = ConsoleDllTests.CopySampleTestDll("DllTests", tempDir);

                // the normalization of the results (Trx2TestRun.xslt) relies on the folder names
                testDll.Should().Be(Path.Combine(tempDir.Path, "SampleTests", "Release", TestResources.DllTestsDll));
                testDll.AsFileInfo().Should().Exist();
                Path.ChangeExtension(testDll, ".pdb").AsFileInfo().Should().Exist();
                // ReSharper disable once AssignNullToNotNullAttribute
                Path.Combine(Path.GetDirectoryName(testDll), TestResources.DllProjectDll).AsFileInfo().Should().Exist();
                Directory.GetFiles(Path.GetDirectoryName(testDll), "*" + TaefConstants.DurationsExtension).Should().BeEmpty();
            }
        }

        private static MethodInfo[] GetTestMethods()
        {
            return typeof(ConsoleDllTests).GetMethods()
                .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
                .ToArray();
        }

        private static IList<Dictionary<string, string>> ReadCsv()
        {
            string[] lines = File.ReadAllLines(CsvFile).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            string[] columns = lines[0].Split('\t');
            columns.Should().Contain(new[]
            {
                "TestFile", "Settings", "TestCaseFilter", "EnableCodeCoverage", "InIsolation",
                "RunInProcess", "RunIgnoredTests", "IsolationLevel", "NrOfTestRepetitions"
            });

            return lines.Skip(1)
                .Select(l => l.Split('\t'))
                .Select(values =>
                {
                    values.Should().HaveCount(columns.Length);
                    return columns.Zip(values, (c, v) => new KeyValuePair<string, string>(c, v.Trim()))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                })
                .ToList();
        }

        private static string Normalize(string content)
        {
            content = content.TrimStart('\uFEFF');
            content = Regex.Replace(content, @"\r\n|\r", "\n");
            return content.TrimEnd();
        }

        /// <returns>TextTransform.exe of the Visual Studio whose vstest.console.exe is used by the tests, or null.</returns>
        private static string FindTextTransform()
        {
            string vsTestConsole = TestResources.GetVsTestConsolePath();
            if (vsTestConsole == null)
                return null;

            // <VS>\Common7\IDE\Extensions\TestPlatform\vstest.console.exe -> <VS>\Common7\IDE\TextTransform.exe
            DirectoryInfo ideDir = new FileInfo(vsTestConsole).Directory?.Parent?.Parent;
            string textTransform = ideDir == null ? null : Path.Combine(ideDir.FullName, "TextTransform.exe");
            return textTransform != null && File.Exists(textTransform) ? textTransform : null;
        }
    }
}
