// This file has been added for TAEF support.

using System;

namespace TaefTestAdapter.TestCases
{

    /// <summary>The scope a TAEF fixture, property or data value belongs to.</summary>
    public enum TaefScope { Module, Class, Test }

    /// <summary>
    /// The kind of a TAEF fixture.
    /// </summary>
    public enum TaefFixtureKind { Setup, Teardown }

    /// <summary>
    /// A setup or teardown method of a module, class or test as listed by <c>TE.exe /listProperties</c>
    /// (<c>Setup: MethodSetup</c>, <c>Teardown: MethodCleanup</c>).
    /// </summary>
    public class TaefFixture
    {
        public TaefScope Scope { get; }
        public TaefFixtureKind Kind { get; }

        /// <summary>The name of the fixture method as listed by TE.exe (without class name).</summary>
        public string Name { get; }

        public TaefFixture(TaefScope scope, TaefFixtureKind kind, string name)
        {
            Scope = scope;
            Kind = kind;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public override string ToString()
        {
            return $"{Scope} {Kind}: {Name}";
        }
    }

}
