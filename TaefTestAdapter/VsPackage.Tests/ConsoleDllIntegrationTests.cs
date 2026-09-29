// This file has been modified for TAEF support.

using System;
using System.IO;
using System.Linq;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage
{

    /// <summary>
    /// Checks that vstest.console.exe (of VS 2026, else VS 2022, see <see cref="TestResources.GetVsTestConsolePath()"/>)
    /// lists the adapter's discoverer, executor and settings provider.
    /// </summary>
    /// <remarks>
    /// vstest.console ignores /TestAdapterPath for the /List* switches, i.e. it only lists extensions which are installed
    /// into its Extensions folder. To not modify the Visual Studio installation, the test platform folder is copied into a
    /// temporary folder, the adapter's DLLs are installed into the copy's Extensions folder, and the copy is used by the
    /// tests of this class (through environment variable <see cref="TestResources.VsTestConsoleEnvVariable"/>). If that is
    /// not possible, the tests end inconclusive.
    /// </remarks>
    [TestClass]
    public class ConsoleDllIntegrationTests : AbstractConsoleIntegrationTests
    {
        private static TemporaryDirectory _vsTestCopy;
        private static string _formerVsTestConsoleVariable;
        private static bool _vsTestConsoleVariableChanged;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            string vsTestConsole = TestResources.GetVsTestConsolePath();
            if (vsTestConsole == null || !File.Exists(vsTestConsole))
                return; // tests will fail with a clear message

            string adapterDir = TestResources.TestAdapterDir;
            string[] adapterDlls = Directory.Exists(adapterDir)
                ? Directory.GetFiles(adapterDir, "TaefTestAdapter.*.dll")
                : new string[0];
            if (!adapterDlls.Any(f => f.EndsWith("TaefTestAdapter.TestAdapter.dll", StringComparison.OrdinalIgnoreCase)))
                return; // tests will be inconclusive

            try
            {
                _vsTestCopy = new TemporaryDirectory();
                string testPlatformDir = Path.GetDirectoryName(vsTestConsole);
                // ReSharper disable once AssignNullToNotNullAttribute
                CopyDirectory(testPlatformDir, _vsTestCopy.Path);

                string extensionsDir = Path.Combine(_vsTestCopy.Path, "Extensions");
                foreach (string adapterDll in adapterDlls)
                {
                    File.Copy(adapterDll, Path.Combine(extensionsDir, Path.GetFileName(adapterDll)), true);
                }
                foreach (string architecture in new[] { "x86", "x64", "arm64" })
                {
                    string msdia = Path.Combine(adapterDir, architecture, "msdia140.dll");
                    if (File.Exists(msdia))
                    {
                        Directory.CreateDirectory(Path.Combine(extensionsDir, architecture));
                        File.Copy(msdia, Path.Combine(extensionsDir, architecture, "msdia140.dll"), true);
                    }
                }

                _formerVsTestConsoleVariable = Environment.GetEnvironmentVariable(TestResources.VsTestConsoleEnvVariable);
                Environment.SetEnvironmentVariable(TestResources.VsTestConsoleEnvVariable,
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Path.Combine(_vsTestCopy.Path, Path.GetFileName(vsTestConsole)));
                _vsTestConsoleVariableChanged = true;
            }
            catch (Exception e)
            {
                testContext.WriteLine($"Could not install the adapter into a copy of the test platform: {e}");
                CleanUp();
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CleanUp();
        }

        private static void CleanUp()
        {
            if (_vsTestConsoleVariableChanged)
            {
                Environment.SetEnvironmentVariable(TestResources.VsTestConsoleEnvVariable, _formerVsTestConsoleVariable);
                _vsTestConsoleVariableChanged = false;
            }
            _vsTestCopy?.Dispose();
            _vsTestCopy = null;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
            }
        }

        protected override string GetAdapterIntegration()
        {
            return GetLogger() + @"/TestAdapterPath:""" + TestAdapterDir + @"""";
        }

        #region method stubs for code coverage

        [TestMethod]
        [TestCategory(EndToEnd)]
        public override void Console_ListDiscoverers_DiscovererIsListed()
        {
            base.Console_ListDiscoverers_DiscovererIsListed();
        }

        [TestMethod]
        [TestCategory(EndToEnd)]
        public override void Console_ListExecutors_ExecutorIsListed()
        {
            base.Console_ListExecutors_ExecutorIsListed();
        }

        [TestMethod]
        [TestCategory(EndToEnd)]
        public override void Console_ListSettingsProviders_SettingsProviderIsListed()
        {
            base.Console_ListSettingsProviders_SettingsProviderIsListed();
        }

        #endregion

    }

}
