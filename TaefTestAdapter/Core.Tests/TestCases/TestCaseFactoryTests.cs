// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.ProcessExecution.Contracts;
using TaefTestAdapter.Runners;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestCases
{

    [TestClass]
    public class TestCaseFactoryTests : TestsBase
    {
        private readonly IProcessExecutorFactory _processExecutorFactory = new ProcessExecutorFactory();

        private Mock<IDiaResolverFactory> _mockDiaResolverFactory;
        private Mock<IDiaResolver> _mockDiaResolver;
        private Dictionary<string, SourceFileLocation> _pdbFunctions;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            // a fake PDB: functions by their PDB names (see SetupFakePdb)
            _pdbFunctions = new Dictionary<string, SourceFileLocation>();
            _mockDiaResolver = new Mock<IDiaResolver>();
            _mockDiaResolver
                .Setup(r => r.FindMemberFunctions(It.IsAny<string>(), It.IsAny<ICollection<string>>()))
                .Returns((string typeName, ICollection<string> members) => members
                    .Where(m => _pdbFunctions.ContainsKey(typeName + "::" + m))
                    .ToDictionary(m => m, m => _pdbFunctions[typeName + "::" + m]));
            _mockDiaResolver
                .Setup(r => r.FindFunctions(It.IsAny<string>()))
                .Returns((string name) => _pdbFunctions.TryGetValue(name, out SourceFileLocation location)
                    ? new List<SourceFileLocation> { location }
                    : new List<SourceFileLocation>());
            _mockDiaResolver
                .Setup(r => r.GetFunctions(It.IsAny<string>()))
                .Returns(new List<SourceFileLocation>());
            _mockDiaResolverFactory = new Mock<IDiaResolverFactory>();
            _mockDiaResolverFactory
                .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                .Returns(_mockDiaResolver.Object);
        }

        #region Test case creation from descriptors (no TE.exe)

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_DoNotParseSymbolInformation_TestCasesWithoutLocation()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);

            IList<TestCase> testCases = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(new[]
            {
                new TestCaseDescriptor("Ns::Class::Method", "Ns::Class"),
                new TestCaseDescriptor("Ns::Class::Row#with::colons [x]", "Ns::Class")
            });

            testCases.Select(tc => tc.FullyQualifiedName).Should().Equal("Ns::Class::Method", "Ns::Class::Row#with::colons [x]");
            testCases.Select(tc => tc.DisplayName).Should().Equal("Ns::Class::Method", "Ns::Class::Row#with::colons [x]");
            testCases.Should().OnlyContain(tc => tc.Source == TestDataCreator.DummyTestDll && tc.CodeFilePath == "" && tc.LineNumber == 0);
            _mockDiaResolverFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_TaefProperties_AreReportedAsTraitsExceptInternalOnes()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            var descriptor = new TestCaseDescriptor("Ns::Class::Method#metadataSet1", "Ns::Class", properties: new[]
            {
                new TaefProperty("Architecture", "x64"),
                new TaefProperty("Owner", "Me"),
                new TaefProperty("TaefTestType", "Native"),
                new TaefProperty("Category", "Smoke Nightly"),
                new TaefProperty("Metadata:Index", "1"),
                new TaefProperty("Data:Color", "{Red,Blue}"),
                new TaefProperty("DataSource", "Table:x.xml#T"),
                new TaefProperty("description", "Not a trait"),
                new TaefProperty("Ignore", "true"),
                new TaefProperty("Empty", "")
            });

            TestCase testCase = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(new[] { descriptor }).Single();

            testCase.Traits.Select(t => t.ToString()).Should().Equal(
                "(Architecture,x64)", "(Owner,Me)", "(Category,Smoke Nightly)", "(Ignore,true)", "(Empty,)");
            TaefConstants.IsIgnored(testCase).Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsReportedAsTrait_InternalTaefProperties_AreNotReported()
        {
            foreach (string property in TaefConstants.PropertiesNotReportedAsTraits)
            {
                TestCaseFactory.IsReportedAsTrait(property).Should().BeFalse(property);
                TestCaseFactory.IsReportedAsTrait(property.ToUpperInvariant()).Should().BeFalse(property.ToUpperInvariant());
            }
            TestCaseFactory.IsReportedAsTrait("Data:Color").Should().BeFalse();
            TestCaseFactory.IsReportedAsTrait("data:x").Should().BeFalse();

            TestCaseFactory.IsReportedAsTrait("Ignore").Should().BeTrue();
            TestCaseFactory.IsReportedAsTrait("Owner").Should().BeTrue();
            TestCaseFactory.IsReportedAsTrait("Priority").Should().BeTrue();
            TestCaseFactory.IsReportedAsTrait("Architecture").Should().BeTrue();
            TestCaseFactory.IsReportedAsTrait("MyData:X").Should().BeTrue();
            TestCaseFactory.IsReportedAsTrait("Träit1").Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTaefTraits_IgnoreValues_AreKeptUnchanged()
        {
            // whether TE.exe ignores a test depends on the exact value (e.g. it ignores "1", but runs " true "), see TaefConstants.IsIgnored
            foreach (string value in new[] { "true", "TRUE", "1", " true ", "true ", "01", "yes", "false true", "" })
            {
                var descriptor = new TestCaseDescriptor("Class::Method", "Class", properties: new[] { new TaefProperty("Ignore", value) });

                TestCaseFactory.GetTaefTraits(descriptor).Should().ContainSingle()
                    .Which.Should().Match<Trait>(t => t.Name == "Ignore" && t.Value == value, $"value '{value}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetDataRowIndex_DataValueIndex_IsTheDataRowIndexIfItIsANumber()
        {
            TaefProperty[] Data(params string[] nameValuePairs) =>
                Enumerable.Range(0, nameValuePairs.Length / 2).Select(i => new TaefProperty(nameValuePairs[2 * i], nameValuePairs[2 * i + 1])).ToArray();
            int? GetIndex(params TaefProperty[] data) =>
                TestCaseFactory.GetDataRowIndex(new TestCaseDescriptor("A::M#row", "A", data: data))?.Index;

            GetIndex().Should().BeNull("not data-driven, or a lightweight row");
            GetIndex(Data("Color", "Red")).Should().BeNull("a lightweight row");
            GetIndex(Data("Index", "0", "V", "1")).Should().Be(0);
            GetIndex(Data("V", "x", "Index", "12")).Should().Be(12);
            GetIndex(Data("index", "3")).Should().Be(3, "names are compared ignoring case");
            foreach (string value in new[] { "07", "-1", "+1", " 1", "1 ", "abc", "", "1.0", "99999999999" })
            {
                GetIndex(Data("Index", value)).Should().BeNull($"'{value}' might be compared differently by TE.exe");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_DataDrivenTests_HaveDataRowIndexOfTheirListing()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser().ParseListPropertiesOutput(LabTaefOutputs.ListProperties);

            IList<TestCase> testCases = CreateFactory(LabTaefOutputs.LabDll).CreateTestCasesFromDescriptors(descriptors);

            int? IndexOf(string name) => testCases.Single(tc => tc.FullyQualifiedName == name)
                .Properties.OfType<TestCaseDataRowIndexProperty>().SingleOrDefault()?.Index;
            IndexOf("WildNs::Wild::Rows#a*").Should().Be(0);
            IndexOf("WildNs::Wild::Rows#a?c").Should().Be(2);
            IndexOf("WildNs::Wild::Rows#q\"x").Should().Be(4);
            IndexOf("Ml::MlTests::D#pretty").Should().Be(0);
            // lightweight rows of a data value named 'Index' (TE.exe also selects them by @Data:Index)
            IndexOf("WildNs::Wild::Light#metadataSet1").Should().Be(8);
            // rows of a data-driven class, and of its data-driven method (whose row index TE.exe uses)
            IndexOf("WildNs::ClassRows#c*::M").Should().Be(0);
            IndexOf("WildNs::ClassRows#cd::M").Should().Be(1);
            IndexOf("WildNs::ClassRows#cd::N#n*").Should().Be(0);
            IndexOf("WildNs::ClassRows#cd::N#nn").Should().Be(1);
            // not data-driven
            IndexOf("Ml::MlTests::E").Should().BeNull();
            IndexOf("TT<int *>::M").Should().BeNull();
            testCases.Count(tc => tc.Properties.OfType<TestCaseDataRowIndexProperty>().Any()).Should().Be(17);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetTaefTraits_MultiValuedProperty_IsOneTrait()
        {
            var descriptor = new TestCaseDescriptor("TaefSamples::Traits::WithEqualTraits", "TaefSamples::Traits", properties: new[] { new TaefProperty("Author", "Alice Bob") });

            TestCaseFactory.GetTaefTraits(descriptor).Select(t => t.ToString()).Should().Equal("(Author,Alice Bob)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_SeveralClasses_MetaDataPropertyContainsCountsOfClassAndDll()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            string[] names =
            {
                "A::M1", "A::M2#0", "A::M2#1", "B::M", "Ns::A::M",
                "DataNs::ClassData#metadataSet0::M1", "DataNs::ClassData#metadataSet0::M2#metadataSet0",
                "DataNs::ClassData#metadataSet1::M1",
                "Templ<std::pair<int,int> >::M1", "Templ<std::pair<int,int> >::M2"
            };
            IEnumerable<TestCaseDescriptor> descriptors = names.Select(n => new TestCaseDescriptor(n, TaefNames.GetClassName(n)));

            IList<TestCase> testCases = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(descriptors);

            testCases.Select(tc => GetMetaData(tc).NrOfTestCasesInClass).Should().Equal(3, 3, 3, 1, 1, 2, 2, 1, 2, 2);
            testCases.Should().OnlyContain(tc => GetMetaData(tc).NrOfTestCasesInTestDll == names.Length);
            testCases.Should().OnlyContain(tc => tc.Properties.Count == 1);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_TestListedTwice_IsCreatedOnce()
        {
            // TE.exe lists DLLs with module-level data once per data row, with the same test names
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);

            IList<TestCase> testCases = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(new[]
            {
                new TestCaseDescriptor("ModTests::M", "ModTests", "C:\\x\\ModuleData.dll#metadataSet0"),
                new TestCaseDescriptor("ModTests::M", "ModTests", "C:\\x\\ModuleData.dll#metadataSet1"),
                new TestCaseDescriptor("ModTests::N", "ModTests", "C:\\x\\ModuleData.dll#metadataSet1")
            });

            testCases.Select(tc => tc.FullyQualifiedName).Should().Equal("ModTests::M", "ModTests::N");
            testCases.Should().OnlyContain(tc => GetMetaData(tc).NrOfTestCasesInClass == 2 && GetMetaData(tc).NrOfTestCasesInTestDll == 2);
            MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains("ModTests::M") && s.Contains("more than once"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_WithCallback_AllTestCasesAreReportedInOrder()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            var reported = new List<TestCase>();

            IList<TestCase> testCases = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(
                new[] { "A::B", "A::C", "D::E" }.Select(n => new TestCaseDescriptor(n, TaefNames.GetClassName(n))),
                reported.Add);

            reported.Should().Equal(testCases);
            reported.Should().OnlyContain(tc => tc.Properties.OfType<TestCaseMetaDataProperty>().Any(), "test cases are reported after all of them have been created");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_EmptyList_ReturnsEmptyList()
        {
            CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(new TestCaseDescriptor[0]).Should().BeEmpty();
            _mockDiaResolverFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
        }

        #endregion

        #region Traits from option 'Before/After test discovery'

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_RegexTraits_AreMergedIn3Phases()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new List<RegexTraitPair>
            {
                new RegexTraitPair("^Ns::Class::.*", "Type", "BeforeType"),   // overridden by TAEF metadata
                new RegexTraitPair("^Ns::Class::.*", "Size", "BeforeSize"),   // overridden by 'after' trait
                new RegexTraitPair("^Ns::Class::.*", "Before", "B"),          // added
                new RegexTraitPair("^Other::", "NotMatching", "X")
            });
            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new List<RegexTraitPair>
            {
                new RegexTraitPair("^Ns::Class::Method$", "Size", "AfterSize"),
                new RegexTraitPair("^Ns::Class::Method$", "Owner", "AfterOwner"), // overrides TAEF metadata
                new RegexTraitPair("#metadataSet", "Row", "Yes")
            });
            var descriptors = new[]
            {
                new TestCaseDescriptor("Ns::Class::Method", "Ns::Class", properties: new[] { new TaefProperty("Type", "Medium"), new TaefProperty("Owner", "Me") }),
                new TestCaseDescriptor("Ns::Class::Row#metadataSet0", "Ns::Class")
            };

            IList<TestCase> testCases = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(descriptors);

            testCases[0].Traits.Select(t => t.ToString()).Should().BeEquivalentTo(
                "(Before,B)", "(Type,Medium)", "(Size,AfterSize)", "(Owner,AfterOwner)");
            testCases[1].Traits.Select(t => t.ToString()).Should().BeEquivalentTo(
                "(Type,BeforeType)", "(Size,BeforeSize)", "(Before,B)", "(Row,Yes)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_SeveralAfterTraitsWithSameName_AllAreAdded()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            MockOptions.Setup(o => o.TraitsRegexesAfter).Returns(new List<RegexTraitPair>
            {
                new RegexTraitPair("TaefSamples::Traits::WithEqualTraits", "Author", "Foo"),
                new RegexTraitPair("TaefSamples::Traits::WithEqualTraits", "Author", "Bar")
            });
            var descriptor = new TestCaseDescriptor("TaefSamples::Traits::WithEqualTraits", "TaefSamples::Traits", properties: new[] { new TaefProperty("Author", "Alice Bob") });

            TestCase testCase = CreateFactory(TestDataCreator.DummyTestDll).CreateTestCasesFromDescriptors(new[] { descriptor }).Single();

            testCase.Traits.Select(t => t.ToString()).Should().Equal("(Author,Foo)", "(Author,Bar)");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_InvalidTraitRegex_ErrorIsLoggedAndRegexIsIgnored()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            MockOptions.Setup(o => o.TraitsRegexesBefore).Returns(new List<RegexTraitPair>
            {
                new RegexTraitPair("[[Invalid", "Type", "X"),
                new RegexTraitPair(".*", "Type", "Valid")
            });

            TestCase testCase = CreateFactory(TestDataCreator.DummyTestDll)
                .CreateTestCasesFromDescriptors(new[] { new TestCaseDescriptor("A::B", "A") }).Single();

            testCase.Traits.Select(t => t.ToString()).Should().Equal("(Type,Valid)");
            MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("[[Invalid"))), Times.Once);
        }

        #endregion

        #region Source locations (mocked DIA)

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_WithPdb_LocationsAreTakenFromPdbAndRowsShareTheirMethodsLocation()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                SetupFakePdb(
                    ("Ns::Class::Method", @"C:\src\Tests.cpp", 10),
                    ("Ns::Class::Rows", @"C:\src\Tests.cpp", 20),
                    ("TaefSamples::MissingDataSource::Test", @"C:\src\Data.cpp", 30),
                    ("DataNs::ClassData::M1", @"C:\src\Data.cpp", 40),
                    ("MissingNs::ClassMissing::TAEF_GetTestClassInfo", @"C:\src\Data.cpp", 50),
                    ("TaefSamples::TemplateTests<std::vector<int,std::allocator<int> > >::CanIterate", @"C:\src\Templ.cpp", 60));
                var descriptors = new[]
                {
                    new TestCaseDescriptor("Ns::Class::Method", "Ns::Class"),
                    new TestCaseDescriptor("Ns::Class::Rows#0", "Ns::Class"),
                    new TestCaseDescriptor("Ns::Class::Rows#with::colons [x]", "Ns::Class"),
                    new TestCaseDescriptor("TaefSamples::MissingDataSource::Test#error", "TaefSamples::MissingDataSource", isDataSourceError: true, dataSourceErrorMessage: "missing"),
                    new TestCaseDescriptor("DataNs::ClassData#metadataSet0::M1", "DataNs::ClassData#metadataSet0"),
                    new TestCaseDescriptor("DataNs::ClassData#metadataSet1::M1", "DataNs::ClassData#metadataSet1"),
                    new TestCaseDescriptor("MissingNs::ClassMissing#error", "MissingNs::ClassMissing#error", isClassDataSourceError: true),
                    new TestCaseDescriptor("TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate", "TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >")
                };

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCasesFromDescriptors(descriptors);

                testCases.Select(tc => $"{tc.CodeFilePath}:{tc.LineNumber}").Should().Equal(
                    @"C:\src\Tests.cpp:10", @"C:\src\Tests.cpp:20", @"C:\src\Tests.cpp:20", @"C:\src\Data.cpp:30",
                    @"C:\src\Data.cpp:40", @"C:\src\Data.cpp:40", @"C:\src\Data.cpp:50", @"C:\src\Templ.cpp:60");
                _mockDiaResolverFactory.Verify(f => f.Create(testDll, Path.ChangeExtension(testDll, ".pdb"), It.IsAny<ILogger>()), Times.Once);
                _mockDiaResolver.Verify(r => r.Dispose(), Times.Once);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_SomeLocationsMissing_OneWarningListingTheTests()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                SetupFakePdb(("A::Found", @"C:\src\A.cpp", 1));
                var names = new[] { "A::Found", "A::Missing1", "A::Missing2", "A::Missing3", "A::Missing4", "A::Missing5", "A::Missing6" };

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCasesFromDescriptors(names.Select(n => new TestCaseDescriptor(n, "A")));

                testCases.Should().HaveCount(names.Length);
                testCases.Count(tc => tc.LineNumber != 0).Should().Be(1);
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                    s.Contains("6 of 7 tests") && s.Contains("A::Missing1") && s.Contains("A::Missing5") && !s.Contains("A::Missing6") && s.Contains("...")
                    && !s.Contains("PDB of the test DLL could not be found"))), Times.Once);
                MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("A::Missing6"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_NoPdb_WarningContainsHint()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.CreateFile("NoPdb_taef.dll");

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCasesFromDescriptors(new[] { new TestCaseDescriptor("A::B", "A") });

                testCases.Single().LineNumber.Should().Be(0);
                _mockDiaResolverFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Never);
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                    s.Contains("1 of 1 tests") && s.Contains("PDB of the test DLL could not be found") && s.Contains(SettingsWrapper.OptionAdditionalPdbs))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_PdbOnlyInAdditionalPdbs_LocationsAreFound()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.CreateFile("NoPdb_taef.dll");
                string additionalPdb = directory.CreateFile(@"pdbs\Other.pdb");
                directory.CreateFile(@"pdbs\NotAPdb.txt");
                MockOptions.Setup(o => o.AdditionalPdbs).Returns(PlaceholderReplacer.TestDllDirPlaceholder + @"\pdbs\*.pdb");
                SetupFakePdb(("A::B", @"C:\src\A.cpp", 7));

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCasesFromDescriptors(new[] { new TestCaseDescriptor("A::B", "A") });

                testCases.Single().LineNumber.Should().Be(7);
                _mockDiaResolverFactory.Verify(f => f.Create(testDll, additionalPdb, It.IsAny<ILogger>()), Times.Once);
                _mockDiaResolverFactory.Verify(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_DiaThrows_TestCasesWithoutLocations()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = CreateFakeDllWithPdb(directory);
                _mockDiaResolverFactory
                    .Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ILogger>()))
                    .Throws(new InvalidOperationException("DIA failed"));

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCasesFromDescriptors(new[] { new TestCaseDescriptor("A::B", "A") });

                testCases.Single().FullyQualifiedName.Should().Be("A::B");
                testCases.Single().LineNumber.Should().Be(0);
                MockLogger.Verify(l => l.DebugError(It.Is<string>(s => s.Contains("DIA failed"))), Times.AtLeastOnce);
            }
        }

        #endregion

        #region Test discovery with a fake TE.exe

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_CapturedListingOfSampleTests_AllTestCasesAreCreated()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                var reported = new List<TestCase>();

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases(reported.Add);

                testCases.Should().HaveCount(TestResources.NrOfTests);
                reported.Should().Equal(testCases);
                testCases.Should().OnlyContain(tc => tc.Source == fakeTe.TestDll);
                testCases.Should().OnlyContain(tc => GetMetaData(tc).NrOfTestCasesInTestDll == TestResources.NrOfTests);
                testCases.Count(TaefConstants.IsIgnored).Should().Be(TestResources.NrOfIgnoredTests);

                TestCase testCase = testCases.Single(tc => tc.FullyQualifiedName == TestResources.TestNames.TestMathAddPassesWithTraits);
                GetMetaData(testCase).NrOfTestCasesInClass.Should().Be(3);
                testCase.Traits.Select(t => t.ToString()).Should().BeEquivalentTo(
                    "(Architecture,x64)", "(Component,SampleTests)", "(Owner,ModuleOwner)", "(Type,Medium)");

                testCase = testCases.Single(tc => tc.FullyQualifiedName == TestResources.TestNames.RowWithColons);
                GetMetaData(testCase).NrOfTestCasesInClass.Should().Be(testCases.Count(tc => tc.FullyQualifiedName.StartsWith("TaefSamples::NamedRows::")));
                testCase.Traits.Select(t => t.ToString()).Should().BeEquivalentTo(
                    "(Architecture,x64)", "(Component,SampleTests)", "(Owner,RowOwner)");

                testCases.Should().Contain(tc => tc.FullyQualifiedName == TestResources.TestNames.MissingDataSource);
                testCases.Should().Contain(tc => tc.FullyQualifiedName == TestResources.TestNames.UmlautTest);
                testCases.Should().Contain(tc => tc.FullyQualifiedName == TestResources.TestNames.TemplateTest);

                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_ListingWithMultiLineValues_AllTestsWithTraitsAndOneWarningAboutUnexpectedLines()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.Create(LabTaefOutputs.ListProperties))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases();

                testCases.Should().HaveCount(LabTaefOutputs.NrOfTests);
                testCases.Should().NotContain(tc => tc.FullyQualifiedName.Contains("Phantom") || tc.FullyQualifiedName.Contains("Fake"));
                TestCase a = testCases.Single(tc => tc.FullyQualifiedName == "Ml::MlTests::A");
                a.Traits.Select(t => t.ToString()).Should().BeEquivalentTo(
                    "(Architecture,x64)", "(ModNote,module note\n            Phantom::Class\n                Phantom::Class::Test)", "(Owner,ModOwner)",
                    "(Area,MlArea)", "(ClassNote,class line 1\n                Ml::MlTests::Phantom\nSummary: class)", "(Priority,1)");
                testCases.Single(tc => tc.FullyQualifiedName == "Ml::MlTests::B").Traits.Should().Contain(t => t.Name == "Owner" && t.Value == "bOwner");
                GetMetaData(a).NrOfTestCasesInClass.Should().Be(6);

                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("3 unexpected line(s)") && s.Contains(fakeTe.TestDll)
                                                                       && s.Contains("'NotAClass::Phantom2'"))), Times.Once);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
                MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.StartsWith("Unexpected output of TE.exe"))), Times.Exactly(3));
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        /// <summary>
        /// The tests the adapter considers ignored (and does not pass to TE.exe unless ignored tests are to be run) are the
        /// ones TE.exe ignores: those listed by <c>/listProperties /runIgnoredTests</c>, but not by <c>/list</c>.
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCasesFromDescriptors_LabListing_IgnoredTestsAreTheOnesTeIgnores()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            IList<TestCaseDescriptor> descriptors = new ListPropertiesParser().ParseListPropertiesOutput(LabTaefOutputs.ListProperties);
            IList<TestCaseDescriptor> notIgnoredByTe = new ListPropertiesParser().ParseListPropertiesOutput(LabTaefOutputs.ListWithoutIgnoredTests);

            IList<TestCase> testCases = CreateFactory(LabTaefOutputs.LabDll).CreateTestCasesFromDescriptors(descriptors);

            testCases.Where(tc => !TaefConstants.IsIgnored(tc)).Select(tc => tc.FullyQualifiedName)
                .Should().Equal(notIgnoredByTe.Select(d => d.Name));
            testCases.Where(TaefConstants.IsIgnored).Select(tc => tc.FullyQualifiedName).Should().Equal(
                "IgnoreValues::V_1", "IgnoreValues::V_TRUE", "IgnoreValues::V_true", "IgnoreValues::V_tRuE", "IgnoredClass::Plain");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_TeIsCalledWithTestDllUserParametersAndDiscoverySwitches()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.Create(new[] { "Test Authoring and Execution Framework v10.104k for x64", "", "" }))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                MockOptions.Setup(o => o.AdditionalTestExecutionParam)
                    .Returns($"/p:\"Dir={PlaceholderReplacer.TestDllDirPlaceholder}\" /p:\"Test={PlaceholderReplacer.TestDirPlaceholder}\" /select:\"@Priority=1\"");
                MockOptions.Setup(o => o.WorkingDir).Returns(PlaceholderReplacer.SolutionDirPlaceholder);
                MockOptions.Setup(o => o.PathExtension).Returns(PlaceholderReplacer.TestDllDirPlaceholder + @"\bin");
                MockOptions.Setup(o => o.EnvironmentVariables).Returns($"{FakeTe.EnvironmentVariableName}=Value {PlaceholderReplacer.TestDllPlaceholder}");

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases();

                testCases.Should().BeEmpty();
                // user parameters come before the adapter's switches, $(TestDir) is not available for discovery
                fakeTe.GetArguments().Should().Be(
                    $"\"{fakeTe.TestDll}\" /p:\"Dir={fakeTe.Directory}\" /p:\"Test=\" /select:\"@Priority=1\" /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false");
                fakeTe.GetWorkingDirectory().TrimEnd('\\').Should().BeEquivalentTo(TestResources.SampleTestsSolutionDir.TrimEnd('\\'));
                fakeTe.GetPath().Should().StartWith(fakeTe.Directory + @"\bin;");
                fakeTe.GetEnvironmentVariableValue().Should().Be("Value " + fakeTe.TestDll);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.DebugInfo(It.Is<string>(s => s.Contains("did not list any tests"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_DefaultSettings_TeIsRunInTestDllDirectoryWithoutUserParameters()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.Create())
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);

                CreateFactory(fakeTe.TestDll).CreateTestCases().Should().BeEmpty();

                fakeTe.GetArguments().Should().Be(
                    $"\"{fakeTe.TestDll}\" /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false");
                fakeTe.GetWorkingDirectory().Should().BeEquivalentTo(fakeTe.Directory);
                fakeTe.GetEnvironmentVariableValue().Should().BeEmpty();
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_TeReturnsStartupError_NoTestsAndErrorWithExplanation()
        {
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Error.SelectSyntaxError.txt", TaefConstants.ExitCodeStartupError))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases();

                testCases.Should().BeEmpty();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s =>
                    s.Contains(fakeTe.TestDll)
                    && s.Contains("0x06000000")
                    && s.Contains(TaefConstants.GetExitCodeDescription(TaefConstants.ExitCodeStartupError))
                    && s.Contains(fakeTe.TeExecutable)
                    && s.Contains("A string literal was not terminated"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_TeReturnsTestCountAsExitCode_NoTestsAreReported()
        {
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt", 3))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                var reported = new List<TestCase>();

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases(reported.Add);

                testCases.Should().BeEmpty();
                reported.Should().BeEmpty();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("exit code 3") && s.Contains("Output of command"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_NotATaefTestDll_DebugWarningOnly()
        {
            // the fake test DLL is no PE file, so the static check agrees with TE.exe
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Error.NotATestDll.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases();

                testCases.Should().BeEmpty();
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
                MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("does not recognize") && s.Contains(fakeTe.TestDll))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_TeReportsErrorsAndWarnings_TheyAreLogged()
        {
            // TE.exe returns 0x05000000 in this case, but the adapter also has to cope with such messages if TE.exe returns 0
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Error.NoTestFiles.txt"))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);

                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases();

                testCases.Should().BeEmpty();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("reported an error") && s.Contains("None of the specified test files were found."))), Times.Once);
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("reported a warning") && s.Contains("does not exist."))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_DiscoveryTimeoutIsExceeded_DiscoveryIsCanceledAndCancellationIsLogged()
        {
            MockOptions.Setup(o => o.TestDiscoveryTimeoutInSeconds).Returns(1);
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.Create(TestResources.ReadTaefOutputLines("Tests_taef.dll.listProperties.txt"), sleepSeconds: 30))
            {
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);
                var reported = new List<TestCase>();

                var stopwatch = Stopwatch.StartNew();
                IList<TestCase> testCases = CreateFactory(fakeTe.TestDll).CreateTestCases(reported.Add);
                stopwatch.Stop();

                testCases.Should().BeEmpty();
                reported.Should().BeEmpty();
                stopwatch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(1) - TestMetadata.Tolerance);
                // generous upper bound: the fake TE.exe would print its output after 30 s
                stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20));
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("cancelled after 1s") && s.Contains(fakeTe.TestDll))), Times.Once);
                MockLogger.Verify(l => l.DebugError(It.Is<string>(s => s.Contains(fakeTe.TeExecutable) && s.Contains("/listProperties"))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_TeExecutableCanNotBeStarted_ErrorIsLogged()
        {
            using (var directory = new TemporaryDirectory())
            {
                // an existing file which is no executable
                string teExecutable = directory.CreateFile("TE.exe", "not an executable");
                string testDll = directory.CreateFile("Fake_taef.dll");
                MockOptions.Setup(o => o.TeExecutable).Returns(teExecutable);

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCases();

                testCases.Should().BeEmpty();
                MockLogger.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to list the tests") && s.Contains(testDll))), Times.Once);
            }
        }

        #endregion

        #region Test discovery with TE.exe and the sample DLLs

        [TestMethod]
        [TestCategory(Integration)]
        public void CreateTestCases_TestDllWithoutPdbButAdditionalPdb_SourceLocationsAreFound()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.LoadTests_ReleaseX86))
            {
                EmbeddedPdbPath.Break(sample.TestDll);
                string renamedPdb = sample.Pdb + ".bak";
                File.Move(sample.Pdb, renamedPdb);
                var diaResolverFactory = new DefaultDiaResolverFactory();

                var reported = new List<TestCase>();
                var factory = new TestCaseFactory(sample.TestDll, MockLogger.Object, TestEnvironment.Options, diaResolverFactory, _processExecutorFactory);
                IList<TestCase> testCases = factory.CreateTestCases(reported.Add);

                testCases.Should().HaveCount(TestResources.NrOfLoadTests);
                testCases.Should().OnlyContain(tc => !HasSourceLocation(tc));
                reported.Should().OnlyContain(tc => !HasSourceLocation(tc));
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains($"{TestResources.NrOfLoadTests} of {TestResources.NrOfLoadTests} tests") && s.Contains("PDB of the test DLL could not be found"))), Times.Once);

                reported.Clear();
                MockOptions.Setup(o => o.AdditionalPdbs).Returns(PlaceholderReplacer.TestDllDirPlaceholder + @"\*.pdb.bak");
                factory = new TestCaseFactory(sample.TestDll, MockLogger.Object, TestEnvironment.Options, diaResolverFactory, _processExecutorFactory);
                testCases = factory.CreateTestCases(reported.Add);

                testCases.Should().HaveCount(TestResources.NrOfLoadTests);
                testCases.Should().OnlyContain(tc => HasSourceLocation(tc));
                reported.Should().OnlyContain(tc => HasSourceLocation(tc));
                testCases.Single(tc => tc.FullyQualifiedName == TestResources.TestNames.LoadTest(0)).CodeFilePath
                    .Should().EndWithEquivalent(@"SampleTests\LoadTests\LoadTests.cpp");
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void CreateTestCases_SampleTestsX64_LocationsOfSpecialTestsAreFound()
        {
            var factory = new TestCaseFactory(TestResources.Tests_DebugX64, MockLogger.Object, TestEnvironment.Options, null, _processExecutorFactory);
            IList<TestCase> testCases = factory.CreateTestCases();

            testCases.Should().HaveCount(TestResources.NrOfTests);
            // line numbers: see samples manifest (pdbLine)
            AssertLocation(testCases, TestResources.TestNames.TemplateTest, @"Tests\ClassTemplateTests.cpp", 57);
            AssertLocation(testCases, "TaefSamples::`anonymous-namespace'::`anonymous-namespace'::Namespace_Anon_Anon::Test", @"Tests\NamespaceTests.cpp", 82);
            AssertLocation(testCases, TestResources.TestNames.RowWithColons, @"Tests\DataDrivenTests.cpp", 152);
            AssertLocation(testCases, TestResources.TestNames.MissingDataSource, @"Tests\DataDrivenTests.cpp", 171);
            AssertLocation(testCases, "TaefSamples::ÜmlautTemplateTests<class TaefSamples::ImplementationA>::Täst", @"Tests\UmlautTests.cpp", 121);
            AssertLocation(testCases, TestResources.TestNames.IgnoredFailing, @"Tests\TraitsTests.cpp", 219);
            testCases.Should().OnlyContain(tc => HasSourceLocation(tc));
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void CreateTestCases_TestDllInNonAsciiFolder_TestsAndLocationsAreFound()
        {
            // TE.exe can not open test DLLs whose path contains non-ASCII characters ('ü' is part of the ANSI code page,
            // the Chinese characters and 'Ω' are not): with the default working directory, the file name is passed
            foreach (string folder in new[] { "Jürgen", "测试Ω" })
            {
                using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_DebugX64))
                {
                    string testDll = CopyToSubFolder(sample, folder);
                    EmbeddedPdbPath.Break(testDll); // the PDB has to be found next to the test DLL
                    var factory = new TestCaseFactory(testDll, MockLogger.Object, TestEnvironment.Options, null, _processExecutorFactory);

                    IList<TestCase> testCases = factory.CreateTestCases();

                    AssertDllTestsAreFound(testCases, testDll, folder);
                }
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void CreateTestCases_NonAsciiPathNotWithinWorkingDir_TestsAreFoundByShortPath()
        {
            using (SampleCopy sample = SampleCopy.Create(TestResources.DllTests_DebugX64, "Tëst_taef.dll"))
            {
                string testDllInNonAsciiFolder = CopyToSubFolder(sample, "Jürgen");
                foreach (string testDll in new[] { sample.TestDll, testDllInNonAsciiFolder })
                {
                    string shortPath = TeArguments.GetShortPath(testDll);
                    if (shortPath == null || !TeArguments.IsAscii(shortPath))
                        Assert.Inconclusive($"8.3 short names are not available for '{testDll}' (e.g. disabled for the volume)");
                }

                // a non-ASCII file name, and a non-ASCII folder which is not TE.exe's working directory
                foreach ((string testDll, string workingDir) in new[]
                {
                    (sample.TestDll, PlaceholderReplacer.TestDllDirPlaceholder),
                    (testDllInNonAsciiFolder, PlaceholderReplacer.SolutionDirPlaceholder)
                })
                {
                    MockLogger.Invocations.Clear();
                    MockOptions.Setup(o => o.WorkingDir).Returns(workingDir);
                    var factory = new TestCaseFactory(testDll, MockLogger.Object, TestEnvironment.Options, null, _processExecutorFactory);

                    IList<TestCase> testCases = factory.CreateTestCases();

                    AssertDllTestsAreFound(testCases, testDll, workingDir);
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void CreateTestCases_TestDllInNonAsciiFolder_FileNameIsPassedToTeAndTestCasesHaveOriginalPath()
        {
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
            using (FakeTe fakeTe = FakeTe.CreateFromCapturedOutput("Tests_taef.dll.listProperties.txt"))
            {
                string directory = Path.Combine(fakeTe.Directory, "Jürgen");
                Directory.CreateDirectory(directory);
                string testDll = Path.Combine(directory, "Tests_taef.dll");
                File.Copy(fakeTe.TestDll, testDll);
                MockOptions.Setup(o => o.TeExecutable).Returns(fakeTe.TeExecutable);

                IList<TestCase> testCases = CreateFactory(testDll).CreateTestCases();

                fakeTe.GetArguments().Should().Be("\"Tests_taef.dll\" /listProperties /runIgnoredTests /unicodeOutput:false /coloredConsoleOutput:false");
                testCases.Should().HaveCount(TestResources.NrOfTests);
                testCases.Should().OnlyContain(tc => tc.Source == testDll);
                MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
            }
        }

        #endregion

        #region Helpers

        /// <returns>The copy of the test DLL (and its PDB and dependencies) in sub folder <paramref name="folder"/>.</returns>
        private static string CopyToSubFolder(SampleCopy sample, string folder)
        {
            string targetDir = sample.GetPath(folder);
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sample.Directory))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
            return Path.Combine(targetDir, Path.GetFileName(sample.TestDll));
        }

        private void AssertDllTestsAreFound(IList<TestCase> testCases, string testDll, string context)
        {
            testCases.Select(tc => tc.FullyQualifiedName).Should().BeEquivalentTo(new[] { TestResources.TestNames.DllTestsPassing, TestResources.TestNames.DllTestsFailing }, context);
            testCases.Should().OnlyContain(tc => tc.Source == testDll, context);
            testCases.Should().OnlyContain(tc => HasSourceLocation(tc) && tc.CodeFilePath.EndsWith(@"\DllTests.cpp", StringComparison.OrdinalIgnoreCase), context);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never, context);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never, context);
        }

        private TestCaseFactory CreateFactory(string testDll)
        {
            return new TestCaseFactory(testDll, MockLogger.Object, TestEnvironment.Options, _mockDiaResolverFactory.Object, _processExecutorFactory);
        }

        private static string CreateFakeDllWithPdb(TemporaryDirectory directory)
        {
            // PdbLocator finds <dll>.pdb next to the DLL; the (mocked) DIA resolver does not read it
            string testDll = directory.CreateFile("Fake_taef.dll");
            directory.CreateFile("Fake_taef.pdb");
            return testDll;
        }

        private void SetupFakePdb(params (string Function, string File, uint Line)[] functions)
        {
            foreach (var function in functions)
            {
                _pdbFunctions[function.Function] = new SourceFileLocation(function.Function, function.File, function.Line);
            }
        }

        private static TestCaseMetaDataProperty GetMetaData(TestCase testCase)
        {
            return testCase.Properties.OfType<TestCaseMetaDataProperty>().Single();
        }

        private static bool HasSourceLocation(TestCase testCase)
        {
            return !string.IsNullOrEmpty(testCase.CodeFilePath) && testCase.LineNumber != 0;
        }

        private static void AssertLocation(IEnumerable<TestCase> testCases, string name, string file, int line)
        {
            TestCase testCase = testCases.Single(tc => tc.FullyQualifiedName == name);
            testCase.CodeFilePath.Should().EndWithEquivalent(@"SampleTests\" + file);
            testCase.LineNumber.Should().Be(line, name);
        }

        #endregion

    }

}
