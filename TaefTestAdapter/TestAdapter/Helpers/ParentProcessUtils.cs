// This file has been modified for TAEF support.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TaefTestAdapter.TestAdapter.Helpers
{
    /// <summary>
    /// A utility class to determine a process parent.
    /// From http://stackoverflow.com/questions/394816/how-to-get-parent-process-in-net-in-managed-way
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ParentProcessUtils
    {
        // These members must match PROCESS_BASIC_INFORMATION
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;

        private const int ProcessBasicInformation = 0;
        private const uint ProcessQueryLimitedInformation = 0x1000;

        private static class NativeMethods
        {
            [DllImport("ntdll.dll")]
            internal static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtils processInformation, int processInformationLength, out int returnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool QueryFullProcessImageName(IntPtr processHandle, int flags, StringBuilder exeName, ref int size);
        }

        /// <summary>
        /// Gets the parent process of the current process.
        /// </summary>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess()
        {
            return GetParentProcess(Process.GetCurrentProcess().Handle);
        }

        /// <summary>
        /// Gets the parent process of specified process.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>An instance of the Process class.</returns>
        public static Process GetParentProcess(int id)
        {
            Process process = Process.GetProcessById(id);
            return GetParentProcess(process.Handle);
        }

        /// <summary>
        /// Gets the parent process of a specified process.
        /// </summary>
        /// <param name="handle">The process handle.</param>
        /// <returns>An instance of the Process class or null if an error occurred.</returns>
        public static Process GetParentProcess(IntPtr handle)
        {
            int? parentId = GetParentProcessIdFromHandle(handle);
            if (!parentId.HasValue)
                return null;

            try
            {
                return Process.GetProcessById(parentId.Value);
            }
            catch (ArgumentException)
            {
                // not found
                return null;
            }
        }

        /// <summary>
        /// Gets the id of the parent process of a process. Only requires limited query access to the process, and works
        /// for processes of a different bitness.
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>The id of the parent process, or null if it can not be determined.</returns>
        public static int? GetParentProcessId(int id)
        {
            IntPtr handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, id);
            if (handle == IntPtr.Zero)
                return null;

            try
            {
                return GetParentProcessIdFromHandle(handle);
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        /// <summary>
        /// Gets the full path of the executable of a process. Only requires limited query access to the process, and
        /// works for processes of a different bitness (as opposed to <see cref="Process.MainModule"/>).
        /// </summary>
        /// <param name="id">The process id.</param>
        /// <returns>The path of the process' executable, or null if it can not be determined.</returns>
        public static string GetProcessImageFileName(int id)
        {
            IntPtr handle = NativeMethods.OpenProcess(ProcessQueryLimitedInformation, false, id);
            if (handle == IntPtr.Zero)
                return null;

            try
            {
                var buffer = new StringBuilder(1024);
                int size = buffer.Capacity;
                return NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size)
                    ? buffer.ToString(0, size)
                    : null;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        private static int? GetParentProcessIdFromHandle(IntPtr handle)
        {
            ParentProcessUtils pbi = new ParentProcessUtils();
            int status = NativeMethods.NtQueryInformationProcess(handle, ProcessBasicInformation, ref pbi, Marshal.SizeOf(pbi), out int _);
            if (status != 0)
                return null;

            return pbi.InheritedFromUniqueProcessId.ToInt32();
        }
    }
}
