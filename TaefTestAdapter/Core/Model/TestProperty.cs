// This file has been modified for TAEF support.

namespace TaefTestAdapter.Model
{
    /// <summary>
    /// A property of a test which is passed to the test framework (and back) in its string serialization.
    /// </summary>
    public abstract class TestProperty
    {
        /// <summary>The string serialization of the property.</summary>
        public string Serialization { get; }

        /// <summary>Creates a property with serialization <paramref name="serialization"/>.</summary>
        protected TestProperty(string serialization)
        {
            Serialization = serialization;
        }

        public override string ToString()
        {
            return $"{GetType()}: {Serialization}";
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;

            var other = (TestProperty) obj;
            return string.Equals(Serialization, other.Serialization);
        }

        public override int GetHashCode()
        {
            return Serialization != null ? Serialization.GetHashCode() : 0;
        }
    }
}