// This file has been modified for TAEF support.

using System.IO;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>
    /// Tests of <see cref="PdbLocator"/>. Tests which need a DLL without PDB work on copies of the sample DLLs whose
    /// embedded PDB path has been changed (the shared sample folders are never modified).
    /// </summary>
    [TestClass]
    public class PdbLocatorTests
    {
        private FakeLogger _fakeLogger;

        [TestInitialize]
        public void SetUp()
        {
            _fakeLogger = new FakeLogger(() => OutputMode.Verbose);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindPdbFile_DllWithPdb_FindsPdbViaEmbeddedPath()
        {
            string testDll = Path.GetFullPath(TestResources.LoadTests_ReleaseX86);
            testDll.AsFileInfo().Should().Exist();

            string pdbFound = PdbLocator.FindPdbFile(testDll, "", _fakeLogger);

            string pdb = Path.ChangeExtension(testDll, ".pdb");
            pdb.AsFileInfo().Should().Exist();
            pdbFound.Should().BeEquivalentTo(pdb);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindPdbFile_CopyWithBrokenPdbPathAndPdbNextToDll_FindsPdbNextToDll()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.LoadTests_ReleaseX86))
            {
                EmbeddedPdbPath.Break(sample.TestDll);

                PdbLocator.FindPdbFile(sample.TestDll, "", _fakeLogger).Should().Be(sample.Pdb);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindPdbFile_DllWithoutPdb_AttemptsToFindPdbAreLogged()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.LoadTests_ReleaseX86, copyPdb: false))
            {
                string brokenPath = EmbeddedPdbPath.Break(sample.TestDll);

                string pdbFound = PdbLocator.FindPdbFile(sample.TestDll, "", _fakeLogger);

                pdbFound.Should().BeNull();
                _fakeLogger.Infos.Should().Contain(msg => msg.Contains("Attempts to find pdb:") && msg.Contains(sample.Pdb));
                brokenPath.AsFileInfo().Should().NotExist();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindPdbFile_PdbInPathExtension_IsFound()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_ReleaseX64))
            using (var pdbDirectory = new TemporaryDirectory())
            {
                EmbeddedPdbPath.Break(sample.TestDll);
                string movedPdb = pdbDirectory.GetPath(Path.GetFileName(sample.Pdb));
                File.Move(sample.Pdb, movedPdb);

                PdbLocator.FindPdbFile(sample.TestDll, "", _fakeLogger).Should().BeNull();
                PdbLocator.FindPdbFile(sample.TestDll, pdbDirectory.Path, _fakeLogger).Should().Be(movedPdb);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindPdbFile_PathExtensionContainsInvalidChars_WarningIsLogged()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.LoadTests_ReleaseX86, copyPdb: false))
            {
                EmbeddedPdbPath.Break(sample.TestDll);

                string pdbFound = PdbLocator.FindPdbFile(sample.TestDll, "My<Invalid>Path", _fakeLogger);

                pdbFound.Should().BeNull();
                _fakeLogger.Warnings
                    .Should()
                    .Contain(msg => msg.Contains("invalid path") && msg.Contains("My<Invalid>Path"));
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindPdbFile_SyntheticDllWithPdbNextToIt_FindsPdb()
        {
            using (var directory = new TemporaryDirectory())
            {
                // synthetic PE files contain no debug directory
                string dll = SyntheticPeFile.TaefTestDll().WriteTo(directory.GetPath("Synthetic_taef.dll"));
                PdbLocator.FindPdbFile(dll, "", _fakeLogger).Should().BeNull();

                string pdb = directory.CreateFile("Synthetic_taef.pdb");
                PdbLocator.FindPdbFile(dll, "", _fakeLogger).Should().Be(pdb);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindPdbFile_NoPeFile_PdbNextToFileIsFound()
        {
            using (var directory = new TemporaryDirectory())
            {
                string dll = directory.CreateFile("NoPe.dll", "no PE file");
                string pdb = directory.CreateFile("NoPe.pdb");

                PdbLocator.FindPdbFile(dll, "", _fakeLogger).Should().Be(pdb);
                _fakeLogger.Errors.Should().BeEmpty();
            }
        }

    }
}
