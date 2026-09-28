// This file has been added for TAEF support.

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Helpers
{

    [TestClass]
    public class TaefNamesTests
    {

        /// <summary>
        /// TAEF name, class name, method name, base name, data suffix, VS fully qualified name. Names are as listed by
        /// TE.exe for the sample DLLs (Tests_taef.dll) and for TAEF test DLLs written for these tests (TaefLab.dll, DiscoveryLab.dll).
        /// </summary>
        private static readonly string[][] NameCases =
        {
            // plain tests
            new[] { "Ns::Class::Method", "Ns::Class", "Method", "Ns::Class::Method", null, "Ns.Class.Method" },
            new[] { "TaefSamples::TestMath::AddPasses", "TaefSamples::TestMath", "AddPasses", "TaefSamples::TestMath::AddPasses", null, "TaefSamples.TestMath.AddPasses" },
            new[] { "Method", "", "Method", "Method", null, "Method" },
            new[] { "Class::Method", "Class", "Method", "Class::Method", null, "Class.Method" },
            new[] { "TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep::Fails",
                "TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep", "Fails",
                "TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep::Fails", null,
                "TaefSamples.Namespace_1.Namespace_2_Nested.Namespace_3_Deeply.Nested.Namespace_Deep.Fails" },

            // data rows of methods
            new[] { "TaefSamples::LightweightDataTests::SingleValue#metadataSet0", "TaefSamples::LightweightDataTests", "SingleValue", "TaefSamples::LightweightDataTests::SingleValue", "metadataSet0", "TaefSamples.LightweightDataTests.SingleValue#metadataSet0" },
            new[] { "Ns::Class::Method#0", "Ns::Class", "Method", "Ns::Class::Method", "0", "Ns.Class.Method#0" },
            new[] { "TaefSamples::TableDataTests::Simple#Underscore", "TaefSamples::TableDataTests", "Simple", "TaefSamples::TableDataTests::Simple", "Underscore", "TaefSamples.TableDataTests.Simple#Underscore" },
            new[] { "TaefSamples::NamedRows::SpecialCharacters#with space", "TaefSamples::NamedRows", "SpecialCharacters", "TaefSamples::NamedRows::SpecialCharacters", "with space", "TaefSamples.NamedRows.SpecialCharacters#with space" },
            new[] { "TaefSamples::NamedRows::SpecialCharacters#with::colons [x]", "TaefSamples::NamedRows", "SpecialCharacters", "TaefSamples::NamedRows::SpecialCharacters", "with::colons [x]", "TaefSamples.NamedRows.SpecialCharacters#with::colons [x]" },
            new[] { "TaefSamples::NamedRows::SpecialCharacters#with#hash", "TaefSamples::NamedRows", "SpecialCharacters", "TaefSamples::NamedRows::SpecialCharacters", "with#hash", "TaefSamples.NamedRows.SpecialCharacters#with#hash" },
            new[] { "TaefSamples::NamedRows::SpecialCharacters#with'quote", "TaefSamples::NamedRows", "SpecialCharacters", "TaefSamples::NamedRows::SpecialCharacters", "with'quote", "TaefSamples.NamedRows.SpecialCharacters#with'quote" },
            new[] { "TaefSamples::NamedRows::SpecialCharacters#back\\slash", "TaefSamples::NamedRows", "SpecialCharacters", "TaefSamples::NamedRows::SpecialCharacters", "back\\slash", "TaefSamples.NamedRows.SpecialCharacters#back\\slash" },
            new[] { "TaefSamples::NamedRows::SpecialCharacters#Ünïcødé 名前", "TaefSamples::NamedRows", "SpecialCharacters", "TaefSamples::NamedRows::SpecialCharacters", "Ünïcødé 名前", "TaefSamples.NamedRows.SpecialCharacters#Ünïcødé 名前" },
            new[] { "MethodRows::Rows#x::y", "MethodRows", "Rows", "MethodRows::Rows", "x::y", "MethodRows.Rows#x::y" },
            new[] { "Ns::Class::Method#a::b", "Ns::Class", "Method", "Ns::Class::Method", "a::b", "Ns.Class.Method#a::b" },

            // data source error pseudo tests
            new[] { "TaefSamples::MissingDataSource::Test#error", "TaefSamples::MissingDataSource", "Test", "TaefSamples::MissingDataSource::Test", "error", "TaefSamples.MissingDataSource.Test#error" },
            new[] { "MissingNs::ClassMissing#error", "MissingNs", "ClassMissing", "MissingNs::ClassMissing", "error", "MissingNs.ClassMissing#error" },
            new[] { "ClassMissing#error", "", "ClassMissing", "ClassMissing", "error", "ClassMissing#error" },

            // class templates ('::' within template arguments does not separate scopes)
            new[] { "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate",
                "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >", "CanIterate",
                "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate", null,
                "TaefSamples.TemplateTests<class std::vector<int,class std::allocator<int> > >.CanIterate" },
            new[] { "TaefSamples::ClassTemplates::NumberTemplateTests<signed char>::CanHoldLargeSum",
                "TaefSamples::ClassTemplates::NumberTemplateTests<signed char>", "CanHoldLargeSum",
                "TaefSamples::ClassTemplates::NumberTemplateTests<signed char>::CanHoldLargeSum", null,
                "TaefSamples.ClassTemplates.NumberTemplateTests<signed char>.CanHoldLargeSum" },
            new[] { "Ns::Templ<std::string>::M", "Ns::Templ<std::string>", "M", "Ns::Templ<std::string>::M", null, "Ns.Templ<std::string>.M" },
            new[] { "ValueTempl<-1>::M", "ValueTempl<-1>", "M", "ValueTempl<-1>::M", null, "ValueTempl<-1>.M" },
            new[] { "Ns::Templ<std::string>::M#metadataSet1", "Ns::Templ<std::string>", "M", "Ns::Templ<std::string>::M", "metadataSet1", "Ns.Templ<std::string>.M#metadataSet1" },

            // anonymous namespaces and non-ASCII names
            new[] { "`anonymous-namespace'::Class::Method", "`anonymous-namespace'::Class", "Method", "`anonymous-namespace'::Class::Method", null, "`anonymous-namespace'.Class.Method" },
            new[] { "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test", "TaefSamples::`anonymous-namespace'::Namespace_Anon", "Test", "TaefSamples::`anonymous-namespace'::Namespace_Anon::Test", null, "TaefSamples.`anonymous-namespace'.Namespace_Anon.Test" },
            new[] { "TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test",
                "TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon", "Test",
                "TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test", null,
                "TaefSamples.`anonymous-namespace'.`anonymous-namespace'.Namespace_Anon_Anon.Test" },
            new[] { "TaefSamples::Ümlautß::Täst", "TaefSamples::Ümlautß", "Täst", "TaefSamples::Ümlautß::Täst", null, "TaefSamples.Ümlautß.Täst" },
            new[] { "TaefSamples::Nämespace::KlässWithSetüp::Träits", "TaefSamples::Nämespace::KlässWithSetüp", "Träits", "TaefSamples::Nämespace::KlässWithSetüp::Träits", null, "TaefSamples.Nämespace.KlässWithSetüp.Träits" },
            new[] { "TaefSamples::DataDrivenTästs::Täst#metadataSet0", "TaefSamples::DataDrivenTästs", "Täst", "TaefSamples::DataDrivenTästs::Täst", "metadataSet0", "TaefSamples.DataDrivenTästs.Täst#metadataSet0" },

            // tests of data-driven classes (TE.exe lists the class as <class>#<row>)
            new[] { "ClassData#metadataSet0::M1", "ClassData#metadataSet0", "M1", "ClassData::M1", "metadataSet0", "ClassData#metadataSet0.M1" },
            new[] { "DataNs::ClassData#metadataSet0::M1", "DataNs::ClassData#metadataSet0", "M1", "DataNs::ClassData::M1", "metadataSet0", "DataNs.ClassData#metadataSet0.M1" },
            new[] { "DataNs::ClassData#metadataSet1::M2#metadataSet0", "DataNs::ClassData#metadataSet1", "M2", "DataNs::ClassData::M2", "metadataSet1#metadataSet0", "DataNs.ClassData#metadataSet1.M2#metadataSet0" },
            new[] { "ClassTable#Row A::M", "ClassTable#Row A", "M", "ClassTable::M", "Row A", "ClassTable#Row A.M" },
            new[] { "ClassTable#a::b::M", "ClassTable#a::b", "M", "ClassTable::M", "a::b", "ClassTable#a::b.M" },
            new[] { "ClassTable#2::M", "ClassTable#2", "M", "ClassTable::M", "2", "ClassTable#2.M" },
            new[] { "Ns::ClassTable#2::M", "Ns::ClassTable#2", "M", "Ns::ClassTable::M", "2", "Ns.ClassTable#2.M" },
            new[] { "ClassTable#a::b::M#x::y", "ClassTable#a::b", "M", "ClassTable::M", "a::b#x::y", "ClassTable#a::b.M#x::y" },
        };

        [TestMethod]
        [TestCategory(Unit)]
        public void GetClassName_AllNameForms_ReturnsClassAsListedByTe()
        {
            foreach (string[] nameCase in NameCases)
            {
                TaefNames.GetClassName(nameCase[0]).Should().Be(nameCase[1], $"class of '{nameCase[0]}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetMethodName_AllNameForms_ReturnsMethodWithoutRow()
        {
            foreach (string[] nameCase in NameCases)
            {
                TaefNames.GetMethodName(nameCase[0]).Should().Be(nameCase[2], $"method of '{nameCase[0]}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetBaseName_AllNameForms_ReturnsCppNameOfTestMethod()
        {
            foreach (string[] nameCase in NameCases)
            {
                TaefNames.GetBaseName(nameCase[0]).Should().Be(nameCase[3], $"base name of '{nameCase[0]}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetDataSuffix_AllNameForms_ReturnsRowsOrNull()
        {
            foreach (string[] nameCase in NameCases)
            {
                TaefNames.GetDataSuffix(nameCase[0]).Should().Be(nameCase[4], $"data suffix of '{nameCase[0]}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsFullyQualifiedName_AllNameForms_ReplacesScopeSeparatorsOutsideOfTemplatesAndRows()
        {
            foreach (string[] nameCase in NameCases)
            {
                TaefNames.ToVsFullyQualifiedName(nameCase[0]).Should().Be(nameCase[5], $"VS name of '{nameCase[0]}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllMethods_AllNameForms_AreConsistent()
        {
            foreach (string[] nameCase in NameCases)
            {
                string name = nameCase[0];
                string className = TaefNames.GetClassName(name);
                string method = TaefNames.GetMethodName(name);
                string suffix = TaefNames.GetDataSuffix(name);

                // the name can be rebuilt from its parts
                if (className.Length > 0 && suffix != null && className.Contains("#"))
                {
                    int classRowLength = className.Length - className.IndexOf('#') - 1;
                    string methodRow = suffix.Length > classRowLength ? suffix.Substring(classRowLength + 1) : null;
                    string rebuilt = className + "::" + method + (methodRow == null ? "" : "#" + methodRow);
                    rebuilt.Should().Be(name);
                }
                else
                {
                    string rebuilt = (className.Length > 0 ? className + "::" : "") + method + (suffix == null ? "" : "#" + suffix);
                    rebuilt.Should().Be(name);
                }

                TaefNames.GetBaseName(name).Should().EndWith(method);
                TaefNames.GetBaseName(name).Should().NotContain("#");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetClassName_NamedRowOfDataDrivenClassWithinNamespace_IsTreatedAsMethodRow()
        {
            // known limitation (see TaefNames): a named class row can not be told apart from a method row if the class is
            // within a namespace; the name is treated as data row "Row::M" of method "Ns::C" (affects grouping only: the
            // failures of the class row's fixtures, which TE.exe prints for scope "Ns::C#Row", must still reach test
            // "Ns::C#Row::M", see StreamingTaefOutputParserTests.ReportLine_FailingClassSetupsOfNamedClassRowsWithinNamespace_*,
            // and the test must be selectable exactly, see CommandLineGeneratorSelectionTests)
            const string name = "Ns::C#Row::M";

            TaefNames.GetClassName(name).Should().Be("Ns");
            TaefNames.GetMethodName(name).Should().Be("C");
            TaefNames.GetDataSuffix(name).Should().Be("Row::M");
            TaefNames.ToVsFullyQualifiedName(name).Should().Be("Ns.C#Row::M");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetClassName_RowNameEndingWithIdentifierAfterScopeSeparator_IsTreatedAsMethodRowIfClassHasNamespace()
        {
            // 'x::y' after the first '#' looks like "<class row>::<method>", but the part before the '#' already
            // contains '::' and 'x' is no row name generated by TAEF
            TaefNames.GetClassName("MethodRows::Rows#x::y").Should().Be("MethodRows");

            // while for a class in the global namespace, it is a class row (tests always belong to a class)
            TaefNames.GetClassName("ClassTable#x::y").Should().Be("ClassTable#x");
            TaefNames.GetMethodName("ClassTable#x::y").Should().Be("y");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetClassName_RowNameEndingWithNonIdentifier_IsTreatedAsMethodRow()
        {
            // "::colons [x]" is not followed by an identifier which ends the name
            TaefNames.GetClassName("Class#with::colons [x]").Should().Be("");
            TaefNames.GetMethodName("Class#with::colons [x]").Should().Be("Class");
            TaefNames.GetDataSuffix("Class#with::colons [x]").Should().Be("with::colons [x]");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToVsFullyQualifiedName_TemplateWithNestedTemplatesAndScopes_KeepsTemplateArgumentsUnchanged()
        {
            TaefNames.ToVsFullyQualifiedName("A::B<C::D<E::F>, G::H>::I<J::K>::M#r::s")
                .Should().Be("A.B<C::D<E::F>, G::H>.I<J::K>.M#r::s");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EscapeForSelect_SingleQuotes_AreDoubled()
        {
            TaefNames.EscapeForSelect("TaefSamples::NamedRows::SpecialCharacters#with'quote").Should().Be("TaefSamples::NamedRows::SpecialCharacters#with''quote");
            TaefNames.EscapeForSelect("'a''b'").Should().Be("''a''''b''");
            TaefNames.EscapeForSelect("TaefSamples::TestMath::AddPasses").Should().Be("TaefSamples::TestMath::AddPasses");
            TaefNames.EscapeForSelect("").Should().Be("");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void EscapeForSelect_OtherSpecialCharacters_AreNotChanged()
        {
            const string name = "TaefSamples::NamedRows::SpecialCharacters#with::colons [x] #hash \\ * ? \"quote\" Ünïcødé";
            TaefNames.EscapeForSelect(name).Should().Be(name);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNameSelectionTerm_NameWithQuotes_UsesEscapedName()
        {
            // TaefConstants builds the /select terms from the escaped names
            TaefConstants.GetNameSelectionTerm("TaefSamples::NamedRows::SpecialCharacters#with'quote")
                .Should().Be("@Name='TaefSamples::NamedRows::SpecialCharacters#with''quote'");
            TaefConstants.GetNameSelectionTerm("A::B#with \"double\" quotes")
                .Should().Be("@Name='A::B#with ?double? quotes'");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsDataSourceErrorPseudoTest_NamesEndingWithError_AreRecognized()
        {
            TaefNames.IsDataSourceErrorPseudoTest("TaefSamples::MissingDataSource::Test#error").Should().BeTrue();
            TaefNames.IsDataSourceErrorPseudoTest("MissingNs::ClassMissing#error").Should().BeTrue();

            TaefNames.IsDataSourceErrorPseudoTest("A::B#errors").Should().BeFalse();
            TaefNames.IsDataSourceErrorPseudoTest("A::B#Error").Should().BeFalse();
            TaefNames.IsDataSourceErrorPseudoTest("A::error").Should().BeFalse();
            TaefNames.IsDataSourceErrorPseudoTest("A::B").Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllMethods_Null_Throw()
        {
            Action[] actions =
            {
                () => TaefNames.GetBaseName(null),
                () => TaefNames.GetClassName(null),
                () => TaefNames.GetMethodName(null),
                () => TaefNames.GetDataSuffix(null),
                () => TaefNames.ToVsFullyQualifiedName(null),
                () => TaefNames.EscapeForSelect(null),
                () => TaefNames.IsDataSourceErrorPseudoTest(null)
            };

            foreach (Action action in actions)
            {
                action.Should().Throw<ArgumentNullException>();
            }
        }

    }

}
