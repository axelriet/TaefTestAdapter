// This file has been added for TAEF support.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver.Helpers;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestCases;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.DiaResolver
{
    [TestClass]
    public class ManagedAssemblyAndSymbolsTests
    {
        private const string Source = @"using System;
using System.Threading.Tasks;
using WEX.TestExecution.Markup;
[assembly: System.Runtime.Versioning.TargetFramework("".NETFramework,Version=v4.8"")]
namespace Sample.Tests
{
    [TestClass]
    public class Basic
    {
        public void Passing() { GC.KeepAlive(1); } // @Passing
        public void Other() { GC.KeepAlive(2); } // @Other
        public void Other(int value) { GC.KeepAlive(value); }
        public async Task Async() { await Task.Delay(1); } // @Async
        public async void AsyncVoid() { await Task.Delay(1); } // @AsyncVoid
    }
    public class Base
    {
        public void Inherited() { GC.KeepAlive(3); } // @Inherited
    }
    public class Derived : Base { }
    public class GenericBase<T>
    {
        public void GenericInherited() { GC.KeepAlive(4); } // @GenericInherited
    }
    public class GenericDerived : GenericBase<int> { }
    public class Outer
    {
        public class Nested { public void Run() { GC.KeepAlive(5); } } // @Nested
    }
    public class AnotherOuter { public class Duplicate { public void Run() { } } }
    public class LastOuter { public class Duplicate { public void Run() { } } }
}
public class GlobalTests { public void Global() { GC.KeepAlive(6); } } // @Global
";

        private const string LegacyPropertyAttribute = @"
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class TestPropertyAttribute : System.Attribute
    {
        public TestPropertyAttribute(string name, string value) { }
    }
}
";

        private const string ExternalBaseSource = @"using System;
using System.Threading.Tasks;
using WEX.TestExecution.Markup;
[assembly: System.Reflection.AssemblyVersion(""1.2.3.4"")]
namespace External.Tests
{
    public class Base
    {
        [TestMethod] public void Inherited() { GC.KeepAlive(42); } // @ExternalInherited
        [TestMethod] public async Task AsyncInherited() { await Task.Delay(1); } // @ExternalAsync
    }
    public class GenericBase<T> { [TestMethod] public void GenericInherited() { GC.KeepAlive(43); } } // @ExternalGeneric
    public class Container { public class NestedBase { [TestMethod] public void NestedInherited() { GC.KeepAlive(44); } } } // @ExternalNested
}
";

        private const string ExternalDerivedSource = @"using WEX.TestExecution.Markup;
namespace External.Tests
{
    [TestClass] public class Derived : Base { }
    [TestClass] public class GenericDerived : GenericBase<int> { }
    [TestClass] public class NestedDerived : Container.NestedBase { }
}
";

        private static string _portable;
        private static string _embedded;
        private static string _windows;
        private static string _x86;
        private static string _modern;
        private static string _plain;
        private static string _compileError;

        [ClassInitialize]
        public static void CompileFixtures(TestContext context)
        {
            _portable = CSharpTestDll.Compile(Source, "portable", "anycpu", out _compileError);
            _embedded = CSharpTestDll.Compile(Source, "embedded", "x64", out _compileError);
            _windows = CSharpTestDll.Compile(Source, "full", "x64", out _compileError);
            _x86 = CSharpTestDll.Compile(Source, "portable", "x86", out _compileError);
            _modern = CSharpTestDll.Compile(Source.Replace(".NETFramework,Version=v4.8", ".NETCoreApp,Version=v10.0"), "portable", "anycpu", out _compileError);
            _plain = CSharpTestDll.Compile(Source.Replace("[TestClass]", "") + "public class StringOnly { public string Value() { return \"TE.Managed\"; } }", "portable", "anycpu", out _compileError);
        }

        [TestInitialize]
        public void RequireFixtures()
        {
            if (new[] { _portable, _embedded, _windows, _x86, _modern, _plain }.Any(string.IsNullOrEmpty))
                Assert.Inconclusive("Could not compile managed fixtures: " + _compileError);
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Read_ManagedAssemblies_ReportsReferencesFlagsAndTargetFramework()
        {
            var logger = new FakeLogger();
            ManagedAssemblyInfo anyCpu = ManagedAssemblyInfo.Read(_portable, logger);
            anyCpu.IsTaefTestAssembly.Should().BeTrue();
            anyCpu.IsILOnly.Should().BeTrue();
            anyCpu.Is32BitRequired.Should().BeFalse();
            anyCpu.MachineType.Should().Be(PeParser.MachineX86);
            anyCpu.TargetFramework.Should().Be(".NETFramework,Version=v4.8");
            ManagedAssemblyInfo.Read(_x86).Is32BitRequired.Should().BeTrue();
            ManagedAssemblyInfo.Read(_embedded).MachineType.Should().Be(PeParser.MachineX64);
            ManagedAssemblyInfo.Read(_modern).TargetFramework.Should().Be(".NETCoreApp,Version=v10.0");
            PeParser.IsTaefTestDll(_portable, logger).Should().BeTrue();
            PeParser.IsNativeTaefTestDll(_portable, logger).Should().BeFalse();
            ManagedAssemblyInfo.Read(typeof(ManagedAssemblyInfo).Assembly.Location).IsTaefTestAssembly.Should().BeFalse();
            ManagedAssemblyInfo.Read(_plain).IsTaefTestAssembly.Should().BeFalse("a string literal or unused compiler reference is not an AssemblyRef");
            logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Read_TestMarkup_RequiresKnownAttributesAsWellAsRealTaefReference()
        {
            const string verifyOnly = "public class Helpers { public void Check() { WEX.TestExecution.Verify.IsTrue(true); } }";
            const string legacy = @"namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    public sealed class TestClassAttribute : System.Attribute { }
    public sealed class TestMethodAttribute : System.Attribute { }
}
";
            foreach (string source in new[]
            {
                Source,
                legacy + "[Microsoft.VisualStudio.TestTools.UnitTesting.TestClass] " + verifyOnly
            })
            {
                string error;
                string binary = CSharpTestDll.Compile(source, "portable", "anycpu", out error);
                binary.Should().NotBeNullOrEmpty(error);
                ManagedAssemblyInfo.Read(binary).IsTaefTestAssembly.Should().BeTrue(source);
                PeParser.IsTaefTestDll(binary, null).Should().BeTrue(source);
            }
            foreach (string source in new[]
            {
                verifyOnly,
                Source.Replace("[TestClass]", "").Replace("public void Passing()", "[TestMethod] public void Passing()"),
                legacy + verifyOnly.Replace("public void Check()", "[Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod] public void Check()"),
                legacy.Replace("Microsoft.VisualStudio.TestTools.UnitTesting", "Lookalike") +
                    verifyOnly.Replace("public void Check()", "[Lookalike.TestMethod] public void Check()")
            })
            {
                string error;
                string binary = CSharpTestDll.Compile(source, "portable", "anycpu", out error);
                binary.Should().NotBeNullOrEmpty(error);
                ManagedAssemblyInfo info = ManagedAssemblyInfo.Read(binary);
                info.ReferencedAssemblies.Should().Contain("TE.Managed");
                info.IsTaefTestAssembly.Should().BeFalse(source);
                PeParser.IsTaefTestDll(binary, null).Should().BeFalse(source);
            }
            string runtime = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Windows Kits\10\Testing\Runtimes\TAEF");
            foreach (string companion in new[] { "Wex.Logger.Interop.dll", @"NetFx4.5\Wex.Common.Managed.dll" })
            {
                string binary = Path.Combine(runtime, companion);
                if (File.Exists(binary))
                    ManagedAssemblyInfo.Read(binary).IsTaefTestAssembly.Should().BeFalse(companion);
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Read_CoreClrProfile_RecognizesKnownAttributesAtTestMetadataScopes()
        {
            const string profile = "[Microsoft.VisualStudio.TestTools.UnitTesting.TestProperty(\"CoreClrProfile\", \"custom-runtime\")]";
            foreach (string source in new[]
            {
                profile.Replace("[Microsoft", "[assembly: Microsoft") + LegacyPropertyAttribute,
                LegacyPropertyAttribute + profile + "public class Tests { }",
                LegacyPropertyAttribute + "public class Tests { " + profile + "public void Test() { } }",
                LegacyPropertyAttribute + "public class Tests { " + profile + "static Tests() { } }"
            })
            {
                string error;
                string binary = CSharpTestDll.Compile(source, "portable", "anycpu", out error);
                binary.Should().NotBeNullOrEmpty(error);
                ManagedAssemblyInfo.Read(binary).HasCoreClrProfile.Should().BeTrue(source);
            }
            string wexSource = Source.Replace("public void Passing()", "[TestProperty(\"coreclrprofile\", \"wex-runtime\")] public void Passing()");
            string wexError;
            string wexBinary = CSharpTestDll.Compile(wexSource, "portable", "anycpu", out wexError);
            wexBinary.Should().NotBeNullOrEmpty(wexError);
            ManagedAssemblyInfo.Read(wexBinary).HasCoreClrProfile.Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Read_CoreClrProfile_IgnoresNamesakesOtherPropertiesAndEmptyValues()
        {
            foreach (string source in new[]
            {
                LegacyPropertyAttribute.Replace("Microsoft.VisualStudio.TestTools.UnitTesting", "Lookalike") +
                    "[Lookalike.TestProperty(\"CoreClrProfile\", \"custom-runtime\")] public class Tests { }",
                LegacyPropertyAttribute + "[Microsoft.VisualStudio.TestTools.UnitTesting.TestProperty(\"OtherProperty\", \"CoreClrProfile\")] public class Tests { }",
                LegacyPropertyAttribute + "[Microsoft.VisualStudio.TestTools.UnitTesting.TestProperty(\"CoreClrProfile\", \"\")] public class Tests { }"
            })
            {
                string error;
                string binary = CSharpTestDll.Compile(source, "portable", "anycpu", out error);
                binary.Should().NotBeNullOrEmpty(error);
                ManagedAssemblyInfo.Read(binary).HasCoreClrProfile.Should().BeFalse(source);
            }
            ManagedAssemblyInfo.Read(_portable).HasCoreClrProfile.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void Read_CoreClrProfile_MalformedAttributeDoesNotDiscardAssemblyMetadata()
        {
            string source = Source.Replace("public void Passing()", "[TestProperty(\"CoreClrProfile\", \"unique-profile-value\")] public void Passing()");
            string error;
            string binary = CSharpTestDll.Compile(source, "portable", "anycpu", out error);
            binary.Should().NotBeNullOrEmpty(error);
            byte[] image = File.ReadAllBytes(binary);
            byte[] value = new byte[] { 1, 0, 14 }.Concat(Encoding.UTF8.GetBytes("CoreClrProfile"))
                .Concat(new byte[] { 20 }).Concat(Encoding.UTF8.GetBytes("unique-profile-value")).ToArray();
            int index = TaefTestAdapter.Helpers.ByteUtils.IndexOf(image, value);
            index.Should().BeGreaterOrEqualTo(0);
            image[index + 2] = 0xe0; // invalid compressed string length inside the otherwise valid attribute blob
            File.WriteAllBytes(binary, image);
            var logger = new FakeLogger();
            ManagedAssemblyInfo info = ManagedAssemblyInfo.Read(binary, logger);
            info.Should().NotBeNull();
            info.IsTaefTestAssembly.Should().BeTrue();
            info.TargetFramework.Should().Be(".NETFramework,Version=v4.8");
            info.HasCoreClrProfile.Should().BeFalse();
            logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
            using (new FileStream(binary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_PortableEmbeddedAndWindowsPdbs_ResolveMethodsAsyncAndInheritance()
        {
            foreach (string binary in new[] { _portable, _embedded, _windows, _modern })
            {
                using (var resolver = new ManagedSymbolResolver(binary))
                {
                    resolver.HasSymbols.Should().BeTrue(binary + ": " + resolver.SymbolsProblem);
                    AssertLocation(resolver, "Sample.Tests.Basic", "Passing", "Passing");
                    AssertLocation(resolver, "Sample.Tests.Basic", "Other", "Other");
                    AssertLocation(resolver, "Sample.Tests.Basic", "Async", "Async");
                    AssertLocation(resolver, "Sample.Tests.Basic", "AsyncVoid", "AsyncVoid");
                    AssertLocation(resolver, "Sample.Tests.Derived", "Inherited", "Inherited");
                    AssertLocation(resolver, "Sample.Tests.GenericDerived", "GenericInherited", "GenericInherited");
                    AssertLocation(resolver, "Nested", "Run", "Nested");
                    AssertLocation(resolver, "GlobalTests", "Global", "Global");
                    resolver.FindLocation("Duplicate", "Run").Should().BeNull("flattened nested type names are ambiguous");
                    resolver.FindLocation("Sample.Tests.Basic", "Missing").Should().BeNull();
                }
                using (new FileStream(binary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                string pdb = Path.ChangeExtension(binary, ".pdb");
                if (File.Exists(pdb))
                    using (new FileStream(pdb, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_InheritedMethodsInAdjacentAssembly_ResolveMatchingSymbolsWithoutDerivedPdb()
        {
            foreach (string debugType in new[] { "portable", "embedded", "full" })
            {
                string baseBinary;
                string derivedBinary = CompileExternalInheritance(debugType, out baseBinary);
                string derivedCopy = CopyFixture(derivedBinary);
                BreakEmbeddedPdbPath(derivedCopy);
                string adjacentBase = CopyAdjacentBase(baseBinary, derivedCopy);
                var logger = new FakeLogger(() => OutputMode.Debug);
                using (var resolver = new ManagedSymbolResolver(derivedCopy, logger))
                {
                    AssertExternalLocation(resolver, "Derived", "Inherited", "ExternalInherited");
                    AssertExternalLocation(resolver, "Derived", "AsyncInherited", "ExternalAsync");
                    AssertExternalLocation(resolver, "GenericDerived", "GenericInherited", "ExternalGeneric");
                    AssertExternalLocation(resolver, "NestedDerived", "NestedInherited", "ExternalNested");
                }
                var container = new Mock<ITaefTestAdapterSettingsContainer>();
                container.Setup(value => value.SolutionSettings).Returns(new RunSettings());
                var settings = new SettingsWrapper(container.Object, Path.GetDirectoryName(derivedCopy))
                {
                    HelperFilesCache = new HelperFilesCache(logger),
                    RegexTraitParser = new TaefTestAdapter.Helpers.RegexTraitParser(logger)
                };
                var descriptor = new TestCaseDescriptor("External.Tests.Derived.Inherited", "External.Tests.Derived", isManaged: true);
                var caseResolver = new TestCaseResolver(derivedCopy, DefaultDiaResolverFactory.Instance, settings, logger);
                caseResolver.FindManagedTestCaseLocations(new[] { descriptor }).Should().ContainKey(descriptor.Name);
                caseResolver.ManagedSymbolsProblem.Should().BeNull("all requested locations were resolved from the base assembly");
                caseResolver.TestDllPdb.Should().BeNull("the derived assembly has no available PDB");
                var testCase = new TestCaseFactory(derivedCopy, logger, settings, DefaultDiaResolverFactory.Instance, null)
                    .CreateTestCasesFromDescriptors(new[] { descriptor }).Single();
                testCase.LineNumber.Should().BeGreaterThan(0);
                logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty("inherited navigation is complete without a derived PDB");
                ManagedAssemblyInfo.Read(adjacentBase).IsTaefTestAssembly.Should().BeFalse("a TestMethod-only base helper is not a TAEF test module");
                using (new FileStream(derivedCopy, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                using (new FileStream(adjacentBase, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                string pdb = Path.ChangeExtension(adjacentBase, ".pdb");
                if (File.Exists(pdb))
                    using (new FileStream(pdb, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_AdjacentInheritedAssemblyWithWrongNameOrVersion_IsNotUsed()
        {
            string baseBinary;
            string derivedBinary = CompileExternalInheritance("portable", out baseBinary);
            string expectedName = Path.GetFileNameWithoutExtension(baseBinary);
            foreach (bool wrongVersion in new[] { true, false })
            {
                string error;
                string wrongBase = CSharpTestDll.Compile(wrongVersion ? ExternalBaseSource.Replace("1.2.3.4", "9.8.7.6") : ExternalBaseSource,
                    "portable", "anycpu", out error, assemblyName: wrongVersion ? expectedName : "WrongBase_" + Guid.NewGuid().ToString("N"));
                wrongBase.Should().NotBeNullOrEmpty(error);
                string derivedCopy = CopyFixture(derivedBinary);
                string adjacentBase = Path.Combine(Path.GetDirectoryName(derivedCopy), Path.GetFileName(baseBinary));
                File.Copy(wrongBase, adjacentBase);
                var logger = new FakeLogger();
                using (var resolver = new ManagedSymbolResolver(derivedCopy, logger))
                    resolver.FindLocation("External.Tests.Derived", "Inherited").Should().BeNull();
                using (new FileStream(adjacentBase, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_AdjacentInheritedAssemblyWithMismatchedPdb_HasNoLocation()
        {
            string baseBinary;
            string derivedBinary = CompileExternalInheritance("portable", out baseBinary);
            string derivedCopy = CopyFixture(derivedBinary);
            string adjacentBase = Path.Combine(Path.GetDirectoryName(derivedCopy), Path.GetFileName(baseBinary));
            File.Copy(baseBinary, adjacentBase);
            BreakEmbeddedPdbPath(adjacentBase);
            string adjacentPdb = Path.ChangeExtension(adjacentBase, ".pdb");
            File.Copy(Path.ChangeExtension(_portable, ".pdb"), adjacentPdb);
            var logger = new FakeLogger();
            using (var resolver = new ManagedSymbolResolver(derivedCopy, logger))
                resolver.FindLocation("External.Tests.Derived", "Inherited").Should().BeNull();
            using (new FileStream(adjacentBase, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            using (new FileStream(adjacentPdb, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_StaleCodeViewCandidate_UsesMatchingAdjacentPdb()
        {
            string copy = CopyFixture(_portable);
            string originalPdb = Path.ChangeExtension(_portable, ".pdb");
            string marker = BreakEmbeddedPdbPath(copy);
            File.Copy(Path.ChangeExtension(_modern, ".pdb"), marker);
            File.Copy(originalPdb, Path.ChangeExtension(copy, ".pdb"));
            using (var resolver = new ManagedSymbolResolver(copy))
                AssertLocation(resolver, "Sample.Tests.Basic", "Passing", "Passing");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_OnlyMismatchedPdb_NoLocationOrWarning()
        {
            string copy = CopyFixture(_portable);
            BreakEmbeddedPdbPath(copy);
            File.Copy(Path.ChangeExtension(_modern, ".pdb"), Path.ChangeExtension(copy, ".pdb"));
            var logger = new FakeLogger();
            using (var resolver = new ManagedSymbolResolver(copy, logger))
            {
                resolver.FindLocation("Sample.Tests.Basic", "Passing").Should().BeNull();
                resolver.HasSymbols.Should().BeFalse();
            }
            logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindLocation_CorruptEmbeddedPdb_UsesMatchingExternalPortablePdb()
        {
            string copy = CopyFixture(_embedded);
            byte[] image = File.ReadAllBytes(copy);
            int index = TaefTestAdapter.Helpers.ByteUtils.IndexOf(image, Encoding.ASCII.GetBytes("MPDB"));
            index.Should().BeGreaterOrEqualTo(0);
            string externalPdb = Path.ChangeExtension(copy, ".pdb");
            using (var compressed = new MemoryStream(image, index + 8, image.Length - index - 8))
            using (var decompressed = new DeflateStream(compressed, CompressionMode.Decompress))
            using (var output = new FileStream(externalPdb, FileMode.CreateNew, FileAccess.Write))
                decompressed.CopyTo(output);
            image[index] = 0; // corrupt only the embedded PDB header, preserving the PE and CodeView identity
            File.WriteAllBytes(copy, image);
            var logger = new FakeLogger();
            using (var resolver = new ManagedSymbolResolver(copy, logger))
            {
                AssertLocation(resolver, "Sample.Tests.Basic", "Passing", "Passing");
                resolver.PdbFile.Should().Be(externalPdb);
            }
            logger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
            using (new FileStream(copy, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            using (new FileStream(externalPdb, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ReadAndResolve_TruncatedPeAndPdb_DoNotThrowOrLockFiles()
        {
            string copy = CopyFixture(_portable);
            File.WriteAllBytes(copy, File.ReadAllBytes(copy).Take(128).ToArray());
            ManagedAssemblyInfo.Read(copy).Should().BeNull();
            using (var resolver = new ManagedSymbolResolver(copy))
                resolver.FindLocation("Sample.Tests.Basic", "Passing").Should().BeNull();
            using (new FileStream(copy, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }

            string intactCopy = CopyFixture(_portable);
            BreakEmbeddedPdbPath(intactCopy);
            File.WriteAllText(Path.ChangeExtension(intactCopy, ".pdb"), "BSJB broken PDB");
            using (var resolver = new ManagedSymbolResolver(intactCopy))
                resolver.HasSymbols.Should().BeFalse();
            using (new FileStream(intactCopy, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindManagedTestCaseLocations_ClassAndMethodRows_KeepExactKeysAndEmbeddedSymbols()
        {
            var logger = new FakeLogger();
            var container = new Mock<ITaefTestAdapterSettingsContainer>();
            container.Setup(value => value.SolutionSettings).Returns(new RunSettings());
            var settings = new SettingsWrapper(container.Object, Path.GetDirectoryName(_embedded))
            {
                HelperFilesCache = new HelperFilesCache(logger),
                RegexTraitParser = new TaefTestAdapter.Helpers.RegexTraitParser(logger)
            };
            var descriptor = new TestCaseDescriptor("Sample.Tests.Basic#class.row::name.Passing#method.row::name", "Sample.Tests.Basic#class.row::name", isManaged: true);
            var resolver = new TestCaseResolver(_embedded, DefaultDiaResolverFactory.Instance, settings, logger);
            var result = resolver.FindManagedTestCaseLocations(new[] { descriptor });
            result.Keys.Should().Equal(descriptor.Name);
            result[descriptor.Name].Line.Should().Be((uint)Array.FindIndex(Source.Split('\n'), line => line.TrimEnd('\r').EndsWith("// @Passing", StringComparison.Ordinal)) + 1);
            resolver.ManagedSymbolsProblem.Should().BeNull();
            resolver.TestDllPdb.Should().BeNull("embedded symbols need no external PDB");
            var testCase = new TestCaseFactory(_embedded, logger, settings, DefaultDiaResolverFactory.Instance, null)
                .CreateTestCasesFromDescriptors(new[] { descriptor }).Single();
            testCase.CodeFilePath.Should().Be(result[descriptor.Name].Sourcefile);
            testCase.LineNumber.Should().Be((int)result[descriptor.Name].Line);
            testCase.FullyQualifiedName.Should().Be(descriptor.Name);
            testCase.IsManaged.Should().BeTrue();
        }

        private static string CopyFixture(string binary)
        {
            string id = Guid.NewGuid().ToString("N");
            string directory = Path.Combine(Path.GetTempPath(), "TaefManagedSymbolsCopy_" + id);
            Directory.CreateDirectory(directory);
            string copy = Path.Combine(directory, "Copy_" + id + ".dll");
            File.Copy(binary, copy);
            return copy;
        }

        private static string CompileExternalInheritance(string debugType, out string baseBinary)
        {
            string error;
            baseBinary = CSharpTestDll.Compile(ExternalBaseSource, debugType, "anycpu", out error);
            baseBinary.Should().NotBeNullOrEmpty(error);
            string derived = CSharpTestDll.Compile(ExternalDerivedSource, "portable", "anycpu", out error, new[] { baseBinary });
            derived.Should().NotBeNullOrEmpty(error);
            return derived;
        }

        private static string CopyAdjacentBase(string baseBinary, string derivedBinary)
        {
            string adjacent = Path.Combine(Path.GetDirectoryName(derivedBinary), Path.GetFileName(baseBinary));
            File.Copy(baseBinary, adjacent);
            string pdb = Path.ChangeExtension(baseBinary, ".pdb");
            if (File.Exists(pdb))
                File.Copy(pdb, Path.ChangeExtension(adjacent, ".pdb"));
            return adjacent;
        }

        private static void AssertExternalLocation(ManagedSymbolResolver resolver, string type, string method, string marker)
        {
            SourceFileLocation location = resolver.FindLocation("External.Tests." + type, method);
            location.Should().NotBeNull(type + "." + method);
            location.Line.Should().Be((uint)Array.FindIndex(ExternalBaseSource.Split('\n'), line => line.TrimEnd('\r').EndsWith("// @" + marker, StringComparison.Ordinal)) + 1);
            location.Sourcefile.Should().EndWith(".cs");
            location.Symbol.Should().Be("External.Tests." + type + "." + method);
        }

        private static string BreakEmbeddedPdbPath(string binary)
        {
            string path = PeParser.ExtractPdbPath(binary, null);
            string oldName = Path.GetFileNameWithoutExtension(path);
            string newPath = Path.Combine(Path.GetDirectoryName(path), oldName.Substring(0, oldName.Length - 32) + Guid.NewGuid().ToString("N") + ".pdb");
            byte[] image = File.ReadAllBytes(binary);
            byte[] oldBytes = Encoding.UTF8.GetBytes(path + "\0");
            byte[] newBytes = Encoding.UTF8.GetBytes(newPath + "\0");
            int index = TaefTestAdapter.Helpers.ByteUtils.IndexOf(image, oldBytes);
            index.Should().BeGreaterOrEqualTo(0);
            Array.Copy(newBytes, 0, image, index, newBytes.Length);
            File.WriteAllBytes(binary, image);
            return newPath;
        }

        private static void AssertLocation(ManagedSymbolResolver resolver, string type, string method, string marker)
        {
            SourceFileLocation location = resolver.FindLocation(type, method);
            location.Should().NotBeNull(type + "." + method + ": " + resolver.SymbolsProblem);
            location.Line.Should().Be((uint)Array.FindIndex(Source.Split('\n'), line => line.TrimEnd('\r').EndsWith("// @" + marker, StringComparison.Ordinal)) + 1);
            location.Sourcefile.Should().EndWith(".cs");
            location.Line.Should().NotBe(0xFEEFEE);
        }
    }
}
