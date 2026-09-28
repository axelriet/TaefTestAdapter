// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using TaefTestAdapter.Common;
using TaefTestAdapter.Settings;
using System;
using System.Collections.Generic;

namespace TaefTestAdapter.Helpers
{
    /// <summary>
    /// Parses the options 'Before test discovery' and 'After test discovery' (traits assigned to tests whose TAEF name
    /// matches a regex). Syntax: <c>&lt;regex&gt;///&lt;trait name&gt;,&lt;trait value&gt;</c>, several pairs separated by
    /// <c>//||//</c>, e.g. <c>MyClass::.*///Type,Small//||//MyOtherClass::.*///Type,Medium</c>. The trait value may
    /// contain commas.
    /// </summary>
    public class RegexTraitParser
    {
        private readonly ILogger _logger;

        public RegexTraitParser(ILogger logger)
        {
            _logger = logger;
        }

        /// <param name="option">The option value</param>
        /// <param name="ignoreErrors">If true, invalid pairs are logged as errors and skipped; otherwise, an exception is thrown.</param>
        public List<RegexTraitPair> ParseTraitsRegexesString(string option, bool ignoreErrors = true)
        {
            var result = new List<RegexTraitPair>();
            if (string.IsNullOrEmpty(option))
                return result;

            string[] pairs = option.Split(
                new[] { SettingsWrapper.TraitsRegexesPairSeparator },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string pair in pairs)
            {
                try
                {
                    result.Add(ParseRegexTraitPair(pair));
                }
                catch (Exception e)
                {
                    string message = "Could not parse pair '" + pair + "', exception message: " + e.Message;
                    if (ignoreErrors)
                        _logger?.LogError(message);
                    else
                        throw new Exception(message, e);
                }
            }

            return result;
        }

        private RegexTraitPair ParseRegexTraitPair(string pair)
        {
            int regexSeparatorIndex = pair.IndexOf(SettingsWrapper.TraitsRegexesRegexSeparator, StringComparison.Ordinal);
            if (regexSeparatorIndex < 0)
                throw new FormatException($"Separator '{SettingsWrapper.TraitsRegexesRegexSeparator}' between regex and trait is missing");
            string regex = pair.Substring(0, regexSeparatorIndex);
            string trait = pair.Substring(regexSeparatorIndex + SettingsWrapper.TraitsRegexesRegexSeparator.Length);

            int traitSeparatorIndex = trait.IndexOf(SettingsWrapper.TraitsRegexesTraitSeparator, StringComparison.Ordinal);
            if (traitSeparatorIndex < 0)
                throw new FormatException($"Separator '{SettingsWrapper.TraitsRegexesTraitSeparator}' between trait name and value is missing");
            string traitName = trait.Substring(0, traitSeparatorIndex);
            string traitValue = trait.Substring(traitSeparatorIndex + SettingsWrapper.TraitsRegexesTraitSeparator.Length);
            if (string.IsNullOrWhiteSpace(traitName))
                throw new FormatException("Trait name is empty");

            Utils.ValidateRegex(regex);
            return new RegexTraitPair(regex, traitName, traitValue);
        }

    }

}
