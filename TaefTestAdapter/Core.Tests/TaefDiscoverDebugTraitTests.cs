// This file has been modified for TAEF support.

using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter
{
    /// <summary>Trait tests with Tests_taef.dll (Debug, x86).</summary>
    [TestClass]
    public class TaefDiscovererDebugTraitTests : TaefDiscovererTraitTestsBase
    {
        protected override string SampleTestToUse => TestResources.Tests_DebugX86;

        #region Method stubs for code coverage

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsAllAmountsOfTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsAllAmountsOfTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_AllTestsHaveModuleTraits()
        {
            base.GetTestsFromTestDll_SampleTests_AllTestsHaveModuleTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_InternalTaefPropertiesAreNoTraits()
        {
            base.GetTestsFromTestDll_SampleTests_InternalTaefPropertiesAreNoTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndOneTrait()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndOneTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndTwoTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndTwoTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndThreeTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTestWithFixturesAndThreeTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTemplateTestWithOneTrait()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTemplateTestWithOneTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTemplateTestWithTwoTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTemplateTestWithTwoTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTemplateTestWithThreeTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTemplateTestWithThreeTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithOneTrait()
        {
            base.GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithOneTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithTwoTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithTwoTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithThreeTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsDataDrivenTestsWithThreeTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_DataRowPropertiesOverrideOtherProperties()
        {
            base.GetTestsFromTestDll_SampleTests_DataRowPropertiesOverrideOtherProperties();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTestWithUmlauts()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTestWithUmlauts();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsDataDrivenTestWithUmlauts()
        {
            base.GetTestsFromTestDll_SampleTests_FindsDataDrivenTestWithUmlauts();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTestWithSetupAndUmlauts()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTestWithSetupAndUmlauts();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_FindsTestWithTwoEqualTraits()
        {
            base.GetTestsFromTestDll_SampleTests_FindsTestWithTwoEqualTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_ClassPropertiesAreInheritedAndOverridden()
        {
            base.GetTestsFromTestDll_SampleTests_ClassPropertiesAreInheritedAndOverridden();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SampleTests_IgnoredTestsHaveIgnoreTrait()
        {
            base.GetTestsFromTestDll_SampleTests_IgnoredTestsHaveIgnoreTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexBeforeFromOptions_TaefTraitWins()
        {
            base.GetTestsFromTestDll_RegexBeforeFromOptions_TaefTraitWins();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexBeforeFromOptionsThreeEqualTraits_TaefTraitWins()
        {
            base.GetTestsFromTestDll_RegexBeforeFromOptionsThreeEqualTraits_TaefTraitWins();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexAfterFromOptionsOneEqualTrait_FindsTestWithOneEqualTrait()
        {
            base.GetTestsFromTestDll_RegexAfterFromOptionsOneEqualTrait_FindsTestWithOneEqualTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexAfterFromOptionsTwoEqualTraits_FindsTestWithTwoEqualTraits()
        {
            base.GetTestsFromTestDll_RegexAfterFromOptionsTwoEqualTraits_FindsTestWithTwoEqualTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexBeforeFromOptionsTwoEqualTraits_FindsTestWithTaefAndTwoEqualTraits()
        {
            base.GetTestsFromTestDll_RegexBeforeFromOptionsTwoEqualTraits_FindsTestWithTaefAndTwoEqualTraits();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexBeforeFromOptions_AddsTraitIfNotAlreadyExisting()
        {
            base.GetTestsFromTestDll_RegexBeforeFromOptions_AddsTraitIfNotAlreadyExisting();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexBeforeFromOptions_TraitFromOptionsIsOverridenByTraitFromTest()
        {
            base.GetTestsFromTestDll_RegexBeforeFromOptions_TraitFromOptionsIsOverridenByTraitFromTest();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_BothRegexesFromOptions_BeforeTraitIsOverridenByAfterTrait()
        {
            base.GetTestsFromTestDll_BothRegexesFromOptions_BeforeTraitIsOverridenByAfterTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexAfterFromOptions_AfterTraitOverridesTraitFromTest()
        {
            base.GetTestsFromTestDll_RegexAfterFromOptions_AfterTraitOverridesTraitFromTest();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexAfterFromOptions_AfterTraitOverridesModuleTrait()
        {
            base.GetTestsFromTestDll_RegexAfterFromOptions_AfterTraitOverridesModuleTrait();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexAfterFromOptions_AddsTraitIfNotAlreadyExisting()
        {
            base.GetTestsFromTestDll_RegexAfterFromOptions_AddsTraitIfNotAlreadyExisting();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_RegexButNoSourceLocation_TraitsAreAdded()
        {
            base.GetTestsFromTestDll_RegexButNoSourceLocation_TraitsAreAdded();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_SelectInAdditionalTeArguments_OnlySelectedTestsAreFound()
        {
            base.GetTestsFromTestDll_SelectInAdditionalTeArguments_OnlySelectedTestsAreFound();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_InvalidSelectInAdditionalTeArguments_NoTestsAreFound()
        {
            base.GetTestsFromTestDll_InvalidSelectInAdditionalTeArguments_NoTestsAreFound();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public override void GetTestsFromTestDll_UnknownSwitchInAdditionalTeArguments_WarningIsLoggedAndTestsAreFound()
        {
            base.GetTestsFromTestDll_UnknownSwitchInAdditionalTeArguments_WarningIsLoggedAndTestsAreFound();
        }

        #endregion

    }

}
