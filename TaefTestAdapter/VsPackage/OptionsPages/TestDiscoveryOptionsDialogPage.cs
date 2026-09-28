// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.ComponentModel;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Settings;
// ReSharper disable LocalizableElement

namespace TaefTestAdapter.VsPackage.OptionsPages
{

    /// <summary>
    /// Options page Tools/Options/Test Adapter for TAEF/Test Discovery.
    /// </summary>
    public class TestDiscoveryOptionsDialogPage : NotifyingDialogPage
    {
        #region Misc

        [Category(SettingsWrapper.CategoryMiscName)]
        [DisplayName(SettingsWrapper.OptionTestDiscoveryRegex)]
        [Description(SettingsWrapper.OptionTestDiscoveryRegexDescription)]
        public string TestDiscoveryRegex
        {
            get => _testDiscoveryRegex;
            set
            {
                Utils.ValidateRegex(value);
                SetAndNotify(ref _testDiscoveryRegex, value);
            }
        }
        private string _testDiscoveryRegex = SettingsWrapper.OptionTestDiscoveryRegexDefaultValue;

        [Category(SettingsWrapper.CategoryMiscName)]
        [DisplayName(SettingsWrapper.OptionTestDiscoveryTimeoutInSeconds)]
        [Description(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDescription)]
        public int TestDiscoveryTimeoutInSeconds
        {
            get => _testDiscoveryTimeoutInSeconds;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(TestDiscoveryTimeoutInSeconds), value, "Expected a number greater than or equal to 0.");
                SetAndNotify(ref _testDiscoveryTimeoutInSeconds, value);
            }
        }
        private int _testDiscoveryTimeoutInSeconds = SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue;

        [Category(SettingsWrapper.CategoryMiscName)]
        [DisplayName(SettingsWrapper.OptionParseSymbolInformation)]
        [Description(SettingsWrapper.OptionParseSymbolInformationDescription)]
        public bool ParseSymbolInformation
        {
            get => _parseSymbolInformation;
            set => SetAndNotify(ref _parseSymbolInformation, value);
        }
        private bool _parseSymbolInformation = SettingsWrapper.OptionParseSymbolInformationDefaultValue;

        #endregion

        #region Traits

        [Category(SettingsWrapper.CategoryTraitsName)]
        [DisplayName(SettingsWrapper.OptionTraitsRegexesBefore)]
        [Description(SettingsWrapper.OptionTraitsDescription)]
        public string TraitsRegexesBefore
        {
            get => _traitsRegexesBefore;
            set
            {
                Utils.ValidateTraitRegexes(value);
                SetAndNotify(ref _traitsRegexesBefore, value);
            }
        }
        private string _traitsRegexesBefore = SettingsWrapper.OptionTraitsRegexesDefaultValue;

        [Category(SettingsWrapper.CategoryTraitsName)]
        [DisplayName(SettingsWrapper.OptionTraitsRegexesAfter)]
        [Description(SettingsWrapper.OptionTraitsDescription)]
        public string TraitsRegexesAfter
        {
            get => _traitsRegexesAfter;
            set
            {
                Utils.ValidateTraitRegexes(value);
                SetAndNotify(ref _traitsRegexesAfter, value);
            }
        }
        private string _traitsRegexesAfter = SettingsWrapper.OptionTraitsRegexesDefaultValue;

        #endregion

    }

}