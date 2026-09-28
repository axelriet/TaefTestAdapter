// This file has been modified for TAEF support.

using System;

namespace TaefTestAdapter.Model
{
    /// <summary>
    /// The outcome of a test.
    /// </summary>
    public enum TestOutcome
    {
        /// <summary>The test passed.</summary>
        Passed,
        /// <summary>The test failed (also TAEF's Blocked, and crashed tests).</summary>
        Failed,
        /// <summary>The test was skipped (also ignored tests).</summary>
        Skipped,
        /// <summary>The test has no outcome (TAEF's NotRun).</summary>
        None,
        /// <summary>No result was reported for the test.</summary>
        NotFound
    }

    /// <summary>
    /// The result of one execution of a test.
    /// </summary>
    public class TestResult
    {
        /// <summary>The test.</summary>
        public TestCase TestCase { get; }

        /// <summary>The computer the test ran on.</summary>
        public string ComputerName { get; set; }
        /// <summary>The name shown to the user.</summary>
        public string DisplayName { get; set; }

        /// <summary>The outcome.</summary>
        public TestOutcome Outcome { get; set; }
        /// <summary>The error message(s) of a failed, blocked or skipped test, or null.</summary>
        public string ErrorMessage { get; set; }
        /// <summary>One clickable entry per error with source information, or null.</summary>
        public string ErrorStackTrace { get; set; }
        /// <summary>The duration of the test as measured by the adapter.</summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Output of the test (the lines TE.exe printed within the test's StartGroup/EndGroup frame, excluding the
        /// framing lines, preceded by the output of failed or crashed setup fixtures which affected the test), or null if
        /// there is none.
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// Creates a result of <paramref name="testCase"/>.
        /// </summary>
        public TestResult(TestCase testCase)
        {
            TestCase = testCase;
        }

        public override string ToString()
        {
            return $"{DisplayName} ({Outcome})";
        }

    }

}