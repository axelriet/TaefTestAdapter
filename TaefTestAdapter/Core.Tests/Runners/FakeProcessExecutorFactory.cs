// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution.Contracts;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// One (fake or real) process execution requested from a <see cref="FakeProcessExecutorFactory"/> or a
    /// <see cref="RecordingProcessExecutorFactory"/>.
    /// </summary>
    public class RecordedExecution
    {
        public const string KindNormal = "Normal";
        public const string KindFrameworkDebugger = "FrameworkDebugger";
        public const string KindNativeDebugger = "NativeDebugger";

        /// <summary><see cref="KindNormal"/>, <see cref="KindFrameworkDebugger"/> or <see cref="KindNativeDebugger"/>.</summary>
        public string Kind { get; set; }
        public DebuggerEngine? DebuggerEngine { get; set; }
        public bool PrintTestOutput { get; set; }

        public string Command { get; set; }
        public string Parameters { get; set; }
        public string WorkingDir { get; set; }
        public string PathExtension { get; set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; }

        /// <summary>True if the caller wanted to receive the output lines (i.e., passed a non-null reportOutputLine).</summary>
        public bool IsOutputRequested { get; set; }

        public bool IsCanceled { get; set; }

        /// <summary>Output lines reported by the (real) process (recorded by <see cref="RecordingProcessExecutorFactory"/> only).</summary>
        public List<string> Output { get; } = new List<string>();

        /// <summary>Exit code of the (real) process (recorded by <see cref="RecordingProcessExecutorFactory"/> only).</summary>
        public int? ExitCode { get; set; }

        /// <summary>Length of the complete command line (quoted command, a space, and the parameters).</summary>
        public int CommandLineLength => Command.Length + 3 + Parameters.Length;

        public override string ToString() => $"{Kind}: \"{Command}\" {Parameters}";
    }

    /// <summary>What a fake process "does" when it is executed.</summary>
    public class FakeProcessBehavior
    {
        /// <summary>Lines reported as output (if the caller requested output).</summary>
        public IList<string> Output { get; set; } = new List<string>();

        public int ExitCode { get; set; }

        /// <summary>Called before the output is reported (e.g. to write files the process would write).</summary>
        public Action<RecordedExecution> BeforeOutput { get; set; }

        /// <summary>Called after the output has been reported, before the exit code is returned.</summary>
        public Action<RecordedExecution> AfterOutput { get; set; }

        /// <summary>If not null, thrown by ExecuteCommandBlocking (after <see cref="BeforeOutput"/>).</summary>
        public Exception Exception { get; set; }

        public FakeProcessBehavior() { }

        public FakeProcessBehavior(IEnumerable<string> output, int exitCode = 0)
        {
            Output = output.ToList();
            ExitCode = exitCode;
        }
    }

    /// <summary>
    /// Creates process executors which do not start any process: each execution is recorded (<see cref="Executions"/>)
    /// and its output and exit code are taken from <see cref="Behavior"/>. Thread-safe.
    /// </summary>
    public class FakeProcessExecutorFactory : IDebuggedProcessExecutorFactory
    {
        private readonly object _lock = new object();
        private readonly List<RecordedExecution> _executions = new List<RecordedExecution>();

        /// <summary>Computes the behavior of an execution (default: no output, exit code 0).</summary>
        public Func<RecordedExecution, FakeProcessBehavior> Behavior { get; set; } = e => new FakeProcessBehavior();

        /// <summary>Snapshot of all executions so far.</summary>
        public IList<RecordedExecution> Executions { get { lock (_lock) return _executions.ToList(); } }

        public IProcessExecutor CreateExecutor(bool printTestOutput, ILogger logger)
            => new FakeProcessExecutor(this, RecordedExecution.KindNormal, null, printTestOutput);

        public IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, ILogger logger)
            => new FakeProcessExecutor(this, RecordedExecution.KindFrameworkDebugger, null, printTestOutput);

        public IDebuggedProcessExecutor CreateNativeDebuggingExecutor(DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger)
            => new FakeProcessExecutor(this, RecordedExecution.KindNativeDebugger, debuggerEngine, printTestOutput);

        private void Add(RecordedExecution execution)
        {
            lock (_lock)
                _executions.Add(execution);
        }

        private class FakeProcessExecutor : IDebuggedProcessExecutor
        {
            private readonly FakeProcessExecutorFactory _factory;
            private readonly string _kind;
            private readonly DebuggerEngine? _debuggerEngine;
            private readonly bool _printTestOutput;
            private RecordedExecution _currentExecution;

            public FakeProcessExecutor(FakeProcessExecutorFactory factory, string kind, DebuggerEngine? debuggerEngine, bool printTestOutput)
            {
                _factory = factory;
                _kind = kind;
                _debuggerEngine = debuggerEngine;
                _printTestOutput = printTestOutput;
            }

            public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension,
                IDictionary<string, string> environmentVariables, Action<string> reportOutputLine)
            {
                var execution = new RecordedExecution
                {
                    Kind = _kind,
                    DebuggerEngine = _debuggerEngine,
                    PrintTestOutput = _printTestOutput,
                    Command = command,
                    Parameters = parameters,
                    WorkingDir = workingDir,
                    PathExtension = pathExtension,
                    EnvironmentVariables = environmentVariables == null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string>(environmentVariables),
                    IsOutputRequested = reportOutputLine != null
                };
                _currentExecution = execution;
                _factory.Add(execution);

                FakeProcessBehavior behavior = _factory.Behavior(execution) ?? new FakeProcessBehavior();
                behavior.BeforeOutput?.Invoke(execution);
                if (behavior.Exception != null)
                    throw behavior.Exception;

                if (reportOutputLine != null)
                {
                    foreach (string line in behavior.Output)
                        reportOutputLine(line);
                }

                behavior.AfterOutput?.Invoke(execution);
                return behavior.ExitCode;
            }

            public void Cancel()
            {
                if (_currentExecution != null)
                    _currentExecution.IsCanceled = true;
            }
        }
    }

    /// <summary>
    /// Decorates a real process executor factory: all executions are recorded (<see cref="Executions"/>) before they are
    /// passed on. The debugging executors are not supported.
    /// </summary>
    public class RecordingProcessExecutorFactory : IDebuggedProcessExecutorFactory
    {
        private readonly object _lock = new object();
        private readonly List<RecordedExecution> _executions = new List<RecordedExecution>();
        private readonly IProcessExecutorFactory _innerFactory;

        public RecordingProcessExecutorFactory(IProcessExecutorFactory innerFactory)
        {
            _innerFactory = innerFactory;
        }

        /// <summary>Snapshot of all executions so far.</summary>
        public IList<RecordedExecution> Executions { get { lock (_lock) return _executions.ToList(); } }

        public IProcessExecutor CreateExecutor(bool printTestOutput, ILogger logger)
            => new RecordingProcessExecutor(this, _innerFactory.CreateExecutor(printTestOutput, logger), printTestOutput);

        public IDebuggedProcessExecutor CreateFrameworkDebuggingExecutor(bool printTestOutput, ILogger logger)
            => throw new NotSupportedException();

        public IDebuggedProcessExecutor CreateNativeDebuggingExecutor(DebuggerEngine debuggerEngine, bool printTestOutput, ILogger logger)
            => throw new NotSupportedException();

        private class RecordingProcessExecutor : IProcessExecutor
        {
            private readonly RecordingProcessExecutorFactory _factory;
            private readonly IProcessExecutor _innerExecutor;
            private readonly bool _printTestOutput;
            private RecordedExecution _currentExecution;

            public RecordingProcessExecutor(RecordingProcessExecutorFactory factory, IProcessExecutor innerExecutor, bool printTestOutput)
            {
                _factory = factory;
                _innerExecutor = innerExecutor;
                _printTestOutput = printTestOutput;
            }

            public int ExecuteCommandBlocking(string command, string parameters, string workingDir, string pathExtension,
                IDictionary<string, string> environmentVariables, Action<string> reportOutputLine)
            {
                var execution = new RecordedExecution
                {
                    Kind = RecordedExecution.KindNormal,
                    PrintTestOutput = _printTestOutput,
                    Command = command,
                    Parameters = parameters,
                    WorkingDir = workingDir,
                    PathExtension = pathExtension,
                    EnvironmentVariables = environmentVariables == null
                        ? new Dictionary<string, string>()
                        : new Dictionary<string, string>(environmentVariables),
                    IsOutputRequested = reportOutputLine != null
                };
                _currentExecution = execution;
                lock (_factory._lock)
                    _factory._executions.Add(execution);

                void ReportOutputLine(string line)
                {
                    lock (execution.Output)
                        execution.Output.Add(line);
                    reportOutputLine?.Invoke(line);
                }

                int exitCode = _innerExecutor.ExecuteCommandBlocking(command, parameters, workingDir, pathExtension, environmentVariables,
                    reportOutputLine == null ? (Action<string>) null : ReportOutputLine);
                execution.ExitCode = exitCode;
                return exitCode;
            }

            public void Cancel()
            {
                if (_currentExecution != null)
                    _currentExecution.IsCanceled = true;
                _innerExecutor.Cancel();
            }
        }
    }

}
