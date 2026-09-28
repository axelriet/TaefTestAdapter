// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestCases
{

    [TestClass]
    public class TestCaseResolverTests : TestsBase
    {
        private FakeLogger _fakeLogger;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _fakeLogger = new FakeLogger(() => OutputMode.Verbose, false);
        }

        #region Unit tests (mocked DIA)

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_MembersOfClass_AreLookedUpOncePerClass()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                var resolver = new Mock<IDiaResolver>();
                resolver.Setup(r => r.FindMemberFunctions("Ns::Class", It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation>
                    {
                        { "A", new SourceFileLocation("Ns::Class::A", @"C:\src\x.cpp", 1) },
                        { "B", new SourceFileLocation("Ns::Class::B", @"C:\src\x.cpp", 2) }
                    });
                var factory = CreateFactory(resolver.Object);

                IDictionary<string, TestCaseLocation> locations = new TestCaseResolver(testDll, factory.Object, MockOptions.Object, _fakeLogger)
                    .FindTestCaseLocations(new[] { "Ns::Class::A", "Ns::Class::B", "Ns::Class::A", "", null });

                locations.Keys.Should().BeEquivalentTo("Ns::Class::A", "Ns::Class::B");
                locations["Ns::Class::A"].Line.Should().Be(1);
                locations["Ns::Class::B"].Sourcefile.Should().Be(@"C:\src\x.cpp");
                locations["Ns::Class::B"].Symbol.Should().Be("Ns::Class::B");
                resolver.Verify(r => r.FindMemberFunctions("Ns::Class", It.Is<ICollection<string>>(c => c.Count == 2 && c.Contains("A") && c.Contains("B"))), Times.Once);
                resolver.Verify(r => r.FindFunctions(It.IsAny<string>()), Times.Never);
                resolver.Verify(r => r.GetFunctions(It.IsAny<string>()), Times.Never);
                factory.Verify(f => f.Create(testDll, Path.ChangeExtension(testDll, ".pdb"), It.IsAny<ILogger>()), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_TemplateClass_IsLookedUpWithPdbSpelling()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                var resolver = new Mock<IDiaResolver>();
                resolver.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation>());
                resolver.Setup(r => r.FindMemberFunctions("TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >", It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation>
                    {
                        { "CanIterate", new SourceFileLocation("TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >::CanIterate", @"C:\src\t.cpp", 54) }
                    });

                TestCaseLocation location = new TestCaseResolver(testDll, CreateFactory(resolver.Object).Object, MockOptions.Object, _fakeLogger)
                    .FindTestCaseLocation(TestResources.TestNames.TemplateTest);

                location.Should().NotBeNull();
                location.Line.Should().Be(54);
                location.Symbol.Should().Be("TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >::CanIterate");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_MemberNotFound_FunctionIsLookedUpByName()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                var resolver = new Mock<IDiaResolver>();
                resolver.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation>());
                resolver.Setup(r => r.FindFunctions(It.IsAny<string>())).Returns(new List<SourceFileLocation>());
                resolver.Setup(r => r.FindFunctions("GlobalFunction"))
                    .Returns(new List<SourceFileLocation> { new SourceFileLocation("GlobalFunction", @"C:\src\g.cpp", 3), new SourceFileLocation("GlobalFunction", @"C:\src\g.cpp", 9) });
                resolver.Setup(r => r.FindFunctions("Templ<A>::M"))
                    .Returns(new List<SourceFileLocation> { new SourceFileLocation("Templ<A>::M", @"C:\src\t.cpp", 4) });

                IDictionary<string, TestCaseLocation> locations = new TestCaseResolver(testDll, CreateFactory(resolver.Object).Object, MockOptions.Object, _fakeLogger)
                    .FindTestCaseLocations(new[] { "GlobalFunction", "Templ<class A>::M", "Does::Not::Exist" });

                locations.Keys.Should().BeEquivalentTo("GlobalFunction", "Templ<class A>::M");
                locations["GlobalFunction"].Line.Should().Be(3, "the first function found is taken (the parameterless one)");
                locations["Templ<class A>::M"].Line.Should().Be(4);
                resolver.Verify(r => r.FindFunctions("Does::Not::Exist"), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_NestedAnonymousNamespaces_AreFoundByNormalizedName()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                var resolver = new Mock<IDiaResolver>();
                resolver.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation>());
                resolver.Setup(r => r.FindFunctions(It.IsAny<string>())).Returns(new List<SourceFileLocation>());
                resolver.Setup(r => r.GetFunctions("*::Namespace_Anon_Anon::Test"))
                    .Returns(new List<SourceFileLocation>
                    {
                        new SourceFileLocation("Other::Namespace_Anon_Anon::Test", @"C:\src\wrong.cpp", 1),
                        new SourceFileLocation("TaefSamples::A0x94c2b18a::`anonymous namespace'::Namespace_Anon_Anon::Test", @"C:\src\NamespaceTests.cpp", 82)
                    });

                const string name = "TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test";
                TestCaseLocation location = new TestCaseResolver(testDll, CreateFactory(resolver.Object).Object, MockOptions.Object, _fakeLogger)
                    .FindTestCaseLocation(name);

                location.Should().NotBeNull();
                location.Sourcefile.Should().Be(@"C:\src\NamespaceTests.cpp");
                location.Line.Should().Be(82);
                resolver.Verify(r => r.GetFunctions("*::Namespace_Anon_Anon::Test"), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_NoPdbOfTestDll_AdditionalPdbsAreSearched()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.CreateFile("Fake_taef.dll");
                string pdb1 = directory.CreateFile(@"pdbs\1.pdb");
                string pdb2 = directory.CreateFile(@"pdbs\2.pdb");
                MockOptions.Setup(o => o.AdditionalPdbs).Returns($"{PlaceholderReplacer.TestDllDirPlaceholder}\\pdbs\\1.pdb;{PlaceholderReplacer.TestDllDirPlaceholder}\\pdbs\\2.pdb");

                var resolver1 = new Mock<IDiaResolver>();
                resolver1.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation> { { "A", new SourceFileLocation("C::A", @"C:\src\1.cpp", 1) } });
                resolver1.Setup(r => r.FindFunctions(It.IsAny<string>())).Returns(new List<SourceFileLocation>());
                var resolver2 = new Mock<IDiaResolver>();
                resolver2.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation> { { "B", new SourceFileLocation("C::B", @"C:\src\2.cpp", 2) } });
                var factory = new Mock<IDiaResolverFactory>();
                factory.Setup(f => f.Create(testDll, pdb1, It.IsAny<ILogger>())).Returns(resolver1.Object);
                factory.Setup(f => f.Create(testDll, pdb2, It.IsAny<ILogger>())).Returns(resolver2.Object);

                var testCaseResolver = new TestCaseResolver(testDll, factory.Object, MockOptions.Object, _fakeLogger);
                IDictionary<string, TestCaseLocation> locations = testCaseResolver.FindTestCaseLocations(new[] { "C::A", "C::B" });

                testCaseResolver.TestDllPdb.Should().BeNull();
                locations["C::A"].Sourcefile.Should().Be(@"C:\src\1.cpp");
                locations["C::B"].Sourcefile.Should().Be(@"C:\src\2.cpp");
                resolver2.Verify(r => r.FindMemberFunctions("C", It.Is<ICollection<string>>(c => c.Count == 1 && c.Contains("B"))), Times.Once,
                    "only the methods not found in the first PDB are searched in the second one");
                resolver1.Verify(r => r.Dispose(), Times.Once);
                resolver2.Verify(r => r.Dispose(), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_AllFoundInTestDllPdb_AdditionalPdbsAreNotSearched()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                directory.CreateFile(@"pdbs\1.pdb");
                MockOptions.Setup(o => o.AdditionalPdbs).Returns($"{PlaceholderReplacer.TestDllDirPlaceholder}\\pdbs\\*.pdb");
                var resolver = new Mock<IDiaResolver>();
                resolver.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation> { { "A", new SourceFileLocation("C::A", @"C:\src\1.cpp", 1) } });
                var factory = CreateFactory(resolver.Object);

                var testCaseResolver = new TestCaseResolver(testDll, factory.Object, MockOptions.Object, _fakeLogger);
                testCaseResolver.FindTestCaseLocations(new[] { "C::A" }).Should().ContainKey("C::A");

                testCaseResolver.TestDllPdb.Should().Be(Path.ChangeExtension(testDll, ".pdb"));
                factory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_AdditionalPdbPatternWithoutMatches_WarningIsLogged()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.CreateFile("Fake_taef.dll");
                MockOptions.Setup(o => o.AdditionalPdbs).Returns($"{PlaceholderReplacer.TestDllDirPlaceholder}\\*.doesnotexist");
                var factory = new Mock<IDiaResolverFactory>();

                new TestCaseResolver(testDll, factory.Object, MockOptions.Object, _fakeLogger)
                    .FindTestCaseLocations(new[] { "C::A" })
                    .Should().BeEmpty();

                _fakeLogger.Warnings.Should().Contain(w => w.Contains("*.doesnotexist") && w.Contains("does not match any files"));
                factory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void FindTestCaseLocations_DiaResolverThrows_ExceptionIsLoggedAndNextSourceIsSearched()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                string additionalPdb = directory.CreateFile(@"pdbs\1.pdb");
                MockOptions.Setup(o => o.AdditionalPdbs).Returns(additionalPdb);
                var resolver = new Mock<IDiaResolver>();
                resolver.Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                    .Returns(new Dictionary<string, SourceFileLocation> { { "A", new SourceFileLocation("C::A", @"C:\src\1.cpp", 1) } });
                var factory = new Mock<IDiaResolverFactory>();
                factory.Setup(f => f.Create(testDll, Path.ChangeExtension(testDll, ".pdb"), It.IsAny<ILogger>())).Throws(new DiaTestException("broken pdb"));
                factory.Setup(f => f.Create(testDll, additionalPdb, It.IsAny<ILogger>())).Returns(resolver.Object);

                IDictionary<string, TestCaseLocation> locations = new TestCaseResolver(testDll, factory.Object, MockOptions.Object, _fakeLogger)
                    .FindTestCaseLocations(new[] { "C::A" });

                locations.Should().ContainKey("C::A");
                _fakeLogger.Errors.Should().Contain(e => e.Contains("broken pdb"));
            }
        }

        #endregion

        #region Integration tests (sample DLLs and their PDBs)

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Named_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::Namespace_1::Namespace_Named::Test", 18);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Named_Named_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_Named_Named::Test", 30);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Deeply_Nested_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep::Fails", 42);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Named_Anon_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::Namespace_1::`anonymous-namespace'::Namespace_Named_Anon::Test", 56);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Anon_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::`anonymous-namespace'::Namespace_Anon::Test", 70);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Anon_Anon_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test", 82);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Anon_Named_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::`anonymous-namespace'::Anon_Nested::Namespace_Anon_Named::Test", 95);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocation_Namespace_Root_LocationIsFound()
        {
            AssertCorrectTestLocationIsFound("TaefSamples::Namespace_Root::Test", 108);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocations_TemplatesUmlautsAndOutOfClassDefinitions_LocationsAreFound()
        {
            var resolver = new TestCaseResolver(TestResources.Tests_ReleaseX64, new DefaultDiaResolverFactory(), MockOptions.Object, _fakeLogger);

            // lines: see samples manifest (pdbLine); for BEGIN_TEST_METHOD, the location of the out-of-class definition
            IDictionary<string, TestCaseLocation> locations = resolver.FindTestCaseLocations(new[]
            {
                "TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>::TwoTraits",
                "TaefSamples::TemplateTests<class std::array<int,3> >::CanDefeatMath",
                "TaefSamples::ClassTemplates::NumberTemplateTests<signed char>::CanHoldLargeSum",
                "TaefSamples::ÜmlautTemplateTests<class TaefSamples::ImplementationB>::Täst",
                "TaefSamples::Nämespace::KlässWithSetüp::Träits",
                "TaefSamples::Traits::With8Traits",
                "TaefSamples::ClassWithFixtures::AddPassesWithTraits"
            });

            _fakeLogger.Errors.Should().BeEmpty();
            resolver.TestDllPdb.Should().Be(Path.ChangeExtension(TestResources.Tests_ReleaseX64, ".pdb"));
            locations.Should().HaveCount(7);
            locations["TaefSamples::TemplateTests<struct TaefSamples::ReversedArray>::TwoTraits"].Line.Should().Be(68);
            locations["TaefSamples::TemplateTests<class std::array<int,3> >::CanDefeatMath"].Line.Should().Be(39);
            locations["TaefSamples::ClassTemplates::NumberTemplateTests<signed char>::CanHoldLargeSum"].Line.Should().Be(93);
            locations["TaefSamples::ÜmlautTemplateTests<class TaefSamples::ImplementationB>::Täst"].Line.Should().Be(121);
            locations["TaefSamples::ÜmlautTemplateTests<class TaefSamples::ImplementationB>::Täst"].Sourcefile.Should().EndWithEquivalent(@"SampleTests\Tests\UmlautTests.cpp");
            locations["TaefSamples::Nämespace::KlässWithSetüp::Träits"].Line.Should().Be(59);
            locations["TaefSamples::Traits::With8Traits"].Line.Should().Be(84);
            locations["TaefSamples::ClassWithFixtures::AddPassesWithTraits"].Line.Should().Be(83);
            locations["TaefSamples::ClassWithFixtures::AddPassesWithTraits"].Sourcefile.Should().EndWithEquivalent(@"SampleTests\Tests\FixtureTests.cpp");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocations_FunctionOfImportedDll_IsFoundInPdbOfImportedDll()
        {
            var resolver = new TestCaseResolver(TestResources.DllTests_ReleaseX86, new DefaultDiaResolverFactory(), MockOptions.Object, _fakeLogger);

            IDictionary<string, TestCaseLocation> locations = resolver.FindTestCaseLocations(new[] { "TaefSamples::Passing::InvokeFunction", "ReturnZero" });

            locations["TaefSamples::Passing::InvokeFunction"].Sourcefile.Should().EndWithEquivalent(@"SampleTests\DllDependentProject\DllTests.cpp");
            locations["TaefSamples::Passing::InvokeFunction"].Line.Should().Be(13);
            locations["ReturnZero"].Sourcefile.Should().EndWithEquivalent(@"SampleTests\DllProject\DllProject.cpp");
            locations["ReturnZero"].Line.Should().Be(5);
            _fakeLogger.Infos.Should().Contain(i => i.Contains("binary '" + TestResources.DllTestsDll_ReleaseX86 + "'"),
                "the imported DLL's PDB has been searched");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindTestCaseLocations_NonExistingFunction_ReturnsNoLocation()
        {
            var resolver = new TestCaseResolver(TestResources.Tests_ReleaseX86, new DefaultDiaResolverFactory(), MockOptions.Object, _fakeLogger);

            resolver.FindTestCaseLocation("TaefSamples::TestMath::DoesNotExist").Should().BeNull();
            resolver.FindTestCaseLocation("DoesNotExist::AddPasses").Should().BeNull();
            _fakeLogger.Errors.Should().BeEmpty();
        }

        private void AssertCorrectTestLocationIsFound(string testName, uint line)
        {
            var resolver = new TestCaseResolver(TestResources.Tests_ReleaseX64, new DefaultDiaResolverFactory(), MockOptions.Object, _fakeLogger);

            TestCaseLocation testCaseLocation = resolver.FindTestCaseLocation(testName);

            _fakeLogger.Errors.Should().BeEmpty();
            testCaseLocation.Should().NotBeNull();
            testCaseLocation.Sourcefile.Should().EndWithEquivalent(@"sampletests\tests\namespacetests.cpp");
            testCaseLocation.Line.Should().Be(line);
        }

        #endregion

        #region Helpers

        private static Mock<IDiaResolverFactory> CreateFactory(IDiaResolver resolver)
        {
            var factory = new Mock<IDiaResolverFactory>();
            factory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>())).Returns(resolver);
            return factory;
        }

        private static string CreateFakeDllWithPdb(TemporaryDirectory directory)
        {
            string testDll = directory.CreateFile("Fake_taef.dll");
            directory.CreateFile("Fake_taef.pdb");
            return testDll;
        }

        private class DiaTestException : Exception
        {
            public DiaTestException(string message) : base(message) { }
        }

        #endregion

    }

}
