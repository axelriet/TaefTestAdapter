// This file has been added for TAEF support.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter
{

    [TestClass]
    public class TaefLocatorTests : TestsBase
    {
        private TemporaryDirectory _directory;
        private FakeLogger _fakeLogger;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
            _fakeLogger = new FakeLogger(() => OutputMode.Verbose, false);
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        #region Architecture

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitecture_MachineTypes_AreMappedToTaefArchitectures()
        {
            TaefLocator.GetArchitecture(PeParser.MachineX86).Should().Be("x86");
            TaefLocator.GetArchitecture(PeParser.MachineX64).Should().Be("x64");
            TaefLocator.GetArchitecture(PeParser.MachineArm64).Should().Be("arm64");
            TaefLocator.GetArchitecture(PeParser.MachineArm64EC).Should().BeNull("0xA641 only occurs in object files, ARM64EC images carry machine type AMD64");
            TaefLocator.GetArchitecture(PeParser.MachineArm).Should().Be("arm");
            TaefLocator.GetArchitecture(PeParser.MachineArmThumb).Should().Be("arm");
            TaefLocator.GetArchitecture(PeParser.MachineArmNT).Should().Be("arm");
            TaefLocator.GetArchitecture(PeParser.MachineUnknown).Should().BeNull();
            TaefLocator.GetArchitecture((ushort)0x0200).Should().BeNull("IA64 is not supported by TAEF");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitecture_SyntheticDlls_ArchitectureOfMachineType()
        {
            TaefLocator.GetArchitecture(CreateDll("x86.dll", SyntheticPeFile.MachineX86), _fakeLogger).Should().Be("x86");
            TaefLocator.GetArchitecture(CreateDll("x64.dll", SyntheticPeFile.MachineX64), _fakeLogger).Should().Be("x64");
            TaefLocator.GetArchitecture(CreateDll("arm64.dll", SyntheticPeFile.MachineArm64), _fakeLogger).Should().Be("arm64");
            _fakeLogger.Warnings.Should().BeEmpty();
        }

        /// <summary>
        /// ARM64EC DLLs carry machine type AMD64 (checked with DLLs linked with /MACHINE:ARM64EC by MSVC 14.51: 0x8664),
        /// so their tests are run with the x64 TE.exe (which loads them in an x64 process, as intended for ARM64EC);
        /// ARM64X DLLs (/MACHINE:ARM64X) carry machine type ARM64 (0xAA64). The machine type ARM64EC (0xA641) is only
        /// used in object files, so a PE image with it is of unknown architecture.
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitecture_Arm64ECAndArm64XDlls_ArchitectureOfTheirPeMachineType()
        {
            TaefLocator.GetArchitecture(CreateDll("arm64ec.dll", SyntheticPeFile.MachineX64), _fakeLogger).Should().Be("x64");
            TaefLocator.GetArchitecture(CreateDll("arm64x.dll", SyntheticPeFile.MachineArm64), _fakeLogger).Should().Be("arm64");
            _fakeLogger.Warnings.Should().BeEmpty();

            string expected = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                expected = "arm64";
            TaefLocator.GetArchitecture(CreateDll("objectfilemachine.dll", SyntheticPeFile.MachineArm64EC), _fakeLogger).Should().Be(expected);
            _fakeLogger.Warnings.Should().Contain(w => w.Contains("Could not determine architecture") && w.Contains("(machine type 0xA641)"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitecture_NoPeFile_ArchitectureOfOperatingSystemAndDebugWarning()
        {
            string noDll = _directory.CreateFile("NoDll.dll", "no PE file");
            string expected = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                expected = "arm64";

            TaefLocator.GetArchitecture(noDll, _fakeLogger).Should().Be(expected);
            TaefLocator.GetArchitecture(_directory.GetPath("DoesNotExist.dll"), _fakeLogger).Should().Be(expected);
            _fakeLogger.Warnings.Should().Contain(w => w.Contains("Could not determine architecture") && w.Contains("NoDll.dll"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetArchitecture_SampleDlls_ArchitectureOfConfiguration()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                foreach (string dll in TestResources.AllSampleTestDlls)
                {
                    TaefLocator.GetArchitecture(TestResources.GetSampleDll(dll, configuration), _fakeLogger)
                        .Should().Be(configuration.GetArchitecture(), $"{dll} ({configuration})");
                }
            }
        }

        #endregion

        #region Configured TE.exe

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTeExecutable_ConfiguredFile_IsUsed()
        {
            string teExecutable = _directory.CreateFile(@"tools\MyTE.exe");
            MockOptions.Setup(o => o.TeExecutable).Returns(teExecutable);

            TaefLocator.FindTeExecutable(CreateDll("x86.dll", SyntheticPeFile.MachineX86), MockOptions.Object, _fakeLogger).Should().Be(teExecutable);
            TaefLocator.FindTeExecutable(CreateDll("x64.dll", SyntheticPeFile.MachineX64), MockOptions.Object, _fakeLogger).Should().Be(teExecutable);
            _fakeLogger.Errors.Should().BeEmpty();
            _fakeLogger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTeExecutable_ConfiguredFolderWithArchitectureFolders_TeOfArchitectureIsUsed()
        {
            string x86Te = _directory.CreateFile(@"TAEF\x86\TE.exe");
            string x64Te = _directory.CreateFile(@"TAEF\x64\TE.exe");
            string arm64Te = _directory.CreateFile(@"TAEF\arm64\TE.exe");
            _directory.CreateFile(@"TAEF\TE.exe");
            MockOptions.Setup(o => o.TeExecutable).Returns(_directory.GetPath("TAEF"));

            TaefLocator.FindTeExecutable(CreateDll("x86.dll", SyntheticPeFile.MachineX86), MockOptions.Object, _fakeLogger).Should().Be(x86Te);
            TaefLocator.FindTeExecutable(CreateDll("x64.dll", SyntheticPeFile.MachineX64), MockOptions.Object, _fakeLogger).Should().Be(x64Te);
            TaefLocator.FindTeExecutable(CreateDll("arm64.dll", SyntheticPeFile.MachineArm64), MockOptions.Object, _fakeLogger).Should().Be(arm64Te);
            _fakeLogger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTeExecutable_ConfiguredFolderWithoutArchitectureFolders_TeOfFolderIsUsed()
        {
            string te = _directory.CreateFile(@"TAEF\TE.exe");
            _directory.CreateFile(@"TAEF\x64\TE.exe");
            MockOptions.Setup(o => o.TeExecutable).Returns(_directory.GetPath("TAEF") + @"\");

            TaefLocator.FindTeExecutable(CreateDll("x86.dll", SyntheticPeFile.MachineX86), MockOptions.Object, _fakeLogger).Should().Be(te);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTeExecutable_ConfiguredValueWithPlaceholders_PlaceholdersAreReplaced()
        {
            string te = _directory.CreateFile(@"tools\x64\TE.exe");
            MockOptions.Setup(o => o.TeExecutable).Returns($" {PlaceholderReplacer.TestDllDirPlaceholder}\\tools ");

            TaefLocator.FindTeExecutable(CreateDll("x64.dll", SyntheticPeFile.MachineX64), MockOptions.Object, _fakeLogger).Should().Be(te);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTeExecutable_ConfiguredFileIsDeleted_CacheEntryIsNotUsedAnymore()
        {
            string te = _directory.CreateFile(@"TAEF\TE.exe");
            string otherTe = _directory.CreateFile(@"TAEF\x64\TE.exe");
            MockOptions.Setup(o => o.TeExecutable).Returns(_directory.GetPath("TAEF"));
            string testDll = CreateDll("x64.dll", SyntheticPeFile.MachineX64);

            TaefLocator.FindTeExecutable(testDll, MockOptions.Object, _fakeLogger).Should().Be(otherTe);
            File.Delete(otherTe);

            TaefLocator.FindTeExecutable(testDll, MockOptions.Object, _fakeLogger).Should().Be(te);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTeExecutable_ConfiguredValueDoesNotExist_WarningOnceAndAutomaticDetection()
        {
            string doesNotExist = _directory.GetPath("DoesNotExist");
            MockOptions.Setup(o => o.TeExecutable).Returns(doesNotExist);
            string testDll = CreateDll("x64.dll", SyntheticPeFile.MachineX64);

            string te1 = TaefLocator.FindTeExecutable(testDll, MockOptions.Object, _fakeLogger);
            string te2 = TaefLocator.FindTeExecutable(testDll, MockOptions.Object, _fakeLogger);

            AssertIsInstalledTe(te1, "x64");
            te2.Should().Be(te1);
            _fakeLogger.Warnings.Where(w => w.Contains(doesNotExist)).Should().ContainSingle()
                .Which.Should().Contain(SettingsWrapper.OptionTeExecutable).And.Contain("automatically");

            // another logger (i.e. another discovery or test run) gets the warning again
            var otherLogger = new FakeLogger(() => OutputMode.Info, false);
            TaefLocator.FindTeExecutable(testDll, MockOptions.Object, otherLogger).Should().Be(te1);
            otherLogger.Warnings.Should().ContainSingle(w => w.Contains(doesNotExist));
        }

        #endregion

        #region Automatic detection (requires TAEF, which is installed with the WDK)

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTeExecutable_SampleDlls_TeOfDllArchitectureFromWindowsKits()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                string te = TaefLocator.FindTeExecutable(TestResources.GetSampleDll(TestResources.TestsDll, configuration), MockOptions.Object, _fakeLogger);

                AssertIsInstalledTe(te, configuration.GetArchitecture());
                te.Should().BeEquivalentTo(Path.GetFullPath(TestResources.GetTeExecutable(configuration)));
            }
            _fakeLogger.Errors.Should().BeEmpty();
            _fakeLogger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTeExecutable_SyntheticDlls_TeOfArchitecture()
        {
            AssertIsInstalledTe(TaefLocator.FindTeExecutable(CreateDll("x86.dll", SyntheticPeFile.MachineX86), MockOptions.Object, _fakeLogger), "x86");
            AssertIsInstalledTe(TaefLocator.FindTeExecutable(CreateDll("x64.dll", SyntheticPeFile.MachineX64), MockOptions.Object, _fakeLogger), "x64");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTeExecutable_NoTeForArchitecture_ErrorOncePerLoggerAndNull()
        {
            string armDll = CreateDll("arm.dll", SyntheticPeFile.MachineArmNT);
            string armTe = Path.Combine(TestResources.TaefRuntimesDir, "arm", "TE.exe");
            bool teOnPath = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
                .Any(d => { try { return File.Exists(Path.Combine(d.Trim().Trim('"'), "TE.exe")); } catch (ArgumentException) { return false; } });
            if (File.Exists(armTe) || teOnPath)
                Assert.Inconclusive("TE.exe for ARM (32 bit) is installed or TE.exe is on the PATH");

            TaefLocator.FindTeExecutable(armDll, MockOptions.Object, _fakeLogger).Should().BeNull();
            TaefLocator.FindTeExecutable(armDll, MockOptions.Object, _fakeLogger).Should().BeNull();

            _fakeLogger.Errors.Should().ContainSingle()
                .Which.Should().Be(TaefLocator.GetTeExecutableNotFoundMessage("arm", ""));

            var otherLogger = new FakeLogger(() => OutputMode.Info, false);
            TaefLocator.FindTeExecutable(armDll, MockOptions.Object, otherLogger).Should().BeNull();
            otherLogger.Errors.Should().ContainSingle();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTeExecutable_AfterClearCache_TeIsFoundAgain()
        {
            string testDll = CreateDll("x86.dll", SyntheticPeFile.MachineX86);
            string te = TaefLocator.FindTeExecutable(testDll, MockOptions.Object, _fakeLogger);

            TaefLocator.ClearCache();

            TaefLocator.FindTeExecutable(testDll, MockOptions.Object, _fakeLogger).Should().Be(te);
        }

        #endregion

        #region Misc

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTeExecutableNotFoundMessage_ContainsHints()
        {
            string message = TaefLocator.GetTeExecutableNotFoundMessage("arm64", "");
            message.Should().Contain("TE.exe").And.Contain("arm64").And.Contain(SettingsWrapper.OptionTeExecutable).And.Contain("WDK");
            message.Should().NotContain("configured value");

            TaefLocator.GetTeExecutableNotFoundMessage("x86", @"C:\wrong").Should().Contain(@"the configured value 'C:\wrong'");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTeExecutable_NullArguments_Throw()
        {
            Action[] actions =
            {
                () => TaefLocator.FindTeExecutable(null, MockOptions.Object, _fakeLogger),
                () => TaefLocator.FindTeExecutable("x.dll", null, _fakeLogger),
                () => TaefLocator.FindTeExecutable("x.dll", MockOptions.Object, null)
            };

            foreach (Action action in actions)
            {
                action.Should().Throw<ArgumentNullException>();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Constants_HaveExpectedValues()
        {
            TaefLocator.KitsRootRegistryKey.Should().Be(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots");
            TaefLocator.KitsRootRegistryValue.Should().Be("KitsRoot10");
            TaefLocator.TaefRuntimesSubDirectory.Should().Be(@"Testing\Runtimes\TAEF");
            TaefConstants.TeExecutableName.Should().Be("TE.exe");
        }

        #endregion

        private string CreateDll(string fileName, ushort machine)
        {
            return SyntheticPeFile.TaefTestDll(machine).WriteTo(_directory.GetPath(fileName));
        }

        private static void AssertIsInstalledTe(string te, string architecture)
        {
            te.Should().NotBeNull();
            File.Exists(te).Should().BeTrue(te);
            Path.GetFileName(te).Should().BeEquivalentTo("TE.exe");
            Path.GetFileName(Path.GetDirectoryName(te)).Should().BeEquivalentTo(architecture);
            te.Should().ContainEquivalentOf(@"Testing\Runtimes\TAEF\");
        }

    }

}
