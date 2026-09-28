// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Settings;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.OptionsPages
{
    [TestClass]
    public class TaefOptionsDialogPageTests
    {
        private TaefOptionsDialogPage _page;
        private readonly List<string> _changedProperties = new List<string>();

        [TestInitialize]
        public void SetUp()
        {
            _page = OptionsPageFactory.Create<TaefOptionsDialogPage>();
            _changedProperties.Clear();
            _page.PropertyChanged += (sender, args) => _changedProperties.Add(args.PropertyName);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor__OptionsHaveDefaultValues()
        {
            _page.TeExecutable.Should().Be(SettingsWrapper.OptionTeExecutableDefaultValue).And.BeEmpty();
            _page.RunIgnoredTests.Should().Be(SettingsWrapper.OptionRunIgnoredTestsDefaultValue).And.BeFalse();
            _page.BreakOnError.Should().Be(SettingsWrapper.OptionBreakOnErrorDefaultValue).And.BeFalse();
            _page.NrOfTestRepetitions.Should().Be(SettingsWrapper.OptionNrOfTestRepetitionsDefaultValue).And.Be(1);
            _page.TestTimeout.Should().Be(SettingsWrapper.OptionTestTimeoutDefaultValue).And.BeEmpty();
            _page.IsolationLevel.Should().Be(SettingsWrapper.OptionIsolationLevelDefaultValue).And.Be(TaefIsolationLevel.Default);
            _page.RunInProcess.Should().Be(SettingsWrapper.OptionRunInProcessDefaultValue).And.BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestTimeout_ValidValues_AreAcceptedAndTrimmed()
        {
            foreach (string value in new[] { "0:05", "0:0:30", "1:02:03.5", "2.1:00", "23:59:59.1234567", "0" })
            {
                _page.TestTimeout = value;
                _page.TestTimeout.Should().Be(value);
            }

            _page.TestTimeout = " 0:10 ";
            _page.TestTimeout.Should().Be("0:10");

            _page.TestTimeout = "";
            _page.TestTimeout.Should().BeEmpty();

            _page.TestTimeout = null;
            _page.TestTimeout.Should().Be(SettingsWrapper.OptionTestTimeoutDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TestTimeout_InvalidValues_AreRejected()
        {
            _page.TestTimeout = "0:05";
            _changedProperties.Clear();

            // TE.exe ignores such values (with a warning), see Utils.IsValidTestTimeout
            foreach (string value in new[] { "0:60", "24", "0:0:60", "abc", "1:2:3:4", "-1", "0:05:00.12345678", "1.24" })
            {
                Action setting = () => _page.TestTimeout = value;
                setting.Should().Throw<ArgumentException>($"'{value}' is not a valid test timeout")
                    .Which.Message.Should().Contain(value);
                _page.TestTimeout.Should().Be("0:05");
            }

            _changedProperties.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void NrOfTestRepetitions_LessThanOne_IsRejected()
        {
            foreach (int value in new[] { 0, -1, int.MinValue })
            {
                Action setting = () => _page.NrOfTestRepetitions = value;
                setting.Should().Throw<ArgumentOutOfRangeException>();
            }
            _page.NrOfTestRepetitions.Should().Be(1);
            _changedProperties.Should().BeEmpty();

            _page.NrOfTestRepetitions = SettingsWrapper.OptionNrOfTestRepetitionsMinValue;
            _page.NrOfTestRepetitions.Should().Be(1);
            _page.NrOfTestRepetitions = 5;
            _page.NrOfTestRepetitions.Should().Be(5);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void TeExecutable_Value_IsTrimmed()
        {
            _page.TeExecutable = @"  C:\TAEF\x64\TE.exe  ";
            _page.TeExecutable.Should().Be(@"C:\TAEF\x64\TE.exe");

            _page.TeExecutable = null;
            _page.TeExecutable.Should().Be(SettingsWrapper.OptionTeExecutableDefaultValue);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void PropertyChanged_ValueChanged_EventIsRaisedOncePerChange()
        {
            _page.TestTimeout = "0:05";
            _page.TestTimeout = "0:05";
            _page.NrOfTestRepetitions = 3;
            _page.TeExecutable = @"C:\TAEF";
            _page.RunInProcess = true;
            _page.RunInProcess = true;
            _page.RunIgnoredTests = true;
            _page.BreakOnError = true;
            _page.IsolationLevel = TaefIsolationLevel.Class;
            _page.IsolationLevel = TaefIsolationLevel.Class;

            _changedProperties.Should().Equal(
                nameof(TaefOptionsDialogPage.TestTimeout),
                nameof(TaefOptionsDialogPage.NrOfTestRepetitions),
                nameof(TaefOptionsDialogPage.TeExecutable),
                nameof(TaefOptionsDialogPage.RunInProcess),
                nameof(TaefOptionsDialogPage.RunIgnoredTests),
                nameof(TaefOptionsDialogPage.BreakOnError),
                nameof(TaefOptionsDialogPage.IsolationLevel));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsolationLevel_PropertyGrid_UsesTaefIsolationLevelConverter()
        {
            PropertyDescriptor property = TypeDescriptor.GetProperties(_page)[nameof(TaefOptionsDialogPage.IsolationLevel)];

            TypeConverter converter = property.Converter;
            converter.Should().BeOfType<TaefIsolationLevelConverter>();
            converter.ConvertToString(TaefIsolationLevel.Default).Should().Be(TaefIsolationLevelConverter.Default).And.Be("TAEF default");
            converter.GetStandardValuesSupported().Should().BeTrue();
            converter.GetStandardValues().Cast<TaefIsolationLevel>().Should().BeEquivalentTo(
                (TaefIsolationLevel[])Enum.GetValues(typeof(TaefIsolationLevel)));

            foreach (TaefIsolationLevel level in Enum.GetValues(typeof(TaefIsolationLevel)))
            {
                string displayed = converter.ConvertToString(level);
                converter.ConvertFromString(displayed).Should().Be(level);
            }
            converter.ConvertFromString("Method").Should().Be(TaefIsolationLevel.Method);

            // the value is persisted (settings store) with the same converter
            property.Attributes.OfType<PropertyPageTypeConverterAttribute>().Should().ContainSingle()
                .Which.ConverterType.Should().Be(typeof(TaefIsolationLevelConverter));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Options_Categories_AreAsDocumented()
        {
            var expectedCategories = new Dictionary<string, string>
            {
                { nameof(TaefOptionsDialogPage.TeExecutable), SettingsWrapper.CategoryTeExecutableName },
                { nameof(TaefOptionsDialogPage.RunIgnoredTests), SettingsWrapper.CategoryTestExecutionName },
                { nameof(TaefOptionsDialogPage.NrOfTestRepetitions), SettingsWrapper.CategoryTestExecutionName },
                { nameof(TaefOptionsDialogPage.TestTimeout), SettingsWrapper.CategoryTestExecutionName },
                { nameof(TaefOptionsDialogPage.RunInProcess), SettingsWrapper.CategoryRuntimeBehaviorName },
                { nameof(TaefOptionsDialogPage.IsolationLevel), SettingsWrapper.CategoryRuntimeBehaviorName },
                { nameof(TaefOptionsDialogPage.BreakOnError), SettingsWrapper.CategoryRuntimeBehaviorName },
            };

            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(_page);
            foreach (var expected in expectedCategories)
            {
                properties[expected.Key].Category.Should().Be(expected.Value, $"option {expected.Key} should be in category {expected.Value}");
            }
            OptionsPagesTests.GetOptions(typeof(TaefOptionsDialogPage)).Select(p => p.Name)
                .Should().BeEquivalentTo(expectedCategories.Keys);
        }

    }
}
