// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Tests of <see cref="ErrorMessageParser"/> with (verbatim) lines TE.exe prints within the group of a test.
    /// </summary>
    [TestClass]
    public class ErrorMessageParserTests
    {
        private const string BasicTestsCpp = @"C:\src\TAEF-Test-Adapter\SampleTests\Tests\BasicTests.cpp";
        private const string MessageParserTestsCpp = @"C:\src\TAEF-Test-Adapter\SampleTests\Tests\MessageParserTests.cpp";
        private const string LabCpp = @"C:\src\TaefLab\TaefLab.cpp";
        private const string LabHelpersCpp = @"C:\src\TaefLab\TaefLabHelpers.cpp";

        private static ErrorMessageParser Parse(params string[] lines)
        {
            var parser = new ErrorMessageParser(lines);
            parser.Parse();
            return parser;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_VerifyErrorWithSourceInformation_MessageWithoutSourceInformationAndClickableStackTrace()
        {
            var parser = Parse(
                "Error: Verify: AreEqual(1000, Add(10, 10)) - Values (1000, 20) [File: " + BasicTestsCpp + ", Function: TaefSamples::TestMath::AddFails, Line: 22]");

            parser.NrOfMessages.Should().Be(1);
            parser.ErrorMessage.Should().Be("Verify: AreEqual(1000, Add(10, 10)) - Values (1000, 20)");
            parser.ErrorStackTrace.Should().Be($"at TaefSamples::TestMath::AddFails in {BasicTestsCpp}:line 22{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_VerifyErrorWithUserMessage_UserMessageIsKept()
        {
            var parser = Parse(
                "Error: Verify: AreEqual(42, 43): custom message here - Values (42, 43) [File: " + LabCpp + ", Function: TaefLab::Basic::BasicTests::FailAreEqualWithMessage, Line: 85]");

            parser.ErrorMessage.Should().Be("Verify: AreEqual(42, 43): custom message here - Values (42, 43)");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("TaefLab::Basic::BasicTests::FailAreEqualWithMessage", LabCpp, "85"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ErrorInHelperFunction_StackTracePointsToHelper()
        {
            var parser = Parse(
                "Error: Verify: IsGreaterThan(value, 0): value must be positive (helper .cpp) - Values (-5, 0) [File: " + LabHelpersCpp + ", Function: TaefLabHelpers::VerifyValueIsPositive, Line: 10]");

            parser.ErrorMessage.Should().Be("Verify: IsGreaterThan(value, 0): value must be positive (helper .cpp) - Values (-5, 0)");
            parser.ErrorStackTrace.Should().Be($"at TaefLabHelpers::VerifyValueIsPositive in {LabHelpersCpp}:line 10{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ErrorsWithAndWithoutSourceInformation_MessagesAreNumbered()
        {
            var parser = Parse(
                "Error: Explicit Log::Error with source info [File: " + LabHelpersCpp + ", Function: TaefLabHelpers::LogErrorWithSourceInfo, Line: 15]",
                "Error: Plain Log::Error without source info");

            parser.NrOfMessages.Should().Be(2);
            parser.ErrorMessage.Should().Be("#1 - Explicit Log::Error with source info\n#2 - Plain Log::Error without source info");
            parser.ErrorStackTrace.Should().Be($"at #1 - TaefLabHelpers::LogErrorWithSourceInfo in {LabHelpersCpp}:line 15{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_TwoErrorsWithScopeLinesInBetween_BothErrorsAreReportedAndOtherLinesAreIgnored()
        {
            var parser = Parse(
                "Scope: TestMethod (MessageParserTests.cpp:102)",
                "Scope: HelperMethod (MessageParserTests.cpp:20)",
                "Error: Verify: AreEqual(0, i) - Values (0, 1) [File: " + MessageParserTestsCpp + ", Function: TaefSamples::CheckIfZero, Line: 15]",
                "End of scope: HelperMethod",
                "Error: Verify: AreEqual(0, 1) - Values (0, 1) [File: " + MessageParserTestsCpp + ", Function: TaefSamples::MessageParserTests::LogScopeInTestMethodAndHelperMethodAndVerifyInTestMethod, Line: 104]",
                "End of scope: TestMethod");

            parser.NrOfMessages.Should().Be(2);
            parser.ErrorMessage.Should().Be("#1 - Verify: AreEqual(0, i) - Values (0, 1)\n#2 - Verify: AreEqual(0, 1) - Values (0, 1)");
            parser.ErrorStackTrace.Should().Be(
                ErrorMessageParser.CreateStackTraceEntry("#1 - TaefSamples::CheckIfZero", MessageParserTestsCpp, "15") +
                ErrorMessageParser.CreateStackTraceEntry("#2 - TaefSamples::MessageParserTests::LogScopeInTestMethodAndHelperMethodAndVerifyInTestMethod", MessageParserTestsCpp, "104"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_Exceptions_MessagesWithoutStackTrace()
        {
            Parse("Error: Caught std::exception: std::runtime_error thrown by the test")
                .ErrorMessage.Should().Be("Caught std::exception: std::runtime_error thrown by the test");
            Parse("Error: Caught an unidentified C++ exception")
                .ErrorMessage.Should().Be("Caught an unidentified C++ exception");

            var parser = Parse("Error: Caught WEX::Common::Exception: WEX::Common::Exception thrown by the test [HRESULT: 0x80070057]");
            parser.ErrorMessage.Should().Be("Caught WEX::Common::Exception: WEX::Common::Exception thrown by the test [HRESULT: 0x80070057]");
            parser.ErrorStackTrace.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_CrashOfTestHost_MessageWithoutStackTrace()
        {
            var parser = Parse(
                "About to dereference a null pointer",
                "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'TaefSamples::Crashing::TheCrash'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))");

            parser.NrOfMessages.Should().Be(1);
            parser.ErrorMessage.Should().Be("TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'TaefSamples::Crashing::TheCrash'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))");
            parser.ErrorStackTrace.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_Timeout_AllErrorsAreReportedButNoWarning()
        {
            var parser = Parse(
                "Error: TAEF: The user-specified test timeout has expired. TAEF will now abort the test. The /miniDumpOnTimeout switch can be used to have TAEF gather a minidump of the test host process and the /liveKernelDumpOnTimeout switch can be used to have TAEF gather an LKD.",
                "Warning: TAEF: Forcibly terminating a test host process (PID: 42372).",
                "Error: TAEF: [HRESULT 0x800705B4] A test timeout expired while running a test operation: 'TaefSamples::LongRunningTests::Test1'. (The test host process was terminated by TAEF while invoking a test operation.)");

            parser.NrOfMessages.Should().Be(2);
            parser.ErrorMessage.Should().StartWith("#1 - TAEF: The user-specified test timeout has expired.");
            parser.ErrorMessage.Should().Contain("\n#2 - TAEF: [HRESULT 0x800705B4] A test timeout expired while running a test operation: 'TaefSamples::LongRunningTests::Test1'.");
            parser.ErrorMessage.Should().NotContain("Forcibly terminating");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ResultMessages_AreReported()
        {
            Parse("This test decides at runtime that it cannot run on this machine", "TestSkipped: Skipped by the test")
                .ErrorMessage.Should().Be("Skipped by the test");
            Parse("TestBlocked: Blocked by the test: a prerequisite is missing")
                .ErrorMessage.Should().Be("Blocked by the test: a prerequisite is missing");
            Parse("TestNotRun: NotRun set by the test")
                .ErrorMessage.Should().Be("NotRun set by the test");
            Parse("TestFailed: Failed set by the test")
                .ErrorMessage.Should().Be("Failed set by the test");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ResultMessageWithoutText_IsIgnored()
        {
            var parser = Parse("TestSkipped: ", "TestBlocked:  ");

            parser.NrOfMessages.Should().Be(0);
            parser.ErrorMessage.Should().BeEmpty();
            parser.ErrorStackTrace.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_FixtureFailureCarriedToTest_ErrorsAreNumbered()
        {
            const string fixtureTestsCpp = @"C:\src\TAEF-Test-Adapter\SampleTests\Tests\FixtureTests.cpp";
            var parser = Parse(
                "Error: Verify: AreEqual(5, 6): VERIFY failing in TEST_CLASS_SETUP - Values (5, 6) [File: " + fixtureTestsCpp + ", Function: TaefSamples::VerifyInClassSetup::ClassSetup, Line: 143]",
                "Error: TAEF: Setup fixture 'TaefSamples::VerifyInClassSetup::ClassSetup' for the scope 'TaefSamples::VerifyInClassSetup' failed.");

            parser.ErrorMessage.Should().Be(
                "#1 - Verify: AreEqual(5, 6): VERIFY failing in TEST_CLASS_SETUP - Values (5, 6)\n" +
                "#2 - TAEF: Setup fixture 'TaefSamples::VerifyInClassSetup::ClassSetup' for the scope 'TaefSamples::VerifyInClassSetup' failed.");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - TaefSamples::VerifyInClassSetup::ClassSetup", fixtureTestsCpp, "143"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ErrorAndResultMessage_BothAreNumbered()
        {
            var parser = Parse(
                "Error: Log::Error marks the test as failed",
                "TestBlocked: TAEF: [HRESULT: 0x80070002] Failed to find the data source: MissingDataSource.xml.");

            parser.ErrorMessage.Should().Be("#1 - Log::Error marks the test as failed\n#2 - TAEF: [HRESULT: 0x80070002] Failed to find the data source: MissingDataSource.xml.");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_OutputWithoutErrors_EmptyMessageAndStackTrace()
        {
            var parser = Parse(
                "Log::Comment output",
                "MyContext: Log::Comment output with a context",
                "Warning: Log::Warning output (does not fail the test)",
                "Verify: IsTrue(true): OutputOfPassingTest",
                "TAEF: Data[i]: 1",
                "Property: TAEF: Description [A test with custom properties]",
                "",
                "Error",
                "Errors: not an error line");

            parser.NrOfMessages.Should().Be(0);
            parser.ErrorMessage.Should().BeEmpty();
            parser.ErrorStackTrace.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_NoLines_EmptyMessageAndStackTrace()
        {
            var parser = new ErrorMessageParser((string[])null);
            parser.ErrorMessage.Should().BeEmpty();
            parser.Parse();
            parser.ErrorMessage.Should().BeEmpty();
            parser.ErrorStackTrace.Should().BeEmpty();

            parser = new ErrorMessageParser((string)null);
            parser.Parse();
            parser.NrOfMessages.Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_CallSiteOfStackTraceOnError_IsIgnored()
        {
            var parser = Parse(
                "Error: Verify: AreEqual(1, 2) - Values (1, 2) [File: " + LabCpp + ", Function: TaefLab::Basic::BasicTests::FailAreEqual, Line: 80]",
                ErrorMessageParser.CallSiteLine,
                "TaefLab.dll!TaefLab::Basic::BasicTests::FailAreEqual+0x5f [" + LabCpp + " @ 80]",
                "");

            parser.NrOfMessages.Should().Be(1);
            parser.ErrorMessage.Should().Be("Verify: AreEqual(1, 2) - Values (1, 2)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ErrorGluedToUnflushedOutput_IsRecognized()
        {
            // TE.exe /inproc: printf() output without new line is glued to TE.exe's next line (verbatim)
            var parser = Parse(
                "before test",
                "printf before test without newline (visible with /inproc only)Error: Verify: AreEqual(1, 2): test output - Values (1, 2) [File: " + BasicTestsCpp + ", Function: TaefSamples::OutputHandling::Output_OneLine, Line: 73]",
                "after test");

            parser.NrOfMessages.Should().Be(1);
            parser.ErrorMessage.Should().Be("Verify: AreEqual(1, 2): test output - Values (1, 2)");
            parser.ErrorStackTrace.Should().Be($"at TaefSamples::OutputHandling::Output_OneLine in {BasicTestsCpp}:line 73{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ErrorWithoutSourceInformationGluedToOutput_IsNotRecognized()
        {
            // without the source information suffix, a glued error can not be told apart from ordinary output (unless it
            // has one of TAEF's fixed forms, see below)
            var parser = Parse("some outputError: something", "CreateFile Error: access denied (logged as comment)");

            parser.NrOfMessages.Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_TaefErrorsAndResultMessagesGluedToUnflushedOutput_AreRecognized()
        {
            // TE.exe /inproc (ParseLab3_taef.dll, verbatim): printf() output without new line is glued to TE.exe's next line
            Parse("working...Error: Caught std::exception: boom").ErrorMessage.Should().Be("Caught std::exception: boom");
            Parse("checking prerequisites...TestSkipped: no GPU available").ErrorMessage.Should().Be("no GPU available");
            Parse("checking...TestBlocked: device missing").ErrorMessage.Should().Be("device missing");
            // a full stdout buffer flushed in the middle of a line (verbatim)
            Parse("progress line Error: Caught std::exception: boom after chatty output").ErrorMessage.Should().Be("Caught std::exception: boom after chatty output");
            Parse("x Error: Caught an unidentified C++ exception").ErrorMessage.Should().Be("Caught an unidentified C++ exception");
            Parse("x Error: Caught WEX::Common::Exception: E_FAIL [HRESULT: 0x80004005]").ErrorMessage.Should().Be("Caught WEX::Common::Exception: E_FAIL [HRESULT: 0x80004005]");
            Parse("x Error: TAEF: The user-specified test timeout has expired.").ErrorMessage.Should().Be("TAEF: The user-specified test timeout has expired.");
            Parse("xTestFailed: failed by the test").ErrorMessage.Should().Be("failed by the test");
            Parse("xTestNotRun: not run").ErrorMessage.Should().Be("not run");
            Parse("xTestSkipped:  ").NrOfMessages.Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_CommentContainingErrorNextToRealError_OnlyRealErrorIsReported()
        {
            // TE.exe /inproc (verbatim): a comment containing "Error: " must not become a message
            var parser = Parse(
                "CreateFile Error: access denied (logged as comment)",
                "Error: Verify: AreEqual(3, 4) - Values (3, 4) [File: " + BasicTestsCpp + ", Function: Lambda::CommentLooksLikeError, Line: 172]");

            parser.NrOfMessages.Should().Be(1);
            parser.ErrorMessage.Should().Be("Verify: AreEqual(3, 4) - Values (3, 4)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_LinesOfTaefContainingGluedMessageForms_AreNoGluedMessages()
        {
            var parser = Parse(
                "Verify: AreEqual(L\"TestFailed: x\", s)",
                "Warning: text TestSkipped: y",
                "TAEF: Data[Text]: Error: TAEF: z",
                "Property: TAEF: Description [TestBlocked: w]");

            parser.NrOfMessages.Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_VerifyOfStringsContainingNewLines_CompleteMessageWithSourceInformation()
        {
            // TE.exe prints the message with its line breaks; only the last line has the source information (ParseLab_taef.dll, verbatim)
            var parser = Parse(
                "Error: Verify: AreEqual(expected, actual) - Values (line 1",
                "line 2, line 1",
                "line X) [File: " + LabCpp + ", Function: MultiLine::VerifyStringsWithNewlines, Line: 135]");

            parser.NrOfMessages.Should().Be(1);
            parser.ErrorMessage.Should().Be("Verify: AreEqual(expected, actual) - Values (line 1\nline 2, line 1\nline X)");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("MultiLine::VerifyStringsWithNewlines", LabCpp, "135"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_VerifyMessagesContainingNewLines_CompleteMessagesWithSourceInformation()
        {
            // verbatim (the \r\n of the second test's strings is split like a line break of TE.exe)
            var parser = Parse(
                "Error: Verify: IsTrue(false): first message line",
                "second message line [File: " + LabCpp + ", Function: MultiLine::VerifyMessageWithNewline, Line: 139]");
            parser.ErrorMessage.Should().Be("Verify: IsTrue(false): first message line\nsecond message line");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("MultiLine::VerifyMessageWithNewline", LabCpp, "139"));

            parser = Parse(
                "Error: Verify: AreEqual(expected, actual) - Values (a",
                "b, a",
                "c) [File: " + LabCpp + ", Function: MultiLine::VerifyStringsWithCR, Line: 153]");
            parser.ErrorMessage.Should().Be("Verify: AreEqual(expected, actual) - Values (a\nb, a\nc)");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("MultiLine::VerifyStringsWithCR", LabCpp, "153"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_MultiLineErrorFollowedByOtherErrors_AllErrorsAreNumbered()
        {
            var parser = Parse(
                "comment before",
                "Error: Verify: AreEqual(expected, actual) - Values (a",
                "",
                "b, a",
                "c) [File: " + LabCpp + ", Function: F, Line: 10]",
                "comment between",
                "Error: Verify: IsTrue(false) [File: " + LabCpp + ", Function: G, Line: 20]",
                "TestFailed: explicitly failed");

            parser.NrOfMessages.Should().Be(3);
            parser.ErrorMessage.Should().Be(
                "#1 - Verify: AreEqual(expected, actual) - Values (a\n\nb, a\nc)\n" +
                "#2 - Verify: IsTrue(false)\n" +
                "#3 - explicitly failed");
            parser.ErrorStackTrace.Should().Be(
                ErrorMessageParser.CreateStackTraceEntry("#1 - F", LabCpp, "10") +
                ErrorMessageParser.CreateStackTraceEntry("#2 - G", LabCpp, "20"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ErrorWithoutSourceInformationContainingNewLines_OnlyFirstLineAndFollowingOutputIsNotAbsorbed()
        {
            // Log::Error(L"error line 1\nerror line 2") followed by Log::Comment(): the lines can not be told apart
            var parser = Parse(
                "Error: error line 1",
                "error line 2",
                "a comment",
                "Error: Verify: IsTrue(false) [File: " + LabCpp + ", Function: G, Line: 20]");

            parser.ErrorMessage.Should().Be("#1 - error line 1\n#2 - Verify: IsTrue(false)");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#2 - G", LabCpp, "20"));

            // verbatim
            Parse("Error: Caught std::exception: exception line 1", "exception line 2").ErrorMessage.Should().Be("Caught std::exception: exception line 1");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_SourceInformationTooFarAwayOrWithinLineOfTaef_IsNoContinuation()
        {
            var lines = new List<string> { "Error: error without source information" };
            lines.AddRange(Enumerable.Range(1, ErrorMessageParser.MaxNrOfContinuationLines).Select(i => $"output {i}"));
            lines.Add("something [File: " + LabCpp + ", Function: F, Line: 10]");
            Parse(lines.ToArray()).ErrorMessage.Should().Be("error without source information");

            Parse("Error: first", "Verify: AreEqual(1, 1)", "x [File: " + LabCpp + ", Function: F, Line: 10]").ErrorStackTrace.Should().BeEmpty();

            // an error glued to the output is an error of its own
            var parser = Parse("Error: first", "outputError: Verify: IsTrue(false) [File: " + LabCpp + ", Function: F, Line: 10]");
            parser.ErrorMessage.Should().Be("#1 - first\n#2 - Verify: IsTrue(false)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_FileNameWithCharactersInvalidInPaths_IsUsedAsLabel()
        {
            // WTT logs (no function) of Log::Error(L"generated failure", L"<generated>", L"", 1) or of a VERIFY after #line 10 "<generated>"
            var parser = Parse("Error: generated failure [File: <generated>, Function: , Line: 1]");

            parser.ErrorMessage.Should().Be("generated failure");
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("<generated>:1", "<generated>", "1"));

            parser = new ErrorMessageParser(new[] { "Error: x [File: a|b\\c<d>.cpp, Function: , Line: 2]" }, @"C:\src\test.cpp");
            parser.Parse();
            parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("c<d>.cpp:2", "a|b\\c<d>.cpp", "2"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetErrors_ReturnsErrorLinesWithContinuationLines()
        {
            IList<string> errors = ErrorMessageParser.GetErrors(new[]
            {
                "comment",
                "Error: single",
                "Error: Verify: AreEqual(expected, actual) - Values (a",
                "b) [File: " + LabCpp + ", Function: F, Line: 10]",
                ErrorMessageParser.CallSiteLine,
                "Error: without source",
                "continuation or comment",
                "TestBlocked: blocked"
            });

            errors.Should().Equal(
                "Error: single",
                "Error: Verify: AreEqual(expected, actual) - Values (a" + Environment.NewLine + "b) [File: " + LabCpp + ", Function: F, Line: 10]",
                "Error: without source");
            ErrorMessageParser.GetErrors(null).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_SourceInformationWithoutFunction_FileNameAndLineAreUsedAsLabel()
        {
            // errors converted from WTT logs have no function
            var parser = Parse("Error: Verify: AreEqual(10, 20) - Values (10, 20) [File: " + LabCpp + ", Function: , Line: 169]");

            parser.ErrorMessage.Should().Be("Verify: AreEqual(10, 20) - Values (10, 20)");
            parser.ErrorStackTrace.Should().Be($"at TaefLab.cpp:169 in {LabCpp}:line 169{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_FileNameContainingCommaAndSpaces_IsParsedCompletely()
        {
            const string file = @"C:\My Tests, Version 2\Some Test.cpp";
            var parser = Parse("Error: Verify: IsTrue(false) [File: " + file + ", Function: Ns::Class::Method, Line: 7]");

            parser.ErrorMessage.Should().Be("Verify: IsTrue(false)");
            parser.ErrorStackTrace.Should().Be($"at Ns::Class::Method in {file}:line 7{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_TemplateAndUnicodeFunctionNames_AreKept()
        {
            const string file = @"C:\src\TAEF-Test-Adapter\SampleTests\Tests\UmlautTests.cpp";
            var parser = Parse("Error: Verify: AreEqual(1, 2): Ümlautß::Täst - Values (1, 2) [File: " + file + ", Function: TaefSamples::Ümlautß::Täst, Line: 16]");

            parser.ErrorMessage.Should().Be("Verify: AreEqual(1, 2): Ümlautß::Täst - Values (1, 2)");
            parser.ErrorStackTrace.Should().Be($"at TaefSamples::Ümlautß::Täst in {file}:line 16{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_RelativeFileName_IsResolvedAgainstSourceFileOfTest()
        {
            using (var directory = new TemporaryDirectory())
            {
                string helperFile = directory.CreateFile(@"project\src\helper.cpp", "// helper");
                string testFile = directory.CreateFile(@"project\src\test.cpp", "// test");

                var parser = new ErrorMessageParser(
                    new[] { @"Error: Verify: IsTrue(false) [File: src\helper.cpp, Function: Helper, Line: 3]" }, testFile);
                parser.Parse();

                parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("Helper", helperFile, "3"));
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_RelativeFileNameWhichCanNotBeResolved_IsKept()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testFile = directory.CreateFile(@"project\src\test.cpp", "// test");

                var parser = new ErrorMessageParser(
                    new[] { @"Error: Verify: IsTrue(false) [File: src\doesnotexist.cpp, Function: Helper, Line: 3]" }, testFile);
                parser.Parse();

                parser.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("Helper", @"src\doesnotexist.cpp", "3"));
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_RelativeFileNameWithoutSourceFileOfTest_IsKept()
        {
            var parser = new ErrorMessageParser(new[] { @"Error: Verify: IsTrue(false) [File: BasicTests.cpp, Function: , Line: 3]" }, null);
            parser.Parse();

            parser.ErrorStackTrace.Should().Be($"at BasicTests.cpp:3 in BasicTests.cpp:line 3{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor_ConsoleOutputString_IsSplitIntoLines()
        {
            var parser = new ErrorMessageParser(
                "before test 1\r\nbefore test 2\n\nError: Verify: AreEqual(1, 2) - Values (1, 2) [File: " + BasicTestsCpp + ", Function: TaefSamples::OutputHandling::ManyLinesWithNewlines, Line: 81]\r\nafter test 1");
            parser.Parse();

            parser.ErrorMessage.Should().Be("Verify: AreEqual(1, 2) - Values (1, 2)");
            parser.ErrorStackTrace.Should().Be($"at TaefSamples::OutputHandling::ManyLinesWithNewlines in {BasicTestsCpp}:line 81{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateStackTraceEntry_ProducesVisualStudioFormat()
        {
            ErrorMessageParser.CreateStackTraceEntry("crash suspect", @"C:\a\b.cpp", "42")
                .Should().Be($@"at crash suspect in C:\a\b.cpp:line 42{Environment.NewLine}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsMessageLine_RecognizesErrorAndResultLines()
        {
            ErrorMessageParser.IsMessageLine("Error: Verify: IsTrue(false)").Should().BeTrue();
            ErrorMessageParser.IsMessageLine("Error: TAEF: No test cases were executed.").Should().BeTrue();
            ErrorMessageParser.IsMessageLine("TestSkipped: Skipped by the test").Should().BeTrue();
            ErrorMessageParser.IsMessageLine("TestBlocked: blocked").Should().BeTrue();
            ErrorMessageParser.IsMessageLine("TestFailed: failed").Should().BeTrue();
            ErrorMessageParser.IsMessageLine("TestNotRun: not run").Should().BeTrue();

            ErrorMessageParser.IsMessageLine(ErrorMessageParser.CallSiteLine).Should().BeFalse();
            ErrorMessageParser.IsMessageLine("Warning: Only a warning - the test passes").Should().BeFalse();
            ErrorMessageParser.IsMessageLine("Verify: AreEqual(20, Add(10, 10))").Should().BeFalse();
            ErrorMessageParser.IsMessageLine("TestPassed: passed").Should().BeFalse();
            ErrorMessageParser.IsMessageLine(" Error: indented").Should().BeFalse();
            ErrorMessageParser.IsMessageLine("").Should().BeFalse();
            ErrorMessageParser.IsMessageLine(null).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetErrorText_ReturnsMessageWithoutPrefixAndSourceInformation()
        {
            ErrorMessageParser.GetErrorText("Error: Verify: AreEqual(1000, Add(10, 10)) - Values (1000, 20) [File: " + BasicTestsCpp + ", Function: TaefSamples::TestMath::AddFails, Line: 22]")
                .Should().Be("Verify: AreEqual(1000, Add(10, 10)) - Values (1000, 20)");
            ErrorMessageParser.GetErrorText("Error: TAEF: No test cases were executed.")
                .Should().Be("TAEF: No test cases were executed.");
            ErrorMessageParser.GetErrorText("Warning: TAEF: something").Should().BeNull();
            ErrorMessageParser.GetErrorText(null).Should().BeNull();
        }

    }

}
