// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Model;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Scheduling
{
    [TestClass]
    public class ManagedExecutionGroupTests : TestsBase
    {
        private static TestCase Test(string name, string listedClass, string group = "Scenario", string source = @"C:\tests\Managed.dll")
        {
            var test = new TestCase(name, source, name, "", 0);
            test.Properties.Add(new TestCaseTaefClassProperty(listedClass));
            if (group != null)
                test.Traits.Add(new Trait("ExecutionGroup", group));
            return test;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void NumberSplitter_KeepsManagedGroupInOneWorkerAndPreservesMemberOrder()
        {
            var tests = new[]
            {
                Test("Ns.C#row.a.First", "Ns.C#row.a"), Test("Ns.C#row.a.Second", "Ns.C#row.a"),
                Test("Ns.C#row.b.First", "Ns.C#row.b"), Test("Ns.C#row.b.Second", "Ns.C#row.b"),
                Test("Ns.Other.First", "Ns.Other")
            };
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(3);

            List<List<TestCase>> result = new NumberBasedTestsSplitter(tests, MockOptions.Object).SplitTestcases();

            result.Should().HaveCount(3);
            result[0].Should().Equal(tests.Take(2));
            result[1].Should().Equal(tests.Skip(2).Take(2));
            result[2].Should().Equal(tests.Skip(4));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DurationSplitter_UsesWholeGroupDurationAndRetainsListingOrder()
        {
            TestCase first = Test("Ns.C.First", "Ns.C");
            TestCase second = Test("Ns.C.Second", "Ns.C");
            first.Properties.Add(new TestCaseExecutionGroupProperty("Raw group"));
            second.Properties.Add(new TestCaseExecutionGroupProperty("Raw group"));
            first.Traits.Clear();
            second.Traits.Clear();
            TestCase independent = Test("Ns.Other.M", "Ns.Other", group: null);
            var durations = new Dictionary<TestCase, int> { { first, 1 }, { second, 100 }, { independent, 50 } };
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);

            List<List<TestCase>> result = new DurationBasedTestsSplitter(durations, MockOptions.Object).SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().Equal(first, second);
            result[1].Should().Equal(independent);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ExecutionGroups_AreScopedBySourceAndExactListedClass()
        {
            var tests = new[]
            {
                Test("Ns.C.M", "Ns.C", source: @"C:\tests\First.dll"),
                Test("Ns.C.M", "Ns.C", source: @"C:\tests\Second.dll"),
                Test("Ns.Other.M", "Ns.Other", source: @"C:\tests\First.dll")
            };
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(3);

            List<List<TestCase>> numbered = new NumberBasedTestsSplitter(tests, MockOptions.Object).SplitTestcases();
            List<List<TestCase>> timed = new DurationBasedTestsSplitter(tests.ToDictionary(test => test, _ => 10), MockOptions.Object)
                .SplitTestcases();

            numbered.Should().HaveCount(3).And.OnlyContain(worker => worker.Count == 1);
            timed.Should().HaveCount(3).And.OnlyContain(worker => worker.Count == 1);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void NativeExecutionGroupTraitsRetainExistingRoundRobinScheduling()
        {
            TestCase first = TestDataCreator.ToTestCase("Native::C::First");
            TestCase second = TestDataCreator.ToTestCase("Native::C::Second");
            first.Traits.Add(new Trait("ExecutionGroup", "Scenario"));
            second.Traits.Add(new Trait("ExecutionGroup", "Scenario"));
            MockOptions.Setup(o => o.MaxNrOfThreads).Returns(2);

            List<List<TestCase>> result = new NumberBasedTestsSplitter(new[] { first, second }, MockOptions.Object).SplitTestcases();

            result.Should().HaveCount(2);
            result[0].Should().Equal(first);
            result[1].Should().Equal(second);
        }
    }
}
