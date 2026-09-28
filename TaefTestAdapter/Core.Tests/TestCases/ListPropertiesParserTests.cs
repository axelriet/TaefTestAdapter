// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// Tests of <see cref="StreamingListPropertiesParser"/> and <see cref="ListPropertiesParser"/> with verbatim outputs of
    /// <c>TE.exe &lt;dll&gt; /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false</c> (inline, and the
    /// outputs captured from the sample DLLs in Tests.Common\Resources\TestData\TaefOutput).
    /// </summary>
    [TestClass]
    public class ListPropertiesParserTests
    {

        #region Test data

        /// <summary>A synthetic listing containing all constructs of the /listProperties format (see StreamingListPropertiesParser).</summary>
        private static readonly string[] SyntheticListing =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
            "        C:\\tests\\Lab.dll",
            "                Setup: ModuleSetup",
            "                Teardown: ModuleCleanup",
            "                Property[Architecture] =  x64",
            "                Property[Owner] =  ModuleOwner",
            "                Property[TaefTestType] =  Native",
            "",
            "            Ns::Class",
            "                    Setup: ClassSetup",
            "                    Property[owner] =  ClassOwner",
            "                    Property[Empty] =  ",
            "                    Property[Trimmed] =",
            "",
            "                Ns::Class::Plain",
            "                Ns::Class::WithProps",
            "                        Setup: MethodSetup",
            "                        Teardown: MethodCleanup",
            "                        Property[Category] =  Smoke Nightly",
            "                        Property[OWNER] =  MethodOwner",
            "                        Property[Ignore[@Cond=true]] =  true",
            "                        Property[Leading] =   space",
            "",
            "                Ns::Class::Rows#with::colons [x]",
            "                        Property[DataSource] =  Table:x.xml#T",
            "",
            "                        Data[Index] = 0",
            "                        Data[s] = ",
            "                        Data[V] = a = b",
            "",
            "                Ns::Class::Missing#error [Blocked]",
            "                    [HRESULT: 0x80070002] Failed to find the data source: x.xml.",
            "                        Property[DataSource] =  Table:missing.xml#T",
            "",
            "            ClassData#metadataSet0",
            "                    Property[Data:Color] =  {Red,Blue}",
            "                    Property[Metadata:Index] =  0",
            "",
            "                    Data[Color] = Red",
            "",
            "                ClassData#metadataSet0::M1",
            "            Ns::ClassMissing#error [Blocked]",
            "                [HRESULT: 0x80070002] Failed to find the data source: y.xml.",
            "                    Property[DataSource] =  Table:y.xml#T",
            "",
            "            Last",
            "                Last::Test",
            "",
            "Error: TAEF: something failed",
            "Warning: TAEF: something is odd",
            "",
            "Summary of Errors Outside of Tests:",
            "    Error: TAEF: something failed",
            ""
        };

        /// <summary>
        /// Verbatim output (DLL path shortened) for a lab DLL with data-driven classes (lightweight and table data with
        /// row names containing spaces and '::'), a data-driven class whose data source is missing, method rows with '::',
        /// an ignored test, same-named classes in different namespaces, anonymous namespaces and class templates.
        /// </summary>
        private static readonly string[] DiscoveryLabListing =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
            "        C:\\lab\\x64\\DiscoveryLab.dll",
            "                Property[Architecture] =  x64",
            "                Property[Owner] =  LabModuleOwner",
            "                Property[TaefTestType] =  Native",
            "",
            "            DataNs::ClassData#metadataSet0",
            "                    Property[Data:Color] =  {Red,Blue}",
            "                    Property[Metadata:Index] =  0",
            "                    Property[Owner] =  DataClassOwner",
            "",
            "                    Data[Color] = Red",
            "",
            "                DataNs::ClassData#metadataSet0::M1",
            "                DataNs::ClassData#metadataSet0::M2#metadataSet0",
            "                        Property[Data:Size] =  {1,2}",
            "                        Property[Metadata:Index] =  0",
            "",
            "                        Data[Size] = 1",
            "",
            "                DataNs::ClassData#metadataSet0::M2#metadataSet1",
            "                        Property[Data:Size] =  {1,2}",
            "                        Property[Metadata:Index] =  1",
            "",
            "                        Data[Size] = 2",
            "",
            "            DataNs::ClassData#metadataSet1",
            "                    Property[Data:Color] =  {Red,Blue}",
            "                    Property[Metadata:Index] =  1",
            "                    Property[Owner] =  DataClassOwner",
            "",
            "                    Data[Color] = Blue",
            "",
            "                DataNs::ClassData#metadataSet1::M1",
            "                DataNs::ClassData#metadataSet1::M2#metadataSet0",
            "                        Property[Data:Size] =  {1,2}",
            "                        Property[Metadata:Index] =  0",
            "",
            "                        Data[Size] = 1",
            "",
            "                DataNs::ClassData#metadataSet1::M2#metadataSet1",
            "                        Property[Data:Size] =  {1,2}",
            "                        Property[Metadata:Index] =  1",
            "",
            "                        Data[Size] = 2",
            "",
            "            ClassTable#Row A",
            "                    Property[DataSource] =  Table:DiscoveryLab.xml#ClassRows",
            "",
            "                    Data[Index] = 0",
            "                    Data[V] = 1",
            "",
            "                ClassTable#Row A::M",
            "            ClassTable#a::b",
            "                    Property[DataSource] =  Table:DiscoveryLab.xml#ClassRows",
            "",
            "                    Data[Index] = 1",
            "                    Data[V] = 2",
            "",
            "                ClassTable#a::b::M",
            "            ClassTable#2",
            "                    Property[DataSource] =  Table:DiscoveryLab.xml#ClassRows",
            "",
            "                    Data[Index] = 2",
            "                    Data[V] = 3",
            "",
            "                ClassTable#2::M",
            "            MissingNs::ClassMissing#error [Blocked]",
            "                [HRESULT: 0x80070002] Failed to find the data source: DoesNotExist.xml. Confirm that the file exists. If specified as a resource, check the resource. If specified as a file path, confirm that the path is relative to the test module location.",
            "                    Property[DataSource] =  Table:DoesNotExist.xml#T",
            "",
            "            MethodRows",
            "                MethodRows::Rows#x::y",
            "                        Property[DataSource] =  Table:DiscoveryLab.xml#MethodRows",
            "",
            "                        Data[Index] = 0",
            "                        Data[V] = 1",
            "",
            "                MethodRows::Rows#1::M",
            "                        Property[DataSource] =  Table:DiscoveryLab.xml#MethodRows",
            "",
            "                        Data[Index] = 1",
            "                        Data[V] = 2",
            "",
            "                MethodRows::Rows#2",
            "                        Property[DataSource] =  Table:DiscoveryLab.xml#MethodRows",
            "                        Property[Priority] =  2",
            "",
            "                        Data[Index] = 2",
            "                        Data[V] = 3",
            "",
            "                MethodRows::IgnoredMethod",
            "                        Property[Ignore] =  true",
            "",
            "                MethodRows::WithLambda",
            "            StructTests",
            "                StructTests::M",
            "            A::Same",
            "                A::Same::Run",
            "            B::Same",
            "                B::Same::Run",
            "            `anonymous-namespace'::Inner::AnonInner",
            "                `anonymous-namespace'::Inner::AnonInner::M",
            "            `anonymous-namespace'::`anonymous-namespace'::`anonymous-namespace'::TripleAnon",
            "                `anonymous-namespace'::`anonymous-namespace'::`anonymous-namespace'::TripleAnon::M",
            "            Outer::`anonymous-namespace'::InNamedThenAnon",
            "                Outer::`anonymous-namespace'::InNamedThenAnon::M",
            "            ValueTempl<-1>",
            "                ValueTempl<-1>::M",
            "            ValueTempl<3>",
            "                ValueTempl<3>::M",
            "            TemplTests<class std::basic_string<wchar_t,struct std::char_traits<wchar_t>,class std::allocator<wchar_t> > >",
            "                TemplTests<class std::basic_string<wchar_t,struct std::char_traits<wchar_t>,class std::allocator<wchar_t> > >::M",
            "            TemplTests<unsigned __int64>",
            "                TemplTests<unsigned __int64>::M",
            "            TemplTests<char const *>",
            "                TemplTests<char const *>::M",
            "            TemplTests<enum Shape>",
            "                TemplTests<enum Shape>::M",
            "            TemplTests<enum Color>",
            "                TemplTests<enum Color>::M",
            "",
        };

        /// <summary>Verbatim output (DLL path shortened) for a lab DLL with module-level lightweight data (MODULE_PROPERTY(L"Data:Mod", L"{x,y}")).</summary>
        private static readonly string[] ModuleDataListing =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
            "        C:\\lab\\x64\\ModuleData.dll#metadataSet0",
            "                Property[Architecture] =  x64",
            "                Property[Data:Mod] =  {x,y}",
            "                Property[Metadata:Index] =  0",
            "                Property[TaefTestType] =  Native",
            "",
            "                Data[Mod] = x",
            "",
            "            ModTests",
            "                ModTests::M",
            "",
            "        C:\\lab\\x64\\ModuleData.dll#metadataSet1",
            "                Property[Architecture] =  x64",
            "                Property[Data:Mod] =  {x,y}",
            "                Property[Metadata:Index] =  1",
            "                Property[TaefTestType] =  Native",
            "",
            "                Data[Mod] = y",
            "",
            "            ModTests",
            "                ModTests::M",
            "",
        };

        /// <summary>
        /// Verbatim TE.exe /list output (DLL path shortened) of a test DLL whose data source cannot be loaded: the data source
        /// file (DataDriven.xml) of the table data-driven methods is missing; the error message is indented by 20 characters.
        /// </summary>
        private static readonly string[] MissingDataSourceListing =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
            "        C:\\lab\\x64\\noxml\\TaefLab.dll",
            "            TaefLab::Basic::BasicTests",
            "                TaefLab::Basic::BasicTests::Passing",
            "                TaefLab::Basic::BasicTests::TableDataDriven#error [Blocked]",
            "                    [HRESULT: 0x80070002] Failed to find the data source: DataDriven.xml. Confirm that the file exists. If specified as a resource, check the resource. If specified as a file path, confirm that the path is relative to the test module location.",
            "                TaefLab::Basic::BasicTests::TableWithRowMetadata#error [Blocked]",
            "                    [HRESULT: 0x80070002] Failed to find the data source: DataDriven.xml. Confirm that the file exists. If specified as a resource, check the resource. If specified as a file path, confirm that the path is relative to the test module location.",
            "            EdgeCaseTests",
            "                EdgeCaseTests::SpecialRowNames#error [Blocked]",
            "                    [HRESULT: 0x80070002] Failed to find the data source: DataDriven.xml. Confirm that the file exists. If specified as a resource, check the resource. If specified as a file path, confirm that the path is relative to the test module location.",
            "",
        };

        /// <summary>Verbatim TE.exe /list output of two DLLs in one invocation (DLL paths shortened).</summary>
        private static readonly string[] TwoDllsListing =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
            "        C:\\lab\\x64\\TaefLab2.dll",
            "            Second::Tests::Alpha",
            "                Second::Tests::Alpha::AlphaPass",
            "                Second::Tests::Alpha::AlphaFail",
            "",
            "        C:\\lab\\x64\\deptest\\TaefLabDep.dll",
            "            DepTests",
            "                DepTests::UsesDependency",
            "",
        };

        /// <summary>Verbatim TE.exe /list output for a DLL containing only WexTestClass.h (no tests): banner only.</summary>
        private static readonly string[] EmptyListing =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
        };

        #endregion

        #region Synthetic listing

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SyntheticListing_AllTestsAreReportedInOrder()
        {
            var parser = CreateParser(out List<TestCaseDescriptor> descriptors);

            foreach (string line in SyntheticListing)
            {
                // TE.exe writes CRLF, lines might still contain the CR
                parser.ReportLine(line + "\r");
            }
            parser.Flush();

            descriptors.Select(d => d.Name).Should().Equal(
                "Ns::Class::Plain", "Ns::Class::WithProps", "Ns::Class::Rows#with::colons [x]", "Ns::Class::Missing#error",
                "ClassData#metadataSet0::M1", "Ns::ClassMissing#error", "Last::Test");
            descriptors.Select(d => d.ClassName).Should().Equal(
                "Ns::Class", "Ns::Class", "Ns::Class", "Ns::Class", "ClassData#metadataSet0", "Ns::ClassMissing#error", "Last");
            descriptors.Should().OnlyContain(d => d.TestDll == "C:\\tests\\Lab.dll");
            descriptors.Should().OnlyContain(d => d.FullyQualifiedName == d.Name && d.DisplayName == d.Name);

            parser.TeVersion.Should().Be("10.104k");
            parser.TeArchitecture.Should().Be("x64");
            parser.NrOfTestDlls.Should().Be(1);
            parser.Errors.Should().Equal("Error: TAEF: something failed");
            parser.Warnings.Should().Equal("Warning: TAEF: something is odd");
            parser.UnexpectedLines.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SyntheticListing_PropertiesAreMergedIgnoringCase()
        {
            IList<TestCaseDescriptor> descriptors = Parse(SyntheticListing);

            // class property 'owner' overrides module property 'Owner' (at the module property's position)
            ToString(descriptors[0].Properties).Should().Be("Architecture=x64;owner=ClassOwner;TaefTestType=Native;Empty=;Trimmed=");
            // test property 'OWNER' overrides both
            ToString(descriptors[1].Properties).Should().Be(
                "Architecture=x64;OWNER=MethodOwner;TaefTestType=Native;Empty=;Trimmed=;Category=Smoke Nightly;Ignore[@Cond=true]=true;Leading= space");
            descriptors[1].GetProperty("owner").Should().Be("MethodOwner");
            descriptors[1].GetProperty("category").Should().Be("Smoke Nightly");
            descriptors[1].GetProperty("DoesNotExist").Should().BeNull();

            // class properties are not inherited by other classes
            ToString(descriptors[6].Properties).Should().Be("Architecture=x64;Owner=ModuleOwner;TaefTestType=Native");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SyntheticListing_FixturesOfAllScopesAreCollected()
        {
            IList<TestCaseDescriptor> descriptors = Parse(SyntheticListing);

            string.Join(";", descriptors[1].Fixtures.Select(f => f.ToString())).Should().Be(
                "Module Setup: ModuleSetup;Module Teardown: ModuleCleanup;Class Setup: ClassSetup;Test Setup: MethodSetup;Test Teardown: MethodCleanup");
            descriptors[0].Fixtures.Select(f => f.Scope).Should().Equal(TaefScope.Module, TaefScope.Module, TaefScope.Class);
            descriptors[0].Fixtures.Select(f => f.Kind).Should().Equal(TaefFixtureKind.Setup, TaefFixtureKind.Teardown, TaefFixtureKind.Setup);
            descriptors[6].Fixtures.Select(f => f.Name).Should().Equal("ModuleSetup", "ModuleCleanup");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SyntheticListing_DataValuesAreCollected()
        {
            IList<TestCaseDescriptor> descriptors = Parse(SyntheticListing);

            ToString(descriptors[2].Data).Should().Be("Index=0;s=;V=a = b");
            descriptors[2].GetProperty("DataSource").Should().Be("Table:x.xml#T");
            descriptors[0].Data.Should().BeEmpty();

            // data of a data-driven class
            ToString(descriptors[4].Data).Should().Be("Color=Red");
            descriptors[4].GetProperty("Metadata:Index").Should().Be("0");
            descriptors[4].GetProperty("Data:Color").Should().Be("{Red,Blue}");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SyntheticListing_DataSourceErrorsAreReportedAsPseudoTests()
        {
            IList<TestCaseDescriptor> descriptors = Parse(SyntheticListing);

            TestCaseDescriptor methodError = descriptors[3];
            methodError.IsDataSourceError.Should().BeTrue();
            methodError.IsClassDataSourceError.Should().BeFalse();
            methodError.DataSourceErrorMessage.Should().Be("[HRESULT: 0x80070002] Failed to find the data source: x.xml.");
            methodError.GetProperty("DataSource").Should().Be("Table:missing.xml#T");

            TestCaseDescriptor classError = descriptors[5];
            classError.IsDataSourceError.Should().BeTrue();
            classError.IsClassDataSourceError.Should().BeTrue();
            classError.DataSourceErrorMessage.Should().Be("[HRESULT: 0x80070002] Failed to find the data source: y.xml.");
            classError.GetProperty("DataSource").Should().Be("Table:y.xml#T");
            classError.GetProperty("Architecture").Should().Be("x64");

            descriptors.Where(d => d != methodError && d != classError)
                .Should().OnlyContain(d => !d.IsDataSourceError && d.DataSourceErrorMessage == null);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_Descriptors_AreReportedAsSoonAsTheNextTestStarts()
        {
            var parser = CreateParser(out List<TestCaseDescriptor> descriptors);

            parser.ReportLine("        C:\\tests\\Lab.dll");
            parser.ReportLine("            Ns::Class");
            parser.ReportLine("                Ns::Class::A");
            parser.ReportLine("                        Property[Priority] =  1");
            descriptors.Should().BeEmpty("properties of the test might follow");

            // TE.exe ends each block of properties with an empty line
            parser.ReportLine("");
            descriptors.Should().BeEmpty("data values of the test might follow");

            parser.ReportLine("                Ns::Class::B");
            descriptors.Select(d => d.Name).Should().Equal("Ns::Class::A");
            descriptors[0].GetProperty("Priority").Should().Be("1");

            parser.ReportLine("            Other");
            descriptors.Select(d => d.Name).Should().Equal("Ns::Class::A", "Ns::Class::B");

            parser.ReportLine("                Other::C");
            parser.Flush();
            descriptors.Select(d => d.Name).Should().Equal("Ns::Class::A", "Ns::Class::B", "Other::C");

            parser.Flush();
            descriptors.Should().HaveCount(3, "Flush() must not report a test twice");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_NullAndEmptyLines_AreIgnored()
        {
            var parser = CreateParser(out List<TestCaseDescriptor> descriptors);

            parser.ReportLine(null);
            parser.ReportLine("");
            parser.ReportLine("   ");
            parser.ReportLine("\r");
            parser.Flush();

            descriptors.Should().BeEmpty();
            parser.UnexpectedLines.Should().BeEmpty();
            parser.TeVersion.Should().BeNull();
            parser.TeArchitecture.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_UnexpectedLines_AreCollected()
        {
            var parser = CreateParser(out List<TestCaseDescriptor> descriptors);

            parser.ReportLine("Stray");
            parser.ReportLine("            ClassWithoutDll");
            parser.ReportLine("        C:\\tests\\Lab.dll");
            parser.ReportLine("            Ns::Class");
            parser.ReportLine("                    Unknown class item");
            parser.ReportLine("                Ns::Class::A");
            parser.ReportLine("                        Unknown test item");
            parser.ReportLine("   odd indentation");
            parser.Flush();

            descriptors.Select(d => d.Name).Should().Equal("Ns::Class::A");
            parser.UnexpectedLines.Should().Equal(
                "Stray",
                "            ClassWithoutDll",
                "                    Unknown class item",
                "                        Unknown test item",
                "   odd indentation");
        }

        #endregion

        #region Verbatim lab listings

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_DataDrivenClassesAndSpecialNames_AllTestsWithTheirClasses()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> descriptors = parser.ParseListPropertiesOutput(DiscoveryLabListing);

            descriptors.Select(d => d.Name).Should().Equal(
                "DataNs::ClassData#metadataSet0::M1",
                "DataNs::ClassData#metadataSet0::M2#metadataSet0",
                "DataNs::ClassData#metadataSet0::M2#metadataSet1",
                "DataNs::ClassData#metadataSet1::M1",
                "DataNs::ClassData#metadataSet1::M2#metadataSet0",
                "DataNs::ClassData#metadataSet1::M2#metadataSet1",
                "ClassTable#Row A::M",
                "ClassTable#a::b::M",
                "ClassTable#2::M",
                "MissingNs::ClassMissing#error",
                "MethodRows::Rows#x::y",
                "MethodRows::Rows#1::M",
                "MethodRows::Rows#2",
                "MethodRows::IgnoredMethod",
                "MethodRows::WithLambda",
                "StructTests::M",
                "A::Same::Run",
                "B::Same::Run",
                "`anonymous-namespace'::Inner::AnonInner::M",
                "`anonymous-namespace'::`anonymous-namespace'::`anonymous-namespace'::TripleAnon::M",
                "Outer::`anonymous-namespace'::InNamedThenAnon::M",
                "ValueTempl<-1>::M",
                "ValueTempl<3>::M",
                "TemplTests<class std::basic_string<wchar_t,struct std::char_traits<wchar_t>,class std::allocator<wchar_t> > >::M",
                "TemplTests<unsigned __int64>::M",
                "TemplTests<char const *>::M",
                "TemplTests<enum Shape>::M",
                "TemplTests<enum Color>::M");

            descriptors.Take(6).Select(d => d.ClassName).Should().Equal(
                "DataNs::ClassData#metadataSet0", "DataNs::ClassData#metadataSet0", "DataNs::ClassData#metadataSet0",
                "DataNs::ClassData#metadataSet1", "DataNs::ClassData#metadataSet1", "DataNs::ClassData#metadataSet1");
            descriptors.Skip(6).Take(3).Select(d => d.ClassName).Should().Equal("ClassTable#Row A", "ClassTable#a::b", "ClassTable#2");
            descriptors.Skip(10).Take(5).Should().OnlyContain(d => d.ClassName == "MethodRows");
            descriptors.Last().ClassName.Should().Be("TemplTests<enum Color>");

            descriptors.Should().OnlyContain(d => d.TestDll == "C:\\lab\\x64\\DiscoveryLab.dll");
            parser.Errors.Should().BeEmpty();
            parser.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_DataDrivenClasses_ClassAndMethodRowsAreMerged()
        {
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser().ParseListPropertiesOutput(DiscoveryLabListing);

            TestCaseDescriptor classRowOnly = descriptors.Single(d => d.Name == "DataNs::ClassData#metadataSet1::M1");
            ToString(classRowOnly.Data).Should().Be("Color=Blue");
            classRowOnly.GetProperty("Owner").Should().Be("DataClassOwner");
            classRowOnly.GetProperty("Metadata:Index").Should().Be("1");

            TestCaseDescriptor classAndMethodRow = descriptors.Single(d => d.Name == "DataNs::ClassData#metadataSet1::M2#metadataSet0");
            ToString(classAndMethodRow.Data).Should().Be("Color=Blue;Size=1");
            classAndMethodRow.GetProperty("Metadata:Index").Should().Be("0", "the method row's index overrides the class row's index");
            classAndMethodRow.GetProperty("Data:Color").Should().Be("{Red,Blue}");
            classAndMethodRow.GetProperty("Data:Size").Should().Be("{1,2}");

            TestCaseDescriptor tableClassRow = descriptors.Single(d => d.Name == "ClassTable#a::b::M");
            ToString(tableClassRow.Data).Should().Be("Index=1;V=2");
            tableClassRow.GetProperty("DataSource").Should().Be("Table:DiscoveryLab.xml#ClassRows");
            tableClassRow.GetProperty("Owner").Should().Be("LabModuleOwner");

            TestCaseDescriptor methodRow = descriptors.Single(d => d.Name == "MethodRows::Rows#2");
            ToString(methodRow.Data).Should().Be("Index=2;V=3");
            methodRow.GetProperty("Priority").Should().Be("2");

            // properties of a data-driven class do not leak into the following classes
            descriptors.Single(d => d.Name == "StructTests::M").Data.Should().BeEmpty();
            descriptors.Single(d => d.Name == "StructTests::M").GetProperty("DataSource").Should().BeNull();
            descriptors.Single(d => d.Name == "MethodRows::IgnoredMethod").GetProperty("Ignore").Should().Be("true");
            descriptors.Single(d => d.Name == "MethodRows::WithLambda").GetProperty("Ignore").Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_MissingDataSourceOfClass_ClassIsReportedAsPseudoTest()
        {
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser().ParseListPropertiesOutput(DiscoveryLabListing);

            TestCaseDescriptor classError = descriptors.Single(d => d.IsDataSourceError);
            classError.Name.Should().Be("MissingNs::ClassMissing#error");
            classError.ClassName.Should().Be("MissingNs::ClassMissing#error");
            classError.IsClassDataSourceError.Should().BeTrue();
            classError.DataSourceErrorMessage.Should().StartWith("[HRESULT: 0x80070002] Failed to find the data source: DoesNotExist.xml.");
            classError.GetProperty("DataSource").Should().Be("Table:DoesNotExist.xml#T");
            classError.GetProperty("Owner").Should().Be("LabModuleOwner");

            descriptors.Single(d => d.Name == "MethodRows::Rows#x::y").ClassName.Should().Be("MethodRows");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_ModuleLevelData_DllIsListedOncePerRow()
        {
            var parser = new StreamingListPropertiesParser();
            var descriptors = new List<TestCaseDescriptor>();
            parser.TestCaseDescriptorCreated += (sender, args) => descriptors.Add(args.TestCaseDescriptor);

            foreach (string line in ModuleDataListing)
            {
                parser.ReportLine(line);
            }
            parser.Flush();

            parser.NrOfTestDlls.Should().Be(2);
            descriptors.Select(d => d.Name).Should().Equal("ModTests::M", "ModTests::M");
            descriptors.Select(d => d.TestDll).Should().Equal("C:\\lab\\x64\\ModuleData.dll#metadataSet0", "C:\\lab\\x64\\ModuleData.dll#metadataSet1");
            descriptors.Select(d => ToString(d.Data)).Should().Equal("Mod=x", "Mod=y");
            descriptors.Select(d => d.GetProperty("Metadata:Index")).Should().Equal("0", "1");
            descriptors.Should().OnlyContain(d => d.GetProperty("Data:Mod") == "{x,y}" && d.ClassName == "ModTests");
            parser.UnexpectedLines.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_ListOutputWithMissingDataSources_ErrorPseudoTestsWithMessages()
        {
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser().ParseListPropertiesOutput(MissingDataSourceListing);

            descriptors.Select(d => d.Name).Should().Equal(
                "TaefLab::Basic::BasicTests::Passing",
                "TaefLab::Basic::BasicTests::TableDataDriven#error",
                "TaefLab::Basic::BasicTests::TableWithRowMetadata#error",
                "EdgeCaseTests::SpecialRowNames#error");
            descriptors.Select(d => d.ClassName).Should().Equal(
                "TaefLab::Basic::BasicTests", "TaefLab::Basic::BasicTests", "TaefLab::Basic::BasicTests", "EdgeCaseTests");
            descriptors.Select(d => d.IsDataSourceError).Should().Equal(false, true, true, true);
            descriptors.Should().OnlyContain(d => !d.IsClassDataSourceError);
            descriptors.Skip(1).Should().OnlyContain(d =>
                d.DataSourceErrorMessage.StartsWith("[HRESULT: 0x80070002] Failed to find the data source: DataDriven.xml.")
                && d.DataSourceErrorMessage.EndsWith("relative to the test module location."));
            descriptors.Should().OnlyContain(d => d.Properties.Count == 0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_TwoDlls_TestsKnowTheirDll()
        {
            var parser = new StreamingListPropertiesParser();
            var descriptors = new List<TestCaseDescriptor>();
            parser.TestCaseDescriptorCreated += (sender, args) => descriptors.Add(args.TestCaseDescriptor);
            foreach (string line in TwoDllsListing)
                parser.ReportLine(line);
            parser.Flush();

            parser.NrOfTestDlls.Should().Be(2);
            descriptors.Select(d => d.Name + "|" + d.TestDll).Should().Equal(
                "Second::Tests::Alpha::AlphaPass|C:\\lab\\x64\\TaefLab2.dll",
                "Second::Tests::Alpha::AlphaFail|C:\\lab\\x64\\TaefLab2.dll",
                "DepTests::UsesDependency|C:\\lab\\x64\\deptest\\TaefLabDep.dll");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_EmptyDll_NoTestsAndNoErrors()
        {
            var parser = new ListPropertiesParser();

            parser.ParseListPropertiesOutput(EmptyListing).Should().BeEmpty();
            parser.Errors.Should().BeEmpty();
            parser.Warnings.Should().BeEmpty();

            parser.ParseListPropertiesOutput(new string[0]).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_MultiLineError_LinesAreJoinedAndSummaryIsIgnored()
        {
            var parser = new StreamingListPropertiesParser();
            foreach (string line in new[]
            {
                "Test Authoring and Execution Framework v10.104k for x64",
                "Error: TAEF: [HRESULT 0x80004004] An error occurred during TAEF startup. (Syntax error in selection criteria.",
                " Exception: A string literal was not terminated.)",
                "",
                "Summary of Errors Outside of Tests:",
                "    Error: TAEF: [HRESULT 0x80004004] An error occurred during TAEF startup. (Syntax error in selection criteria.",
                " Exception: A string literal was not terminated.)",
                "",
                "Warning: w1",
                "Warning: w2",
                "",
                "Stray"
            })
            {
                parser.ReportLine(line);
            }
            parser.Flush();

            parser.Errors.Should().Equal(
                "Error: TAEF: [HRESULT 0x80004004] An error occurred during TAEF startup. (Syntax error in selection criteria."
                + Environment.NewLine + " Exception: A string literal was not terminated.)");
            parser.Warnings.Should().Equal("Warning: w1", "Warning: w2");
            parser.UnexpectedLines.Should().Equal("Stray");
        }

        #endregion

        #region Values spanning several lines

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_LabListingWithMultiLineValues_AllTestsAndNoPhantomTests()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> descriptors = parser.ParseListPropertiesOutput(LabTaefOutputs.ListProperties);
            IList<TestCaseDescriptor> listedWithoutIgnored = new ListPropertiesParser().ParseListPropertiesOutput(LabTaefOutputs.ListWithoutIgnoredTests);

            descriptors.Should().HaveCount(LabTaefOutputs.NrOfTests);
            descriptors.Select(d => d.Name).Should().OnlyHaveUniqueItems();
            descriptors.Select(d => d.Name).Where(n => !IsIgnoredByTe(n))
                .Should().Equal(listedWithoutIgnored.Select(d => d.Name), "/list prints the same tests (except for the ignored ones) without properties");
            descriptors.Select(d => d.Name).Take(7).Should().Equal(
                "Ml::MlTests::A", "Ml::MlTests::B", "Ml::MlTests::C", "Ml::MlTests::D#pretty", "Ml::MlTests::D#flat", "Ml::MlTests::E", "Ml::Second::F");
            descriptors.Should().OnlyContain(d => d.Name.StartsWith(d.ClassName + "::") && d.TestDll == LabTaefOutputs.LabDll);
            descriptors.Should().NotContain(d => d.Name.Contains("Phantom") || d.Name.Contains("Fake") || d.ClassName.Contains("Fake"));

            parser.Errors.Should().BeEmpty();
            parser.Warnings.Should().BeEmpty();
        }

        private static bool IsIgnoredByTe(string name)
        {
            return new[] { "IgnoreValues::V_1", "IgnoreValues::V_TRUE", "IgnoreValues::V_true", "IgnoreValues::V_tRuE", "IgnoredClass::Plain" }.Contains(name);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_LabListingWithMultiLineValues_ValuesAreJoinedByLineFeeds()
        {
            IList<TestCaseDescriptor> descriptors = Parse(LabTaefOutputs.ListProperties);
            TestCaseDescriptor Get(string name) => descriptors.Single(d => d.Name == name);

            // module property (continued by lines looking like a class and a test)
            descriptors.Should().OnlyContain(d => d.GetProperty("ModNote") == "module note\n            Phantom::Class\n                Phantom::Class::Test");
            descriptors.Should().OnlyContain(d => d.GetProperty("Owner") == (d.Name == "Ml::MlTests::B" ? "bOwner" : "ModOwner"));
            descriptors.Should().OnlyContain(d => d.GetProperty("TaefTestType") == "Native");

            // class property (continued by lines looking like a test and a summary line)
            TestCaseDescriptor a = Get("Ml::MlTests::A");
            a.GetProperty("ClassNote").Should().Be("class line 1\n                Ml::MlTests::Phantom\nSummary: class");
            a.GetProperty("Area").Should().Be("MlArea");
            descriptors.Where(d => d.ClassName == "Ml::MlTests").Should().HaveCount(6).And.OnlyContain(d => d.GetProperty("Area") == "MlArea");
            Get("Ml::Second::F").GetProperty("ClassNote").Should().BeNull();

            // test properties (continued by lines looking like TE.exe's summary, a DLL, a class and an error)
            a.GetProperty("Description").Should().Be("Parses.\nSummary of Errors Outside of Tests:\n    Error: fake");
            a.GetProperty("Priority").Should().Be("1");
            TestCaseDescriptor c = Get("Ml::MlTests::C");
            c.GetProperty("Note").Should().Be("x\n        C:\\fake\\Fake.dll\n            Ml::Fake\nError: not an error");
            c.GetProperty("Priority").Should().Be("3");

            // the lines after the empty lines within the value can not be told apart from the listing: they are ignored
            TestCaseDescriptor b = Get("Ml::MlTests::B");
            b.GetProperty("Note").Should().Be("first");
            b.GetProperty("Fake").Should().BeNull();
            Get("Ml::MlTests::E").Properties.Select(p => p.Name).Should().Equal("Architecture", "ModNote", "Owner", "TaefTestType", "Area", "ClassNote");

            // data value (the line of blanks ending the value is taken for an empty line)
            TestCaseDescriptor pretty = Get("Ml::MlTests::D#pretty");
            ToString(pretty.Data).Should().Be("Index=0;Text=\n                Ml::MlTests::FakeFromData\n        C:\\fake\\FromData.dll");
            ToString(Get("Ml::MlTests::D#flat").Data).Should().Be("Index=1;Text=flat");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_LabListingWithMultiLineValues_LinesAfterEmptyLinesWithinValuesAreUnexpected()
        {
            var parser = new ListPropertiesParser();
            parser.ParseListPropertiesOutput(LabTaefOutputs.ListProperties);

            parser.UnexpectedLines.Should().Equal(
                "                NotAClass::Phantom2",
                "Warning: fake warning",
                "        Property[Fake] =  injected");
            parser.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ValueFollowedByItemsOfSameScope_ItemsAreNoContinuation()
        {
            IList<TestCaseDescriptor> descriptors = Parse(new[]
            {
                "        C:\\tests\\Lab.dll",
                "            Ns::Class",
                "                Ns::Class::A",
                "                        Property[Note] =  line 1",
                "line 2",
                "                        Property[Priority] =  1",
                "                        Property[Other] =  x",
                "                    Property[NotOfTheTest] =  y",
                "",
                "                        Data[Index] = 0",
                "                        Data[Text] = a",
                "  b",
                "                        Data[V] = 1",
                "",
                "                Ns::Class::B",
            });

            descriptors.Select(d => d.Name).Should().Equal("Ns::Class::A", "Ns::Class::B");
            ToString(descriptors[0].Properties).Should().Be("Note=line 1\nline 2;Priority=1;Other=x\n                    Property[NotOfTheTest] =  y");
            ToString(descriptors[0].Data).Should().Be("Index=0;Text=a\n  b;V=1");
            descriptors[1].Properties.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_LinesWhichCanNotBelongToTheListing_AreUnexpected()
        {
            var parser = CreateParser(out List<TestCaseDescriptor> descriptors);
            foreach (string line in new[]
            {
                "Test Authoring and Execution Framework v10.104k for x64",
                "",
                "",
                "        C:\\tests\\Lab.dll",
                "            Ns::Class",
                "                Ns::Class::A",
                // not after an empty line: no messages of TE.exe
                "Error: e1",
                "Warning: w1",
                "Summary of Errors Outside of Tests:",
                // no test of the class, no rooted path
                "                Other::B",
                "                Ns::Class::",
                "        Fake.dll",
                "                Ns::Class::C",
                "",
                // after an empty line: summary headers are recognized exactly
                "Summary: a value",
                "",
                "Error: TAEF: e2",
                "Warning: TAEF: w2",
                "",
                "Summary of Errors Outside of Tests:",
                "    Error: TAEF: e2",
                "",
                "        \\\\server\\share\\Second.dll",
                "            Second",
                "                Second::D",
                ""
            })
            {
                parser.ReportLine(line);
            }
            parser.Flush();

            descriptors.Select(d => d.Name + "|" + d.TestDll).Should().Equal(
                "Ns::Class::A|C:\\tests\\Lab.dll", "Ns::Class::C|C:\\tests\\Lab.dll", "Second::D|\\\\server\\share\\Second.dll");
            parser.UnexpectedLines.Should().Equal(
                "Error: e1", "Warning: w1", "Summary of Errors Outside of Tests:", "                Other::B", "                Ns::Class::",
                "        Fake.dll", "Summary: a value");
            parser.Errors.Should().Equal("Error: TAEF: e2");
            parser.Warnings.Should().Equal("Warning: TAEF: w2");
            parser.NrOfTestDlls.Should().Be(2);
        }

        #endregion

        #region Captured outputs of the sample DLLs (Tests.Common\Resources\TestData\TaefOutput)

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_SampleTestsWithIgnoredTests_AllTestsAreFound()
        {
            var parser = new StreamingListPropertiesParser();
            var descriptors = new List<TestCaseDescriptor>();
            parser.TestCaseDescriptorCreated += (sender, args) => descriptors.Add(args.TestCaseDescriptor);
            foreach (string line in TestResources.ReadTaefOutputLines("Tests_taef.dll.listProperties.txt"))
                parser.ReportLine(line);
            parser.Flush();

            descriptors.Should().HaveCount(TestResources.NrOfTests);
            descriptors.Select(d => d.Name).Should().OnlyHaveUniqueItems();
            descriptors.Where(d => d.GetProperty(TaefConstants.IgnoreProperty) == "true").Select(d => d.Name)
                .Should().BeEquivalentTo(TestResources.TestNames.IgnoredTest, TestResources.TestNames.IgnoredPassing, TestResources.TestNames.IgnoredFailing);
            descriptors.Should().OnlyContain(d => d.TestDll.EndsWith("\\Debug-x64\\" + TestResources.TestsDll));
            descriptors.Should().OnlyContain(d => d.Name.StartsWith(d.ClassName + "::"));

            parser.NrOfTestDlls.Should().Be(1);
            parser.TeVersion.Should().Be("10.104k");
            parser.TeArchitecture.Should().Be("x64");
            parser.Errors.Should().BeEmpty();
            parser.Warnings.Should().BeEmpty();
            parser.UnexpectedLines.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_SampleTests_WellKnownTestsHaveExpectedMetadata()
        {
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser()
                .ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Tests_taef.dll.listProperties.txt"));

            // module properties and fixtures apply to all tests
            descriptors.Should().OnlyContain(d => d.GetProperty("Component") == "SampleTests"
                                                  && d.GetProperty("TaefTestType") == "Native"
                                                  && d.GetProperty("Architecture") == "x64");
            descriptors.Should().OnlyContain(d => d.Fixtures.Count(f => f.Scope == TaefScope.Module) == 2);

            TestCaseDescriptor Get(string name) => descriptors.Single(d => d.Name == name);

            Get("TaefSamples::TestMath::AddPassesWithTraits").GetProperty("Type").Should().Be("Medium");
            Get("TaefSamples::TestMath::AddPassesWithTraits").GetProperty("Owner").Should().Be("ModuleOwner");
            Get("TaefSamples::Traits::WithEqualTraits").GetProperty("Author").Should().Be("Alice Bob");
            Get("TaefSamples::Traits::With8Traits").Properties.Count(p => p.Name.StartsWith("Trait")).Should().Be(8);
            Get("TaefSamples::ClassAndMethodProperties::InheritsClassProperties").GetProperty("Owner").Should().Be("ClassOwner");
            Get("TaefSamples::ClassAndMethodProperties::OverridesClassProperties").GetProperty("Owner").Should().Be("MethodOwner");
            Get("TaefSamples::ClassAndMethodProperties::OverridesClassProperties").GetProperty("Category").Should().Be("MethodCategory");
            Get("TaefSamples::ClassAndMethodProperties::OverridesClassProperties").GetProperty("ClassTrait").Should().Be("ClassValue");
            Get("TaefSamples::ClassAndMethodProperties::WithCustomPropertiesAndFails").GetProperty("Area").Should().Be("Area with spaces");
            Get("TaefSamples::ClassAndMethodProperties::WithCustomPropertiesAndFails").GetProperty("Description").Should().Be("A test with custom properties");
            Get(TestResources.TestNames.RowWithColons).GetProperty("Owner").Should().Be("RowOwner");
            Get(TestResources.TestNames.RowWithQuote).GetProperty("Priority").Should().Be("1");
            Get("TaefSamples::Ümlautß::Träits").GetProperty("Träit1").Should().Be("Völue1a Völue1b");

            ToString(Get("TaefSamples::LightweightDataTests::TwoValues#metadataSet1").Data).Should().Be("Color=Blue;Size=10");
            Get("TaefSamples::LightweightDataTests::TwoValues#metadataSet1").GetProperty("Metadata:Index").Should().Be("1");
            Get(TestResources.TestNames.SimpleDataRow0).GetProperty("DataSource").Should().Be("Table:DataDrivenTests.xml#SimpleTable");
            ToString(Get(TestResources.TestNames.SimpleDataRow0).Data).Should().Be("i=1;Index=0;s=");

            string.Join(";", Get("TaefSamples::ClassWithFixtures::AddFails").Fixtures.Select(f => f.ToString())).Should().Be(
                "Module Setup: TestsModuleSetup;Module Teardown: TestsModuleCleanup;Class Setup: ClassSetup;Class Teardown: ClassCleanup;Test Setup: MethodSetup;Test Teardown: MethodCleanup");
            Get("TaefSamples::FailingMethodCleanup::TestPassesAlthoughCleanupFails").Fixtures.Last().ToString().Should().Be("Test Teardown: MethodCleanup");

            TestCaseDescriptor missingDataSource = Get(TestResources.TestNames.MissingDataSource);
            missingDataSource.ClassName.Should().Be("TaefSamples::MissingDataSource");
            missingDataSource.IsDataSourceError.Should().BeTrue();
            missingDataSource.IsClassDataSourceError.Should().BeFalse();
            missingDataSource.DataSourceErrorMessage.Should().StartWith("[HRESULT: 0x80070002] Failed to find the data source: MissingDataSource.xml.");
            missingDataSource.GetProperty("DataSource").Should().Be("Table:MissingDataSource.xml#Table");
            descriptors.Count(d => d.IsDataSourceError).Should().Be(1);

            Get(TestResources.TestNames.TemplateTest).ClassName.Should().Be("TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >");
            Get(TestResources.TestNames.AnonymousNamespaceTest).ClassName.Should().Be("TaefSamples::`anonymous-namespace'::Namespace_Anon");
            Get(TestResources.TestNames.RowWithColons).ClassName.Should().Be("TaefSamples::NamedRows");
            Get(TestResources.TestNames.UmlautTest).ClassName.Should().Be("TaefSamples::Ümlautß");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_SampleTestsWithoutIgnoredTests_IgnoredTestsAreMissing()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> descriptors = parser
                .ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Tests_taef.dll.listPropertiesWithoutIgnored.txt"));

            descriptors.Should().HaveCount(TestResources.NrOfNotIgnoredTests);
            descriptors.Should().NotContain(d => d.GetProperty(TaefConstants.IgnoreProperty) != null);
            parser.Errors.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_ListOutputOfSampleTests_NamesAndClassesWithoutMetadata()
        {
            IList<TestCaseDescriptor> withProperties = new ListPropertiesParser()
                .ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Tests_taef.dll.listPropertiesWithoutIgnored.txt"));

            var parser = new StreamingListPropertiesParser();
            var descriptors = new List<TestCaseDescriptor>();
            parser.TestCaseDescriptorCreated += (sender, args) => descriptors.Add(args.TestCaseDescriptor);
            foreach (string line in TestResources.ReadTaefOutputLines("Tests_taef.dll.list.txt"))
                parser.ReportLine(line);
            parser.Flush();

            parser.UnexpectedLines.Should().BeEmpty();
            descriptors.Select(d => d.Name).Should().Equal(withProperties.Select(d => d.Name));
            descriptors.Select(d => d.ClassName).Should().Equal(withProperties.Select(d => d.ClassName));
            descriptors.Should().OnlyContain(d => d.Properties.Count == 0 && d.Fixtures.Count == 0 && d.Data.Count == 0);
            descriptors.Single(d => d.IsDataSourceError).DataSourceErrorMessage.Should().Contain("MissingDataSource.xml");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_X86Listing_SameTestsAsX64()
        {
            var parser = new StreamingListPropertiesParser();
            var descriptors = new List<TestCaseDescriptor>();
            parser.TestCaseDescriptorCreated += (sender, args) => descriptors.Add(args.TestCaseDescriptor);
            foreach (string line in TestResources.ReadTaefOutputLines("Tests_taef.dll.x86.listProperties.txt"))
                parser.ReportLine(line);
            parser.Flush();

            IList<TestCaseDescriptor> x64Descriptors = new ListPropertiesParser()
                .ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Tests_taef.dll.listProperties.txt"));

            parser.TeArchitecture.Should().Be("x86");
            parser.UnexpectedLines.Should().BeEmpty();
            descriptors.Select(d => d.Name).Should().Equal(x64Descriptors.Select(d => d.Name));
            descriptors.Should().OnlyContain(d => d.GetProperty("Architecture") == "x86");
            descriptors.Should().OnlyContain(d => d.TestDll.EndsWith("\\Debug\\" + TestResources.TestsDll));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_NotATestDll_NoTestsAndOneError()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> descriptors = parser.ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Error.NotATestDll.txt"));

            descriptors.Should().BeEmpty();
            parser.Errors.Should().ContainSingle()
                .Which.Should().StartWith("Error: TAEF: [HRESULT 0x80004005] Failed to load '")
                .And.EndWith("DllProject.dll'. (The file was not recognized to be a TAEF test.)");
            parser.Warnings.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_NoTestFiles_WarningAndErrorWithoutSummaryRepetitions()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> descriptors = parser.ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Error.NoTestFiles.txt"));

            descriptors.Should().BeEmpty();
            parser.Errors.Should().Equal("Error: TAEF: None of the specified test files were found.");
            parser.Warnings.Should().ContainSingle()
                .Which.Should().Contain("DoesNotExist_taef.dll\" does not exist.");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ParseListPropertiesOutput_SelectSyntaxError_MultiLineErrorIsReportedOnce()
        {
            var parser = new ListPropertiesParser();
            IList<TestCaseDescriptor> descriptors = parser.ParseListPropertiesOutput(TestResources.ReadTaefOutputLines("Error.SelectSyntaxError.txt"));

            descriptors.Should().BeEmpty();
            parser.Errors.Should().ContainSingle()
                .Which.Should().Contain("Syntax error in selection criteria")
                .And.EndWith(Environment.NewLine + " Exception: A string literal was not terminated.)");
        }

        #endregion

        #region Helpers

        private static StreamingListPropertiesParser CreateParser(out List<TestCaseDescriptor> descriptors)
        {
            var parser = new StreamingListPropertiesParser();
            var result = new List<TestCaseDescriptor>();
            parser.TestCaseDescriptorCreated += (sender, args) => result.Add(args.TestCaseDescriptor);
            descriptors = result;
            return parser;
        }

        private static IList<TestCaseDescriptor> Parse(IEnumerable<string> lines)
        {
            return new ListPropertiesParser().ParseListPropertiesOutput(lines);
        }

        private static string ToString(IEnumerable<TaefProperty> properties)
        {
            return string.Join(";", properties.Select(p => $"{p.Name}={p.Value}"));
        }

        #endregion

    }

}
