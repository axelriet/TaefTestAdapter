// This file has been added for TAEF support.

using System.Collections.Generic;
using TaefTestAdapter.Common;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;
using TaefTestAdapter.TestCases;

namespace TaefTestAdapter.Tests.Common
{
    /// <summary>
    /// Verbatim outputs of TE.exe 10.104k (x64) for a probe test DLL (not part of the enlistment; the path of the DLL
    /// has been shortened to <see cref="ProbeDll"/>) with names and data rows the sample tests do not have:
    /// <list type="bullet">
    /// <item>Data-driven classes within namespace <c>ProbeNs</c> with class-level table data sources whose rows are named
    /// <c>one</c> and <c>two</c> (<c>ProbeNs::ClassData</c>; <c>ProbeNs::ClassDataBlocked</c> with a TEST_CLASS_SETUP
    /// returning false; <c>ProbeNs::ClassDataVerifySetup</c> with a TEST_CLASS_SETUP failing a VERIFY at Probe.cpp line
    /// 139), the same with lightweight class data (<c>ProbeNs::ClassLight</c>, <c>ProbeNs::ClassLightBlocked</c>) and in
    /// the global namespace (<c>GlobalDataBlocked</c>), and a class-level data source which does not exist
    /// (<c>ProbeNs::ClassMissing</c>).</item>
    /// <item>Method <c>ProbeNs::Wild::Rows</c> with a table data source whose rows are named <c>a*</c>, <c>ab</c>,
    /// <c>a?c</c> and <c>abc</c> (<c>*</c> and <c>?</c> are wildcards in TE.exe's <c>/select</c> queries).</item>
    /// <item>Classes <c>ProbeNs::CaseA</c> and <c>ProbeNs::casea</c> whose names only differ in case.</item>
    /// </list>
    /// </summary>
    public static class ProbeTaefOutputs
    {
        /// <summary>The (shortened) path of the probe DLL as listed in <see cref="ListProperties"/>.</summary>
        public const string ProbeDll = @"C:\taef-probe\x64\Probe_taef.dll";

        /// <summary>Output of <c>TE.exe Probe_taef.dll /listProperties /unicodeOutput:false /coloredConsoleOutput:false</c>.</summary>
        public static readonly string[] ListProperties =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "",
            "        C:\\taef-probe\\x64\\Probe_taef.dll",
            "                Property[Architecture] =  x64",
            "                Property[TaefTestType] =  Native",
            "",
            "            ProbeNs::ClassData#one",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 0",
            "                    Data[V] = 1",
            "",
            "                ProbeNs::ClassData#one::First",
            "                ProbeNs::ClassData#one::Second",
            "            ProbeNs::ClassData#two",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 1",
            "                    Data[V] = 2",
            "",
            "                ProbeNs::ClassData#two::First",
            "                ProbeNs::ClassData#two::Second",
            "            ProbeNs::ClassLight#metadataSet0",
            "                    Property[Data:Color] =  {Red, Green}",
            "                    Property[Metadata:Index] =  0",
            "",
            "                    Data[Color] = Red",
            "",
            "                ProbeNs::ClassLight#metadataSet0::M",
            "            ProbeNs::ClassLight#metadataSet1",
            "                    Property[Data:Color] =  {Red, Green}",
            "                    Property[Metadata:Index] =  1",
            "",
            "                    Data[Color] = Green",
            "",
            "                ProbeNs::ClassLight#metadataSet1::M",
            "            ProbeNs::Wild",
            "                ProbeNs::Wild::Rows#a*",
            "                        Property[DataSource] =  Table:Probe.xml#WildRows",
            "",
            "                        Data[Index] = 0",
            "                        Data[V] = 1",
            "",
            "                ProbeNs::Wild::Rows#ab",
            "                        Property[DataSource] =  Table:Probe.xml#WildRows",
            "",
            "                        Data[Index] = 1",
            "                        Data[V] = 2",
            "",
            "                ProbeNs::Wild::Rows#a?c",
            "                        Property[DataSource] =  Table:Probe.xml#WildRows",
            "",
            "                        Data[Index] = 2",
            "                        Data[V] = 3",
            "",
            "                ProbeNs::Wild::Rows#abc",
            "                        Property[DataSource] =  Table:Probe.xml#WildRows",
            "",
            "                        Data[Index] = 3",
            "                        Data[V] = 4",
            "",
            "            ProbeNs::CaseA",
            "                ProbeNs::CaseA::Test",
            "            ProbeNs::casea",
            "                ProbeNs::casea::Test",
            "            ProbeNs::ClassDataBlocked#one",
            "                    Setup: Setup",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 0",
            "                    Data[V] = 1",
            "",
            "                ProbeNs::ClassDataBlocked#one::A",
            "                ProbeNs::ClassDataBlocked#one::B",
            "            ProbeNs::ClassDataBlocked#two",
            "                    Setup: Setup",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 1",
            "                    Data[V] = 2",
            "",
            "                ProbeNs::ClassDataBlocked#two::A",
            "                ProbeNs::ClassDataBlocked#two::B",
            "            ProbeNs::ClassLightBlocked#metadataSet0",
            "                    Setup: Setup",
            "                    Property[Data:Color] =  {Red, Green}",
            "                    Property[Metadata:Index] =  0",
            "",
            "                    Data[Color] = Red",
            "",
            "                ProbeNs::ClassLightBlocked#metadataSet0::A",
            "            ProbeNs::ClassLightBlocked#metadataSet1",
            "                    Setup: Setup",
            "                    Property[Data:Color] =  {Red, Green}",
            "                    Property[Metadata:Index] =  1",
            "",
            "                    Data[Color] = Green",
            "",
            "                ProbeNs::ClassLightBlocked#metadataSet1::A",
            "            GlobalDataBlocked#one",
            "                    Setup: Setup",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 0",
            "                    Data[V] = 1",
            "",
            "                GlobalDataBlocked#one::A",
            "            GlobalDataBlocked#two",
            "                    Setup: Setup",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 1",
            "                    Data[V] = 2",
            "",
            "                GlobalDataBlocked#two::A",
            "            ProbeNs::ClassMissing#error [Blocked]",
            "                [HRESULT: 0x80070002] Failed to find the data source: Missing.xml. Confirm that the file exists. If specified as a resource, check the resource. If specified as a file path, confirm that the path is relative to the test module location.",
            "                    Property[DataSource] =  Table:Missing.xml#T",
            "",
            "            ProbeNs::ClassDataVerifySetup#one",
            "                    Setup: Setup",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 0",
            "                    Data[V] = 1",
            "",
            "                ProbeNs::ClassDataVerifySetup#one::A",
            "            ProbeNs::ClassDataVerifySetup#two",
            "                    Setup: Setup",
            "                    Property[DataSource] =  Table:Probe.xml#ClassRows",
            "",
            "                    Data[Index] = 1",
            "                    Data[V] = 2",
            "",
            "                ProbeNs::ClassDataVerifySetup#two::A",
            "",
        };

