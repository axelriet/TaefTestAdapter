// This file has been modified for TAEF support.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.Settings
{
    /// <summary>
    /// Replaces the placeholders (e.g. <c>$(TestDllDir)</c>, <c>%ENVVAR%</c>, keys of settings helper files) in the
    /// values of the settings.
    /// </summary>
    public class PlaceholderReplacer
    {
        private const string OnlyInsideVs = " (only available inside VS)";
        private const string TestExecutionOnly = " (test execution only)";

        public const string SolutionDirPlaceholder = "$(SolutionDir)";
        private const string DescriptionOfSolutionDirPlaceHolder = SolutionDirPlaceholder + " - directory of the solution" + OnlyInsideVs;

        public const string PlatformNamePlaceholder = "$(PlatformName)";
        private const string DescriptionOfPlatformNamePlaceholder = PlatformNamePlaceholder + " - the name of the solution's current platform" + OnlyInsideVs;

        public const string ConfigurationNamePlaceholder = "$(ConfigurationName)";
        private const string DescriptionOfConfigurationNamePlaceholder = ConfigurationNamePlaceholder + " - the name of the solution's current configuration" + OnlyInsideVs;

        public const string TestDllPlaceholder = "$(TestDll)";
        private const string DescriptionOfTestDllPlaceHolder = TestDllPlaceholder + " - path of the test DLL";

        public const string TestDllDirPlaceholder = "$(TestDllDir)";
        public const string DescriptionOfTestDllDirPlaceHolder = TestDllDirPlaceholder + " - directory containing the test DLL";

        public const string TestDirPlaceholder = "$(TestDir)";
        private const string DescriptionOfTestDirPlaceholder = TestDirPlaceholder + " - path of a directory which can be used by the tests";

        public const string ThreadIdPlaceholder = "$(ThreadId)";
        private const string DescriptionOfThreadIdPlaceholder = ThreadIdPlaceholder + " - id of thread executing the current tests";

        private const string DescriptionOfEnvVarPlaceholders = "Environment variables are also possible, e.g. %PATH%";


        private static readonly Regex PlaceholdersRegex = new Regex(@"\$\(\w+\)", RegexOptions.Compiled);

        private readonly Func<string> _getSolutionDir;
        private readonly Func<ITaefTestAdapterSettings> _getSettings;
        private readonly HelperFilesCache _helperFilesCache;
        private readonly ILogger _logger;

        private string SolutionDir => _getSolutionDir();
        private ITaefTestAdapterSettings Settings => _getSettings();

        public PlaceholderReplacer(Func<string> getSolutionDir, Func<ITaefTestAdapterSettings> getSettings, HelperFilesCache helperFilesCache, ILogger logger)
        {
            _getSolutionDir = getSolutionDir;
            _getSettings = getSettings;
            _helperFilesCache = helperFilesCache;
            _logger = logger;
        }


        public const string TeExecutablePlaceholders = "Placeholders:\n" +
                                                       DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                       DescriptionOfPlatformNamePlaceholder + "\n" +
                                                       DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                       DescriptionOfTestDllDirPlaceHolder + "\n" +
                                                       DescriptionOfTestDllPlaceHolder + "\n" +
                                                       DescriptionOfEnvVarPlaceholders;

        public string ReplaceTeExecutablePlaceholders(string teExecutable, string testDll)
        {
            teExecutable = ReplaceTestDllPlaceholders(teExecutable?.Trim(), testDll);
            teExecutable = ReplacePlatformAndConfigurationPlaceholders(teExecutable, testDll);
            teExecutable = ReplaceSolutionDirPlaceholder(teExecutable, testDll);
            teExecutable = ReplaceEnvironmentVariables(teExecutable);
            teExecutable = ReplaceHelperFileSettings(teExecutable, testDll);

            CheckForRemainingPlaceholders(teExecutable, SettingsWrapper.OptionTeExecutable);

            return teExecutable;
        }


        public const string AdditionalPdbsPlaceholders = "Placeholders:\n" +
                                                         DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                         DescriptionOfPlatformNamePlaceholder + "\n" +
                                                         DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                         DescriptionOfTestDllDirPlaceHolder + "\n" +
                                                         DescriptionOfTestDllPlaceHolder + "\n" +
                                                         DescriptionOfEnvVarPlaceholders;

        public string ReplaceAdditionalPdbsPlaceholders(string pdb, string testDll)
        {
            pdb = ReplaceTestDllPlaceholders(pdb.Trim(), testDll);
            pdb = ReplacePlatformAndConfigurationPlaceholders(pdb, testDll);
            pdb = ReplaceSolutionDirPlaceholder(pdb, testDll);
            pdb = ReplaceEnvironmentVariables(pdb);
            pdb = ReplaceHelperFileSettings(pdb, testDll);

            CheckForRemainingPlaceholders(pdb, SettingsWrapper.OptionAdditionalPdbs);

            return pdb;
        }


        public const string WorkingDirPlaceholders = "Placeholders:\n" +
                                                     DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                     DescriptionOfPlatformNamePlaceholder + "\n" +
                                                     DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                     DescriptionOfTestDllDirPlaceHolder + "\n" +
                                                     DescriptionOfTestDllPlaceHolder + "\n" +
                                                     DescriptionOfTestDirPlaceholder + TestExecutionOnly + "\n" +
                                                     DescriptionOfThreadIdPlaceholder + TestExecutionOnly + "\n" +
                                                     DescriptionOfEnvVarPlaceholders;

        public string ReplaceWorkingDirPlaceholdersForDiscovery(string workingDir, string testDll)
        {
            workingDir = ReplaceTestDllPlaceholders(workingDir, testDll);
            workingDir = RemoveTestDirAndThreadIdPlaceholders(workingDir);
            workingDir = ReplacePlatformAndConfigurationPlaceholders(workingDir, testDll);
            workingDir = ReplaceSolutionDirPlaceholder(workingDir, testDll);
            workingDir = ReplaceEnvironmentVariables(workingDir);
            workingDir = ReplaceHelperFileSettings(workingDir, testDll);

            CheckForRemainingPlaceholders(workingDir, SettingsWrapper.OptionWorkingDir);

            return workingDir;
        }

        public string ReplaceWorkingDirPlaceholdersForExecution(string workingDir, string testDll,
            string testDirectory, int threadId)
        {
            workingDir = ReplaceTestDirAndThreadIdPlaceholders(workingDir, testDirectory, threadId);
            workingDir = ReplaceTestDllPlaceholders(workingDir, testDll);
            workingDir = ReplacePlatformAndConfigurationPlaceholders(workingDir, testDll);
            workingDir = ReplaceSolutionDirPlaceholder(workingDir, testDll);
            workingDir = ReplaceEnvironmentVariables(workingDir);
            workingDir = ReplaceHelperFileSettings(workingDir, testDll);

            CheckForRemainingPlaceholders(workingDir, SettingsWrapper.OptionWorkingDir);

            return workingDir;
        }


        public const string PathExtensionPlaceholders = "Placeholders:\n" +
                                                        DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                        DescriptionOfPlatformNamePlaceholder + "\n" +
                                                        DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                        DescriptionOfTestDllDirPlaceHolder + "\n" +
                                                        DescriptionOfTestDllPlaceHolder + "\n" +
                                                        DescriptionOfEnvVarPlaceholders;

        public string ReplacePathExtensionPlaceholders(string pathExtension, string testDll)
        {
            pathExtension = ReplaceTestDllPlaceholders(pathExtension, testDll);
            pathExtension = ReplacePlatformAndConfigurationPlaceholders(pathExtension, testDll);
            pathExtension = ReplaceSolutionDirPlaceholder(pathExtension, testDll);
            pathExtension = ReplaceEnvironmentVariables(pathExtension);
            pathExtension = ReplaceHelperFileSettings(pathExtension, testDll);

            CheckForRemainingPlaceholders(pathExtension, SettingsWrapper.OptionPathExtension);

            return pathExtension;
        }


        public const string EnvironmentPlaceholders = "Placeholders:\n" +
                                                        DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                        DescriptionOfPlatformNamePlaceholder + "\n" +
                                                        DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                        DescriptionOfTestDllDirPlaceHolder + "\n" +
                                                        DescriptionOfTestDllPlaceHolder + "\n" +
                                                        DescriptionOfTestDirPlaceholder + TestExecutionOnly + "\n" +
                                                        DescriptionOfThreadIdPlaceholder + TestExecutionOnly + "\n" +
                                                        DescriptionOfEnvVarPlaceholders;

        public string ReplaceEnvironmentVariablesPlaceholdersForExecution(string environmentVariables, string testDll, string testDirectory, int threadId)
        {
            environmentVariables =
                ReplaceTestDirAndThreadIdPlaceholders(environmentVariables, testDirectory, threadId);
            environmentVariables = ReplaceTestDllPlaceholders(environmentVariables, testDll);
            environmentVariables = ReplacePlatformAndConfigurationPlaceholders(environmentVariables, testDll);
            environmentVariables = ReplaceSolutionDirPlaceholder(environmentVariables, testDll);
            environmentVariables = ReplaceEnvironmentVariables(environmentVariables);
            environmentVariables = ReplaceHelperFileSettings(environmentVariables, testDll);

            CheckForRemainingPlaceholders(environmentVariables, SettingsWrapper.OptionEnvironmentVariables);

            return environmentVariables;
        }

        public string ReplaceEnvironmentVariablesPlaceholdersForDiscovery(string environmentVariables, string testDll)
        {
            environmentVariables = ReplaceTestDllPlaceholders(environmentVariables, testDll);
            environmentVariables = RemoveTestDirAndThreadIdPlaceholders(environmentVariables);
            environmentVariables = ReplacePlatformAndConfigurationPlaceholders(environmentVariables, testDll);
            environmentVariables = ReplaceSolutionDirPlaceholder(environmentVariables, testDll);
            environmentVariables = ReplaceEnvironmentVariables(environmentVariables);
            environmentVariables = ReplaceHelperFileSettings(environmentVariables, testDll);

            CheckForRemainingPlaceholders(environmentVariables, SettingsWrapper.OptionEnvironmentVariables);

            return environmentVariables;
        }


        public const string AdditionalTestExecutionParamPlaceholders = "Placeholders:\n" +
                                                                       DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                                       DescriptionOfPlatformNamePlaceholder + "\n" +
                                                                       DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                                       DescriptionOfTestDllDirPlaceHolder + "\n" +
                                                                       DescriptionOfTestDllPlaceHolder + "\n" +
                                                                       DescriptionOfTestDirPlaceholder + TestExecutionOnly + "\n" +
                                                                       DescriptionOfThreadIdPlaceholder + TestExecutionOnly + "\n" +
                                                                       DescriptionOfEnvVarPlaceholders;

        public string ReplaceAdditionalTestExecutionParamPlaceholdersForDiscovery(string additionalTestExecutionParam, string testDll)
        {
            additionalTestExecutionParam = ReplaceTestDllPlaceholders(additionalTestExecutionParam, testDll);
            additionalTestExecutionParam = RemoveTestDirAndThreadIdPlaceholders(additionalTestExecutionParam);
            additionalTestExecutionParam = ReplacePlatformAndConfigurationPlaceholders(additionalTestExecutionParam, testDll);
            additionalTestExecutionParam = ReplaceSolutionDirPlaceholder(additionalTestExecutionParam, testDll);
            additionalTestExecutionParam = ReplaceEnvironmentVariables(additionalTestExecutionParam);
            additionalTestExecutionParam = ReplaceHelperFileSettings(additionalTestExecutionParam, testDll);

            CheckForRemainingPlaceholders(additionalTestExecutionParam, SettingsWrapper.OptionAdditionalTestExecutionParam);

            return additionalTestExecutionParam;
        }

        public string ReplaceAdditionalTestExecutionParamPlaceholdersForExecution(string additionalTestExecutionParam, string testDll, string testDirectory, int threadId)
        {
            additionalTestExecutionParam =
                ReplaceTestDirAndThreadIdPlaceholders(additionalTestExecutionParam, testDirectory, threadId);
            additionalTestExecutionParam = ReplaceTestDllPlaceholders(additionalTestExecutionParam, testDll);
            additionalTestExecutionParam = ReplacePlatformAndConfigurationPlaceholders(additionalTestExecutionParam, testDll);
            additionalTestExecutionParam = ReplaceSolutionDirPlaceholder(additionalTestExecutionParam, testDll);
            additionalTestExecutionParam = ReplaceEnvironmentVariables(additionalTestExecutionParam);
            additionalTestExecutionParam = ReplaceHelperFileSettings(additionalTestExecutionParam, testDll);

            CheckForRemainingPlaceholders(additionalTestExecutionParam, SettingsWrapper.OptionAdditionalTestExecutionParam);

            return additionalTestExecutionParam;
        }


        public const string BatchesPlaceholders = "Placeholders:\n" +
                                                  DescriptionOfSolutionDirPlaceHolder + "\n" +
                                                  DescriptionOfPlatformNamePlaceholder + "\n" +
                                                  DescriptionOfConfigurationNamePlaceholder + "\n" +
                                                  DescriptionOfTestDirPlaceholder + "\n" +
                                                  DescriptionOfThreadIdPlaceholder + "\n" +
                                                  DescriptionOfEnvVarPlaceholders;


        public string ReplaceSetupBatchPlaceholders(string batch, string testDirectory, int threadId)
        {
            batch = ReplaceBatchPlaceholders(batch, testDirectory, threadId);
            CheckForRemainingPlaceholders(batch, SettingsWrapper.OptionBatchForTestSetup);
            return batch;
        }

        public string ReplaceTeardownBatchPlaceholders(string batch, string testDirectory, int threadId)
        {
            batch = ReplaceBatchPlaceholders(batch, testDirectory, threadId);
            CheckForRemainingPlaceholders(batch, SettingsWrapper.OptionBatchForTestTeardown);
            return batch;
        }

        private string ReplaceBatchPlaceholders(string batch, string testDirectory, int threadId)
        {
            batch = ReplaceTestDirAndThreadIdPlaceholders(batch, testDirectory, threadId);
            batch = ReplacePlatformAndConfigurationPlaceholders(batch);
            batch = ReplaceSolutionDirPlaceholder(batch);
            batch = ReplaceEnvironmentVariables(batch);
            return batch;
        }


        private string ReplaceSolutionDirPlaceholder(string theString, string testDll = null)
        {
            return string.IsNullOrWhiteSpace(theString)
                ? ""
                : ReplaceValueWithHelperFile(theString, SolutionDirPlaceholder, SolutionDir,
                    testDll, nameof(ITaefTestAdapterSettings.SolutionDir));
        }

        private string ReplacePlatformAndConfigurationPlaceholders(string theString, string testDll = null)
        {
            if (string.IsNullOrWhiteSpace(theString))
                return "";

            theString = ReplaceValueWithHelperFile(theString, PlatformNamePlaceholder, Settings.PlatformName,
                testDll, nameof(ITaefTestAdapterSettings.PlatformName));
            theString = ReplaceValueWithHelperFile(theString, ConfigurationNamePlaceholder, Settings.ConfigurationName,
                testDll, nameof(ITaefTestAdapterSettings.ConfigurationName));
            return theString;
        }

        private string ReplaceTestDllPlaceholders(string theString, string testDll)
        {
            return string.IsNullOrWhiteSpace(theString)
                ? ""
                : theString
                    // ReSharper disable once PossibleNullReferenceException
                    .Replace(TestDllDirPlaceholder, new FileInfo(testDll).Directory.FullName)
                    .Replace(TestDllPlaceholder, testDll);
        }

        private string ReplaceTestDirAndThreadIdPlaceholders(string theString, string testDirectory, int threadId)
        {
            return ReplaceTestDirAndThreadIdPlaceholders(theString, testDirectory, threadId.ToString());
        }

        private string RemoveTestDirAndThreadIdPlaceholders(string theString)
        {
            return ReplaceTestDirAndThreadIdPlaceholders(theString, "", "");
        }

        private string ReplaceEnvironmentVariables(string theString)
        {
            return string.IsNullOrWhiteSpace(theString)
                ? ""
                : Environment.ExpandEnvironmentVariables(theString);
        }

        private string ReplaceTestDirAndThreadIdPlaceholders(string theString, string testDirectory, string threadId)
        {
            return string.IsNullOrWhiteSpace(theString)
                ? ""
                : theString
                    .Replace(TestDirPlaceholder, testDirectory)
                    .Replace(ThreadIdPlaceholder, threadId);
        }

        private string ReplaceHelperFileSettings(string theString, string testDll)
        {
            var replacementMap = _helperFilesCache.GetReplacementsMap(testDll);
            foreach (var nameValuePair in replacementMap)
            {
                theString = theString.Replace($"$({nameValuePair.Key})", nameValuePair.Value);
            }

            return theString;
        }

        private string ReplaceValueWithHelperFile(string theString, string placeholder, string value, string testDll,
            string settingName)
        {
            if (string.IsNullOrWhiteSpace(value) && testDll != null)
            {
                var map = _helperFilesCache.GetReplacementsMap(testDll);
                if (map.TryGetValue(settingName, out string helperFileValue))
                {
                    value = helperFileValue;
                }
            }

            return string.IsNullOrWhiteSpace(value)
                ? theString.Replace(placeholder, "")
                : theString.Replace(placeholder, value);
        }

        private void CheckForRemainingPlaceholders(string theString, string optionName)
        {
            var matches = PlaceholdersRegex.Matches(theString);
            if (matches.Count > 0)
            {

                var placeholders = matches.Cast<Match>().Select(m => m.Value).Distinct().OrderBy(s => s);
                string message = $"Option '{optionName}': Apparently, the following placeholders could not be replaced. {string.Join(", ", placeholders)}";
                _logger.LogWarning(message);
            }
        }

    }

}
