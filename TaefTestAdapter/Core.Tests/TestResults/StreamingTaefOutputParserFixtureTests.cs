// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using TaefTestAdapter.Framework;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;
using ILogger = TaefTestAdapter.Common.ILogger;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;

namespace TaefTestAdapter.TestResults
{
    /// <summary>
    /// Tests of <see cref="StreamingTaefOutputParser"/> (and <see cref="TaefOutputParser"/>) with verbatim TE.exe 10.104k
    /// x64 outputs of fixtures which fail in various ways (logging errors before returning false, log groups, crashes and
    /// timeouts of fixtures, output starting like TE.exe's trailer, in-process crashes outside of tests) and of messages
    /// containing line breaks or glued to unflushed output. The outputs were captured from TAEF test DLLs written for
    /// these tests; only the source and DLL paths TE.exe printed (in 8.3 short and in long form) have been replaced by
    /// neutral paths.
    /// </summary>
    [TestClass]
    public class StreamingTaefOutputParserFixtureTests : TestsBase
    {
        private const string LabSourceDir = @"C:\src\LABSOU~1\";
        private const string LabSourceDirLong = @"C:\src\LabSources\";
        private const string ParseLabCpp = LabSourceDir + @"ParseLab\ParseLab.cpp";
        private const string ParseLab5Cpp = LabSourceDir + @"ParseLab\ParseLab5.cpp";
        private const string FixtureGroupsCpp = LabSourceDir + @"FixtureGroups\FG.cpp";
        private const string PendingErrorsCpp = LabSourceDirLong + @"PendingErrors\PendR.cpp";
        private const string LabDll = @"C:\src\LabSources\bin\ParseLab_taef.dll";

        private StreamingTaefOutputParser _parser;

        #region Verbatim outputs

        // ClassReason_taef.dll: class and method setup fixtures which call Log::Error() and return false, and a class setup
        // which only returns false
        private static readonly string[] SetupLogsErrorAndReturnsFalseRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "Error: CLASS_REASON: device not present",
            "TestBlocked: TAEF: Setup fixture 'ClassLogErrorFalse::ClassSetup' for the scope 'ClassLogErrorFalse' returned 'false'.",
            "Error: TAEF: Setup fixture 'ClassLogErrorFalse::ClassSetup' for the scope 'ClassLogErrorFalse' failed.",
            "",
            "StartGroup: ClassLogErrorFalse::T1",
            "EndGroup: ClassLogErrorFalse::T1 [Failed]",
            "",
            "StartGroup: ClassLogErrorFalse::T2",
            "EndGroup: ClassLogErrorFalse::T2 [Failed]",
            "Error: METHOD_REASON: resource missing",
            "TestBlocked: TAEF: Setup fixture 'MethodLogErrorFalse::MethodSetup' for the scope 'MethodLogErrorFalse::M1' returned 'false'.",
            "Error: TAEF: Setup fixture 'MethodLogErrorFalse::MethodSetup' for the scope 'MethodLogErrorFalse::M1' failed.",
            "",
            "StartGroup: MethodLogErrorFalse::M1",
            "EndGroup: MethodLogErrorFalse::M1 [Failed]",
            "CONTROL comment",
            "TestBlocked: TAEF: Setup fixture 'ClassReturnFalseOnly::ClassSetup' for the scope 'ClassReturnFalseOnly' returned 'false'.",
            "",
            "StartGroup: ClassReturnFalseOnly::C1",
            "EndGroup: ClassReturnFalseOnly::C1 [Blocked]",
            "",
            "StartGroup: PassingClass::P1",
            "EndGroup: PassingClass::P1 [Passed]",
            "",
            "Summary of Errors Outside of Tests:",
            "    Error: CLASS_REASON: device not present",
            "    Error: TAEF: Setup fixture 'ClassLogErrorFalse::ClassSetup' for the scope 'ClassLogErrorFalse' failed.",
            "    Error: METHOD_REASON: resource missing",
            "    Error: TAEF: Setup fixture 'MethodLogErrorFalse::MethodSetup' for the scope 'MethodLogErrorFalse::M1' failed.",
            "",
            "Summary of Non-passing Tests:",
            "    ClassLogErrorFalse::T1 [Failed]",
            "    ClassLogErrorFalse::T2 [Failed]",
            "    MethodLogErrorFalse::M1 [Failed]",
            "    ClassReturnFalseOnly::C1 [Blocked]",
            "",
            "Summary: Total=5, Passed=1, Failed=3, Blocked=1, Not Run=0, Skipped=0"
        };

