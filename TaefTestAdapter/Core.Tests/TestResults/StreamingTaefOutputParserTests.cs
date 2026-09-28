// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using ILogger = TaefTestAdapter.Common.ILogger;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Tests of <see cref="StreamingTaefOutputParser"/> (and of the non-streaming <see cref="TaefOutputParser"/>, which
    /// must produce the same results) with verbatim TE.exe outputs: inline excerpts of the outputs of TaefLab.dll (a TAEF
    /// test DLL written for these tests; source and DLL paths replaced by neutral paths) and of the sample tests, and the
    /// complete outputs captured from the sample test DLLs (Tests.Common\Resources\TestData\TaefOutput).
    /// </summary>
    [TestClass]
    public class StreamingTaefOutputParserTests : TestsBase
    {
        private const string LabDll = @"C:\src\TaefLab\bin\x64\TaefLab.dll";
        private const string LabCpp = @"C:\src\TaefLab\TaefLab.cpp";
        private const string SamplesDir = @"C:\src\TAEF-Test-Adapter\SampleTests\";
        private const string DllTestsDll = @"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\DllTests_taef.dll";
        private const string CrashingTestsDll = @"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\CrashingTests_taef.dll";
        private const string LeakCheckTestsDll = @"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\LeakCheckTests_taef.dll";

        private StreamingTaefOutputParser _parser;

        #region Verbatim outputs

        // DllTests_taef.dll (Debug x64), complete output
        private static readonly string[] DllTestsRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "StartGroup: TaefSamples::Passing::InvokeFunction",
            "Verify: AreEqual(0, ReturnZero())",
            "EndGroup: TaefSamples::Passing::InvokeFunction [Passed]",
            "",
            "StartGroup: TaefSamples::Failing::InvokeFunction",
            @"Error: Verify: AreEqual(1, ReturnZero()) - Values (1, 0) [File: C:\src\TAEF-Test-Adapter\SampleTests\DllDependentProject\DllTests.cpp, Function: TaefSamples::Failing::InvokeFunction, Line: 24]",
            "EndGroup: TaefSamples::Failing::InvokeFunction [Failed]",
            "",
            "Summary of Non-passing Tests:",
            "    TaefSamples::Failing::InvokeFunction [Failed]",
            "",
            "Summary: Total=2, Passed=1, Failed=1, Blocked=0, Not Run=0, Skipped=0"
        };

        // Tests_taef.dll: tests setting their results explicitly
        private static readonly string[] ExplicitResultsRun =
        {
            "",
            "StartGroup: TaefSamples::ExplicitResults::SkippedByTest",
            "This test decides at runtime that it cannot run on this machine",
            "TestSkipped: Skipped by the test",
            "EndGroup: TaefSamples::ExplicitResults::SkippedByTest [Skipped]",
            "",
            "StartGroup: TaefSamples::ExplicitResults::BlockedByTest",
            "TestBlocked: Blocked by the test: a prerequisite is missing",
            "EndGroup: TaefSamples::ExplicitResults::BlockedByTest [Blocked]",
            "",
            "StartGroup: TaefSamples::ExplicitResults::NotRunByTest",
            "TestNotRun: NotRun set by the test",
            "EndGroup: TaefSamples::ExplicitResults::NotRunByTest [NotRun]",
            "",
            "StartGroup: TaefSamples::ExplicitResults::FailedByLogResult",
            "TestFailed: Failed set by the test",
            "EndGroup: TaefSamples::ExplicitResults::FailedByLogResult [Failed]",
            "",
            "StartGroup: TaefSamples::ExplicitResults::FailedByLogError",
            "Error: Log::Error marks the test as failed",
            "EndGroup: TaefSamples::ExplicitResults::FailedByLogError [Failed]",
            "",
            "StartGroup: TaefSamples::ExplicitResults::PassedWithWarning",
            "Warning: Only a warning - the test passes",
            "EndGroup: TaefSamples::ExplicitResults::PassedWithWarning [Passed]"
        };

        // Tests_taef.dll: failing fixtures (setup fixtures print their failures before the affected tests)
        private static readonly string[] FixtureTestsRun =
        {
            "[ClassWithFixtures] method cleanup",
            "[ClassWithFixtures] class cleanup",
            "[FailingClassSetup] class setup returns false",
            "TestBlocked: TAEF: Setup fixture 'TaefSamples::FailingClassSetup::ClassSetup' for the scope 'TaefSamples::FailingClassSetup' returned 'false'.",
            "",
            "StartGroup: TaefSamples::FailingClassSetup::FirstTest",
            "EndGroup: TaefSamples::FailingClassSetup::FirstTest [Blocked]",
            "",
            "StartGroup: TaefSamples::FailingClassSetup::SecondTest",
            "EndGroup: TaefSamples::FailingClassSetup::SecondTest [Blocked]",
            "[FailingMethodSetup] method setup returns false",
            "TestBlocked: TAEF: Setup fixture 'TaefSamples::FailingMethodSetup::MethodSetup' for the scope 'TaefSamples::FailingMethodSetup::Test' returned 'false'.",
            "",
            "StartGroup: TaefSamples::FailingMethodSetup::Test",
            "EndGroup: TaefSamples::FailingMethodSetup::Test [Blocked]",
            @"Error: Verify: AreEqual(5, 6): VERIFY failing in TEST_CLASS_SETUP - Values (5, 6) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\FixtureTests.cpp, Function: TaefSamples::VerifyInClassSetup::ClassSetup, Line: 143]",
            "Error: TAEF: Setup fixture 'TaefSamples::VerifyInClassSetup::ClassSetup' for the scope 'TaefSamples::VerifyInClassSetup' failed.",
            "",
            "StartGroup: TaefSamples::VerifyInClassSetup::Test",
            "EndGroup: TaefSamples::VerifyInClassSetup::Test [Failed]",
            @"Error: Verify: AreEqual(7, 8): VERIFY failing in TEST_METHOD_SETUP - Values (7, 8) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\FixtureTests.cpp, Function: TaefSamples::VerifyInMethodSetup::MethodSetup, Line: 160]",
            "Error: TAEF: Setup fixture 'TaefSamples::VerifyInMethodSetup::MethodSetup' for the scope 'TaefSamples::VerifyInMethodSetup::Test' failed.",
            "",
            "StartGroup: TaefSamples::VerifyInMethodSetup::Test",
            "EndGroup: TaefSamples::VerifyInMethodSetup::Test [Failed]",
            "",
            "StartGroup: TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails",
            "Verify: IsTrue(true): TestPassesAlthoughCleanupFails",
            "EndGroup: TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails [Passed]",
            "[FailingMethodCleanup] method cleanup returns false",
            "TestBlocked: TAEF: Cleanup fixture 'TaefSamples::FailingMethodCleanup::MethodCleanup' for the scope 'TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails' returned 'false'.",
            "",
            "StartGroup: TaefSamples::TableDataTests::Simple#0",
            "TAEF: Data[i]: 1",
            "TAEF: Data[Index]: 0",
            "TAEF: Data[s]: ",
            "Verify: SUCCEEDED(TestData::TryGetValue(L\"i\", i))",
            "Verify: SUCCEEDED(TestData::TryGetValue(L\"s\", s))",
            "Simple: TestData = (1,)",
            "Verify: AreEqual(1, i)",
            "Verify: AreEqual(String(L\"\"), s)",
            "EndGroup: TaefSamples::TableDataTests::Simple#0 [Passed]"
        };

        // TaefLab.dll: class and method fixtures (blocked and failed), a failing cleanup, and a Unicode test
        private static readonly string[] LabFixtureRun =
        {
            "EndGroup: GlobalTests::GlobalFailing [Failed]",
            "[BlockedClassTests::FailingClassSetup] returning false",
            "TestBlocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup' for the scope 'BlockedClassTests' returned 'false'.",
            "",
            "StartGroup: BlockedClassTests::WillBeBlocked1",
            "EndGroup: BlockedClassTests::WillBeBlocked1 [Blocked]",
            "",
            "StartGroup: BlockedClassTests::WillBeBlocked2",
            "EndGroup: BlockedClassTests::WillBeBlocked2 [Blocked]",
            "[MethodSetupFailTests::FailingMethodSetup] returning false",
            "TestBlocked: TAEF: Setup fixture 'MethodSetupFailTests::FailingMethodSetup' for the scope 'MethodSetupFailTests::MethodSetupBlocked' returned 'false'.",
            "",
            "StartGroup: MethodSetupFailTests::MethodSetupBlocked",
            "EndGroup: MethodSetupFailTests::MethodSetupBlocked [Blocked]",
            "",
            "StartGroup: MethodCleanupFailTests::PassButCleanupFails",
            "test body passes",
            "EndGroup: MethodCleanupFailTests::PassButCleanupFails [Passed]",
            "[MethodCleanupFailTests::FailingMethodCleanup] returning false",
            "TestBlocked: TAEF: Cleanup fixture 'MethodCleanupFailTests::FailingMethodCleanup' for the scope 'MethodCleanupFailTests::PassButCleanupFails' returned 'false'.",
            "Error: Verify: AreEqual(5, 6): VERIFY failing inside TEST_CLASS_SETUP - Values (5, 6) [File: " + LabCpp + ", Function: ClassSetupVerifyFailTests::VerifyFailingClassSetup, Line: 436]",
            "Error: TAEF: Setup fixture 'ClassSetupVerifyFailTests::VerifyFailingClassSetup' for the scope 'ClassSetupVerifyFailTests' failed.",
            "",
            "StartGroup: ClassSetupVerifyFailTests::BlockedByVerifyInSetup",
            "EndGroup: ClassSetupVerifyFailTests::BlockedByVerifyInSetup [Failed]",
            "",
            "StartGroup: UnicodeTests::Tëst_Ünïcødé_名前",
            "unicode comment: éè 名前 ✓",
            "EndGroup: UnicodeTests::Tëst_Ünïcødé_名前 [Passed]"
        };

        // TaefLab.dll, /inproc: unflushed printf() output is glued to the EndGroup line
        private static readonly string[] LabInProcessOutputRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "[LabModuleSetup] Log::Comment from MODULE_SETUP",
            "[LabModuleSetup] printf from MODULE_SETUP",
            "[BasicTests::ClassSetup] Log::Comment",
            "[BasicTests::MethodSetup] Log::Comment",
            "",
            "StartGroup: TaefLab::Basic::BasicTests::Output",
            "printf line 1",
            "std::cout line 2",
            "wprintf line 5",
            "Log::Comment line 7",
            "Warning: Log::Warning line 8",
            "multi-line comment line A",
            "multi-line comment line B",
            "printf without newline at endEndGroup: TaefLab::Basic::BasicTests::Output [Passed]",
            "[BasicTests::MethodCleanup] Log::Comment",
            "[BasicTests::MethodSetup] Log::Comment",
            "",
            "StartGroup: TaefLab::Basic::BasicTests::OutputThenFail",
            "printf before failure",
            "comment before failure",
            "Error: Verify: AreEqual(10, 20) - Values (10, 20) [File: " + LabCpp + ", Function: TaefLab::Basic::BasicTests::OutputThenFail, Line: 169]",
            "EndGroup: TaefLab::Basic::BasicTests::OutputThenFail [Failed]",
            "[BasicTests::MethodCleanup] Log::Comment",
            "[BasicTests::ClassCleanup] Log::Comment",
            "[LabModuleCleanup] Log::Comment from MODULE_CLEANUP",
            "",
            "Summary of Non-passing Tests:",
            "    TaefLab::Basic::BasicTests::OutputThenFail [Failed]",
            "",
            "Summary: Total=2, Passed=1, Failed=1, Blocked=0, Not Run=0, Skipped=0"
        };

        // Tests_taef.dll, /inproc: unflushed printf() output is glued to an Error line
        private static readonly string[] InProcessGluedErrorRun =
        {
            "",
            "StartGroup: TaefSamples::OutputHandling::Output_OneLine",
            "before test",
            @"printf before test without newline (visible with /inproc only)Error: Verify: AreEqual(1, 2): test output - Values (1, 2) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\BasicTests.cpp, Function: TaefSamples::OutputHandling::Output_OneLine, Line: 73]",
            "after test",
            "EndGroup: TaefSamples::OutputHandling::Output_OneLine [Failed]",
            "",
            "StartGroup: TaefSamples::OutputHandling::ManyLinesWithNewlines",
            "before test 1",
            "before test 2",
            "",
            @"Error: Verify: AreEqual(1, 2) - Values (1, 2) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\BasicTests.cpp, Function: TaefSamples::OutputHandling::ManyLinesWithNewlines, Line: 81]",
            "after test 1",
            "after test 2",
            "",
            "EndGroup: TaefSamples::OutputHandling::ManyLinesWithNewlines [Failed]"
        };

        // TaefLab.dll: a test logging framing lines (of itself and of another test) with Log::Comment
        private static readonly string[] LabSpoofedFramingRun =
        {
            "",
            "StartGroup: EdgeCaseTests::SpoofFramingLines",
            "EndGroup: EdgeCaseTests::SpoofFramingLines [Passed]",
            "StartGroup: Fake::Test",
            "Summary: Total=99, Passed=99, Failed=0, Blocked=0, Not Run=0, Skipped=0",
            "Error: Verify: real failure after spoofed lines [File: " + LabCpp + ", Function: EdgeCaseTests::SpoofFramingLines, Line: 499]",
            "EndGroup: EdgeCaseTests::SpoofFramingLines [Failed]",
            "",
            "StartGroup: UnflushedOutputTests::A_UnflushedPrintf",
            "EndGroup: UnflushedOutputTests::A_UnflushedPrintf [Passed]",
            "",
            "StartGroup: UnflushedOutputTests::B_AfterUnflushed",
            "B_AfterUnflushed ran",
            "EndGroup: UnflushedOutputTests::B_AfterUnflushed [Passed]",
            "[LabModuleCleanup] Log::Comment from MODULE_CLEANUP"
        };

        // Tests_taef.dll: data rows with special names (the row names are part of the test names)
        private static readonly string[] SpecialRowNamesRun =
        {
            "",
            "StartGroup: TaefSamples::NamedRows::SpecialCharacters#with space",
            "EndGroup: TaefSamples::NamedRows::SpecialCharacters#with space [Passed]",
            "",
            "StartGroup: TaefSamples::NamedRows::SpecialCharacters#with'quote",
            "TAEF: Data[Index]: 1",
            "TAEF: Data[Number]: 2",
            "Verify: SUCCEEDED(TestData::TryGetValue(L\"Number\", number))",
            @"Error: Verify: AreEqual(1, number % 2): rows with an even Number are designed to fail - Values (1, 0) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\DataDrivenTests.cpp, Function: TaefSamples::NamedRows::SpecialCharacters, Line: 155]",
            "EndGroup: TaefSamples::NamedRows::SpecialCharacters#with'quote [Failed]",
            "",
            "StartGroup: TaefSamples::NamedRows::SpecialCharacters#with#hash",
            "EndGroup: TaefSamples::NamedRows::SpecialCharacters#with#hash [Passed]",
            "",
            "StartGroup: TaefSamples::NamedRows::SpecialCharacters#with::colons [x]",
            @"Error: Verify: AreEqual(1, number % 2): rows with an even Number are designed to fail - Values (1, 0) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\DataDrivenTests.cpp, Function: TaefSamples::NamedRows::SpecialCharacters, Line: 155]",
            "EndGroup: TaefSamples::NamedRows::SpecialCharacters#with::colons [x] [Failed]",
            "",
            @"StartGroup: TaefSamples::NamedRows::SpecialCharacters#back\slash",
            @"EndGroup: TaefSamples::NamedRows::SpecialCharacters#back\slash [Passed]",
            "",
            "StartGroup: TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate",
            "EndGroup: TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate [Passed]",
            "",
            "StartGroup: TaefSamples::`anonymous-namespace'::Namespace_Anon::Test",
            "EndGroup: TaefSamples::`anonymous-namespace'::Namespace_Anon::Test [Passed]",
            "",
            "StartGroup: TaefSamples::Ümlautß::Täst",
            "Error: Verify: AreEqual(1, 2): Ümlautß::Täst - Values (1, 2) [File: C:\\src\\TAEF-Test-Adapter\\SampleTests\\Tests\\UmlautTests.cpp, Function: TaefSamples::Ümlautß::Täst, Line: 16]",
            "EndGroup: TaefSamples::Ümlautß::Täst [Failed]"
        };

        #endregion

        #region Helpers

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
        }

        /// <summary>
        /// Parses <paramref name="lines"/> with a <see cref="StreamingTaefOutputParser"/> (available as <see cref="_parser"/>
        /// afterwards) and checks that the non-streaming <see cref="TaefOutputParser"/> produces the same results.
        /// </summary>
        private IList<TestResult> Parse(IEnumerable<TestCase> testCases, IEnumerable<string> lines, int? teExitCode = null)
        {
            List<TestCase> testCasesList = testCases.ToList();
            List<string> linesList = lines.ToList();

            _parser = new StreamingTaefOutputParser(testCasesList, MockLogger.Object, MockFrameworkReporter.Object);
            foreach (string line in linesList)
            {
                _parser.ReportLine(line);
            }
            _parser.Flush(teExitCode);

            var nonStreamingParser = new TaefOutputParser(testCasesList, linesList, new Mock<ILogger>().Object);
            IList<TestResult> nonStreamingResults = nonStreamingParser.GetTestResults();
            nonStreamingResults.Select(tr => tr.TestCase).Should().Equal(_parser.TestResults.Select(tr => tr.TestCase));
            nonStreamingResults.Select(tr => tr.Outcome).Should().Equal(_parser.TestResults.Select(tr => tr.Outcome));
            nonStreamingResults.Select(tr => tr.Output).Should().Equal(_parser.TestResults.Select(tr => tr.Output));
            nonStreamingResults.Select(tr => tr.ErrorStackTrace).Should().Equal(_parser.TestResults.Select(tr => tr.ErrorStackTrace));
            if (!teExitCode.HasValue)
                nonStreamingResults.Select(tr => tr.ErrorMessage).Should().Equal(_parser.TestResults.Select(tr => tr.ErrorMessage));
            nonStreamingParser.SummaryFound.Should().Be(_parser.SummaryFound);
            nonStreamingParser.CrashedTestCase.Should().Be(_parser.CrashedTestCase);

            return _parser.TestResults;
        }

        private IList<TestResult> Parse(string testDll, IEnumerable<string> lines, params string[] testNames)
            => Parse(CapturedTaefOutputs.ToTestCases(testDll, testNames), lines);

        private static TestResult ResultOf(IEnumerable<TestResult> results, string testName)
            => results.Single(tr => tr.TestCase.FullyQualifiedName == testName);

        private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

        private IList<TestCase> GetCapturedTestCasesOfTestsDll()
        {
            IList<TestCase> testCases = CapturedTaefOutputs.GetTestCasesOfTestsDll(MockOptions.Object, MockLogger.Object);
            testCases.Should().HaveCount(TestResources.NrOfTests);
            return testCases;
        }

        private void VerifyNoWarningsOrErrorsLogged()
        {
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Basics

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CompleteOutput_ResultsAreCreatedCorrectly()
        {
            IList<TestResult> results = Parse(DllTestsDll, DllTestsRun, "TaefSamples::Passing::InvokeFunction", "TaefSamples::Failing::InvokeFunction");

            results.Should().HaveCount(2);
            results[0].TestCase.FullyQualifiedName.Should().Be("TaefSamples::Passing::InvokeFunction");
            TestResultAssertions.AssertTestResultIsPassed(results[0]);
            results[0].ErrorStackTrace.Should().BeNull();
            results[0].Output.Should().Be("Verify: AreEqual(0, ReturnZero())");
            results[0].DisplayName.Should().Be("TaefSamples::Passing::InvokeFunction");
            results[0].ComputerName.Should().Be(Environment.MachineName);

            results[1].TestCase.FullyQualifiedName.Should().Be("TaefSamples::Failing::InvokeFunction");
            TestResultAssertions.AssertTestResultIsFailure(results[1], "Verify: AreEqual(1, ReturnZero()) - Values (1, 0)");
            results[1].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry(
                "TaefSamples::Failing::InvokeFunction", @"C:\src\TAEF-Test-Adapter\SampleTests\DllDependentProject\DllTests.cpp", "24"));

            _parser.SummaryFound.Should().BeTrue();
            _parser.CrashedTestCase.Should().BeNull();
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_StartAndEndOfGroup_TestIsReportedAsStartedFirstAndResultWhenGroupEnds()
        {
            var events = new List<string>();
            MockFrameworkReporter.Setup(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()))
                .Callback<IEnumerable<TestCase>>(tcs => events.AddRange(tcs.Select(tc => "started " + tc.FullyQualifiedName)));
            MockFrameworkReporter.Setup(r => r.ReportTestResults(It.IsAny<IEnumerable<TestResult>>()))
                .Callback<IEnumerable<TestResult>>(trs => events.AddRange(trs.Select(tr => $"result {tr.TestCase.FullyQualifiedName} {tr.Outcome}")));

            var parser = new StreamingTaefOutputParser(
                CapturedTaefOutputs.ToTestCases(DllTestsDll, "TaefSamples::Passing::InvokeFunction", "TaefSamples::Failing::InvokeFunction"),
                MockLogger.Object, MockFrameworkReporter.Object);

            parser.ReportLine("Test Authoring and Execution Framework v10.104k for x64");
            parser.ReportLine("");
            events.Should().BeEmpty();

            parser.ReportLine("StartGroup: TaefSamples::Passing::InvokeFunction");
            events.Should().Equal("started TaefSamples::Passing::InvokeFunction");

            parser.ReportLine("Verify: AreEqual(0, ReturnZero())");
            events.Should().HaveCount(1);
            parser.TestResults.Should().BeEmpty();

            parser.ReportLine("EndGroup: TaefSamples::Passing::InvokeFunction [Passed]");
            events.Should().Equal("started TaefSamples::Passing::InvokeFunction", "result TaefSamples::Passing::InvokeFunction Passed");
            parser.TestResults.Should().ContainSingle();

            foreach (string line in DllTestsRun.Skip(5))
                parser.ReportLine(line);
            parser.Flush();

            events.Should().Equal(
                "started TaefSamples::Passing::InvokeFunction", "result TaefSamples::Passing::InvokeFunction Passed",
                "started TaefSamples::Failing::InvokeFunction", "result TaefSamples::Failing::InvokeFunction Failed");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_OutcomesSetByTests_AreMappedToVsOutcomes()
        {
            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, ExplicitResultsRun,
                "TaefSamples::ExplicitResults::SkippedByTest", "TaefSamples::ExplicitResults::BlockedByTest", "TaefSamples::ExplicitResults::NotRunByTest",
                "TaefSamples::ExplicitResults::FailedByLogResult", "TaefSamples::ExplicitResults::FailedByLogError", "TaefSamples::ExplicitResults::PassedWithWarning");

            results.Should().HaveCount(6);

            TestResult skipped = ResultOf(results, "TaefSamples::ExplicitResults::SkippedByTest");
            TestResultAssertions.AssertTestResultIsSkipped(skipped);
            skipped.ErrorMessage.Should().Be("Skipped by the test");
            skipped.Output.Should().Be(Lines("This test decides at runtime that it cannot run on this machine", "TestSkipped: Skipped by the test"));

            TestResult blocked = ResultOf(results, "TaefSamples::ExplicitResults::BlockedByTest");
            TestResultAssertions.AssertTestResultIsFailure(blocked, StreamingTaefOutputParser.BlockedPrefix + "Blocked by the test: a prerequisite is missing");

            TestResult notRun = ResultOf(results, "TaefSamples::ExplicitResults::NotRunByTest");
            notRun.Outcome.Should().Be(TestOutcome.None);
            notRun.ErrorMessage.Should().Be("NotRun set by the test");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "TaefSamples::ExplicitResults::FailedByLogResult"), "Failed set by the test");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "TaefSamples::ExplicitResults::FailedByLogError"), "Log::Error marks the test as failed");

            TestResult passedWithWarning = ResultOf(results, "TaefSamples::ExplicitResults::PassedWithWarning");
            TestResultAssertions.AssertTestResultIsPassed(passedWithWarning);
            passedWithWarning.Output.Should().Be("Warning: Only a warning - the test passes");

            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ResultsWithoutMessages_DefaultMessageForBlockedOnly()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: TaefLab::Basic::BasicTests::SkipSelfNoComment",
                    "EndGroup: TaefLab::Basic::BasicTests::SkipSelfNoComment [Skipped]",
                    "",
                    "StartGroup: TaefLab::Basic::BasicTests::BlockedWithoutComment",
                    "EndGroup: TaefLab::Basic::BasicTests::BlockedWithoutComment [Blocked]",
                    "",
                    "StartGroup: TaefLab::Basic::BasicTests::NotRunWithoutComment",
                    "EndGroup: TaefLab::Basic::BasicTests::NotRunWithoutComment [NotRun]"
                },
                "TaefLab::Basic::BasicTests::SkipSelfNoComment", "TaefLab::Basic::BasicTests::BlockedWithoutComment",
                "TaefLab::Basic::BasicTests::NotRunWithoutComment");

            results[0].Outcome.Should().Be(TestOutcome.Skipped);
            results[0].ErrorMessage.Should().BeNull();
            results[0].Output.Should().BeNull();

            TestResultAssertions.AssertTestResultIsFailure(results[1], "Blocked: no reason has been reported by TE.exe");

            results[2].Outcome.Should().Be(TestOutcome.None);
            results[2].ErrorMessage.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_OutputOfTest_ContainsAllLinesOfGroupExceptFraming()
        {
            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, InProcessGluedErrorRun,
                "TaefSamples::OutputHandling::Output_OneLine", "TaefSamples::OutputHandling::ManyLinesWithNewlines");

            // empty lines within a group are output of the test
            ResultOf(results, "TaefSamples::OutputHandling::ManyLinesWithNewlines").Output.Should().Be(Lines(
                "before test 1",
                "before test 2",
                "",
                @"Error: Verify: AreEqual(1, 2) - Values (1, 2) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\BasicTests.cpp, Function: TaefSamples::OutputHandling::ManyLinesWithNewlines, Line: 81]",
                "after test 1",
                "after test 2",
                ""));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TestsWithSpecialNames_AreParsedCorrectly()
        {
            string[] names =
            {
                "TaefSamples::NamedRows::SpecialCharacters#with space", "TaefSamples::NamedRows::SpecialCharacters#with'quote",
                "TaefSamples::NamedRows::SpecialCharacters#with#hash", "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]",
                @"TaefSamples::NamedRows::SpecialCharacters#back\slash", "TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate",
                "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test", "TaefSamples::Ümlautß::Täst"
            };

            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, SpecialRowNamesRun, names);

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(names);
            results.Select(tr => tr.Outcome).Should().Equal(
                TestOutcome.Passed, TestOutcome.Failed, TestOutcome.Passed, TestOutcome.Failed,
                TestOutcome.Passed, TestOutcome.Passed, TestOutcome.Passed, TestOutcome.Failed);
            ResultOf(results, "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]").ErrorMessage
                .Should().Be("Verify: AreEqual(1, number % 2): rows with an even Number are designed to fail - Values (1, 0)");
            ResultOf(results, "TaefSamples::Ümlautß::Täst").ErrorMessage.Should().Be("Verify: AreEqual(1, 2): Ümlautß::Täst - Values (1, 2)");
            ResultOf(results, "TaefSamples::NamedRows::SpecialCharacters#with'quote").Output.Should().StartWith(Lines("TAEF: Data[Index]: 1", "TAEF: Data[Number]: 2"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TestNamesWhichArePrefixesOfEachOther_AreDistinguished()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: Test::AB",
                    "Some output",
                    "EndGroup: Test::AB [Passed]",
                    "",
                    "StartGroup: Test::A",
                    "EndGroup: Test::AB [Failed]",
                    "EndGroup: Test::A [Failed]",
                    "",
                    "StartGroup: Test::B",
                    "EndGroup: Test::B#0 [Failed]",
                    "EndGroup: My::Test::B [Failed]",
                    "EndGroup: Test::B [Passed]"
                },
                "Test::A", "Test::AB", "Test::B");

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}")
                .Should().Equal("Test::AB Passed", "Test::A Failed", "Test::B Passed");
            ResultOf(results, "Test::A").Output.Should().Be("EndGroup: Test::AB [Failed]");
            ResultOf(results, "Test::B").Output.Should().Be(Lines("EndGroup: Test::B#0 [Failed]", "EndGroup: My::Test::B [Failed]"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_Durations_AreMeasuredBetweenStartAndEndOfGroup()
        {
            var parser = new StreamingTaefOutputParser(CapturedTaefOutputs.ToTestCases(LabDll, "A::Slow"), MockLogger.Object, MockFrameworkReporter.Object);

            parser.ReportLine("");
            parser.ReportLine("StartGroup: A::Slow");
            Thread.Sleep(200);
            parser.ReportLine("EndGroup: A::Slow [Passed]");
            parser.Flush();

            parser.TestResults.Single().Duration.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(200) - TestMetadata.Tolerance);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void NormalizeDuration_ShortDurations_AreReplacedByShortTestDuration()
        {
            StreamingTaefOutputParser.ShortTestDuration.Should().Be(TimeSpan.FromTicks(2500));
            StreamingTaefOutputParser.NormalizeDuration(TimeSpan.Zero).Should().Be(StreamingTaefOutputParser.ShortTestDuration);
            StreamingTaefOutputParser.NormalizeDuration(TimeSpan.FromTicks(9999)).Should().Be(StreamingTaefOutputParser.ShortTestDuration);
            StreamingTaefOutputParser.NormalizeDuration(TimeSpan.FromMilliseconds(1)).Should().Be(TimeSpan.FromMilliseconds(1));
            StreamingTaefOutputParser.NormalizeDuration(TimeSpan.FromSeconds(2)).Should().Be(TimeSpan.FromSeconds(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestResult_Factories_ProduceResultsWithoutEmptyMessages()
        {
            TestCase testCase = CapturedTaefOutputs.ToTestCases(LabDll, "A::B").Single();

            TestResult passed = StreamingTaefOutputParser.CreatePassedTestResult(testCase, TimeSpan.FromMilliseconds(3));
            passed.Outcome.Should().Be(TestOutcome.Passed);
            passed.Duration.Should().Be(TimeSpan.FromMilliseconds(3));
            passed.DisplayName.Should().Be("A::B");
            passed.ComputerName.Should().Be(Environment.MachineName);
            passed.ErrorMessage.Should().BeNull();

            TestResult skipped = StreamingTaefOutputParser.CreateSkippedTestResult(testCase, TimeSpan.Zero, "", "");
            skipped.Outcome.Should().Be(TestOutcome.Skipped);
            skipped.ErrorMessage.Should().BeNull();
            skipped.ErrorStackTrace.Should().BeNull();

            TestResult failed = StreamingTaefOutputParser.CreateFailedTestResult(testCase, TimeSpan.Zero, "message", "stack");
            failed.Outcome.Should().Be(TestOutcome.Failed);
            failed.ErrorMessage.Should().Be("message");
            failed.ErrorStackTrace.Should().Be("stack");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor_NullArguments_Throw()
        {
            var testCases = new List<TestCase>();

            // ReSharper disable ObjectCreationAsStatement
            new Action(() => new StreamingTaefOutputParser(null, MockLogger.Object, MockFrameworkReporter.Object)).Should().Throw<ArgumentNullException>();
            new Action(() => new StreamingTaefOutputParser(testCases, null, MockFrameworkReporter.Object)).Should().Throw<ArgumentNullException>();
            new Action(() => new StreamingTaefOutputParser(testCases, MockLogger.Object, null)).Should().Throw<ArgumentNullException>();
            // ReSharper restore ObjectCreationAsStatement
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_NullAndDuplicateTestCases_AreHandled()
        {
            List<TestCase> testCases = CapturedTaefOutputs.ToTestCases(DllTestsDll, "TaefSamples::Passing::InvokeFunction", "TaefSamples::Passing::InvokeFunction").ToList();
            var parser = new StreamingTaefOutputParser(testCases, MockLogger.Object, MockFrameworkReporter.Object);

            parser.ReportLine(null);
            foreach (string line in DllTestsRun)
                parser.ReportLine(line);
            parser.Flush();

            parser.TestResults.Should().ContainSingle().Which.Outcome.Should().Be(TestOutcome.Passed);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ReporterCancelsTestRun_ExceptionIsPropagated()
        {
            MockFrameworkReporter.Setup(r => r.ReportTestResults(It.IsAny<IEnumerable<TestResult>>()))
                .Throws(new TestRunCanceledException("canceled", new Exception("inner")));
            var parser = new StreamingTaefOutputParser(CapturedTaefOutputs.ToTestCases(DllTestsDll, "TaefSamples::Passing::InvokeFunction"), MockLogger.Object, MockFrameworkReporter.Object);

            parser.ReportLine("StartGroup: TaefSamples::Passing::InvokeFunction");
            new Action(() => parser.ReportLine("EndGroup: TaefSamples::Passing::InvokeFunction [Passed]")).Should().Throw<TestRunCanceledException>();
        }

        #endregion

        #region Tests not part of the run, spoofed framing lines

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TestNotPartOfRun_IsIgnored()
        {
            // TE.exe always runs data source error pseudo tests (TaefSamples::MissingDataSource::Test#error), even if they are not selected
            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.TestsRunIgnored),
                TestResources.TestNames.IgnoredTest, TestResources.TestNames.IgnoredPassing, TestResources.TestNames.IgnoredFailing);

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(
                TestResources.TestNames.IgnoredTest, TestResources.TestNames.IgnoredPassing, TestResources.TestNames.IgnoredFailing);
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(TestResources.NrOfPassingIgnoredTests);
            results.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(TestResources.NrOfFailingIgnoredTests);

            MockFrameworkReporter.Verify(r => r.ReportTestsStarted(It.Is<IEnumerable<TestCase>>(tcs =>
                tcs.Any(tc => tc.FullyQualifiedName == TestResources.TestNames.MissingDataSource))), Times.Never);
            MockFrameworkReporter.Verify(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()), Times.Exactly(3));
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains($"ignoring test '{TestResources.TestNames.MissingDataSource}'"))), Times.Once);
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_OnlyTestsNotPartOfRun_NoResults()
        {
            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.TestsRunNoMatch),
                "DoesNotExist::Test");

            results.Should().BeEmpty();
            _parser.SummaryFound.Should().BeTrue();
            _parser.CrashedTestCase.Should().BeNull();
            MockFrameworkReporter.Verify(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FramingLinesOfOtherTestsWithinGroup_AreOutputOfTest()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: EdgeCaseTests::LogsFramingOfOtherTests",
                    "StartGroup: Fake::Test",
                    "EndGroup: Fake::Test [Failed]",
                    "StartGroup: EdgeCaseTests::Other",
                    "EndGroup: EdgeCaseTests::Other [Passed]",
                    "Summary: Total=99, Passed=99, Failed=0, Blocked=0, Not Run=0, Skipped=0",
                    "Error: Verify: real failure after spoofed lines [File: " + LabCpp + ", Function: EdgeCaseTests::LogsFramingOfOtherTests, Line: 499]",
                    "EndGroup: EdgeCaseTests::LogsFramingOfOtherTests [Failed]",
                    "",
                    "StartGroup: EdgeCaseTests::Other",
                    "EndGroup: EdgeCaseTests::Other [Failed]"
                },
                "EdgeCaseTests::LogsFramingOfOtherTests", "EdgeCaseTests::Other");

            results.Should().HaveCount(2);
            TestResult first = ResultOf(results, "EdgeCaseTests::LogsFramingOfOtherTests");
            TestResultAssertions.AssertTestResultIsFailure(first, "Verify: real failure after spoofed lines");
            first.Output.Should().StartWith(Lines("StartGroup: Fake::Test", "EndGroup: Fake::Test [Failed]", "StartGroup: EdgeCaseTests::Other"));
            ResultOf(results, "EdgeCaseTests::Other").Outcome.Should().Be(TestOutcome.Failed);

            _parser.SummaryFound.Should().BeFalse("a Summary: line within a group is output of the test");
            MockFrameworkReporter.Verify(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()), Times.Exactly(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SpoofedEndGroupOfOwnName_FirstEndGroupWinsAndFollowingTestsAreParsed()
        {
            // Known limitation: a test logging "EndGroup: <own name> [Passed]" can not be told apart from
            // real framing. What matters is that the parser recovers and reports the following tests correctly.
            var testCases = CapturedTaefOutputs.ToTestCases(LabDll,
                "EdgeCaseTests::SpoofFramingLines", "UnflushedOutputTests::A_UnflushedPrintf", "UnflushedOutputTests::B_AfterUnflushed");
            var parser = new StreamingTaefOutputParser(testCases, MockLogger.Object, MockFrameworkReporter.Object);

            foreach (string line in LabSpoofedFramingRun)
                parser.ReportLine(line);

            parser.TestResults.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal(
                "EdgeCaseTests::SpoofFramingLines Passed",
                "UnflushedOutputTests::A_UnflushedPrintf Passed",
                "UnflushedOutputTests::B_AfterUnflushed Passed");
            ResultOf(parser.TestResults, "UnflushedOutputTests::B_AfterUnflushed").Output.Should().Be("B_AfterUnflushed ran");
            parser.SummaryFound.Should().BeFalse("the spoofed Summary: line is part of the (ignored) spoofed group");
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("'Fake::Test' has not been ended"))), Times.Once);

            parser.ReportLine("");
            parser.ReportLine("Summary: Total=67, Passed=34, Failed=26, Blocked=4, Not Run=1, Skipped=2");
            parser.Flush();
            parser.SummaryFound.Should().BeTrue();
            parser.CrashedTestCase.Should().BeNull();
        }

        #endregion

        #region Fixtures

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FailingSetupFixtures_FailuresAreCarriedToAffectedTests()
        {
            const string fixtureTestsCpp = @"C:\src\TAEF-Test-Adapter\SampleTests\Tests\FixtureTests.cpp";
            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, FixtureTestsRun,
                "TaefSamples::FailingClassSetup::FirstTest", "TaefSamples::FailingClassSetup::SecondTest", "TaefSamples::FailingMethodSetup::Test",
                "TaefSamples::VerifyInClassSetup::Test", "TaefSamples::VerifyInMethodSetup::Test", "TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails",
                "TaefSamples::TableDataTests::Simple#0");

            results.Should().HaveCount(7);

            const string classSetupBlocked = "TestBlocked: TAEF: Setup fixture 'TaefSamples::FailingClassSetup::ClassSetup' for the scope 'TaefSamples::FailingClassSetup' returned 'false'.";
            foreach (string testName in new[] { "TaefSamples::FailingClassSetup::FirstTest", "TaefSamples::FailingClassSetup::SecondTest" })
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result,
                    "Blocked: TAEF: Setup fixture 'TaefSamples::FailingClassSetup::ClassSetup' for the scope 'TaefSamples::FailingClassSetup' returned 'false'.");
                // the fixture output printed since the previous test (cleanups of the previous class, setup of this class)
                result.Output.Should().Be(Lines(
                    "[ClassWithFixtures] method cleanup", "[ClassWithFixtures] class cleanup", "[FailingClassSetup] class setup returns false", classSetupBlocked));
            }

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "TaefSamples::FailingMethodSetup::Test"),
                "Blocked: TAEF: Setup fixture 'TaefSamples::FailingMethodSetup::MethodSetup' for the scope 'TaefSamples::FailingMethodSetup::Test' returned 'false'.");

            TestResult verifyInClassSetup = ResultOf(results, "TaefSamples::VerifyInClassSetup::Test");
            TestResultAssertions.AssertTestResultIsFailure(verifyInClassSetup,
                "#1 - Verify: AreEqual(5, 6): VERIFY failing in TEST_CLASS_SETUP - Values (5, 6)\n" +
                "#2 - TAEF: Setup fixture 'TaefSamples::VerifyInClassSetup::ClassSetup' for the scope 'TaefSamples::VerifyInClassSetup' failed.");
            verifyInClassSetup.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - TaefSamples::VerifyInClassSetup::ClassSetup", fixtureTestsCpp, "143"));
            verifyInClassSetup.Output.Should().Be(Lines(
                @"Error: Verify: AreEqual(5, 6): VERIFY failing in TEST_CLASS_SETUP - Values (5, 6) [File: C:\src\TAEF-Test-Adapter\SampleTests\Tests\FixtureTests.cpp, Function: TaefSamples::VerifyInClassSetup::ClassSetup, Line: 143]",
                "Error: TAEF: Setup fixture 'TaefSamples::VerifyInClassSetup::ClassSetup' for the scope 'TaefSamples::VerifyInClassSetup' failed."));

            TestResult verifyInMethodSetup = ResultOf(results, "TaefSamples::VerifyInMethodSetup::Test");
            TestResultAssertions.AssertTestResultIsFailure(verifyInMethodSetup,
                "#1 - Verify: AreEqual(7, 8): VERIFY failing in TEST_METHOD_SETUP - Values (7, 8)\n" +
                "#2 - TAEF: Setup fixture 'TaefSamples::VerifyInMethodSetup::MethodSetup' for the scope 'TaefSamples::VerifyInMethodSetup::Test' failed.");
            verifyInMethodSetup.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - TaefSamples::VerifyInMethodSetup::MethodSetup", fixtureTestsCpp, "160"));

            // setup failures are not carried beyond their scope
            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, "TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails"));
            TestResult dataRow = ResultOf(results, "TaefSamples::TableDataTests::Simple#0");
            TestResultAssertions.AssertTestResultIsPassed(dataRow);
            dataRow.Output.Should().StartWith(Lines("TAEF: Data[i]: 1", "TAEF: Data[Index]: 0", "TAEF: Data[s]: "));

            // the failing cleanup (after the test's result has been reported) is logged as warning
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.StartsWith("Test 'TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails': cleanup failed after the result has been reported")
                && s.Contains("TestBlocked: TAEF: Cleanup fixture 'TaefSamples::FailingMethodCleanup::MethodCleanup'"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_LabFixtures_ClassFailuresAffectAllTestsOfClassAndMethodFailuresOnlyTheirTest()
        {
            string[] names =
            {
                "GlobalTests::GlobalFailing", "BlockedClassTests::WillBeBlocked1", "BlockedClassTests::WillBeBlocked2",
                "MethodSetupFailTests::MethodSetupBlocked", "MethodCleanupFailTests::PassButCleanupFails",
                "ClassSetupVerifyFailTests::BlockedByVerifyInSetup", "UnicodeTests::Tëst_Ünïcødé_名前"
            };
            IList<TestResult> results = Parse(LabDll, LabFixtureRun, names);

            // GlobalTests::GlobalFailing: the output starts after its StartGroup line
            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(names.Skip(1));
            results.Select(tr => tr.Outcome).Should().Equal(
                TestOutcome.Failed, TestOutcome.Failed, TestOutcome.Failed, TestOutcome.Passed, TestOutcome.Failed, TestOutcome.Passed);

            results[0].ErrorMessage.Should().Be("Blocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup' for the scope 'BlockedClassTests' returned 'false'.");
            results[1].ErrorMessage.Should().Be(results[0].ErrorMessage);
            results[2].ErrorMessage.Should().Be("Blocked: TAEF: Setup fixture 'MethodSetupFailTests::FailingMethodSetup' for the scope 'MethodSetupFailTests::MethodSetupBlocked' returned 'false'.");
            results[4].ErrorMessage.Should().Be(
                "#1 - Verify: AreEqual(5, 6): VERIFY failing inside TEST_CLASS_SETUP - Values (5, 6)\n" +
                "#2 - TAEF: Setup fixture 'ClassSetupVerifyFailTests::VerifyFailingClassSetup' for the scope 'ClassSetupVerifyFailTests' failed.");
            results[4].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - ClassSetupVerifyFailTests::VerifyFailingClassSetup", LabCpp, "436"));
            results[5].ErrorMessage.Should().BeNull();
            results[5].Output.Should().Be("unicode comment: éè 名前 ✓");

            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.StartsWith("Test 'MethodCleanupFailTests::PassButCleanupFails': cleanup failed"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ClassFixtureFailureAndFirstTestOfClassNotRun_FailureIsCarriedToFollowingTest()
        {
            IList<TestResult> results = Parse(LabDll, LabFixtureRun, "BlockedClassTests::WillBeBlocked2");

            results.Should().ContainSingle();
            TestResultAssertions.AssertTestResultIsFailure(results[0],
                "Blocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup' for the scope 'BlockedClassTests' returned 'false'.");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FixtureFailureNotAffectingTestsOfRun_IsLoggedAsDebugWarning()
        {
            IList<TestResult> results = Parse(LabDll, LabFixtureRun, "UnicodeTests::Tëst_Ünïcødé_名前");

            TestResultAssertions.AssertTestResultIsPassed(results.Single());
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s =>
                s.Contains("fixture failure for scope 'BlockedClassTests' did not affect any of the tests run"))), Times.Once);
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s =>
                s.Contains("fixture failure for scope 'MethodSetupFailTests::MethodSetupBlocked' did not affect any of the tests run"))), Times.Once);
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s =>
                s.Contains("fixture failure for scope 'ClassSetupVerifyFailTests' did not affect any of the tests run"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FailingModuleSetup_FailureIsCarriedToAllTestsOfTestDll()
        {
            const string testDll = @"C:\tests\My_taef.dll";
            string[] output =
            {
                "Test Authoring and Execution Framework v10.104k for x64",
                @"Error: Verify: IsTrue(false): module setup [File: C:\src\Module.cpp, Function: ModuleSetup, Line: 5]",
                @"Error: TAEF: Setup fixture 'ModuleSetup' for the scope 'D:\other\location\My_taef.dll' failed.",
                "",
                "StartGroup: A::T1",
                "EndGroup: A::T1 [Failed]",
                "",
                "StartGroup: B::T2",
                "EndGroup: B::T2 [Failed]",
                "",
                "Summary: Total=2, Passed=0, Failed=2, Blocked=0, Not Run=0, Skipped=0"
            };

            IList<TestResult> results = Parse(testDll, output, "A::T1", "B::T2");

            results.Should().HaveCount(2);
            foreach (TestResult result in results)
            {
                TestResultAssertions.AssertTestResultIsFailure(result,
                    "#1 - Verify: IsTrue(false): module setup\n#2 - TAEF: Setup fixture 'ModuleSetup' for the scope 'D:\\other\\location\\My_taef.dll' failed.");
                result.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - ModuleSetup", @"C:\src\Module.cpp", "5"));
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CleanupFailuresAfterEndGroup_AreLoggedAsWarningsAndDoNotChangeResults()
        {
            string[] names =
            {
                TestResources.TestNames.LeakCheckPassing, TestResources.TestNames.LeakCheckFailing,
                TestResources.TestNames.LeakCheckPassingAndLeaking, TestResources.TestNames.LeakCheckFailingAndLeaking
            };
            IList<TestResult> results = Parse(LeakCheckTestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.LeakCheckTestsRun), names);

            results.Select(tr => tr.Outcome).Should().Equal(TestOutcome.Passed, TestOutcome.Failed, TestOutcome.Failed, TestOutcome.Failed);

            // the leak errors printed after the EndGroup of a test are neither added to it nor carried to the next test
            ResultOf(results, TestResources.TestNames.LeakCheckPassingAndLeaking).ErrorMessage
                .Should().Be("Verify: AreEqual(static_cast<size_t>(0), leakedBytes): memory leaked by the test: 100 bytes in 1 blocks - Values (0, 100)");
            ResultOf(results, TestResources.TestNames.LeakCheckFailingAndLeaking).ErrorMessage.Should().Be("Verify: IsTrue(false)");

            foreach (string testName in new[] { TestResources.TestNames.LeakCheckPassingAndLeaking, TestResources.TestNames.LeakCheckFailingAndLeaking })
            {
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                    s.StartsWith($"Test '{testName}': cleanup failed after the result has been reported")
                    && s.Contains("Error: memory leak detected after the test: 100 bytes in 1 blocks")
                    && s.Contains($"Error: TAEF: Cleanup fixture 'TaefSamples::MemoryLeaks::CheckForLeaks' for the scope '{testName}' failed."))), Times.Once);
            }
            // the "Summary of Errors Outside of Tests" does not produce further warnings
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(2));
            _parser.SummaryFound.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FailingClassAndModuleCleanups_AreLoggedAsWarningsNamingTheirScope()
        {
            const string testDll = @"C:	ests\My_taef.dll";
            IList<TestResult> results = Parse(testDll, new[]
                {
                    "",
                    "StartGroup: A::T1",
                    "EndGroup: A::T1 [Passed]",
                    "TestBlocked: TAEF: Cleanup fixture 'A::ClassCleanup' for the scope 'A' returned 'false'.",
                    @"Error: Verify: IsTrue(false): module cleanup [File: C:\src\Module.cpp, Function: ModuleCleanup, Line: 9]",
                    "Error: TAEF: Cleanup fixture 'ModuleCleanup' for the scope '" + testDll + "' failed.",
                    "",
                    "Summary: Total=1, Passed=1, Failed=0, Blocked=0, Not Run=0, Skipped=0"
                },
                "A::T1");

            TestResultAssertions.AssertTestResultIsPassed(results.Single());
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.StartsWith("Test class 'A': cleanup failed after the result has been reported")
                && s.EndsWith("TestBlocked: TAEF: Cleanup fixture 'A::ClassCleanup' for the scope 'A' returned 'false'."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.StartsWith($"Test DLL '{testDll}': cleanup failed after the result has been reported")
                && s.Contains("Error: Verify: IsTrue(false): module cleanup")
                && s.EndsWith($"Error: TAEF: Cleanup fixture 'ModuleCleanup' for the scope '{testDll}' failed."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TestDllCanNotBeLoaded_FailureIsCarriedToAllTestsAndLoggedOnce()
        {
            const string testDll = @"C:\src\TAEF-Test-Adapter\out\TaefOutputCapture\DllTests_taef.dll";
            IList<TestResult> results = Parse(testDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.DllTestsRunWithoutDependency),
                TestResources.TestNames.DllTestsPassing, TestResources.TestNames.DllTestsFailing);

            results.Should().HaveCount(2);
            foreach (TestResult result in results)
            {
                TestResultAssertions.AssertTestResultIsFailure(result);
                result.ErrorMessage.Should().StartWith("Blocked: TAEF: [HRESULT: 0x8007007E] A failure occurred while preparing to run tests in 'DllTests_taef.dll'. (Failed to load ");
            }

            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("A failure occurred while preparing to run tests in 'DllTests_taef.dll'"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_LoopMode_EveryEndGroupProducesAResult()
        {
            IList<TestResult> results = Parse(DllTestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.DllTestsRunLoop),
                TestResources.TestNames.DllTestsPassing, TestResources.TestNames.DllTestsFailing);

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal(
                "TaefSamples::Passing::InvokeFunction Passed", "TaefSamples::Failing::InvokeFunction Failed",
                "TaefSamples::Passing::InvokeFunction Passed", "TaefSamples::Failing::InvokeFunction Failed");
            MockFrameworkReporter.Verify(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()), Times.Exactly(4));
            MockFrameworkReporter.Verify(r => r.ReportTestResults(It.IsAny<IEnumerable<TestResult>>()), Times.Exactly(4));
            _parser.SummaryFound.Should().BeTrue();
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_LoopMode_FixtureFailuresAreNotCarriedIntoNextLoop()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "TestBlocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup' for the scope 'BlockedClassTests' returned 'false'.",
                    "",
                    "StartGroup: BlockedClassTests::WillBeBlocked1",
                    "EndGroup: BlockedClassTests::WillBeBlocked1 [Blocked]",
                    "",
                    "Loop Summary: Total=1, Passed=0, Failed=0, Blocked=1, Not Run=0, Skipped=0",
                    "",
                    "StartGroup: BlockedClassTests::WillBeBlocked1",
                    "EndGroup: BlockedClassTests::WillBeBlocked1 [Passed]"
                },
                "BlockedClassTests::WillBeBlocked1");

            results.Should().HaveCount(2);
            results[0].ErrorMessage.Should().StartWith("Blocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup'");
            TestResultAssertions.AssertTestResultIsPassed(results[1]);
            results[1].Output.Should().BeNull();
        }

        private static string ClassSetupReturnedFalse(string fixture, string classRow)
            => $"TAEF: Setup fixture '{fixture}' for the scope '{classRow}' returned 'false'.";

        private static string ClassSetupFailed(string fixture, string classRow)
            => $"TAEF: Setup fixture '{fixture}' for the scope '{classRow}' failed.";

        private static void AssertIsBlockedByClassSetupOfRow(TestResult result, string fixture, string classRow, string otherClassRow)
        {
            string because = $"the setup of class row {classRow} has blocked {result.TestCase.FullyQualifiedName}";
            TestResultAssertions.AssertTestResultIsFailure(result);
            result.ErrorMessage.Should().StartWith(StreamingTaefOutputParser.BlockedPrefix, because);
            result.ErrorMessage.Should().Contain(ClassSetupReturnedFalse(fixture, classRow), because);
            result.ErrorMessage.Should().NotContain($"'{otherClassRow}'", "the failure of another class row does not affect the test");
            result.Output.Should().Contain("TestBlocked: " + ClassSetupReturnedFalse(fixture, classRow));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FailingClassSetupsOfNamedClassRowsWithinNamespace_FailuresAreCarriedToTheTestsOfTheirRow()
        {
            // the names of named rows of data-driven classes within a namespace can not be told apart from those of method
            // rows ("Ns::C#one::A" is a test of class row "Ns::C#one" or row "one::A" of method "Ns::C"), but TE.exe prints
            // the scope of the class row
            string[] names =
            {
                "ProbeNs::ClassDataBlocked#one::A", "ProbeNs::ClassDataBlocked#one::B",
                "ProbeNs::ClassDataBlocked#two::A", "ProbeNs::ClassDataBlocked#two::B",
                "ProbeNs::ClassLightBlocked#metadataSet0::A", "ProbeNs::ClassLightBlocked#metadataSet1::A",
                "GlobalDataBlocked#one::A", "GlobalDataBlocked#two::A",
                "ProbeNs::ClassDataVerifySetup#one::A", "ProbeNs::ClassDataVerifySetup#two::A"
            };
            IList<TestResult> results = Parse(ProbeTaefOutputs.ProbeDll, ProbeTaefOutputs.RunOfClassesWithFailingSetups, names);

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(names);

            const string classDataSetup = "ProbeNs::ClassDataBlocked::Setup";
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "ProbeNs::ClassDataBlocked#one::A"), classDataSetup, "ProbeNs::ClassDataBlocked#one", "ProbeNs::ClassDataBlocked#two");
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "ProbeNs::ClassDataBlocked#one::B"), classDataSetup, "ProbeNs::ClassDataBlocked#one", "ProbeNs::ClassDataBlocked#two");
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "ProbeNs::ClassDataBlocked#two::A"), classDataSetup, "ProbeNs::ClassDataBlocked#two", "ProbeNs::ClassDataBlocked#one");
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "ProbeNs::ClassDataBlocked#two::B"), classDataSetup, "ProbeNs::ClassDataBlocked#two", "ProbeNs::ClassDataBlocked#one");

            // lightweight class rows and rows of a class in the global namespace
            const string classLightSetup = "ProbeNs::ClassLightBlocked::Setup";
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "ProbeNs::ClassLightBlocked#metadataSet0::A"), classLightSetup, "ProbeNs::ClassLightBlocked#metadataSet0", "ProbeNs::ClassLightBlocked#metadataSet1");
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "ProbeNs::ClassLightBlocked#metadataSet1::A"), classLightSetup, "ProbeNs::ClassLightBlocked#metadataSet1", "ProbeNs::ClassLightBlocked#metadataSet0");
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "GlobalDataBlocked#one::A"), "GlobalDataBlocked::Setup", "GlobalDataBlocked#one", "GlobalDataBlocked#two");
            AssertIsBlockedByClassSetupOfRow(ResultOf(results, "GlobalDataBlocked#two::A"), "GlobalDataBlocked::Setup", "GlobalDataBlocked#two", "GlobalDataBlocked#one");

            // a VERIFY failing in the class setup: the error lines printed before the fixture failure explain the result
            foreach (string row in new[] { "one", "two" })
            {
                TestResult result = ResultOf(results, $"ProbeNs::ClassDataVerifySetup#{row}::A");
                string because = $"the setup of class row ProbeNs::ClassDataVerifySetup#{row} has failed";
                TestResultAssertions.AssertTestResultIsFailure(result);
                result.ErrorMessage.Should().Contain("Verify: AreEqual(5, 6): VERIFY failing in class setup - Values (5, 6)", because);
                result.ErrorMessage.Should().Contain(ClassSetupFailed("ProbeNs::ClassDataVerifySetup::Setup", $"ProbeNs::ClassDataVerifySetup#{row}"), because);
                result.ErrorStackTrace.Should().Contain(ErrorMessageParser.CreateStackTraceEntry("#1 - ProbeNs::ClassDataVerifySetup::Setup", "Probe.cpp", "139"), because);
                result.Output.Should().Contain("Error: " + ClassSetupFailed("ProbeNs::ClassDataVerifySetup::Setup", $"ProbeNs::ClassDataVerifySetup#{row}"));
            }

            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("did not affect any of the tests run"))), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FailingClassSetupsOfNamedClassRowsAndFirstTestsOfRowsNotRun_FailuresAreCarriedToFollowingTestsOfTheirRow()
        {
            IList<TestResult> results = Parse(ProbeTaefOutputs.ProbeDll, ProbeTaefOutputs.RunOfClassesWithFailingSetups,
                "ProbeNs::ClassDataBlocked#one::B", "ProbeNs::ClassDataBlocked#two::B");

            results.Should().HaveCount(2);
            const string setup = "ProbeNs::ClassDataBlocked::Setup";
            AssertIsBlockedByClassSetupOfRow(results[0], setup, "ProbeNs::ClassDataBlocked#one", "ProbeNs::ClassDataBlocked#two");
            AssertIsBlockedByClassSetupOfRow(results[1], setup, "ProbeNs::ClassDataBlocked#two", "ProbeNs::ClassDataBlocked#one");
        }

        #endregion

        #region Crashes, timeouts, errors outside of tests

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CrashesOutOfProcess_CrashingTestsFailAndRunContinues()
        {
            IList<TestResult> results = Parse(CrashingTestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.CrashingTestsRun),
                TestResources.TestNames.CrashingAddFailsBeforeCrash, TestResources.TestNames.CrashingAddPassesBeforeCrash,
                TestResources.TestNames.CrashingTheCrash, TestResources.TestNames.CrashingAddFailsAfterCrash,
                TestResources.TestNames.CrashingAddPassesAfterCrash, TestResources.TestNames.CrashingLongRunning,
                TestResources.TestNames.CrashingTheAbort, TestResources.TestNames.CrashingTheStackOverflow,
                TestResources.TestNames.CrashingAddPassesAfterAllCrashes);

            results.Should().HaveCount(TestResources.NrOfCrashingTests);
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(TestResources.NrOfCrashingTestsPassing);
            results.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(TestResources.NrOfCrashingTestsFailing + TestResources.NrOfCrashingTestsCrashing);
            results.Should().NotContain(tr => tr.Outcome == TestOutcome.Skipped, "'TestSkipped: TAEF: The cleanup method ...' lines are no results");

            foreach ((string testName, string exitCode) in new[]
            {
                (TestResources.TestNames.CrashingTheCrash, "0xC0000005"),
                (TestResources.TestNames.CrashingTheAbort, "0x00000003"),
                (TestResources.TestNames.CrashingTheStackOverflow, "0xC00000FD")
            })
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result,
                    $"TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: '{testName}'. (The test host process was unexpectedly terminated with exit code {exitCode} while invoking a test operation. (An RPC call failed.))");
                result.ErrorStackTrace.Should().BeNull();
            }
            ResultOf(results, TestResources.TestNames.CrashingTheCrash).Output.Should().StartWith("About to dereference a null pointer" + Environment.NewLine);

            _parser.CrashedTestCase.Should().BeNull();
            _parser.SummaryFound.Should().BeTrue();
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_OutputEndsWithinGroup_TestIsReportedAsCrashed()
        {
            string[] names =
            {
                TestResources.TestNames.CrashingAddFailsBeforeCrash, TestResources.TestNames.CrashingAddPassesBeforeCrash,
                TestResources.TestNames.CrashingTheCrash, TestResources.TestNames.CrashingAddFailsAfterCrash,
                TestResources.TestNames.CrashingAddPassesAfterCrash, TestResources.TestNames.CrashingLongRunning,
                TestResources.TestNames.CrashingTheAbort, TestResources.TestNames.CrashingTheStackOverflow,
                TestResources.TestNames.CrashingAddPassesAfterAllCrashes
            };
            IList<TestResult> results = Parse(CapturedTaefOutputs.ToTestCases(CrashingTestsDll, names),
                CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.CrashingTestsRunInProcess), unchecked((int)0xC0000005));

            results.Should().HaveCount(TestResources.NrOfCrashingTests - TestResources.NrOfCrashingTestsNotStartedInProcess);
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(TestResources.NrOfCrashingTestsPassingInProcess);

            TestResult crashed = ResultOf(results, TestResources.TestNames.CrashingTheCrash);
            TestResultAssertions.AssertTestResultIsFailure(crashed,
                StreamingTaefOutputParser.CrashText + " (TE.exe terminated with exit code 0xC0000005)\nTest output:\n\nAbout to dereference a null pointer");
            crashed.Output.Should().Be("About to dereference a null pointer");

            _parser.CrashedTestCase.FullyQualifiedName.Should().Be(TestResources.TestNames.CrashingTheCrash);
            _parser.SummaryFound.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_OutputEndsWithinGroupAndExitCodeUnknown_CrashTextWithoutExitCode()
        {
            IList<TestResult> results = Parse(CrashingTestsDll, new[]
                {
                    "",
                    "StartGroup: TaefSamples::Crashing::TheCrash"
                },
                TestResources.TestNames.CrashingTheCrash);

            TestResultAssertions.AssertTestResultIsFailure(results.Single(), StreamingTaefOutputParser.CrashText);
            results.Single().Output.Should().BeNull();
            _parser.CrashedTestCase.Should().NotBeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_OutputEndsWithinGroupOfTestNotPartOfRun_NoCrashIsReported()
        {
            IList<TestResult> results = Parse(CapturedTaefOutputs.ToTestCases(CrashingTestsDll,
                    TestResources.TestNames.CrashingAddFailsBeforeCrash, TestResources.TestNames.CrashingAddPassesBeforeCrash),
                CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.CrashingTestsRunInProcess), unchecked((int)0xC0000005));

            results.Should().HaveCount(2);
            _parser.CrashedTestCase.Should().BeNull();
            _parser.SummaryFound.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TestTimeouts_TestsFailWithTimeoutMessages()
        {
            IList<TestResult> results = Parse(@"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\LongRunningTests_taef.dll",
                CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.LongRunningTestsRunTimeout),
                TestResources.TestNames.LongRunningTest1, TestResources.TestNames.LongRunningTest2);

            results.Should().HaveCount(TestResources.NrOfLongRunningTests);
            foreach (TestResult result in results)
            {
                TestResultAssertions.AssertTestResultIsFailure(result);
                result.ErrorMessage.Should().StartWith("#1 - TAEF: The user-specified test timeout has expired. TAEF will now abort the test.");
                result.ErrorMessage.Should().Contain($"\n#2 - TAEF: [HRESULT 0x800705B4] A test timeout expired while running a test operation: '{result.TestCase.FullyQualifiedName}'.");
                result.Output.Should().Contain("Warning: TAEF: Forcibly terminating a test host process (PID: ");
            }

            // the TAEF warnings are part of the tests' output, and the summary section is not logged
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_NoTestMatchesSelection_ErrorsAreLoggedOnceAsWarnings()
        {
            IList<TestResult> results = Parse(DllTestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorNoMatchingTests), "DoesNotExist::Test");

            results.Should().BeEmpty();
            _parser.SummaryFound.Should().BeFalse();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.EndsWith("Error: TAEF: The selection criteria did not match any tests."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.EndsWith("Error: TAEF: No test cases were executed."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TestDllDoesNotExist_WarningAndErrorAreLoggedOnce()
        {
            IList<TestResult> results = Parse(@"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\DoesNotExist_taef.dll",
                CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorNoTestFiles), "DoesNotExist::Test");

            results.Should().BeEmpty();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Warning: TAEF: The test file ") && s.Contains("does not exist."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.EndsWith("Error: TAEF: None of the specified test files were found."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_StartupErrorWithContinuationLine_IsLoggedAsOneWarning()
        {
            IList<TestResult> results = Parse(DllTestsDll, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorSelectSyntaxError), "TaefSamples::Passing::InvokeFunction");

            results.Should().BeEmpty();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.Contains("Error: TAEF: [HRESULT 0x80004004] An error occurred during TAEF startup. (Syntax error in selection criteria.")
                && s.EndsWith(Environment.NewLine + " Exception: A string literal was not terminated.)"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_LoggerCanNotBeInitialized_ErrorIsLoggedAsWarning()
        {
            // TaefLab.dll run by a TE.exe without WTTLog.dll (exit code 0x03000000)
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "Test Authoring and Execution Framework v10.104k for x64",
                    "",
                    "[HRESULT 0x8007007E] Failed to initialize the logger. (Failed to delay-load WTTLog.dll.)"
                },
                "GlobalTests::GlobalPassing");

            results.Should().BeEmpty();
            _parser.SummaryFound.Should().BeFalse();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.EndsWith("[HRESULT 0x8007007E] Failed to initialize the logger. (Failed to delay-load WTTLog.dll.)"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_NotATestDll_ErrorIsLoggedOnceAsWarning()
        {
            IList<TestResult> results = Parse(@"C:\src\TAEF-Test-Adapter\out\binaries\SampleTests\Debug-x64\DllProject.dll",
                CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.ErrorNotATestDll), "Some::Test");

            results.Should().BeEmpty();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.EndsWith("(The file was not recognized to be a TAEF test.)"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region /inproc

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_EndGroupGluedToOutput_IsRecognized()
        {
            IList<TestResult> results = Parse(LabDll, LabInProcessOutputRun,
                "TaefLab::Basic::BasicTests::Output", "TaefLab::Basic::BasicTests::OutputThenFail");

            results.Should().HaveCount(2);
            TestResult output = ResultOf(results, "TaefLab::Basic::BasicTests::Output");
            TestResultAssertions.AssertTestResultIsPassed(output);
            output.Output.Should().Be(Lines(
                "printf line 1", "std::cout line 2", "wprintf line 5", "Log::Comment line 7", "Warning: Log::Warning line 8",
                "multi-line comment line A", "multi-line comment line B", "printf without newline at end"));

            TestResult outputThenFail = ResultOf(results, "TaefLab::Basic::BasicTests::OutputThenFail");
            TestResultAssertions.AssertTestResultIsFailure(outputThenFail, "Verify: AreEqual(10, 20) - Values (10, 20)");
            outputThenFail.Output.Should().StartWith(Lines("printf before failure", "comment before failure"));

            _parser.SummaryFound.Should().BeTrue();
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ErrorGluedToOutput_TestFailsWithMessageAndStackTrace()
        {
            IList<TestResult> results = Parse(CapturedTaefOutputs.TestsDll, InProcessGluedErrorRun,
                "TaefSamples::OutputHandling::Output_OneLine", "TaefSamples::OutputHandling::ManyLinesWithNewlines");

            TestResult result = ResultOf(results, "TaefSamples::OutputHandling::Output_OneLine");
            TestResultAssertions.AssertTestResultIsFailure(result, "Verify: AreEqual(1, 2): test output - Values (1, 2)");
            result.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry(
                "TaefSamples::OutputHandling::Output_OneLine", @"C:\src\TAEF-Test-Adapter\SampleTests\Tests\BasicTests.cpp", "73"));
            result.Output.Should().Contain("printf before test without newline (visible with /inproc only)");
        }

        #endregion

        #region Complete captured runs of Tests_taef.dll

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CompleteRunOfSampleTests_OutcomesAsDocumented()
        {
            IList<TestCase> testCases = GetCapturedTestCasesOfTestsDll();
            IList<TestResult> results = Parse(testCases, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.TestsRun));

            results.Should().HaveCount(TestResources.NrOfNotIgnoredTests);
            results.Select(tr => tr.TestCase).Should().OnlyHaveUniqueItems();
            results.Should().NotContain(tr => TaefConstants.IsIgnored(tr.TestCase));
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(TestResources.NrOfPassingTests);
            results.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(TestResources.NrOfFailingTests + TestResources.NrOfBlockedTests);
            results.Count(tr => tr.Outcome == TestOutcome.Failed && tr.ErrorMessage.StartsWith(StreamingTaefOutputParser.BlockedPrefix))
                .Should().Be(TestResources.NrOfBlockedTests);
            results.Count(tr => tr.Outcome == TestOutcome.Skipped).Should().Be(TestResources.NrOfSkippedTests);
            results.Count(tr => tr.Outcome == TestOutcome.None).Should().Be(TestResources.NrOfNotRunTests);
            results.Where(tr => tr.Outcome == TestOutcome.Failed).Should().OnlyContain(tr => !string.IsNullOrEmpty(tr.ErrorMessage));

            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, TestResources.TestNames.TestDirectoryIsSet));
            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, TestResources.TestNames.WorkingDirIsSolutionDirectory));
            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, TestResources.TestNames.EnvironmentVariableIsSet));
            ResultOf(results, TestResources.TestNames.MissingDataSource).ErrorMessage.Should().StartWith(
                "Blocked: TAEF: [HRESULT: 0x80070002] Failed to find the data source: MissingDataSource.xml.");
            ResultOf(results, "TaefSamples::Ümlautß::Träits").Output.Should().Be("Ümlaut output: äöü ÄÖÜ ß 名前 ✓");
            // TE.exe prints the Description property before and within the group of the test
            ResultOf(results, "TaefSamples::ClassAndMethodProperties::WithCustomPropertiesAndFails").Output
                .Should().StartWith("Property: TAEF: Description [A test with custom properties]" + Environment.NewLine + "Error: ");
            TestResult messageParserTest = ResultOf(results, "TaefSamples::MessageParserTests::LogErrorWithSourceInfo");
            messageParserTest.ErrorMessage.Should().Be("#1 - Log::Error with source information\n#2 - Log::Error without source information");
            messageParserTest.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry(
                "#1 - TaefSamples::LogErrorWithSourceInfoInOtherFile", SamplesDir + @"Tests\Helpers.cpp", "14"));

            _parser.SummaryFound.Should().BeTrue();
            _parser.CrashedTestCase.Should().BeNull();
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.StartsWith("Test 'TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails': cleanup failed"))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CompleteRunOfSampleTestsWithoutSampleSettings_OutcomesAsDocumented()
        {
            IList<TestCase> testCases = GetCapturedTestCasesOfTestsDll();
            IList<TestResult> results = Parse(testCases, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.TestsRunWithoutSettings));

            results.Should().HaveCount(TestResources.NrOfNotIgnoredTests);
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(TestResources.NrOfPassingTestsWithoutSampleSettings);
            results.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(TestResources.NrOfFailingTestsWithoutSampleSettings + TestResources.NrOfBlockedTests);
            results.Count(tr => tr.Outcome == TestOutcome.Skipped).Should().Be(TestResources.NrOfSkippedTests);
            results.Count(tr => tr.Outcome == TestOutcome.None).Should().Be(TestResources.NrOfNotRunTests);

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, TestResources.TestNames.TestDirectoryIsSet));
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, TestResources.TestNames.WorkingDirIsSolutionDirectory));
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, TestResources.TestNames.EnvironmentVariableIsSet));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CompleteRunOfSampleTestsInProcess_OutcomesAsDocumentedAndOutputOfTestsIsVisible()
        {
            IList<TestCase> testCases = GetCapturedTestCasesOfTestsDll();
            IList<TestResult> results = Parse(testCases, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.TestsRunInProcess));

            results.Should().HaveCount(TestResources.NrOfNotIgnoredTests);
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(TestResources.NrOfPassingTests);
            results.Count(tr => tr.Outcome == TestOutcome.Failed).Should().Be(TestResources.NrOfFailingTests + TestResources.NrOfBlockedTests);
            results.Where(tr => tr.Outcome == TestOutcome.Failed).Should().OnlyContain(tr => !string.IsNullOrEmpty(tr.ErrorMessage));

            ResultOf(results, "TaefSamples::OutputHandling::OutputOfPassingTest").Output.Should().Contain("printf output (visible with /inproc only)");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "TaefSamples::OutputHandling::Output_OneLine"), "Verify: AreEqual(1, 2): test output - Values (1, 2)");
            _parser.SummaryFound.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SelectedTestsOfSampleTests_OnlyRequestedTestsAreReported()
        {
            IList<TestCase> allTestCases = GetCapturedTestCasesOfTestsDll();
            List<TestCase> testCases = allTestCases.Where(tc => tc.FullyQualifiedName != TestResources.TestNames.MissingDataSource).ToList();

            IList<TestResult> results = Parse(testCases, CapturedTaefOutputs.ReadLines(CapturedTaefOutputs.TestsRunSelected));

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(
                "TaefSamples::TestMath::AddPasses", "TaefSamples::ClassWithFixtures::AddFails", "TaefSamples::ClassWithFixtures::AddPasses", "TaefSamples::ClassWithFixtures::AddPassesWithTraits",
                "TaefSamples::ClassWithFixtures::AddPassesWithTraits2", "TaefSamples::ClassWithFixtures::AddPassesWithTraits3", "TaefSamples::ClassWithFixtures::SetupRunsBeforeEachTest",
                "TaefSamples::NamedRows::SpecialCharacters#with'quote", "TaefSamples::Ümlautß::Täst", "TaefSamples::Ümlautß::Träits");
            results.Count(tr => tr.Outcome == TestOutcome.Passed).Should().Be(7);
        }

        #endregion

    }

}
