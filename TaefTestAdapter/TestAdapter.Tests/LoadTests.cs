// This file has been added for TAEF support.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Runners;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestNames = TaefTestAdapter.Tests.Common.TestResources.TestNames;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Discovers and runs the 5000 tests of LoadTests_taef.dll (<c>TaefSamples::LoadTests::Test0</c> ... <c>TaefSamples::LoadTests::Test4999</c>,
    /// tests with odd numbers pass) through the VsTest framework integration.
    /// </summary>
    [TestClass]
    public class LoadTests : TestAdapterTestsBase
    {
        /// <summary>Generous upper bound for discovering or running the load tests (a few seconds on a developer machine).</summary>
        private const int MaxDurationInMs = 60000;

        private readonly Mock<IDebuggerAttacher> _mockDebuggerAttacher = new Mock<IDebuggerAttacher>();

        private TestExecutor CreateExecutor() => new TestExecutor(TestEnvironment.Logger, TestEnvironment.Options, _mockDebuggerAttacher.Object);

        [TestMethod]
        [TestCategory(Load)]
        public void DiscoverTests_LoadTests_AllTestsAreFound()
        {
            string testDll = CopySample(TestResources.LoadTests_ReleaseX64).TestDll;
            var mockDiscoverySink = new Mock<ITestCaseDiscoverySink>();
            var discoveredTestCases = new List<VsTestCase>();
            mockDiscoverySink.Setup(s => s.SendTestCase(It.IsAny<VsTestCase>())).Callback((VsTestCase tc) => { lock (discoveredTestCases) discoveredTestCases.Add(tc); });

            Stopwatch stopwatch = Stopwatch.StartNew();
            new TestDiscoverer(TestEnvironment.Logger, TestEnvironment.Options)
                .DiscoverTests(testDll.Yield(), new Mock<IDiscoveryContext>().Object, MockVsLogger.Object, mockDiscoverySink.Object);
            stopwatch.Stop();

            discoveredTestCases.Should().HaveCount(TestResources.NrOfLoadTests);
            discoveredTestCases.Select(tc => tc.DisplayName).Should().OnlyHaveUniqueItems();
            discoveredTestCases.Should().Contain(tc => tc.DisplayName == TestNames.LoadTest(4999) && tc.FullyQualifiedName == "TaefSamples.LoadTests.Test4999" && tc.LineNumber > 0);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(MaxDurationInMs);
        }

        [TestMethod]
        [TestCategory(Load)]
        public void RunTests_AllLoadTests_CorrectResultsWithOneTeInvocation()
        {
            string testDll = CopySample(TestResources.LoadTests_ReleaseX64).TestDll;

            Stopwatch stopwatch = Stopwatch.StartNew();
            CreateExecutor().RunTests(testDll.Yield(), MockRunContext.Object, MockFrameworkHandle.Object);
            stopwatch.Stop();

            var results = GetRecordedResults();
            results.Count(r => r.Outcome == VsTestOutcome.Passed).Should().Be(TestResources.NrOfPassingLoadTests);
            results.Count(r => r.Outcome == VsTestOutcome.Failed).Should().Be(TestResources.NrOfFailingLoadTests);
            results.Should().HaveCount(TestResources.NrOfLoadTests);
            // all tests of the DLL are run: no /select
            MockLogger.Verify(l => l.VerboseInfo(It.Is<string>(s => s.StartsWith("Executing \"") && !s.Contains(TaefConstants.SelectOption))), Times.Once);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(MaxDurationInMs);
        }

        [TestMethod]
        [TestCategory(Load)]
        public void RunTests_ManyLoadTests_SelectionIsSplitIntoSeveralCommandLines()
        {
            string testDll = CopySample(TestResources.LoadTests_ReleaseX86).TestDll;
            List<VsTestCase> testCases = TestDataCreator.GetTestCasesOfTestDll(testDll)
                .Where(tc => int.Parse(tc.FullyQualifiedName.Substring("TaefSamples::LoadTests::Test".Length)) < 3000)
                .Select(tc => tc.ToVsTestCase())
                .ToList();
            testCases.Should().HaveCount(3000);

            CreateExecutor().RunTests(testCases, MockRunContext.Object, MockFrameworkHandle.Object);

            var results = GetRecordedResults();
            results.Should().HaveCount(3000);
            results.Select(r => r.TestCase.DisplayName).Should().OnlyHaveUniqueItems();
            results.Count(r => r.Outcome == VsTestOutcome.Passed).Should().Be(1500);
            results.Count(r => r.Outcome == VsTestOutcome.Failed).Should().Be(1500);
            results.Single(r => r.TestCase.DisplayName == TestNames.LoadTest(2999)).Outcome.Should().Be(VsTestOutcome.Passed);

            // each command line (quoted TE.exe path, blank, arguments) must not exceed the maximum length
            List<string> executions = MockLogger.Invocations
                .Where(i => i.Method.Name == nameof(ILogger.VerboseInfo))
                .Select(i => (string)i.Arguments[0])
                .Where(s => s.StartsWith("Executing \""))
                .ToList();
            executions.Should().HaveCountGreaterThan(1);
            executions.Should().OnlyContain(s => s.Contains(TaefConstants.SelectOption));
            executions.Should().OnlyContain(s => s.Length - "Executing ".Length <= CommandLineGenerator.MaxCommandLength);
        }

    }
}
