// This file has been modified for TAEF support.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using TaefTestAdapter.Common;
using TaefTestAdapter.TestAdapter.ProcessExecution;

namespace TaefTestAdapter.VsPackage.Debugging
{
    /// <summary>
    /// Attaches the Visual Studio debugger to a (suspended) process, i.e. to TE.exe running the tests to be debugged.
    /// </summary>
    public class VsDebuggerAttacher : IDebuggerAttacher
    {
        /// <summary>Maximum number of attempts to attach the debugger if the call is rejected (see <see cref="IsCallRejected"/>).</summary>
        public const int MaxAttachTries = 10;
        private static readonly TimeSpan AttachRetryDelay = TimeSpan.FromMilliseconds(100);

        private const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001);
        private const int RPC_E_SERVERCALL_RETRYLATER = unchecked((int)0x8001010A);

        private readonly IServiceProvider _serviceProvider;
        private readonly JoinableTaskFactory _joinableTaskFactory;

        /// <param name="serviceProvider">Provides the debugger service (SVsShellDebugger)</param>
        /// <param name="joinableTaskFactory">Used to switch to the UI thread</param>
        internal VsDebuggerAttacher(IServiceProvider serviceProvider, JoinableTaskFactory joinableTaskFactory)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
        }

        /// <summary>
        /// Attaches the debugger. Can be called from any thread (e.g. by the debugger attacher service); the
        /// debugger is attached on the UI thread. If the call is rejected (e.g. because the debugger is busy), it is
        /// retried a few times.
        /// </summary>
        public bool AttachDebugger(int processId, DebuggerEngine debuggerEngine)
        {
            return _joinableTaskFactory.Run(() => RetryWhileCallIsRejectedAsync(
                async () =>
                {
                    await _joinableTaskFactory.SwitchToMainThreadAsync();
                    return AttachDebuggerOnUiThread(processId, debuggerEngine);
                },
                MaxAttachTries,
                // the UI thread is not blocked while waiting
                () => Task.Delay(AttachRetryDelay)));
        }

        /// <summary>
        /// Runs <paramref name="action"/>. If it throws a <see cref="COMException"/> because the call has been rejected
        /// (see <see cref="IsCallRejected"/>), <paramref name="delay"/> is awaited and the action is run again, at most
        /// <paramref name="maxTries"/> times in total; the last exception is rethrown. Other exceptions are not caught.
        /// </summary>
        public static async Task<T> RetryWhileCallIsRejectedAsync<T>(Func<Task<T>> action, int maxTries, Func<Task> delay)
        {
            for (int tries = 1; ; tries++)
            {
                try
                {
                    return await action();
                }
                catch (COMException e) when (IsCallRejected(e) && tries < maxTries)
                {
                    // retry below (can not await in a catch clause)
                }
                await delay();
            }
        }

        /// <returns>True for RPC_E_CALL_REJECTED ("Call was rejected by callee") and RPC_E_SERVERCALL_RETRYLATER.</returns>
        public static bool IsCallRejected(COMException exception)
        {
            return exception.ErrorCode == RPC_E_CALL_REJECTED || exception.ErrorCode == RPC_E_SERVERCALL_RETRYLATER;
        }

        private bool AttachDebuggerOnUiThread(int processId, DebuggerEngine debuggerEngine)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(_serviceProvider.GetService(typeof(SVsShellDebugger)) is IVsDebugger4 debugger))
                throw new InvalidOperationException("Visual Studio's debugger service (SVsShellDebugger) is not available");

            IntPtr pDebugEngine = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(Guid)));
            try
            {
                Guid debuggerEngineGuid = debuggerEngine == DebuggerEngine.Native
                    ? VSConstants.DebugEnginesGuids.NativeOnly
                    : VSConstants.DebugEnginesGuids.ManagedAndNative;
                Marshal.StructureToPtr(debuggerEngineGuid, pDebugEngine, false);

                var debugTarget = new VsDebugTargetInfo4
                {
                    dlo = (uint) DEBUG_LAUNCH_OPERATION.DLO_AlreadyRunning
                          | (uint) _DEBUG_LAUNCH_OPERATION4.DLO_AttachToSuspendedLaunchProcess,
                    dwProcessId = (uint) processId,
                    dwDebugEngineCount = 1,
                    pDebugEngines = pDebugEngine,
                };

                debugger.LaunchDebugTargets4(1, new[] { debugTarget }, new VsDebugTargetProcessInfo[1]);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pDebugEngine);
            }
            return true;
        }

    }
}
