// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Tests of the TE.exe command lines created by <see cref="CommandLineGenerator"/>: exact switches and their order,
    /// <c>/select</c> queries (class and name terms, escaping, combination with the user's selection), ignored tests,
    /// splitting of long command lines, and test DLLs whose path contains non-ASCII characters.
    /// </summary>
    [TestClass]
    public class CommandLineGeneratorTests : TestsBase
    {
        private const string TestDll = @"C:\tests\Sample_taef.dll";
        private const string TeExecutable = @"C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF\x64\TE.exe";

        private const string BaseCommandLine = "\"" + TestDll + "\" /unicodeOutput:false /coloredConsoleOutput:false";

        private static string Select(params string[] terms) => $" /select:\"{string.Join(" or ", terms)}\"";

        private static string Name(string name) => $"@Name='{name}'";

        private static string Class(string className) => $"(@Name='{className}::*' and not @Name='{className}::*::*')";

        private List<CommandLineGenerator.Args> Generate(IEnumerable<TestCase> testCases, string userParameters = "",
            bool isBeingDebugged = false, string wttLogFile = null, int lengthOfTeExecutable = -1, string testDll = TestDll, string workingDir = null)
        {
            if (lengthOfTeExecutable < 0)
                lengthOfTeExecutable = TeExecutable.Length;
            return new CommandLineGenerator(testCases, testDll, lengthOfTeExecutable, userParameters, isBeingDebugged, MockOptions.Object, wttLogFile,
                    workingDir, MockLogger.Object)
                .GetCommandLines()
                .ToList();
        }

        private static List<TestCase> TestCases(IEnumerable<string> namesToRun, IEnumerable<string> allNames, IEnumerable<string> ignoredNames = null)
            => RunnerTestData.CreateTestCases(TestDll, namesToRun, allNames, ignoredNames);

        /// <summary>The test cases "A::X", "A::Y" (all tests of the DLL: "A::X", "A::Y", "B::Z").</summary>
        private static List<TestCase> SubsetOfTests => TestCases(new[] { "A::X" }, new[] { "A::X", "A::Y", "B::Z" });

        #region Arguments

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor_InvalidArguments_Throw()
        {
            var testCases = new List<TestCase>();

            // ReSharper disable ObjectCreationAsStatement
            new Action(() => new CommandLineGenerator(null, TestDll, 0, "", false, MockOptions.Object)).Should().Throw<ArgumentNullException>();
            new Action(() => new CommandLineGenerator(testCases, null, 0, "", false, MockOptions.Object)).Should().Throw<ArgumentException>();
            new Action(() => new CommandLineGenerator(testCases, " ", 0, "", false, MockOptions.Object)).Should().Throw<ArgumentException>();
            new Action(() => new CommandLineGenerator(testCases, TestDll, -1, "", false, MockOptions.Object)).Should().Throw<ArgumentOutOfRangeException>();
            new Action(() => new CommandLineGenerator(testCases, TestDll, 0, null, false, MockOptions.Object)).Should().Throw<ArgumentNullException>();
            new Action(() => new CommandLineGenerator(testCases, TestDll, 0, "", false, null)).Should().Throw<ArgumentNullException>();
            // ReSharper restore ObjectCreationAsStatement
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void MaxCommandLength_Is30000()
        {
            CommandLineGenerator.MaxCommandLength.Should().Be(30000);
            CommandLineGenerator.MaxCommandLength.Should().Be(TaefConstants.MaxCommandLength);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_NoTests_NoCommandLines()
        {
            Generate(new List<TestCase>()).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllTestsOfTestDll_DefaultArgumentsWithoutSelection()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y", "B::Z");

            List<CommandLineGenerator.Args> commandLines = Generate(testCases);

            commandLines.Should().ContainSingle();
            commandLines[0].CommandLine.Should().Be(BaseCommandLine);
            commandLines[0].TestCases.Should().Equal(testCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_UserParameters_AreInsertedAfterTestDll()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X");

            string commandLine = Generate(testCases, "  /p:\"TestDirectory=C:\\my dir\" /runas:Elevated ").Single().CommandLine;

            commandLine.Should().Be("\"" + TestDll + "\" /p:\"TestDirectory=C:\\my dir\" /runas:Elevated /unicodeOutput:false /coloredConsoleOutput:false");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_UserSelection_IsCombinedWithAdaptersSelection()
        {
            string commandLine = Generate(SubsetOfTests, "/p:\"A=B\" /select:\"@Name='A::*' or @Priority=1\" /runas:Elevated").Single().CommandLine;

            // a single selection: TE.exe would ignore the user's selection otherwise (and warn about several selections)
            commandLine.Should().Be("\"" + TestDll + "\" /p:\"A=B\" /runas:Elevated /unicodeOutput:false /coloredConsoleOutput:false" +
                                    " /select:\"(@Name='A::*' or @Priority=1) and (@Name='A::X')\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_UserSelectionAndAllDiscoveredTestsOfClass_ClassTermIsRestrictedByUserSelection()
        {
            // discovery with the user's selection has found the tests A::X, A::Y and B::Z (the classes have further tests,
            // e.g. manual ones, which the user's selection excludes)
            List<TestCase> testCases = TestCases(new[] { "A::X", "A::Y" }, new[] { "A::X", "A::Y", "B::Z" });

            CommandLineGenerator.Args args = Generate(testCases, "/select:\"not @Category='Manual'\"").Single();

            args.CommandLine.Should().Be(BaseCommandLine + " /select:\"(not @Category='Manual') and (" + Class("A") + ")\"");
            args.TestCases.Should().Equal(testCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_UserName_IsCombinedWithAdaptersSelectionAsNameTerm()
        {
            List<TestCase> testCases = TestCases(new[] { "A::X", "B::Z" }, new[] { "A::X", "A::Y", "B::Z", "B::W" });

            // TE.exe ignores a /name following a selection
            Generate(testCases, "/name:*::X /p:x=1 -name:ignored").Single().CommandLine.Should().Be(
                "\"" + TestDll + "\" /p:x=1 /unicodeOutput:false /coloredConsoleOutput:false /select:\"(@Name='*::X') and (@Name='A::X' or @Name='B::Z')\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_UserSelectionAndAllTestsOfTestDll_UserParametersAreUnchangedAndNoSelectionIsAdded()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases(TestDll, "A::X", "A::Y", "B::Z");

            Generate(testCases, " /name:A /select:\"@Priority=1\"  /p:x=1 ").Single().CommandLine
                .Should().Be("\"" + TestDll + "\" /name:A /select:\"@Priority=1\"  /p:x=1 /unicodeOutput:false /coloredConsoleOutput:false");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_EmptyUserSelection_IsRemovedAndNotCombined()
        {
            // /select:"" selects all tests
            Generate(SubsetOfTests, "/select:\"\" /p:x=1").Single().CommandLine
                .Should().Be("\"" + TestDll + "\" /p:x=1 /unicodeOutput:false /coloredConsoleOutput:false" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RelativeTestDll_IsPassedAsAbsolutePath()
        {
            List<TestCase> testCases = RunnerTestData.CreateTestCases("Sample_taef.dll", "A::X");

            string commandLine = new CommandLineGenerator(testCases, "Sample_taef.dll", TeExecutable.Length, "", false, MockOptions.Object)
                .GetCommandLines().Single().CommandLine;

            commandLine.Should().StartWith("\"" + Path.GetFullPath("Sample_taef.dll") + "\" ");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllOptionsSet_SwitchesInCorrectOrder()
        {
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);
            MockOptions.Setup(o => o.BreakOnError).Returns(true);
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(3);
            MockOptions.Setup(o => o.TestTimeout).Returns("0:0:5");
            MockOptions.Setup(o => o.IsolationLevel).Returns(TaefIsolationLevel.Class);

            string commandLine = Generate(SubsetOfTests, "/p:\"A=B\"").Single().CommandLine;

            // no /breakOnError without debugger (TE.exe would crash on the first error)
            commandLine.Should().Be("\"" + TestDll + "\" /p:\"A=B\" /unicodeOutput:false /coloredConsoleOutput:false /runIgnoredTests " +
                                    "/testmode:Loop /Loop:3 /LoopTest:1 /testTimeout:0:0:5 /isolationLevel:Class" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllOptionsSetAndBeingDebugged_DebuggingSwitchesAndNoTimeout()
        {
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);
            MockOptions.Setup(o => o.BreakOnError).Returns(true);
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(3);
            MockOptions.Setup(o => o.TestTimeout).Returns("0:0:5");
            MockOptions.Setup(o => o.IsolationLevel).Returns(TaefIsolationLevel.Class);

            string commandLine = Generate(SubsetOfTests, "/p:\"A=B\"", isBeingDebugged: true).Single().CommandLine;

            // no timeout and isolation level with /inproc (TE.exe can not start further test hosts), each test is repeated in a single loop
            commandLine.Should().Be("\"" + TestDll + "\" /p:\"A=B\" /unicodeOutput:false /coloredConsoleOutput:false /runIgnoredTests /breakOnError " +
                                    "/testmode:Loop /Loop:1 /LoopTest:3 /inproc /disableTimeouts" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_BeingDebugged_InprocAndDisableTimeouts()
        {
            Generate(SubsetOfTests, isBeingDebugged: true).Single().CommandLine
                .Should().Be(BaseCommandLine + " /inproc /disableTimeouts" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_BreakOnErrorWithoutDebugger_SwitchIsNotPassed()
        {
            MockOptions.Setup(o => o.BreakOnError).Returns(true);

            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RunInProcess_InprocWithoutDisableTimeoutsAndWithoutTestTimeout()
        {
            MockOptions.Setup(o => o.RunInProcess).Returns(true);
            MockOptions.Setup(o => o.TestTimeout).Returns("0:1");

            // TE.exe ignores /testTimeout with /inproc (and warns about it for every test)
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /inproc" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RunInProcess_NoSwitchesRequiringFurtherTestHosts()
        {
            MockOptions.Setup(o => o.RunInProcess).Returns(true);
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(3);
            MockOptions.Setup(o => o.IsolationLevel).Returns(TaefIsolationLevel.Test);
            MockOptions.Setup(o => o.TestTimeout).Returns("0:0:5");

            // TE.exe would block all tests needing another test host ("TAEF would need to start a second test host, but the
            // /InProc switch is being used"): no isolation level, and no /Loop:3 (each loop needs new test hosts)
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /testmode:Loop /Loop:1 /LoopTest:3 /inproc" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_RepetitionsInProcessOrWhileDebugging_EachTestIsRepeatedInSingleLoop()
        {
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(1);
            MockOptions.Setup(o => o.RunInProcess).Returns(true);
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /inproc" + Select(Name("A::X")));

            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(2);
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /testmode:Loop /Loop:1 /LoopTest:2 /inproc" + Select(Name("A::X")));

            MockOptions.Setup(o => o.RunInProcess).Returns(false);
            Generate(SubsetOfTests, isBeingDebugged: true).Single().CommandLine
                .Should().Be(BaseCommandLine + " /testmode:Loop /Loop:1 /LoopTest:2 /inproc /disableTimeouts" + Select(Name("A::X")));
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /testmode:Loop /Loop:2 /LoopTest:1" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_TestTimeout_IsPassedIfNotEmpty()
        {
            MockOptions.Setup(o => o.TestTimeout).Returns("1.2:3:4.5");
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /testTimeout:1.2:3:4.5" + Select(Name("A::X")));

            MockOptions.Setup(o => o.TestTimeout).Returns("  ");
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_Repetitions_LoopSwitchesOnlyForMoreThanOneRepetition()
        {
            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(1);
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X")));

            MockOptions.Setup(o => o.NrOfTestRepetitions).Returns(4711);
            Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + " /testmode:Loop /Loop:4711 /LoopTest:1" + Select(Name("A::X")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_IsolationLevels_AreMappedToSwitch()
        {
            var expected = new Dictionary<TaefIsolationLevel, string>
            {
                { TaefIsolationLevel.Default, "" },
                { TaefIsolationLevel.Test, " /isolationLevel:Test" },
                { TaefIsolationLevel.Method, " /isolationLevel:Method" },
                { TaefIsolationLevel.Class, " /isolationLevel:Class" },
                { TaefIsolationLevel.Module, " /isolationLevel:Module" }
            };

            foreach (KeyValuePair<TaefIsolationLevel, string> pair in expected)
            {
                MockOptions.Setup(o => o.IsolationLevel).Returns(pair.Key);
                Generate(SubsetOfTests).Single().CommandLine.Should().Be(BaseCommandLine + pair.Value + Select(Name("A::X")), pair.Key.ToString());
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_WttLogFile_WttSwitchesBeforeSelection()
        {
            Generate(SubsetOfTests, isBeingDebugged: true, wttLogFile: @"C:\tmp\my log.wtl").Single().CommandLine
                .Should().Be(BaseCommandLine + " /inproc /disableTimeouts /enableWttLogging /logFile:\"C:\\tmp\\my log.wtl\"" + Select(Name("A::X")));

            Generate(RunnerTestData.CreateTestCases(TestDll, "A::X"), wttLogFile: @"C:\tmp\x.wtl").Single().CommandLine
                .Should().Be(BaseCommandLine + " /enableWttLogging /logFile:\"C:\\tmp\\x.wtl\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_NonAsciiTestDllPathAndWorkingDirIsFolderOfTestDll_FileNameIsPassed()
        {
            // TE.exe can not open test DLLs whose path contains non-ASCII characters
            const string testDll = @"C:\Users\Jürgen\tests\Sample_taef.dll";
            List<TestCase> testCases = RunnerTestData.CreateTestCases(testDll, new[] { "A::X" }, new[] { "A::X", "A::Y" });

            CommandLineGenerator.Args args = Generate(testCases, testDll: testDll, workingDir: @"C:\Users\Jürgen\tests").Single();

            args.CommandLine.Should().Be("\"Sample_taef.dll\" /unicodeOutput:false /coloredConsoleOutput:false" + Select(Name("A::X")));
            args.TestCases.Single().Source.Should().Be(testDll, "the adapter keeps using the original path");
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_NonAsciiTestDllPathWithoutAsciiAlternative_FullPathIsPassedAndErrorIsLogged()
        {
            // the test DLL does not exist, so it has no 8.3 short path
            const string testDll = @"C:\does\not\exist\dirü\Sample_taef.dll";
            List<TestCase> testCases = RunnerTestData.CreateTestCases(testDll, "A::X");

            Generate(testCases, testDll: testDll, workingDir: @"C:\other").Single().CommandLine
                .Should().Be("\"" + testDll + "\" /unicodeOutput:false /coloredConsoleOutput:false");
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains(testDll) && s.Contains("non-ASCII"))), Times.Once);
        }

        #endregion

        #region Selection

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_SomeTestsOfClass_TestsAreSelectedByName()
        {
            List<TestCase> testCases = TestCases(new[] { "A::X", "A::Z" }, new[] { "A::X", "A::Y", "A::Z", "B::U" });

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X"), Name("A::Z")));
            args.TestCases.Should().Equal(testCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllTestsOfClass_ClassIsSelectedWithoutNestedClasses()
        {
            List<TestCase> testCases = TestCases(new[] { "A::X", "A::Y", "A::Z" }, new[] { "A::X", "A::Y", "A::Z", "B::U" });

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Class("A")));
            args.CommandLine.Should().EndWith(" /select:\"(@Name='A::*' and not @Name='A::*::*')\"");
            args.TestCases.Should().Equal(testCases);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_WholeClassAndSingleTestOfOtherClass_ClassAndNameTerms()
        {
            List<TestCase> testCases = TestCases(
                new[] { "FooClass::BarTest", "FooClass::BazTest", "BarClass::BazTest1" },
                new[] { "FooClass::BarTest", "FooClass::BazTest", "BarClass::BazTest1", "BarClass::BazTest2" });

            Generate(testCases).Single().CommandLine.Should().Be(BaseCommandLine + Select(Class("FooClass"), Name("BarClass::BazTest1")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_OrderOfTests_ClassesInOrderOfFirstAppearance()
        {
            List<TestCase> testCases = TestCases(
                new[] { "A::1", "B::1", "C::1" },
                new[] { "A::1", "A::2", "B::1", "B::2", "C::1" });
            testCases.Reverse();

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Class("C"), Name("B::1"), Name("A::1")));
            args.TestCases.Select(tc => tc.FullyQualifiedName).Should().Equal("C::1", "B::1", "A::1");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_NestedClasses_ClassTermsDoNotSelectNestedClasses()
        {
            string[] allNames = { "Outer::T1", "Outer::Inner::T2", "Outer::Inner::T3", "Other::X" };

            Generate(TestCases(new[] { "Outer::T1", "Outer::Inner::T2", "Outer::Inner::T3" }, allNames)).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Class("Outer"), Class("Outer::Inner")));

            Generate(TestCases(new[] { "Outer::T1", "Outer::Inner::T2" }, allNames)).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Class("Outer"), Name("Outer::Inner::T2")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DataRowsWhoseNamesContainScopeSeparator_AreSelectedByName()
        {
            string[] rows =
            {
                "TaefSamples::NamedRows::SpecialCharacters#with space", "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]",
                "TaefSamples::NamedRows::SpecialCharacters#with'quote", "TaefSamples::NamedRows::SpecialCharacters#with#hash"
            };
            List<TestCase> testCases = TestCases(rows, rows.Concat(new[] { "Other::X" }));

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Class("TaefSamples::NamedRows"), Name("TaefSamples::NamedRows::SpecialCharacters#with::colons [x]")));
            args.TestCases.Select(tc => tc.FullyQualifiedName).Should().Equal(
                "TaefSamples::NamedRows::SpecialCharacters#with space", "TaefSamples::NamedRows::SpecialCharacters#with'quote",
                "TaefSamples::NamedRows::SpecialCharacters#with#hash", "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_SingleQuotesInNames_AreDoubled()
        {
            string[] allNames =
            {
                "TaefSamples::NamedRows::SpecialCharacters#with'quote", "TaefSamples::NamedRows::SpecialCharacters#other",
                "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test", "Other::X"
            };

            Generate(TestCases(new[] { "TaefSamples::NamedRows::SpecialCharacters#with'quote", "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test" }, allNames))
                .Single().CommandLine.Should().Be(BaseCommandLine + Select(
                    "@Name='TaefSamples::NamedRows::SpecialCharacters#with''quote'",
                    "(@Name='TaefSamples::`anonymous-namespace''::Namespace_Anon::*' and not @Name='TaefSamples::`anonymous-namespace''::Namespace_Anon::*::*')"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DoubleQuotesAndControlCharactersInNames_AreReplacedByWildcard()
        {
            string[] allNames = { "Q::T#say \"hi\"", "Q::T#tab\there", "Q::Other", "Q\"uote::A", "Z::Z" };

            Generate(TestCases(new[] { "Q::T#say \"hi\"", "Q::T#tab\there", "Q\"uote::A" }, allNames)).Single().CommandLine
                .Should().Be(BaseCommandLine + Select("@Name='Q::T#say ?hi?'", "@Name='Q::T#tab?here'", "@Name='Q?uote::A'"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_ClassNamesWithWildcardCharacters_TestsAreSelectedByName()
        {
            string[] classTests = { "TaefSamples::TemplateTests<char *>::A", "TaefSamples::TemplateTests<char *>::B" };

            // '?' also matches '*', but only a single character (i.e. not e.g. TaefSamples::TemplateTests<char * *>::A)
            Generate(TestCases(classTests, classTests.Concat(new[] { "Other::X" }))).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Name("TaefSamples::TemplateTests<char ?>::A"), Name("TaefSamples::TemplateTests<char ?>::B")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DataRowsWithWildcardCharactersInNames_AreRestrictedToTheirDataRowIndex()
        {
            // verified with TE.exe 10.104k: "(@Name='WildNs::Wild::Rows#a?' and @Data:Index=0)" only selects row "a*",
            // "@Name='WildNs::Wild::Rows#a?'" alone also row "ab"
            string[] allNames = { "W::Rows#a*", "W::Rows#ab", "W::Rows#a?c", "W::Rows#abc", "W::Rows#q\"x", "W::Rows#qyx", "W::Other" };
            List<TestCase> testCases = TestCases(new[] { "W::Rows#a*", "W::Rows#ab", "W::Rows#a?c", "W::Rows#q\"x" }, allNames);
            for (int i = 0; i < testCases.Count; i++)
            {
                testCases[i].Properties.Add(new Model.TestCaseDataRowIndexProperty(i));
            }

            Generate(testCases).Single().CommandLine.Should().Be(BaseCommandLine + Select(
                "(@Name='W::Rows#a?' and @Data:Index=0)",
                Name("W::Rows#ab"),
                "(@Name='W::Rows#a?c' and @Data:Index=2)",
                "(@Name='W::Rows#q?x' and @Data:Index=3)"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_WildcardNamesWithoutDataRowIndex_AreSelectedByNamePattern()
        {
            // e.g. lightweight data rows (which have no data value 'Index') of a class template instantiated with a pointer
            string[] allNames = { "T<int *>::M#metadataSet0", "T<int *>::M#metadataSet1", "Other::X" };

            Generate(TestCases(new[] { "T<int *>::M#metadataSet1" }, allNames)).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Name("T<int ?>::M#metadataSet1")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DataRowIndexOfTestWithoutWildcardCharacters_IsNotUsed()
        {
            List<TestCase> testCases = TestCases(new[] { "A::Rows#1", "A::Rows#named" }, new[] { "A::Rows#1", "A::Rows#named", "A::Rows#2" });
            testCases[0].Properties.Add(new Model.TestCaseDataRowIndexProperty(1));
            testCases[1].Properties.Add(new Model.TestCaseDataRowIndexProperty(0));

            Generate(testCases).Single().CommandLine.Should().Be(BaseCommandLine + Select(Name("A::Rows#1"), Name("A::Rows#named")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_TemplateAndUnicodeClasses_AreSelectedByClass()
        {
            string[] classTests =
            {
                "TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate", "TaefSamples::TemplateTests<class std::array<int,3> >::CanDefeatMath",
                "TaefSamples::Nämespace::KlässWithSetüp::Täst", "TaefSamples::Nämespace::KlässWithSetüp::Träits"
            };

            Generate(TestCases(classTests, classTests.Concat(new[] { "Other::X" }))).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Class("TaefSamples::TemplateTests<class std::array<int,3> >"), Class("TaefSamples::Nämespace::KlässWithSetüp")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_TestsWithoutClass_AreSelectedByName()
        {
            Generate(TestCases(new[] { "GlobalTest1", "GlobalTest2" }, new[] { "GlobalTest1", "GlobalTest2", "A::X" })).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Name("GlobalTest1"), Name("GlobalTest2")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DataDrivenClasses_EachClassRowIsSelectedByClass()
        {
            string[] classTests =
            {
                "Ns::C#metadataSet0::M1", "Ns::C#metadataSet0::M2", "Ns::C#metadataSet1::M1", "Ns::C#metadataSet1::M2"
            };

            Generate(TestCases(classTests, classTests.Concat(new[] { "Other::X" }))).Single().CommandLine
                .Should().Be(BaseCommandLine + Select(Class("Ns::C#metadataSet0"), Class("Ns::C#metadataSet1")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_TestsWithoutMetaData_AreAlwaysSelectedByName()
        {
            var testCases = new List<TestCase>
            {
                new TestCase("A::X", TestDll, "A::X", "", 0),
                new TestCase("A::Y", TestDll, "A::Y", "", 0)
            };

            Generate(testCases).Single().CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X"), Name("A::Y")));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_DuplicateTestCases_AreSelectedOnce()
        {
            List<TestCase> testCases = TestCases(new[] { "A::X" }, new[] { "A::X", "A::Y" });
            testCases.Add(testCases[0]);

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X")));
            args.TestCases.Should().ContainSingle();
        }

        #endregion

        #region Ignored tests

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_AllTestsIncludingIgnoredOnes_NoSelectionAndIgnoredTestsAreNotExpected()
        {
            string[] allNames = { "A::X", "A::Y", "B::Z" };
            List<TestCase> testCases = TestCases(allNames, allNames, new[] { "A::Y" });

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine);
            args.TestCases.Select(tc => tc.FullyQualifiedName).Should().Equal("A::X", "B::Z");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_ClassWithIgnoredTest_ClassIsSelectedAndIgnoredTestIsNotExpected()
        {
            List<TestCase> testCases = TestCases(new[] { "A::X", "A::Y", "B::Z" }, new[] { "A::X", "A::Y", "B::Z", "B::W" }, new[] { "A::Y" });

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Class("A"), Name("B::Z")));
            args.CommandLine.Should().NotContain("/runIgnoredTests");
            args.TestCases.Select(tc => tc.FullyQualifiedName).Should().Equal("A::X", "B::Z");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_IgnoredTestsOfPartiallySelectedClass_AreNotSelected()
        {
            List<TestCase> testCases = TestCases(new[] { "A::Y", "B::Z" }, new[] { "A::X", "A::Y", "B::Z", "B::W" }, new[] { "A::Y" });

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + Select(Name("B::Z")));
            args.TestCases.Select(tc => tc.FullyQualifiedName).Should().Equal("B::Z");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_OnlyIgnoredTests_NoCommandLines()
        {
            List<TestCase> testCases = TestCases(new[] { "A::Y", "B::W" }, new[] { "A::X", "A::Y", "B::Z", "B::W" }, new[] { "A::Y", "B::W" });

            Generate(testCases).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_IgnoredTestsWithRunIgnoredTests_AreSelectedAndExpected()
        {
            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);
            List<TestCase> testCases = TestCases(new[] { "A::X", "A::Y", "B::Z" }, new[] { "A::X", "A::Y", "B::Z", "B::W" }, new[] { "A::Y" });

            CommandLineGenerator.Args args = Generate(testCases).Single();

            args.CommandLine.Should().Be(BaseCommandLine + " /runIgnoredTests" + Select(Class("A"), Name("B::Z")));
            args.TestCases.Select(tc => tc.FullyQualifiedName).Should().Equal("A::X", "A::Y", "B::Z");

            MockOptions.Setup(o => o.RunIgnoredTests).Returns(true);
            testCases = TestCases(new[] { "A::Y" }, new[] { "A::X", "A::Y", "B::Z", "B::W" }, new[] { "A::Y" });
            Generate(testCases).Single().CommandLine.Should().Be(BaseCommandLine + " /runIgnoredTests" + Select(Name("A::Y")));
        }

        #endregion

        #region Splitting of long command lines

        private static string LongName(int i) => $"SomeNamespace::SomeRatherLongTestClassName{i:D4}::SomeRatherLongTestMethodName";

        /// <returns>1000 tests of 1000 classes (each class has a second test which is not run)</returns>
        private static List<TestCase> ManyTestsOfManyClasses()
        {
            IEnumerable<int> indexes = Enumerable.Range(0, 1000);
            return TestCases(
                indexes.Select(LongName),
                indexes.SelectMany(i => new[] { LongName(i), LongName(i) + "2" }));
        }

        private static void AssertCommandLinesAreSplitCorrectly(IList<CommandLineGenerator.Args> commandLines, IList<TestCase> testCases, int lengthOfTeExecutable)
        {
            commandLines.SelectMany(a => a.TestCases).Should().Equal(testCases, "all tests are run exactly once, in the original order");
            for (int i = 0; i < commandLines.Count; i++)
            {
                string commandLine = commandLines[i].CommandLine;
                commandLine.Should().StartWith(BaseCommandLine + " /select:\"");
                commandLine.Should().EndWith("'\"");
                (lengthOfTeExecutable + 3 + commandLine.Length).Should().BeLessOrEqualTo(CommandLineGenerator.MaxCommandLength);
                string expectedQuery = string.Join(" or ", commandLines[i].TestCases.Select(tc => Name(tc.FullyQualifiedName)));
                commandLine.Should().Be(BaseCommandLine + " /select:\"" + expectedQuery + "\"");

                if (i < commandLines.Count - 1)
                {
                    // the first test of the next command line did not fit
                    int lengthWithNextTest = lengthOfTeExecutable + 3 + commandLine.Length + " or ".Length + Name(commandLines[i + 1].TestCases[0].FullyQualifiedName).Length;
                    lengthWithNextTest.Should().BeGreaterThan(CommandLineGenerator.MaxCommandLength);
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_ManyTests_AreSplitIntoSeveralCommandLines()
        {
            List<TestCase> testCases = ManyTestsOfManyClasses();

            List<CommandLineGenerator.Args> commandLines = Generate(testCases);

            commandLines.Should().HaveCount(3);
            AssertCommandLinesAreSplitCorrectly(commandLines, testCases, TeExecutable.Length);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_LongTeExecutablePath_IsTakenIntoAccount()
        {
            List<TestCase> testCases = ManyTestsOfManyClasses();
            const int lengthOfTeExecutable = 20000;

            List<CommandLineGenerator.Args> commandLines = Generate(testCases, lengthOfTeExecutable: lengthOfTeExecutable);

            commandLines.Should().HaveCount(9);
            AssertCommandLinesAreSplitCorrectly(commandLines, testCases, lengthOfTeExecutable);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_CommandLineOfExactlyMaxLength_IsNotSplit()
        {
            // total length: "<TE.exe>" <base> /select:"@Name='A::X' or @Name='B::<padding>'"
            int lengthOfFixedParts = TeExecutable.Length + 3 + (BaseCommandLine + " /select:\"").Length + "\"".Length;
            int lengthOfFirstTerm = Name("A::X").Length + " or ".Length;
            int lengthOfPadding = CommandLineGenerator.MaxCommandLength - lengthOfFixedParts - lengthOfFirstTerm - Name("B::").Length;
            string longName = "B::" + new string('x', lengthOfPadding);

            List<CommandLineGenerator.Args> commandLines = Generate(TestCases(new[] { "A::X", longName }, new[] { "A::X", "A::Y", longName, "B::Z" }));
            commandLines.Should().ContainSingle();
            (TeExecutable.Length + 3 + commandLines[0].CommandLine.Length).Should().Be(CommandLineGenerator.MaxCommandLength);

            longName += "x";
            commandLines = Generate(TestCases(new[] { "A::X", longName }, new[] { "A::X", "A::Y", longName, "B::Z" }));
            commandLines.Should().HaveCount(2);
            commandLines[0].CommandLine.Should().Be(BaseCommandLine + Select(Name("A::X")));
            commandLines[1].CommandLine.Should().Be(BaseCommandLine + Select(Name(longName)));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_TermLongerThanMaxLength_IsPassedInCommandLineOfItsOwn()
        {
            string hugeName = "B::" + new string('x', CommandLineGenerator.MaxCommandLength);
            List<TestCase> testCases = TestCases(new[] { "A::X", hugeName, "C::Y" }, new[] { "A::X", "A::Z", hugeName, "B::Z", "C::Y", "C::Z" });

            List<CommandLineGenerator.Args> commandLines = Generate(testCases);

            commandLines.Select(a => a.TestCases.Single().FullyQualifiedName).Should().Equal("A::X", hugeName, "C::Y");
            commandLines[1].CommandLine.Should().Be(BaseCommandLine + Select(Name(hugeName)));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_ManyTestsAndUserSelection_EachCommandLineContainsUserSelectionAndIsNotTooLong()
        {
            List<TestCase> testCases = ManyTestsOfManyClasses();
            string userQuery = "@Priority=1 and not @Name='" + new string('x', 2000) + "'";

            List<CommandLineGenerator.Args> commandLines = Generate(testCases, $"/select:\"{userQuery}\"");

            // 3 without the user's selection
            commandLines.Should().HaveCount(4);
            commandLines.SelectMany(a => a.TestCases).Should().Equal(testCases);
            for (int i = 0; i < commandLines.Count; i++)
            {
                string commandLine = commandLines[i].CommandLine;
                (TeExecutable.Length + 3 + commandLine.Length).Should().BeLessOrEqualTo(CommandLineGenerator.MaxCommandLength);
                string expectedQuery = string.Join(" or ", commandLines[i].TestCases.Select(tc => Name(tc.FullyQualifiedName)));
                commandLine.Should().Be(BaseCommandLine + $" /select:\"({userQuery}) and ({expectedQuery})\"");
                if (i < commandLines.Count - 1)
                {
                    // the first test of the next command line did not fit
                    int lengthWithNextTest = TeExecutable.Length + 3 + commandLine.Length + " or ".Length + Name(commandLines[i + 1].TestCases[0].FullyQualifiedName).Length;
                    lengthWithNextTest.Should().BeGreaterThan(CommandLineGenerator.MaxCommandLength);
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetCommandLines_ManyWholeClasses_ClassTermsAreSplitWithTheirTests()
        {
            // 400 classes with 3 tests each, all run except for the last test of the DLL: class terms
            List<string> allNames = Enumerable.Range(0, 400)
                .SelectMany(i => new[] { $"{LongName(i)}::A", $"{LongName(i)}::B", $"{LongName(i)}::C" })
                .ToList();
            List<TestCase> testCases = TestCases(allNames.Take(allNames.Count - 1), allNames);

            List<CommandLineGenerator.Args> commandLines = Generate(testCases);

            commandLines.Should().HaveCountGreaterThan(1);
            commandLines.SelectMany(a => a.TestCases).Should().Equal(testCases);
            foreach (CommandLineGenerator.Args args in commandLines)
            {
                (TeExecutable.Length + 3 + args.CommandLine.Length).Should().BeLessOrEqualTo(CommandLineGenerator.MaxCommandLength);
                foreach (IGrouping<string, TestCase> classTests in args.TestCases.GroupBy(tc => Helpers.TaefNames.GetClassName(tc.FullyQualifiedName)))
                {
                    args.CommandLine.Should().Contain(classTests.Count() == 3
                        ? Class(classTests.Key)
                        : string.Join(" or ", classTests.Select(tc => Name(tc.FullyQualifiedName))));
                }
            }
            commandLines.Last().CommandLine.Should().EndWith($" or {Name(allNames[allNames.Count - 3])} or {Name(allNames[allNames.Count - 2])}\"");
        }

        #endregion

    }

}
