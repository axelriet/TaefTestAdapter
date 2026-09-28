// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.DiaResolver
{
    [TestClass]
    public class PeParserTests : TestsBase
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

        #region Imports and PDB path (sample DLLs)

        [TestMethod]
        [TestCategory(Integration)]
        public void ParseImports_X86TestDllWithDependency_ContainsTaefDllsAndDependency()
        {
            List<string> imports = PeParser.ParseImports(TestResources.DllTests_ReleaseX86, MockLogger.Object);

            imports.Should().OnlyHaveUniqueItems();
            imports.Should().Contain(new[] { "Wex.Common.dll", "Wex.Logger.dll", TestResources.DllProjectDll });
            imports.Should().Contain(i => i.Equals("KERNEL32.dll", StringComparison.OrdinalIgnoreCase));
            imports.Should().NotContain("TE.Common.dll", "the DLL uses neither test data nor runtime parameters");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ParseImports_X86PlainDll_ContainsNoTaefDlls()
        {
            List<string> imports = PeParser.ParseImports(TestResources.DllTestsDll_ReleaseX86, MockLogger.Object);

            imports.Should().NotBeEmpty();
            imports.Should().Contain(i => i.Equals("KERNEL32.dll", StringComparison.OrdinalIgnoreCase));
            imports.Should().NotContain(i => PeParser.TaefImports.Contains(i, StringComparer.OrdinalIgnoreCase));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ParseImports_X64TestDll_ContainsAllTaefDlls()
        {
            List<string> imports = PeParser.ParseImports(TestResources.Tests_ReleaseX64, MockLogger.Object);

            imports.Should().Contain(PeParser.TaefImports);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindImport_TestDllWithDependency_IsFoundDependingOnComparison()
        {
            PeParser.FindImport(TestResources.DllTests_ReleaseX64, TestResources.DllProjectDll, StringComparison.Ordinal, MockLogger.Object).Should().BeTrue();
            PeParser.FindImport(TestResources.DllTests_ReleaseX64, "dllproject.dll", StringComparison.OrdinalIgnoreCase, MockLogger.Object).Should().BeTrue();
            PeParser.FindImport(TestResources.DllTests_ReleaseX64, "dllproject.dll", StringComparison.Ordinal, MockLogger.Object).Should().BeFalse();
            PeParser.FindImport(TestResources.DllTests_ReleaseX64, "OtherFramework.dll", StringComparison.OrdinalIgnoreCase, MockLogger.Object).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExtractPdbPath_X64TestDll_FindsEmbeddedPdbPath()
        {
            string pdb = PeParser.ExtractPdbPath(TestResources.Tests_ReleaseX64, MockLogger.Object);
            string expectedPdb = Path.GetFullPath(Path.ChangeExtension(TestResources.Tests_ReleaseX64, ".pdb"));
            pdb.Should().BeEquivalentTo(expectedPdb);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExtractPdbPath_X86TestDll_FindsEmbeddedPdbPath()
        {
            string pdb = PeParser.ExtractPdbPath(TestResources.LoadTests_ReleaseX86, MockLogger.Object);
            string expectedPdb = Path.GetFullPath(Path.ChangeExtension(TestResources.LoadTests_ReleaseX86, ".pdb"));
            pdb.Should().BeEquivalentTo(expectedPdb);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExtractPdbPath_AllSampleDlls_EmbeddedPdbIsNextToDll()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                foreach (string dll in TestResources.AllSampleTestDlls.Concat(new[] { TestResources.DllProjectDll }))
                {
                    string sampleDll = TestResources.GetSampleDll(dll, configuration);
                    PeParser.ExtractPdbPath(sampleDll, MockLogger.Object)
                        .Should().BeEquivalentTo(Path.GetFullPath(Path.ChangeExtension(sampleDll, ".pdb")), $"{dll} ({configuration})");
                }
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ExtractPdbPath_CopyWithBrokenPdbPath_ReturnsChangedPath()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_ReleaseX86))
            {
                string brokenPath = EmbeddedPdbPath.Break(sample.TestDll);

                PeParser.ExtractPdbPath(sample.TestDll, MockLogger.Object).Should().Be(brokenPath);
                PeParser.ExtractPdbPath(sample.OriginalTestDll, MockLogger.Object).Should().NotBe(brokenPath);
                PeParser.IsTaefTestDll(sample.TestDll, _fakeLogger).Should().BeTrue();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractPdbPathAndParseImports_NoPeFiles_ReturnNothing()
        {
            string textFile = _directory.CreateFile("Text.dll", "no PE file");
            string emptyFile = _directory.CreateFile("Empty.dll");

            foreach (string file in new[] { textFile, emptyFile, _directory.GetPath("DoesNotExist.dll") })
            {
                PeParser.ExtractPdbPath(file, _fakeLogger).Should().BeNull(file);
                PeParser.ParseImports(file, _fakeLogger).Should().BeEmpty(file);
                PeParser.FindImport(file, "KERNEL32.dll", StringComparison.OrdinalIgnoreCase, _fakeLogger).Should().BeFalse(file);
            }
        }

        #endregion

        #region Static TAEF test DLL detection (sample and system DLLs)

        [TestMethod]
        [TestCategory(Integration)]
        public void IsTaefTestDll_SampleTestDlls_AreRecognized()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                foreach (string dll in TestResources.AllSampleTestDlls)
                {
                    PeParser.IsTaefTestDll(TestResources.GetSampleDll(dll, configuration), _fakeLogger)
                        .Should().BeTrue($"{dll} ({configuration}) is a TAEF test DLL");
                }
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void IsTaefTestDll_OtherSampleFiles_AreNotRecognized()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                string directory = TestResources.GetSampleDirectory(configuration);
                IEnumerable<string> otherFiles = TestResources.NonTaefSampleDlls.Select(dll => directory + dll)
                    .Concat(Directory.GetFiles(directory, "*.pdb"))
                    .Concat(Directory.GetFiles(directory, "*.lib"));

                foreach (string file in otherFiles)
                {
                    PeParser.IsTaefTestDll(file, _fakeLogger).Should().BeFalse($"{file} is no TAEF test DLL");
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_SystemFiles_AreNotRecognized()
        {
            foreach (string file in new[] { "kernel32.dll", "ntdll.dll", "notepad.exe", "cmd.exe" })
            {
                PeParser.IsTaefTestDll(Path.Combine(Environment.SystemDirectory, file), _fakeLogger).Should().BeFalse(file);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void IsTaefTestDll_TaefRuntimeFiles_AreNotRecognized()
        {
            foreach (string architecture in new[] { "x86", "x64" })
            {
                string runtimeDir = Path.GetDirectoryName(TestResources.GetTeExecutable(architecture));
                foreach (string file in new[] { "TE.exe", "Wex.Logger.dll", "Wex.Common.dll", "TE.Common.dll" })
                {
                    string path = Path.Combine(runtimeDir, file);
                    if (!File.Exists(path))
                        Assert.Inconclusive($"TAEF is not installed ({path} does not exist)");
                    PeParser.IsTaefTestDll(path, _fakeLogger).Should().BeFalse(path);
                }
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetMachineType_SampleDlls_MachineOfConfiguration()
        {
            foreach (SampleConfiguration configuration in TestResources.AllSampleConfigurations)
            {
                ushort expected = configuration.GetArchitecture() == "x86" ? PeParser.MachineX86 : PeParser.MachineX64;
                foreach (string dll in TestResources.AllSampleTestDlls.Concat(new[] { TestResources.DllProjectDll }))
                {
                    PeParser.GetMachineType(TestResources.GetSampleDll(dll, configuration), _fakeLogger).Should().Be(expected, $"{dll} ({configuration})");
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetMachineType_SystemDll_MachineOfProcess()
        {
            // System32 is redirected to SysWOW64 for 32 bit processes
            ushort expected = Environment.Is64BitProcess ? PeParser.MachineX64 : PeParser.MachineX86;
            if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                Assert.Inconclusive("System DLLs of ARM64 Windows are ARM64X");

            PeParser.GetMachineType(Path.Combine(Environment.SystemDirectory, "kernel32.dll"), _fakeLogger).Should().Be(expected);
        }

        #endregion

        #region Static TAEF test DLL detection (synthetic PE files)

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_SyntheticTaefDlls_AreRecognized()
        {
            foreach (ushort machine in new[] { SyntheticPeFile.MachineX86, SyntheticPeFile.MachineX64, SyntheticPeFile.MachineArm64, SyntheticPeFile.MachineArm64EC })
            {
                AssertIsTaefTestDll(SyntheticPeFile.TaefTestDll(machine), true, $"machine 0x{machine:X4}");
            }
            _fakeLogger.Warnings.Should().BeEmpty();
            _fakeLogger.Infos.Should().Contain(i => i.Contains("is a TAEF test DLL"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_TaefAbiVersions_OnlyKnownVersionsAreAccepted()
        {
            foreach (bool pe32Plus in new[] { true, false })
            {
                foreach (ulong version in new ulong[] { 0, 9, 14, 0x46454154, ulong.MaxValue })
                {
                    if (!pe32Plus && version > uint.MaxValue)
                        continue;
                    AssertIsTaefTestDll(new SyntheticPeFile { IsPe32Plus = pe32Plus, TaefAbiVersion = version }, false, $"ABI version {version}, PE32+: {pe32Plus}");
                }
                for (ulong version = PeParser.MinTaefAbiVersion; version <= PeParser.MaxTaefAbiVersion; version++)
                {
                    AssertIsTaefTestDll(new SyntheticPeFile { IsPe32Plus = pe32Plus, TaefAbiVersion = version }, true, $"ABI version {version}, PE32+: {pe32Plus}");
                }
            }
            _fakeLogger.Infos.Should().Contain(i => i.Contains("unsupported TAEF metadata ABI version 9"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WrongSignature_IsNotRecognized()
        {
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSignature = Encoding.ASCII.GetBytes("TAEX") }, false);
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSignature = Encoding.ASCII.GetBytes("taef") }, false);
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSignature = new byte[0] }, false);
            _fakeLogger.Infos.Should().Contain(i => i.Contains("does not start with the TAEF signature"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_WrongSectionName_IsNotRecognized()
        {
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSectionName = null }, false);
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSectionName = "testdat" }, false);
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSectionName = "TESTDATA" }, false);
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataSectionName = ".data" }, false);
            _fakeLogger.Infos.Should().Contain(i => i.Contains("no 'testdata' section"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_TestDataSectionTooSmall_IsNotRecognized()
        {
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataRawSize = 4 }, false, "PE32+ needs 16 bytes");
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataRawSize = 15 }, false, "PE32+ needs 16 bytes");
            AssertIsTaefTestDll(new SyntheticPeFile { TestDataRawSize = 16 }, true, "PE32+ needs 16 bytes");
            AssertIsTaefTestDll(new SyntheticPeFile { Machine = SyntheticPeFile.MachineX86, TestDataRawSize = 7 }, false, "PE32 needs 8 bytes");
            AssertIsTaefTestDll(new SyntheticPeFile { Machine = SyntheticPeFile.MachineX86, TestDataRawSize = 8 }, true, "PE32 needs 8 bytes");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_TaefImports_AtLeastOneIsRequired()
        {
            AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string> { "KERNEL32.dll" } }, false, "DLLs only including WexTestClass.h contain no tests");
            AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string>() }, false, "no imports");
            AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string> { "Wex.Communication.dll", "OtherFramework.dll" } }, false, "other DLLs");

            foreach (string taefImport in PeParser.TaefImports)
            {
                AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string> { "KERNEL32.dll", taefImport } }, true, taefImport);
                AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string> { taefImport.ToUpperInvariant() } }, true, taefImport.ToUpperInvariant());
            }
            _fakeLogger.Infos.Should().Contain(i => i.Contains("none of Wex.Logger.dll, Wex.Common.dll, TE.Common.dll is imported"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_TaefImportsDelayLoaded_IsRecognized()
        {
            // TE.exe runs TAEF test DLLs linked with /DELAYLOAD:Wex.Logger.dll /DELAYLOAD:Wex.Common.dll (delayimp.lib)
            foreach (string taefImport in PeParser.TaefImports)
            {
                AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string> { "KERNEL32.dll" }, DelayLoadImports = new List<string> { taefImport } }, true, taefImport);
                AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string>(), DelayLoadImports = new List<string> { "USER32.dll", taefImport.ToUpperInvariant() } }, true, taefImport.ToUpperInvariant());
            }

            AssertIsTaefTestDll(new SyntheticPeFile { Machine = SyntheticPeFile.MachineX86, Imports = new List<string> { "KERNEL32.dll" }, DelayLoadImports = new List<string> { "Wex.Logger.dll" } }, true, "PE32");
            AssertIsTaefTestDll(new SyntheticPeFile { Machine = SyntheticPeFile.MachineX86, Imports = new List<string> { "KERNEL32.dll" }, DelayLoadImports = new List<string> { "Wex.Logger.dll" }, DelayLoadDescriptorsUseRvas = false }, true, "VC 6 descriptors (VAs)");
            _fakeLogger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_OnlyOtherDllsDelayLoaded_IsNotRecognized()
        {
            AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string> { "KERNEL32.dll" }, DelayLoadImports = new List<string> { "USER32.dll", "Wex.Communication.dll" } }, false);
            AssertIsTaefTestDll(new SyntheticPeFile { Imports = new List<string>(), DelayLoadImports = new List<string> { "Wex.Logger.dll.bak" } }, false);
            _fakeLogger.Infos.Should().Contain(i => i.Contains("none of Wex.Logger.dll, Wex.Common.dll, TE.Common.dll is imported or delay-loaded"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseImportsAndFindImport_DelayLoadedDlls_AreNotContained()
        {
            string dll = new SyntheticPeFile { Imports = new List<string> { "KERNEL32.dll", "Wex.Common.dll" }, DelayLoadImports = new List<string> { "Wex.Logger.dll" } }
                .WriteTo(_directory.GetPath("DelayLoad_taef.dll"));

            PeParser.ParseImports(dll, _fakeLogger).Should().Equal("KERNEL32.dll", "Wex.Common.dll");
            PeParser.FindImport(dll, "Wex.Common.dll", StringComparison.Ordinal, _fakeLogger).Should().BeTrue();
            PeParser.FindImport(dll, "Wex.Logger.dll", StringComparison.OrdinalIgnoreCase, _fakeLogger).Should().BeFalse();
            PeParser.IsTaefTestDll(dll, _fakeLogger).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractPdbPath_SyntheticDlls_PathIsDecodedAsUtf8()
        {
            foreach (string pdbPath in new[] { @"C:\build\Tests_taef.pdb", @"C:\Users\Jürgen\source\repos\测试Ω\x64\Debug\Tests_taef.pdb", "relative.pdb" })
            {
                foreach (ushort machine in new[] { SyntheticPeFile.MachineX64, SyntheticPeFile.MachineX86 })
                {
                    string dll = new SyntheticPeFile { Machine = machine, PdbPath = pdbPath }.WriteTo(_directory.GetPath(Guid.NewGuid() + ".dll"));
                    PeParser.ExtractPdbPath(dll, _fakeLogger).Should().Be(pdbPath, $"machine 0x{machine:X4}");
                }
            }

            PeParser.ExtractPdbPath(new SyntheticPeFile().WriteTo(_directory.GetPath("NoDebugDirectory.dll")), _fakeLogger).Should().BeNull();
            _fakeLogger.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractPdbPathAndParseImports_FileInFolderOutsideOfAnsiCodePage_AreRead()
        {
            // 'Ω' and the Chinese characters are not part of the ANSI code page (and could not be marshalled to imagehlp.dll)
            string directory = _directory.GetPath("测试Ω");
            Directory.CreateDirectory(directory);
            string pdbPath = Path.Combine(directory, "Tests_taef.pdb");
            string dll = new SyntheticPeFile { PdbPath = pdbPath }.WriteTo(Path.Combine(directory, "Tests_taef.dll"));
            File.WriteAllText(pdbPath, "not a real PDB");

            PeParser.ExtractPdbPath(dll, _fakeLogger).Should().Be(pdbPath);
            PdbLocator.FindPdbFile(dll, "", _fakeLogger).Should().Be(pdbPath);
            PeParser.ParseImports(dll, _fakeLogger).Should().Equal("KERNEL32.dll", "Wex.Common.dll", "Wex.Logger.dll");
            PeParser.IsTaefTestDll(dll, _fakeLogger).Should().BeTrue();
            _fakeLogger.Warnings.Should().BeEmpty();
            _fakeLogger.Errors.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_NoDll_IsNotRecognized()
        {
            AssertIsTaefTestDll(new SyntheticPeFile { IsDll = false }, false);
            _fakeLogger.Infos.Should().Contain(i => i.Contains("not a DLL"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_MalformedFiles_ReturnsFalseWithoutException()
        {
            byte[] taefDll = SyntheticPeFile.TaefTestDll().Build();
            var random = new Random(4711);
            var randomBytes = new byte[4096];
            random.NextBytes(randomBytes);
            var mzGarbage = (byte[])randomBytes.Clone();
            mzGarbage[0] = (byte)'M';
            mzGarbage[1] = (byte)'Z';

            var files = new Dictionary<string, byte[]>
            {
                { "Empty.dll", new byte[0] },
                { "MZ.dll", Encoding.ASCII.GetBytes("MZ") },
                { "Text.dll", Encoding.ASCII.GetBytes("This is not a PE file") },
                { "Random.dll", randomBytes },
                { "MzGarbage.dll", mzGarbage },
                { "HeadersOnly.dll", taefDll.Take(0x400).ToArray() },
                { "TruncatedTestData.dll", taefDll.Take(taefDll.Length - 0x200 + 4).ToArray() },
                { "PeOffsetBeyondEnd.dll", PatchUInt32(taefDll, 0x3C, 0x7FFFFFF0) },
                { "TooManySections.dll", PatchUInt16(taefDll, 0x80 + 6, 0xFFFF) }
            };

            foreach (KeyValuePair<string, byte[]> file in files)
            {
                string path = _directory.GetPath(file.Key);
                File.WriteAllBytes(path, file.Value);
                PeParser.IsTaefTestDll(path, _fakeLogger).Should().BeFalse(file.Key);
                PeParser.IsTaefTestDll(path, null).Should().BeFalse(file.Key);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_NonExistingFileOrDirectory_ReturnsFalseAndLogsDebugWarning()
        {
            PeParser.IsTaefTestDll(_directory.GetPath("DoesNotExist.dll"), _fakeLogger).Should().BeFalse();
            PeParser.IsTaefTestDll(_directory.Path, _fakeLogger).Should().BeFalse();
            PeParser.IsTaefTestDll(_directory.GetPath("DoesNotExist.dll"), null).Should().BeFalse();

            _fakeLogger.Warnings.Should().HaveCount(2).And.OnlyContain(w => w.Contains("Could not check whether"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsTaefTestDll_FileOpenedForWritingByOtherProcess_CanBeChecked()
        {
            string dll = SyntheticPeFile.TaefTestDll().WriteTo(_directory.GetPath("Open_taef.dll"));
            using (new FileStream(dll, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                PeParser.IsTaefTestDll(dll, _fakeLogger).Should().BeTrue();
                PeParser.GetMachineType(dll, _fakeLogger).Should().Be(PeParser.MachineX64);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetMachineType_SyntheticFiles_MachineOrUnknown()
        {
            foreach (ushort machine in new[] { PeParser.MachineX86, PeParser.MachineX64, PeParser.MachineArm64, PeParser.MachineArm64EC, PeParser.MachineArmNT })
            {
                string dll = new SyntheticPeFile { Machine = machine }.WriteTo(_directory.GetPath($"{machine:X4}.dll"));
                PeParser.GetMachineType(dll, _fakeLogger).Should().Be(machine);
            }

            PeParser.GetMachineType(_directory.CreateFile("Text.dll", "no PE file"), _fakeLogger).Should().Be(PeParser.MachineUnknown);
            PeParser.GetMachineType(_directory.GetPath("DoesNotExist.dll"), _fakeLogger).Should().Be(PeParser.MachineUnknown);
            PeParser.GetMachineType(_directory.GetPath("DoesNotExist.dll"), null).Should().Be(PeParser.MachineUnknown);
            _fakeLogger.Warnings.Should().Contain(w => w.Contains("not a PE file")).And.Contain(w => w.Contains("DoesNotExist.dll"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Constants_HaveExpectedValues()
        {
            PeParser.MachineX86.Should().Be(0x014C);
            PeParser.MachineX64.Should().Be(0x8664);
            PeParser.MachineArm64.Should().Be(0xAA64);
            PeParser.MachineArm64EC.Should().Be(0xA641);
            PeParser.TaefMetadataSectionName.Should().Be("testdata");
            PeParser.TaefImports.Should().BeEquivalentTo("Wex.Logger.dll", "Wex.Common.dll", "TE.Common.dll");
            PeParser.MinTaefAbiVersion.Should().Be(10);
            PeParser.MaxTaefAbiVersion.Should().Be(13);
        }

        #endregion

        private void AssertIsTaefTestDll(SyntheticPeFile peFile, bool expected, string because = "")
        {
            string dll = peFile.WriteTo(_directory.GetPath(Guid.NewGuid() + ".dll"));
            PeParser.IsTaefTestDll(dll, _fakeLogger).Should().Be(expected, because);
        }

        private static byte[] PatchUInt32(byte[] bytes, int offset, uint value)
        {
            var result = (byte[])bytes.Clone();
            Array.Copy(BitConverter.GetBytes(value), 0, result, offset, 4);
            return result;
        }

        private static byte[] PatchUInt16(byte[] bytes, int offset, ushort value)
        {
            var result = (byte[])bytes.Clone();
            Array.Copy(BitConverter.GetBytes(value), 0, result, offset, 2);
            return result;
        }

    }

}
