// This file has been added for TAEF support.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Debugging
{
    /// <summary>
    /// Tests of the retrying of <see cref="VsDebuggerAttacher"/> if Visual Studio rejects the call attaching the debugger.
    /// </summary>
    [TestClass]
    public class VsDebuggerAttacherTests
    {
        private const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001);
        private const int RPC_E_SERVERCALL_RETRYLATER = unchecked((int)0x8001010A);

        private int _nrOfDelays;

        private Task DelayAsync()
        {
            _nrOfDelays++;
            return Task.CompletedTask;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public async Task RetryWhileCallIsRejectedAsync_CallIsRejectedTwice_IsRetriedUntilItSucceedsAsync()
        {
            int nrOfCalls = 0;
            Task<bool> ActionAsync()
            {
                nrOfCalls++;
                if (nrOfCalls == 1)
                    throw new COMException("Call was rejected by callee.", RPC_E_CALL_REJECTED);
                if (nrOfCalls == 2)
                    throw new COMException("The message filter indicated that the application is busy.", RPC_E_SERVERCALL_RETRYLATER);
                return Task.FromResult(true);
            }

            bool result = await VsDebuggerAttacher.RetryWhileCallIsRejectedAsync(ActionAsync, VsDebuggerAttacher.MaxAttachTries, DelayAsync);

            result.Should().BeTrue();
            nrOfCalls.Should().Be(3);
            _nrOfDelays.Should().Be(2);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RetryWhileCallIsRejectedAsync_CallIsAlwaysRejected_LastExceptionIsThrownAfterMaxTries()
        {
            int nrOfCalls = 0;
            Task<bool> ActionAsync()
            {
                nrOfCalls++;
                throw new COMException("Call was rejected by callee.", RPC_E_CALL_REJECTED);
            }

            Func<Task> retrying = () => VsDebuggerAttacher.RetryWhileCallIsRejectedAsync(ActionAsync, 4, DelayAsync);

            retrying.Should().Throw<COMException>().Which.ErrorCode.Should().Be(RPC_E_CALL_REJECTED);
            nrOfCalls.Should().Be(4);
            _nrOfDelays.Should().Be(3);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RetryWhileCallIsRejectedAsync_OtherException_IsNotRetried()
        {
            int nrOfCalls = 0;
            Task<bool> ActionAsync()
            {
                nrOfCalls++;
                throw new COMException("Unspecified error", unchecked((int)0x80004005));
            }

            Func<Task> retrying = () => VsDebuggerAttacher.RetryWhileCallIsRejectedAsync(ActionAsync, VsDebuggerAttacher.MaxAttachTries, DelayAsync);

            retrying.Should().Throw<COMException>().Which.ErrorCode.Should().Be(unchecked((int)0x80004005));
            nrOfCalls.Should().Be(1);
            _nrOfDelays.Should().Be(0);
        }
    }
}
