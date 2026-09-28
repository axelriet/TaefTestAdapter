// This file has been added for TAEF support.

using FluentAssertions;
using TaefTestAdapter.Model;

namespace TaefTestAdapter.Tests.Common.Assertions
{
    /// <summary>
    /// Assertions on test results of the core model.
    /// </summary>
    public static class TestResultAssertions
    {
        public static void AssertTestResultIsPassed(TestResult testResult)
        {
            testResult.Outcome.Should().Be(TestOutcome.Passed);
            testResult.ErrorMessage.Should().BeNullOrEmpty();
        }

        public static void AssertTestResultIsSkipped(TestResult testResult)
        {
            testResult.Outcome.Should().Be(TestOutcome.Skipped);
        }

        public static void AssertTestResultIsFailure(TestResult testResult)
        {
            testResult.Outcome.Should().Be(TestOutcome.Failed);
            testResult.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        /// <summary>Asserts a failed result whose error message equals <paramref name="errorMessage"/> (line endings are ignored).</summary>
        public static void AssertTestResultIsFailure(TestResult testResult, string errorMessage)
        {
            AssertTestResultIsFailure(testResult);
            testResult.ErrorMessage.Replace("\r\n", "\n").Should().Be(errorMessage.Replace("\r\n", "\n"));
        }
    }
}
