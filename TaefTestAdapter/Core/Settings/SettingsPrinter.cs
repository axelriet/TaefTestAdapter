// This file has been modified for TAEF support.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TaefTestAdapter.Settings
{
    /// <summary>
    /// Prints the effective settings (for the debug output).
    /// </summary>
    public class SettingsPrinter
    {
        private static readonly string[] NotPrintedProperties =
        {
            nameof(SettingsWrapper.RegexTraitParser),
            nameof(SettingsWrapper.EnvironmentVariablesParser),
            nameof(SettingsWrapper.HelperFilesCache),
            nameof(SettingsWrapper.DebuggingNamedPipeId),
            nameof(SettingsWrapper.SolutionDir)
        };

        private static readonly PropertyInfo[] PropertiesToPrint = typeof(SettingsWrapper)
            .GetProperties()
            .Where(pi => !NotPrintedProperties.Contains(pi.Name))
            .OrderBy(p => p.Name)
            .ToArray();


        private readonly SettingsWrapper _settings;

        public SettingsPrinter(SettingsWrapper settings)
        {
            _settings = settings;
        }

        public string ToReadableString()
        {
            return string.Join(", ", PropertiesToPrint.Select(ToString));
        }

        private string ToString(PropertyInfo propertyInfo)
        {
            var value = propertyInfo.GetValue(_settings);
            if (value is string)
                return $"{propertyInfo.Name}: '{value}'";

            if (value is IEnumerable<RegexTraitPair> pairs)
                return $"{propertyInfo.Name}: {{{string.Join(", ", pairs)}}}";

            return $"{propertyInfo.Name}: {value}";
        }

    }
}
