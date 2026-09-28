// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Xml.XPath;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using TaefTestAdapter.Settings;
using Constants = Microsoft.VisualStudio.TestPlatform.ObjectModel.Constants;

namespace TaefTestAdapter.TestAdapter.Settings
{

    /// <summary>
    /// Provides the adapter's settings (node <c>&lt;TaefTestAdapterSettings&gt;</c>) to Visual Studio's Test Explorer.
    /// The settings of the user's .runsettings file are merged with the settings of the solution settings file
    /// (<c>&lt;solution name&gt;.taef.runsettings</c>, located next to the solution file) and the adapter's Visual
    /// Studio options (see <see cref="IGlobalRunSettings"/>), in this order of precedence.
    /// </summary>
    [Export(typeof(IRunSettingsService))]
    [SettingsName(TaefConstants.SettingsName)]
    public class RunSettingsService : IRunSettingsService
    {
        public string Name => TaefConstants.SettingsName;

        private readonly IGlobalRunSettings _globalRunSettings;

        [ImportingConstructor]
        public RunSettingsService([Import(typeof(IGlobalRunSettings))] IGlobalRunSettings globalRunSettings)
        {
            _globalRunSettings = globalRunSettings;
        }

        public IXPathNavigable AddRunSettings(IXPathNavigable runSettingDocument,
            IRunSettingsConfigurationInfo configurationInfo, ILogger logger)
        {
            XPathNavigator runSettingsNavigator = runSettingDocument.CreateNavigator();
            Debug.Assert(runSettingsNavigator != null, "userRunSettingsNavigator == null!");
            if (!runSettingsNavigator.MoveToChild(Constants.RunSettingsName, ""))
            {
                logger.Log(MessageLevel.Warning, "RunSettingsDocument does not contain a RunSettings node! Canceling settings merging...");
                return runSettingsNavigator;
            }

            var settingsContainer = new RunSettingsContainer();

            try
            {
                if (settingsContainer.GetUnsetValuesFrom(runSettingsNavigator))
                {
                    runSettingsNavigator.DeleteSelf(); // this node is to be replaced by the final run settings
                }
            }
            catch (InvalidRunSettingsException e)
            {
                logger.Log(MessageLevel.Error,
                    $"Invalid run settings, node {TaefConstants.SettingsName} is ignored (the settings of the solution settings file and the Visual Studio options are used instead): {GetMessage(e)}");

                // the invalid settings are replaced by the settings of the solution settings file and the VS options
                if (runSettingsNavigator.LocalName == TaefConstants.SettingsName)
                {
                    runSettingsNavigator.DeleteSelf();
                }
            }

            GetValuesFromSolutionSettingsFile(settingsContainer, logger);

            foreach (var projectSettings in settingsContainer.ProjectSettings)
            {
                projectSettings.GetUnsetValuesFrom(settingsContainer.SolutionSettings);
            }

            GetValuesFromGlobalSettings(settingsContainer);

            runSettingsNavigator.MoveToRoot();
            runSettingsNavigator.MoveToChild(Constants.RunSettingsName, "");
            runSettingsNavigator.AppendChild(settingsContainer.ToXml().CreateNavigator());

            runSettingsNavigator.MoveToRoot();
            return runSettingsNavigator;
        }

        private void GetValuesFromSolutionSettingsFile(RunSettingsContainer settingsContainer, ILogger logger)
        {
            string solutionRunSettingsFile = null;
            try
            {
                solutionRunSettingsFile = GetSolutionSettingsXmlFile();
                if (!string.IsNullOrEmpty(solutionRunSettingsFile) && File.Exists(solutionRunSettingsFile))
                {
                    if (!settingsContainer.GetUnsetValuesFrom(solutionRunSettingsFile))
                    {
                        logger.Log(MessageLevel.Warning,
                            $"Solution test settings file found at '{solutionRunSettingsFile}', but does not contain {Constants.RunSettingsName} node");
                    }
                }
            }
            catch (InvalidRunSettingsException e)
            {
                logger.Log(MessageLevel.Error,
                    $"Solution test settings file could not be parsed, check file '{solutionRunSettingsFile}'. Error message: {GetMessage(e)}");
            }
            catch (Exception e)
            {
                logger.Log(MessageLevel.Error,
                    $"Solution test settings file could not be parsed, check file '{solutionRunSettingsFile}'. Exception:{Environment.NewLine}{e}");
            }
        }

        private void GetValuesFromGlobalSettings(RunSettingsContainer settingsContainer)
        {
            GetValuesFromGlobalSettings(settingsContainer.SolutionSettings);
            foreach (RunSettings projectSettings in settingsContainer.ProjectSettings)
            {
                GetValuesFromGlobalSettings(projectSettings);
            }
        }

        private void GetValuesFromGlobalSettings(RunSettings settings)
        {
            // these settings must not be provided through runsettings files. If they still
            // are, the following makes sure that they are ignored
            settings.SkipOriginCheck = null;

            // internal
            settings.DebuggingNamedPipeId = null;
            settings.SolutionDir = null;
            settings.PlatformName = null;
            settings.ConfigurationName = null;

            settings.GetUnsetValuesFrom(_globalRunSettings.RunSettings);
        }

        /// <returns>
        /// The message of <paramref name="e"/>, followed by the message of its inner exception unless already contained:
        /// the message of an <see cref="InvalidRunSettingsException"/> caused by a violation of the settings schema does not
        /// tell which element or value is invalid, the message of its inner exception does.
        /// </returns>
        private static string GetMessage(InvalidRunSettingsException e)
        {
            string innerMessage = e.InnerException?.Message;
            return string.IsNullOrEmpty(innerMessage) || e.Message.Contains(innerMessage)
                ? e.Message
                : $"{e.Message}: {innerMessage}";
        }

        /// <returns>The path of the solution settings file (which might not exist), or null if no solution is open</returns>
        // protected for testing
        protected virtual string GetSolutionSettingsXmlFile()
        {
            string solutionFile = ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var solution = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
                if (solution == null || ErrorHandler.Failed(solution.GetSolutionInfo(out _, out string file, out _)))
                    return null;
                return file;
            });

            return string.IsNullOrEmpty(solutionFile)
                ? null
                : Path.ChangeExtension(solutionFile, TaefConstants.SettingsExtension);
        }

    }

}