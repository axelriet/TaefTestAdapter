// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver.Helpers;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.DiaResolver
{

    /// <summary>
    /// Tests of <see cref="IDiaResolver"/>, <see cref="PeParser"/> and <see cref="PdbLocator"/> with a DLL compiled at test
    /// time (see <see cref="CppTestDll"/>; the tests are inconclusive if no MSVC compiler is installed): overloaded member
    /// functions (e.g. a TAEF test method <c>void Run()</c> and a helper <c>void Run(int)</c>), and a DLL and PDB within a
    /// folder whose name contains characters which are not part of the ANSI code page.
    /// </summary>
    [TestClass]
    public class CompiledTestDllTests
    {
        /// <summary>'Ü' is part of the ANSI code page 1252, the Chinese characters and 'Ω' are not.</summary>
        private const string NonAnsiFolder = "Überladungen_测试Ω";

        // The comments at the end of the lines mark the lines of the functions (see LineOf()). Every function is defined on a
        // single line, so its location in the PDB is that line.
        private const string Source = @"// Compiled by CompiledTestDllTests (no headers, no runtime library)

class OverloadAfter
{
public:
    void Run() { Run(1); } // @OverloadAfter::Run()
    void Run(int) {} // @OverloadAfter::Run(int)
};

class OverloadBefore
{
public:
    void Run(int) {} // @OverloadBefore::Run(int)
    void Run() { Run(1); } // @OverloadBefore::Run()
};

class OverloadStatic
{
public:
    void Run() { Run(1); } // @OverloadStatic::Run()
    static void Run(int) {} // @OverloadStatic::Run(int)
};

class OverloadOutOfLine
{
public:
    void Go();
    void Go(int, int);
};

void OverloadOutOfLine::Go(int, int) {} // @OverloadOutOfLine::Go(int,int)
void OverloadOutOfLine::Go() { Go(1, 2); } // @OverloadOutOfLine::Go()

namespace Ns
{
    class Checks
    {
    public:
        void Check(const wchar_t*) {} // @Ns::Checks::Check(const wchar_t*)
        void Check() { Check(L""text""); Check(1, 2); } // @Ns::Checks::Check()
        void Check(int, int) const {} // @Ns::Checks::Check(int,int)
    };
}

class NoOverload
{
public:
    void Single() {} // @NoOverload::Single()
};

extern ""C"" __declspec(dllexport) void UseAll()
{
    OverloadAfter().Run();
    OverloadBefore().Run();
    OverloadStatic().Run();
    OverloadOutOfLine().Go();
    Ns::Checks().Check();
    NoOverload().Single();
}
";

        private static TemporaryDirectory _directory;
        private static string _dll; // in a folder with an ASCII name
        private static string _nonAnsiDll; // the same code, in a folder whose name is not part of the ANSI code page
        private static string _compileError;

        private FakeLogger _fakeLogger;

        [ClassInitialize]
        public static void CompileTestDll(TestContext testContext)
        {
            _directory = new TemporaryDirectory();
            try
            {
                _dll = CppTestDll.Compile(Source, _directory.GetPath("Ascii"), "Overloads", out _compileError);
                if (_dll != null)
                    _nonAnsiDll = CppTestDll.Compile(Source, _directory.GetPath(NonAnsiFolder), "Overloads", out _compileError);
            }
            catch (Exception e)
            {
                _compileError = e.ToString();
            }
        }

        [ClassCleanup]
        public static void DeleteTestDll()
        {
            _directory?.Dispose();
        }

        [TestInitialize]
        public void SetUp()
        {
            if (_dll == null || _nonAnsiDll == null)
                Assert.Inconclusive($"The test DLL could not be compiled: {_compileError}");

            _fakeLogger = new FakeLogger(() => OutputMode.Verbose);
        }

        #region Overloads

        [TestMethod]
        [TestCategory(Integration)]
        public void FindMemberFunctions_OverloadedMembers_ParameterlessMemberIsFound()
        {
            // a TAEF test method has no parameters, but DIA's parameter count of member functions includes 'this'
            foreach ((string type, string member, string expectedFunction) in new[]
            {
                ("OverloadAfter", "Run", "OverloadAfter::Run()"),
                ("OverloadBefore", "Run", "OverloadBefore::Run()"),
                ("OverloadStatic", "Run", "OverloadStatic::Run()"),
                ("OverloadOutOfLine", "Go", "OverloadOutOfLine::Go()"),
                ("Ns::Checks", "Check", "Ns::Checks::Check()"),
                ("NoOverload", "Single", "NoOverload::Single()")
            })
            {
                IDictionary<string, SourceFileLocation> members = Resolve(_dll, r => r.FindMemberFunctions(type, new[] { member }));

                members.Keys.Should().BeEquivalentTo(new[] { member }, expectedFunction);
                members[member].Line.Should().Be(LineOf(expectedFunction), expectedFunction);
                members[member].Sourcefile.Should().EndWith(@"\Overloads.cpp", expectedFunction);
            }
            _fakeLogger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void FindFunctions_OverloadedMembers_ParameterlessMemberIsFirst()
        {
            foreach ((string function, string expectedFirst, int nrOfOverloads) in new[]
            {
                ("OverloadAfter::Run", "OverloadAfter::Run()", 2),
                ("OverloadBefore::Run", "OverloadBefore::Run()", 2),
                ("OverloadStatic::Run", "OverloadStatic::Run()", 2),
                ("OverloadOutOfLine::Go", "OverloadOutOfLine::Go()", 2),
                ("Ns::Checks::Check", "Ns::Checks::Check()", 3)
            })
            {
                IList<SourceFileLocation> locations = Resolve(_dll, r => r.FindFunctions(function));

                locations.Should().HaveCount(nrOfOverloads, function);
                locations.Should().OnlyContain(l => l.Symbol == function);
                locations[0].Line.Should().Be(LineOf(expectedFirst), function);
            }
        }

        #endregion

        #region Folder outside of the ANSI code page

        [TestMethod]
        [TestCategory(Integration)]
        public void ExtractPdbPath_DllInFolderOutsideOfAnsiCodePage_EmbeddedPathIsDecodedCorrectly()
        {
            _nonAnsiDll.Should().Contain(NonAnsiFolder);
            string pdb = Path.ChangeExtension(_nonAnsiDll, ".pdb");

            // the linker stores the path UTF-8 encoded
            PeParser.ExtractPdbPath(_nonAnsiDll, _fakeLogger).Should().Be(pdb);
            PdbLocator.FindPdbFile(_nonAnsiDll, "", _fakeLogger).Should().Be(pdb);
            PeParser.ParseImports(_nonAnsiDll, _fakeLogger).Should().BeEmpty("the DLL does not import anything");
            PeParser.FindImport(_nonAnsiDll, "KERNEL32.dll", StringComparison.OrdinalIgnoreCase, _fakeLogger).Should().BeFalse();
            PeParser.IsTaefTestDll(_nonAnsiDll, _fakeLogger).Should().BeFalse();
            PeParser.GetMachineType(_nonAnsiDll, _fakeLogger).Should().Be(PeParser.GetMachineType(_dll, _fakeLogger)).And.NotBe(PeParser.MachineUnknown);
            Resolve(_nonAnsiDll, r => r.FindMemberFunctions("NoOverload", new[] { "Single" }))["Single"].Line.Should().Be(LineOf("NoOverload::Single()"));
            _fakeLogger.GetMessages(Severity.Warning, Severity.Error).Should().BeEmpty();
        }

        #endregion

        /// <returns>The (1-based) line of the function marked with <c>// @&lt;function&gt;</c> in <see cref="Source"/>.</returns>
        private static uint LineOf(string function)
        {
            string[] lines = Source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int index = Array.FindIndex(lines, l => l.EndsWith("// @" + function, StringComparison.Ordinal));
            index.Should().BeGreaterOrEqualTo(0, $"function {function} is marked in the source");
            return (uint)index + 1;
        }

        private T Resolve<T>(string dll, Func<IDiaResolver, T> resolve)
        {
            string pdb = PdbLocator.FindPdbFile(dll, "", _fakeLogger);
            pdb.Should().NotBeNull();
            using (IDiaResolver resolver = DefaultDiaResolverFactory.Instance.Create(dll, pdb, _fakeLogger))
            {
                return resolve(resolver);
            }
        }
    }

}
