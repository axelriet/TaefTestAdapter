// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.ProcessExecution;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Tests of <see cref="WttLogParser"/>: WTT logs (as written by TE.exe with <c>/enableWttLogging /logFile:...</c>) are
    /// converted into console output lines which <see cref="StreamingTaefOutputParser"/> understands.
    /// </summary>
    [TestClass]
    public class WttLogParserTests : TestsBase
    {
        private const string LabCpp = @"C:\src\TaefLab\TaefLab.cpp";
        private const string LabDll = @"C:\src\TaefLab\bin\x64\TaefLab.dll";

        private const string Passing = "TaefLab::Basic::BasicTests::Passing";
        private const string FailAreEqual = "TaefLab::Basic::BasicTests::FailAreEqual";
        private const string Output = "TaefLab::Basic::BasicTests::Output";
        private const string SkipSelf = "TaefLab::Basic::BasicTests::SkipSelf";
        private const string BlockSelf = "TaefLab::Basic::BasicTests::BlockSelf";
        private const string LogResultNotRun = "TaefLab::Basic::BasicTests::LogResultNotRun";
        private const string ThrowStdException = "TaefLab::Crashy::CrashTests::ThrowStdException";
        private const string WillBeBlocked1 = "BlockedClassTests::WillBeBlocked1";
        private const string BlockedByVerifyInSetup = "ClassSetupVerifyFailTests::BlockedByVerifyInSetup";

        private static readonly string[] AllTests =
        {
            Passing, FailAreEqual, Output, SkipSelf, BlockSelf, LogResultNotRun, ThrowStdException, WillBeBlocked1, BlockedByVerifyInSetup
        };

        private const string TruncationMarker = "<!-- TRUNCATE HERE -->";

        // Excerpts of a WTT log written by TE.exe 10.104k for TaefLab.dll, a TAEF test DLL written for these tests (verbatim
        // entries, some MetadataRef entries omitted, machine name and source paths replaced)
        private const string WttLog = @"<?xml version=""1.0"" encoding=""UTF-16"" ?>
<WTT-Logger>
<RTI ID=""2524176819"" Machine=""TESTMACHINE"" ProcessName=""C:\Program Files (x86)\Windows Kits\10\Testing\Runtimes\TAEF\x64\TE.exe"" ProcessID=""33660"" ThreadID=""48312"" BaseTime=""2026:9:26 12:2:30:841"" Frequency=""10000000"" />
<CTX ID=""384048256"" Current=""WTTLOG"" Parent=""ROOT"" />
<Msg
	UserText=""WTTLogger_CPP_GitEnlistment(IMProdBldB); Version: 2.7.3483.0"" CA=""4843"" LA=""19557"" >
	<rti id=""2524176819"" />
	<ctx id=""384048256"" />
</Msg>
<Msg
	UserText=""[LabModuleSetup] Log::Comment from MODULE_SETUP"" CA=""1465445"" LA=""1465738"" >
<Data>
<WexTraceInfo ThreadId=""21896"" ProcessId=""16760"" TimeStamp=""4050719819641"" LogSessionId=""1"" SessionTraceCount=""3""/>
</Data>	<rti id=""3467988916"" />
	<ctx id=""384048256"" />
</Msg>
<Msg
	UserText=""TaefLab::Basic::BasicTests::Passing"" CA=""1826138"" LA=""1827182"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628775263"" LogSessionId=""1"" SessionTraceCount=""19""/><WexContext><![CDATA[TestScope]]></WexContext><StartGroup />
</Data>	<rti id=""555633079"" />
	<ctx id=""384048256"" />
</Msg>
<CTX ID=""3245490589"" Current=""TaefLab::Basic::BasicTests::Passing"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Basic::BasicTests::Passing""
	TUID="""" CA=""1315631"" LA=""1315847"" >
<Data>
<WexTraceInfo ThreadId=""22356"" ProcessId=""27720"" TimeStamp=""4034643497688"" LogSessionId=""1"" SessionTraceCount=""21""/><TestGroup TestId=""4690D4C4-8154-4E4C-ADD8-403CED381E72"" />
</Data>	<rti id=""1526901515"" />
	<ctx id=""3245490589"" />
</StartTest>
<MetadataRef
	Id=""WexLogger""
	Inherit=""False""
	AttributeName=""Architecture""
	AttributeValue=""x64"" CA=""1316664"" LA=""1316899"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628784425"" LogSessionId=""1"" SessionTraceCount=""22""/><WexContext><![CDATA[TAEF]]></WexContext>
</Data>	<rti id=""555633079"" />
	<ctx id=""3245490589"" />
</MetadataRef>
<Msg
	UserText=""AreEqual(1, 1)"" CA=""30391"" LA=""30618"" >
<Data>
<WexTraceInfo ThreadId=""38500"" ProcessId=""19144"" TimeStamp=""4034643505219"" LogSessionId=""1"" SessionTraceCount=""28""/><WexContext><![CDATA[Verify]]></WexContext>
</Data>	<rti id=""2470291231"" />
	<ctx id=""3245490589"" />
</Msg>
<EndTest
	Title=""TaefLab::Basic::BasicTests::Passing""
	TUID=""""
	Result=""Pass""
	Repro="""" CA=""1325084"" LA=""1325381"" >
<Data>
<WexTraceInfo ThreadId=""22356"" ProcessId=""27720"" TimeStamp=""4034643507344"" LogSessionId=""1"" SessionTraceCount=""29""/>
</Data>	<rti id=""1526901515"" />
	<ctx id=""3245490589"" />
</EndTest>
<CTX ID=""2671590606"" Current=""TaefLab::Basic::BasicTests::FailAreEqual"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Basic::BasicTests::FailAreEqual""
	TUID="""" CA=""1464553"" LA=""1464883"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628419491"" LogSessionId=""1"" SessionTraceCount=""40""/><TestGroup TestId=""0B5D8E53-5D07-4A9D-8C68-8B1D4F1E4C11"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""2671590606"" />
</StartTest>
<Error
	File=""C:\src\TaefLab\TaefLab.cpp""
	Line=""80""
	ErrCode=""0x0""
	ErrType=""""
	ErrorText=""Error 0x00000000""
	UserText=""AreEqual(1, 2) - Values (1, 2)"" CA=""59065"" LA=""59466"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628423791"" LogSessionId=""1"" SessionTraceCount=""45""/><WexContext><![CDATA[Verify]]></WexContext>
</Data>	<rti id=""3412985685"" />
	<ctx id=""2671590606"" />
</Error>
<EndTest
	Title=""TaefLab::Basic::BasicTests::FailAreEqual""
	TUID=""""
	Result=""Fail""
	Repro="""" CA=""1483005"" LA=""1483259"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628426002"" LogSessionId=""1"" SessionTraceCount=""46""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""2671590606"" />
</EndTest>
<CTX ID=""1832099245"" Current=""TaefLab::Basic::BasicTests::Output"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Basic::BasicTests::Output""
	TUID="""" CA=""1700001"" LA=""1700202"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628710000"" LogSessionId=""1"" SessionTraceCount=""250""/><TestGroup TestId=""7C4B5E7B-3E0D-4B0A-9C1B-0F5E0F9F5A12"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""1832099245"" />
</StartTest>
<Warn
	File=""""
	Line=""-1""
	UserText=""Log::Warning line 8"" CA=""350960"" LA=""351267"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628715968"" LogSessionId=""1"" SessionTraceCount=""253""/>
</Data>	<rti id=""3412985685"" />
	<ctx id=""1832099245"" />
</Warn>
<Msg
	UserText=""multi-line comment line A
multi-line comment line B"" CA=""353001"" LA=""353251"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628718063"" LogSessionId=""1"" SessionTraceCount=""255""/>
</Data>	<rti id=""3412985685"" />
	<ctx id=""1832099245"" />
</Msg>
<EndTest
	Title=""TaefLab::Basic::BasicTests::Output""
	TUID=""""
	Result=""Pass""
	Repro="""" CA=""1710084"" LA=""1710381"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628720344"" LogSessionId=""1"" SessionTraceCount=""256""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""1832099245"" />
</EndTest>
<CTX ID=""3532577527"" Current=""TaefLab::Basic::BasicTests::SkipSelf"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Basic::BasicTests::SkipSelf""
	TUID="""" CA=""1834848"" LA=""1835284"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628782618"" LogSessionId=""1"" SessionTraceCount=""300""/><TestGroup TestId=""F0D0FB29-770E-46D2-B94E-53B1ECC3E779"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""3532577527"" />
</StartTest>
<Msg
	UserText=""About to mark self skipped"" CA=""426867"" LA=""427193"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628791050"" LogSessionId=""1"" SessionTraceCount=""307""/>
</Data>	<rti id=""3412985685"" />
	<ctx id=""3532577527"" />
</Msg>
<Msg
	UserText=""skipping because reasons"" CA=""428643"" LA=""428968"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628793583"" LogSessionId=""1"" SessionTraceCount=""308""/><Result>Skipped</Result>
</Data>	<rti id=""3412985685"" />
	<ctx id=""3532577527"" />
</Msg>
<EndTest
	Title=""TaefLab::Basic::BasicTests::SkipSelf""
	TUID=""""
	Result=""Skipped""
	Repro="""" CA=""1847195"" LA=""1847711"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628795647"" LogSessionId=""1"" SessionTraceCount=""309""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""3532577527"" />
</EndTest>
<Msg
	UserText=""TaefLab::Basic::BasicTests::MethodCleanup"" CA=""1848336"" LA=""1848602"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628797172"" LogSessionId=""1"" SessionTraceCount=""310""/><WexContext><![CDATA[Cleanup]]></WexContext><StartGroup />
</Data>	<rti id=""555633079"" />
	<ctx id=""384048256"" />
</Msg>
<CTX ID=""2505465172"" Current=""TaefLab::Basic::BasicTests::BlockSelf"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Basic::BasicTests::BlockSelf""
	TUID="""" CA=""1876000"" LA=""1876311"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628826002"" LogSessionId=""1"" SessionTraceCount=""333""/><TestGroup TestId=""2A1C6E60-8C3B-4E55-9C2A-8D7D2C9E4B13"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""2505465172"" />
</StartTest>
<Warn
	File=""""
	Line=""-1""
	UserText=""blocking because reasons"" CA=""469239"" LA=""469721"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628834119"" LogSessionId=""1"" SessionTraceCount=""342""/><Result>Blocked</Result>
</Data>	<rti id=""3412985685"" />
	<ctx id=""2505465172"" />
</Warn>
<EndTest
	Title=""TaefLab::Basic::BasicTests::BlockSelf""
	TUID=""""
	Result=""Blocked""
	Repro="""" CA=""1887297"" LA=""1887740"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628836002"" LogSessionId=""1"" SessionTraceCount=""343""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""2505465172"" />
</EndTest>
<CTX ID=""198878283"" Current=""TaefLab::Basic::BasicTests::LogResultNotRun"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Basic::BasicTests::LogResultNotRun""
	TUID="""" CA=""1913000"" LA=""1913304"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628860002"" LogSessionId=""1"" SessionTraceCount=""366""/><TestGroup TestId=""5E8B7D10-4B6C-4C3F-8C5B-3A9F0E7D6C14"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""198878283"" />
</StartTest>
<Warn
	File=""""
	Line=""-1""
	UserText=""explicit Result NotRun"" CA=""505043"" LA=""505487"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050628870217"" LogSessionId=""1"" SessionTraceCount=""376""/><Result>NotRun</Result>
</Data>	<rti id=""3412985685"" />
	<ctx id=""198878283"" />
</Warn>
<EndTest
	Title=""TaefLab::Basic::BasicTests::LogResultNotRun""
	TUID=""""
	Result=""Blocked""
	Repro="""" CA=""1923155"" LA=""1923395"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050628871581"" LogSessionId=""1"" SessionTraceCount=""377""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""198878283"" />
</EndTest>
<CTX ID=""2731680903"" Current=""TaefLab::Crashy::CrashTests::ThrowStdException"" Parent=""WTTLOG"" />
<StartTest
	Title=""TaefLab::Crashy::CrashTests::ThrowStdException""
	TUID="""" CA=""31110000"" LA=""31110304"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050659480002"" LogSessionId=""1"" SessionTraceCount=""890""/><TestGroup TestId=""9C0D1E2F-3A4B-4C5D-8E6F-7A8B9C0D1E15"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""2731680903"" />
</StartTest>
" + TruncationMarker + @"
<Error
	File=""""
	Line=""-1""
	ErrCode=""0x0""
	ErrType=""""
	ErrorText=""Error 0x00000000""
	UserText=""Caught std::exception: std::runtime_error thrown from test"" CA=""31118115"" LA=""31118347"" >
<Data>
<WexTraceInfo ThreadId=""11704"" ProcessId=""33632"" TimeStamp=""4050659483116"" LogSessionId=""1"" SessionTraceCount=""896""/>
</Data>	<rti id=""3412985685"" />
	<ctx id=""2731680903"" />
</Error>
<EndTest
	Title=""TaefLab::Crashy::CrashTests::ThrowStdException""
	TUID=""""
	Result=""Fail""
	Repro="""" CA=""31120000"" LA=""31120304"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050659485002"" LogSessionId=""1"" SessionTraceCount=""897""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""2731680903"" />
</EndTest>
<Msg
	UserText=""BlockedClassTests::FailingClassSetup"" CA=""92868599"" LA=""92869450"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719817686"" LogSessionId=""1"" SessionTraceCount=""1030""/><WexContext><![CDATA[Setup]]></WexContext><StartGroup />
</Data>	<rti id=""555633079"" />
	<ctx id=""384048256"" />
</Msg>
<Msg
	UserText=""[BlockedClassTests::FailingClassSetup] returning false"" CA=""1465445"" LA=""1465738"" >
<Data>
<WexTraceInfo ThreadId=""21896"" ProcessId=""16760"" TimeStamp=""4050719819641"" LogSessionId=""1"" SessionTraceCount=""1031""/>
</Data>	<rti id=""3467988916"" />
	<ctx id=""384048256"" />
</Msg>
<Warn
	File=""""
	Line=""-1""
	UserText=""Setup fixture &apos;BlockedClassTests::FailingClassSetup&apos; for the scope &apos;BlockedClassTests&apos; returned &apos;false&apos;."" CA=""1467282"" LA=""1467844"" >
<Data>
<WexTraceInfo ThreadId=""21896"" ProcessId=""16760"" TimeStamp=""4050719821320"" LogSessionId=""1"" SessionTraceCount=""1032""/><WexContext><![CDATA[TAEF]]></WexContext><Result>Blocked</Result>
</Data>	<rti id=""3467988916"" />
	<ctx id=""384048256"" />
</Warn>
<Msg
	UserText=""BlockedClassTests::FailingClassSetup"" CA=""92875819"" LA=""92876138"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719824778"" LogSessionId=""1"" SessionTraceCount=""1033""/><WexContext><![CDATA[Setup]]></WexContext><EndGroup Result=""Blocked"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""384048256"" />
</Msg>
<CTX ID=""3828301989"" Current=""BlockedClassTests::WillBeBlocked1"" Parent=""WTTLOG"" />
<StartTest
	Title=""BlockedClassTests::WillBeBlocked1""
	TUID="""" CA=""92877626"" LA=""92877930"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719825274"" LogSessionId=""1"" SessionTraceCount=""1034""/><TestGroup TestId=""9D0388D4-D0D6-46BB-896F-2AD4194A8C25"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""3828301989"" />
</StartTest>
<Warn
	File=""""
	Line=""-1""
	UserText="""" CA=""92878527"" LA=""92878799"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719827158"" LogSessionId=""1"" SessionTraceCount=""1035""/><WexContext><![CDATA[TAEF]]></WexContext><Result Reason=""Setup"">Blocked</Result>
</Data>	<rti id=""555633079"" />
	<ctx id=""3828301989"" />
</Warn>
<EndTest
	Title=""BlockedClassTests::WillBeBlocked1""
	TUID=""""
	Result=""Blocked""
	Repro="""" CA=""92880140"" LA=""92880428"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719827938"" LogSessionId=""1"" SessionTraceCount=""1036""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""3828301989"" />
</EndTest>
<Error
	File=""C:\src\TaefLab\TaefLab.cpp""
	Line=""436""
	ErrCode=""0x0""
	ErrType=""""
	ErrorText=""Error 0x00000000""
	UserText=""AreEqual(5, 6): VERIFY failing inside TEST_CLASS_SETUP - Values (5, 6)"" CA=""1518848"" LA=""1519093"" >
<Data>
<WexTraceInfo ThreadId=""21896"" ProcessId=""16760"" TimeStamp=""4050719873804"" LogSessionId=""1"" SessionTraceCount=""1075""/><WexContext><![CDATA[Verify]]></WexContext>
</Data>	<rti id=""3467988916"" />
	<ctx id=""384048256"" />
</Error>
<Error
	File=""""
	Line=""-1""
	ErrCode=""0x0""
	ErrType=""""
	ErrorText=""Error 0x00000000""
	UserText=""Setup fixture &apos;ClassSetupVerifyFailTests::VerifyFailingClassSetup&apos; for the scope &apos;ClassSetupVerifyFailTests&apos; failed."" CA=""92926262"" LA=""92926492"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719875392"" LogSessionId=""1"" SessionTraceCount=""1076""/><WexContext><![CDATA[TAEF]]></WexContext>
</Data>	<rti id=""555633079"" />
	<ctx id=""384048256"" />
</Error>
<CTX ID=""1803282242"" Current=""ClassSetupVerifyFailTests::BlockedByVerifyInSetup"" Parent=""WTTLOG"" />
<StartTest
	Title=""ClassSetupVerifyFailTests::BlockedByVerifyInSetup""
	TUID="""" CA=""92927626"" LA=""92927930"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719877274"" LogSessionId=""1"" SessionTraceCount=""1077""/><TestGroup TestId=""3B2A1C0D-9E8F-4A7B-8C6D-5E4F3A2B1C16"" />
</Data>	<rti id=""555633079"" />
	<ctx id=""1803282242"" />
</StartTest>
<EndTest
	Title=""ClassSetupVerifyFailTests::BlockedByVerifyInSetup""
	TUID=""""
	Result=""Fail""
	Repro="""" CA=""92930140"" LA=""92930428"" >
<Data>
<WexTraceInfo ThreadId=""28120"" ProcessId=""33660"" TimeStamp=""4050719879938"" LogSessionId=""1"" SessionTraceCount=""1078""/>
</Data>	<rti id=""555633079"" />
	<ctx id=""1803282242"" />
</EndTest>
<PFRollup
	Total=""9""
	Passed=""2""
	Failed=""3""
	Blocked=""3""
	Warned=""0""
	Skipped=""1"" CA=""145441834"" LA=""145442361"" >
	<rti id=""2524176819"" />
	<ctx id=""384048256"" />
</PFRollup>
</WTT-Logger>
";

        private static readonly string[] ExpectedConsoleOutput =
        {
            "",
            "StartGroup: " + Passing,
            "Verify: AreEqual(1, 1)",
            "EndGroup: " + Passing + " [Passed]",
            "",
            "StartGroup: " + FailAreEqual,
            "Error: Verify: AreEqual(1, 2) - Values (1, 2) [File: " + LabCpp + ", Function: , Line: 80]",
            "EndGroup: " + FailAreEqual + " [Failed]",
            "",
            "StartGroup: " + Output,
            "Warning: Log::Warning line 8",
            "multi-line comment line A",
            "multi-line comment line B",
            "EndGroup: " + Output + " [Passed]",
            "",
            "StartGroup: " + SkipSelf,
            "About to mark self skipped",
            "TestSkipped: skipping because reasons",
            "EndGroup: " + SkipSelf + " [Skipped]",
            "",
            "StartGroup: " + BlockSelf,
            "TestBlocked: blocking because reasons",
            "EndGroup: " + BlockSelf + " [Blocked]",
            "",
            "StartGroup: " + LogResultNotRun,
            "TestNotRun: explicit Result NotRun",
            "EndGroup: " + LogResultNotRun + " [NotRun]",
            "",
            "StartGroup: " + ThrowStdException,
            "Error: Caught std::exception: std::runtime_error thrown from test",
            "EndGroup: " + ThrowStdException + " [Failed]",
            "TestBlocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup' for the scope 'BlockedClassTests' returned 'false'.",
            "",
            "StartGroup: " + WillBeBlocked1,
            "EndGroup: " + WillBeBlocked1 + " [Blocked]",
            "Error: Verify: AreEqual(5, 6): VERIFY failing inside TEST_CLASS_SETUP - Values (5, 6) [File: " + LabCpp + ", Function: , Line: 436]",
            "Error: TAEF: Setup fixture 'ClassSetupVerifyFailTests::VerifyFailingClassSetup' for the scope 'ClassSetupVerifyFailTests' failed.",
            "",
            "StartGroup: " + BlockedByVerifyInSetup,
            "EndGroup: " + BlockedByVerifyInSetup + " [Failed]"
        };

        private TemporaryDirectory _directory;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            _directory = new TemporaryDirectory();
        }

        [TestCleanup]
        public override void TearDown()
        {
            _directory.Dispose();
            base.TearDown();
        }

        /// <summary>Writes a WTT log as TE.exe does (UTF-16 with BOM).</summary>
        private string WriteWttLog(string content, string fileName = "test.wtl")
        {
            string file = _directory.GetPath(fileName);
            File.WriteAllText(file, content, Encoding.Unicode);
            return file;
        }

        private IList<TestResult> ParseConsoleOutput(IEnumerable<string> lines, out StreamingTaefOutputParser parser)
        {
            parser = new StreamingTaefOutputParser(CapturedTaefOutputs.ToTestCases(LabDll, AllTests), MockLogger.Object, MockFrameworkReporter.Object);
            foreach (string line in lines)
                parser.ReportLine(line);
            parser.Flush();
            return parser.TestResults;
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_WttLog_IsConvertedIntoConsoleOutput()
        {
            string wttLog = WriteWttLog(WttLog.Replace(TruncationMarker, ""));

            IList<string> lines = new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput();

            lines.Should().Equal(ExpectedConsoleOutput);
            MockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_WttLog_ResultsAreEqualToThoseOfConsoleOutput()
        {
            string wttLog = WriteWttLog(WttLog.Replace(TruncationMarker, ""));

            IList<TestResult> results = ParseConsoleOutput(new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput(), out _);

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(AllTests);
            TestResultAssertions.AssertTestResultIsPassed(results[0]);

            TestResultAssertions.AssertTestResultIsFailure(results[1], "Verify: AreEqual(1, 2) - Values (1, 2)");
            results[1].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("TaefLab.cpp:80", LabCpp, "80"));

            TestResultAssertions.AssertTestResultIsPassed(results[2]);
            results[2].Output.Should().Be(string.Join(Environment.NewLine, "Warning: Log::Warning line 8", "multi-line comment line A", "multi-line comment line B"));

            TestResultAssertions.AssertTestResultIsSkipped(results[3]);
            results[3].ErrorMessage.Should().Be("skipping because reasons");

            TestResultAssertions.AssertTestResultIsFailure(results[4], "Blocked: blocking because reasons");

            results[5].Outcome.Should().Be(TestOutcome.None, "WTT logs report NotRun as Blocked, together with a NotRun result message");
            results[5].ErrorMessage.Should().Be("explicit Result NotRun");

            TestResultAssertions.AssertTestResultIsFailure(results[6], "Caught std::exception: std::runtime_error thrown from test");
            results[6].ErrorStackTrace.Should().BeNull();

            TestResultAssertions.AssertTestResultIsFailure(results[7],
                "Blocked: TAEF: Setup fixture 'BlockedClassTests::FailingClassSetup' for the scope 'BlockedClassTests' returned 'false'.");

            TestResultAssertions.AssertTestResultIsFailure(results[8],
                "#1 - Verify: AreEqual(5, 6): VERIFY failing inside TEST_CLASS_SETUP - Values (5, 6)\n" +
                "#2 - TAEF: Setup fixture 'ClassSetupVerifyFailTests::VerifyFailingClassSetup' for the scope 'ClassSetupVerifyFailTests' failed.");
            results[8].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - TaefLab.cpp:436", LabCpp, "436"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_TruncatedWttLog_EverythingUpToTruncationIsConvertedAndRunningTestCrashed()
        {
            string content = WttLog.Substring(0, WttLog.IndexOf(TruncationMarker, StringComparison.Ordinal))
                             + "<Error \n\tFile=\"\" \n\tLine=\"-1\" \n\tErrCode=\"0x0\" ";
            string wttLog = WriteWttLog(content);

            IList<string> lines = new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput();

            lines.Should().Equal(ExpectedConsoleOutput.Take(ExpectedConsoleOutput.ToList().IndexOf("StartGroup: " + ThrowStdException) + 1));
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("could not be parsed completely"))), Times.Once);

            IList<TestResult> results = ParseConsoleOutput(lines, out StreamingTaefOutputParser parser);
            results.Should().HaveCount(7);
            parser.CrashedTestCase.FullyQualifiedName.Should().Be(ThrowStdException);
            TestResultAssertions.AssertTestResultIsFailure(results.Last(), StreamingTaefOutputParser.CrashText);
        }

        // Excerpts of WTT logs written by TE.exe 10.104k (/inproc /enableWttLogging) for TAEF test DLLs written for these
        // tests: a VERIFY_ARE_EQUAL of strings containing new lines (ParseLab_taef.dll), and Log::Error(L"generated failure",
        // L"<generated>", L"", 1) (ParseLab4_taef.dll, file names like that also result from #line directives)
        private const string MultiLineAndGeneratedFileWttLog = @"<?xml version=""1.0"" encoding=""UTF-16"" ?>
<WTT-Logger>
<StartTest
	Title=""MultiLine::VerifyStringsWithNewlines""
	TUID="""" CA=""139748"" LA=""140265"" >
<Data>
<WexTraceInfo ThreadId=""6904"" ProcessId=""42212"" TimeStamp=""4412504060063"" LogSessionId=""1"" SessionTraceCount=""130""/><TestGroup TestId=""1200C314-4CFB-4AD0-9EB6-F53258E431DF"" />
</Data>	<rti id=""653078538"" />
	<ctx id=""3289281220"" />
</StartTest>
<Error
	File=""C:\src\LabSources\ParseLab\ParseLab.cpp""
	Line=""135""
	ErrCode=""0x0""
	ErrType=""""
	ErrorText=""Error 0x00000000""
	UserText=""AreEqual(expected, actual) - Values (line 1
line 2, line 1
line X)"" CA=""91896"" LA=""92136"" >
<Data>
<WexTraceInfo ThreadId=""42068"" ProcessId=""42212"" TimeStamp=""4412504061415"" LogSessionId=""1"" SessionTraceCount=""133""/><WexContext><![CDATA[Verify]]></WexContext>
</Data>	<rti id=""4002432429"" />
	<ctx id=""3289281220"" />
</Error>
<EndTest
	Title=""MultiLine::VerifyStringsWithNewlines""
	TUID=""""
	Result=""Fail""
	Repro="""" CA=""141828"" LA=""142075"" >
<Data>
<WexTraceInfo ThreadId=""6904"" ProcessId=""42212"" TimeStamp=""4412504061956"" LogSessionId=""1"" SessionTraceCount=""134""/>
</Data>	<rti id=""653078538"" />
	<ctx id=""3289281220"" />
</EndTest>
<StartTest
	Title=""Gen::EmptyFunction""
	TUID="""" CA=""139748"" LA=""140265"" >
<Data>
<WexTraceInfo ThreadId=""11240"" ProcessId=""25564"" TimeStamp=""4419075742000"" LogSessionId=""1"" SessionTraceCount=""9""/><TestGroup TestId=""5200C314-4CFB-4AD0-9EB6-F53258E431DF"" />
</Data>	<rti id=""214893055"" />
	<ctx id=""2034017238"" />
</StartTest>
<Error
	File=""&lt;generated&gt;""
	Line=""1""
	ErrCode=""0x0""
	ErrType=""""
	ErrorText=""Error 0x00000000""
	UserText=""generated failure"" CA=""570"" LA=""964"" >
<Data>
<WexTraceInfo ThreadId=""11240"" ProcessId=""25564"" TimeStamp=""4419075742561"" LogSessionId=""1"" SessionTraceCount=""11""/>
</Data>	<rti id=""214893055"" />
	<ctx id=""2034017238"" />
</Error>
<EndTest
	Title=""Gen::EmptyFunction""
	TUID=""""
	Result=""Fail""
	Repro="""" CA=""8585"" LA=""9142"" >
<Data>
<WexTraceInfo ThreadId=""11240"" ProcessId=""25564"" TimeStamp=""4419075751033"" LogSessionId=""1"" SessionTraceCount=""18""/>
</Data>	<rti id=""214893055"" />
	<ctx id=""2034017238"" />
</EndTest>
</WTT-Logger>
";

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_ErrorsContainingNewLinesAndFileNamesInvalidInPaths_ResultsHaveCompleteMessagesAndStackTraces()
        {
            string wttLog = WriteWttLog(MultiLineAndGeneratedFileWttLog);

            IList<string> lines = new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput();
            IList<TestResult> results = new TaefOutputParser(
                CapturedTaefOutputs.ToTestCases(LabDll, "MultiLine::VerifyStringsWithNewlines", "Gen::EmptyFunction"), lines, MockLogger.Object).GetTestResults();

            // the message is split into lines like TE.exe's console output: the prefix is part of the first line, the source information of the last one
            lines.Should().ContainInOrder(
                "Error: Verify: AreEqual(expected, actual) - Values (line 1",
                "line 2, line 1",
                @"line X) [File: C:\src\LabSources\ParseLab\ParseLab.cpp, Function: , Line: 135]");
            results.Should().HaveCount(2);
            TestResultAssertions.AssertTestResultIsFailure(results[0], "Verify: AreEqual(expected, actual) - Values (line 1\nline 2, line 1\nline X)");
            results[0].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("ParseLab.cpp:135", @"C:\src\LabSources\ParseLab\ParseLab.cpp", "135"));

            TestResultAssertions.AssertTestResultIsFailure(results[1], "generated failure");
            results[1].ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("<generated>:1", "<generated>", "1"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_FileDoesNotExist_EmptyOutputAndDebugWarning()
        {
            string wttLog = _directory.GetPath("DoesNotExist.wtl");

            new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput().Should().BeEmpty();
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("does not exist"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_NoXml_EmptyOutputAndDebugWarning()
        {
            string wttLog = WriteWttLog("This is not a WTT log");

            new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput().Should().BeEmpty();
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("could not be parsed completely"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_EmptyLog_EmptyOutput()
        {
            string wttLog = WriteWttLog("<?xml version=\"1.0\" encoding=\"UTF-16\" ?>\n<WTT-Logger>\n</WTT-Logger>\n");

            new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput().Should().BeEmpty();
            MockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DeleteLogFile_ExistingAndMissingFiles_AreHandled()
        {
            string wttLog = WriteWttLog(WttLog);

            WttLogParser.DeleteLogFile(wttLog, MockLogger.Object);
            File.Exists(wttLog).Should().BeFalse();

            WttLogParser.DeleteLogFile(wttLog, MockLogger.Object);
            MockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DeleteLogFile_TraceFileLeftBehindByTerminatedTe_IsDeletedToo()
        {
            string wttLog = WriteWttLog(WttLog);
            string traceFile = WriteWttLog(ToTraceFileContent(WttLog), "test.wtl" + WttLogParser.TraceFileExtension);

            WttLogParser.DeleteLogFile(wttLog, MockLogger.Object);

            File.Exists(wttLog).Should().BeFalse();
            File.Exists(traceFile).Should().BeFalse();
            MockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_OnlyTraceFileOfCompleteRun_IsConvertedLikeTheWttLog()
        {
            string wttLog = Path.Combine(Path.GetDirectoryName(WriteWttLog(WttLog, "reference.wtl")), "test.wtl");
            WriteWttLog(ToTraceFileContent(WttLog), "test.wtl" + WttLogParser.TraceFileExtension);

            IList<string> lines = new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput();

            lines.Should().Equal(ExpectedConsoleOutput);
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("converting") && s.Contains(WttLogParser.TraceFileExtension))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetConsoleOutput_OnlyTraceFileOfCrashedTe_EverythingUpToTruncationIsConvertedAndRunningTestCrashed()
        {
            string content = WttLog.Substring(0, WttLog.IndexOf(TruncationMarker, StringComparison.Ordinal))
                             + "<Error \n\tFile=\"\" \n\tLine=\"-1\" \n\tErrCode=\"0x0\" ";
            string wttLog = Path.Combine(Path.GetDirectoryName(WriteWttLog(WttLog, "reference.wtl")), "test.wtl");
            WriteWttLog(ToTraceFileContent(content), "test.wtl" + WttLogParser.TraceFileExtension);

            IList<string> lines = new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput();

            lines.Should().Equal(ExpectedConsoleOutput.Take(ExpectedConsoleOutput.ToList().IndexOf("StartGroup: " + ThrowStdException) + 1));
            IList<TestResult> results = ParseConsoleOutput(lines, out StreamingTaefOutputParser parser);
            parser.CrashedTestCase.FullyQualifiedName.Should().Be(ThrowStdException);
            TestResultAssertions.AssertTestResultIsFailure(results.Last(), StreamingTaefOutputParser.CrashText);
        }

        // TE.exe's WTT logger writes the entries without XML declaration and root element while tests are running
        private static string ToTraceFileContent(string wttLog)
        {
            string content = Regex.Replace(wttLog, @"^\s*<\?xml[^>]*\?>\s*<WTT-Logger>\s*", "");
            return Regex.Replace(content, @"\s*</WTT-Logger>\s*$", "\n");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Constructor_NullArguments_Throw()
        {
            // ReSharper disable ObjectCreationAsStatement
            new Action(() => new WttLogParser(null, MockLogger.Object)).Should().Throw<ArgumentNullException>();
            new Action(() => new WttLogParser("x.wtl", null)).Should().Throw<ArgumentNullException>();
            // ReSharper restore ObjectCreationAsStatement
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetConsoleOutput_WttLogOfSampleTests_ResultsAreEqualToThoseOfConsoleOutput()
        {
            // TE.exe writes the WTT log in addition to its console output
            string testDll = TestResources.Tests_DebugX64;
            string wttLog = _directory.GetPath("Tests_taef.wtl");
            string[] selectedClasses = { "TaefSamples::ExplicitResults", "TaefSamples::FailingClassSetup", "TaefSamples::VerifyInClassSetup", "TaefSamples::Exceptions", "TaefSamples::NamedRows" };
            string query = string.Join(TaefConstants.SelectionOrOperator, selectedClasses.Select(TaefConstants.GetClassSelectionTerm));

            var consoleOutput = new List<string>();
            new DotNetProcessExecutor(false, MockLogger.Object).ExecuteCommandBlocking(
                TestResources.GetTeExecutable(SampleConfiguration.DebugX64),
                $"\"{testDll}\" {TaefConstants.OutputFormatOptions} {TaefConstants.GetWttLoggingOptions(wttLog)} {TaefConstants.GetSelectOption(query)}",
                _directory.Path, null, new Dictionary<string, string>(), consoleOutput.Add);

            File.Exists(wttLog).Should().BeTrue();
            IList<string> wttOutput = new WttLogParser(wttLog, MockLogger.Object).GetConsoleOutput();

            List<TestCase> testCases = consoleOutput
                .Where(l => l.StartsWith(StreamingTaefOutputParser.StartGroupPrefix))
                .Select(l => l.Substring(StreamingTaefOutputParser.StartGroupPrefix.Length))
                .Distinct()
                .Select(n => new TestCase(n, testDll, n, "", 0))
                .ToList();
            testCases.Should().HaveCountGreaterThan(15);

            IList<TestResult> consoleResults = ParseAll(testCases, consoleOutput);
            IList<TestResult> wttResults = ParseAll(testCases, wttOutput);

            wttResults.Select(tr => tr.TestCase).Should().Equal(consoleResults.Select(tr => tr.TestCase));
            wttResults.Select(tr => tr.Outcome).Should().Equal(consoleResults.Select(tr => tr.Outcome));
            wttResults.Select(tr => tr.ErrorMessage).Should().Equal(consoleResults.Select(tr => tr.ErrorMessage));
            // WTT logs do not contain the function, so only the source locations can be compared
            wttResults.Select(tr => GetSourceLocations(tr.ErrorStackTrace)).Should().Equal(consoleResults.Select(tr => GetSourceLocations(tr.ErrorStackTrace)));
            consoleResults.Should().Contain(tr => tr.ErrorStackTrace != null);
            consoleResults.Should().Contain(tr => tr.Outcome == TestOutcome.None);
            consoleResults.Should().Contain(tr => tr.Outcome == TestOutcome.Skipped);
            consoleResults.Should().Contain(tr => tr.ErrorMessage != null && tr.ErrorMessage.StartsWith(StreamingTaefOutputParser.BlockedPrefix));
        }

        private IList<TestResult> ParseAll(IEnumerable<TestCase> testCases, IEnumerable<string> lines)
        {
            return new TaefOutputParser(testCases, lines, MockLogger.Object).GetTestResults();
        }

        private static string GetSourceLocations(string stackTrace)
        {
            if (stackTrace == null)
                return null;
            return string.Join("|", System.Text.RegularExpressions.Regex.Matches(stackTrace, @" in (.+):line (\d+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value + ":" + m.Groups[2].Value));
        }

    }

}
