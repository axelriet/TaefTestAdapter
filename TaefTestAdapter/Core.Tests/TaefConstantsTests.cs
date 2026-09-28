// This file has been added for TAEF support.

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter
{
    /// <summary>
    /// Tests of the TE.exe switches, <c>/select</c> query terms, ignore detection and exit code descriptions of
    /// <see cref="TaefConstants"/>.
    /// </summary>
    [TestClass]
    public class TaefConstantsTests
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void Switches_HaveTheSpellingTeExpects()
        {
            TaefConstants.OutputFormatOptions.Should().Be("/unicodeOutput:false /coloredConsoleOutput:false");
            TaefConstants.ListPropertiesOption.Should().Be("/listProperties");
            TaefConstants.RunIgnoredTestsOption.Should().Be("/runIgnoredTests");
            TaefConstants.BreakOnErrorOption.Should().Be("/breakOnError");
            TaefConstants.InProcOption.Should().Be("/inproc");
            TaefConstants.DisableTimeoutsOption.Should().Be("/disableTimeouts");
            TaefConstants.SelectionOrOperator.Should().Be(" or ");
            TaefConstants.SelectionAndOperator.Should().Be(" and ");
            TaefConstants.MaxCommandLength.Should().Be(30000);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetLoopOptions_ReturnsLoopSwitches()
        {
            TaefConstants.GetLoopOptions(3).Should().Be("/testmode:Loop /Loop:3 /LoopTest:1");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetLoopTestOptions_ReturnsSwitchesRepeatingEachTestInSingleLoop()
        {
            TaefConstants.GetLoopTestOptions(3).Should().Be("/testmode:Loop /Loop:1 /LoopTest:3");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTestTimeoutOption_ReturnsTimeoutSwitch()
        {
            TaefConstants.GetTestTimeoutOption("0:0:5").Should().Be("/testTimeout:0:0:5");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetIsolationLevelOption_ReturnsSwitchOrNullForDefault()
        {
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Default).Should().BeNull();
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Test).Should().Be("/isolationLevel:Test");
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Method).Should().Be("/isolationLevel:Method");
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Class).Should().Be("/isolationLevel:Class");
            TaefConstants.GetIsolationLevelOption(TaefIsolationLevel.Module).Should().Be("/isolationLevel:Module");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetWttLoggingOptions_LogFileIsQuoted()
        {
            TaefConstants.GetWttLoggingOptions(@"C:\my temp\x.wtl").Should().Be("/enableWttLogging /logFile:\"C:\\my temp\\x.wtl\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetSelectOption_QuoteFollowsColon()
        {
            // quoting the whole argument ("/select:...") makes TE.exe run all tests
            TaefConstants.GetSelectOption("@Name='A::B' or @Name='C::*'").Should().Be("/select:\"@Name='A::B' or @Name='C::*'\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNameSelectionTerm_EscapesNames()
        {
            TaefConstants.GetNameSelectionTerm("TaefSamples::TestMath::AddPasses").Should().Be("@Name='TaefSamples::TestMath::AddPasses'");
            TaefConstants.GetNameSelectionTerm("TaefSamples::NamedRows::SpecialCharacters#with'quote").Should().Be("@Name='TaefSamples::NamedRows::SpecialCharacters#with''quote'");
            TaefConstants.GetNameSelectionTerm("TaefSamples::`anonymous-namespace'::Namespace_Anon::Test").Should().Be("@Name='TaefSamples::`anonymous-namespace''::Namespace_Anon::Test'");
            TaefConstants.GetNameSelectionTerm("Q::T#say \"hi\"").Should().Be("@Name='Q::T#say ?hi?'");
            TaefConstants.GetNameSelectionTerm("Q::T#tab\tand\nnew line").Should().Be("@Name='Q::T#tab?and?new line'");
            TaefConstants.GetNameSelectionTerm("TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前").Should().Be("@Name='TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前'");
            TaefConstants.GetNameSelectionTerm(@"TaefSamples::NamedRows::SpecialCharacters#back\slash").Should().Be(@"@Name='TaefSamples::NamedRows::SpecialCharacters#back\slash'");
            TaefConstants.GetNameSelectionTerm("Class::*").Should().Be("@Name='Class::*'");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToSelectablePattern_ReplacesDoubleQuotesAndControlCharactersOnly()
        {
            TaefConstants.ToSelectablePattern("A::B#it's \"x\"\r\n").Should().Be("A::B#it's ?x???");
            const string unchanged = "TaefSamples::TemplateTests<class std::array<int,3> >::CanIterate";
            TaefConstants.ToSelectablePattern(unchanged).Should().BeSameAs(unchanged);
            new Action(() => TaefConstants.ToSelectablePattern(null)).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetClassSelectionTerm_SelectsClassWithoutNestedClasses()
        {
            TaefConstants.GetClassSelectionTerm("TaefSamples::ClassWithFixtures").Should().Be("(@Name='TaefSamples::ClassWithFixtures::*' and not @Name='TaefSamples::ClassWithFixtures::*::*')");
            TaefConstants.GetClassSelectionTerm("Ns::It's").Should().Be("(@Name='Ns::It''s::*' and not @Name='Ns::It''s::*::*')");
            new Action(() => TaefConstants.GetClassSelectionTerm(null)).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsIgnored_RecognizesIgnoreTraitWithValueTrue()
        {
            TaefConstants.IsIgnored(ToTestCase()).Should().BeFalse();
            TaefConstants.IsIgnored(ToTestCase(new Trait("Owner", "true"))).Should().BeFalse();
            TaefConstants.IsIgnored(ToTestCase(new Trait("Ignore", "false"))).Should().BeFalse();
            TaefConstants.IsIgnored(ToTestCase(new Trait("Ignore", "true"))).Should().BeTrue();
            TaefConstants.IsIgnored(ToTestCase(new Trait("ignore", "TRUE"))).Should().BeTrue();
            TaefConstants.IsIgnored(ToTestCase(new Trait("Owner", "me"), new Trait("Ignore", "True"))).Should().BeTrue();
            TaefConstants.IsIgnored(ToTestCase(new Trait("Ignore", "1"))).Should().BeTrue();
            new Action(() => TaefConstants.IsIgnored(null)).Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// The values TE.exe 10.104k treats as ignored (verified with <c>TE.exe &lt;lab DLL&gt; /list</c>, which does not list
        /// ignored tests, on test methods with these values of metadata <c>Ignore</c>, and a class with <c>Ignore=1</c>):
        /// only <c>1</c> and <c>true</c> (ignoring case), without trimming.
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void IsIgnored_ValuesAsTreatedByTe()
        {
            foreach (string value in new[] { "1", "true", "TRUE", "True", "tRuE" })
            {
                TaefConstants.IsIgnoreValue(value).Should().BeTrue(value);
                TaefConstants.IsIgnored(ToTestCase(new Trait("Ignore", value))).Should().BeTrue(value);
            }

            foreach (string value in new[] { " true ", "true ", " true", " 1", "1 ", "2", "-1", "01", "0", "yes", "false", "", "true true", "false true", "\ttrue", null })
            {
                TaefConstants.IsIgnoreValue(value).Should().BeFalse($"'{value}'");
                TaefConstants.IsIgnored(ToTestCase(new Trait("Ignore", value))).Should().BeFalse($"'{value}'");
            }
        }

        private static TestCase ToTestCase(params Trait[] traits)
        {
            var testCase = new TestCase("A::B", "c:\\a.dll", "A::B", "", 0);
            testCase.Traits.AddRange(traits);
            return testCase;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetDataRowIndexSelectionTerm_SelectsDataValueIndex()
        {
            TaefConstants.DataRowIndexName.Should().Be("Index");
            TaefConstants.GetDataRowIndexSelectionTerm(0).Should().Be("@Data:Index=0");
            TaefConstants.GetDataRowIndexSelectionTerm(12).Should().Be("@Data:Index=12");
            new Action(() => TaefConstants.GetDataRowIndexSelectionTerm(-1)).Should().Throw<ArgumentOutOfRangeException>();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IgnoredTestMessage_NamesOption()
        {
            TaefConstants.IgnoredTestMessage.Should().Be("Test is marked Ignore=true - enable option 'Also run ignored tests' to run it.");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetExitCodeDescription_SpecialExitCodes_AreExplained()
        {
            TaefConstants.ExitCodeLoggerInitializationFailed.Should().Be(0x03000000);
            TaefConstants.ExitCodeNoTestFiles.Should().Be(0x05000000);
            TaefConstants.ExitCodeStartupError.Should().Be(0x06000000);
            TaefConstants.ExitCodeNoTestsExecuted.Should().Be(0x07000000);
            TaefConstants.ExitCodeSessionTimeout.Should().Be(0x08000000);

            TaefConstants.GetExitCodeDescription(0x03000000).Should().Contain("logger");
            TaefConstants.GetExitCodeDescription(0x05000000).Should().Contain("did not find any test files");
            TaefConstants.GetExitCodeDescription(0x06000000).Should().Contain("/select");
            TaefConstants.GetExitCodeDescription(0x07000000).Should().Contain("did not execute any tests");
            TaefConstants.GetExitCodeDescription(0x08000000).Should().Contain("session timeout");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetExitCodeDescription_NumbersOfFailedTestsAndCrashCodes_AreNotExplained()
        {
            // TE.exe returns the number of Failed + Blocked + NotRun tests, or the crash code if it crashes in process
            foreach (int exitCode in new[] { 0, 1, 68, 5000, unchecked((int)0xC0000005), 3 })
            {
                TaefConstants.GetExitCodeDescription(exitCode).Should().BeNull(exitCode.ToString());
            }
        }

    }

}
