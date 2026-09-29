// This file has been added for TAEF support.

using System;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;

namespace TaefTestAdapter.TestAdapter
{
    /// <summary>
    /// Tests of <see cref="TestExecutor"/> as <see cref="ITestExecutor2"/>: the VsTest framework must not attach the
    /// debugger to its test host process (which runs the adapter), since the tests run in TE.exe, which the adapter
    /// debugs itself with every debugger engine.
    /// </summary>
    [TestClass]
    public class TestExecutorTestHostTests : TestAdapterTestsBase
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutor_ImplementsITestExecutor2()
        {
            typeof(ITestExecutor2).IsAssignableFrom(typeof(TestExecutor)).Should().BeTrue();
        }

        /// <summary>The VsTest framework asks the executor it has created before running any tests.</summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void ShouldAttachToTestHost_ExecutorCreatedByTheVsTestFramework_False()
        {
            MockRunContext.Setup(c => c.IsBeingDebugged).Returns(true);
            ITestExecutor2 executor = new TestExecutor();

            executor.ShouldAttachToTestHost(new[] { TestResources.Tests_DebugX64 }, MockRunContext.Object).Should().BeFalse();
            executor.ShouldAttachToTestHost(new[] { CreateVsTestCase() }, MockRunContext.Object).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ShouldAttachToTestHost_EveryDebuggerEngine_FalseWhetherBeingDebuggedOrNot()
        {
            foreach (DebuggerKind debuggerKind in Enum.GetValues(typeof(DebuggerKind)).Cast<DebuggerKind>())
            {
                foreach (bool isBeingDebugged in new[] { true, false })
                {
                    MockOptions.Setup(o => o.DebuggerKind).Returns(debuggerKind);
                    MockRunContext.Setup(c => c.IsBeingDebugged).Returns(isBeingDebugged);
                    ITestExecutor2 executor = new TestExecutor(TestEnvironment.Logger, TestEnvironment.Options, new Mock<IDebuggerAttacher>().Object);

                    string because = $"{debuggerKind}, being debugged: {isBeingDebugged}";
                    executor.ShouldAttachToTestHost(new[] { TestResources.Tests_DebugX64 }, MockRunContext.Object).Should().BeFalse(because);
                    executor.ShouldAttachToTestHost(new[] { CreateVsTestCase() }, MockRunContext.Object).Should().BeFalse(because);
                }
            }
        }

        private static VsTestCase CreateVsTestCase()
        {
            return new VsTestCase("TaefSamples::Passing::Test", TestExecutor.ExecutorUri, TestResources.Tests_DebugX64);
        }
    }
}
