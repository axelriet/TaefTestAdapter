// This file has been added for TAEF support.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.TestResults;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Runs TE.exe with command lines created by <see cref="CommandLineGenerator"/> for tests of Tests_taef.dll with
    /// special names (quotes, row names containing <c>::</c>, templates, anonymous namespaces, Unicode) and with selections
    /// of the user, and checks that TE.exe runs exactly the selected tests (TE.exe only reads the test DLL, so the shared
    /// sample DLL is used).
    /// </summary>
    [TestClass]
    public class CommandLineGeneratorIntegrationTests : TestsBase
    {
        private static readonly string[] SelectedTests =
        {
            // single tests
            "TaefSamples::TestMath::AddPasses",
            "TaefSamples::NamedRows::SpecialCharacters#with'quote",
            "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]",
            @"TaefSamples::NamedRows::SpecialCharacters#back\slash",
            "TaefSamples::NamedRows::SpecialCharacters#with#hash",
            "TaefSamples::NamedRows::SpecialCharacters#with space",
            "TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前",
            "TaefSamples::TableDataTests::Simple#0",
            "TaefSamples::LightweightDataTests::SingleValue#metadataSet1",
            "TaefSamples::Namespace_1::`anonymous-namespace'::Namespace_Named_Anon::Test",
            "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanDefeatMath",
            // whole classes
            "TaefSamples::ClassWithFixtures::AddFails", "TaefSamples::ClassWithFixtures::AddPasses", "TaefSamples::ClassWithFixtures::AddPassesWithTraits",
            "TaefSamples::ClassWithFixtures::AddPassesWithTraits2", "TaefSamples::ClassWithFixtures::AddPassesWithTraits3", "TaefSamples::ClassWithFixtures::SetupRunsBeforeEachTest",
            "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test",
            "TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate", "TaefSamples::TemplateTests<class std::array<int,3> >::CanDefeatMath",
            "TaefSamples::TemplateTests<class std::array<int,3> >::TwoTraits", "TaefSamples::TemplateTests<class std::array<int,3> >::ThreeTraits",
            "TaefSamples::Nämespace::KlässWithSetüp::Täst", "TaefSamples::Nämespace::KlässWithSetüp::Träits",
            "TaefSamples::Ümlautß::Täst", "TaefSamples::Ümlautß::Träits"
        };

        private List<TestCase> GetSelectedTestCases(string testDll)
        {
            List<TestCase> allTestCases = TestDataCreator.GetTestCasesOfTestDll(testDll);
            allTestCases.Should().HaveCount(TestResources.NrOfTests);
            List<TestCase> testCases = allTestCases.Where(tc => SelectedTests.Contains(tc.FullyQualifiedName)).ToList();
            testCases.Should().HaveCount(SelectedTests.Length);
            return testCases;
        }

        /// <returns>The names of the tests TE.exe has run (StartGroup lines).</returns>
        private List<string> Run(string teExecutable, CommandLineGenerator.Args args, string testDll)
        {
            var output = new List<string>();
            new DotNetProcessExecutor(false, MockLogger.Object).ExecuteCommandBlocking(
                teExecutable, args.CommandLine, Path.GetDirectoryName(testDll), null, new Dictionary<string, string>(), output.Add);

            output.Should().Contain(l => l.StartsWith("Summary: "), string.Join("\n", output));
            output.Should().NotContain(l => l.Contains("Multiple /select or /name options"), "TE.exe ignores all but one selection");
            return output
                .Where(l => l.StartsWith(StreamingTaefOutputParser.StartGroupPrefix))
                .Select(l => l.Substring(StreamingTaefOutputParser.StartGroupPrefix.Length))
                .Where(n => n != TestResources.TestNames.MissingDataSource) // always run by TE.exe
                .ToList();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetCommandLines_TestsWithSpecialNames_TeRunsExactlyTheSelectedTests()
        {
            string testDll = TestResources.Tests_DebugX64;
            string teExecutable = TestResources.GetTeExecutable(SampleConfiguration.DebugX64);
            List<TestCase> testCases = GetSelectedTestCases(testDll);

            CommandLineGenerator.Args args = new CommandLineGenerator(testCases, testDll, teExecutable.Length, "", false, MockOptions.Object)
                .GetCommandLines().Single();

            args.CommandLine.Should().Contain("(@Name='TaefSamples::ClassWithFixtures::*' and not @Name='TaefSamples::ClassWithFixtures::*::*')");
            args.CommandLine.Should().Contain("@Name='TaefSamples::NamedRows::SpecialCharacters#with''quote'");
            args.CommandLine.Should().Contain("(@Name='TaefSamples::`anonymous-namespace''::Namespace_Anon::*' and not @Name='TaefSamples::`anonymous-namespace''::Namespace_Anon::*::*')");
            Run(teExecutable, args, testDll).Should().BeEquivalentTo(SelectedTests);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetCommandLines_SplitCommandLines_EachTeInvocationRunsItsShareOfTests()
        {
            string testDll = TestResources.Tests_DebugX86;
            string teExecutable = TestResources.GetTeExecutable(SampleConfiguration.DebugX86);
            List<TestCase> testCases = GetSelectedTestCases(testDll);
            // pretend TE.exe had a very long path to force splitting
            int lengthOfTeExecutable = CommandLineGenerator.MaxCommandLength - 700;

            List<CommandLineGenerator.Args> commandLines = new CommandLineGenerator(testCases, testDll, lengthOfTeExecutable, "", false, MockOptions.Object)
                .GetCommandLines().ToList();

            commandLines.Should().HaveCountGreaterThan(2);
            var testsRun = new List<string>();
            foreach (CommandLineGenerator.Args args in commandLines)
            {
                List<string> testsRunByInvocation = Run(teExecutable, args, testDll);
                testsRunByInvocation.Should().BeEquivalentTo(args.TestCases.Select(tc => tc.FullyQualifiedName));
                testsRun.AddRange(testsRunByInvocation);
            }
            testsRun.Should().BeEquivalentTo(SelectedTests);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetCommandLines_UserSelectionAndAllDiscoveredTestsOfClass_TeRunsOnlyTestsSelectedByUser()
        {
            string testDll = TestResources.Tests_DebugX64;
            string teExecutable = TestResources.GetTeExecutable(SampleConfiguration.DebugX64);
            string[] expectedTests = { TestResources.TestNames.TestMathAddPasses, TestResources.TestNames.TestMathAddPassesWithTraits };

            // selections restricting discovery to the passing tests of class TestMath (TaefSamples::TestMath::AddFails is not discovered)
            foreach (string userParameters in new[]
            {
                $"/select:\"not @Name='{TestResources.TestNames.TestMathAddFails}'\"",
                "/p:\"A=B\" /name:TaefSamples::TestMath::AddPass*"
            })
            {
                MockOptions.Setup(o => o.AdditionalTestExecutionParam).Returns(userParameters);
                List<TestCase> testCases = new TaefDiscoverer(MockLogger.Object, MockOptions.Object).GetTestsFromTestDll(testDll)
                    .Where(tc => tc.FullyQualifiedName.StartsWith("TaefSamples::TestMath::"))
                    .ToList();
                testCases.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(expectedTests, userParameters);

                CommandLineGenerator.Args args = new CommandLineGenerator(testCases, testDll, teExecutable.Length, userParameters, false, MockOptions.Object)
                    .GetCommandLines().Single();

                // the class term alone would also select TaefSamples::TestMath::AddFails
                args.CommandLine.Should().Contain("(@Name='TaefSamples::TestMath::*' and not @Name='TaefSamples::TestMath::*::*')");
                Run(teExecutable, args, testDll).Should().BeEquivalentTo(expectedTests, args.CommandLine);
            }
        }

    }

}
