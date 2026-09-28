// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.DiaResolver
{

    /// <summary>
    /// Tests of the DIA based <see cref="IDiaResolver"/> with the PDBs of the sample test DLLs. Expected lines are those
    /// of the samples manifest (pdbLine: the opening brace of the test method, or of its out-of-class definition).
    /// </summary>
    [TestClass]
    public class DiaResolverTests
    {
        private FakeLogger _fakeLogger;

        [TestInitialize]
        public void SetUp()
        {
            _fakeLogger = new FakeLogger(() => OutputMode.Info);
        }

        #region GetFunctions

        [TestMethod]
        [TestCategory(Integration)]
        public void GetFunctions_X86_EverythingMatches_ContainsAllTestMethods()
        {
            IList<SourceFileLocation> locations = Resolve(TestResources.LoadTests_ReleaseX86, r => r.GetFunctions("*"));

            locations.Should().HaveCountGreaterOrEqualTo(TestResources.NrOfLoadTests);
            var symbols = new HashSet<string>(locations.Select(l => l.Symbol));
            symbols.Should().Contain(TestResources.TestNames.LoadTest(0)).And.Contain(TestResources.TestNames.LoadTest(4999));
            locations.Should().OnlyContain(l => l.Line > 0 && !string.IsNullOrEmpty(l.Sourcefile));
            _fakeLogger.Errors.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetFunctions_X64_EverythingMatches_ContainsAllTestMethods()
        {
            IList<SourceFileLocation> locations = Resolve(TestResources.DllTests_ReleaseX64, r => r.GetFunctions("*"));

            locations.Select(l => l.Symbol).Should().Contain(new[] { "TaefSamples::Passing::InvokeFunction", "TaefSamples::Failing::InvokeFunction" });
            locations.Should().OnlyContain(l => l.Line > 0);
            _fakeLogger.Errors.Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetFunctions_Wildcard_MatchingFunctionsAreFound()
        {
            IList<SourceFileLocation> locations = Resolve(TestResources.Tests_ReleaseX64, r => r.GetFunctions("*::Namespace_Anon_Anon::Test"));

            locations.Should().ContainSingle()
                .Which.Symbol.Should().Be("TaefSamples::A0x94c2b18a::`anonymous namespace'::Namespace_Anon_Anon::Test");
            locations[0].Line.Should().Be(82);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetFunctions_X86_NonMatchingFilter_NoResults()
        {
            Resolve(TestResources.LoadTests_ReleaseX86, r => r.GetFunctions("ThisFunctionDoesNotExist")).Should().BeEmpty();
            _fakeLogger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetFunctions_X64_NonMatchingFilter_NoResults()
        {
            Resolve(TestResources.DllTests_ReleaseX64, r => r.GetFunctions("ThisFunctionDoesNotExist")).Should().BeEmpty();
            _fakeLogger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        #endregion

        #region FindFunctions

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_TestMethod_IsFoundWithLocation()
        {
            foreach (string testDll in new[] { TestResources.Tests_DebugX86, TestResources.Tests_ReleaseX86, TestResources.Tests_DebugX64, TestResources.Tests_ReleaseX64 })
            {
                IList<SourceFileLocation> locations = Resolve(testDll, r => r.FindFunctions("TaefSamples::TestMath::AddPasses"));

                locations.Should().ContainSingle(testDll);
                locations[0].Symbol.Should().Be("TaefSamples::TestMath::AddPasses");
                locations[0].Sourcefile.Should().EndWithEquivalent(@"SampleTests\Tests\BasicTests.cpp");
                locations[0].Line.Should().Be(26, testDll);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_PdbSpellingsOfTemplatesAndAnonymousNamespaces_AreFound()
        {
            AssertFindsFunction(TestResources.Tests_ReleaseX64, "TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >::CanIterate", @"Tests\ClassTemplateTests.cpp", 57);
            AssertFindsFunction(TestResources.Tests_ReleaseX64, "TaefSamples::TemplateTests<TaefSamples::ReversedArray>::TwoTraits", @"Tests\ClassTemplateTests.cpp", 68);
            AssertFindsFunction(TestResources.Tests_ReleaseX64, "TaefSamples::`anonymous namespace'::Namespace_Anon::Test", @"Tests\NamespaceTests.cpp", 70);
            AssertFindsFunction(TestResources.Tests_ReleaseX64, "TaefSamples::Namespace_1::`anonymous namespace'::Namespace_Named_Anon::Test", @"Tests\NamespaceTests.cpp", 56);
            AssertFindsFunction(TestResources.Tests_ReleaseX64, "TaefSamples::Nämespace::KlässWithSetüp::Träits", @"Tests\UmlautTests.cpp", 59);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_TaefSpellingsOfTemplatesAndAnonymousNamespaces_AreNotFound()
        {
            // see SymbolNames: TAEF spells names like typeid(), the PDB does not
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindFunctions(TestResources.TestNames.TemplateTest)).Should().BeEmpty();
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindFunctions(TestResources.TestNames.AnonymousNamespaceTest)).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_NameIsCaseSensitive()
        {
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindFunctions("taefsamples::testmath::addpasses")).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_FunctionOfPlainDll_IsFound()
        {
            AssertFindsFunction(TestResources.DllTestsDll_ReleaseX86, "ReturnZero", @"DllProject\DllProject.cpp", 5);
            AssertFindsFunction(TestResources.DllTestsDll_DebugX64, "ReturnZero", @"DllProject\DllProject.cpp", 5);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_Null_Throws()
        {
            string pdb = PdbLocator.FindPdbFile(TestResources.DllTests_ReleaseX64, "", _fakeLogger);
            using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(TestResources.DllTests_ReleaseX64, pdb, _fakeLogger))
            {
                resolver.Invoking(r => r.FindFunctions(null)).Should().Throw<ArgumentNullException>();
                resolver.Invoking(r => r.FindMemberFunctions(null, new[] { "M" })).Should().Throw<ArgumentNullException>();
                resolver.Invoking(r => r.FindMemberFunctions("C", null)).Should().Throw<ArgumentNullException>();
            }
        }

        #endregion

        #region FindMemberFunctions

        [TestMethod]
        [TestCategory(Integration)]
        public void FindMemberFunctions_ClassWithTestMethods_RequestedMembersAreFound()
        {
            IDictionary<string, SourceFileLocation> members = Resolve(TestResources.Tests_DebugX86,
                r => r.FindMemberFunctions("TaefSamples::TestMath", new[] { "AddFails", "AddPasses", "AddPassesWithTraits", "DoesNotExist" }));

            members.Keys.Should().BeEquivalentTo("AddFails", "AddPasses", "AddPassesWithTraits");
            members["AddFails"].Line.Should().Be(21);
            members["AddPasses"].Line.Should().Be(26);
            members["AddPassesWithTraits"].Line.Should().Be(36, "the out-of-class definition of a BEGIN_TEST_METHOD test");
            members["AddPasses"].Symbol.Should().Be("TaefSamples::TestMath::AddPasses");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindMemberFunctions_SameMethodNameInClassesWhoseNamesAreSuffixesOfEachOther_CorrectClassIsUsed()
        {
            Resolve(TestResources.Tests_ReleaseX86, r => r.FindMemberFunctions("TaefSamples::abcd", new[] { "t" }))["t"].Line.Should().Be(121);
            Resolve(TestResources.Tests_ReleaseX86, r => r.FindMemberFunctions("TaefSamples::bbcd", new[] { "t" }))["t"].Line.Should().Be(131);
            Resolve(TestResources.Tests_ReleaseX86, r => r.FindMemberFunctions("TaefSamples::bcd", new[] { "t" }))["t"].Line.Should().Be(141);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindMemberFunctions_TypesInNamespacesAndTemplates_AreFound()
        {
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindMemberFunctions("TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep", new[] { "Fails" }))
                ["Fails"].Line.Should().Be(42);
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindMemberFunctions("TaefSamples::ClassTemplates::NumberTemplateTests<signed char>", new[] { "CanHoldLargeSum" }))
                ["CanHoldLargeSum"].Line.Should().Be(93);
            // (classes within anonymous namespaces are not found this way, see TestCaseResolver - their methods are found with FindFunctions())
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindMemberFunctions_NonExistingTypeOrNoMembers_EmptyResult()
        {
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindMemberFunctions("DoesNotExist", new[] { "Test" })).Should().BeEmpty();
            Resolve(TestResources.Tests_ReleaseX64, r => r.FindMemberFunctions("TaefSamples::TestMath", new string[0])).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindMemberFunctions_5000Members_AreFoundQuickly()
        {
            string[] members = Enumerable.Range(0, TestResources.NrOfLoadTests).Select(i => $"Test{i}").ToArray();

            var stopwatch = Stopwatch.StartNew();
            IDictionary<string, SourceFileLocation> locations = Resolve(TestResources.LoadTests_ReleaseX64, r => r.FindMemberFunctions("TaefSamples::LoadTests", members));
            stopwatch.Stop();

            locations.Should().HaveCount(TestResources.NrOfLoadTests);
            locations.Values.Should().OnlyContain(l => l.Sourcefile.EndsWith(@"LoadTests\LoadTests.cpp", StringComparison.OrdinalIgnoreCase));
            // looking up the class once is much faster than 5000 lookups by name (generous bound for loaded machines)
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(CiSupport.GetWeightedDuration(10000)));
        }

        #endregion

        #region Missing and broken PDBs

        [TestMethod]
        [TestCategory(Integration)]
        public void Create_PdbDoesNotExist_ErrorIsLoggedAndNothingIsFound()
        {
            string testDll = TestResources.LoadTests_ReleaseX86;
            testDll.AsFileInfo().Should().Exist();
            string pdb = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdb");
            var fakeLogger = new FakeLogger(() => OutputMode.Verbose);

            using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(testDll, pdb, fakeLogger))
            {
                resolver.GetFunctions("*").Should().BeEmpty();
                resolver.FindFunctions("TaefSamples::LoadTests::Test0").Should().BeEmpty();
                resolver.FindMemberFunctions("LoadTests", new[] { "Test0" }).Should().BeEmpty();
            }

            fakeLogger.Errors.Should().Contain(msg => msg.Contains("PDB file") && msg.Contains("does not exist"));
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Create_BrokenPdb_WarningIsLoggedAndNothingIsFound()
        {
            using (var directory = new TemporaryDirectory())
            {
                string pdb = directory.CreateFile("Broken.pdb", "This is not a PDB file");
                var fakeLogger = new FakeLogger(() => OutputMode.Verbose);

                using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(TestResources.LoadTests_ReleaseX86, pdb, fakeLogger))
                {
                    resolver.GetFunctions("*").Should().BeEmpty();
                    resolver.FindFunctions("TaefSamples::LoadTests::Test0").Should().BeEmpty();
                }

                fakeLogger.Warnings.Should().Contain(msg => msg.Contains("Could not load PDB file") && msg.Contains(pdb));
                fakeLogger.Errors.Should().BeEmpty();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Create_PdbOfOtherDll_FunctionsOfThatPdbAreFound()
        {
            // additional PDBs are not checked against the test DLL
            string otherPdb = PdbLocator.FindPdbFile(TestResources.DllTests_ReleaseX64, "", _fakeLogger);

            using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(TestResources.LoadTests_ReleaseX64, otherPdb, _fakeLogger))
            {
                resolver.FindFunctions("TaefSamples::Passing::InvokeFunction").Should().ContainSingle();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            string pdb = PdbLocator.FindPdbFile(TestResources.DllTests_ReleaseX64, "", _fakeLogger);
            IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(TestResources.DllTests_ReleaseX64, pdb, _fakeLogger);
            resolver.FindFunctions("TaefSamples::Passing::InvokeFunction").Should().ContainSingle();

            resolver.Dispose();
            resolver.Invoking(r => r.Dispose()).Should().NotThrow();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Dispose_PdbIsReleased_PdbCanBeDeleted()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_ReleaseX86))
            {
                using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(sample.TestDll, sample.Pdb, _fakeLogger))
                {
                    resolver.FindFunctions("TaefSamples::Passing::InvokeFunction").Should().ContainSingle();
                }

                File.Delete(sample.Pdb);
                sample.Pdb.AsFileInfo().Should().NotExist();
            }
        }

        #endregion

        private T Resolve<T>(string testDll, Func<IDiaResolver, T> resolve)
        {
            string pdb = PdbLocator.FindPdbFile(testDll, "", _fakeLogger);
            pdb.Should().NotBeNull($"the PDB of {testDll} should exist");
            using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(testDll, pdb, _fakeLogger))
            {
                return resolve(resolver);
            }
        }

        private void AssertFindsFunction(string binary, string function, string file, uint line)
        {
            IList<SourceFileLocation> locations = Resolve(binary, r => r.FindFunctions(function));

            locations.Should().NotBeEmpty(function);
            locations[0].Symbol.Should().Be(function);
            locations[0].Sourcefile.Should().EndWithEquivalent(@"SampleTests\" + file);
            locations[0].Line.Should().Be(line, function);
        }
    }

}
