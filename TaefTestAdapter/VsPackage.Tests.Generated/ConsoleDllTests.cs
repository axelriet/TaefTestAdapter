// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using TaefTestAdapter.TestAdapter;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// End-to-end tests running vstest.console.exe with the adapter on the sample test DLLs; the test methods are generated
    /// from TAEF_Console.csv (see ConsoleTests.tt). The results (TRX files, normalized by Trx2TestRun.xslt of Tests.Common)
    /// and the lists of tests are compared with the golden files in GoldenFiles.
    /// </summary>
    /// <remarks>
    /// The tests run on copies of the sample test DLLs (in a temporary folder <c>SampleTests\&lt;configuration&gt;</c>,
    /// i.e. the normalized paths are the same as those of the original DLLs), since the adapter writes files next to the
    /// test DLLs (test durations). The adapter's settings are passed as fallback settings (environment variable
    /// <see cref="CommonFunctions.TaefSettingsEnvVariable"/>), vstest.console's results are written into the temporary
    /// folder, and the lists of tests are written into files (i.e. they are UTF-8 and do not depend on the console's code
    /// page).
    /// </remarks>
    public partial class ConsoleDllTests
    {
        private static string GoldenFilesDir => Path.Combine(GeneratedTestsProjectDir, "GoldenFiles");
        private static string TestErrorsDir => Path.Combine(GeneratedTestsProjectDir, "TestErrors");

        /// <summary>Runs the tests of <paramref name="testCase"/> and compares the results with the golden file of the test.</summary>
        private void RunConsoleTest(ConsoleTestCase testCase, [CallerMemberName] string testCaseName = null)
        {
            CheckPrerequisites(GetTestDll(testCase.TestFile));

            using (var tempDir = new TemporaryDirectory())
            // the settings file is set by RunTestsAndCheckOutput() - it must not be taken from this process' environment
            using (new EnvironmentVariableScope(CommonFunctions.TaefSettingsEnvVariable, null))
            {
                string testDll = CopySampleTestDll(testCase.TestFile, tempDir);
                string settingsFile = CreateSettingsFile(testCase, tempDir);
                string arguments = GetArguments(testCase, testDll, tempDir.GetPath("TestResults"));

                // ReSharper disable once ExplicitCallerInfoArgument
                RunTestsAndCheckOutput(settingsFile, arguments, testCaseName);
            }
        }

        /// <summary>
        /// Lists the tests of the sample test DLL <paramref name="testFile"/> with vstest.console's /ListFullyQualifiedTests
        /// (i.e. the VS names of the tests, in the order of discovery) and compares them with the golden file of the test.
        /// </summary>
        private void ListFullyQualifiedTestsOf(string testFile, [CallerMemberName] string testCaseName = null)
        {
            CheckPrerequisites(GetTestDll(testFile));

            using (var tempDir = new TemporaryDirectory())
            using (new EnvironmentVariableScope(CommonFunctions.TaefSettingsEnvVariable, null))
            {
                string testDll = CopySampleTestDll(testFile, tempDir);
                string listFile = tempDir.GetPath("Tests.txt");
                string arguments = $@"/TestAdapterPath:""{TestAdapterDir}"" ""{testDll}"" /ListFullyQualifiedTests /ListTestsTargetPath:""{listFile}"""
                                   + $@" /Settings:""{TestResources.UserTestSettingsForListingTests}""";

                string vsTestConsole = TestResources.GetVsTestConsolePath();
                vsTestConsole.Should().NotBeNull("vstest.console.exe of VS 2026 or 2022 is needed (or set environment variable {0})", TestResources.VsTestConsoleEnvVariable);

                new TestProcessLauncher().GetOutputStreams(Path.GetDirectoryName(SolutionFile), vsTestConsole, arguments,
                    out _, out _, out List<string> output);

                listFile.AsFileInfo().Should().Exist($"vstest.console should have listed the tests. Output:{Environment.NewLine}{string.Join(Environment.NewLine, output)}");
                string tests = string.Join("\n", File.ReadAllLines(listFile, Encoding.UTF8));

                new Tests.Common.ResultChecker.ResultChecker(GoldenFilesDir, TestErrorsDir, ".txt")
                    // ReSharper disable once ExplicitCallerInfoArgument
                    .CheckResults(tests, GetType().Name, testCaseName);
            }
        }

        /// <returns>The sample test DLL of <paramref name="testFile"/> (see <see cref="ConsoleTestDlls.GetTestDll"/>).</returns>
        internal static string GetTestDll(string testFile)
        {
            return Path.GetFullPath(ConsoleTestDlls.GetTestDll(testFile));
        }

        /// <summary>
        /// Copies the sample test DLL of <paramref name="testFile"/> into folder <c>SampleTests\&lt;configuration&gt;</c> of
        /// <paramref name="tempDir"/>, together with its PDB, its runtime dependencies (e.g. DllProject.dll) and the files
        /// <c>&lt;DLL file name&gt;.*</c> (except for test durations files).
        /// </summary>
        /// <returns>The full path of the copied test DLL.</returns>
        internal static string CopySampleTestDll(string testFile, TemporaryDirectory tempDir)
        {
            string originalDll = GetTestDll(testFile);
            string sourceDir = Path.GetDirectoryName(originalDll) ?? "";
            string configurationDir = Path.GetFileName(sourceDir);
            string targetDir = tempDir.GetPath(Path.Combine("SampleTests", configurationDir));
            Directory.CreateDirectory(targetDir);

            string dllFileName = Path.GetFileName(originalDll);
            var files = new List<string> { dllFileName, Path.ChangeExtension(dllFileName, ".pdb") };
            files.AddRange(TestResources.GetRuntimeDependencies(dllFileName));
            files.AddRange(Directory.GetFiles(sourceDir, dllFileName + ".*")
                .Select(Path.GetFileName)
                .Where(f => !f.EndsWith(TaefConstants.DurationsExtension, StringComparison.OrdinalIgnoreCase)));

            foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string source = Path.Combine(sourceDir, file);
                if (File.Exists(source))
                    File.Copy(source, Path.Combine(targetDir, file));
            }

            return Path.Combine(targetDir, dllFileName);
        }

        /// <returns>The settings file of <paramref name="settings"/> (false, true, Solution, SolutionProject), or null.</returns>
        internal static string GetSettingsFile(string settings)
        {
            switch (settings)
            {
                case ConsoleTestCase.NoSettings:
                    return null;
                case "true":
                    return TestResources.UserTestSettingsForGeneratedTests_Project;
                case "Solution":
                    return TestResources.UserTestSettingsForGeneratedTests_Solution;
                case "SolutionProject":
                    return TestResources.UserTestSettingsForGeneratedTests_SolutionProject;
                default:
                    throw new ArgumentException($"Unknown settings key: {settings}", nameof(settings));
            }
        }

        /// <returns>
        /// The settings file of the test case: the file of <see cref="ConsoleTestCase.Settings"/> if no setting is overridden
        /// by the test case, else a copy of that file (or a new file) in <paramref name="tempDir"/> with the overridden
        /// settings set in the solution settings and in all project settings which set them.
        /// </returns>
        internal static string CreateSettingsFile(ConsoleTestCase testCase, TemporaryDirectory tempDir)
        {
            string settingsFile = GetSettingsFile(testCase.Settings);
            if (!testCase.OverridesSettings)
                return settingsFile;

            var overriddenSettings = new Dictionary<string, string>();
            if (testCase.RunInProcess)
                overriddenSettings["RunInProcess"] = "true";
            if (testCase.RunIgnoredTests != ConsoleTestCase.DefaultValue)
                overriddenSettings["RunIgnoredTests"] = bool.Parse(testCase.RunIgnoredTests) ? "true" : "false";
            if (testCase.IsolationLevel != ConsoleTestCase.DefaultIsolationLevel)
                overriddenSettings["IsolationLevel"] = testCase.IsolationLevel;
            if (testCase.NrOfTestRepetitions != 1)
                overriddenSettings["NrOfTestRepetitions"] = testCase.NrOfTestRepetitions.ToString();

            XDocument document = settingsFile != null
                ? XDocument.Load(settingsFile)
                : new XDocument(new XElement("RunSettings"));

            XElement adapterSettings = GetOrAddElement(document.Root, TaefConstants.SettingsName);
            XElement solutionSettings = GetOrAddElement(GetOrAddElement(adapterSettings, "SolutionSettings"), "Settings");
            IList<XElement> projectSettings = adapterSettings.Element("ProjectSettings")?.Elements("Settings").ToList() ?? new List<XElement>();
            foreach (var setting in overriddenSettings)
            {
                solutionSettings.SetElementValue(setting.Key, setting.Value);
                foreach (XElement settings in projectSettings.Where(s => s.Element(setting.Key) != null))
                {
                    settings.SetElementValue(setting.Key, setting.Value);
                }
            }

            string file = tempDir.GetPath("Settings.runsettings");
            document.Save(file);
            return file;
        }

        private static XElement GetOrAddElement(XElement parent, string name)
        {
            XElement element = parent.Element(name);
            if (element == null)
            {
                element = new XElement(name);
                parent.Add(element);
            }
            return element;
        }

        /// <returns>The vstest.console arguments of <paramref name="testCase"/> (without the adapter integration).</returns>
        internal static string GetArguments(ConsoleTestCase testCase, string testDll, string resultsDirectory)
        {
            var arguments = new StringBuilder();
            arguments.Append(@" """).Append(testDll).Append('"');

            if (testCase.TestCaseFilter != ConsoleTestCase.NoFilter)
            {
                // a leading * just marks a negative test case (a filter with a non-existing property)
                string filter = testCase.TestCaseFilter.TrimStart('*');
                arguments.Append(@" /TestCaseFilter:""").Append(filter).Append('"');
            }

            if (testCase.EnableCodeCoverage)
                arguments.Append(" /EnableCodeCoverage");

            if (testCase.InIsolation)
                arguments.Append(" /InIsolation");

            // keep the solution folder of the samples clean
            arguments.Append(@" /ResultsDirectory:""").Append(resultsDirectory).Append('"');

            return arguments.ToString();
        }

        private void CheckPrerequisites(string testDll)
        {
            testDll.AsFileInfo().Should().Exist($"the sample test DLLs must have been built (SampleTests.sln), or environment variable {TestResources.SamplesDirEnvVariable} must point to them");
            Path.Combine(TestAdapterDir, "TaefTestAdapter.TestAdapter.dll").AsFileInfo().Should().Exist("the adapter must have been built");
        }

        /// <summary>Sets an environment variable of this process and restores its former value on dispose.</summary>
        private sealed class EnvironmentVariableScope : IDisposable
        {
            private readonly string _name;
            private readonly string _formerValue;

            public EnvironmentVariableScope(string name, string value)
            {
                _name = name;
                _formerValue = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }

            public void Dispose()
            {
                Environment.SetEnvironmentVariable(_name, _formerValue);
            }
        }
    }
}
