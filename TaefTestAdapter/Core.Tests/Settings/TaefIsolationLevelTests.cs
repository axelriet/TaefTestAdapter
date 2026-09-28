// This file has been added for TAEF support.

using System;
using System.ComponentModel;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Settings
{

    [TestClass]
    public class TaefIsolationLevelTests
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void ToTeValue_AllLevels_DefaultIsNotPassedToTe()
        {
            TaefIsolationLevel.Default.ToTeValue().Should().BeNull();
            TaefIsolationLevel.Test.ToTeValue().Should().Be("Test");
            TaefIsolationLevel.Method.ToTeValue().Should().Be("Method");
            TaefIsolationLevel.Class.ToTeValue().Should().Be("Class");
            TaefIsolationLevel.Module.ToTeValue().Should().Be("Module");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetIsolationLevelOption_AllLevels_ReturnsTeSwitchOrNull()
        {
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Default).Should().BeNull();
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Test).Should().Be("/isolationLevel:Test");
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Method).Should().Be("/isolationLevel:Method");
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Class).Should().Be("/isolationLevel:Class");
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Module).Should().Be("/isolationLevel:Module");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Converter_AllLevels_RoundTrip()
        {
            var converter = new TaefIsolationLevelConverter();
            var levels = Enum.GetValues(typeof(TaefIsolationLevel)).Cast<TaefIsolationLevel>().ToList();

            foreach (TaefIsolationLevel level in levels)
            {
                string readable = converter.ConvertToString(level);
                readable.Should().Be(level.ToReadableString());
                converter.ConvertFrom(readable).Should().Be(level);
            }

            levels.Select(l => l.ToReadableString()).Should().OnlyHaveUniqueItems();
            TaefIsolationLevel.Default.ToReadableString().Should().Be(TaefIsolationLevelConverter.Default);
            TaefIsolationLevel.Class.ToReadableString().Should().Be("Class");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Converter_IsRegisteredForEnum()
        {
            TypeDescriptor.GetConverter(typeof(TaefIsolationLevel)).Should().BeOfType<TaefIsolationLevelConverter>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Converter_EnumNames_CanBeConverted()
        {
            // values of the XML settings are enum names
            var converter = new TaefIsolationLevelConverter();

            converter.ConvertFrom("Module").Should().Be(TaefIsolationLevel.Module);
            converter.ConvertFrom("Default").Should().Be(TaefIsolationLevel.Default);
        }

    }

}
