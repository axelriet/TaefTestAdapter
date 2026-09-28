// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Common;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Settings;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.OptionsPages
{
    /// <summary>
    /// Tests of the options pages General, Test Discovery and Test Execution, and of properties common to all pages
    /// (the TAEF page is tested by <see cref="TaefOptionsDialogPageTests"/>).
    /// </summary>
    [TestClass]
    public class OptionsPagesTests
    {
        private static readonly Type[] AllPageTypes =
        {
            typeof(GeneralOptionsDialogPage), typeof(TestDiscoveryOptionsDialogPage),
            typeof(TestExecutionOptionsDialogPage), typeof(TaefOptionsDialogPage)
        };

        /// <returns>The options of a page, i.e. the public properties declared by the page class itself.</returns>
        internal static PropertyInfo[] GetOptions(Type pageType)
        {
            return pageType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        }

        #region All pages

        [TestMethod]
        [TestCategory(Unit)]
        public void AllPages_Options_HaveCategoryDisplayNameAndDescription()
        {
            foreach (Type pageType in AllPageTypes)
            {
                PropertyInfo[] options = GetOptions(pageType);
                options.Should().NotBeEmpty();
                foreach (PropertyInfo option in options)
                {
                    string because = $"option {pageType.Name}.{option.Name} is shown in Tools/Options";
                    option.GetCustomAttribute<CategoryAttribute>()?.Category.Should().NotBeNullOrWhiteSpace(because);
                    option.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName.Should().NotBeNullOrWhiteSpace(because);
                    option.GetCustomAttribute<DescriptionAttribute>()?.Description.Should().NotBeNullOrWhiteSpace(because);
                    option.GetCustomAttribute<CategoryAttribute>().Should().NotBeNull(because);
                    option.GetCustomAttribute<DisplayNameAttribute>().Should().NotBeNull(because);
                    option.GetCustomAttribute<DescriptionAttribute>().Should().NotBeNull(because);
                    option.CanRead.Should().BeTrue(because);
                    option.CanWrite.Should().BeTrue(because);
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllPages_Texts_UseTaefTerminology()
        {
            foreach (Type pageType in AllPageTypes)
            {
                foreach (PropertyInfo option in GetOptions(pageType))
                {
                    string texts = string.Join(Environment.NewLine,
                        option.GetCustomAttribute<CategoryAttribute>()?.Category,
                        option.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName,
                        option.GetCustomAttribute<DescriptionAttribute>()?.Description);
                    foreach (string forbidden in new[] { "executable", "suite", "parameterized" })
                    {
                        texts.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase).Should().Be(-1,
                            $"option {pageType.Name}.{option.Name} should not contain '{forbidden}': options are about TAEF test DLLs and classes");
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllPages_DisplayNames_AreUnique()
        {
            AllPageTypes
                .SelectMany(GetOptions)
                .Select(o => o.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName)
                .Should().OnlyHaveUniqueItems();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllPages_EnumOptions_HavePropertyPageTypeConverter()
        {
            // without such a converter, VS persists enum options by their display names
            foreach (Type pageType in AllPageTypes)
            {
                foreach (PropertyInfo option in GetOptions(pageType).Where(o => o.PropertyType.IsEnum && o.PropertyType != typeof(OutputMode)))
                {
                    option.GetCustomAttribute<PropertyPageTypeConverterAttribute>()
                        .Should().NotBeNull($"enum option {pageType.Name}.{option.Name} should be persisted with a converter");
                }
            }
        }

        #endregion

        #region General

        [TestMethod]
        [TestCategory(Unit)]
        public void GeneralPage_Constructor_OptionsHaveDefaultValues()
        {
            var page = OptionsPageFactory.Create<GeneralOptionsDialogPage>();

            page.PrintTestOutput.Should().Be(SettingsWrapper.OptionPrintTestOutputDefaultValue);
            page.OutputMode.Should().Be(SettingsWrapper.OptionOutputModeDefaultValue);
            page.TimestampMode.Should().Be(SettingsWrapper.OptionTimestampModeDefaultValue);
            page.SeverityMode.Should().Be(SettingsWrapper.OptionSeverityModeDefaultValue);
            page.SummaryMode.Should().Be(SettingsWrapper.OptionSummaryModeDefaultValue);
            page.PrefixOutputWithTaef.Should().Be(SettingsWrapper.OptionPrefixOutputWithTaefDefaultValue);
            page.SkipOriginCheck.Should().Be(SettingsWrapper.OptionSkipOriginCheckDefaultValue).And.BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GeneralPage_PrefixOutputWithTaef_DisplayNameMentionsTaefPrefix()
        {
            PropertyDescriptor property = TypeDescriptor.GetProperties(typeof(GeneralOptionsDialogPage))[nameof(GeneralOptionsDialogPage.PrefixOutputWithTaef)];

            property.DisplayName.Should().Be(SettingsWrapper.OptionPrefixOutputWithTaef).And.Contain("[TAEF]");
        }

        #endregion

        #region Test Discovery

        [TestMethod]
        [TestCategory(Unit)]
        public void TestDiscoveryPage_Constructor_OptionsHaveDefaultValues()
        {
            var page = OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>();

            page.TestDiscoveryRegex.Should().Be(SettingsWrapper.OptionTestDiscoveryRegexDefaultValue);
            page.TestDiscoveryTimeoutInSeconds.Should().Be(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue);
            page.ParseSymbolInformation.Should().Be(SettingsWrapper.OptionParseSymbolInformationDefaultValue);
            page.TraitsRegexesBefore.Should().Be(SettingsWrapper.OptionTraitsRegexesDefaultValue);
            page.TraitsRegexesAfter.Should().Be(SettingsWrapper.OptionTraitsRegexesDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestDiscoveryPage_TestDiscoveryRegex_IsValidated()
        {
            var page = OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>();

            page.TestDiscoveryRegex = @".*_taef\.dll";
            page.TestDiscoveryRegex.Should().Be(@".*_taef\.dll");

            Action setting = () => page.TestDiscoveryRegex = "(unclosed";
            setting.Should().Throw<Exception>().Which.Message.Should().Contain("(unclosed");
            page.TestDiscoveryRegex.Should().Be(@".*_taef\.dll");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestDiscoveryPage_TestDiscoveryTimeoutInSeconds_MustNotBeNegative()
        {
            var page = OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>();

            Action setting = () => page.TestDiscoveryTimeoutInSeconds = -1;
            setting.Should().Throw<ArgumentOutOfRangeException>();
            page.TestDiscoveryTimeoutInSeconds.Should().Be(SettingsWrapper.OptionTestDiscoveryTimeoutInSecondsDefaultValue);

            page.TestDiscoveryTimeoutInSeconds = 0;
            page.TestDiscoveryTimeoutInSeconds.Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestDiscoveryPage_TraitsRegexes_AreValidated()
        {
            var page = OptionsPageFactory.Create<TestDiscoveryOptionsDialogPage>();
            string validValue = "TaefSamples::TestMath::.*" + SettingsWrapper.TraitsRegexesRegexSeparator + "Type" + SettingsWrapper.TraitsRegexesTraitSeparator + "Small";

            page.TraitsRegexesBefore = validValue;
            page.TraitsRegexesAfter = validValue;
            page.TraitsRegexesBefore.Should().Be(validValue);
            page.TraitsRegexesAfter.Should().Be(validValue);

            Action settingBefore = () => page.TraitsRegexesBefore = "TaefSamples::TestMath::.*";
            Action settingAfter = () => page.TraitsRegexesAfter = "TaefSamples::TestMath::.*" + SettingsWrapper.TraitsRegexesRegexSeparator + "Type";
            settingBefore.Should().Throw<Exception>();
            settingAfter.Should().Throw<Exception>();
            page.TraitsRegexesBefore.Should().Be(validValue);
            page.TraitsRegexesAfter.Should().Be(validValue);
        }

        #endregion

        #region Test Execution

        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutionPage_Constructor_OptionsHaveDefaultValues()
        {
            var page = OptionsPageFactory.Create<TestExecutionOptionsDialogPage>();

            page.ParallelTestExecution.Should().Be(SettingsWrapper.OptionParallelTestExecutionDefaultValue);
            page.MaxNrOfThreads.Should().Be(SettingsWrapper.OptionMaxNrOfThreadsDefaultValue);
            page.AdditionalPdbs.Should().Be(SettingsWrapper.OptionAdditionalPdbsDefaultValue);
            page.WorkingDir.Should().Be(SettingsWrapper.OptionWorkingDirDefaultValue).And.Be("$(TestDllDir)");
            page.PathExtension.Should().Be(SettingsWrapper.OptionPathExtensionDefaultValue);
            page.EnvironmentVariables.Should().Be(SettingsWrapper.OptionEnvironmentVariablesDefaultValue);
            page.AdditionalTestExecutionParam.Should().Be(SettingsWrapper.OptionAdditionalTestExecutionParamDefaultValue);
            page.BatchForTestSetup.Should().Be(SettingsWrapper.OptionBatchForTestSetupDefaultValue);
            page.BatchForTestTeardown.Should().Be(SettingsWrapper.OptionBatchForTestTeardownDefaultValue);
            page.KillProcessesOnCancel.Should().Be(SettingsWrapper.OptionKillProcessesOnCancelDefaultValue);
            page.DebuggerKind.Should().Be(SettingsWrapper.OptionDebuggerKindDefaultValue).And.Be(DebuggerKind.Native);
            page.MissingTestsReportMode.Should().Be(SettingsWrapper.OptionMissingTestsReportModeDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutionPage_MaxNrOfThreads_MustNotBeNegative()
        {
            var page = OptionsPageFactory.Create<TestExecutionOptionsDialogPage>();

            Action setting = () => page.MaxNrOfThreads = -1;
            setting.Should().Throw<ArgumentOutOfRangeException>();

            page.MaxNrOfThreads = 4;
            page.MaxNrOfThreads.Should().Be(4);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutionPage_AdditionalPdbs_NewValueIsValidated()
        {
            var page = OptionsPageFactory.Create<TestExecutionOptionsDialogPage>();

            page.AdditionalPdbs = @"$(TestDllDir)\*.pdb;C:\Symbols\foo.pdb";
            page.AdditionalPdbs.Should().Be(@"$(TestDllDir)\*.pdb;C:\Symbols\foo.pdb");

            // a folder is not a valid pattern (a file pattern is missing)
            Action setting = () => page.AdditionalPdbs = @"C:\foo\";
            setting.Should().Throw<ArgumentException>();
            page.AdditionalPdbs.Should().Be(@"$(TestDllDir)\*.pdb;C:\Symbols\foo.pdb");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutionPage_EnvironmentVariables_AreValidated()
        {
            var page = OptionsPageFactory.Create<TestExecutionOptionsDialogPage>();

            page.EnvironmentVariables = "MYENVVAR=MyValue//||//OTHER=1";
            page.EnvironmentVariables.Should().Be("MYENVVAR=MyValue//||//OTHER=1");

            Action setting = () => page.EnvironmentVariables = "MYENVVAR";
            setting.Should().Throw<Exception>().Which.Message.Should().Contain("MYENVVAR");
            page.EnvironmentVariables.Should().Be("MYENVVAR=MyValue//||//OTHER=1");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutionPage_DebuggerKind_UsesDebuggerKindConverter()
        {
            PropertyDescriptor property = TypeDescriptor.GetProperties(typeof(TestExecutionOptionsDialogPage))[nameof(TestExecutionOptionsDialogPage.DebuggerKind)];

            property.Attributes.OfType<PropertyPageTypeConverterAttribute>().Should().ContainSingle()
                .Which.ConverterType.Should().Be(typeof(DebuggerKindConverter));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestExecutionPage_AdditionalTestExecutionParam_IsAboutTeExe()
        {
            PropertyDescriptor property = TypeDescriptor.GetProperties(typeof(TestExecutionOptionsDialogPage))[nameof(TestExecutionOptionsDialogPage.AdditionalTestExecutionParam)];

            property.DisplayName.Should().Contain("TE.exe");
        }

        #endregion

        [TestMethod]
        [TestCategory(Unit)]
        public void AllPages_PropertyChanged_RaisedForEveryOption()
        {
            // the package updates the settings of the test adapter whenever an option changes
            foreach (Type pageType in AllPageTypes)
            {
                var page = (NotifyingDialogPage)typeof(OptionsPageFactory).GetMethod(nameof(OptionsPageFactory.Create), BindingFlags.Static | BindingFlags.Public)
                    .MakeGenericMethod(pageType).Invoke(null, null);
                var changed = new List<string>();
                page.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

                PropertyInfo[] options = GetOptions(pageType);
                foreach (PropertyInfo option in options)
                {
                    option.SetValue(page, GetOtherValidValue(option, option.GetValue(page)));
                }

                changed.Should().BeEquivalentTo(options.Select(o => o.Name), $"all options of {pageType.Name} should notify changes");
            }
        }

        /// <returns>A valid value of <paramref name="option"/> which differs from <paramref name="currentValue"/>.</returns>
        internal static object GetOtherValidValue(PropertyInfo option, object currentValue)
        {
            Type type = option.PropertyType;
            if (type == typeof(bool))
                return !(bool)currentValue;
            if (type == typeof(int))
                return (int)currentValue + 1;
            if (type.IsEnum)
            {
                object[] values = Enum.GetValues(type).Cast<object>().ToArray();
                return values.First(v => !v.Equals(currentValue));
            }
            if (type == typeof(string))
            {
                switch (option.Name)
                {
                    case nameof(TestDiscoveryOptionsDialogPage.TestDiscoveryRegex):
                        return @".*Other_taef\.dll";
                    case nameof(TestDiscoveryOptionsDialogPage.TraitsRegexesBefore):
                    case nameof(TestDiscoveryOptionsDialogPage.TraitsRegexesAfter):
                        return "Other::.*" + SettingsWrapper.TraitsRegexesRegexSeparator + "Type" + SettingsWrapper.TraitsRegexesTraitSeparator + option.Name;
                    case nameof(TestExecutionOptionsDialogPage.AdditionalPdbs):
                        return @"$(TestDllDir)\Other*.pdb";
                    case nameof(TestExecutionOptionsDialogPage.EnvironmentVariables):
                        return "OTHER=Value";
                    case nameof(TaefOptionsDialogPage.TestTimeout):
                        return "0:07";
                    default:
                        return "Other" + option.Name;
                }
            }
            throw new NotSupportedException($"Option {option.Name} has unsupported type {type}");
        }

    }
}