        // ModuleFail_taef.dll: MODULE_SETUP which calls Log::Error() and returns false (the scope is the path of the test DLL)
        private static readonly string[] ModuleSetupLogsErrorAndReturnsFalseRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "Error: module setup: reason why the module cannot run",
            "TestBlocked: TAEF: Setup fixture 'ModSetup' for the scope '" + LabSourceDir + "bin\\ModuleFail_taef.dll' returned 'false'.",
            "Error: TAEF: Setup fixture 'ModSetup' for the scope '" + LabSourceDir + "bin\\ModuleFail_taef.dll' failed.",
            "",
            "StartGroup: ModA::T1",
            "EndGroup: ModA::T1 [Failed]",
            "",
            "StartGroup: N::ModB::T2",
            "EndGroup: N::ModB::T2 [Failed]",
            "",
            "Summary of Errors Outside of Tests:",
            "    Error: module setup: reason why the module cannot run",
            "    Error: TAEF: Setup fixture 'ModSetup' for the scope '" + LabSourceDir + "bin\\ModuleFail_taef.dll' failed.",
            "",
            "Summary of Non-passing Tests:",
            "    ModA::T1 [Failed]",
            "    N::ModB::T2 [Failed]",
            "",
            "Summary: Total=2, Passed=0, Failed=2, Blocked=0, Not Run=0, Skipped=0"
        };

        // PendR_taef.dll: class and method setup fixtures logging other lines between their errors and the fixture failure lines
        private static readonly string[] SetupErrorsFollowedByOtherLinesRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "Error: R1 contiguous reason",
            "Error: TAEF: Setup fixture 'P1_Contiguous::ClassSetup' for the scope 'P1_Contiguous' failed.",
            "",
            "StartGroup: P1_Contiguous::T1",
            "EndGroup: P1_Contiguous::T1 [Failed]",
            "",
            "StartGroup: P1_Contiguous::T2",
            "EndGroup: P1_Contiguous::T2 [Failed]",
            "Error: R2 service XYZ is not running",
            "cleaning up partially initialized state",
            "Error: TAEF: Setup fixture 'P2_ErrComment::ClassSetup' for the scope 'P2_ErrComment' failed.",
            "",
            "StartGroup: P2_ErrComment::T1",
            "EndGroup: P2_ErrComment::T1 [Failed]",
            "Error: R3 resource missing",
            "Warning: retry not possible",
            "Error: TAEF: Setup fixture 'P3_MethodErrWarn::MethodSetup' for the scope 'P3_MethodErrWarn::M1' failed.",
            "",
            "StartGroup: P3_MethodErrWarn::M1",
            "EndGroup: P3_MethodErrWarn::M1 [Failed]",
            "Error: Verify: AreEqual(1, 2): R4 init failed - Values (1, 2) [File: " + LabSourceDirLong + "PendingErrors\\PendR.cpp, Function: P4_VerifyThenComment::ClassSetup, Line: 53]",
            "after verify",
            "Error: TAEF: Setup fixture 'P4_VerifyThenComment::ClassSetup' for the scope 'P4_VerifyThenComment' failed.",
            "",
            "StartGroup: P4_VerifyThenComment::T1",
            "EndGroup: P4_VerifyThenComment::T1 [Failed]",
            "Error: Verify: AreEqual(expected, actual) - Values (R5 line 1",
            "line 2, R5 line 1",
            "line X) [File: " + LabSourceDirLong + "PendingErrors\\PendR.cpp, Function: P5_MultiLineVerify::ClassSetup, Line: 69]",
            "Error: TAEF: Setup fixture 'P5_MultiLineVerify::ClassSetup' for the scope 'P5_MultiLineVerify' failed.",
            "",
            "StartGroup: P5_MultiLineVerify::T1",
            "EndGroup: P5_MultiLineVerify::T1 [Failed]",
            "Error: R6 the real reason",
            "cleaning up",
            "TestBlocked: TAEF: Setup fixture 'P6_ErrCommentFalse::ClassSetup' for the scope 'P6_ErrCommentFalse' returned 'false'.",
            "Error: TAEF: Setup fixture 'P6_ErrCommentFalse::ClassSetup' for the scope 'P6_ErrCommentFalse' failed.",
            "",
            "StartGroup: P6_ErrCommentFalse::T1",
            "EndGroup: P6_ErrCommentFalse::T1 [Failed]",
            "Error: R7 contiguous then false",
            "TestBlocked: TAEF: Setup fixture 'P7_ErrFalse::ClassSetup' for the scope 'P7_ErrFalse' returned 'false'.",
            "Error: TAEF: Setup fixture 'P7_ErrFalse::ClassSetup' for the scope 'P7_ErrFalse' failed.",
            "",
            "StartGroup: P7_ErrFalse::T1",
            "EndGroup: P7_ErrFalse::T1 [Failed]",
            "Error: R8 first check failed",
            "Verify: IsTrue(true)",
            "Error: TAEF: Setup fixture 'P8_ErrPassVerify::ClassSetup' for the scope 'P8_ErrPassVerify' failed.",
            "",
            "StartGroup: P8_ErrPassVerify::T1",
            "EndGroup: P8_ErrPassVerify::T1 [Failed]",
            "",
            "StartGroup: P9_Passing::T1",
            "body",
            "EndGroup: P9_Passing::T1 [Passed]",
            "",
            "Summary of Errors Outside of Tests (showing 10 of 16):",
            "    Error: R1 contiguous reason",
            "    Error: TAEF: Setup fixture 'P1_Contiguous::ClassSetup' for the scope 'P1_Contiguous' failed.",
            "    Error: R2 service XYZ is not running",
            "    Error: TAEF: Setup fixture 'P2_ErrComment::ClassSetup' for the scope 'P2_ErrComment' failed.",
            "    Error: R3 resource missing",
            "    Error: TAEF: Setup fixture 'P3_MethodErrWarn::MethodSetup' for the scope 'P3_MethodErrWarn::M1' failed.",
            "    Error: Verify: AreEqual(1, 2): R4 init failed - Values (1, 2) [File: " + LabSourceDirLong + "PendingErrors\\PendR.cpp, Function: P4_VerifyThenComment::ClassSetup, Line: 53]",
            "    Error: TAEF: Setup fixture 'P4_VerifyThenComment::ClassSetup' for the scope 'P4_VerifyThenComment' failed.",
            "    Error: Verify: AreEqual(expected, actual) - Values (R5 line 1",
            "line 2, R5 line 1",
            "line X) [File: " + LabSourceDirLong + "PendingErrors\\PendR.cpp, Function: P5_MultiLineVerify::ClassSetup, Line: 69]",
            "    Error: TAEF: Setup fixture 'P5_MultiLineVerify::ClassSetup' for the scope 'P5_MultiLineVerify' failed.",
            "",
            "Summary of Non-passing Tests:",
            "    P1_Contiguous::T1 [Failed]",
            "    P1_Contiguous::T2 [Failed]",
            "    P2_ErrComment::T1 [Failed]",
            "    P3_MethodErrWarn::M1 [Failed]",
            "    P4_VerifyThenComment::T1 [Failed]",
            "    P5_MultiLineVerify::T1 [Failed]",
            "    P6_ErrCommentFalse::T1 [Failed]",
            "    P7_ErrFalse::T1 [Failed]",
            "    P8_ErrPassVerify::T1 [Failed]",
            "",
            "Summary: Total=10, Passed=1, Failed=9, Blocked=0, Not Run=0, Skipped=0"
        };

        // FG_taef.dll: fixtures using log groups (WEX Log::StartGroup/EndGroup); a failing VERIFY leaves the group open,
        // which TE.exe ends as "Mismatched Group"
        private static readonly string[] FixtureLogGroupsRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "StartGroup: Initialize device",
            "initializing",
            "Error: Verify: AreEqual(1, 2): device initialization failed - Values (1, 2) [File: " + LabSourceDir + "FixtureGroups\\FG.cpp, Function: A_ClassSetupVerifyInGroup::ClassSetup, Line: 13]",
            "Error: TAEF: Setup fixture 'A_ClassSetupVerifyInGroup::ClassSetup' for the scope 'A_ClassSetupVerifyInGroup' failed.",
            "Error: Wex.Logger Mismatched Group: A log grouping named \"A_ClassSetupVerifyInGroup::ClassSetup\" was ended when the active group was \"Initialize device\".",
            "EndGroup: Wex.Logger Mismatched Group: Initialize device [Failed]",
            "",
            "StartGroup: A_ClassSetupVerifyInGroup::T1",
            "EndGroup: A_ClassSetupVerifyInGroup::T1 [Failed]",
            "",
            "StartGroup: A_ClassSetupVerifyInGroup::T2",
            "EndGroup: A_ClassSetupVerifyInGroup::T2 [Failed]",
            "",
            "StartGroup: Connect",
            "Error: reason: server not reachable",
            "EndGroup: Connect [Failed]",
            "TestBlocked: TAEF: Setup fixture 'B_ClassSetupErrorInGroupReturnFalse::ClassSetup' for the scope 'B_ClassSetupErrorInGroupReturnFalse' returned 'false'.",
            "Error: TAEF: Setup fixture 'B_ClassSetupErrorInGroupReturnFalse::ClassSetup' for the scope 'B_ClassSetupErrorInGroupReturnFalse' failed.",
            "",
            "StartGroup: B_ClassSetupErrorInGroupReturnFalse::T1",
            "EndGroup: B_ClassSetupErrorInGroupReturnFalse::T1 [Failed]",
            "",
            "StartGroup: Prepare",
            "Error: Verify: IsTrue(false): prepare failed [File: " + LabSourceDir + "FixtureGroups\\FG.cpp, Function: C_MethodSetupVerifyInGroup::MethodSetup, Line: 42]",
            "Error: TAEF: Setup fixture 'C_MethodSetupVerifyInGroup::MethodSetup' for the scope 'C_MethodSetupVerifyInGroup::T1' failed.",
            "Error: Wex.Logger Mismatched Group: A log grouping named \"C_MethodSetupVerifyInGroup::MethodSetup\" was ended when the active group was \"Prepare\".",
            "EndGroup: Wex.Logger Mismatched Group: Prepare [Failed]",
            "",
            "StartGroup: C_MethodSetupVerifyInGroup::T1",
            "EndGroup: C_MethodSetupVerifyInGroup::T1 [Failed]",
            "",
            "StartGroup: Setup ok",
            "fine",
            "EndGroup: Setup ok [Passed]",
            "",
            "StartGroup: D_ClassSetupGroupOk::T1",
            "ran",
            "EndGroup: D_ClassSetupGroupOk::T1 [Passed]",
            "",
            "StartGroup: Unclosed",
            "Error: reason: unclosed group error",
            "TestBlocked: TAEF: Setup fixture 'E_ClassSetupErrorNoEndGroup::ClassSetup' for the scope 'E_ClassSetupErrorNoEndGroup' returned 'false'.",
            "Error: TAEF: Setup fixture 'E_ClassSetupErrorNoEndGroup::ClassSetup' for the scope 'E_ClassSetupErrorNoEndGroup' failed.",
            "Error: Wex.Logger Mismatched Group: A log grouping named \"E_ClassSetupErrorNoEndGroup::ClassSetup\" was ended when the active group was \"Unclosed\".",
            "EndGroup: Wex.Logger Mismatched Group: Unclosed [Failed]",
            "",
            "StartGroup: E_ClassSetupErrorNoEndGroup::T1",
            "EndGroup: E_ClassSetupErrorNoEndGroup::T1 [Failed]",
            "Error: Verify: AreEqual(1, 2): no group verify - Values (1, 2) [File: " + LabSourceDir + "FixtureGroups\\FG.cpp, Function: F_ClassSetupVerifyNoGroup::ClassSetup, Line: 82]",
            "Error: TAEF: Setup fixture 'F_ClassSetupVerifyNoGroup::ClassSetup' for the scope 'F_ClassSetupVerifyNoGroup' failed.",
            "",
            "StartGroup: F_ClassSetupVerifyNoGroup::T1",
            "EndGroup: F_ClassSetupVerifyNoGroup::T1 [Failed]",
            "",
            "StartGroup: Z_Ok::Passes",
            "ok",
            "EndGroup: Z_Ok::Passes [Passed]",
            "",
            "StartGroup: Prep2",
            "Error: reason: method setup prep failed",
            "EndGroup: Prep2 [Failed]",
            "TestBlocked: TAEF: Setup fixture 'G_MethodSetupErrorInGroupReturnFalse::MethodSetup' for the scope 'G_MethodSetupErrorInGroupReturnFalse::T1' returned 'false'.",
            "Error: TAEF: Setup fixture 'G_MethodSetupErrorInGroupReturnFalse::MethodSetup' for the scope 'G_MethodSetupErrorInGroupReturnFalse::T1' failed.",
            "",
            "StartGroup: G_MethodSetupErrorInGroupReturnFalse::T1",
            "EndGroup: G_MethodSetupErrorInGroupReturnFalse::T1 [Failed]",
            "",
            "Summary of Errors Outside of Tests:",
            "    Error: TAEF: Setup fixture 'B_ClassSetupErrorInGroupReturnFalse::ClassSetup' for the scope 'B_ClassSetupErrorInGroupReturnFalse' failed.",
            "    Error: Verify: AreEqual(1, 2): no group verify - Values (1, 2) [File: " + LabSourceDir + "FixtureGroups\\FG.cpp, Function: F_ClassSetupVerifyNoGroup::ClassSetup, Line: 82]",
            "    Error: TAEF: Setup fixture 'F_ClassSetupVerifyNoGroup::ClassSetup' for the scope 'F_ClassSetupVerifyNoGroup' failed.",
            "    Error: TAEF: Setup fixture 'G_MethodSetupErrorInGroupReturnFalse::MethodSetup' for the scope 'G_MethodSetupErrorInGroupReturnFalse::T1' failed.",
            "",
            "Summary of Non-passing Tests:",
            "    Initialize device [Failed]",
            "    A_ClassSetupVerifyInGroup::T1 [Failed]",
            "    A_ClassSetupVerifyInGroup::T2 [Failed]",
            "    Connect [Failed]",
            "    B_ClassSetupErrorInGroupReturnFalse::T1 [Failed]",
            "    Prepare [Failed]",
            "    C_MethodSetupVerifyInGroup::T1 [Failed]",
            "    Unclosed [Failed]",
            "    E_ClassSetupErrorNoEndGroup::T1 [Failed]",
            "    F_ClassSetupVerifyNoGroup::T1 [Failed]",
            "    Prep2 [Failed]",
            "    G_MethodSetupErrorInGroupReturnFalse::T1 [Failed]",
            "",
            "Summary: Total=15, Passed=3, Failed=12, Blocked=0, Not Run=0, Skipped=0"
        };

        // ParseLab5_taef.dll: VERIFY failing within a log group of a class setup
        private static readonly string[] VerifyInLogGroupOfClassSetupRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "StartGroup: Initialize device",
            "Error: Verify: AreEqual(1, 2): device initialization failed - Values (1, 2) [File: " + LabSourceDir + "ParseLab\\ParseLab5.cpp, Function: GroupInSetupVerify::ClassSetup, Line: 11]",
            "Error: TAEF: Setup fixture 'GroupInSetupVerify::ClassSetup' for the scope 'GroupInSetupVerify' failed.",
            "Error: Wex.Logger Mismatched Group: A log grouping named \"GroupInSetupVerify::ClassSetup\" was ended when the active group was \"Initialize device\".",
            "EndGroup: Wex.Logger Mismatched Group: Initialize device [Failed]",
            "",
            "StartGroup: GroupInSetupVerify::T1",
            "EndGroup: GroupInSetupVerify::T1 [Failed]",
            "",
            "StartGroup: GroupInSetupVerify::T2",
            "EndGroup: GroupInSetupVerify::T2 [Failed]",
            "",
            "StartGroup: ZZOk::Passes",
            "ok",
            "EndGroup: ZZOk::Passes [Passed]",
            "",
            "Summary of Non-passing Tests:",
            "    Initialize device [Failed]",
            "    GroupInSetupVerify::T1 [Failed]",
            "    GroupInSetupVerify::T2 [Failed]",
            "",
            "Summary: Total=4, Passed=1, Failed=3, Blocked=0, Not Run=0, Skipped=0"
        };

        // SetupCrash_taef.dll (out of process): crashing method cleanup (the following tests run normally), crashing
        // method setup (the test is blocked), crashing class cleanup
        private static readonly string[] CrashingFixturesRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "StartGroup: A_CleanupCrash::T1",
            "A T1 ran",
            "EndGroup: A_CleanupCrash::T1 [Passed]",
            "method cleanup about to crash",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'A_CleanupCrash::MethodCleanup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: A_CleanupCrash::T2",
            "A T2 ran",
            "EndGroup: A_CleanupCrash::T2 [Passed]",
            "method cleanup about to crash",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'A_CleanupCrash::MethodCleanup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "B setup",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'B_SetupCrashOnce::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: B_SetupCrashOnce::T1",
            "EndGroup: B_SetupCrashOnce::T1 [Blocked]",
            "B setup",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'B_SetupCrashOnce::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: B_SetupCrashOnce::T2",
            "EndGroup: B_SetupCrashOnce::T2 [Blocked]",
            "",
            "StartGroup: C_ClassCleanupCrash::T1",
            "C T1 ran",
            "EndGroup: C_ClassCleanupCrash::T1 [Passed]",
            "class cleanup about to crash",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'C_ClassCleanupCrash::ClassCleanup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: D_After::T1",
            "D T1 ran",
            "EndGroup: D_After::T1 [Passed]",
            "",
            "Summary of Errors Outside of Tests:",
            "    Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'A_CleanupCrash::MethodCleanup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "    Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'A_CleanupCrash::MethodCleanup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "    Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'B_SetupCrashOnce::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "    Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'B_SetupCrashOnce::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "    Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'C_ClassCleanupCrash::ClassCleanup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "Summary of Non-passing Tests:",
            "    B_SetupCrashOnce::T1 [Blocked]",
            "    B_SetupCrashOnce::T2 [Blocked]",
            "",
            "Summary: Total=6, Passed=4, Failed=0, Blocked=2, Not Run=0, Skipped=0"
        };

        // ParseLab_taef.dll (out of process): namespaced fixtures, setup fixtures logging errors and other lines before
        // returning false, a class cleanup logging "Summary of ...", log groups in fixtures and tests, messages containing
        // line breaks, crashing method and class setups
        private static readonly string[] ParseLabRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "[NsClassSetupFails] returning false",
            "TestBlocked: TAEF: Setup fixture 'NsA::NsB::NsClassSetupFails::ClassSetup' for the scope 'NsA::NsB::NsClassSetupFails' returned 'false'.",
            "",
            "StartGroup: NsA::NsB::NsClassSetupFails::T1",
            "EndGroup: NsA::NsB::NsClassSetupFails::T1 [Blocked]",
            "",
            "StartGroup: NsA::NsB::NsClassSetupFails::T2",
            "EndGroup: NsA::NsB::NsClassSetupFails::T2 [Blocked]",
            "TestBlocked: TAEF: Setup fixture 'NsA::NsB::NsMethodSetupFails::MethodSetup' for the scope 'NsA::NsB::NsMethodSetupFails::T1' returned 'false'.",
            "",
            "StartGroup: NsA::NsB::NsMethodSetupFails::T1",
            "EndGroup: NsA::NsB::NsMethodSetupFails::T1 [Blocked]",
            "Error: Verify: AreEqual(1, 2): verify in namespaced class setup - Values (1, 2) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: NsA::NsB::NsClassSetupVerify::ClassSetup, Line: 42]",
            "Error: TAEF: Setup fixture 'NsA::NsB::NsClassSetupVerify::ClassSetup' for the scope 'NsA::NsB::NsClassSetupVerify' failed.",
            "",
            "StartGroup: NsA::NsB::NsClassSetupVerify::T1",
            "EndGroup: NsA::NsB::NsClassSetupVerify::T1 [Failed]",
            "Error: the real reason: service XYZ is not running",
            "cleaning up partially initialized state",
            "TestBlocked: TAEF: Setup fixture 'SetupErrorThenComment::ClassSetup' for the scope 'SetupErrorThenComment' returned 'false'.",
            "Error: TAEF: Setup fixture 'SetupErrorThenComment::ClassSetup' for the scope 'SetupErrorThenComment' failed.",
            "",
            "StartGroup: SetupErrorThenComment::T1",
            "EndGroup: SetupErrorThenComment::T1 [Failed]",
            "Error: reason from setup: config file missing",
            "Warning: falling back to defaults failed",
            "TestBlocked: TAEF: Setup fixture 'SetupWarningThenFalse::ClassSetup' for the scope 'SetupWarningThenFalse' returned 'false'.",
            "Error: TAEF: Setup fixture 'SetupWarningThenFalse::ClassSetup' for the scope 'SetupWarningThenFalse' failed.",
            "",
            "StartGroup: SetupWarningThenFalse::T1",
            "EndGroup: SetupWarningThenFalse::T1 [Failed]",
            "",
            "StartGroup: CleanupLogsSummary::T1",
            "passes",
            "EndGroup: CleanupLogsSummary::T1 [Passed]",
            "Summary of allocations: 0 leaks",
            "Error: reason: device not present",
            "TestBlocked: TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' returned 'false'.",
            "Error: TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' failed.",
            "",
            "StartGroup: ZSetupAfterSummary::T1",
            "EndGroup: ZSetupAfterSummary::T1 [Failed]",
            "",
            "StartGroup: Setup phase",
            "inside user group in setup",
            "EndGroup: Setup phase [Passed]",
            "",
            "StartGroup: UserGroups::InTest",
            "",
            "StartGroup: Phase 1",
            "inside user group in test",
            "EndGroup: Phase 1 [Passed]",
            "Error: Verify: IsTrue(false) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: UserGroups::InTest, Line: 111]",
            "EndGroup: UserGroups::InTest [Failed]",
            "",
            "StartGroup: Setup phase 2",
            "Error: reason inside a user group",
            "EndGroup: Setup phase 2 [Failed]",
            "TestBlocked: TAEF: Setup fixture 'UserGroupSetupFails::ClassSetup' for the scope 'UserGroupSetupFails' returned 'false'.",
            "Error: TAEF: Setup fixture 'UserGroupSetupFails::ClassSetup' for the scope 'UserGroupSetupFails' failed.",
            "",
            "StartGroup: UserGroupSetupFails::T1",
            "EndGroup: UserGroupSetupFails::T1 [Failed]",
            "",
            "StartGroup: MultiLine::VerifyStringsWithNewlines",
            "Error: Verify: AreEqual(expected, actual) - Values (line 1",
            "line 2, line 1",
            "line X) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: MultiLine::VerifyStringsWithNewlines, Line: 135]",
            "EndGroup: MultiLine::VerifyStringsWithNewlines [Failed]",
            "",
            "StartGroup: MultiLine::VerifyMessageWithNewline",
            "Error: Verify: IsTrue(false): first message line",
            "second message line [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: MultiLine::VerifyMessageWithNewline, Line: 139]",
            "EndGroup: MultiLine::VerifyMessageWithNewline [Failed]",
            "",
            "StartGroup: MultiLine::LogErrorWithNewline",
            "Error: error line 1",
            "error line 2",
            "EndGroup: MultiLine::LogErrorWithNewline [Failed]",
            "",
            "StartGroup: MultiLine::StdExceptionWithNewline",
            "Error: Caught std::exception: exception line 1",
            "exception line 2",
            "EndGroup: MultiLine::StdExceptionWithNewline [Failed]",
            "",
            "StartGroup: MultiLine::VerifyStringsWithCR",
            "Error: Verify: AreEqual(expected, actual) - Values (a",
            "b, a",
            "c) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: MultiLine::VerifyStringsWithCR, Line: 153]",
            "EndGroup: MultiLine::VerifyStringsWithCR [Failed]",
            "",
            "StartGroup: Lambda::FailInLambda",
            "Error: Verify: AreEqual(1, 2) - Values (1, 2) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: Lambda::FailInLambda::<lambda_a7c43df15a618f4f944d0fa9a444e168>::operator (), Line: 162]",
            "EndGroup: Lambda::FailInLambda [Failed]",
            "",
            "StartGroup: Lambda::ExplicitFileLine",
            "Error: explicit location [File: srcParseLab.cpp, Function: Lambda::ExplicitFileLine, Line: 170]",
            "EndGroup: Lambda::ExplicitFileLine [Failed]",
            "",
            "StartGroup: Lambda::CommentLooksLikeError",
            "Error: this is just a comment explaining the next step",
            "Error: Verify: AreEqual(3, 4) - Values (3, 4) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: Lambda::CommentLooksLikeError, Line: 172]",
            "EndGroup: Lambda::CommentLooksLikeError [Failed]",
            "method setup about to crash",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'CrashInSetup::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: CrashInSetup::T1",
            "EndGroup: CrashInSetup::T1 [Blocked]",
            "method setup about to crash",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'CrashInSetup::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: CrashInSetup::T2",
            "EndGroup: CrashInSetup::T2 [Blocked]",
            "class setup about to crash",
            "Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'CrashInClassSetup::ClassSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "StartGroup: CrashInClassSetup::T1",
            "EndGroup: CrashInClassSetup::T1 [Blocked]",
            "",
            "StartGroup: CrashInClassSetup::T2",
            "EndGroup: CrashInClassSetup::T2 [Blocked]",
            "",
            "StartGroup: ZZAfter::Passes",
            "ZZAfter::Passes ran",
            "EndGroup: ZZAfter::Passes [Passed]",
            "",
            "Summary of Errors Outside of Tests (showing 10 of 12):",
            "    Error: Verify: AreEqual(1, 2): verify in namespaced class setup - Values (1, 2) [File: " + LabSourceDir + "ParseLab\\ParseLab.cpp, Function: NsA::NsB::NsClassSetupVerify::ClassSetup, Line: 42]",
            "    Error: TAEF: Setup fixture 'NsA::NsB::NsClassSetupVerify::ClassSetup' for the scope 'NsA::NsB::NsClassSetupVerify' failed.",
            "    Error: the real reason: service XYZ is not running",
            "    Error: TAEF: Setup fixture 'SetupErrorThenComment::ClassSetup' for the scope 'SetupErrorThenComment' failed.",
            "    Error: reason from setup: config file missing",
            "    Error: TAEF: Setup fixture 'SetupWarningThenFalse::ClassSetup' for the scope 'SetupWarningThenFalse' failed.",
            "    Error: reason: device not present",
            "    Error: TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' failed.",
            "    Error: TAEF: Setup fixture 'UserGroupSetupFails::ClassSetup' for the scope 'UserGroupSetupFails' failed.",
            "    Error: TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: 'CrashInSetup::MethodSetup'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))",
            "",
            "Summary of Non-passing Tests (showing 20 of 22):",
            "    NsA::NsB::NsClassSetupVerify::T1 [Failed]",
            "    SetupErrorThenComment::T1 [Failed]",
            "    SetupWarningThenFalse::T1 [Failed]",
            "    ZSetupAfterSummary::T1 [Failed]",
            "    UserGroups::InTest [Failed]",
            "    Setup phase 2 [Failed]",
            "    UserGroupSetupFails::T1 [Failed]",
            "    MultiLine::VerifyStringsWithNewlines [Failed]",
            "    MultiLine::VerifyMessageWithNewline [Failed]",
            "    MultiLine::LogErrorWithNewline [Failed]",
            "    MultiLine::StdExceptionWithNewline [Failed]",
            "    MultiLine::VerifyStringsWithCR [Failed]",
            "    Lambda::FailInLambda [Failed]",
            "    Lambda::ExplicitFileLine [Failed]",
            "    Lambda::CommentLooksLikeError [Failed]",
            "    NsA::NsB::NsClassSetupFails::T1 [Blocked]",
            "    NsA::NsB::NsClassSetupFails::T2 [Blocked]",
            "    NsA::NsB::NsMethodSetupFails::T1 [Blocked]",
            "    CrashInSetup::T1 [Blocked]",
            "    CrashInSetup::T2 [Blocked]",
            "",
            "Summary: Total=26, Passed=4, Failed=15, Blocked=7, Not Run=0, Skipped=0"
        };

        // ParseLab3_taef.dll with /inproc: printf() output without new line glued to TE.exe's next line
        private static readonly string[] GluedMessagesRun =
        {
            "Test Authoring and Execution Framework v10.104k for x64",
            "",
            "StartGroup: Glue::Throws",
            "working...Error: Caught std::exception: boom",
            "EndGroup: Glue::Throws [Failed]",
            "",
            "StartGroup: Glue::Skips",
            "checking prerequisites...TestSkipped: no GPU available",
            "EndGroup: Glue::Skips [Skipped]",
            "",
            "StartGroup: Glue::Blocks",
            "checking...TestBlocked: device missing",
            "EndGroup: Glue::Blocks [Blocked]",
            "",
            "Summary of Non-passing Tests:",
            "    Glue::Throws [Failed]",
            "    Glue::Blocks [Blocked]",
            "    Glue::Skips [Skipped]",
            "",
            "Summary: Total=3, Passed=0, Failed=1, Blocked=1, Not Run=0, Skipped=1"
        };

        #endregion

        #region Helpers

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();
            MockOptions.Setup(o => o.ParseSymbolInformation).Returns(false);
        }

        /// <summary>
        /// Parses <paramref name="lines"/> with a <see cref="StreamingTaefOutputParser"/> (available as <see cref="_parser"/>
        /// afterwards) and checks that the non-streaming <see cref="TaefOutputParser"/> produces the same results.
        /// </summary>
        private IList<TestResult> Parse(string testDll, IEnumerable<string> lines, params string[] testNames)
        {
            IList<TestCase> testCases = CapturedTaefOutputs.ToTestCases(testDll, testNames);
            List<string> linesList = lines.ToList();

            IList<TestResult> results = ParseStreaming(testCases, linesList, null);

            IList<TestResult> nonStreamingResults = new TaefOutputParser(testCases, linesList, new Mock<ILogger>().Object).GetTestResults();
            nonStreamingResults.Select(tr => tr.TestCase).Should().Equal(results.Select(tr => tr.TestCase));
            nonStreamingResults.Select(tr => tr.Outcome).Should().Equal(results.Select(tr => tr.Outcome));
            nonStreamingResults.Select(tr => tr.ErrorMessage).Should().Equal(results.Select(tr => tr.ErrorMessage));
            nonStreamingResults.Select(tr => tr.ErrorStackTrace).Should().Equal(results.Select(tr => tr.ErrorStackTrace));
            nonStreamingResults.Select(tr => tr.Output).Should().Equal(results.Select(tr => tr.Output));

            return results;
        }

        private IList<TestResult> ParseStreaming(IEnumerable<TestCase> testCases, IEnumerable<string> lines, int? teExitCode)
        {
            _parser = new StreamingTaefOutputParser(testCases, MockLogger.Object, MockFrameworkReporter.Object);
            foreach (string line in lines)
            {
                _parser.ReportLine(line);
            }
            _parser.Flush(teExitCode);
            return _parser.TestResults;
        }

        private static TestResult ResultOf(IEnumerable<TestResult> results, string testName)
            => results.Single(tr => tr.TestCase.FullyQualifiedName == testName);

        private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

        private static string HostCrash(string operation)
            => $"TAEF: [HRESULT 0x800706BE] A failure occurred while running a test operation: '{operation}'. (The test host process was unexpectedly terminated with exit code 0xC0000005 while invoking a test operation. (An RPC call failed.))";

        private static string[] Excerpt(string[] lines, string first, string last)
        {
            int start = Array.IndexOf(lines, first);
            int end = Array.IndexOf(lines, last);
            start.Should().BeGreaterOrEqualTo(0);
            end.Should().BeGreaterOrEqualTo(start);
            return lines.Skip(start).Take(end - start + 1).ToArray();
        }

        private void VerifyNoWarningsOrErrorsLogged()
        {
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region Setup fixtures logging errors

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SetupFixturesLoggingErrorAndReturningFalse_ReasonIsCarriedTogetherWithBothFixtureLines()
        {
            IList<TestResult> results = Parse(LabDll, SetupLogsErrorAndReturnsFalseRun,
                "ClassLogErrorFalse::T1", "ClassLogErrorFalse::T2", "MethodLogErrorFalse::M1", "ClassReturnFalseOnly::C1", "PassingClass::P1");

            results.Should().HaveCount(5);
            const string classBlocked = "TAEF: Setup fixture 'ClassLogErrorFalse::ClassSetup' for the scope 'ClassLogErrorFalse' returned 'false'.";
            const string classFailed = "TAEF: Setup fixture 'ClassLogErrorFalse::ClassSetup' for the scope 'ClassLogErrorFalse' failed.";
            foreach (string testName in new[] { "ClassLogErrorFalse::T1", "ClassLogErrorFalse::T2" })
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result, $"#1 - CLASS_REASON: device not present\n#2 - {classBlocked}\n#3 - {classFailed}");
                result.ErrorStackTrace.Should().BeNull();
                result.Output.Should().Be(Lines("Error: CLASS_REASON: device not present", "TestBlocked: " + classBlocked, "Error: " + classFailed));
            }

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "MethodLogErrorFalse::M1"),
                "#1 - METHOD_REASON: resource missing\n" +
                "#2 - TAEF: Setup fixture 'MethodLogErrorFalse::MethodSetup' for the scope 'MethodLogErrorFalse::M1' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'MethodLogErrorFalse::MethodSetup' for the scope 'MethodLogErrorFalse::M1' failed.");

            // no Log::Error(): only the TestBlocked line, the test is Blocked; the output of the fixture is carried as well
            TestResult classReturnsFalse = ResultOf(results, "ClassReturnFalseOnly::C1");
            TestResultAssertions.AssertTestResultIsFailure(classReturnsFalse,
                "Blocked: TAEF: Setup fixture 'ClassReturnFalseOnly::ClassSetup' for the scope 'ClassReturnFalseOnly' returned 'false'.");
            classReturnsFalse.Output.Should().Be(Lines("CONTROL comment",
                "TestBlocked: TAEF: Setup fixture 'ClassReturnFalseOnly::ClassSetup' for the scope 'ClassReturnFalseOnly' returned 'false'."));

            TestResult passing = ResultOf(results, "PassingClass::P1");
            TestResultAssertions.AssertTestResultIsPassed(passing);
            passing.Output.Should().BeNull();

            _parser.SummaryFound.Should().BeTrue();
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ModuleSetupLoggingErrorAndReturningFalse_ReasonIsCarriedToAllTests()
        {
            // the test DLL is run from another directory than the one TE.exe printed
            IList<TestResult> results = Parse(@"C:\tests\ModuleFail_taef.dll", ModuleSetupLogsErrorAndReturnsFalseRun, "ModA::T1", "N::ModB::T2");

            results.Should().HaveCount(2);
            const string scope = LabSourceDir + @"bin\ModuleFail_taef.dll";
            foreach (TestResult result in results)
            {
                TestResultAssertions.AssertTestResultIsFailure(result,
                    "#1 - module setup: reason why the module cannot run\n" +
                    $"#2 - TAEF: Setup fixture 'ModSetup' for the scope '{scope}' returned 'false'.\n" +
                    $"#3 - TAEF: Setup fixture 'ModSetup' for the scope '{scope}' failed.");
            }
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SetupFixtureFailingAgainAfterTestOfItsScope_IsANewFailure()
        {
            // e.g. a class setup which is run again in a new test host after a test of the class crashed the host process
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "TestBlocked: TAEF: Setup fixture 'C::ClassSetup' for the scope 'C' returned 'false'.",
                    "",
                    "StartGroup: C::T1",
                    "EndGroup: C::T1 [Blocked]",
                    "Error: second reason",
                    "Error: TAEF: Setup fixture 'C::ClassSetup' for the scope 'C' failed.",
                    "",
                    "StartGroup: C::T2",
                    "EndGroup: C::T2 [Failed]"
                },
                "C::T1", "C::T2");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "C::T1"),
                "Blocked: TAEF: Setup fixture 'C::ClassSetup' for the scope 'C' returned 'false'.");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "C::T2"),
                "#1 - second reason\n#2 - TAEF: Setup fixture 'C::ClassSetup' for the scope 'C' failed.");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_SetupErrorsFollowedByOtherLines_ErrorsAreCarriedWithFixtureFailure()
        {
            IList<TestResult> results = Parse(LabDll, SetupErrorsFollowedByOtherLinesRun,
                "P1_Contiguous::T1", "P1_Contiguous::T2", "P2_ErrComment::T1", "P3_MethodErrWarn::M1", "P4_VerifyThenComment::T1",
                "P5_MultiLineVerify::T1", "P6_ErrCommentFalse::T1", "P7_ErrFalse::T1", "P8_ErrPassVerify::T1", "P9_Passing::T1");

            string Failed(string className, string fixture = "ClassSetup", string scope = null)
                => $"TAEF: Setup fixture '{className}::{fixture}' for the scope '{scope ?? className}' failed.";
            string ReturnedFalse(string className)
                => $"TAEF: Setup fixture '{className}::ClassSetup' for the scope '{className}' returned 'false'.";

            foreach (string testName in new[] { "P1_Contiguous::T1", "P1_Contiguous::T2" })
            {
                TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, testName), $"#1 - R1 contiguous reason\n#2 - {Failed("P1_Contiguous")}");
            }

            TestResult p2 = ResultOf(results, "P2_ErrComment::T1");
            TestResultAssertions.AssertTestResultIsFailure(p2, $"#1 - R2 service XYZ is not running\n#2 - {Failed("P2_ErrComment")}");
            p2.Output.Should().Be(Lines("Error: R2 service XYZ is not running", "cleaning up partially initialized state", "Error: " + Failed("P2_ErrComment")));

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "P3_MethodErrWarn::M1"),
                $"#1 - R3 resource missing\n#2 - {Failed("P3_MethodErrWarn", "MethodSetup", "P3_MethodErrWarn::M1")}");

            TestResult p4 = ResultOf(results, "P4_VerifyThenComment::T1");
            TestResultAssertions.AssertTestResultIsFailure(p4, $"#1 - Verify: AreEqual(1, 2): R4 init failed - Values (1, 2)\n#2 - {Failed("P4_VerifyThenComment")}");
            p4.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - P4_VerifyThenComment::ClassSetup", PendingErrorsCpp, "53"));

            // a VERIFY_ARE_EQUAL of strings containing new lines
            TestResult p5 = ResultOf(results, "P5_MultiLineVerify::T1");
            TestResultAssertions.AssertTestResultIsFailure(p5,
                $"#1 - Verify: AreEqual(expected, actual) - Values (R5 line 1\nline 2, R5 line 1\nline X)\n#2 - {Failed("P5_MultiLineVerify")}");
            p5.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - P5_MultiLineVerify::ClassSetup", PendingErrorsCpp, "69"));

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "P6_ErrCommentFalse::T1"),
                $"#1 - R6 the real reason\n#2 - {ReturnedFalse("P6_ErrCommentFalse")}\n#3 - {Failed("P6_ErrCommentFalse")}");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "P7_ErrFalse::T1"),
                $"#1 - R7 contiguous then false\n#2 - {ReturnedFalse("P7_ErrFalse")}\n#3 - {Failed("P7_ErrFalse")}");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "P8_ErrPassVerify::T1"),
                $"#1 - R8 first check failed\n#2 - {Failed("P8_ErrPassVerify")}");

            TestResult p9 = ResultOf(results, "P9_Passing::T1");
            TestResultAssertions.AssertTestResultIsPassed(p9);
            p9.Output.Should().Be("body");

            // all errors belong to fixture failures (the trailing summary is not logged)
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ErrorsOutsideOfTestsWithoutFixtureFailure_AreLoggedAsWarningsOnce()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: A::T1",
                    "EndGroup: A::T1 [Passed]",
                    "Error: stray error of a cleanup",
                    "a comment",
                    "Error: Verify: AreEqual(expected, actual) - Values (a",
                    @"b) [File: C:\src\a.cpp, Function: A::MethodCleanup, Line: 3]",
                    "",
                    "StartGroup: A::T2",
                    "EndGroup: A::T2 [Passed]",
                    "Error: error at the end"
                },
                "A::T1", "A::T2");

            results.Should().OnlyContain(tr => tr.Outcome == TestOutcome.Passed && tr.Output == null);
            MockLogger.Verify(l => l.LogWarning("ParseLab_taef.dll: Error: stray error of a cleanup"), Times.Once);
            MockLogger.Verify(l => l.LogWarning("ParseLab_taef.dll: " + Lines(
                "Error: Verify: AreEqual(expected, actual) - Values (a", @"b) [File: C:\src\a.cpp, Function: A::MethodCleanup, Line: 3]")), Times.Once);
            MockLogger.Verify(l => l.LogWarning("ParseLab_taef.dll: Error: error at the end"), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(3));
        }

        #endregion

        #region Log groups of fixtures

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FixturesWithLogGroups_ContentOfGroupsIsFixtureOutput()
        {
            string[] names =
            {
                "A_ClassSetupVerifyInGroup::T1", "A_ClassSetupVerifyInGroup::T2", "B_ClassSetupErrorInGroupReturnFalse::T1",
                "C_MethodSetupVerifyInGroup::T1", "D_ClassSetupGroupOk::T1", "E_ClassSetupErrorNoEndGroup::T1",
                "F_ClassSetupVerifyNoGroup::T1", "Z_Ok::Passes", "G_MethodSetupErrorInGroupReturnFalse::T1"
            };
            IList<TestResult> results = Parse(LabDll, FixtureLogGroupsRun, names);

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(names);
            MockFrameworkReporter.Verify(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()), Times.Exactly(names.Length));

            // A: VERIFY in a class setup within a log group, which TE.exe ends as mismatched group after the fixture failure
            const string aFailed = "TAEF: Setup fixture 'A_ClassSetupVerifyInGroup::ClassSetup' for the scope 'A_ClassSetupVerifyInGroup' failed.";
            foreach (string testName in names.Take(2))
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result,
                    $"#1 - Verify: AreEqual(1, 2): device initialization failed - Values (1, 2)\n#2 - {aFailed}");
                result.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - A_ClassSetupVerifyInGroup::ClassSetup", FixtureGroupsCpp, "13"));
                result.Output.Should().Be(Lines(
                    "StartGroup: Initialize device",
                    "initializing",
                    "Error: Verify: AreEqual(1, 2): device initialization failed - Values (1, 2) [File: " + FixtureGroupsCpp + ", Function: A_ClassSetupVerifyInGroup::ClassSetup, Line: 13]",
                    "Error: " + aFailed));
            }

            // B: Log::Error() within a properly ended log group, then return false
            TestResult b = ResultOf(results, "B_ClassSetupErrorInGroupReturnFalse::T1");
            TestResultAssertions.AssertTestResultIsFailure(b,
                "#1 - reason: server not reachable\n" +
                "#2 - TAEF: Setup fixture 'B_ClassSetupErrorInGroupReturnFalse::ClassSetup' for the scope 'B_ClassSetupErrorInGroupReturnFalse' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'B_ClassSetupErrorInGroupReturnFalse::ClassSetup' for the scope 'B_ClassSetupErrorInGroupReturnFalse' failed.");
            b.Output.Should().StartWith(Lines("StartGroup: Connect", "Error: reason: server not reachable", "EndGroup: Connect [Failed]", "TestBlocked: "));

            // C: VERIFY in a method setup within a log group
            TestResult c = ResultOf(results, "C_MethodSetupVerifyInGroup::T1");
            TestResultAssertions.AssertTestResultIsFailure(c,
                "#1 - Verify: IsTrue(false): prepare failed\n" +
                "#2 - TAEF: Setup fixture 'C_MethodSetupVerifyInGroup::MethodSetup' for the scope 'C_MethodSetupVerifyInGroup::T1' failed.");
            c.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - C_MethodSetupVerifyInGroup::MethodSetup", FixtureGroupsCpp, "42"));

            // D: a passing fixture with a log group does not change the test
            TestResult d = ResultOf(results, "D_ClassSetupGroupOk::T1");
            TestResultAssertions.AssertTestResultIsPassed(d);
            d.Output.Should().Be("ran");

            // E: Log::Error() within a log group which the fixture does not end, then return false
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "E_ClassSetupErrorNoEndGroup::T1"),
                "#1 - reason: unclosed group error\n" +
                "#2 - TAEF: Setup fixture 'E_ClassSetupErrorNoEndGroup::ClassSetup' for the scope 'E_ClassSetupErrorNoEndGroup' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'E_ClassSetupErrorNoEndGroup::ClassSetup' for the scope 'E_ClassSetupErrorNoEndGroup' failed.");

            // F: control without log group
            TestResult f = ResultOf(results, "F_ClassSetupVerifyNoGroup::T1");
            TestResultAssertions.AssertTestResultIsFailure(f,
                "#1 - Verify: AreEqual(1, 2): no group verify - Values (1, 2)\n" +
                "#2 - TAEF: Setup fixture 'F_ClassSetupVerifyNoGroup::ClassSetup' for the scope 'F_ClassSetupVerifyNoGroup' failed.");
            f.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - F_ClassSetupVerifyNoGroup::ClassSetup", FixtureGroupsCpp, "82"));

            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, "Z_Ok::Passes"));

            // G: Log::Error() within a log group of a method setup, then return false
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "G_MethodSetupErrorInGroupReturnFalse::T1"),
                "#1 - reason: method setup prep failed\n" +
                "#2 - TAEF: Setup fixture 'G_MethodSetupErrorInGroupReturnFalse::MethodSetup' for the scope 'G_MethodSetupErrorInGroupReturnFalse::T1' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'G_MethodSetupErrorInGroupReturnFalse::MethodSetup' for the scope 'G_MethodSetupErrorInGroupReturnFalse::T1' failed.");

            _parser.SummaryFound.Should().BeTrue();
            // the "Mismatched Group" errors are not logged as warnings
            VerifyNoWarningsOrErrorsLogged();
            MockLogger.Verify(l => l.DebugWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_VerifyInLogGroupOfClassSetup_FailureIsCarriedToAllTestsOfClass()
        {
            IList<TestResult> results = Parse(LabDll, VerifyInLogGroupOfClassSetupRun, "GroupInSetupVerify::T1", "GroupInSetupVerify::T2", "ZZOk::Passes");

            foreach (TestResult result in results.Take(2))
            {
                TestResultAssertions.AssertTestResultIsFailure(result,
                    "#1 - Verify: AreEqual(1, 2): device initialization failed - Values (1, 2)\n" +
                    "#2 - TAEF: Setup fixture 'GroupInSetupVerify::ClassSetup' for the scope 'GroupInSetupVerify' failed.");
                result.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("#1 - GroupInSetupVerify::ClassSetup", ParseLab5Cpp, "11"));
            }
            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, "ZZOk::Passes"));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_NestedLogGroupsAndLogGroupNotEndedBeforeTest_AreHandled()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: A::T1",
                    "EndGroup: A::T1 [Passed]",
                    "StartGroup: Teardown",
                    "StartGroup: Inner",
                    @"Error: Verify: IsTrue(false): cleanup check [File: C:\src\a.cpp, Function: A::ClassCleanup, Line: 9]",
                    "Error: TAEF: Cleanup fixture 'A::ClassCleanup' for the scope 'A' failed.",
                    "Error: Wex.Logger Mismatched Group: A log grouping named \"A::ClassCleanup\" was ended when the active group was \"Inner\".",
                    "EndGroup: Wex.Logger Mismatched Group: Inner [Failed]",
                    "EndGroup: Teardown [Failed]",
                    "StartGroup: Not ended",
                    "TestBlocked: TAEF: Setup fixture 'B::ClassSetup' for the scope 'B' returned 'false'.",
                    "",
                    "StartGroup: B::T1",
                    "EndGroup: B::T1 [Blocked]"
                },
                "A::T1", "B::T1");

            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, "A::T1"));
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "B::T1"),
                "Blocked: TAEF: Setup fixture 'B::ClassSetup' for the scope 'B' returned 'false'.");

            // the cleanup failure within the log groups is logged
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s =>
                s.StartsWith("Test class 'A': cleanup failed after the result has been reported")
                && s.Contains(Lines("StartGroup: Teardown", "StartGroup: Inner", @"Error: Verify: IsTrue(false): cleanup check"))
                && s.EndsWith("Error: TAEF: Cleanup fixture 'A::ClassCleanup' for the scope 'A' failed."))), Times.Once);
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
            MockLogger.Verify(l => l.DebugWarning(It.Is<string>(s => s.Contains("group 'Not ended' has not been ended before test 'B::T1' started"))), Times.Once);
        }

        #endregion

        #region Crashing and timed out fixtures

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CrashingFixturesOutOfProcess_CrashOfSetupExplainsBlockedTestsOnly()
        {
            string[] names =
            {
                "A_CleanupCrash::T1", "A_CleanupCrash::T2", "B_SetupCrashOnce::T1", "B_SetupCrashOnce::T2", "C_ClassCleanupCrash::T1", "D_After::T1"
            };
            IList<TestResult> results = Parse(LabDll, CrashingFixturesRun, names);

            results.Select(tr => tr.Outcome).Should().Equal(
                TestOutcome.Passed, TestOutcome.Passed, TestOutcome.Failed, TestOutcome.Failed, TestOutcome.Passed, TestOutcome.Passed);

            // the crash of the method cleanup (printed before the next test of the class) does not affect the next test
            ResultOf(results, "A_CleanupCrash::T2").Output.Should().Be("A T2 ran");

            // the crash of the method setup is printed before each blocked test
            foreach (string testName in new[] { "B_SetupCrashOnce::T1", "B_SetupCrashOnce::T2" })
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result, "Blocked: " + HostCrash("B_SetupCrashOnce::MethodSetup"));
                result.Output.Should().Be(Lines("B setup", "Error: " + HostCrash("B_SetupCrashOnce::MethodSetup")));
            }
            ResultOf(results, "D_After::T1").Output.Should().Be("D T1 ran");

            // each crash is logged as warning
            foreach ((string operation, int times) in new[] { ("A_CleanupCrash::MethodCleanup", 2), ("B_SetupCrashOnce::MethodSetup", 2), ("C_ClassCleanupCrash::ClassCleanup", 1) })
            {
                MockLogger.Verify(l => l.LogWarning("ParseLab_taef.dll: Error: " + HostCrash(operation)), Times.Exactly(times));
            }
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(5));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CrashingMethodAndClassSetupsOutOfProcess_BlockedTestsReportTheCrash()
        {
            string[] lines = Excerpt(ParseLabRun, "method setup about to crash", "EndGroup: ZZAfter::Passes [Passed]");
            IList<TestResult> results = Parse(LabDll, lines, "CrashInSetup::T1", "CrashInSetup::T2", "CrashInClassSetup::T1", "CrashInClassSetup::T2", "ZZAfter::Passes");

            foreach (string testName in new[] { "CrashInSetup::T1", "CrashInSetup::T2" })
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result, "Blocked: " + HostCrash("CrashInSetup::MethodSetup"));
                result.Output.Should().Be(Lines("method setup about to crash", "Error: " + HostCrash("CrashInSetup::MethodSetup")));
            }

            // the crash of the class setup is printed only once, before the first blocked test of the class
            foreach (string testName in new[] { "CrashInClassSetup::T1", "CrashInClassSetup::T2" })
            {
                TestResult result = ResultOf(results, testName);
                TestResultAssertions.AssertTestResultIsFailure(result, "Blocked: " + HostCrash("CrashInClassSetup::ClassSetup"));
                result.Output.Should().Be(Lines("class setup about to crash", "Error: " + HostCrash("CrashInClassSetup::ClassSetup")));
            }

            TestResult after = ResultOf(results, "ZZAfter::Passes");
            TestResultAssertions.AssertTestResultIsPassed(after);
            after.Output.Should().Be("ZZAfter::Passes ran");
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("A failure occurred while running a test operation"))), Times.Exactly(3));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CrashOfFixtureAndBlockedTestWithOwnReason_OwnReasonIsReported()
        {
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: C::T1",
                    "EndGroup: C::T1 [Passed]",
                    "Error: " + HostCrash("C::MethodCleanup"),
                    "TestBlocked: TAEF: Setup fixture 'C::MethodSetup' for the scope 'C::T2' returned 'false'.",
                    "",
                    "StartGroup: C::T2",
                    "EndGroup: C::T2 [Blocked]",
                    "",
                    "StartGroup: C::T3",
                    "TestBlocked: blocked by the test",
                    "EndGroup: C::T3 [Blocked]"
                },
                "C::T1", "C::T2", "C::T3");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "C::T2"),
                "Blocked: TAEF: Setup fixture 'C::MethodSetup' for the scope 'C::T2' returned 'false'.");
            TestResult t3 = ResultOf(results, "C::T3");
            TestResultAssertions.AssertTestResultIsFailure(t3, "Blocked: blocked by the test");
            t3.Output.Should().Be("TestBlocked: blocked by the test");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TimeoutOfSetupFixture_BlockedTestReportsTheTimeout()
        {
            // lines as printed for timed out tests (LongRunningTests_taef.dll.runTimeout.txt), here for a method setup
            const string timeoutExpired = "TAEF: The user-specified test timeout has expired. TAEF will now abort the test.";
            const string testTimeout = "TAEF: [HRESULT 0x800705B4] A test timeout expired while running a test operation: 'Ns::Slow::MethodSetup'. (The test host process was terminated by TAEF while invoking a test operation.)";
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "Error: " + timeoutExpired,
                    "Warning: TAEF: Forcibly terminating a test host process (PID: 42372).",
                    "Error: " + testTimeout,
                    "",
                    "StartGroup: Ns::Slow::Test",
                    "EndGroup: Ns::Slow::Test [Blocked]"
                },
                "Ns::Slow::Test");

            TestResultAssertions.AssertTestResultIsFailure(results.Single(), $"Blocked: #1 - {timeoutExpired}\n#2 - {testTimeout}");
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(3));
        }

        #endregion

        #region Complete run of ParseLab_taef.dll

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_CompleteRunOfParseLab_AllResultsHaveTheirReasons()
        {
            string[] names =
            {
                "NsA::NsB::NsClassSetupFails::T1", "NsA::NsB::NsClassSetupFails::T2", "NsA::NsB::NsMethodSetupFails::T1",
                "NsA::NsB::NsClassSetupVerify::T1", "SetupErrorThenComment::T1", "SetupWarningThenFalse::T1", "CleanupLogsSummary::T1",
                "ZSetupAfterSummary::T1", "UserGroups::InTest", "UserGroupSetupFails::T1", "MultiLine::VerifyStringsWithNewlines",
                "MultiLine::VerifyMessageWithNewline", "MultiLine::LogErrorWithNewline", "MultiLine::StdExceptionWithNewline",
                "MultiLine::VerifyStringsWithCR", "Lambda::FailInLambda", "Lambda::ExplicitFileLine", "Lambda::CommentLooksLikeError",
                "CrashInSetup::T1", "CrashInSetup::T2", "CrashInClassSetup::T1", "CrashInClassSetup::T2", "ZZAfter::Passes"
            };
            IList<TestResult> results = Parse(LabDll, ParseLabRun, names);

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(names);
            results.Where(tr => tr.Outcome == TestOutcome.Passed).Select(tr => tr.TestCase.FullyQualifiedName)
                .Should().Equal("CleanupLogsSummary::T1", "ZZAfter::Passes");
            results.Where(tr => tr.Outcome == TestOutcome.Failed).Should().OnlyContain(tr => !string.IsNullOrEmpty(tr.ErrorMessage));
            results.Should().NotContain(tr => tr.ErrorMessage != null && tr.ErrorMessage.Contains("no reason has been reported"));

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "SetupErrorThenComment::T1"),
                "#1 - the real reason: service XYZ is not running\n" +
                "#2 - TAEF: Setup fixture 'SetupErrorThenComment::ClassSetup' for the scope 'SetupErrorThenComment' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'SetupErrorThenComment::ClassSetup' for the scope 'SetupErrorThenComment' failed.");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "SetupWarningThenFalse::T1"),
                "#1 - reason from setup: config file missing\n" +
                "#2 - TAEF: Setup fixture 'SetupWarningThenFalse::ClassSetup' for the scope 'SetupWarningThenFalse' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'SetupWarningThenFalse::ClassSetup' for the scope 'SetupWarningThenFalse' failed.");

            // the class cleanup of CleanupLogsSummary logs "Summary of allocations: 0 leaks", which is no trailer of TE.exe
            TestResult afterSummary = ResultOf(results, "ZSetupAfterSummary::T1");
            TestResultAssertions.AssertTestResultIsFailure(afterSummary,
                "#1 - reason: device not present\n" +
                "#2 - TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' failed.");
            afterSummary.Output.Should().StartWith(Lines("Summary of allocations: 0 leaks", "Error: reason: device not present"));

            // log groups within a test are output of the test; log groups of fixtures contain fixture output
            TestResult inTest = ResultOf(results, "UserGroups::InTest");
            TestResultAssertions.AssertTestResultIsFailure(inTest, "Verify: IsTrue(false)");
            inTest.Output.Should().StartWith(Lines("", "StartGroup: Phase 1", "inside user group in test", "EndGroup: Phase 1 [Passed]"));
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "UserGroupSetupFails::T1"),
                "#1 - reason inside a user group\n" +
                "#2 - TAEF: Setup fixture 'UserGroupSetupFails::ClassSetup' for the scope 'UserGroupSetupFails' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'UserGroupSetupFails::ClassSetup' for the scope 'UserGroupSetupFails' failed.");

            // messages containing line breaks
            TestResult strings = ResultOf(results, "MultiLine::VerifyStringsWithNewlines");
            TestResultAssertions.AssertTestResultIsFailure(strings, "Verify: AreEqual(expected, actual) - Values (line 1\nline 2, line 1\nline X)");
            strings.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("MultiLine::VerifyStringsWithNewlines", ParseLabCpp, "135"));
            strings.Output.Should().Be(Lines(
                "Error: Verify: AreEqual(expected, actual) - Values (line 1",
                "line 2, line 1",
                "line X) [File: " + ParseLabCpp + ", Function: MultiLine::VerifyStringsWithNewlines, Line: 135]"));
            TestResult message = ResultOf(results, "MultiLine::VerifyMessageWithNewline");
            TestResultAssertions.AssertTestResultIsFailure(message, "Verify: IsTrue(false): first message line\nsecond message line");
            message.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("MultiLine::VerifyMessageWithNewline", ParseLabCpp, "139"));
            TestResult carriageReturns = ResultOf(results, "MultiLine::VerifyStringsWithCR");
            TestResultAssertions.AssertTestResultIsFailure(carriageReturns, "Verify: AreEqual(expected, actual) - Values (a\nb, a\nc)");
            carriageReturns.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("MultiLine::VerifyStringsWithCR", ParseLabCpp, "153"));
            // without source information, the following lines can not be told apart from other output (they are part of the output)
            TestResult logError = ResultOf(results, "MultiLine::LogErrorWithNewline");
            TestResultAssertions.AssertTestResultIsFailure(logError, "error line 1");
            logError.Output.Should().Be(Lines("Error: error line 1", "error line 2"));
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "MultiLine::StdExceptionWithNewline"), "Caught std::exception: exception line 1");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "Lambda::CommentLooksLikeError"),
                "#1 - this is just a comment explaining the next step\n#2 - Verify: AreEqual(3, 4) - Values (3, 4)");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "CrashInClassSetup::T2"), "Blocked: " + HostCrash("CrashInClassSetup::ClassSetup"));

            _parser.SummaryFound.Should().BeTrue();
            // only the crashes of the setup fixtures are logged (errors followed by fixture failures and the summary are not)
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Exactly(3));
            MockLogger.Verify(l => l.LogError(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region TE.exe's trailer

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FixtureOutputStartingLikeSummary_IsNoTrailer()
        {
            var lines = new List<string>(Excerpt(ParseLabRun, "StartGroup: CleanupLogsSummary::T1", "EndGroup: ZSetupAfterSummary::T1 [Failed]"));
            lines.AddRange(new[]
            {
                "Summary: 3 devices found",
                "class setup about to crash",
                "Error: " + HostCrash("D1CrashSetup::ClassSetup"),
                "",
                "StartGroup: D1CrashSetup::T1",
                "EndGroup: D1CrashSetup::T1 [Blocked]"
            });
            IList<TestResult> results = Parse(LabDll, lines, "CleanupLogsSummary::T1", "ZSetupAfterSummary::T1", "D1CrashSetup::T1");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "ZSetupAfterSummary::T1"),
                "#1 - reason: device not present\n" +
                "#2 - TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' returned 'false'.\n" +
                "#3 - TAEF: Setup fixture 'ZSetupAfterSummary::ClassSetup' for the scope 'ZSetupAfterSummary' failed.");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "D1CrashSetup::T1"), "Blocked: " + HostCrash("D1CrashSetup::ClassSetup"));
            _parser.SummaryFound.Should().BeFalse();
            MockLogger.Verify(l => l.LogWarning("ParseLab_taef.dll: Error: " + HostCrash("D1CrashSetup::ClassSetup")), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_FixtureOutputEqualToTrailerHeaderFollowedByLogGroup_FixtureFailureIsCarried()
        {
            // TE.exe's trailer contains no groups: a log group proves that the header has been fixture output
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: A::T1",
                    "EndGroup: A::T1 [Passed]",
                    "Summary of TAEF Warnings:",
                    "StartGroup: Connect",
                    "Error: reason",
                    "EndGroup: Connect [Failed]",
                    "TestBlocked: TAEF: Setup fixture 'B::ClassSetup' for the scope 'B' returned 'false'.",
                    "",
                    "StartGroup: B::T1",
                    "EndGroup: B::T1 [Blocked]"
                },
                "A::T1", "B::T1");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "B::T1"),
                "Blocked: #1 - reason\n#2 - TAEF: Setup fixture 'B::ClassSetup' for the scope 'B' returned 'false'.");
            _parser.SummaryFound.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_TrailerWithTruncatedSections_IsRecognizedAndNotLogged()
        {
            string[] lines = Excerpt(ParseLabRun, "StartGroup: ZZAfter::Passes", ParseLabRun.Last());
            lines.Should().Contain("Summary of Errors Outside of Tests (showing 10 of 12):");

            IList<TestResult> results = Parse(LabDll, lines, "ZZAfter::Passes");

            TestResultAssertions.AssertTestResultIsPassed(results.Single());
            _parser.SummaryFound.Should().BeTrue();
            VerifyNoWarningsOrErrorsLogged();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_OutputEndsWithinGroupAfterFixtureOutputStartingLikeSummary_SummaryIsNotFound()
        {
            IList<TestResult> results = ParseStreaming(CapturedTaefOutputs.ToTestCases(LabDll, "A::T1", "B::T1"), new[]
                {
                    "",
                    "StartGroup: A::T1",
                    "EndGroup: A::T1 [Passed]",
                    "Summary: 3 devices found",
                    "",
                    "StartGroup: B::T1"
                },
                unchecked((int)0xC0000005));

            _parser.SummaryFound.Should().BeFalse();
            _parser.CrashedTestCase.FullyQualifiedName.Should().Be("B::T1");
            results.Should().HaveCount(2);
        }

        #endregion

        #region TE.exe terminating abnormally outside of tests (/inproc)

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_InProcessCrashOfSetupFixture_AllTestsWithoutResultFail()
        {
            // ParseLab_taef.dll with /inproc /select:"@Name='CrashInSetup::*' or @Name='ZZAfter::*'" (verbatim), exit code 0xC0000005
            string[] names = { "CrashInSetup::T1", "CrashInSetup::T2", "ZZAfter::Passes" };
            var reportedAsStarted = new List<TestCase>();
            MockFrameworkReporter.Setup(r => r.ReportTestsStarted(It.IsAny<IEnumerable<TestCase>>()))
                .Callback<IEnumerable<TestCase>>(tcs => reportedAsStarted.AddRange(tcs));

            IList<TestResult> results = ParseStreaming(CapturedTaefOutputs.ToTestCases(LabDll, names),
                new[] { "Test Authoring and Execution Framework v10.104k for x64", "method setup about to crash" },
                unchecked((int)0xC0000005));

            results.Select(tr => tr.TestCase.FullyQualifiedName).Should().Equal(names);
            foreach (TestResult result in results)
            {
                TestResultAssertions.AssertTestResultIsFailure(result,
                    StreamingTaefOutputParser.TerminatedOutsideOfTestText + " with exit code 0xC0000005 outside of a test (probably in a setup or cleanup fixture running in process)" +
                    "\nLast output of TE.exe:\n\nmethod setup about to crash");
                result.Output.Should().Be("method setup about to crash");
                result.ErrorStackTrace.Should().BeNull();
            }
            reportedAsStarted.Select(tc => tc.FullyQualifiedName).Should().Equal(names);
            MockFrameworkReporter.Verify(r => r.ReportTestResults(It.IsAny<IEnumerable<TestResult>>()), Times.Exactly(3));
            _parser.TerminatedOutsideOfTest.Should().BeTrue();
            _parser.CrashedTestCase.Should().BeNull();
            _parser.SummaryFound.Should().BeFalse();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_InProcessTerminationAfterSomeTests_OnlyTestsWithoutResultFail()
        {
            IList<TestResult> results = ParseStreaming(CapturedTaefOutputs.ToTestCases(LabDll, "A::T1", "B::T1", "B::T2"), new[]
                {
                    "Test Authoring and Execution Framework v10.104k for x64",
                    "",
                    "StartGroup: A::T1",
                    "EndGroup: A::T1 [Passed]",
                    "A class cleanup",
                    "StartGroup: Connect",
                    @"Error: Verify: IsTrue(false): connect [File: C:\src\b.cpp, Function: B::ClassSetup, Line: 7]"
                },
                3);

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal("A::T1 Passed", "B::T1 Failed", "B::T2 Failed");
            TestResult result = ResultOf(results, "B::T1");
            result.ErrorMessage.Should().StartWith(StreamingTaefOutputParser.TerminatedOutsideOfTestText + " with exit code 0x00000003 outside of a test");
            result.Output.Should().Be(Lines("A class cleanup", "StartGroup: Connect", @"Error: Verify: IsTrue(false): connect [File: C:\src\b.cpp, Function: B::ClassSetup, Line: 7]"));
            result.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("B::ClassSetup", @"C:\src\b.cpp", "7"));
            _parser.TerminatedOutsideOfTest.Should().BeTrue();
            // the pending error is logged as well
            MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("Error: Verify: IsTrue(false): connect"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_InProcessTerminationWithinTestNotPartOfRun_TestsWithoutResultFail()
        {
            IList<TestResult> results = ParseStreaming(CapturedTaefOutputs.ToTestCases(LabDll, "A::T1"), new[]
                {
                    "",
                    "StartGroup: A::Data#error"
                },
                unchecked((int)0xC0000005));

            TestResultAssertions.AssertTestResultIsFailure(results.Single(),
                StreamingTaefOutputParser.TerminatedOutsideOfTestText + " with exit code 0xC0000005 while running test 'A::Data#error', which is not part of the tests run");
            _parser.CrashedTestCase.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_InProcessTerminationWithinUnknownGroup_GroupIsMentioned()
        {
            // the group might be a log group of a fixture, or a test TE.exe runs although it is not part of the run (e.g. a
            // data row matched by the pattern of a selected row name)
            IList<TestResult> results = ParseStreaming(CapturedTaefOutputs.ToTestCases(LabDll, "W::Rows#c*", "W::Rows#zz"), new[]
                {
                    "Test Authoring and Execution Framework v10.104k for x64",
                    "",
                    "StartGroup: W::Rows#c*",
                    "EndGroup: W::Rows#c* [Passed]",
                    "",
                    "StartGroup: W::Rows#cboom",
                    "about to crash"
                },
                unchecked((int)0xC0000005));

            results.Select(tr => $"{tr.TestCase.FullyQualifiedName} {tr.Outcome}").Should().Equal("W::Rows#c* Passed", "W::Rows#zz Failed");
            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "W::Rows#zz"),
                StreamingTaefOutputParser.TerminatedOutsideOfTestText + " with exit code 0xC0000005 outside of a test, within group 'W::Rows#cboom' " +
                "(a log group of a setup or cleanup fixture running in process, or a test which is not part of the tests run)" +
                "\nLast output of TE.exe:\n\nStartGroup: W::Rows#cboom\nabout to crash");
            _parser.TerminatedOutsideOfTest.Should().BeTrue();
            _parser.TestCasesNotRun.Select(tc => tc.FullyQualifiedName).Should().Equal("W::Rows#zz");
            _parser.CrashedTestCase.Should().BeNull();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Flush_NoAbnormalTermination_NoResultsForTestsWithoutResult()
        {
            IList<TestCase> testCases = CapturedTaefOutputs.ToTestCases(LabDll, "A::T1", "B::T1");
            string[] output = { "Test Authoring and Execution Framework v10.104k for x64", "", "StartGroup: A::T1", "EndGroup: A::T1 [Passed]" };
            string[] outputWithSummary = output.Concat(new[] { "", "Summary: Total=1, Passed=1, Failed=0, Blocked=0, Not Run=0, Skipped=0" }).ToArray();

            // TE.exe terminated normally (with its summary), exit code of TAEF itself, TE.exe could not be run, unknown exit
            // code, no output at all (e.g. debugging with the VsTest framework)
            foreach ((string[] lines, int? exitCode) in new (string[], int?)[]
            {
                (outputWithSummary, 0), (outputWithSummary, unchecked((int)0xC0000005)), (output, TaefConstants.ExitCodeStartupError),
                (output, int.MaxValue), (output, null), (new string[0], unchecked((int)0xC0000005))
            })
            {
                IList<TestResult> results = ParseStreaming(testCases, lines, exitCode);

                results.Should().HaveCount(lines.Length == 0 ? 0 : 1, $"exit code: {exitCode}");
                _parser.TerminatedOutsideOfTest.Should().BeFalse();
                _parser.TestCasesNotRun.Should().BeEmpty();
            }
        }

        #endregion

        #region Messages within groups

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_MessagesGluedToUnflushedOutputInProcess_AreRecognized()
        {
            IList<TestResult> results = Parse(LabDll, GluedMessagesRun, "Glue::Throws", "Glue::Skips", "Glue::Blocks");

            TestResultAssertions.AssertTestResultIsFailure(ResultOf(results, "Glue::Throws"), "Caught std::exception: boom");
            TestResult skips = ResultOf(results, "Glue::Skips");
            TestResultAssertions.AssertTestResultIsSkipped(skips);
            skips.ErrorMessage.Should().Be("no GPU available");
            TestResult blocks = ResultOf(results, "Glue::Blocks");
            TestResultAssertions.AssertTestResultIsFailure(blocks, "Blocked: device missing");
            blocks.Output.Should().Be("checking...TestBlocked: device missing");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReportLine_ErrorWithFileNameInvalidInPaths_TestFailsWithStackTrace()
        {
            // console output converted from a WTT log (no function) of Log::Error(L"generated failure", L"<generated>", L"", 1)
            IList<TestResult> results = Parse(LabDll, new[]
                {
                    "",
                    "StartGroup: Gen::EmptyFunction",
                    "Error: generated failure [File: <generated>, Function: , Line: 1]",
                    "EndGroup: Gen::EmptyFunction [Failed]",
                    "",
                    "StartGroup: Gen::Passes",
                    "EndGroup: Gen::Passes [Passed]"
                },
                "Gen::EmptyFunction", "Gen::Passes");

            TestResult failed = ResultOf(results, "Gen::EmptyFunction");
            TestResultAssertions.AssertTestResultIsFailure(failed, "generated failure");
            failed.ErrorStackTrace.Should().Be(ErrorMessageParser.CreateStackTraceEntry("<generated>:1", "<generated>", "1"));
            TestResultAssertions.AssertTestResultIsPassed(ResultOf(results, "Gen::Passes"));
        }

        #endregion

    }

}
