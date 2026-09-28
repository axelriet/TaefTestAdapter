// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.Framework;
using TaefTestAdapter.TestAdapter.Settings;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Functionality shared by <see cref="TestDiscoverer"/> and <see cref="TestExecutor"/> (settings, logger,
    /// summary of errors).
    /// </summary>
    public static class CommonFunctions
    {
        public const string TaefSettingsEnvVariable = "TAEF_ADAPTER_FALLBACK_SETTINGS";
        private const int MinWorkerThreads = 32;

        public static void ReportErrors(ILogger logger, string phase, OutputMode outputMode, SummaryMode summaryMode)
        {
            if (summaryMode == SummaryMode.Never)
                return;

            bool hasErrors = logger.GetMessages(Severity.Error).Count > 0;
            if (!hasErrors && summaryMode == SummaryMode.Error)
                return;

            IList<string> errors = logger.GetMessages(Severity.Error, Severity.Warning);
            if (!errors.Any())
                return;

            string hint = outputMode > OutputMode.Info
                ? ""
                : " (enable debug mode for more information)";
            string jointErrors = string.Join(Environment.NewLine, errors);

            string message = $"{Environment.NewLine}================{Environment.NewLine}" 
                + $"The following warnings and errors occurred during {phase}{hint}:{Environment.NewLine}" 
                + jointErrors;

            if (hasErrors)
                logger.LogError(message);
            else
                logger.LogWarning(message);
        }

        public static void CreateEnvironment(IRunSettings runSettings, IMessageLogger messageLogger, out ILogger logger, out SettingsWrapper settings, string solutionDir = null)
        {
            if (string.IsNullOrWhiteSpace(solutionDir))
            {
                solutionDir = null;
            }

            var settingsProvider = SafeGetRunSettingsProvider(runSettings, messageLogger);

            var ourRunSettings = GetRunSettingsContainer(settingsProvider, messageLogger);
            foreach (RunSettings projectSettings in ourRunSettings.ProjectSettings)
            {
                projectSettings.GetUnsetValuesFrom(ourRunSettings.SolutionSettings);
            }

            var settingsWrapper = new SettingsWrapper(ourRunSettings, solutionDir);

            var loggerAdapter = new VsTestFrameworkLogger(messageLogger, () => settingsWrapper.OutputMode, 
                () => settingsWrapper.TimestampMode, () => settingsWrapper.SeverityMode, () => settingsWrapper.PrefixOutputWithTaef);
            settingsWrapper.RegexTraitParser = new RegexTraitParser(loggerAdapter);
            settingsWrapper.EnvironmentVariablesParser = new EnvironmentVariablesParser(loggerAdapter);
            settingsWrapper.HelperFilesCache = new HelperFilesCache(loggerAdapter);

            settings = settingsWrapper;
            logger = loggerAdapter;

            SpeedupThreadPoolHack(logger);
        }

        private static RunSettingsProvider SafeGetRunSettingsProvider(IRunSettings runSettings, IMessageLogger messageLogger)
        {
            try
            {
                return runSettings.GetSettings(TaefConstants.SettingsName) as RunSettingsProvider;
            }
            catch (Exception e)
            {
                string errorMessage =
                    $"ERROR: Visual Studio test framework failed to provide settings. Error message: {e.Message}";
                // if fallback settings are configured, we do not want to make the tests fail
                var level = AreFallbackSettingsConfigured() ? TestMessageLevel.Informational : TestMessageLevel.Error;

                messageLogger.SendMessage(level, errorMessage);
                return null;
            }
        }

        private static RunSettingsContainer GetRunSettingsContainer(RunSettingsProvider settingsProvider,
            IMessageLogger messageLogger)
        {
            RunSettingsContainer ourRunSettings;
            if (settingsProvider != null)
            {
                // e.g. vstest.console.exe or the NuGet package in VS (the VS extension replaces invalid settings itself)
                if (settingsProvider.LoadError != null)
                    messageLogger.SendMessage(TestMessageLevel.Error,
                        $"ERROR: Invalid run settings, node {TaefConstants.SettingsName} is ignored and default settings are used: {settingsProvider.LoadError}");
                ourRunSettings = settingsProvider.SettingsContainer;
            }
            else
            {
                ourRunSettings = GetRunSettingsFromEnvVariable(messageLogger);
                if (ourRunSettings == null)
                {
                    messageLogger.SendMessage(TestMessageLevel.Warning, "Warning: Using default settings.");
                    ourRunSettings = new RunSettingsContainer();
                }
            }

            return ourRunSettings;
        }

        private static RunSettingsContainer GetRunSettingsFromEnvVariable(IMessageLogger messageLogger)
        {
            string settingsFile;
            try
            {
                settingsFile = Environment.GetEnvironmentVariable(TaefSettingsEnvVariable);
                if (settingsFile == null)
                {
                    messageLogger.SendMessage(TestMessageLevel.Informational, $"No settings file provided through env variable {TaefSettingsEnvVariable}");
                    return null;
                }
            }
            catch (Exception e)
            {
                messageLogger.SendMessage(TestMessageLevel.Error, $"ERROR: Exception while trying to access env variable {TaefSettingsEnvVariable}, message: {e.Message}");
                return null;
            }
                
            try
            {
                if (!File.Exists(settingsFile))
                {
                    messageLogger.SendMessage(TestMessageLevel.Warning,
                        $"Warning: Settings file is provided through env variable {TaefSettingsEnvVariable}, but file '{settingsFile}' does not exist");
                    return null;
                }

                var settingsContainer = new RunSettingsContainer();
                if (!settingsContainer.GetUnsetValuesFrom(settingsFile))
                {
                    messageLogger.SendMessage(TestMessageLevel.Warning,
                        $"Warning: Settings file is provided through env variable {TaefSettingsEnvVariable}, but file '{settingsFile}' could not be loaded");
                    return null;
                }

                messageLogger.SendMessage(TestMessageLevel.Informational,
                    $"Using fallback settings from file '{settingsFile}' (provided through env variable {TaefSettingsEnvVariable})");
                return settingsContainer;
            }
            catch (Exception e)
            {
                messageLogger.SendMessage(TestMessageLevel.Error, $"ERROR: Settings file is provided through env variable {TaefSettingsEnvVariable}, but an exception occurred while trying to read file '{settingsFile}'. Exception message: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs the version of Visual Studio (see <see cref="VsVersionUtils"/>), and warns if it is not supported.
        /// </summary>
        public static void LogVisualStudioVersion(ILogger logger)
        {
            VsVersion version = VsVersionUtils.VsVersion;
            if (version == VsVersion.Unknown)
            {
                logger.DebugInfo("Could not identify the Visual Studio version");
                return;
            }

            if (!version.IsSupported())
            {
                string versionName = version.Year() > 0 ? version.Year().ToString() : version.VersionString();
                logger.LogWarning($"{Strings.Instance.ExtensionName} requires Visual Studio {VsVersionUtils.FirstSupportedVersion.Year()} or later, but seems to be running in Visual Studio {versionName} ({VsVersionUtils.VersionSource})");
                return;
            }

            logger.DebugInfo($"Visual Studio version: {version} ({VsVersionUtils.VersionSource})");
        }

        private static bool AreFallbackSettingsConfigured()
        {
            try
            {
                return Environment.GetEnvironmentVariable(TaefSettingsEnvVariable) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // after https://stackoverflow.com/questions/22036365/newly-created-threads-using-task-factory-startnew-starts-very-slowly/22074892#22074892
        private static void SpeedupThreadPoolHack(ILogger logger)
        {
            int workerThreadsMin, workerThreadsMax, completionPortThreadsMin, completionPortThreadsMax;
            ThreadPool.GetMinThreads(out workerThreadsMin, out completionPortThreadsMin);
            ThreadPool.GetMaxThreads(out workerThreadsMax, out completionPortThreadsMax);

            ThreadPool.SetMinThreads(MinWorkerThreads, completionPortThreadsMin);
            logger.VerboseInfo($"Tuned ThreadPool. MinThreads: ({workerThreadsMin}->{MinWorkerThreads}, {completionPortThreadsMin}); MaxThreads: ({workerThreadsMax}, {completionPortThreadsMax})");
        }
    }
}