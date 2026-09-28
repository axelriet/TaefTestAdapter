// This file has been added for TAEF support.

using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestCases
{

    [TestClass]
    public class SymbolNamesTests
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void GetFunctionNameCandidates_Template_FirstCandidateHasPdbSpelling()
        {
            // TAEF spells template arguments like typeid(), the PDB omits the class keys (see samples manifest, diaName)
            SymbolNames.GetFunctionNameCandidates("TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate")[0]
                .Should().Be("TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >::CanIterate");
            SymbolNames.GetFunctionNameCandidates("TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>::CanIterate")[0]
                .Should().Be("TaefSamples::TemplateTests<TaefSamples::ReversedArray>::CanIterate");
            SymbolNames.GetFunctionNameCandidates("TaefSamples::ÜmlautTemplateTests<class TaefSamples::ImplementationA>::Täst")[0]
                .Should().Be("TaefSamples::ÜmlautTemplateTests<TaefSamples::ImplementationA>::Täst");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetFunctionNameCandidates_AnonymousNamespace_FirstCandidateHasPdbSpelling()
        {
            SymbolNames.GetFunctionNameCandidates("TaefSamples::`anonymous-namespace'::Namespace_Anon::Test")[0]
                .Should().Be("TaefSamples::`anonymous namespace'::Namespace_Anon::Test");
            SymbolNames.GetFunctionNameCandidates("TaefSamples::Namespace_1::`anonymous-namespace'::Namespace_Named_Anon::Test")[0]
                .Should().Be("TaefSamples::Namespace_1::`anonymous namespace'::Namespace_Named_Anon::Test");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetFunctionNameCandidates_EnumTemplateArgument_AlsoTriesSpellingWithoutEnumKey()
        {
            var candidates = SymbolNames.GetFunctionNameCandidates("TemplTests<enum Color>::M");

            candidates[0].Should().Be("TemplTests<enum Color>::M");
            candidates.Should().Contain("TemplTests<Color>::M");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetFunctionNameCandidates_PlainName_OnlyContainsName()
        {
            SymbolNames.GetFunctionNameCandidates("Ns::Class::Method").Should().Equal("Ns::Class::Method");
            SymbolNames.GetFunctionNameCandidates("TaefSamples::Ümlautß::Täst").Should().Equal("TaefSamples::Ümlautß::Täst");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetFunctionNameCandidates_AllCandidates_AreDistinctAndContainOriginalName()
        {
            const string name = "Outer::`anonymous-namespace'::Templ<class A,enum E>::M";

            var candidates = SymbolNames.GetFunctionNameCandidates(name);

            candidates.Should().OnlyHaveUniqueItems();
            candidates.Should().Contain(name);
            candidates[0].Should().Be("Outer::`anonymous namespace'::Templ<A,enum E>::M");
            candidates.Should().Contain("Outer::`anonymous namespace'::Templ<A,E>::M");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTypeNameCandidates_AnonymousNamespace_KeepsTaefSpellingFirst()
        {
            // type names in PDBs use TAEF's spelling of anonymous namespaces
            var candidates = SymbolNames.GetTypeNameCandidates("`anonymous-namespace'::Inner::AnonInner");

            candidates[0].Should().Be("`anonymous-namespace'::Inner::AnonInner");
            candidates.Should().Contain("`anonymous namespace'::Inner::AnonInner");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTypeNameCandidates_Template_RemovesClassKeysFirst()
        {
            var candidates = SymbolNames.GetTypeNameCandidates("TaefSamples::TemplateTests<class std::array<int,3> >");

            candidates[0].Should().Be("TaefSamples::TemplateTests<std::array<int,3> >");
            candidates.Should().Contain("TaefSamples::TemplateTests<class std::array<int,3> >");
            SymbolNames.GetTypeNameCandidates("TemplTests<enum Color>")[0].Should().Be("TemplTests<enum Color>");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Normalize_TaefAndPdbSpellings_AreEqual()
        {
            SymbolNames.Normalize("TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate")
                .Should().Be(SymbolNames.Normalize("TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >::CanIterate"));
            // nested anonymous namespaces are named by a hash in the PDB (see samples manifest, diaName)
            SymbolNames.Normalize("TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test")
                .Should().Be(SymbolNames.Normalize("TaefSamples::A0x94c2b18a::`anonymous namespace'::Namespace_Anon_Anon::Test"));
            SymbolNames.Normalize("`anonymous-namespace'::`anonymous-namespace'::`anonymous-namespace'::TripleAnon::M")
                .Should().Be(SymbolNames.Normalize("A0x2c3be4bd::A0x2c3be4bd::`anonymous namespace'::TripleAnon::M"));
            SymbolNames.Normalize("TemplTests<enum Color>::M").Should().Be(SymbolNames.Normalize("TemplTests<Color>::M"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Normalize_DifferentNames_AreDifferent()
        {
            SymbolNames.Normalize("A::Same::Run").Should().NotBe(SymbolNames.Normalize("B::Same::Run"));
            SymbolNames.Normalize("`anonymous-namespace'::X::M").Should().NotBe(SymbolNames.Normalize("Named::X::M"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Normalize_IdentifiersContainingKeywords_AreNotChanged()
        {
            SymbolNames.Normalize("Myclass::M").Should().Be("Myclass::M");
            SymbolNames.Normalize("subclass::structure::unions::enumerate").Should().Be("subclass::structure::unions::enumerate");
            SymbolNames.Normalize("A0x12::M").Should().Be("A0x12::M");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ContainsAnonymousNamespace_AllSpellings_AreRecognized()
        {
            SymbolNames.ContainsAnonymousNamespace("`anonymous-namespace'::X::M").Should().BeTrue();
            SymbolNames.ContainsAnonymousNamespace("`anonymous namespace'::X::M").Should().BeTrue();
            SymbolNames.ContainsAnonymousNamespace("A0x94c2b18a::`anonymous namespace'::X::M").Should().BeTrue();
            SymbolNames.ContainsAnonymousNamespace("A0x94c2b18a::X::M").Should().BeTrue();

            SymbolNames.ContainsAnonymousNamespace("Namespace_1::X::M").Should().BeFalse();
            SymbolNames.ContainsAnonymousNamespace(null).Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetPartAfterLastAnonymousNamespace_ReturnsRemainder()
        {
            SymbolNames.GetPartAfterLastAnonymousNamespace("`anonymous-namespace'::`anonymous-namespace'::X::M").Should().Be("X::M");
            SymbolNames.GetPartAfterLastAnonymousNamespace("Outer::`anonymous namespace'::InNamedThenAnon::M").Should().Be("InNamedThenAnon::M");
            SymbolNames.GetPartAfterLastAnonymousNamespace("Ns::X::M").Should().Be("Ns::X::M");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllMethods_Null_Throw()
        {
            Action[] actions =
            {
                () => SymbolNames.GetFunctionNameCandidates(null),
                () => SymbolNames.GetTypeNameCandidates(null),
                () => SymbolNames.Normalize(null),
                () => SymbolNames.GetPartAfterLastAnonymousNamespace(null)
            };

            foreach (Action action in actions)
            {
                action.Should().Throw<ArgumentNullException>();
            }
        }

    }

}
