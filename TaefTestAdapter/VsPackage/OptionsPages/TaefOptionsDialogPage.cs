// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Shell;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Settings;
// ReSharper disable LocalizableElement

namespace TaefTestAdapter.VsPackage.OptionsPages
{

    /// <summary>
    /// Options page Tools/Options/Test Adapter for TAEF/TAEF: options which are specific to TAEF and TE.exe.
    /// </summary>
    public class TaefOptionsDialogPage : NotifyingDialogPage
    {
        #region TE.exe

        [Category(SettingsWrapper.CategoryTeExecutableName)]
        [DisplayName(SettingsWrapper.OptionTeExecutable)]
        [Description(SettingsWrapper.OptionTeExecutableDescription)]
        public string TeExecutable
        {
            get => _teExecutable;
            set => SetAndNotify(ref _teExecutable, value?.Trim() ?? SettingsWrapper.OptionTeExecutableDefaultValue);
        }
        private string _teExecutable = SettingsWrapper.OptionTeExecutableDefaultValue;

        #endregion

        #region Test execution

        [Category(SettingsWrapper.CategoryTestExecutionName)]
        [DisplayName(SettingsWrapper.OptionRunIgnoredTests)]
        [Description(SettingsWrapper.OptionRunIgnoredTestsDescription)]
        public bool RunIgnoredTests
        {
            get => _runIgnoredTests;
            set => SetAndNotify(ref _runIgnoredTests, value);
        }
        private bool _runIgnoredTests = SettingsWrapper.OptionRunIgnoredTestsDefaultValue;

        [Category(SettingsWrapper.CategoryTestExecutionName)]
        [DisplayName(SettingsWrapper.OptionNrOfTestRepetitions)]
        [Description(SettingsWrapper.OptionNrOfTestRepetitionsDescription)]
        public int NrOfTestRepetitions
        {
            get => _nrOfTestRepetitions;
            set
            {
                if (value < SettingsWrapper.OptionNrOfTestRepetitionsMinValue)
                    throw new ArgumentOutOfRangeException(nameof(NrOfTestRepetitions), value,
                        $"Expected a number greater than or equal to {SettingsWrapper.OptionNrOfTestRepetitionsMinValue}.");
                SetAndNotify(ref _nrOfTestRepetitions, value);
            }
        }
        private int _nrOfTestRepetitions = SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue;

        [Category(SettingsWrapper.CategoryTestExecutionName)]
        [DisplayName(SettingsWrapper.OptionTestTimeout)]
        [Description(SettingsWrapper.OptionTestTimeoutDescription)]
        public string TestTimeout
        {
            get => _testTimeout;
            set
            {
                Utils.ValidateTestTimeout(value);
                SetAndNotify(ref _testTimeout, value?.Trim() ?? SettingsWrapper.OptionTestTimeoutDefaultValue);
            }
        }
        private string _testTimeout = SettingsWrapper.OptionTestTimeoutDefaultValue;

        #endregion

        #region Runtime behavior

        [Category(SettingsWrapper.CategoryRuntimeBehaviorName)]
        [DisplayName(SettingsWrapper.OptionRunInProcess)]
        [Description(SettingsWrapper.OptionRunInProcessDescription)]
        public bool RunInProcess
        {
            get => _runInProcess;
            set => SetAndNotify(ref _runInProcess, value);
        }
        private bool _runInProcess = SettingsWrapper.OptionRunInProcessDefaultValue;

        [Category(SettingsWrapper.CategoryRuntimeBehaviorName)]
        [DisplayName(SettingsWrapper.OptionIsolationLevel)]
        [Description(SettingsWrapper.OptionIsolationLevelDescription)]
        [PropertyPageTypeConverter(typeof(TaefIsolationLevelConverter))]
        [TypeConverter(typeof(TaefIsolationLevelConverter))]
        public TaefIsolationLevel IsolationLevel
        {
            get => _isolationLevel;
            set => SetAndNotify(ref _isolationLevel, value);
        }
        private TaefIsolationLevel _isolationLevel = SettingsWrapper.OptionIsolationLevelDefaultValue;

        [Category(SettingsWrapper.CategoryRuntimeBehaviorName)]
        [DisplayName(SettingsWrapper.OptionBreakOnError)]
        [Description(SettingsWrapper.OptionBreakOnErrorDescription)]
        public bool BreakOnError
        {
            get => _breakOnError;
            set => SetAndNotify(ref _breakOnError, value);
        }
        private bool _breakOnError = SettingsWrapper.OptionBreakOnErrorDefaultValue;

        #endregion
    }

}
