// This file has been added for TAEF support.

using System;

namespace TaefTestAdapter.Model
{
    /// <summary>The class of a managed test as listed by TAEF, including its class data row.</summary>
    public class TestCaseTaefClassProperty : TestProperty
    {
        public static readonly string Id = typeof(TestCaseTaefClassProperty).FullName;
        public const string Label = "Managed TAEF class";

        public string ClassName => Serialization;

        public TestCaseTaefClassProperty(string className) : base(className)
        {
            if (string.IsNullOrEmpty(className))
                throw new ArgumentException("The managed TAEF class must not be empty", nameof(className));
        }
    }
}
