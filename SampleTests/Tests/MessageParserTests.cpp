// Failing tests whose error messages point to different source locations: the test method, a helper method in
// the same file, a helper in another .cpp file (Helpers.cpp) and a header-inline helper (Helpers.h). TE.exe
// reports every failing VERIFY/Log::Error as "Error: <message> [File: <file>, Function: <function>, Line: <n>]".
// LOG_SCOPE (Helpers.h) logs entering and leaving a scope with Log::Comment.
#include "WexTestClass.h"
#include "Helpers.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;

namespace TaefSamples
{
    static void CheckIfZero(int i)
    {
        VERIFY_ARE_EQUAL(0, i);
    }

    static void HelperMethodWithLogScope()
    {
        LOG_SCOPE(L"HelperMethod");
        CheckIfZero(1);
    }

    class MessageParserTests
    {
        TEST_CLASS(MessageParserTests)

        // VERIFY_* throws on failure (the test ends) ...
        TEST_METHOD(VerifyThrows)
        {
            VERIFY_ARE_EQUAL(1, 2);
        }

        // ... unless DisableVerifyExceptions is in scope (the test continues)
        TEST_METHOD(VerifyWithoutExceptions)
        {
            DisableVerifyExceptions continueOnFailure;
            VERIFY_ARE_EQUAL(2, 3);
        }

        TEST_METHOD(VerifyWithoutAndWithExceptions)
        {
            {
                DisableVerifyExceptions continueOnFailure;
                VERIFY_ARE_EQUAL(3, 4);
            }
            VERIFY_ARE_EQUAL(4, 5);
        }

        TEST_METHOD(VerifyInOtherMethod)
        {
            CheckIfZero(1);
        }

        TEST_METHOD(VerifyInOtherFile)
        {
            CheckIfZeroInOtherFile(1);
        }

        TEST_METHOD(VerifyInHeader)
        {
            CheckIfZeroInHeader(1);
        }

        TEST_METHOD(VerifyInTestAndMethodAndOtherFile)
        {
            DisableVerifyExceptions continueOnFailure;
            VERIFY_ARE_EQUAL(5, 6);
            CheckIfZero(1);
            CheckIfZeroInOtherFile(1);
        }

        TEST_METHOD(LogScopeInTestMethod)
        {
            LOG_SCOPE(L"TestMethod");
            CheckIfZero(1);
        }

        TEST_METHOD(TwoLogScopesInTestMethod)
        {
            LOG_SCOPE(L"TestMethod Outer");
            {
                LOG_SCOPE(L"TestMethod Inner");
                CheckIfZero(1);
            }
        }

        TEST_METHOD(LogScopeInHelperMethod)
        {
            HelperMethodWithLogScope();
        }

        TEST_METHOD(LogScopeInTestMethodAndHelperMethod)
        {
            LOG_SCOPE(L"TestMethod");
            HelperMethodWithLogScope();
        }

        TEST_METHOD(LogScopeInTestMethodAndHelperMethodAndVerifyInTestMethod)
        {
            DisableVerifyExceptions continueOnFailure;
            LOG_SCOPE(L"TestMethod");
            HelperMethodWithLogScope();
            VERIFY_ARE_EQUAL(0, 1);
        }

        TEST_METHOD(LogErrorWithSourceInfo)
        {
            LogErrorWithSourceInfoInOtherFile();
            Log::Error(L"Log::Error without source information");
        }
    };
}
