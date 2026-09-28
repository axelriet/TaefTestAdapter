// This file has been added for TAEF support.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Tests of <see cref="TeArguments"/>: ASCII alternatives for paths TE.exe can not handle (test DLLs and WTT logs in
    /// folders with non-ASCII characters), and extraction of the user's test selection from additional TE.exe arguments.
    /// </summary>
    [TestClass]
    public class TeArgumentsTests : TestsBase
    {
        private const string NonAsciiDir = @"C:\Users\Jürgen\source\repos\Tests\x64\Debug";
        private const string NonAsciiDll = NonAsciiDir + @"\Sample_taef.dll";
        private const string ShortDir = @"C:\Users\JRGEN~1\source\repos\Tests\x64\Debug";
        private const string ShortDll = ShortDir + @"\SAMPLE~1.DLL";

        private static string NoShortPath(string path) => null;

        private void VerifyNoErrorLogged() => MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);

        #region IsAscii, GetShortPath

        [TestMethod]
        [TestCategory(Unit)]
        public void IsAscii_VariousStrings_OnlyAsciiCharactersAreAscii()
        {
            TeArguments.IsAscii(null).Should().BeTrue();
            TeArguments.IsAscii("").Should().BeTrue();
            TeArguments.IsAscii(@"C:\Program Files (x86)\a~1 !#$%&'()+,;=@[]^_`{}~" + "\x7F").Should().BeTrue();
            TeArguments.IsAscii(@"C:\dirü").Should().BeFalse();
            TeArguments.IsAscii(@"C:\dir名").Should().BeFalse();
            TeArguments.IsAscii("\x80").Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetShortPath_NotExistingOrEmptyPath_ReturnsNull()
        {
            TeArguments.GetShortPath(null).Should().BeNull();
            TeArguments.GetShortPath("").Should().BeNull();
            TeArguments.GetShortPath(@"C:\does\not\exist\ü.dll").Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetShortPath_ExistingFileInNonAsciiFolder_ReturnsPathOfSameFile()
        {
            using (var directory = new TemporaryDirectory())
            {
                string file = directory.CreateFile(Path.Combine("dirü名", "Tëst_taef.dll"), "content");

                string shortPath = TeArguments.GetShortPath(file);

                shortPath.Should().NotBeNullOrEmpty();
                File.ReadAllText(shortPath).Should().Be("content");
                if (!TeArguments.IsAscii(shortPath))
                    Assert.Inconclusive($"8.3 file names seem to be disabled for the volume of '{directory.Path}' (short path: '{shortPath}')");
                Path.GetFileName(shortPath).Should().NotBe("Tëst_taef.dll");
            }
        }

        #endregion

        #region GetTestDllArgument

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestDllArgument_AsciiPath_FullPathIsReturned()
        {
            TeArguments.GetTestDllArgument(@"C:\tests\Sample_taef.dll", @"C:\other", MockLogger.Object, p => throw new InvalidOperationException())
                .Should().Be(@"C:\tests\Sample_taef.dll");
            TeArguments.GetTestDllArgument("Sample_taef.dll", null, MockLogger.Object)
                .Should().Be(Path.GetFullPath("Sample_taef.dll"));
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestDllArgument_NonAsciiPathAndWorkingDirIsFolderOfTestDll_FileNameIsReturned()
        {
            foreach (string workingDir in new[] { NonAsciiDir, NonAsciiDir + @"\", NonAsciiDir.ToUpperInvariant(), $"\"{NonAsciiDir}\"" })
            {
                TeArguments.GetTestDllArgument(NonAsciiDll, workingDir, MockLogger.Object, p => ShortDll)
                    .Should().Be("Sample_taef.dll", workingDir);
            }
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestDllArgument_NonAsciiPathAndOtherWorkingDir_ShortPathIsReturned()
        {
            foreach (string workingDir in new[] { null, "", @"C:\other", NonAsciiDir + @"\sub" })
            {
                TeArguments.GetTestDllArgument(NonAsciiDll, workingDir, MockLogger.Object, p => p == NonAsciiDll ? ShortDll : null)
                    .Should().Be(ShortDll, workingDir);
            }
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestDllArgument_NonAsciiFileNameInWorkingDir_ShortPathIsReturned()
        {
            const string testDll = @"C:\tests\Tëst_taef.dll";

            TeArguments.GetTestDllArgument(testDll, @"C:\tests", MockLogger.Object, p => @"C:\tests\TST_TA~1.DLL")
                .Should().Be(@"C:\tests\TST_TA~1.DLL");
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestDllArgument_NoAsciiAlternative_ErrorIsLoggedAndFullPathIsReturned()
        {
            // no short path, or 8.3 names disabled (the "short" path is the long path)
            foreach (Func<string, string> getShortPath in new Func<string, string>[] { NoShortPath, p => p })
            {
                MockLogger.Invocations.Clear();

                TeArguments.GetTestDllArgument(NonAsciiDll, @"C:\other", MockLogger.Object, getShortPath).Should().Be(NonAsciiDll);

                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains(NonAsciiDll) && s.Contains("non-ASCII characters")
                    && s.Contains(@"C:\other") && s.Contains(Settings.SettingsWrapper.OptionWorkingDir))), Times.Once);
            }

            // no logger
            TeArguments.GetTestDllArgument(NonAsciiDll, null, null, NoShortPath).Should().Be(NonAsciiDll);
        }

        #endregion

        #region CreateWttLogFile

        private static void AssertIsNewWttLogFileName(string fileName)
        {
            fileName.Should().MatchRegex(@"^TaefTestAdapter_[0-9a-f]{32}\.wtl$");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateWttLogFile_AsciiTempDir_FileInTempDirIsPassedAsIs()
        {
            TeArguments.WttLogFile logFile = TeArguments.CreateWttLogFile(@"C:\Users\Juergen\AppData\Local\Temp\", NonAsciiDir, "", MockLogger.Object,
                p => throw new InvalidOperationException());

            Path.GetDirectoryName(logFile.File).Should().Be(@"C:\Users\Juergen\AppData\Local\Temp");
            AssertIsNewWttLogFileName(Path.GetFileName(logFile.File));
            logFile.Argument.Should().Be(logFile.File);
            TeArguments.CreateWttLogFile(@"C:\Temp", null, null, MockLogger.Object).File.Should().NotBe(logFile.File, "each log file is new");
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateWttLogFile_NonAsciiTempDirWithShortPath_FileInShortTempDirIsPassed()
        {
            TeArguments.WttLogFile logFile = TeArguments.CreateWttLogFile(@"C:\Users\Jürgen\AppData\Local\Temp\", NonAsciiDir, "", MockLogger.Object,
                p => p == @"C:\Users\Jürgen\AppData\Local\Temp\" ? @"C:\Users\JRGEN~1\AppData\Local\Temp\" : null);

            Path.GetDirectoryName(logFile.File).Should().Be(@"C:\Users\Jürgen\AppData\Local\Temp");
            AssertIsNewWttLogFileName(Path.GetFileName(logFile.File));
            logFile.Argument.Should().Be(@"C:\Users\JRGEN~1\AppData\Local\Temp\" + Path.GetFileName(logFile.File));
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateWttLogFile_NonAsciiTempDirWithoutShortPath_FileNameRelativeToWorkingDirIsPassed()
        {
            foreach (Func<string, string> getShortPath in new Func<string, string>[] { NoShortPath, p => p })
            {
                TeArguments.WttLogFile logFile = TeArguments.CreateWttLogFile(@"C:\Users\Jürgen\AppData\Local\Temp", NonAsciiDir, "/p:\"A=B\"", MockLogger.Object, getShortPath);

                AssertIsNewWttLogFileName(logFile.Argument);
                logFile.File.Should().Be(Path.Combine(NonAsciiDir, logFile.Argument), "TE.exe resolves the relative path against its working directory");
            }
            VerifyNoErrorLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateWttLogFile_NoAsciiAlternative_ErrorIsLoggedAndFullPathIsPassed()
        {
            const string tempDir = @"C:\Users\Jürgen\AppData\Local\Temp";

            // no working dir, or TE.exe would resolve the relative path against the user's output folder
            foreach ((string workingDir, string userParameters) in new (string, string)[] { (null, ""), (NonAsciiDir, "/p:\"A=B\" /outputFolder:\"C:\\out\""), (NonAsciiDir, "-OUTPUTFOLDER:out") })
            {
                MockLogger.Invocations.Clear();

                TeArguments.WttLogFile logFile = TeArguments.CreateWttLogFile(tempDir, workingDir, userParameters, MockLogger.Object, NoShortPath);

                Path.GetDirectoryName(logFile.File).Should().Be(tempDir);
                logFile.Argument.Should().Be(logFile.File);
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains(tempDir) && s.Contains("non-ASCII characters") && s.Contains("TMP"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void CreateWttLogFile_RealNonAsciiTempDir_ArgumentIsAsciiAndDenotesTheFile()
        {
            using (var directory = new TemporaryDirectory())
            {
                string tempDir = directory.GetPath("Temp_Jürgen_名前");
                string workingDir = directory.GetPath("wörk");
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(workingDir);

                TeArguments.WttLogFile logFile = TeArguments.CreateWttLogFile(tempDir, workingDir, "", MockLogger.Object);

                TeArguments.IsAscii(logFile.Argument).Should().BeTrue(logFile.Argument);
                string argumentAsPath = Path.IsPathRooted(logFile.Argument) ? logFile.Argument : Path.Combine(workingDir, logFile.Argument);
                File.WriteAllText(argumentAsPath, "log");
                File.ReadAllText(logFile.File).Should().Be("log");
                VerifyNoErrorLogged();
            }
        }

        #endregion

        #region ExtractSelection

        private static (string Query, string Remaining) Extract(string arguments)
        {
            string query = TeArguments.ExtractSelection(arguments, out string remaining);
            return (query, remaining);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractSelection_NoSelection_ArgumentsAreUnchanged()
        {
            Extract(null).Should().Be(((string)null, ""));
            Extract("").Should().Be(((string)null, ""));
            Extract("  /p:\"TestDirectory=C:\\my  dir\"   /runas:Elevated ").Should().Be(((string)null, "/p:\"TestDirectory=C:\\my  dir\"   /runas:Elevated"));
            // a value containing the switch, a quoted argument (a file expression for TE.exe), switches without value
            Extract("/p:\"X= /select:\"@Name='A'\"").Query.Should().BeNull();
            Extract("\"/select:@Name='A' or @Name='B'\"").Should().Be(((string)null, "\"/select:@Name='A' or @Name='B'\""));
            Extract("/select: /name:").Should().Be(((string)null, "/select: /name:"));
            Extract("/selection:\"x\" /names:y").Should().Be(((string)null, "/selection:\"x\" /names:y"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractSelection_Select_QueryIsReturnedAndSwitchIsRemoved()
        {
            Extract("/select:\"@Name='A::*' or @Priority=1\"").Should().Be(("@Name='A::*' or @Priority=1", ""));
            Extract("/p:\"A=B C\" /select:@Priority=1 /runas:Elevated").Should().Be(("@Priority=1", "/p:\"A=B C\" /runas:Elevated"));
            Extract("-SELECT:\"@Name='x'\"").Should().Be(("@Name='x'", ""));
            Extract("/Select:\"(@Priority=1) and not @Name='a b'\"").Query.Should().Be("(@Priority=1) and not @Name='a b'");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractSelection_Name_NameTermIsReturnedAndSwitchIsRemoved()
        {
            Extract("/name:*Test* /p:x=5").Should().Be(("@Name='*Test*'", "/p:x=5"));
            Extract("/p:x=5 -Name:\"TaefSamples::NamedRows::SpecialCharacters#with space\"").Should().Be(("@Name='TaefSamples::NamedRows::SpecialCharacters#with space'", "/p:x=5"));
            // TE.exe selects no test then (but is fine with it)
            Extract("/name:\"\"").Query.Should().Be("@Name=''");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExtractSelection_SeveralSelections_SelectionOfTeIsReturnedAndAllSwitchesAreRemoved()
        {
            // verified with TE.exe 10.104k: the last /select wins, a /name only takes effect if no selection has been specified before
            Extract("/select:\"@Name='A'\" /p:x=1 /select:\"@Name='B'\"").Should().Be(("@Name='B'", "/p:x=1"));
            Extract("/name:A /name:B").Should().Be(("@Name='A'", ""));
            Extract("/select:\"@Name='A'\" /name:B").Should().Be(("@Name='A'", ""));
            Extract("/name:A /select:\"@Name='B'\"").Should().Be(("@Name='B'", ""));
            Extract("/name:A /select:\"@Name='B'\" /name:C").Should().Be(("@Name='B'", ""));
            // an empty query selects all tests
            Extract("/select:\"\" /name:A").Should().Be(("@Name='A'", ""));
            Extract("/name:A /select:\"\"").Should().Be(((string)null, ""));
            Extract("/select:\"@Name='A'\" /select:\"\" /p:x=1").Should().Be(((string)null, "/p:x=1"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ContainsSwitch_VariousArguments_SwitchesWithValueAreFound()
        {
            TeArguments.ContainsSwitch("/p:x=1 /outputFolder:\"C:\\my dir\"", "outputFolder").Should().BeTrue();
            TeArguments.ContainsSwitch("-OUTPUTFOLDER:C:\\x", "outputFolder").Should().BeTrue();
            TeArguments.ContainsSwitch("/p:\"outputFolder:x\" /outputFolder:", "outputFolder").Should().BeFalse();
            TeArguments.ContainsSwitch(null, "outputFolder").Should().BeFalse();
        }

        #endregion

    }

}
