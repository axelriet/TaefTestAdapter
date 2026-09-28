// This file has been modified for TAEF support.

using System;
using System.Diagnostics;
using System.Threading;

namespace TaefTestAdapter.Common
{

    /// <summary>
    /// Waits for the exit of a process (using its <see cref="System.Diagnostics.Process.Exited"/> event).
    /// </summary>
    public class ProcessWaiter
    {
        public int ProcessExitCode { get; private set; } = -1;
        private bool _exited;


        public ProcessWaiter(Process process)
        {
            process.EnableRaisingEvents = true;
            process.Exited += OnExited;
        }


        public int WaitForExit()
        {
            lock (this)
            {
                while (!_exited)
                {
                    Monitor.Wait(this);
                }
            }

            return ProcessExitCode;
        }

        private void OnExited(object sender, EventArgs e)
        {
            if (sender is Process process)
            {
                lock (this)
                {
                    ProcessExitCode = process.ExitCode;
                    _exited = true;

                    process.Exited -= OnExited;

                    Monitor.Pulse(this);
                }
            }
        }

    }

}