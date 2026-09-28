// This file has been modified for TAEF support.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TaefTestAdapter.TestAdapter;
using TaefTestAdapter.Tests.Common.ResultChecker;

namespace TaefTestAdapter.Tests.Common
{
    /// <summary>
    /// Base class of the generated end-to-end tests (VsPackage.Tests.Generated): runs vstest.console.exe with the adapter
    /// and compares the (normalized) TRX file or test list with golden files in VsPackage.Tests.Generated\GoldenFiles.
    /// Settings files are passed through environment variable <see cref="CommonFunctions.TaefSettingsEnvVariable"/>.
    /// </summary>
    public abstract class AbstractConsoleTests
    {
        /// <summary>Project folder of VsPackage.Tests.Generated (contains GoldenFiles and TestErrors).</summary>
        public static string GeneratedTestsProjectDir => TestResources.AdapterSolutionDir + @"VsPackage.Tests.Generated\";

        protected readonly string SolutionFile;
        protected readonly string TestAdapterDir;

        private string _envVarStorage;

        protected AbstractConsoleTests()
        {
            AbstractConsoleIntegrationTests.GetDirectories(out TestAdapterDir, out SolutionFile);
        }

        protected void RunTestsAndCheckOutput(string settingsFile, string arguments, [CallerMemberName] string testCaseName = null)
        {
            string finalArguments = GetAdapterIntegration() + arguments;

            if (string.IsNullOrWhiteSpace(settingsFile))
            {
                DoRunTestsAndCheckOutput(finalArguments, testCaseName);
                return;
            }

            Setup(settingsFile);
            try
            {
                DoRunTestsAndCheckOutput(finalArguments, testCaseName);
            }
            finally
            {
                Teardown();
            }
        }

        protected void ListTestsOf(string testDll, [CallerMemberName] string testCaseName = null)
        {
            Setup(TestResources.UserTestSettingsForListingTests);
            try
            {
                DoListTestsOf(testDll, testCaseName);
            }
            finally
            {
                Teardown();
            }
        }

        private string GetLogger()
        {
            return AbstractConsoleIntegrationTests.GetLogger();
        }

        private string GetAdapterIntegration()
        {
            return GetLogger() + @"/TestAdapterPath:""" + TestAdapterDir + @"""";
        }

        private void Setup(string settingsFile)
        {
            if (_envVarStorage != null)
            {
                throw new InvalidOperationException();
            }

            _envVarStorage = Environment.GetEnvironmentVariable(CommonFunctions.TaefSettingsEnvVariable);
            Environment.SetEnvironmentVariable(CommonFunctions.TaefSettingsEnvVariable, settingsFile);
        }

        private void Teardown()
        {
            Environment.SetEnvironmentVariable(CommonFunctions.TaefSettingsEnvVariable, _envVarStorage);
            _envVarStorage = null;
        }

        private void DoRunTestsAndCheckOutput(string arguments, string testCaseName)
        {
            TrxResultChecker resultChecker = new TrxResultChecker(SolutionFile);
            resultChecker.RunTestsAndCheckOutput(GetType().Name, arguments, testCaseName);
        }

        private void DoListTestsOf(string testDll, string testCaseName)
        {
            testDll = Path.GetFullPath(testDll);
            string arguments = GetAdapterIntegration() + @" /ListTests:""" + testDll + @"""";
            arguments += @" /Settings:""" + TestResources.UserTestSettingsForListingTests + @"""";
            string resultString = AbstractConsoleIntegrationTests.RunVsTestConsoleAndGetOutput(SolutionFile, arguments);
            string[] resultLines = resultString.Split('\n');
            resultLines = resultLines.Where(l => l.StartsWith("    ")).Select(l => l.Trim()).ToArray();
            for (int i = 0; i < resultLines.Length; i++)
            {
                resultLines[i] = Regex.Replace(resultLines[i], "Information:.*", "");
            }
            resultString = string.Join("\n", resultLines);
            new ResultChecker.ResultChecker(Path.Combine(GeneratedTestsProjectDir, "GoldenFiles"), Path.Combine(GeneratedTestsProjectDir, "TestErrors"), ".txt")
                // ReSharper disable once ExplicitCallerInfoArgument
                .CheckResults(resultString, GetType().Name, testCaseName);
        }

    }

}
