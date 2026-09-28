// This file has been modified for TAEF support.

using TaefTestAdapter.Model;

namespace TaefTestAdapter.Settings
{
    /// <summary>
    /// A trait which is assigned to the tests whose names match a regex (options TraitsRegexesBefore/After).
    /// </summary>
    public class RegexTraitPair
    {
        public string Regex { get; }
        public Trait Trait { get; }

        public RegexTraitPair(string regex, string name, string value)
        {
            Regex = regex;
            Trait = new Trait(name, value);
        }

        public override string ToString()
        {
            return $"'{Regex}': {Trait}";
        }
    }
}