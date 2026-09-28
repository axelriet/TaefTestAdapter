// This file has been added for TAEF support.

using System;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// A TAEF metadata property (<c>Property[Name] =  Value</c>) or data value (<c>Data[Name] = Value</c>) as listed by
    /// <c>TE.exe /listProperties</c>.
    /// </summary>
    public class TaefProperty
    {
        public string Name { get; }
        public string Value { get; }

        public TaefProperty(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? "";
        }

        public override string ToString()
        {
            return $"{Name}={Value}";
        }
    }

}
