// This file has been modified for TAEF support.

namespace TaefTestAdapter.Model
{
    /// <summary>
    /// A trait of a test (a name/value pair, e.g. a TAEF property).
    /// </summary>
    public class Trait
    {
        /// <summary>The name of the trait.</summary>
        public string Name { get; }
        /// <summary>The value of the trait.</summary>
        public string Value { get; }

        /// <summary>Creates a trait.</summary>
        public Trait(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString()
        {
            return $"({Name},{Value})";
        }

    }

}