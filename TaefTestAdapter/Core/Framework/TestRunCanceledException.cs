// This file has been modified for TAEF support.

using System;

namespace TaefTestAdapter.Framework
{
    /// <summary>
    /// Thrown if a test run has been canceled.
    /// </summary>
    [Serializable]
    public sealed class TestRunCanceledException : Exception
    {
        public TestRunCanceledException(string message, Exception innerException) : base(message, innerException) { }
    }
}