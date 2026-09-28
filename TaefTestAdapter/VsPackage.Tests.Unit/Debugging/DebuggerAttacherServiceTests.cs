// This file has been modified by Microsoft on 7/2017.
// This file has been modified for TAEF support.

using FluentAssertions;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Linq;
using System.ServiceModel;
using TaefTestAdapter.TestAdapter.ProcessExecution;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Debugging
{
    /// <summary>
    /// Tests of the WCF service (hosted by the VS package) which attaches the debugger on behalf of the test adapter. The
    /// service is hosted in the test process on a named pipe with a unique id, i.e. the tests do not need Visual Studio.
    /// </summary>
    [TestClass]
    public class DebuggerAttacherServiceTests
    {
        /// <summary>
        /// Timeout of the WCF operations: an upper bound only (the calls usually take a few milliseconds), chosen
        /// generously since the first WCF call of a process can take seconds on a busy machine.
        /// </summary>
        internal static readonly TimeSpan WaitingTime = TimeSpan.FromSeconds(30);

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
        [TestCategory(Unit)]
        public void DebuggerAttacherService_ReceivesMessage_AnswersImmediately()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(MessageBasedDebuggerAttacherTests.GetAttachDebuggerAction(() => true));

            DoTest(null);

            MockDebuggerAttacher.Verify(a => a.AttachDebugger(DebuggeeProcessId, DebuggerEngine.Native), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebuggerAttacherService_AttacherThrows_AnswerIncludesExceptionMessage()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(MessageBasedDebuggerAttacherTests.GetAttachDebuggerAction(() => { throw new Exception("my message"); }));

            DoTest("my message");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebuggerAttacherService_AttacherReturnsFalse_AnswerWithoutReason()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(MessageBasedDebuggerAttacherTests.GetAttachDebuggerAction(() => false));

            DoTest("unknown reasons");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebuggerAttacherService_ManagedAndNativeEngine_EngineIsPassedToAttacher()
        {
            MockDebuggerAttacher
                .Setup(a => a.AttachDebugger(It.IsAny<int>(), It.IsAny<DebuggerEngine>()))
                .Returns(true);

            DoTest(null, DebuggerEngine.ManagedAndNative);

            MockDebuggerAttacher.Verify(a => a.AttachDebugger(DebuggeeProcessId, DebuggerEngine.ManagedAndNative), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebuggerAttacherServiceHost_Address_IsNamedPipeOfTaefTestAdapter()
        {
            string pipeId = Guid.NewGuid().ToString();

            var host = new DebuggerAttacherServiceHost(pipeId, MockDebuggerAttacher.Object, MockLogger.Object);
            try
            {
                host.BaseAddresses.Should().ContainSingle()
                    .Which.ToString().Should().Be($"net.pipe://localhost/TaefTestAdapter_{pipeId}/");
                host.BaseAddresses.Single().Should().Be(DebuggerAttacherServiceConfiguration.ConstructPipeUri(pipeId));
                host.Description.Endpoints.Should().ContainSingle()
                    .Which.Address.Uri.ToString().Should().Be($"net.pipe://localhost/TaefTestAdapter_{pipeId}/IDebuggerAttacherService");
            }
            finally
            {
                host.Abort();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebuggerAttacherServiceConfiguration_DifferentIds_DifferentPipes()
        {
            DebuggerAttacherServiceConfiguration.ConstructPipeUri("a")
                .Should().NotBe(DebuggerAttacherServiceConfiguration.ConstructPipeUri("b"));
            DebuggerAttacherServiceConfiguration.ConstructPipeUri("my-id").ToString()
                .Should().Be("net.pipe://localhost/TaefTestAdapter_my-id/");
        }

        private void DoTest(string expectedErrorMessagePart, DebuggerEngine debuggerEngine = DebuggerEngine.Native)
        {
            string pipeId = Guid.NewGuid().ToString();

            var host = new DebuggerAttacherServiceHost(pipeId, MockDebuggerAttacher.Object, MockLogger.Object);
            try
            {
                host.Open();

                var proxy = DebuggerAttacherServiceConfiguration.CreateProxy(pipeId, WaitingTime);
                using (var client = new DebuggerAttacherServiceProxyWrapper(proxy))
                {
                    client.Should().NotBeNull();
                    client.Service.Should().NotBeNull();

                    Action attaching = () => client.Service.AttachDebugger(DebuggeeProcessId, debuggerEngine);
                    if (expectedErrorMessagePart == null)
                    {
                        attaching.Should().NotThrow();
                    }
                    else
                    {
                        attaching.Should().Throw<FaultException<DebuggerAttacherServiceFault>>().Where(
                            (FaultException<DebuggerAttacherServiceFault> ex) => ex.Detail.Message.Contains(expectedErrorMessagePart)
                                                                                 && ex.Detail.Message.Contains(DebuggeeProcessId.ToString()));
                    }
                }

                host.Close();
            }
            finally
            {
                if (host.State != CommunicationState.Closed)
                    host.Abort();
            }
        }

    }
}
