// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Model;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestResults
{
    [TestClass]
    public class ManagedTaefOutputParserTests : TestsBase
    {
        private const string TestDll = @"C:\tests\ManagedTests.dll";

        private static TestCase Test(string name, string listedClass)
        {
            var test = new TestCase(name, TestDll, name, @"C:\src\ManagedTests.cs", 20);
            test.Properties.Add(new TestCaseTaefClassProperty(listedClass));
            return test;
        }

        private IList<TestResult> Parse(IList<TestCase> tests, params string[] lines)
        {
            var parser = new StreamingTaefOutputParser(tests, MockLogger.Object, MockFrameworkReporter.Object);
            foreach (string line in lines)
                parser.ReportLine(line);
            parser.Flush();

            IList<TestResult> nonStreaming = new TaefOutputParser(tests, lines, MockLogger.Object).GetTestResults();
            nonStreaming.Select(r => r.Outcome).Should().Equal(parser.TestResults.Select(r => r.Outcome));
            nonStreaming.Select(r => r.ErrorMessage).Should().Equal(parser.TestResults.Select(r => r.ErrorMessage));
            nonStreaming.Select(r => r.ErrorStackTrace).Should().Equal(parser.TestResults.Select(r => r.ErrorStackTrace));
            nonStreaming.Select(r => r.Output).Should().Equal(parser.TestResults.Select(r => r.Output));
            return parser.TestResults;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ManagedExceptions_PreserveNetFrameworkAndPortablePdbStackFrames()
        {
            var tests = new[] { Test("Ns.C.NetFramework", "Ns.C"), Test("Ns.C.Modern", "Ns.C") };

            IList<TestResult> results = Parse(tests,
                "StartGroup: Ns.C.NetFramework",
                "Error: System.InvalidOperationException: boom",
                "   at Ns.C.ThrowHelper()",
                "   at Ns.C.NetFramework()",
                "EndGroup: Ns.C.NetFramework [Failed]",
                "StartGroup: Ns.C.Modern",
                "Error: System.InvalidOperationException: async boom",
                @"   at Ns.C.ThrowAsync() in C:\src\ManagedTests.cs:line 31",
                "--- End of stack trace from previous location ---",
                @"   at Ns.C.Modern() in C:\src\ManagedTests.cs:line 45",
                "ordinary output after exception",
                "EndGroup: Ns.C.Modern [Failed]");

            results[0].Outcome.Should().Be(TestOutcome.Failed);
            results[0].ErrorMessage.Should().Be("System.InvalidOperationException: boom");
            results[0].ErrorStackTrace.Should().Be("at Ns.C.ThrowHelper()" + Environment.NewLine
                + "at Ns.C.NetFramework()" + Environment.NewLine);
            results[1].ErrorStackTrace.Should().Be(
                ErrorMessageParser.CreateStackTraceEntry("Ns.C.ThrowAsync()", @"C:\src\ManagedTests.cs", "31")
                + "--- End of stack trace from previous location ---" + Environment.NewLine
                + ErrorMessageParser.CreateStackTraceEntry("Ns.C.Modern()", @"C:\src\ManagedTests.cs", "45"));
            results[1].Output.Should().Contain("ordinary output after exception");
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ManagedClassRowFixtureFailureStaysWithinTheListedClassRow()
        {
            const string listedClass = "Ns.Data#a.b :: c#d";
            var tests = new[]
            {
                Test(listedClass + ".M#x.y", listedClass), Test(listedClass + ".N", listedClass),
                Test(listedClass + "#extra.M", listedClass + "#extra"), Test("Ns.Other.M", "Ns.Other")
            };

            IList<TestResult> results = Parse(tests,
                "Error: System.InvalidOperationException: setup failed",
                @"   at Ns.Data.Init() in C:\src\ManagedTests.cs:line 12",
                "TestBlocked: TAEF: Setup fixture 'Init' for the scope '" + listedClass + "' returned 'false'.",
                "StartGroup: " + tests[0].FullyQualifiedName,
                "EndGroup: " + tests[0].FullyQualifiedName + " [Blocked]",
                "StartGroup: " + tests[1].FullyQualifiedName,
                "EndGroup: " + tests[1].FullyQualifiedName + " [Blocked]",
                "StartGroup: " + tests[2].FullyQualifiedName, "EndGroup: " + tests[2].FullyQualifiedName + " [Passed]",
                "StartGroup: Ns.Other.M", "EndGroup: Ns.Other.M [Passed]");

            results.Take(2).Should().OnlyContain(r => r.Outcome == TestOutcome.Failed
                && r.ErrorMessage.Contains("setup failed") && r.ErrorStackTrace.Contains("ManagedTests.cs:line 12"));
            results.Skip(2).Should().OnlyContain(r => r.Outcome == TestOutcome.Passed && r.ErrorMessage == null);
            results.Skip(2).Should().OnlyContain(r => r.Output == null || !r.Output.Contains("setup failed"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ManagedClassLoadFailureExplainsBlockedTestsWithoutAffectingAdjacentClass()
        {
            var tests = new[] { Test("Ns.Generic`1.M", "Ns.Generic`1"), Test("Ns.Other.M", "Ns.Other") };
            const string error = "Error: TAEF: [HRESULT: 0x80070057] A failure occurred while preparing to run tests in class 'Ns.Generic`1'. (Generic test classes are unsupported.)";

            IList<TestResult> results = Parse(tests, error,
                "StartGroup: Ns.Generic`1.M", "EndGroup: Ns.Generic`1.M [Blocked]",
                "StartGroup: Ns.Other.M", "EndGroup: Ns.Other.M [Passed]");

            results[0].ErrorMessage.Should().Contain("Generic test classes are unsupported");
            results[1].ErrorMessage.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BareManagedOperationFailureIsLoggedWithoutInventingItsClass()
        {
            var tests = new[] { Test("Ns.A.M", "Ns.A"), Test("Ns.B.M", "Ns.B") };
            const string error = "Error: TAEF: [HRESULT 0x80131509] A failure occurred while running a test operation: 'Init'.";

            IList<TestResult> results = Parse(tests, error,
                "StartGroup: Ns.A.M", "EndGroup: Ns.A.M [Passed]",
                "StartGroup: Ns.B.M", "EndGroup: Ns.B.M [Blocked]");

            results[0].Outcome.Should().Be(TestOutcome.Passed);
            results[1].ErrorMessage.Should().Be("Blocked: no reason has been reported by TE.exe");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("test operation: 'Init'"))), Times.Exactly(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BareManagedHostLoss_ContiguousBlockedTestsUseLatestFailureInTheirListedClass()
        {
            // Captured from net48 and net10 TAEF runs: ClassInitialize calls Environment.Exit(37),
            // TestInitialize calls Environment.Exit(38), and an adjacent control class runs normally.
            var tests = new[]
            {
                Test("Negative.Managed.ClassExit.First", "Negative.Managed.ClassExit"),
                Test("Negative.Managed.ClassExit.Second", "Negative.Managed.ClassExit"),
                Test("Negative.Managed.MethodExit.First", "Negative.Managed.MethodExit"),
                Test("Negative.Managed.MethodExit.Second", "Negative.Managed.MethodExit"),
                Test("Negative.Managed.Control.Passing", "Negative.Managed.Control")
            };
            const string classFailure = "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Init'. (The test host process was unexpectedly terminated with exit code 0x00000025 while invoking a test operation. (An RPC call failed.))";
            const string methodFailure = "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Init'. (The test host process was unexpectedly terminated with exit code 0x00000026 while invoking a test operation. (An RPC call failed.))";

            IList<TestResult> results = Parse(tests,
                classFailure,
                "StartGroup: Negative.Managed.ClassExit.First", "EndGroup: Negative.Managed.ClassExit.First [Blocked]",
                "StartGroup: Negative.Managed.ClassExit.Second", "EndGroup: Negative.Managed.ClassExit.Second [Blocked]",
                methodFailure,
                "StartGroup: Negative.Managed.MethodExit.First", "EndGroup: Negative.Managed.MethodExit.First [Blocked]",
                methodFailure,
                "StartGroup: Negative.Managed.MethodExit.Second", "EndGroup: Negative.Managed.MethodExit.Second [Blocked]",
                "StartGroup: Negative.Managed.Control.Passing", "EndGroup: Negative.Managed.Control.Passing [Passed]");

            results.Take(2).Should().OnlyContain(r => r.Outcome == TestOutcome.Failed && r.ErrorMessage.Contains("0x00000025"));
            results.Skip(2).Take(2).Should().OnlyContain(r => r.Outcome == TestOutcome.Failed
                && r.ErrorMessage.Contains("0x00000026") && !r.ErrorMessage.Contains("0x00000025"));
            results[4].Outcome.Should().Be(TestOutcome.Passed);
            results[4].ErrorMessage.Should().BeNull();
            results[4].Output.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BareManagedCleanupHostLoss_InterveningPassedTestDiscardsUnboundFailure()
        {
            var tests = new[]
            {
                Test("Ns.Cleanup.M", "Ns.Cleanup"), Test("Ns.Passing.M", "Ns.Passing"), Test("Ns.Blocked.M", "Ns.Blocked")
            };

            IList<TestResult> results = Parse(tests,
                "StartGroup: Ns.Cleanup.M", "EndGroup: Ns.Cleanup.M [Passed]",
                "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Cleanup'. (The test host process was unexpectedly terminated with exit code 0x00000027 while invoking a test operation. (An RPC call failed.))",
                "StartGroup: Ns.Passing.M", "EndGroup: Ns.Passing.M [Passed]",
                "StartGroup: Ns.Blocked.M", "EndGroup: Ns.Blocked.M [Blocked]");

            results.Take(2).Should().OnlyContain(r => r.Outcome == TestOutcome.Passed && r.ErrorMessage == null && r.Output == null);
            results[2].ErrorMessage.Should().Be("Blocked: no reason has been reported by TE.exe");
            results[2].Output.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void BareManagedHostLoss_DifferentClassOrFirstTestsOwnReasonCannotBindScope()
        {
            var tests = new[]
            {
                Test("Ns.A.First", "Ns.A"), Test("Ns.B.First", "Ns.B"),
                Test("Ns.C.First", "Ns.C"), Test("Ns.C.Second", "Ns.C")
            };
            const string error = "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Init'. (The test host process was unexpectedly terminated with exit code 0x00000025 while invoking a test operation. (An RPC call failed.))";

            IList<TestResult> results = Parse(tests,
                error, "StartGroup: Ns.A.First", "EndGroup: Ns.A.First [Blocked]",
                "StartGroup: Ns.B.First", "EndGroup: Ns.B.First [Blocked]",
                error, "StartGroup: Ns.C.First", "TestBlocked: explicit reason", "EndGroup: Ns.C.First [Blocked]",
                "StartGroup: Ns.C.Second", "EndGroup: Ns.C.Second [Blocked]");

            results[0].ErrorMessage.Should().Contain("0x00000025");
            results[1].ErrorMessage.Should().Be("Blocked: no reason has been reported by TE.exe");
            results[2].ErrorMessage.Should().Be("Blocked: explicit reason");
            results[3].ErrorMessage.Should().Be("Blocked: no reason has been reported by TE.exe");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void QualifiedManagedModuleHostLoss_ExplainsBlockedTestsAcrossClasses()
        {
            // Captured from net48 and net10: AssemblyInitialize terminates its host with Environment.Exit(40).
            var tests = new[]
            {
                Test("Negative.Managed.AssemblyInitExit.First", "Negative.Managed.AssemblyInitExit"),
                Test("Negative.Managed.AssemblyInitExit.Second", "Negative.Managed.AssemblyInitExit"),
                Test("Negative.Managed.Control.Passing", "Negative.Managed.Control")
            };
            foreach (TestCase test in tests)
                test.Properties.Add(new TestCaseModuleFixturesProperty(new[] { "Negative.Managed.AssemblyInitExit.Init" }));

            IList<TestResult> results = Parse(tests,
                "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Negative.Managed.AssemblyInitExit.Init'. (The test host process was unexpectedly terminated with exit code 0x00000028 while invoking a test operation. (An RPC call failed.))",
                "StartGroup: Negative.Managed.AssemblyInitExit.First", "EndGroup: Negative.Managed.AssemblyInitExit.First [Blocked]",
                "StartGroup: Negative.Managed.AssemblyInitExit.Second", "EndGroup: Negative.Managed.AssemblyInitExit.Second [Blocked]",
                "StartGroup: Negative.Managed.Control.Passing", "EndGroup: Negative.Managed.Control.Passing [Blocked]");

            results.Should().HaveCount(3).And.OnlyContain(result => result.Outcome == TestOutcome.Failed
                && result.ErrorMessage.Contains("0x00000028"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void QualifiedManagedClassOrTestOperationsDoNotAcquireModuleScope()
        {
            var tests = new[] { Test("Ns.A.First", "Ns.A"), Test("Ns.B.First", "Ns.B") };
            foreach (TestCase test in tests)
                test.Properties.Add(new TestCaseModuleFixturesProperty(new[] { "Ns.Module.Init" }));

            foreach (string operation in new[] { "Ns.A.Init", "Ns.A.First" })
            {
                IList<TestResult> results = Parse(tests,
                    "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: '" + operation + "'. (The test host process was unexpectedly terminated with exit code 0x00000025 while invoking a test operation. (An RPC call failed.))",
                    "StartGroup: Ns.A.First", "EndGroup: Ns.A.First [Blocked]",
                    "StartGroup: Ns.B.First", "EndGroup: Ns.B.First [Blocked]");

                results[0].ErrorMessage.Should().Contain("0x00000025");
                results[1].ErrorMessage.Should().Be("Blocked: no reason has been reported by TE.exe");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void QualifiedManagedModuleCleanupHostLossPreservesReportedResults()
        {
            var tests = new[] { Test("Ns.A.First", "Ns.A"), Test("Ns.B.First", "Ns.B") };
            foreach (TestCase test in tests)
                test.Properties.Add(new TestCaseModuleFixturesProperty(new[] { "Ns.A.Cleanup" }));

            IList<TestResult> results = Parse(tests,
                "StartGroup: Ns.A.First", "EndGroup: Ns.A.First [Passed]",
                "StartGroup: Ns.B.First", "EndGroup: Ns.B.First [Passed]",
                "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Ns.A.Cleanup'. (The test host process was unexpectedly terminated with exit code 0x00000029 while invoking a test operation. (An RPC call failed.))",
                "Summary: Total=2, Passed=2, Failed=0, Blocked=0, Not Run=0, Skipped=0");

            results.Should().HaveCount(2).And.OnlyContain(result => result.Outcome == TestOutcome.Passed && result.ErrorMessage == null);
            MockLogger.Verify(logger => logger.LogWarning(It.Is<string>(message => message.Contains("0x00000029"))), Times.Exactly(2));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ManagedQualifiedCleanupHostLoss_InterveningPassedTestExpiresFallback()
        {
            var tests = new[]
            {
                Test("Ns.C.CleanupTest", "Ns.C"), Test("Ns.C.Passing", "Ns.C"), Test("Ns.C.Blocked", "Ns.C")
            };
            foreach (TestCase test in tests)
                test.Properties.Add(new TestCaseModuleFixturesProperty(new[] { "Ns.Module.Cleanup" }));

            foreach (string operation in new[] { "Ns.Module.Cleanup", "Ns.C.Cleanup" })
            {
                IList<TestResult> results = Parse(tests,
                    "StartGroup: Ns.C.CleanupTest", "EndGroup: Ns.C.CleanupTest [Passed]",
                    "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: '" + operation + "'. (The test host process was unexpectedly terminated with exit code 0x00000029 while invoking a test operation. (An RPC call failed.))",
                    "StartGroup: Ns.C.Passing", "EndGroup: Ns.C.Passing [Passed]",
                    "StartGroup: Ns.C.Blocked", "EndGroup: Ns.C.Blocked [Blocked]");

                results.Take(2).Should().OnlyContain(result => result.Outcome == TestOutcome.Passed
                    && result.ErrorMessage == null && result.Output == null);
                results[2].ErrorMessage.Should().Be("Blocked: no reason has been reported by TE.exe");
                results[2].Output.Should().BeNull();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ManagedHostTransition_NewerBareFixtureFailureSupersedesModuleCleanup()
        {
            // With separate hosts for later classes, a module cleanup failure can precede a new class setup failure.
            var tests = new[]
            {
                Test("Ns.Cleanup.M", "Ns.Cleanup"), Test("Ns.Blocked.First", "Ns.Blocked"), Test("Ns.Blocked.Second", "Ns.Blocked")
            };
            foreach (TestCase test in tests)
                test.Properties.Add(new TestCaseModuleFixturesProperty(new[] { "Ns.Module.Cleanup" }));

            IList<TestResult> results = Parse(tests,
                "StartGroup: Ns.Cleanup.M", "EndGroup: Ns.Cleanup.M [Passed]",
                "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Ns.Module.Cleanup'. (The test host process was unexpectedly terminated with exit code 0x00000029 while invoking a test operation. (An RPC call failed.))",
                "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'Init'. (The test host process was unexpectedly terminated with exit code 0x00000025 while invoking a test operation. (An RPC call failed.))",
                "StartGroup: Ns.Blocked.First", "EndGroup: Ns.Blocked.First [Blocked]",
                "StartGroup: Ns.Blocked.Second", "EndGroup: Ns.Blocked.Second [Blocked]");

            results[0].Outcome.Should().Be(TestOutcome.Passed);
            results.Skip(1).Should().OnlyContain(result => result.Outcome == TestOutcome.Failed
                && result.ErrorMessage.Contains("0x00000025") && !result.ErrorMessage.Contains("0x00000029"));
            results.Skip(1).Should().OnlyContain(result => !result.Output.Contains("0x00000029"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExceptionFramesRequireManagedKindAndDoNotConsumeUnrelatedOutput()
        {
            string[] lines =
            {
                "Error: System.Exception: outer",
                " ---> System.ArgumentException: inner",
                @"   at Ns.C.Helper() in C:\src\Helper.cs:line 9",
                "   --- End of inner exception stack trace ---",
                "   at Ns.C.M()",
                "ordinary comment",
                "   at this is ordinary output after a comment"
            };
            var managed = new ErrorMessageParser(lines, isManaged: true);
            managed.Parse();
            managed.NrOfMessages.Should().Be(1);
            managed.ErrorStackTrace.Should().Contain("System.ArgumentException: inner");
            managed.ErrorStackTrace.Should().Contain("Helper.cs:line 9");
            managed.ErrorStackTrace.Should().NotContain("ordinary output");
            ErrorMessageParser.GetErrors(lines, isManaged: true).Single().Should().EndWith("   at Ns.C.M()");

            var native = new ErrorMessageParser(lines);
            native.Parse();
            native.ErrorStackTrace.Should().BeEmpty();
            ErrorMessageParser.GetErrors(lines).Should().Equal("Error: System.Exception: outer");
        }
    }
}
