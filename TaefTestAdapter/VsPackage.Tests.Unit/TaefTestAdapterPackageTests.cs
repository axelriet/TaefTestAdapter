// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.TestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.VsPackage.Debugging;
using TaefTestAdapter.VsPackage.OptionsPages;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// Tests of the package's registration and of the mapping of its options pages to the settings of the test adapter
    /// (which is done without Visual Studio on an uninitialized package object).
    /// </summary>
    [TestClass]
    public class TaefTestAdapterPackageTests
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void Package_Registration_IsAsyncPackageLoadedInBackground()
        {
            Type package = typeof(TaefTestAdapterPackage);

            package.Should().BeDerivedFrom<AsyncPackage>();
            package.GUID.Should().Be(new Guid(TaefTestAdapterPackage.PackageGuidString));

            var registration = package.GetCustomAttribute<PackageRegistrationAttribute>();
            registration.Should().NotBeNull();
            registration.AllowsBackgroundLoading.Should().BeTrue();
            registration.UseManagedResourcesOnly.Should().BeTrue();

            var autoLoads = package.GetCustomAttributes<ProvideAutoLoadAttribute>().ToList();
            autoLoads.Should().HaveCount(2);
            autoLoads.Should().Contain(a => a.LoadGuid == VSConstants.UICONTEXT.SolutionExists_guid && a.Flags == PackageAutoLoadFlags.BackgroundLoad);
            autoLoads.Should().Contain(a => a.LoadGuid == VSConstants.UICONTEXT.FolderOpened_guid && a.Flags == PackageAutoLoadFlags.BackgroundLoad);

            package.GetCustomAttribute<ProvideMenuResourceAttribute>().ResourceID.Should().Be("Menus.ctmenu");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Package_Guids_DifferFromUpstreamAdapter()
        {
            // Visual Studio ships a test adapter built from the code base this adapter derives from (see NOTICE): package,
            // command set and toolbar image GUIDs must differ so that both can be loaded side by side.
            var upstreamAdapterGuids = new[]
            {
                new Guid("e7c90fcb-0943-4908-9ae8-3b6a9d22ec9e"),
                new Guid("e0d9835f-9c16-4d27-a9ad-4df7568650f7"),
                new Guid("bbf62e40-ec35-4371-bd35-abcc599796ca"),
                // package of the adapter shipped with Visual Studio 2026 (from its installed pkgdef)
                new Guid("6fac3232-df1d-400a-95ac-7daeaaee74ac")
            };

            upstreamAdapterGuids.Should().NotContain(new Guid(TaefTestAdapterPackage.PackageGuidString));
            upstreamAdapterGuids.Should().NotContain(GetCommandSet());
            upstreamAdapterGuids.Should().NotContain(GetVsctGuidSymbols().Values);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Vsct_Symbols_MatchPackageAndCommands()
        {
            IDictionary<string, Guid> guidSymbols = GetVsctGuidSymbols();
            guidSymbols["guidTaefTestAdapterPackage"].Should().Be(new Guid(TaefTestAdapterPackage.PackageGuidString));
            guidSymbols["guidTaefTestAdapterPackageCmdSet"].Should().Be(GetCommandSet());

            XDocument vsct = XDocument.Load(VsctFile);
            XNamespace ns = vsct.Root.Name.Namespace;
            var commandIds = vsct.Descendants(ns + "GuidSymbol")
                .Single(s => (string)s.Attribute("name") == "guidTaefTestAdapterPackageCmdSet")
                .Elements(ns + "IDSymbol")
                .ToDictionary(s => (string)s.Attribute("name"), s => Convert.ToInt32((string)s.Attribute("value"), 16));

            string[] commands =
            {
                "SwitchRunInProcessOptionCommand", "SwitchBreakOnErrorOptionCommand",
                "SwitchParallelExecutionOptionCommand", "SwitchPrintTestOutputOptionCommand"
            };
            foreach (string command in commands)
            {
                Type commandType = typeof(TaefTestAdapterPackage).Assembly.GetType($"TaefTestAdapter.VsPackage.Commands.{command}");
                commandType.Should().NotBeNull($"command {command} should exist");
                // ReSharper disable once PossibleNullReferenceException
                int commandId = (int)commandType.GetField("CommandId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                commandIds.Should().Contain(command + "Id", commandId, $"the id of {command} must be identical in code and .vsct file");
            }
        }

        private static string VsctFile => Path.Combine(TestResources.AdapterSolutionDir, "VsPackage", "TaefTestAdapterPackage.vsct");

        private static IDictionary<string, Guid> GetVsctGuidSymbols()
        {
            XDocument vsct = XDocument.Load(VsctFile);
            XNamespace ns = vsct.Root.Name.Namespace;
            return vsct.Descendants(ns + "GuidSymbol")
                .ToDictionary(s => (string)s.Attribute("name"), s => new Guid((string)s.Attribute("value")));
        }

        private static Guid GetCommandSet()
        {
            Type commandType = typeof(TaefTestAdapterPackage).Assembly.GetType("TaefTestAdapter.VsPackage.Commands.AbstractSwitchBooleanOptionCommand");
            // ReSharper disable once PossibleNullReferenceException
            return (Guid)commandType.GetProperty("CommandSet", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Package_OptionsPages_AreRegisteredInTaefCategory()
        {
            var pages = typeof(TaefTestAdapterPackage).GetCustomAttributes<ProvideOptionPageAttribute>().ToList();

            pages.Should().HaveCount(4);
            pages.Should().OnlyContain(p => p.CategoryName == TaefTestAdapterPackage.OptionsCategoryName);
            TaefTestAdapterPackage.OptionsCategoryName.Should().Be("Test Adapter for TAEF");

            var expectedPages = new Dictionary<Type, string>
            {
                { typeof(GeneralOptionsDialogPage), SettingsWrapper.PageGeneralName },
                { typeof(TestDiscoveryOptionsDialogPage), SettingsWrapper.PageTestDiscovery },
                { typeof(TestExecutionOptionsDialogPage), SettingsWrapper.PageTestExecution },
                { typeof(TaefOptionsDialogPage), SettingsWrapper.PageTaefName },
            };
            foreach (var expectedPage in expectedPages)
            {
                pages.Should().ContainSingle(p => p.PageType == expectedPage.Key)
                    .Which.PageName.Should().Be(expectedPage.Value);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetRunSettingsFromOptionPages_AllOptionsChanged_AllSettingsAreMapped()
        {
            var general = OptionsPageFactory.Create<GeneralOptionsDialogPage>();
            var discovery = OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>();
            var execution = OptionsPageFactory.Create<TestExecutionOptionsDialogPage>();
            var taef = OptionsPageFactory.Create<TaefOptionsDialogPage>();
            var pages = new DialogPage[] { general, discovery, execution, taef };
            foreach (DialogPage page in pages)
            {
                foreach (PropertyInfo option in OptionsPagesTests.GetOptions(page.GetType()))
                {
                    option.SetValue(page, OptionsPagesTests.GetOtherValidValue(option, option.GetValue(page)));
                }
            }

            RunSettings runSettings = GetRunSettingsFromOptionPages(general, discovery, execution, taef, "the-pipe-id");

            // every option is mapped to the setting of the same name (except for the few listed here)
            var settingOfOption = new Dictionary<string, string>
            {
                { nameof(TestExecutionOptionsDialogPage.ParallelTestExecution), nameof(RunSettings.ParallelTestExecution) },
                { nameof(TestExecutionOptionsDialogPage.AdditionalTestExecutionParam), nameof(RunSettings.AdditionalTestExecutionParam) },
            };
            foreach (DialogPage page in pages)
            {
                foreach (PropertyInfo option in OptionsPagesTests.GetOptions(page.GetType()))
                {
                    string settingName = settingOfOption.TryGetValue(option.Name, out string name) ? name : option.Name;
                    PropertyInfo setting = typeof(RunSettings).GetProperty(settingName);
                    setting.Should().NotBeNull($"option {page.GetType().Name}.{option.Name} should have a setting");
                    // ReSharper disable once PossibleNullReferenceException
                    setting.GetValue(runSettings).Should().Be(option.GetValue(page), $"option {page.GetType().Name}.{option.Name} should be mapped to setting {settingName}");
                }
            }

            // settings which are not options: only internal ones (and the project regex of project settings)
            var notMapped = typeof(RunSettings).GetProperties()
                .Where(p => p.GetValue(runSettings) == null)
                .Select(p => p.Name);
            notMapped.Should().BeEquivalentTo(
                nameof(RunSettings.ProjectRegex),
                // determined from DTE, which is not available outside of Visual Studio
                nameof(RunSettings.SolutionDir), nameof(RunSettings.PlatformName), nameof(RunSettings.ConfigurationName));
            runSettings.DebuggingNamedPipeId.Should().Be("the-pipe-id");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToolbarProperties_Getters_ReadOptionsPages()
        {
            var general = OptionsPageFactory.Create<GeneralOptionsDialogPage>();
            var discovery = OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>();
            var execution = OptionsPageFactory.Create<TestExecutionOptionsDialogPage>();
            var taef = OptionsPageFactory.Create<TaefOptionsDialogPage>();
            ITaefTestAdapterPackage package = CreateUninitializedPackage(general, discovery, execution, taef, "id");

            package.RunInProcess.Should().BeFalse();
            package.BreakOnError.Should().BeFalse();
            package.ParallelTestExecution.Should().BeFalse();
            package.PrintTestOutput.Should().BeFalse();

            taef.RunInProcess = true;
            taef.BreakOnError = true;
            execution.ParallelTestExecution = true;
            general.PrintTestOutput = true;

            package.RunInProcess.Should().BeTrue();
            package.BreakOnError.Should().BeTrue();
            package.ParallelTestExecution.Should().BeTrue();
            package.PrintTestOutput.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void InitializeDebuggerAttacherService_ServiceHostIsOpened_PipeIdIsPublishedAndAdapterCanAttach()
        {
            var mockDebuggerAttacher = new Mock<IDebuggerAttacher>();
            mockDebuggerAttacher.Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>())).Returns(true);
            var mockLogger = new Mock<ILogger>();
            var globalRunSettings = new GlobalRunSettingsProvider();
            TaefTestAdapterPackage package = CreateUninitializedPackageWithGlobalRunSettings(globalRunSettings);
            string pipeId = Guid.NewGuid().ToString();

            try
            {
                InitializeDebuggerAttacherService(package, pipeId, mockDebuggerAttacher.Object, mockLogger.Object);

                globalRunSettings.RunSettings.DebuggingNamedPipeId.Should().Be(pipeId);
                GetServiceHost(package).Should().NotBeNull();
                GetServiceHost(package).State.Should().Be(CommunicationState.Opened);
                mockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);

                // the test adapter attaches the debugger through the published pipe
                var client = new MessageBasedDebuggerAttacher(globalRunSettings.RunSettings.DebuggingNamedPipeId,
                    DebuggerAttacherServiceTests.WaitingTime, new Mock<ILogger>().Object);
                client.AttachDebugger(2017, DebuggerEngine.Native).Should().BeTrue();
                mockDebuggerAttacher.Verify(a => a.AttachDebugger(2017, DebuggerEngine.Native), Times.Once);
            }
            finally
            {
                GetServiceHost(package)?.Abort();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void InitializeDebuggerAttacherService_ServiceHostCanNotBeOpened_PipeIdIsNotPublishedAndErrorIsLogged()
        {
            var mockLogger = new Mock<ILogger>();
            var globalRunSettings = new GlobalRunSettingsProvider { RunSettings = null };
            TaefTestAdapterPackage package = CreateUninitializedPackageWithGlobalRunSettings(globalRunSettings);
            string pipeId = Guid.NewGuid().ToString();

            // another service listening on the pipe makes opening the package's service host fail (within a process with an
            // InvalidOperationException, which is no CommunicationException, across processes with an AddressAlreadyInUseException)
            var blockingHost = new DebuggerAttacherServiceHost(pipeId, new Mock<IDebuggerAttacher>().Object, new Mock<ILogger>().Object);
            try
            {
                blockingHost.Open();

                InitializeDebuggerAttacherService(package, pipeId, new Mock<IDebuggerAttacher>().Object, mockLogger.Object);

                // the test adapter then rejects debugging with the native debugger engines up front (see TestExecutor)
                globalRunSettings.RunSettings.Should().NotBeNull();
                globalRunSettings.RunSettings.DebuggingNamedPipeId.Should().BeNull();
                GetServiceHost(package).Should().BeNull();
                mockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Could not open the debugger attacher service")
                    && s.Contains($"'{DebuggerKindConverter.Native}'") && s.Contains($"'{DebuggerKindConverter.ManagedAndNative}'")
                    && s.Contains(pipeId))), Times.Once); // the exception's details
            }
            finally
            {
                blockingHost.Abort();
            }
        }

        private static TaefTestAdapterPackage CreateUninitializedPackageWithGlobalRunSettings(IGlobalRunSettingsInternal globalRunSettings)
        {
            TaefTestAdapterPackage package = CreateUninitializedPackage(
                OptionsPageFactory.Create<GeneralOptionsDialogPage>(), OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>(),
                OptionsPageFactory.Create<TestExecutionOptionsDialogPage>(), OptionsPageFactory.Create<TaefOptionsDialogPage>(), null);
            SetField(package, "_globalRunSettings", globalRunSettings);
            return package;
        }

        private static void InitializeDebuggerAttacherService(TaefTestAdapterPackage package, string pipeId, IDebuggerAttacher debuggerAttacher, ILogger logger)
        {
            MethodInfo method = typeof(TaefTestAdapterPackage).GetMethod("InitializeDebuggerAttacherService", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(IDebuggerAttacher), typeof(ILogger) }, null);
            method.Should().NotBeNull("the package opens the debugger attacher service in method InitializeDebuggerAttacherService(string, IDebuggerAttacher, ILogger)");
            try
            {
                // ReSharper disable once PossibleNullReferenceException
                method.Invoke(package, new object[] { pipeId, debuggerAttacher, logger });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }

        private static ServiceHost GetServiceHost(TaefTestAdapterPackage package)
        {
            FieldInfo field = typeof(TaefTestAdapterPackage).GetField("_debuggerAttacherServiceHost", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull("the package stores its debugger attacher service host in field _debuggerAttacherServiceHost");
            // ReSharper disable once PossibleNullReferenceException
            return (ServiceHost)field.GetValue(package);
        }

        private static TaefTestAdapterPackage CreateUninitializedPackage(GeneralOptionsDialogPage general, TestDiscoveryOptionsDialogPage discovery,
            TestExecutionOptionsDialogPage execution, TaefOptionsDialogPage taef, string debuggingNamedPipeId)
        {
            // the package can not be initialized outside of Visual Studio
            var package = (TaefTestAdapterPackage)FormatterServices.GetUninitializedObject(typeof(TaefTestAdapterPackage));
            GC.SuppressFinalize(package);

            SetField(package, "_generalOptions", general);
            SetField(package, "_testDiscoveryOptions", discovery);
            SetField(package, "_testExecutionOptions", execution);
            SetField(package, "_taefOptions", taef);
            SetField(package, "_debuggingNamedPipeId", debuggingNamedPipeId);

            return package;
        }

        private static RunSettings GetRunSettingsFromOptionPages(GeneralOptionsDialogPage general, TestDiscoveryOptionsDialogPage discovery,
            TestExecutionOptionsDialogPage execution, TaefOptionsDialogPage taef, string debuggingNamedPipeId)
        {
            TaefTestAdapterPackage package = CreateUninitializedPackage(general, discovery, execution, taef, debuggingNamedPipeId);

            MethodInfo method = typeof(TaefTestAdapterPackage).GetMethod("GetRunSettingsFromOptionPages", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull("the package maps its options pages to run settings in method GetRunSettingsFromOptionPages()");
            // ReSharper disable once PossibleNullReferenceException
            return (RunSettings)method.Invoke(package, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull($"the package stores its state in field {fieldName}");
            // ReSharper disable once PossibleNullReferenceException
            field.SetValue(target, value);
        }
    }
}
