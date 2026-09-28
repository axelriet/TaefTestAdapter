// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using FluentAssertions;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Helpers
{
    [TestClass]
    public class RegexTraitParserTests : TestsBase
    {
        private RegexTraitParser Parser { get; set; }

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            Parser = new RegexTraitParser(TestEnvironment.Logger);
        }


        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_UnparsableString_FailsNicely()
        {
            List<RegexTraitPair> result = Parser.ParseTraitsRegexesString("vrr<erfwe");

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_EmptyString_EmptyResult()
        {
            List<RegexTraitPair> result = Parser.ParseTraitsRegexesString("");

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_OneRegex_ParsedCorrectly()
        {
            string optionsString = CreateTraitsRegex("MyTest*", "Type", "Small");

            List<RegexTraitPair> result = Parser.ParseTraitsRegexesString(optionsString);

            result.Should().NotBeNull();
            result.Should().ContainSingle();
            result[0].Regex.Should().Be("MyTest*");
            result[0].Trait.Name.Should().Be("Type");
            result[0].Trait.Value.Should().Be("Small");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_TwoRegexes_ParsedCorrectly()
        {
            string optionsString = ConcatTraitsRegexes(
                CreateTraitsRegex("MyTest*", "Type", "Small"),
                CreateTraitsRegex(".*MyOtherTest*", "Category", "Integration"));

            List<RegexTraitPair> result = Parser.ParseTraitsRegexesString(optionsString);

            result.Should().NotBeNull();
            result.Should().HaveCount(2);

            result[0].Regex.Should().Be("MyTest*");
            result[0].Trait.Name.Should().Be("Type");
            result[0].Trait.Value.Should().Be("Small");

            result[1].Regex.Should().Be(".*MyOtherTest*");
            result[1].Trait.Name.Should().Be("Category");
            result[1].Trait.Value.Should().Be("Integration");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_InvalidRegex_IsIgnored()
        {
            string optionsString = ConcatTraitsRegexes(
                CreateTraitsRegex("GoodRegexOne", "Type", "Small"),
                CreateTraitsRegex("[[MalformedRegex", "Type", "Medium"),
                CreateTraitsRegex("GoodRegexTwo", "Type", "Large"));

            List<RegexTraitPair> results = Parser.ParseTraitsRegexesString(optionsString);

            results.Should().NotBeNull();
            results.Should().HaveCount(2);

            results[0].Regex.Should().Be("GoodRegexOne");
            results[0].Trait.Value.Should().Be("Small");

            results[1].Regex.Should().Be("GoodRegexTwo");
            results[1].Trait.Value.Should().Be("Large");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_InvalidRegex_Throws()
        {
            string optionsString = ConcatTraitsRegexes(
                CreateTraitsRegex("GoodRegexOne", "Type", "Small"),
                CreateTraitsRegex("[[MalformedRegex", "Type", "Medium"),
                CreateTraitsRegex("GoodRegexTwo", "Type", "Large"));

            Action parse = () => Parser.ParseTraitsRegexesString(optionsString, ignoreErrors: false);

            parse.Should().Throw<Exception>();
        }


        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_TaefNamesAndValueWithCommas_ParsedCorrectly()
        {
            string optionsString = ConcatTraitsRegexes(
                CreateTraitsRegex(@"^Ns::Class::Method#metadataSet\d+$", "Kind", "Data,Row,Lightweight"),
                CreateTraitsRegex(@"TaefSamples::NamedRows::SpecialCharacters#with::colons \[x\]", "Special", ""));

            List<RegexTraitPair> result = Parser.ParseTraitsRegexesString(optionsString);

            result.Should().HaveCount(2);
            result[0].Regex.Should().Be(@"^Ns::Class::Method#metadataSet\d+$");
            result[0].Trait.Name.Should().Be("Kind");
            result[0].Trait.Value.Should().Be("Data,Row,Lightweight");
            result[1].Regex.Should().Be(@"TaefSamples::NamedRows::SpecialCharacters#with::colons \[x\]");
            result[1].Trait.Value.Should().Be("");
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_MissingSeparatorsOrName_ErrorsAreLogged()
        {
            string optionsString = ConcatTraitsRegexes(
                "RegexWithoutTrait",
                "Regex" + SettingsWrapper.TraitsRegexesRegexSeparator + "NameWithoutValue",
                "Regex" + SettingsWrapper.TraitsRegexesRegexSeparator + SettingsWrapper.TraitsRegexesTraitSeparator + "ValueWithoutName",
                CreateTraitsRegex("Good", "Type", "Small"));

            List<RegexTraitPair> result = Parser.ParseTraitsRegexesString(optionsString);

            result.Should().ContainSingle().Which.Regex.Should().Be("Good");
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("RegexWithoutTrait") && s.Contains(SettingsWrapper.TraitsRegexesRegexSeparator))), Times.Once);
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("NameWithoutValue"))), Times.Once);
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("ValueWithoutName") && s.Contains("empty"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseTraitsRegexesString_NullLogger_DoesNotThrow()
        {
            new RegexTraitParser(null).ParseTraitsRegexesString("[[Invalid///A,B").Should().BeEmpty();
        }


        private string CreateTraitsRegex(string regex, string name, string value)
        {
            return regex +
                SettingsWrapper.TraitsRegexesRegexSeparator + name +
                SettingsWrapper.TraitsRegexesTraitSeparator + value;
        }

        private string ConcatTraitsRegexes(params string[] regexes)
        {
            return string.Join(SettingsWrapper.TraitsRegexesPairSeparator, regexes);
        }

    }

}