// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Threading;
using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.TestAdapter.Settings;
using TaefTestAdapter.VsPackage.Commands;
using TaefTestAdapter.VsPackage.Debugging;
using TaefTestAdapter.VsPackage.Helpers;
using TaefTestAdapter.VsPackage.OptionsPages;
using SolutionConfiguration2 = EnvDTE80.SolutionConfiguration2;
using Task = System.Threading.Tasks.Task;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// The Visual Studio package of the Test Adapter for TAEF. It
    /// <list type="bullet">
    /// <item>provides the options pages (Tools/Options/Test Adapter for TAEF) and the toolbar of the adapter,</item>
    /// <item>makes the options available to the test adapter (through <see cref="IGlobalRunSettingsInternal"/>, which is
    /// merged into the run settings by <see cref="RunSettingsService"/>),</item>
    /// <item>hosts the service which attaches the debugger to TE.exe if tests are debugged with
    /// <see cref="TaefTestAdapter.ProcessExecution.DebuggerKind.Native"/> or
    /// <see cref="TaefTestAdapter.ProcessExecution.DebuggerKind.ManagedAndNative"/> debugger engine.</item>
    /// </list>
    /// The package is loaded in the background as soon as a solution or folder is opened.
    /// The wizard of the project and item templates (<see cref="Templates.TaefTemplateWizard"/>) is part of the package's
    /// assembly, but Visual Studio loads it without the package.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    // the resource ids refer to the category and page names in Resources\VSPackage.resx
    [ProvideOptionPage(typeof(GeneralOptionsDialogPage), OptionsCategoryName, SettingsWrapper.PageGeneralName, 120, 121, true)]
    [ProvideOptionPage(typeof(TestDiscoveryOptionsDialogPage), OptionsCategoryName, SettingsWrapper.PageTestDiscovery, 120, 122, true)]
    [ProvideOptionPage(typeof(TestExecutionOptionsDialogPage), OptionsCategoryName, SettingsWrapper.PageTestExecution, 120, 123, true)]
    [ProvideOptionPage(typeof(TaefOptionsDialogPage), OptionsCategoryName, SettingsWrapper.PageTaefName, 120, 124, true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class TaefTestAdapterPackage : AsyncPackage, ITaefTestAdapterPackage
    {
        /// <summary>
        /// GUID of the package; must be identical to the GUID of symbol guidTaefTestAdapterPackage in TaefTestAdapterPackage.vsct.
        /// </summary>
        public const string PackageGuidString = "62ca60da-de1d-4838-9730-5bc75b592f7c";

        /// <summary>
        /// Name of the options category in Tools/Options.
        /// </summary>
        public const string OptionsCategoryName = "Test Adapter for TAEF";

        /// <summary>
        /// Id of the named pipe of the debugger attacher service as published to the test adapter (setting
        /// DebuggingNamedPipeId); null unless the service host has been opened, so that the adapter rejects debugging with
        /// the native debugger engines up front (instead of failing to attach the debugger to TE.exe).
        /// </summary>
        private string _debuggingNamedPipeId;

        private IGlobalRunSettingsInternal _globalRunSettings;
        private DTE _dte;

        private GeneralOptionsDialogPage _generalOptions;
        private TestDiscoveryOptionsDialogPage _testDiscoveryOptions;
        private TestExecutionOptionsDialogPage _testExecutionOptions;
        private TaefOptionsDialogPage _taefOptions;

        private SolutionChangeListener _solutionChangeListener;
        private DebuggerAttacherServiceHost _debuggerAttacherServiceHost;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            var componentModel = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            Assumes.Present(componentModel);
            _globalRunSettings = componentModel.GetService<IGlobalRunSettingsInternal>();

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            object solutionBuildManagerService = await GetServiceAsync(typeof(SVsSolutionBuildManager));
            object dteService = await GetServiceAsync(typeof(DTE));

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var solutionBuildManager = solutionBuildManagerService as IVsSolutionBuildManager;
            _dte = dteService as DTE;

            InitializeOptions();
            InitializeCommands(commandService);
            InitializeSolutionChangeListener(solutionBuildManager);
            InitializeDebuggerAttacherService();
        }

        private void InitializeOptions()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _generalOptions = (GeneralOptionsDialogPage) GetDialogPage(typeof(GeneralOptionsDialogPage));
            _testDiscoveryOptions = (TestDiscoveryOptionsDialogPage) GetDialogPage(typeof(TestDiscoveryOptionsDialogPage));
            _testExecutionOptions = (TestExecutionOptionsDialogPage) GetDialogPage(typeof(TestExecutionOptionsDialogPage));
            _taefOptions = (TaefOptionsDialogPage) GetDialogPage(typeof(TaefOptionsDialogPage));

            UpdateGlobalRunSettings();

            _generalOptions.PropertyChanged += OptionsChanged;
            _testDiscoveryOptions.PropertyChanged += OptionsChanged;
            _testExecutionOptions.PropertyChanged += OptionsChanged;
            _taefOptions.PropertyChanged += OptionsChanged;
        }

        private void InitializeCommands(OleMenuCommandService commandService)
        {
            if (commandService == null)
            {
                new ActivityLogLogger(() => OutputMode.Verbose).LogWarning("Menu command service is not available, toolbar of the Test Adapter for TAEF will not work");
                return;
            }

            SwitchRunInProcessOptionCommand.Initialize(this, commandService);
            SwitchBreakOnErrorOptionCommand.Initialize(this, commandService);
            SwitchParallelExecutionOptionCommand.Initialize(this, commandService);
            SwitchPrintTestOutputOptionCommand.Initialize(this, commandService);
        }

        private void InitializeSolutionChangeListener(IVsSolutionBuildManager solutionBuildManager)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // solution dir, platform and configuration are part of the global run settings (for the placeholders
            // $(SolutionDir), $(PlatformName) and $(ConfigurationName)) and thus need to be updated if they change
            _solutionChangeListener = new SolutionChangeListener(solutionBuildManager, UpdateGlobalRunSettings);
        }

        private void InitializeDebuggerAttacherService()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var logger = new ActivityLogLogger(() => _generalOptions.OutputMode);
            var debuggerAttacher = new VsDebuggerAttacher(this, JoinableTaskFactory);
            InitializeDebuggerAttacherService(Guid.NewGuid().ToString(), debuggerAttacher, logger);
        }

        /// <summary>
        /// Opens the debugger attacher service on the named pipe with id <paramref name="pipeId"/>, and publishes the id to
        /// the test adapter (setting DebuggingNamedPipeId) if and only if the service is available.
        /// </summary>
        private void InitializeDebuggerAttacherService(string pipeId, IDebuggerAttacher debuggerAttacher, ILogger logger)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _debuggerAttacherServiceHost = OpenDebuggerAttacherServiceHost(pipeId, debuggerAttacher, logger);
            _debuggingNamedPipeId = _debuggerAttacherServiceHost == null ? null : pipeId;
            UpdateGlobalRunSettings();
        }

        /// <returns>The opened service host, or null if it could not be opened (which has been logged as error).</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "the package must be usable (options, toolbar) even if the service can not be opened")]
        private static DebuggerAttacherServiceHost OpenDebuggerAttacherServiceHost(string pipeId, IDebuggerAttacher debuggerAttacher, ILogger logger)
        {
            DebuggerAttacherServiceHost host = null;
            try
            {
                host = new DebuggerAttacherServiceHost(pipeId, debuggerAttacher, logger);
                host.Open();
                return host;
            }
            catch (Exception e)
            {
                logger.LogError(
                    $"Could not open the debugger attacher service, so tests can not be debugged with debugger engine '{DebuggerKindConverter.Native}' " +
                    $"or '{DebuggerKindConverter.ManagedAndNative}' (debugger engine '{DebuggerKindConverter.VsTestFramework}' still works):{Environment.NewLine}{e}");
                host?.Abort();
                return null;
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed",
            MessageId = nameof(_debuggerAttacherServiceHost), Justification = "Close() includes Dispose()")]
        protected override void Dispose(bool disposing)
        {
            // Visual Studio disposes packages on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            if (disposing)
            {
                _solutionChangeListener?.Dispose();
                _solutionChangeListener = null;

                _generalOptions?.Dispose();
                _testDiscoveryOptions?.Dispose();
                _testExecutionOptions?.Dispose();
                _taefOptions?.Dispose();

                try
                {
                    _debuggerAttacherServiceHost?.Close();
                }
                catch (CommunicationException)
                {
                    _debuggerAttacherServiceHost?.Abort();
                }
                catch (TimeoutException)
                {
                    _debuggerAttacherServiceHost?.Abort();
                }
            }
            base.Dispose(disposing);
        }

        #region ITaefTestAdapterPackage (toolbar)

        public bool BreakOnError
        {
            get => _taefOptions.BreakOnError;
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _taefOptions.BreakOnError = value;
                SaveAndRefreshVsUi(_taefOptions);
            }
        }

        public bool ParallelTestExecution
        {
            get => _testExecutionOptions.ParallelTestExecution;
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _testExecutionOptions.ParallelTestExecution = value;
                SaveAndRefreshVsUi(_testExecutionOptions);
            }
        }

        public bool PrintTestOutput
        {
            get => _generalOptions.PrintTestOutput;
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _generalOptions.PrintTestOutput = value;
                SaveAndRefreshVsUi(_generalOptions);
            }
        }

        public bool RunInProcess
        {
            get => _taefOptions.RunInProcess;
            set
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _taefOptions.RunInProcess = value;
                SaveAndRefreshVsUi(_taefOptions);
            }
        }

        private void SaveAndRefreshVsUi(DialogPage changedPage)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // options switched by the toolbar are persisted just like options changed in Tools/Options
            changedPage.SaveSettingsToStorage();

            if (GetService(typeof(SVsUIShell)) is IVsUIShell vsShell)
            {
                int hr = vsShell.UpdateCommandUI(Convert.ToInt32(false));
                ErrorHandler.ThrowOnFailure(hr);
            }
        }

        #endregion

        #region Global run settings

        private void OptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateGlobalRunSettings();
        }

        private void UpdateGlobalRunSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _globalRunSettings.RunSettings = GetRunSettingsFromOptionPages();
        }

        private RunSettings GetRunSettingsFromOptionPages()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            GetVisualStudioConfiguration(out string solutionDir, out string platformName, out string configurationName);

            return new RunSettings
            {
                PrintTestOutput = _generalOptions.PrintTestOutput,
                OutputMode = _generalOptions.OutputMode,
                TimestampMode = _generalOptions.TimestampMode,
                SeverityMode = _generalOptions.SeverityMode,
                SummaryMode = _generalOptions.SummaryMode,
                PrefixOutputWithTaef = _generalOptions.PrefixOutputWithTaef,
                SkipOriginCheck = _generalOptions.SkipOriginCheck,

                TestDiscoveryRegex = _testDiscoveryOptions.TestDiscoveryRegex,
                TestDiscoveryTimeoutInSeconds = _testDiscoveryOptions.TestDiscoveryTimeoutInSeconds,
                TraitsRegexesBefore = _testDiscoveryOptions.TraitsRegexesBefore,
                TraitsRegexesAfter = _testDiscoveryOptions.TraitsRegexesAfter,
                ParseSymbolInformation = _testDiscoveryOptions.ParseSymbolInformation,

                AdditionalPdbs = _testExecutionOptions.AdditionalPdbs,
                WorkingDir = _testExecutionOptions.WorkingDir,
                PathExtension = _testExecutionOptions.PathExtension,
                EnvironmentVariables = _testExecutionOptions.EnvironmentVariables,
                AdditionalTestExecutionParam = _testExecutionOptions.AdditionalTestExecutionParam,
                BatchForTestSetup = _testExecutionOptions.BatchForTestSetup,
                BatchForTestTeardown = _testExecutionOptions.BatchForTestTeardown,
                KillProcessesOnCancel = _testExecutionOptions.KillProcessesOnCancel,
                ParallelTestExecution = _testExecutionOptions.ParallelTestExecution,
                MaxNrOfThreads = _testExecutionOptions.MaxNrOfThreads,
                DebuggerKind = _testExecutionOptions.DebuggerKind,
                MissingTestsReportMode = _testExecutionOptions.MissingTestsReportMode,

                TeExecutable = _taefOptions.TeExecutable,
                RunIgnoredTests = _taefOptions.RunIgnoredTests,
                BreakOnError = _taefOptions.BreakOnError,
                NrOfTestRepetitions = _taefOptions.NrOfTestRepetitions,
                TestTimeout = _taefOptions.TestTimeout,
                IsolationLevel = _taefOptions.IsolationLevel,
                RunInProcess = _taefOptions.RunInProcess,

                DebuggingNamedPipeId = _debuggingNamedPipeId,
                SolutionDir = solutionDir,
                PlatformName = platformName,
                ConfigurationName = configurationName
            };
        }

        private void GetVisualStudioConfiguration(out string solutionDir, out string platformName, out string configurationName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            solutionDir = platformName = configurationName = null;
            try
            {
                Solution solution = _dte?.Solution;
                if (solution == null)
                    return;

                string solutionFile = solution.FullName;
                if (!string.IsNullOrEmpty(solutionFile))
                    solutionDir = Path.GetDirectoryName(solutionFile);

                // platform and configuration of the first project with a configuration (solution folders have none),
                // i.e. the project's platform names such as Win32 or x64
                foreach (Project project in solution.Projects)
                {
                    Configuration activeConfiguration = GetActiveConfiguration(project);
                    if (activeConfiguration != null)
                    {
                        platformName = activeConfiguration.PlatformName;
                        configurationName = activeConfiguration.ConfigurationName;
                        return;
                    }
                }

                // no such project (e.g. all projects are located in solution folders): use the solution's configuration
                if (solution.SolutionBuild?.ActiveConfiguration is SolutionConfiguration2 solutionConfiguration)
                {
                    platformName = solutionConfiguration.PlatformName;
                    configurationName = solutionConfiguration.Name;
                }
            }
            catch (Exception e)
            {
                new ActivityLogLogger(() => OutputMode.Verbose)
                    .LogError($"Exception while receiving configuration info from Visual Studio.{Environment.NewLine}{e}");
            }
        }

        private static Configuration GetActiveConfiguration(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return project?.ConfigurationManager?.ActiveConfiguration;
            }
            catch (Exception)
            {
                // projects without configurations (e.g. solution folders, unloaded projects) might throw
                return null;
            }
        }

        #endregion
    }

}
