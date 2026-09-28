// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.ProcessExecution
{
    /// <summary>
    /// A process (usually TE.exe) with redirected output, running in a job object together with all its descendants.
    /// <list type="bullet">
    /// <item>Standard output and standard error of the process are redirected into <em>one</em> pipe, so that lines
    /// written to the two streams keep their order (e.g. a test's <c>std::cerr</c> output under <c>TE.exe /inproc</c>,
    /// which must end up within the output of that test). Only the pipe (and <c>NUL</c> as standard input) is inherited by
    /// the process.</item>
    /// <item>The output is decoded as UTF-8 (see <see cref="OutputEncoding"/>) and reported line by line on one thread
    /// (i.e. never concurrently).</item>
    /// <item>The process is created suspended (<see cref="StartSuspended"/>, e.g. for attaching a debugger) within the job,
    /// i.e. before it can start other processes; <see cref="ResumeAndWaitForExit"/> resumes it.</item>
    /// <item>The processes started by the process inherit the output pipe (e.g. TE.ProcessHost.exe, but also helper
    /// processes started by tests), so the pipe may stay open after the process has exited. Once the process has exited,
    /// <see cref="ResumeAndWaitForExit"/> only waits until the pipe has been read empty, plus a grace period; processes
    /// still holding the pipe open afterwards are logged, and are not waited for.</item>
    /// <item><see cref="Kill"/> terminates the job, i.e. the process and all its descendants, even those whose parent has
    /// already terminated. The job is also terminated if the current process terminates (e.g. if the test host is
    /// killed), and if the object is disposed while the process is still running. Processes left behind by the process
    /// after it has exited (without having been killed) keep running.</item>
    /// </list>
    /// If the job object can not be created, <see cref="Kill"/> falls back to <see cref="ProcessUtils.KillProcessTree"/>.
    /// </summary>
    public sealed class RedirectedProcess : IDisposable
    {
        /// <summary>Encoding of the output of the processes (UTF-8 without BOM).</summary>
        public static readonly Encoding OutputEncoding = new UTF8Encoding(false);

        /// <summary>
        /// Time to wait for the end of the output after the process has exited and its output has been read completely
        /// (the pipe is usually closed a few milliseconds after the process has exited).
        /// </summary>
        public static readonly TimeSpan DefaultOutputGracePeriod = TimeSpan.FromSeconds(3);

        private const uint TerminationExitCode = 1;
        private const int OutputBufferSize = 4096;
        private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(50);

        private readonly string _command;
        private readonly ILogger _logger;

        // process, thread and job handles; Kill and Dispose may be called concurrently with ResumeAndWaitForExit
        private readonly object _lock = new object();
        private SafeProcessHandle _processHandle;
        private SafeKernelHandle _threadHandle;
        private SafeKernelHandle _job;
        private volatile bool _killed;
        private bool _disposed;

        // state of the thread reading the output (see ReadOutput)
        private readonly object _readerLock = new object();
        private FileStream _outputStream;
        private bool _readerStarted;
        private bool _readerFinished;
        private bool _isReading;
        private long _readStartedAt;
        private bool _processHasExited;
        private long _processExitedAt;
        private long _ticksBlockedAfterExit;

        // reporting of output lines; the lock is held while a line is reported
        private readonly object _reportLock = new object();
        private Action<string> _reportOutputLine;
        private bool _reportingStopped;
        private Exception _reportException;

        /// <summary>The id of the process.</summary>
        public int ProcessId { get; private set; }

        private RedirectedProcess(string command, ILogger logger)
        {
            _command = command;
            _logger = logger;
        }

        /// <summary>
        /// Creates the process suspended, with its output redirected, and assigns it to a new job object.
        /// </summary>
        /// <param name="command">The executable to be started.</param>
        /// <param name="parameters">The complete argument string (passed as is, i.e. it must already be quoted properly).</param>
        /// <param name="workingDir">The working directory of the process (current directory if null or empty).</param>
        /// <param name="environmentVariablesToSet">Environment variables of the process in addition to (or overriding) the
        /// environment of the current process (see <see cref="ProcessEnvironment.GetEnvironmentVariablesToSet"/>).</param>
        /// <param name="createNoWindow">If true, the process is created with flag <c>CREATE_NO_WINDOW</c> (i.e. without
        /// a console, like <see cref="ProcessStartInfo.CreateNoWindow"/>); otherwise, it shares the console of the
        /// current process (if any).</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="Win32Exception">If the process can not be created.</exception>
        public static RedirectedProcess StartSuspended(string command, string parameters, string workingDir,
            IDictionary<string, string> environmentVariablesToSet, bool createNoWindow, ILogger logger)
        {
            var process = new RedirectedProcess(command, logger);
            try
            {
                process.Create(parameters, workingDir, environmentVariablesToSet, createNoWindow);
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Resumes the process, reports its output line by line, and waits for it to exit. Returns when the process has
        /// exited and its output has been read completely (i.e. the pipe has been closed), or has been read empty and not
        /// been closed within <paramref name="outputGracePeriod"/> (the processes holding it open are logged), or when
        /// the process has exited after it has been killed (see <see cref="Kill"/>). No lines are reported afterwards.
        /// </summary>
        /// <param name="reportOutputLine">Receives the lines of output (may be null). If it throws, the process is killed
        /// and the exception is rethrown by this method.</param>
        /// <param name="outputGracePeriod">See <see cref="DefaultOutputGracePeriod"/>.</param>
        /// <returns>The exit code of the process.</returns>
        public int ResumeAndWaitForExit(Action<string> reportOutputLine, TimeSpan outputGracePeriod)
        {
            _reportOutputLine = reportOutputLine;
            var readerThread = new Thread(ReadOutput)
            {
                IsBackground = true,
                Name = $"Output reader of process {ProcessId}"
            };
            lock (_readerLock)
            {
                _readerStarted = true;
            }
            readerThread.Start();

            Resume();

            if (NativeMethods.WaitForSingleObject(_processHandle, NativeMethods.INFINITE) != NativeMethods.WAIT_OBJECT_0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not wait for process {ProcessId}");
            if (!NativeMethods.GetExitCodeProcess(_processHandle, out int exitCode))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not get exit code of process {ProcessId}");

            bool outputIsStillOpen = !WaitForEndOfOutput(outputGracePeriod);

            Exception reportException;
            lock (_reportLock)
            {
                _reportingStopped = true;
                reportException = _reportException;
            }
            if (reportException != null)
                ExceptionDispatchInfo.Capture(reportException).Throw();

            if (outputIsStillOpen)
            {
                _logger.LogWarning($"Process {ProcessId} ('{_command}') has exited, but its output is still being kept open{DescribeRemainingProcesses()}. Not waiting for them to terminate.");
            }

            return exitCode;
        }

        /// <summary>
        /// Kills the process together with all its descendants (i.e., terminates the job). If the process has exited and
        /// the output pipe is kept open by one of its descendants, <see cref="ResumeAndWaitForExit"/> returns immediately.
        /// Can be called from any thread; never throws.
        /// </summary>
        public void Kill()
        {
            try
            {
                lock (_lock)
                {
                    if (_disposed || _processHandle == null)
                        return;
                    _killed = true;

                    if (_job != null)
                    {
                        if (NativeMethods.TerminateJobObject(_job, TerminationExitCode))
                        {
                            _logger.DebugInfo($"Killed process {ProcessId} ('{_command}') and all processes started by it");
                            return;
                        }
                        _logger.DebugWarning($"Could not terminate the job object of process {ProcessId}: {Win32Utils.GetLastWin32Error()}");
                    }

                    // Holding the process handle ensures that the process id has not been reused.
                    if (HasExited())
                    {
                        _logger.DebugInfo($"Process {ProcessId} ('{_command}') has already exited, not killing its process tree");
                        return;
                    }
                    ProcessUtils.KillProcessTree(ProcessId, _logger);
                }
            }
            catch (Exception e)
            {
                _logger.DebugWarning($"Could not kill process {ProcessId}: {e.Message}");
            }
        }

        /// <summary>
        /// Releases the process. If the process is still running (e.g. because it has not been resumed, or because an
        /// exception occurred), it is killed together with all its descendants. Processes left behind by the process after
        /// it has exited normally keep running.
        /// </summary>
        public void Dispose()
        {
            bool processIsRunning;
            lock (_lock)
            {
                if (_disposed)
                    return;
                processIsRunning = _processHandle != null && !HasExited();
            }

            if (processIsRunning)
                Kill();

            lock (_lock)
            {
                _disposed = true;
                if (_job != null)
                {
                    // KILL_ON_JOB_CLOSE would kill the processes remaining in the job when its handle is closed
                    if (!_killed && !processIsRunning)
                        SetJobLimits(_job, killOnJobClose: false);
                    _job.Dispose();
                    _job = null;
                }
                _threadHandle?.Dispose();
                _threadHandle = null;
                _processHandle?.Dispose();
                _processHandle = null;
            }

            lock (_readerLock)
            {
                // otherwise, the reader thread disposes the stream (it might still be reading it)
                if (!_readerStarted)
                    _outputStream?.Dispose();
            }
        }

        #region Process creation

        private void Create(string parameters, string workingDir, IDictionary<string, string> environmentVariablesToSet, bool createNoWindow)
        {
            var commandLine = new StringBuilder($"\"{_command}\"");
            if (!string.IsNullOrEmpty(parameters))
                commandLine.Append(' ').Append(parameters);
            if (string.IsNullOrEmpty(workingDir))
                workingDir = null;
            string environmentBlock = ProcessEnvironment.CreateEnvironmentBlock(environmentVariablesToSet);

            if (!NativeMethods.CreatePipe(out SafeFileHandle readingEnd, out SafeFileHandle writingEnd, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create pipe");
            _outputStream = new FileStream(readingEnd, FileAccess.Read, OutputBufferSize, false);

            SafeKernelHandle job = CreateJob(_logger);
            try
            {
                // The parent's copies of the pipe's writing end and of NUL are closed right after the process has been
                // created - otherwise, the end of the output would never be detected.
                using (writingEnd)
                using (SafeFileHandle standardInput = OpenNulDevice())
                {
                    MakeInheritable(writingEnd);
                    MakeInheritable(standardInput);

                    // The process is created within the job, so that it is killed with the job even if the current
                    // process terminates right after the process has been created.
                    bool createdInJob = job != null;
                    NativeMethods.PROCESS_INFORMATION processInformation;
                    try
                    {
                        processInformation = CreateSuspendedProcess(commandLine, workingDir, environmentBlock, standardInput, writingEnd, job, createNoWindow, parameters);
                    }
                    catch (Win32Exception e) when (job != null && IsPossiblyCausedByJob(e.NativeErrorCode))
                    {
                        _logger.DebugWarning($"Could not create process within job object, creating it without: {e.Message} ({e.NativeErrorCode})");
                        createdInJob = false;
                        processInformation = CreateSuspendedProcess(commandLine, workingDir, environmentBlock, standardInput, writingEnd, null, createNoWindow, parameters);
                    }

                    lock (_lock)
                    {
                        _processHandle = new SafeProcessHandle(processInformation.hProcess, true);
                        _threadHandle = new SafeKernelHandle(processInformation.hThread);
                        ProcessId = processInformation.dwProcessId;
                    }

                    // the process is still suspended, i.e. it can not have started other processes yet
                    if (job != null && !createdInJob && !NativeMethods.AssignProcessToJobObject(job, _processHandle))
                    {
                        _logger.DebugWarning($"Could not assign process {ProcessId} to job object: {Win32Utils.GetLastWin32Error()}");
                        job.Dispose();
                        job = null;
                    }
                }
            }
            finally
            {
                lock (_lock)
                {
                    // disposed together with the process (killing the process if it is still running)
                    _job = job;
                }
            }
        }

        private static bool IsPossiblyCausedByJob(int nativeErrorCode)
        {
            const int errorAccessDenied = 5;
            const int errorNotSupported = 50;
            const int errorInvalidParameter = 87;
            return nativeErrorCode == errorAccessDenied || nativeErrorCode == errorNotSupported || nativeErrorCode == errorInvalidParameter;
        }

        /// <param name="commandLine">The command line (must be writable).</param>
        /// <param name="workingDir">The working directory (current directory if null).</param>
        /// <param name="environmentBlock">See <see cref="ProcessEnvironment.CreateEnvironmentBlock"/>.</param>
        /// <param name="standardInput">Inheritable standard input of the process.</param>
        /// <param name="standardOutputAndError">Inheritable standard output and error of the process.</param>
        /// <param name="job">If not null, the process is created within this job.</param>
        /// <param name="createNoWindow">See <see cref="StartSuspended"/>.</param>
        /// <param name="parameters">The parameters (for the error message).</param>
        private NativeMethods.PROCESS_INFORMATION CreateSuspendedProcess(StringBuilder commandLine, string workingDir, string environmentBlock,
            SafeFileHandle standardInput, SafeFileHandle standardOutputAndError, SafeKernelHandle job, bool createNoWindow, string parameters)
        {
            bool inputRefAdded = false, outputRefAdded = false, jobRefAdded = false;
            IntPtr attributeList = IntPtr.Zero;
            IntPtr inheritedHandles = IntPtr.Zero;
            IntPtr jobList = IntPtr.Zero;
            IntPtr environment = IntPtr.Zero;
            try
            {
                standardInput.DangerousAddRef(ref inputRefAdded);
                standardOutputAndError.DangerousAddRef(ref outputRefAdded);
                IntPtr input = standardInput.DangerousGetHandle();
                IntPtr output = standardOutputAndError.DangerousGetHandle();

                int nrOfAttributes = job != null ? 2 : 1;
                IntPtr size = IntPtr.Zero;
                NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, nrOfAttributes, 0, ref size);
                attributeList = Marshal.AllocHGlobal(size);
                if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, nrOfAttributes, 0, ref size))
                {
                    Marshal.FreeHGlobal(attributeList);
                    attributeList = IntPtr.Zero;
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not initialize the attributes of the process");
                }

                // only these handles are inherited (and not e.g. the output pipe of another TE.exe created concurrently)
                inheritedHandles = Marshal.AllocHGlobal(2 * IntPtr.Size);
                Marshal.WriteIntPtr(inheritedHandles, 0, input);
                Marshal.WriteIntPtr(inheritedHandles, IntPtr.Size, output);
                if (!NativeMethods.UpdateProcThreadAttribute(attributeList, 0, NativeMethods.PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                    inheritedHandles, new IntPtr(2 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set the handles to be inherited by the process");

                if (job != null)
                {
                    job.DangerousAddRef(ref jobRefAdded);
                    jobList = Marshal.AllocHGlobal(IntPtr.Size);
                    Marshal.WriteIntPtr(jobList, 0, job.DangerousGetHandle());
                    if (!NativeMethods.UpdateProcThreadAttribute(attributeList, 0, NativeMethods.PROC_THREAD_ATTRIBUTE_JOB_LIST,
                        jobList, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not set the job object of the process");
                }

                var startupInfo = new NativeMethods.STARTUPINFOEX
                {
                    StartupInfo = new NativeMethods.STARTUPINFO
                    {
                        cb = Marshal.SizeOf(typeof(NativeMethods.STARTUPINFOEX)),
                        dwFlags = NativeMethods.STARTF_USESTDHANDLES,
                        hStdInput = input,
                        hStdOutput = output,
                        hStdError = output
                    },
                    lpAttributeList = attributeList
                };

                environment = Marshal.StringToHGlobalUni(environmentBlock);
                if (!NativeMethods.CreateProcess(
                    null, commandLine, IntPtr.Zero, IntPtr.Zero, true,
                    NativeMethods.CREATE_SUSPENDED | NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.EXTENDED_STARTUPINFO_PRESENT
                    | (createNoWindow ? NativeMethods.CREATE_NO_WINDOW : 0),
                    environment, workingDir, ref startupInfo, out NativeMethods.PROCESS_INFORMATION processInformation))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(),
                        $"Could not create process. Command: '{_command}', parameters: '{parameters}', working dir: '{workingDir}'");
                }
                return processInformation;
            }
            finally
            {
                if (environment != IntPtr.Zero)
                    Marshal.FreeHGlobal(environment);
                if (attributeList != IntPtr.Zero)
                {
                    NativeMethods.DeleteProcThreadAttributeList(attributeList);
                    Marshal.FreeHGlobal(attributeList);
                }
                if (inheritedHandles != IntPtr.Zero)
                    Marshal.FreeHGlobal(inheritedHandles);
                if (jobList != IntPtr.Zero)
                    Marshal.FreeHGlobal(jobList);
                if (jobRefAdded)
                    job.DangerousRelease();
                if (outputRefAdded)
                    standardOutputAndError.DangerousRelease();
                if (inputRefAdded)
                    standardInput.DangerousRelease();
            }
        }

        private static SafeFileHandle OpenNulDevice()
        {
            SafeFileHandle handle = NativeMethods.CreateFile("NUL", NativeMethods.GENERIC_READ,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE, IntPtr.Zero, NativeMethods.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open NUL");
            return handle;
        }

        private static void MakeInheritable(SafeHandle handle)
        {
            if (!NativeMethods.SetHandleInformation(handle, NativeMethods.HANDLE_FLAG_INHERIT, NativeMethods.HANDLE_FLAG_INHERIT))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not make handle inheritable");
        }

        /// <returns>A new job object (killing its processes when its handle is closed), or null if it could not be created.</returns>
        private static SafeKernelHandle CreateJob(ILogger logger)
        {
            SafeKernelHandle job = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid)
            {
                logger.DebugWarning($"Could not create job object: {Win32Utils.GetLastWin32Error()}");
                job.Dispose();
                return null;
            }

            if (!SetJobLimits(job, killOnJobClose: true))
            {
                logger.DebugWarning($"Could not set limits of job object: {Win32Utils.GetLastWin32Error()}");
                job.Dispose();
                return null;
            }

            return job;
        }

        private static bool SetJobLimits(SafeKernelHandle job, bool killOnJobClose)
        {
            // processes of the job may explicitly break away from it (CREATE_BREAKAWAY_FROM_JOB), as they could without
            // the job
            var limits = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new NativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = NativeMethods.JOB_OBJECT_LIMIT_BREAKAWAY_OK
                                 | (killOnJobClose ? NativeMethods.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE : 0)
                }
            };
            return NativeMethods.SetInformationJobObject(job, NativeMethods.JobObjectExtendedLimitInformation,
                ref limits, (uint)Marshal.SizeOf(typeof(NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION)));
        }

        private void Resume()
        {
            lock (_lock)
            {
                if (NativeMethods.ResumeThread(_threadHandle) == uint.MaxValue && !_killed)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not resume process {ProcessId}");
            }
        }

        /// <summary>Requires <see cref="_lock"/> and a valid process handle.</summary>
        private bool HasExited()
        {
            return NativeMethods.WaitForSingleObject(_processHandle, 0) == NativeMethods.WAIT_OBJECT_0;
        }

        #endregion

        #region Output

        /// <summary>
        /// Waits until the output has been read completely, i.e. the reader thread has finished. After the process has
        /// exited, only the processes started by it can write to the pipe - so if the reader thread has been waiting for
        /// output for <paramref name="outputGracePeriod"/> since then (in total, i.e. excluding the time needed for
        /// reporting lines which had been buffered), all output of the process has been reported, and the pipe is kept
        /// open by another process. Does not wait if the process has been killed.
        /// </summary>
        /// <returns>False if the output has not been read completely within the grace period.</returns>
        private bool WaitForEndOfOutput(TimeSpan outputGracePeriod)
        {
            double gracePeriodInTicks = Math.Max(0, outputGracePeriod.TotalSeconds) * Stopwatch.Frequency;
            lock (_readerLock)
            {
                _processHasExited = true;
                _processExitedAt = Stopwatch.GetTimestamp();
                while (!_readerFinished && !_killed)
                {
                    if (GetTicksBlockedAfterExit() >= gracePeriodInTicks)
                        return false;
                    Monitor.Wait(_readerLock, PollingInterval);
                }
            }
            return true;
        }

        /// <summary>Requires <see cref="_readerLock"/>.</summary>
        private long GetTicksBlockedAfterExit()
        {
            long ticks = _ticksBlockedAfterExit;
            if (_isReading)
                ticks += Stopwatch.GetTimestamp() - Math.Max(_readStartedAt, _processExitedAt);
            return ticks;
        }

        private string DescribeRemainingProcesses()
        {
            IList<int> processIds;
            lock (_lock)
            {
                processIds = _job != null ? GetProcessIdsOfJob(_job) : new List<int>();
            }

            var processes = new List<string>();
            foreach (int processId in processIds)
            {
                try
                {
                    using (Process process = Process.GetProcessById(processId))
                    {
                        // the console host of processes started without console window is part of the job, too
                        if (!string.Equals(process.ProcessName, "conhost", StringComparison.OrdinalIgnoreCase))
                            processes.Add($"{process.ProcessName} ({processId})");
                    }
                }
                catch (Exception)
                {
                    // process has exited meanwhile
                }
            }

            return processes.Count == 0
                ? " by processes started by it"
                : $" by processes started by it which are still running: {string.Join(", ", processes)}";
        }

        private static IList<int> GetProcessIdsOfJob(SafeKernelHandle job)
        {
            const int maxNrOfProcesses = 64;
            int size = 2 * sizeof(uint) + maxNrOfProcesses * IntPtr.Size;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                var result = new List<int>();
                // fails with ERROR_MORE_DATA if there are more processes (the list is filled anyway)
                NativeMethods.QueryInformationJobObject(job, NativeMethods.JobObjectBasicProcessIdList, buffer, (uint)size, IntPtr.Zero);
                int nrOfProcessIds = Math.Min(Marshal.ReadInt32(buffer, sizeof(uint)), maxNrOfProcesses);
                for (int i = 0; i < nrOfProcessIds; i++)
                {
                    result.Add((int)Marshal.ReadIntPtr(buffer, 2 * sizeof(uint) + i * IntPtr.Size).ToInt64());
                }
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Body of the reader thread: reads the pipe until its end (also after <see cref="ResumeAndWaitForExit"/> has
        /// stopped waiting for it, so that processes still writing to it are not blocked), and reports complete lines.
        /// Line breaks are <c>\r\n</c>, <c>\n</c> or <c>\r</c> (as for <see cref="StreamReader.ReadLine"/>).
        /// </summary>
        private void ReadOutput()
        {
            FileStream stream;
            lock (_readerLock)
            {
                stream = _outputStream;
            }

            try
            {
                Decoder decoder = OutputEncoding.GetDecoder();
                var bytes = new byte[OutputBufferSize];
                var chars = new char[OutputEncoding.GetMaxCharCount(bytes.Length) + 4];
                var line = new StringBuilder();
                bool isFirstChar = true;
                bool lastCharWasCarriageReturn = false;

                while (true)
                {
                    lock (_readerLock)
                    {
                        _isReading = true;
                        _readStartedAt = Stopwatch.GetTimestamp();
                    }
                    int nrOfBytes;
                    try
                    {
                        nrOfBytes = stream.Read(bytes, 0, bytes.Length);
                    }
                    finally
                    {
                        lock (_readerLock)
                        {
                            _isReading = false;
                            if (_processHasExited)
                                _ticksBlockedAfterExit += Stopwatch.GetTimestamp() - Math.Max(_readStartedAt, _processExitedAt);
                        }
                    }

                    int nrOfChars = decoder.GetChars(bytes, 0, nrOfBytes, chars, 0, nrOfBytes == 0);
                    for (int i = 0; i < nrOfChars; i++)
                    {
                        char c = chars[i];
                        if (isFirstChar)
                        {
                            isFirstChar = false;
                            // byte order mark
                            if (c == '﻿')
                                continue;
                        }

                        if (lastCharWasCarriageReturn)
                        {
                            lastCharWasCarriageReturn = false;
                            if (c == '\n')
                                continue;
                        }

                        if (c == '\r' || c == '\n')
                        {
                            ReportLine(line.ToString());
                            line.Clear();
                            lastCharWasCarriageReturn = c == '\r';
                        }
                        else
                        {
                            line.Append(c);
                        }
                    }

                    if (nrOfBytes == 0)
                        break;
                }

                if (line.Length > 0)
                    ReportLine(line.ToString());
            }
            catch (Exception e)
            {
                bool reportingStopped;
                lock (_reportLock)
                {
                    reportingStopped = _reportingStopped;
                }
                if (!reportingStopped)
                    _logger.DebugWarning($"Could not read output of process {ProcessId}: {e.Message}");
            }
            finally
            {
                try
                {
                    stream.Dispose();
                }
                catch (Exception)
                {
                    // nothing to do
                }

                lock (_readerLock)
                {
                    _readerFinished = true;
                    Monitor.PulseAll(_readerLock);
                }
            }
        }

        private void ReportLine(string line)
        {
            lock (_reportLock)
            {
                if (_reportingStopped)
                    return;

                try
                {
                    _reportOutputLine?.Invoke(line);
                }
                catch (Exception e)
                {
                    // the output can not be processed any more
                    _reportException = e;
                    _reportingStopped = true;
                    Kill();
                }
            }
        }

        #endregion

        #region Native methods

        private sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            // used by the P/Invoke marshaller
            private SafeKernelHandle() : base(true) { }

            public SafeKernelHandle(IntPtr handle) : base(true)
            {
                SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }

        // ReSharper disable InconsistentNaming, FieldCanBeMadeReadOnly.Local, MemberCanBePrivate.Local
        private static class NativeMethods
        {
            public const uint INFINITE = 0xFFFFFFFF;
            public const uint WAIT_OBJECT_0 = 0;
            public const int STARTF_USESTDHANDLES = 0x00000100;
            public const uint CREATE_SUSPENDED = 0x00000004;
            public const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
            public const uint CREATE_NO_WINDOW = 0x08000000;
            public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
            public const uint HANDLE_FLAG_INHERIT = 0x00000001;
            public static readonly IntPtr PROC_THREAD_ATTRIBUTE_HANDLE_LIST = new IntPtr(0x00020002);
            public static readonly IntPtr PROC_THREAD_ATTRIBUTE_JOB_LIST = new IntPtr(0x0002000D);
            public const uint GENERIC_READ = 0x80000000;
            public const uint FILE_SHARE_READ = 0x00000001;
            public const uint FILE_SHARE_WRITE = 0x00000002;
            public const uint OPEN_EXISTING = 3;
            public const int JobObjectBasicProcessIdList = 3;
            public const int JobObjectExtendedLimitInformation = 9;
            public const uint JOB_OBJECT_LIMIT_BREAKAWAY_OK = 0x00000800;
            public const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

            [StructLayout(LayoutKind.Sequential)]
            public struct STARTUPINFO
            {
                public int cb;
                public IntPtr lpReserved;
                public IntPtr lpDesktop;
                public IntPtr lpTitle;
                public int dwX;
                public int dwY;
                public int dwXSize;
                public int dwYSize;
                public int dwXCountChars;
                public int dwYCountChars;
                public int dwFillAttribute;
                public int dwFlags;
                public short wShowWindow;
                public short cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct STARTUPINFOEX
            {
                public STARTUPINFO StartupInfo;
                public IntPtr lpAttributeList;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_INFORMATION
            {
                public IntPtr hProcess;
                public IntPtr hThread;
                public int dwProcessId;
                public int dwThreadId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                public long PerProcessUserTimeLimit;
                public long PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IO_COUNTERS
            {
                public ulong ReadOperationCount;
                public ulong WriteOperationCount;
                public ulong OtherOperationCount;
                public ulong ReadTransferCount;
                public ulong WriteTransferCount;
                public ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
                public IO_COUNTERS IoInfo;
                public UIntPtr ProcessMemoryLimit;
                public UIntPtr JobMemoryLimit;
                public UIntPtr PeakProcessMemoryUsed;
                public UIntPtr PeakJobMemoryUsed;
            }

            [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CreateProcess(
                string lpApplicationName, StringBuilder lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
                [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
                ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue,
                IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

            [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
                uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetHandleInformation(SafeHandle hObject, uint dwMask, uint dwFlags);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint ResumeThread(SafeKernelHandle hThread);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint WaitForSingleObject(SafeProcessHandle hHandle, uint dwMilliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetExitCodeProcess(SafeProcessHandle hProcess, out int lpExitCode);

            [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeKernelHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetInformationJobObject(SafeKernelHandle hJob, int jobObjectInfoClass,
                ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, uint cbJobObjectInfoLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool QueryInformationJobObject(SafeKernelHandle hJob, int jobObjectInfoClass, IntPtr lpJobObjectInfo,
                uint cbJobObjectInfoLength, IntPtr lpReturnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AssignProcessToJobObject(SafeKernelHandle hJob, SafeProcessHandle hProcess);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool TerminateJobObject(SafeKernelHandle hJob, uint uExitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);
        }
        // ReSharper restore InconsistentNaming, FieldCanBeMadeReadOnly.Local, MemberCanBePrivate.Local

        #endregion
    }
}
