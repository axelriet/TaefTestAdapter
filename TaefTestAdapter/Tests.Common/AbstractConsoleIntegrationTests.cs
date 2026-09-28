// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TaefTestAdapter.Tests.Common
{

    /// <summary>
    /// End-to-end tests running vstest.console.exe (<see cref="TestResources.GetVsTestConsolePath()"/>) with the adapter
    /// built by this build (<see cref="TestResources.TestAdapterDir"/>).
    /// </summary>
    public abstract class AbstractConsoleIntegrationTests
    {
        /// <summary>Executor URI of the adapter as printed by vstest.console (in lower case).</summary>
        public const string ExecutorUriLowerCase = "executor://testadapterfortaef/v1";

        protected readonly string TestAdapterDir;
        private readonly string _solutionFile;

        protected AbstractConsoleIntegrationTests()
        {
            GetDirectories(out TestAdapterDir, out _solutionFile);
        }

        /// <returns>The vstest.console arguments integrating the adapter, e.g. "/Logger:Trx /TestAdapterPath:&lt;dir&gt;".</returns>
        protected abstract string GetAdapterIntegration();

        public static string GetLogger()
        {
            return "/Logger:Trx ";
        }

        /// <param name="testAdapterDir">Folder containing the adapter's DLLs (value for /TestAdapterPath).</param>
        /// <param name="testSolutionFile">SampleTests.sln.</param>
        public static void GetDirectories(out string testAdapterDir, out string testSolutionFile)
        {
            testAdapterDir = TestResources.TestAdapterDir.TrimEnd('\\');
            testSolutionFile = TestResources.SampleTestsSolutionFile;
        }


        [TestMethod]
        [TestCategory(TestMetadata.TestCategories.EndToEnd)]
        public virtual void Console_ListDiscoverers_DiscovererIsListed()
        {
            string arguments = CreateListDiscoverersArguments();
            string output = RunVsTestConsoleAndGetOutput(_solutionFile, arguments);
            AssertIsListedOrInconclusive(output, ExecutorUriLowerCase, "/ListDiscoverers");
        }

        [TestMethod]
        [TestCategory(TestMetadata.TestCategories.EndToEnd)]
        public virtual void Console_ListExecutors_ExecutorIsListed()
        {
            string arguments = CreateListExecutorsArguments();
            string output = RunVsTestConsoleAndGetOutput(_solutionFile, arguments);
            AssertIsListedOrInconclusive(output, ExecutorUriLowerCase, "/ListExecutors");
        }

        [TestMethod]
        [TestCategory(TestMetadata.TestCategories.EndToEnd)]
        public virtual void Console_ListSettingsProviders_SettingsProviderIsListed()
        {
            string arguments = CreateListSettingsProvidersArguments();
            string output = RunVsTestConsoleAndGetOutput(_solutionFile, arguments);
            AssertIsListedOrInconclusive(output, TaefConstants.SettingsName.ToLowerInvariant(), "/ListSettingsProviders");
        }

        // vstest.console (at least up to version 18.10) ignores /TestAdapterPath for the /List* switches, i.e. the adapter is
        // only listed if it is installed into vstest's Extensions folder
        private static void AssertIsListedOrInconclusive(string output, string expectedLowerCase, string listSwitch)
        {
            if (!output.ToLowerInvariant().Contains(expectedLowerCase))
                Assert.Inconclusive($"'{expectedLowerCase}' is not listed: vstest.console ignores /TestAdapterPath for {listSwitch} (the adapter would have to be installed into vstest's Extensions folder)");
        }


        public static string RunVsTestConsoleAndGetOutput(string solutionFile, string arguments)
        {
            string command = TestResources.GetVsTestConsolePath();
            command.Should().NotBeNull("vstest.console.exe of VS 2026 or 2022 is needed (or set environment variable {0})", TestResources.VsTestConsoleEnvVariable);
            command.AsFileInfo().Should().Exist();

            string workingDir = Path.GetDirectoryName(solutionFile);

            var launcher = new TestProcessLauncher();
            launcher.GetOutputStreams(workingDir, command, arguments, out List<string> standardOut, out var standardErr);

            string resultString = string.Join("\n", standardOut) + "\n\n" + string.Join("\n", standardErr);
            // ReSharper disable once AssignNullToNotNullAttribute
            string baseDir = Directory.GetParent(Path.GetDirectoryName(solutionFile)).FullName;
            resultString = NormalizeOutput(resultString, baseDir);

            return resultString;
        }

        private static string NormalizeOutput(string resultString, string baseDir)
        {
            resultString = resultString.ReplaceIgnoreCase(TestResources.OutRoot, "${OutRoot}\\");
            resultString = resultString.ReplaceIgnoreCase(baseDir, "${BaseDir}");
            resultString = Regex.Replace(resultString, @"\\(Debug|Release)(-x64)?\\", @"\${ConfigurationName}$2\");
            resultString = Regex.Replace(resultString, @"Test execution time: .*", "Test execution time: ${RunTime}");
            resultString = Regex.Replace(resultString, @"Total time: .*", "Total time: ${RunTime}");
            resultString = TestResources.NormalizePointerInfo(resultString);
            resultString = Regex.Replace(resultString, @"Version .*\s*Copyright", "Version ${ToolVersion} Copyright");
            resultString = Regex.Replace(resultString, "Found [0-9]+ tests in test DLL", "Found ${NrOfTests} tests in test DLL");

            // exception messages are localized (thanks, MS). Add your own language here...
            resultString = resultString.Replace("   bei ", "   at ");
            resultString = Regex.Replace(resultString, @":Zeile ([0-9]+)\.", ":line $1");

            string testExecutionCompletedPattern = @".*Test execution completed, overall duration: .*\n";
            if (Regex.IsMatch(resultString, testExecutionCompletedPattern))
            {
                resultString = Regex.Replace(resultString, testExecutionCompletedPattern, "");
                resultString += "\n\nTest execution completed, overall duration: ${OverallDuration}\n";
            }

            string coveragePattern = @"Attachments:\n.*\.coverage\n\n";
            if (Regex.IsMatch(resultString, coveragePattern))
            {
                resultString = Regex.Replace(resultString, coveragePattern, "");
                resultString += "\n\nTest Adapter for TAEF Coverage Marker";
            }
            else
            {
                // workaround for build server
                coveragePattern = @"Attachments:\n.*\.coverage\n";
                if (Regex.IsMatch(resultString, coveragePattern))
                {
                    resultString = Regex.Replace(resultString, coveragePattern, "");
                    resultString += "\nTest Adapter for TAEF Coverage Marker";
                }
            }

            string noDataAdapterPattern = "Warning: Could not find diagnostic data adapter 'Code Coverage'. Make sure diagnostic data adapter is installed and try again.\n\n";
            if (Regex.IsMatch(resultString, noDataAdapterPattern))
            {
                resultString = Regex.Replace(resultString, noDataAdapterPattern, "");
                resultString += "\n\nTest Adapter for TAEF Coverage Marker";
            }

            string emptyLinePattern = @"\n\n";
            while (Regex.IsMatch(resultString, emptyLinePattern))
            {
                resultString = Regex.Replace(resultString, emptyLinePattern, "\n");
            }

            return resultString;
        }

        private string CreateListDiscoverersArguments()
        {
            return GetAdapterIntegration() + @" /ListDiscoverers " + GetLogger();
        }

        private string CreateListExecutorsArguments()
        {
            return GetAdapterIntegration() + @" /ListExecutors " + GetLogger();
        }

        private string CreateListSettingsProvidersArguments()
        {
            return GetAdapterIntegration() + @" /ListSettingsProviders " + GetLogger();
        }

    }

}