        /// <summary>
        /// Output of <c>TE.exe Probe_taef.dll /unicodeOutput:false /coloredConsoleOutput:false
        /// /select:"@Name='ProbeNs::ClassDataBlocked*' or @Name='ProbeNs::ClassLightBlocked*' or @Name='GlobalDataBlocked*'
        /// or @Name='ProbeNs::ClassDataVerifySetup*'"</c>: TE.exe prints the failures of the class setups for the scope of
        /// the class row (e.g. <c>ProbeNs::ClassDataBlocked#one</c>) before the first test of the row, and always runs the
        /// data source error pseudo test <c>ProbeNs::ClassMissing#error</c>.
        /// </summary>
        public static readonly string[] RunOfClassesWithFailingSetups =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "class setup returns false",
            "TestBlocked: TAEF: Setup fixture 'ProbeNs::ClassDataBlocked::Setup' for the scope 'ProbeNs::ClassDataBlocked#one' returned 'false'.",
            "",
            "StartGroup: ProbeNs::ClassDataBlocked#one::A",
            "EndGroup: ProbeNs::ClassDataBlocked#one::A [Blocked]",
            "",
            "StartGroup: ProbeNs::ClassDataBlocked#one::B",
            "EndGroup: ProbeNs::ClassDataBlocked#one::B [Blocked]",
            "class setup returns false",
            "TestBlocked: TAEF: Setup fixture 'ProbeNs::ClassDataBlocked::Setup' for the scope 'ProbeNs::ClassDataBlocked#two' returned 'false'.",
            "",
            "StartGroup: ProbeNs::ClassDataBlocked#two::A",
            "EndGroup: ProbeNs::ClassDataBlocked#two::A [Blocked]",
            "",
            "StartGroup: ProbeNs::ClassDataBlocked#two::B",
            "EndGroup: ProbeNs::ClassDataBlocked#two::B [Blocked]",
            "TestBlocked: TAEF: Setup fixture 'ProbeNs::ClassLightBlocked::Setup' for the scope 'ProbeNs::ClassLightBlocked#metadataSet0' returned 'false'.",
            "",
            "StartGroup: ProbeNs::ClassLightBlocked#metadataSet0::A",
            "EndGroup: ProbeNs::ClassLightBlocked#metadataSet0::A [Blocked]",
            "TestBlocked: TAEF: Setup fixture 'ProbeNs::ClassLightBlocked::Setup' for the scope 'ProbeNs::ClassLightBlocked#metadataSet1' returned 'false'.",
            "",
            "StartGroup: ProbeNs::ClassLightBlocked#metadataSet1::A",
            "EndGroup: ProbeNs::ClassLightBlocked#metadataSet1::A [Blocked]",
            "TestBlocked: TAEF: Setup fixture 'GlobalDataBlocked::Setup' for the scope 'GlobalDataBlocked#one' returned 'false'.",
            "",
            "StartGroup: GlobalDataBlocked#one::A",
            "EndGroup: GlobalDataBlocked#one::A [Blocked]",
            "TestBlocked: TAEF: Setup fixture 'GlobalDataBlocked::Setup' for the scope 'GlobalDataBlocked#two' returned 'false'.",
            "",
            "StartGroup: GlobalDataBlocked#two::A",
            "EndGroup: GlobalDataBlocked#two::A [Blocked]",
            "",
            "StartGroup: ProbeNs::ClassMissing#error",
            "TestBlocked: TAEF: [HRESULT: 0x80070002] Failed to find the data source: Missing.xml. Confirm that the file exists. If specified as a resource, check the resource. If specified as a file path, confirm that the path is relative to the test module location.",
            "EndGroup: ProbeNs::ClassMissing#error [Blocked]",
            "Error: Verify: AreEqual(5, 6): VERIFY failing in class setup - Values (5, 6) [File: Probe.cpp, Function: ProbeNs::ClassDataVerifySetup::Setup, Line: 139]",
            "Error: TAEF: Setup fixture 'ProbeNs::ClassDataVerifySetup::Setup' for the scope 'ProbeNs::ClassDataVerifySetup#one' failed.",
            "",
            "StartGroup: ProbeNs::ClassDataVerifySetup#one::A",
            "EndGroup: ProbeNs::ClassDataVerifySetup#one::A [Failed]",
            "Error: Verify: AreEqual(5, 6): VERIFY failing in class setup - Values (5, 6) [File: Probe.cpp, Function: ProbeNs::ClassDataVerifySetup::Setup, Line: 139]",
            "Error: TAEF: Setup fixture 'ProbeNs::ClassDataVerifySetup::Setup' for the scope 'ProbeNs::ClassDataVerifySetup#two' failed.",
            "",
            "StartGroup: ProbeNs::ClassDataVerifySetup#two::A",
            "EndGroup: ProbeNs::ClassDataVerifySetup#two::A [Failed]",
            "",
            "Summary of Errors Outside of Tests:",
            "    Error: Verify: AreEqual(5, 6): VERIFY failing in class setup - Values (5, 6) [File: Probe.cpp, Function: ProbeNs::ClassDataVerifySetup::Setup, Line: 139]",
            "    Error: TAEF: Setup fixture 'ProbeNs::ClassDataVerifySetup::Setup' for the scope 'ProbeNs::ClassDataVerifySetup#one' failed.",
            "    Error: Verify: AreEqual(5, 6): VERIFY failing in class setup - Values (5, 6) [File: Probe.cpp, Function: ProbeNs::ClassDataVerifySetup::Setup, Line: 139]",
            "    Error: TAEF: Setup fixture 'ProbeNs::ClassDataVerifySetup::Setup' for the scope 'ProbeNs::ClassDataVerifySetup#two' failed.",
            "",
            "Summary of Non-passing Tests:",
            "    ProbeNs::ClassDataVerifySetup#one::A [Failed]",
            "    ProbeNs::ClassDataVerifySetup#two::A [Failed]",
            "    ProbeNs::ClassDataBlocked#one::A [Blocked]",
            "    ProbeNs::ClassDataBlocked#one::B [Blocked]",
            "    ProbeNs::ClassDataBlocked#two::A [Blocked]",
            "    ProbeNs::ClassDataBlocked#two::B [Blocked]",
            "    ProbeNs::ClassLightBlocked#metadataSet0::A [Blocked]",
            "    ProbeNs::ClassLightBlocked#metadataSet1::A [Blocked]",
            "    GlobalDataBlocked#one::A [Blocked]",
            "    GlobalDataBlocked#two::A [Blocked]",
            "    ProbeNs::ClassMissing#error [Blocked]",
            "",
            "Summary: Total=11, Passed=0, Failed=2, Blocked=9, Not Run=0, Skipped=0",
        };

        /// <returns>The tests of <see cref="ListProperties"/> as parsed by the adapter.</returns>
        public static IList<TestCaseDescriptor> GetDescriptors()
        {
            return new ListPropertiesParser().ParseListPropertiesOutput(ListProperties);
        }

        /// <returns>
        /// The test cases of <see cref="ListProperties"/> as the adapter creates them (traits, meta data etc.; no source
        /// locations, i.e. <paramref name="settings"/> must return false for ParseSymbolInformation).
        /// </returns>
        public static IList<TestCase> GetTestCases(SettingsWrapper settings, ILogger logger)
        {
            return new TestCaseFactory(ProbeDll, logger, settings, null, null).CreateTestCasesFromDescriptors(GetDescriptors());
        }
    }
}
