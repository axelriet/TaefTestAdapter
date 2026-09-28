// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TaefTestAdapter.Common
{
    /// <summary>
    /// Kills processes and process trees.
    /// </summary>
    public static class ProcessUtils
    {
        private const int MaxNrOfSweeps = 5;
        private const uint WaitForTerminationInMs = 5000;
        private const uint WaitForExitingProcessInMs = 1000;
        private const uint TerminationExitCode = 1;

        public static void KillProcess(int processId, ILogger logger)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                DateTime startTime = process.StartTime;
                try
                {
                    process.Kill();
                    logger.DebugInfo($"Killed process {process} with startTime={startTime.ToShortTimeString()}");
                }
                catch (Exception e)
                {
                    logger.DebugWarning($"Could not kill process {process} with startTime={startTime.ToShortTimeString()}: {e.Message}");
                }
            }
            catch (Exception)
            {
                // process was not running - nothing to do
            }
        }

        /// <summary>
        /// Kills the process with id <paramref name="processId"/> together with all its descendants (e.g. TE.exe and the
        /// TE.ProcessHost.exe processes started by it - killing TE.exe alone leaves the test host running, which keeps
        /// the output pipes open). The process tree is determined from a toolhelp snapshot (descendants must have been
        /// created after their parent, which protects against reused process ids); descendants are killed first
        /// (bottom-up), then the process itself. Afterwards, processes which have been started by any of the killed
        /// processes in the meantime are killed as well. Processes which have already exited are skipped. Never throws;
        /// problems are logged as debug messages.
        /// Note that descendants whose parent has already terminated (e.g. a helper process started by a test in
        /// TE.ProcessHost.exe, which has exited) can not be found this way - processes started by the adapter itself are
        /// therefore run in a job object (see <c>RedirectedProcess</c>), which is used instead of this method if possible.
        /// </summary>
        public static void KillProcessTree(int processId, ILogger logger)
        {
            try
            {
                DoKillProcessTree(processId, logger);
            }
            catch (Exception e)
            {
                logger.DebugWarning($"Could not kill process tree of process {processId}: {e.Message}");
            }
        }

        private static void DoKillProcessTree(int processId, ILogger logger)
        {
            int currentProcessId;
            using (Process currentProcess = Process.GetCurrentProcess())
            {
                currentProcessId = currentProcess.Id;
            }
            if (processId == currentProcessId)
            {
                logger.DebugWarning($"Refusing to kill the process tree of the current process ({processId})");
                return;
            }

            long rootCreationTime = GetCreationTime(processId);
            if (rootCreationTime < 0)
            {
                logger.DebugInfo($"Process {processId} is not running (or can not be accessed) - not killing its process tree");
                return;
            }

            IList<ProcessEntry> snapshot = TakeProcessSnapshot();
            string rootExeFile = snapshot.FirstOrDefault(e => e.ProcessId == processId)?.ExeFile;
            var root = new ProcessEntry(processId, 0, rootExeFile) { CreationTime = rootCreationTime };
            var treeMembers = new Dictionary<int, ProcessEntry> { { processId, root } };

            List<ProcessEntry> descendants = FindNewDescendants(snapshot, treeMembers);
            descendants.ForEach(d => treeMembers[d.ProcessId] = d);

            // bottom-up: the descendants list is in breadth-first order
            for (int i = descendants.Count - 1; i >= 0; i--)
                Kill(descendants[i], logger);
            Kill(root, logger);

            // processes started in the meantime (e.g. a new test host started by TE.exe after its host has been killed)
            for (int sweep = 0; sweep < MaxNrOfSweeps; sweep++)
            {
                List<ProcessEntry> lateDescendants = FindNewDescendants(TakeProcessSnapshot(), treeMembers);
                if (lateDescendants.Count == 0)
                    break;

                lateDescendants.ForEach(d => treeMembers[d.ProcessId] = d);
                for (int i = lateDescendants.Count - 1; i >= 0; i--)
                    Kill(lateDescendants[i], logger);
            }
        }

        /// <returns>
        /// The processes of <paramref name="snapshot"/> which are descendants of <paramref name="knownTreeMembers"/> and
        /// not contained in it, in breadth-first order
        /// </returns>
        private static List<ProcessEntry> FindNewDescendants(IList<ProcessEntry> snapshot, IDictionary<int, ProcessEntry> knownTreeMembers)
        {
            var parents = new Dictionary<int, ProcessEntry>(knownTreeMembers);
            var result = new List<ProcessEntry>();
            var queue = new Queue<ProcessEntry>(knownTreeMembers.Values);
            while (queue.Count > 0)
            {
                ProcessEntry parent = queue.Dequeue();
                List<ProcessEntry> candidates = snapshot
                    .Where(e => e.ParentProcessId == parent.ProcessId && e.ProcessId != parent.ProcessId && !parents.ContainsKey(e.ProcessId))
                    .ToList();
                if (candidates.Count == 0)
                    continue;

                // if the parent has terminated and its process id has been reused, only processes created before the
                // reuse can be children of the parent
                long parentIdReusedAt = long.MaxValue;
                long currentCreationTimeOfParentId = GetCreationTime(parent.ProcessId);
                if (currentCreationTimeOfParentId >= 0 && currentCreationTimeOfParentId != parent.CreationTime)
                    parentIdReusedAt = currentCreationTimeOfParentId;

                foreach (ProcessEntry candidate in candidates)
                {
                    candidate.CreationTime = GetCreationTime(candidate.ProcessId);
                    // a child can not have been created before its parent - otherwise the parent's process id has been reused
                    if (candidate.CreationTime < 0 || candidate.CreationTime < parent.CreationTime || candidate.CreationTime >= parentIdReusedAt)
                        continue;

                    parents.Add(candidate.ProcessId, candidate);
                    result.Add(candidate);
                    queue.Enqueue(candidate);
                }
            }
            return result;
        }

        private static void Kill(ProcessEntry process, ILogger logger)
        {
            string description = process.ExeFile == null ? $"{process.ProcessId}" : $"{process.ProcessId} ({process.ExeFile})";
            using (SafeKernelHandle handle = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_TERMINATE | NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeMethods.SYNCHRONIZE, false, process.ProcessId))
            {
                if (handle.IsInvalid)
                {
                    logger.DebugInfo($"Could not open process {description} for termination (probably not running any more): {Win32Utils.GetLastWin32Error()}");
                    return;
                }

                if (GetCreationTime(handle) != process.CreationTime)
                {
                    logger.DebugInfo($"Process {description} is not running any more (process id has been reused)");
                    return;
                }

                // An exited process can still be opened as long as someone holds a handle to it (e.g. the process
                // executor which has started it), but TerminateProcess would fail with ERROR_ACCESS_DENIED.
                if (NativeMethods.WaitForSingleObject(handle, 0) == NativeMethods.WAIT_OBJECT_0)
                {
                    logger.DebugInfo($"Process {description} has already exited");
                    return;
                }

                if (!NativeMethods.TerminateProcess(handle, TerminationExitCode))
                {
                    string error = Win32Utils.GetLastWin32Error();
                    // TerminateProcess also fails with ERROR_ACCESS_DENIED if the process is exiting on its own in the
                    // meantime (e.g. cmd.exe or conhost.exe after their child has been killed)
                    if (NativeMethods.WaitForSingleObject(handle, WaitForExitingProcessInMs) == NativeMethods.WAIT_OBJECT_0)
                    {
                        logger.DebugInfo($"Process {description} has exited in the meantime");
                        return;
                    }

                    logger.DebugWarning($"Could not kill process {description}: {error}");
                    return;
                }

                NativeMethods.WaitForSingleObject(handle, WaitForTerminationInMs);
                logger.DebugInfo($"Killed process {description}");
            }
        }

        /// <returns>The creation time of the process (as FILETIME), or -1 if it is not running or can not be accessed</returns>
        private static long GetCreationTime(int processId)
        {
            using (SafeKernelHandle handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, processId))
            {
                return handle.IsInvalid ? -1 : GetCreationTime(handle);
            }
        }

        private static long GetCreationTime(SafeKernelHandle processHandle)
        {
            return NativeMethods.GetProcessTimes(processHandle, out long creationTime, out _, out _, out _)
                ? creationTime
                : -1;
        }

        private static IList<ProcessEntry> TakeProcessSnapshot()
        {
            var result = new List<ProcessEntry>();
            using (SafeKernelHandle snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0))
            {
                if (snapshot.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateToolhelp32Snapshot failed");

                var entry = new NativeMethods.PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.PROCESSENTRY32)) };
                if (!NativeMethods.Process32First(snapshot, ref entry))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == NativeMethods.ERROR_NO_MORE_FILES)
                        return result;
                    throw new Win32Exception(error, "Process32First failed");
                }

                do
                {
                    result.Add(new ProcessEntry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, entry.szExeFile));
                }
                while (NativeMethods.Process32Next(snapshot, ref entry));
            }
            return result;
        }

        private class ProcessEntry
        {
            public int ProcessId { get; }
            public int ParentProcessId { get; }
            public string ExeFile { get; }
            public long CreationTime { get; set; } = -1;

            public ProcessEntry(int processId, int parentProcessId, string exeFile)
            {
                ProcessId = processId;
                ParentProcessId = parentProcessId;
                ExeFile = exeFile;
            }
        }

        private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            // used by the P/Invoke marshaller
            private SafeKernelHandle() : base(true) { }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }

        private static class NativeMethods
        {
            public const uint TH32CS_SNAPPROCESS = 0x00000002;
            public const uint PROCESS_TERMINATE = 0x0001;
            public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            public const uint SYNCHRONIZE = 0x00100000;
            public const uint WAIT_OBJECT_0 = 0;
            public const int ERROR_NO_MORE_FILES = 18;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct PROCESSENTRY32
            {
                public uint dwSize;
                public uint cntUsage;
                public uint th32ProcessID;
                public IntPtr th32DefaultHeapID;
                public uint th32ModuleID;
                public uint cntThreads;
                public uint th32ParentProcessID;
                public int pcPriClassBase;
                public uint dwFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public string szExeFile;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern SafeKernelHandle CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

            [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool Process32First(SafeKernelHandle hSnapshot, ref PROCESSENTRY32 lppe);

            [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool Process32Next(SafeKernelHandle hSnapshot, ref PROCESSENTRY32 lppe);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern SafeKernelHandle OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool TerminateProcess(SafeKernelHandle hProcess, uint uExitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint WaitForSingleObject(SafeKernelHandle hHandle, uint dwMilliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetProcessTimes(SafeKernelHandle hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);
        }
    }
}
