// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using FluentAssertions;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.ServiceModel;
using System.Threading;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Debugging
{
    /// <summary>
    /// Tests of the adapter's client of the debugger attacher service (<see cref="MessageBasedDebuggerAttacher"/>) against
    /// the service as hosted by the VS package (<see cref="DebuggerAttacherServiceHost"/>, hosted in the test process).
    /// </summary>
    [TestClass]
    public class MessageBasedDebuggerAttacherTests
    {
        /// <summary>
        /// Timeout of the client: an upper bound only (attaching usually takes a few milliseconds), chosen generously since
        /// the first WCF call of a process can take seconds on a busy machine.
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private const int DebuggeeProcessId = 2017;

        private Mock<IDebuggerAttacher> MockDebuggerAttacher { get; } = new Mock<IDebuggerAttacher>();
        private Mock<ILogger> MockLogger { get; } = new Mock<ILogger>();

        [TestInitialize]
        public void Setup()
        {
            MockDebuggerAttacher.Reset();
            MockLogger.Reset();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void AttachDebugger_AttachingSucceeds_DebugOutputGenerated()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(GetAttachDebuggerAction(() => true));

            DoTest(true);

            MockLogger.Verify(l => l.DebugInfo(It.IsAny<string>()), Times.Exactly(1));
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.ToLower().Contains("attached") && s.Contains(DebuggeeProcessId.ToString()))));
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void AttachDebugger_AttachingFails_ErrorOutputGenerated()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(GetAttachDebuggerAction(() => false));

            DoTest(false);

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("unknown reasons"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void AttachDebugger_AttachingThrows_ErrorOutputGenerated()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(GetAttachDebuggerAction(() => { throw new Exception("my message"); }));

            DoTest(false);

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("my message"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void AttachDebugger_ManagedAndNativeEngine_EngineIsPassedToService()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(true);

            DoTest(true, DebuggerEngine.ManagedAndNative);

            MockDebuggerAttacher.Verify(a => a.AttachDebugger(DebuggeeProcessId, DebuggerEngine.ManagedAndNative), Times.Once);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void AttachDebugger_NoPipeAvailable_ErrorOutputGenerated()
        {
            var client = new MessageBasedDebuggerAttacher(Guid.NewGuid().ToString(), Timeout, MockLogger.Object);
            client.AttachDebugger(DebuggeeProcessId, DebuggerEngine.Native).Should().BeFalse();

            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("EndpointNotFoundException"))), Times.Once);
        }

        private void DoTest(bool expectedResult, DebuggerEngine debuggerEngine = DebuggerEngine.Native)
        {
            string pipeId = Guid.NewGuid().ToString();

            var host = new DebuggerAttacherServiceHost(pipeId, MockDebuggerAttacher.Object, MockLogger.Object);
            try
            {
                host.Open();

                var client = new MessageBasedDebuggerAttacher(pipeId, Timeout, MockLogger.Object);

                client.AttachDebugger(DebuggeeProcessId, debuggerEngine).Should().Be(expectedResult);

                MockDebuggerAttacher.Verify(a => a.AttachDebugger(It.Is<int>(processId => processId == DebuggeeProcessId), It.IsAny<DebuggerEngine>()),
                    Times.Once);

                host.Close();
            }
            finally
            {
                if (host.State != CommunicationState.Closed)
                    host.Abort();
            }
        }

        public static Func<bool> GetAttachDebuggerAction(Func<bool> function)
        {
            return () =>
            {
                Thread.Sleep(25);
                return function();
            };
        }

    }
}
