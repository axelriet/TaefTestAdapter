// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestCases
{
    [TestClass]
    public class ManagedDiscoveryTests : TestsBase
    {
        private const string TestDll = @"C:\tests\ManagedTests.dll";

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ModuleKindIsSeparateFromMergedPropertiesAndResetsForNextModule()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> tests = parser.ParseListPropertiesOutput(new[]
            {
                "        " + TestDll,
                "                Property[TaefTestType] =  Managed", "",
                "                Setup: InitializeAssembly", "",
                "            Ns.Data#a.b c::d#e",
                "                    Property[TaefTestType] =  Native",
                "                    Property[Owner] =  Class owner", "",
                "                    Setup: InitializeClass", "",
                "                Ns.Data#a.b c::d#e.M#x.y::z#q",
                "                        Property[Owner] =  Method owner",
                "                        Data[Index] = 7", "",
                "        C:\\tests\\NativeTests.dll",
                "            Native::Class",
                "                Native::Class::M", ""
            });

            tests.Select(t => t.Name).Should().Equal("Ns.Data#a.b c::d#e.M#x.y::z#q", "Native::Class::M");
            tests[0].ClassName.Should().Be("Ns.Data#a.b c::d#e");
            tests[0].IsManaged.Should().BeTrue();
            tests[0].GetProperty("TaefTestType").Should().Be("Native", "ordinary metadata still merges by scope");
            tests[0].GetProperty("Owner").Should().Be("Method owner");
            tests[0].Fixtures.Select(f => f.Scope).Should().Equal(TaefScope.Module, TaefScope.Class);
            tests[1].IsManaged.Should().BeFalse();
            parser.UnexpectedLines.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_MetadataHintSupportsListingsWithoutTypePropertyButExplicitNativeWins()
        {
            var parser = new ListPropertiesParser(isManaged: true);
            IList<TestCaseDescriptor> tests = parser.ParseListPropertiesOutput(new[]
            {
                "        " + TestDll,
                "            GlobalClass",
                "                GlobalClass.M", "",
                "        C:\\tests\\MixedMode.dll",
                "                Property[TaefTestType] =  Native", "",
                "            Native::Class",
                "                Native::Class::M", ""
            });

            tests.Select(t => t.IsManaged).Should().Equal(true, false);
            parser.UnexpectedLines.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Parse_ManagedDataSourceErrorsKeepTheirListedClassAndError()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> tests = parser.ParseListPropertiesOutput(new[]
            {
                "        " + TestDll,
                "                Property[TaefTestType] =  Managed", "",
                "            Ns.MissingClass#error [Blocked]",
                "                TAEF: Failed to load the class data source.", "",
                "            Ns.MissingMethod",
                "                Ns.MissingMethod.M#error [Blocked]",
                "                    TAEF: Failed to load the method data source.", ""
            });

            tests.Should().HaveCount(2).And.OnlyContain(t => t.IsManaged && t.IsDataSourceError);
            tests[0].IsClassDataSourceError.Should().BeTrue();
            tests[0].ClassName.Should().Be("Ns.MissingClass#error");
            tests[1].ClassName.Should().Be("Ns.MissingMethod");
            tests[1].DataSourceErrorMessage.Should().Contain("method data source");
            parser.UnexpectedLines.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Create_ManagedClassRowsCountExactlyAndRetainIdentityWithoutSymbols()
        {
            var descriptors = new[]
            {
                new TestCaseDescriptor("Ns.C#a.b.M#x.y", "Ns.C#a.b",
                    properties: new[] { new TaefProperty("ExecutionGroup", "Scenario") },
                    fixtures: new[] { new TaefFixture(TaefScope.Module, TaefFixtureKind.Setup, "Ns.C.ModuleInit"),
                        new TaefFixture(TaefScope.Class, TaefFixtureKind.Setup, "Init") }, isManaged: true),
                new TestCaseDescriptor("Ns.C#a.b.N#q' :: #", "Ns.C#a.b", isManaged: true),
                new TestCaseDescriptor("Ns.C#other.M", "Ns.C#other", isManaged: true),
                new TestCaseDescriptor("Global.M", "Global", isManaged: true)
            };

            IList<TestCase> tests = new TestCaseFactory(TestDll, MockLogger.Object, MockOptions.Object, null, null)
                .CreateTestCasesFromDescriptors(descriptors);

            tests.Select(t => t.FullyQualifiedName).Should().Equal(descriptors.Select(d => d.Name));
            tests.Select(t => t.TaefClassName).Should().Equal(descriptors.Select(d => d.ClassName));
            tests.Should().OnlyContain(t => t.IsManaged && t.CodeFilePath == "" && t.LineNumber == 0);
            tests[0].Properties.OfType<TestCaseExecutionGroupProperty>().Single().Name.Should().Be("Scenario");
            tests[0].Properties.OfType<TestCaseModuleFixturesProperty>().Single().Names.Should().Equal("Ns.C.ModuleInit");
            tests.Select(t => t.Properties.OfType<TestCaseMetaDataProperty>().Single().NrOfTestCasesInClass)
                .Should().Equal(2, 2, 1, 1);
            tests.Should().OnlyContain(t => t.Properties.OfType<TestCaseMetaDataProperty>().Single().NrOfTestCasesInTestDll == 4);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTraits_ManagedCompilerAttributesAreHiddenAndUserAttributesRemain()
        {
            var properties = new[]
            {
                new TaefProperty("Owner", "Alice"),
                new TaefProperty("Priority", "1"),
                new TaefProperty("Ignore", "true"),
                new TaefProperty("System.Runtime.CompilerServices.AsyncStateMachineAttribute", "typeof(Ns.C+<M>d__0)"),
                new TaefProperty("System.Diagnostics.DebuggerStepThroughAttribute", "true"),
                new TaefProperty("System.ObsoleteAttribute", "old"),
                new TaefProperty("Contoso.CustomAttribute", "value"),
                new TaefProperty("Description", "A description"),
                new TaefProperty("TaefTestType", "Managed")
            };

            var managed = new TestCaseDescriptor("Ns.C.M", "Ns.C", properties: properties, isManaged: true);
            TestCaseFactory.GetTaefTraits(managed).Select(t => t.Name).Should().Equal(
                "Owner", "Priority", "Ignore", "System.ObsoleteAttribute", "Contoso.CustomAttribute");
            var native = new TestCaseDescriptor("Ns::C::M", "Ns::C", properties: properties);
            TestCaseFactory.GetTaefTraits(native).Select(t => t.Name).Should().Contain(new[]
            {
                "System.Runtime.CompilerServices.AsyncStateMachineAttribute", "System.Diagnostics.DebuggerStepThroughAttribute"
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ManagedNames_UseListedClassWhenRowsContainSeparators()
        {
            const string listedClass = "Ns.C#a.b :: c#d";
            const string name = listedClass + ".M#q.r :: s#t";

            TaefNames.GetManagedMethodName(name, listedClass).Should().Be("M");
            TaefNames.GetManagedBaseName(name, listedClass).Should().Be("Ns.C.M");
            TaefNames.GetManagedMethodName("Global.M#x", "Global").Should().Be("M");
            TaefNames.GetManagedClassName("Ns.C.M#x.y").Should().Be("Ns.C");
            TaefNames.GetManagedClassName("Ns.C#metadataSet0.M#x.y").Should().Be("Ns.C#metadataSet0");
            TaefNames.GetManagedMethodName("Ns.C#error", "Ns.C#error").Should().BeEmpty();
            var property = new TestCaseTaefClassProperty(listedClass);
            new TestCaseTaefClassProperty(property.Serialization).Should().Be(property);
            new Action(() => new TestCaseTaefClassProperty("")).Should().Throw<ArgumentException>();
        }
    }
}
