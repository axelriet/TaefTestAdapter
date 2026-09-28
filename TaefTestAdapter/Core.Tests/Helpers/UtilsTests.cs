// This file has been modified for TAEF support.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using Moq;

namespace TaefTestAdapter.Helpers
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void DeleteDirectory_CanNotBeDeleted_ReturnsFalseAndMessage()
        {
            string dir = Utils.GetTempDirectory();
            SetReadonlyFlag(dir);

            string errorMessage;
            bool result = Utils.DeleteDirectory(dir, out errorMessage);

            result.Should().BeFalse();
            errorMessage.Should().Contain(dir);

            RemoveReadonlyFlag(dir);

            result = Utils.DeleteDirectory(dir, out errorMessage);

            result.Should().BeTrue();
            errorMessage.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DeleteDirectory_DirectoryWithContent_IsDeleted()
        {
            string dir = Utils.GetTempDirectory();
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            File.WriteAllText(Path.Combine(dir, "sub", "file.txt"), "content");

            Utils.DeleteDirectory(dir).Should().BeTrue();

            Directory.Exists(dir).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTempDirectory__DirectoryDoesExistAndCanBeDeleted()
        {
            string dir = Utils.GetTempDirectory();
            Directory.Exists(dir).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(dir).Should().BeEmpty();
            Utils.GetTempDirectory().Should().NotBe(dir);

            // ReSharper disable once UnusedVariable
            Utils.DeleteDirectory(dir, out string errorMessage).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetExtendedPath_WithExtension_ExtendsPath()
        {
            const string toAdd = @"c:\some\path\to\add";
            string result = Utils.GetExtendedPath(toAdd);

            string path = Environment.GetEnvironmentVariable("PATH");
            result.Should().HaveLength(path.Length + toAdd.Length + 1);
            result.Should().Contain(path);
            result.Should().StartWith(toAdd + ";");
            string[] pathParts = result.Split(';');
            pathParts.Should().Contain(s => s.Equals(toAdd));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetExtendedPath_NoExtension_ReturnsPath()
        {
            string result = Utils.GetExtendedPath("");

            string path = Environment.GetEnvironmentVariable("PATH");
            result.Should().Be(path);
            Utils.GetExtendedPath(null).Should().Be(path);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTimestamp__HasFormatHoursMinutesSecondsMilliseconds()
        {
            Utils.GetTimestamp().Should().MatchRegex(@"^\d\d:\d\d:\d\d\.\d\d\d$");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SpawnAndWait_SeveralTasks_AreExecutedInParallel()
        {
            int nrOfTasks = Math.Min(4, Environment.ProcessorCount);
            if (nrOfTasks < 2)
                Assert.Inconclusive("System does not have enough processors, skipping test");

            const int taskDurationInMs = 1000;
            int nrOfFinishedTasks = 0;

            var tasks = new Action[nrOfTasks];
            for (int i = 0; i < nrOfTasks; i++)
            {
                tasks[i] = () =>
                {
                    Thread.Sleep(taskDurationInMs);
                    Interlocked.Increment(ref nrOfFinishedTasks);
                };
            }

            var stopWatch = Stopwatch.StartNew();
            bool result = Utils.SpawnAndWait(tasks);
            stopWatch.Stop();

            result.Should().BeTrue();
            nrOfFinishedTasks.Should().Be(nrOfTasks);
            stopWatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(taskDurationInMs - TestMetadata.ToleranceInMs);
            // sequential execution would take nrOfTasks * taskDuration (generous bound for loaded machines)
            stopWatch.ElapsedMilliseconds.Should().BeLessThan((long)((nrOfTasks - 0.5) * taskDurationInMs));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SpawnAndWait_TaskWithTimeout_TimeoutsAndReturnsFalse()
        {
            const int taskDurationInMs = 3000, timeoutInMs = 250;
            var tasks = new Action[] { () => Thread.Sleep(taskDurationInMs) };

            var stopWatch = Stopwatch.StartNew();
            bool hasFinishedTasks = Utils.SpawnAndWait(tasks, timeoutInMs);
            stopWatch.Stop();

            hasFinishedTasks.Should().BeFalse();
            stopWatch.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(timeoutInMs - TestMetadata.ToleranceInMs);
            stopWatch.ElapsedMilliseconds.Should().BeLessThan(taskDurationInMs);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SpawnAndWait_TaskThrows_AggregateExceptionIsThrown()
        {
            var tasks = new Action[] { () => { }, () => throw new InvalidOperationException("task failed") };

            Action spawn = () => Utils.SpawnAndWait(tasks);

            spawn.Should().Throw<AggregateException>()
                .Which.InnerExceptions.Should().ContainSingle(e => e.Message == "task failed");
        }

        #region Test timeout (TE.exe /testTimeout)

        [TestMethod]
        [TestCategory(Unit)]
        public void IsValidTestTimeout_ValidValues_ReturnsTrue()
        {
            foreach (string value in new[] { null, "", "  ", "0", "1", "23", "0:05", "0:0:30", "1:2:3", "23:59:59", "0:0:0.5", "0:0:1.1234567", "1.02", "99999999.23:59:59.9999999", " 0:0:3 " })
            {
                Utils.IsValidTestTimeout(value).Should().BeTrue($"'{value}' is valid");
                Invoking(() => Utils.ValidateTestTimeout(value)).Should().NotThrow();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsValidTestTimeout_InvalidValues_ReturnsFalse()
        {
            // TE.exe 10.104k ignores these values with a warning
            foreach (string value in new[] { "24", "1.24", "0:60", "0:0:60", "0:90", "0:0:1.12345678", "abc", "-1", "1:2:3:4", "0:5:", ":5", "1..2", "123", "0:0:1.", "123456789.1" })
            {
                Utils.IsValidTestTimeout(value).Should().BeFalse($"'{value}' is invalid");
                Invoking(() => Utils.ValidateTestTimeout(value)).Should().Throw<ArgumentException>()
                    .Which.Message.Should().Contain(value).And.Contain(Utils.TestTimeoutFormat);
            }
        }

        #endregion

        #region Validation of options

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidateRegex_InvalidRegex_ThrowsWithPatternInMessage()
        {
            Invoking(() => Utils.ValidateRegex("Some.*Expression")).Should().NotThrow();
            Invoking(() => Utils.ValidateRegex("d[ddd[")).Should().Throw<Exception>().Which.Message.Should().Contain("d[ddd[");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidateTraitRegexes_InvalidValue_Throws()
        {
            Invoking(() => Utils.ValidateTraitRegexes("Ns::Class::.*///Type,Small//||//.*#metadataSet.*///Kind,Data,Row")).Should().NotThrow();
            Invoking(() => Utils.ValidateTraitRegexes("")).Should().NotThrow();
            Invoking(() => Utils.ValidateTraitRegexes("[[Invalid///Type,Small")).Should().Throw<Exception>();
            Invoking(() => Utils.ValidateTraitRegexes("NoSeparator")).Should().Throw<Exception>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidateEnvironmentVariables_InvalidValue_Throws()
        {
            Invoking(() => Utils.ValidateEnvironmentVariables("A=B//||//C=")).Should().NotThrow();
            Invoking(() => Utils.ValidateEnvironmentVariables("NoValue")).Should().Throw<Exception>();
            Invoking(() => Utils.ValidateEnvironmentVariables("%Invalid=Name")).Should().Throw<Exception>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void SplitAdditionalPdbs_SeveralPatterns_EmptyEntriesAreRemoved()
        {
            Utils.SplitAdditionalPdbs(@"a.pdb;;$(TestDllDir)\*.pdb;").Should().Equal("a.pdb", @"$(TestDllDir)\*.pdb");
            Utils.SplitAdditionalPdbs("").Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidatePattern_EmptyPattern_BothPartsReported()
        {
            bool result = Utils.ValidatePattern("", out string errorMessage);

            result.Should().BeFalse();
            errorMessage.Should().Contain("file pattern part");
            errorMessage.Should().Contain("path part");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidatePattern_InvalidPattern_BothPartsReported()
        {
            char[] invalidPathChars = Path.GetInvalidPathChars();
            if (invalidPathChars.Length < 1)
                Assert.Inconclusive("Cannot test invalid path chars as none are reported.");

            bool result = Utils.ValidatePattern(""+invalidPathChars[0], out string errorMessage);

            result.Should().BeFalse();
            errorMessage.Should().Contain("file pattern part");
            errorMessage.Should().Contain("path part");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidatePattern_TempDir_FilePartReported()
        {
            bool result = Utils.ValidatePattern(Path.GetTempPath(), out string errorMessage);

            result.Should().BeFalse();
            errorMessage.Should().Contain("file pattern part");
            errorMessage.Should().NotContain("path part");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidatePattern_UncRoot_PathPartReported()
        {
            // the root of a UNC path has no directory part
            bool result = Utils.ValidatePattern(@"\\server\share", out string errorMessage);

            result.Should().BeFalse();
            errorMessage.Should().NotContain("file pattern part");
            errorMessage.Should().Contain("path part");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ValidatePattern_ValidInput_ValidationSuceeds()
        {
            bool result = Utils.ValidatePattern(@"C:\foo\Bar.pdb", out string errorMessage);

            result.Should().BeTrue();
            errorMessage.Should().BeNullOrEmpty();
        }

        #endregion

        #region GetMatchingFiles

        [TestMethod]
        [TestCategory(Unit)]
        public void GetMatchingFiles_ValidInput_ReturnsValidList()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.CreateFile("My_taef.dll");
                string pdb = directory.CreateFile("My_taef.pdb");
                directory.CreateFile("Other.txt");
                directory.CreateFile(@"sub\Nested_taef.dll");
                var mockLogger = new Mock<ILogger>();

                string[] result = Utils.GetMatchingFiles(Path.Combine(directory.Path, "*.dll"), mockLogger.Object);
                result.Should().Equal(testDll);

                result = Utils.GetMatchingFiles(Path.Combine(directory.Path, "My_taef.*"), mockLogger.Object);
                result.Should().BeEquivalentTo(testDll, pdb);

                mockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetMatchingFiles_InvalidInput_ErrorReported()
        {
            var mockLogger = new Mock<ILogger>();
            string[] result = Utils.GetMatchingFiles(null, mockLogger.Object);

            result.Length.Should().Be(0);
            mockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetMatchingFiles_NonExistingPath_ErrorReported()
        {
            var mockLogger = new Mock<ILogger>();
            string[] result = Utils.GetMatchingFiles(@"some\non\exisiting\path", mockLogger.Object);

            result.Length.Should().Be(0);
            mockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetMatchingFiles_SampleTestsFolder_ContainsAllSampleDlls()
        {
            var mockLogger = new Mock<ILogger>();
            string[] result = Utils.GetMatchingFiles(TestResources.GetSampleDirectory(SampleConfiguration.DebugX86) + "*_taef.dll", mockLogger.Object);

            result.Select(Path.GetFileName).Should().BeEquivalentTo(TestResources.AllSampleTestDlls);
            result.Should().Contain(s => s.Equals(TestResources.Tests_DebugX86, StringComparison.OrdinalIgnoreCase));
            mockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        #endregion

        private static Action Invoking(Action action) => action;

        private void SetReadonlyFlag(string dir)
        {
            FileAttributes fileAttributes = File.GetAttributes(dir);
            fileAttributes |= FileAttributes.ReadOnly;
            File.SetAttributes(dir, fileAttributes);
        }

        private void RemoveReadonlyFlag(string dir)
        {
            FileAttributes fileAttributes = File.GetAttributes(dir);
            fileAttributes = fileAttributes & ~FileAttributes.ReadOnly;
            File.SetAttributes(dir, fileAttributes);
        }

    }

}
