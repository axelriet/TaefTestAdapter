// This file has been added for TAEF support.

using System;

namespace TaefTestAdapter.Model
{
    /// <summary>The managed execution group supplied by TAEF before display trait transformations.</summary>
    public class TestCaseExecutionGroupProperty : TestProperty
    {
        public static readonly string Id = typeof(TestCaseExecutionGroupProperty).FullName;
        public const string Label = "TAEF execution group";

        public string Name => Serialization;

        public TestCaseExecutionGroupProperty(string name) : base(name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("The TAEF execution group must not be empty", nameof(name));
        }
    }
}
