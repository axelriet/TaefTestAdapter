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

namespace TaefTestAdapter.Runners
{
    [TestClass]
    public class ManagedSelectionTests : TestsBase
    {
        private const string TestDll = @"C:\tests\ManagedTests.dll";
        private const string BaseCommand = "\"" + TestDll + "\" /unicodeOutput:false /coloredConsoleOutput:false";

        private static TestCase Test(string name, string listedClass, int classCount, int dllCount)
        {
            var test = new TestCase(name, TestDll, name, "", 0);
            test.Properties.Add(new TestCaseTaefClassProperty(listedClass));
            test.Properties.Add(new TestCaseMetaDataProperty(classCount, dllCount));
            return test;
        }

        private IList<CommandLineGenerator.Args> Generate(params TestCase[] tests)
        {
            return new CommandLineGenerator(tests, TestDll, 20, "", false, MockOptions.Object)
                .GetCommandLines().ToList();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Selection_CompleteManagedClassUsesDotsAndAddsRowsWithDotsByName()
        {
            const string listedClass = "Ns.Data#a.b c::d#e";
            TestCase first = Test(listedClass + ".M", listedClass, 2, 3);
            TestCase row = Test(listedClass + ".N#x.y::z", listedClass, 2, 3);

            CommandLineGenerator.Args args = Generate(first, row).Single();

            args.CommandLine.Should().Be(BaseCommand + " /select:\""
                + "(@Name='" + listedClass + ".*' and not @Name='" + listedClass + ".*.*')"
                + " or @Name='" + listedClass + ".N#x.y::z'\"");
            args.TestCases.Should().Equal(first, row);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Selection_OneManagedDataRowKeepsExactNameAndNarrowsWildcardWithIndex()
        {
            TestCase row = Test("Ns.C#class.row.M#a*\"b", "Ns.C#class.row", 2, 3);
            row.Properties.Add(new TestCaseDataRowIndexProperty(7));

            string command = Generate(row).Single().CommandLine;

            command.Should().Be(BaseCommand + " /select:\"(@Name='Ns.C#class.row.M#a??b' and @Data:Index=7)\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Selection_AllManagedTestsOfDllKeepExistingNoSelectionOptimization()
        {
            TestCase test = Test("Global.M#row.with::colons", "Global", 1, 1);

            Generate(test).Single().CommandLine.Should().Be(BaseCommand);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Selection_ManagedDataSourceErrorIsSelectedByExactPseudoTestName()
        {
            TestCase test = Test("Ns.C#error", "Ns.C#error", 1, 2);

            Generate(test).Single().CommandLine.Should().Be(BaseCommand + " /select:\"@Name='Ns.C#error'\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Selection_ManagedExecutionGroupRemainsAtomicAcrossCommandLineSplits()
        {
            TestCase first = Test("Ns.C.First", "Ns.C", 3, 10);
            TestCase second = Test("Ns.C.Second", "Ns.C", 3, 10);
            first.Traits.Add(new Trait("ExecutionGroup", "Scenario"));
            second.Traits.Add(new Trait("ExecutionGroup", "Scenario"));
            TestCase independent = Test("Ns.Other.IndependentTest", "Ns.Other", 2, 10);

            List<CommandLineGenerator.Args> args = GenerateWithQueryCapacity(65, first, second, independent);

            args.Should().HaveCount(2);
            args[0].TestCases.Should().Equal(first, second);
            args[0].CommandLine.Should().Be(BaseCommand + " /select:\"(@Name='Ns.C.First' or @Name='Ns.C.Second')\"");
            args[1].TestCases.Should().Equal(independent);
            args.SelectMany(arg => arg.TestCases).Should().Equal(first, second, independent);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Selection_ManagedGroupTooLongForOneCommandReportsActionableFailure()
        {
            TestCase first = Test("Ns.C.First", "Ns.C", 3, 10);
            TestCase second = Test("Ns.C.Second", "Ns.C", 3, 10);
            first.Traits.Add(new Trait("ExecutionGroup", "Scenario"));
            second.Traits.Add(new Trait("ExecutionGroup", "Scenario"));

            new Action(() => GenerateWithQueryCapacity(25, first, second)).Should().Throw<InvalidOperationException>()
                .WithMessage("*execution groups*Ns.C*cannot fit*prerequisites*");
            MockLogger.Verify(l => l.LogError(It.Is<string>(message => message.Contains("cannot fit in one TE.exe command line"))), Times.Once);
        }

        private List<CommandLineGenerator.Args> GenerateWithQueryCapacity(int queryCapacity, params TestCase[] tests)
        {
            int fixedLength = 3 + BaseCommand.Length + " /select:\"".Length + "\"".Length;
            return new CommandLineGenerator(tests, TestDll, CommandLineGenerator.MaxCommandLength - fixedLength - queryCapacity,
                "", false, MockOptions.Object, logger: MockLogger.Object).GetCommandLines().ToList();
        }
    }
}
