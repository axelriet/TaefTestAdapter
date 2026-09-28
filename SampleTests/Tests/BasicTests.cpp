// Basic TAEF tests: passing and failing VERIFY_* macros, test output, exceptions, explicit test results
// (Skipped/Blocked/NotRun via Log::Result), long running tests and a static library dependency (LibProject).
#include <windows.h>
#include <cstdio>
#include <iostream>
#include <stdexcept>
#include "WexTestClass.h"
#include "../LibProject/Lib.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;
using WEX::Common::String;

namespace TaefSamples
{
    class TestMath
    {
        TEST_CLASS(TestMath)

        TEST_METHOD(AddFails)
        {
            VERIFY_ARE_EQUAL(1000, Add(10, 10));
        }

        TEST_METHOD(AddPasses)
        {
            VERIFY_ARE_EQUAL(20, Add(10, 10));
        }

        BEGIN_TEST_METHOD(AddPassesWithTraits)
            TEST_METHOD_PROPERTY(L"Type", L"Medium")
        END_TEST_METHOD()
    };

    void TestMath::AddPassesWithTraits()
    {
        VERIFY_ARE_EQUAL(20, Add(10, 10), L"AddPassesWithTraits");
    }


    // Output of a test: out of process (TE.exe's default) only WEX::Logging output (Log::Comment, Log::Warning,
    // Log::Error, VERIFY_* messages) reaches TE.exe's console. printf/std::cout output of the test is only
    // visible if TE.exe runs the tests in its own process (/inproc).
    // DisableVerifyExceptions keeps failing VERIFY_* macros from throwing, so the test continues after a failure.
    class OutputHandling
    {
        TEST_CLASS(OutputHandling)

        TEST_METHOD(Output_ManyLinesWithNewlines)
        {
            DisableVerifyExceptions continueOnFailure;
            Log::Comment(L"before test 1\nbefore test 2\n");
            std::cout << "std::cout before test (visible with /inproc only)" << std::endl;
            VERIFY_ARE_EQUAL(1, 2, L"test output");
            Log::Comment(L"after test 1\nafter test 2\n");
            std::cout << "std::cout after test (visible with /inproc only)" << std::endl;
        }

        TEST_METHOD(Output_OneLineWithNewlines)
        {
            DisableVerifyExceptions continueOnFailure;
            Log::Comment(L"before test\n");
            VERIFY_ARE_EQUAL(1, 2, L"test output");
            Log::Comment(L"after test\n");
        }

        TEST_METHOD(Output_OneLine)
        {
            DisableVerifyExceptions continueOnFailure;
            Log::Comment(L"before test");
            printf("printf before test without newline (visible with /inproc only)");
            fflush(stdout);
            VERIFY_ARE_EQUAL(1, 2, L"test output");
            Log::Comment(L"after test");
        }

        TEST_METHOD(ManyLinesWithNewlines)
        {
            DisableVerifyExceptions continueOnFailure;
            Log::Comment(L"before test 1\nbefore test 2\n");
            VERIFY_ARE_EQUAL(1, 2);
            Log::Comment(L"after test 1\nafter test 2\n");
        }

        TEST_METHOD(OneLineWithNewlines)
        {
            DisableVerifyExceptions continueOnFailure;
            Log::Comment(L"before test\n");
            VERIFY_ARE_EQUAL(1, 2);
            Log::Comment(L"after test\n");
        }

        TEST_METHOD(OneLine)
        {
            DisableVerifyExceptions continueOnFailure;
            Log::Comment(L"before test");
            VERIFY_ARE_EQUAL(1, 2);
            Log::Comment(L"after test");
        }

        TEST_METHOD(OutputOfPassingTest)
        {
            Log::Comment(L"Log::Comment output");
            Log::Comment(L"Log::Comment output with a context", L"MyContext");
            Log::Warning(L"Log::Warning output (does not fail the test)");
            printf("printf output (visible with /inproc only)\n");
            fflush(stdout);
            std::cerr << "std::cerr output (visible with /inproc only)" << std::endl;
            VERIFY_IS_TRUE(true, L"OutputOfPassingTest");
        }
    };


    // Class names which are suffixes of each other: selecting class bcd ('TaefSamples::bcd::*') must not select
    // abcd::t or bbcd::t
    class abcd
    {
        TEST_CLASS(abcd)

        TEST_METHOD(t)
        {
            VERIFY_ARE_EQUAL(1, 1, L"abcd::t");
        }
    };

    class bbcd
    {
        TEST_CLASS(bbcd)

        TEST_METHOD(t)
        {
            VERIFY_ARE_EQUAL(1, 1, L"bbcd::t");
        }
    };

    class bcd
    {
        TEST_CLASS(bcd)

        TEST_METHOD(t)
        {
            VERIFY_ARE_EQUAL(1, 1, L"bcd::t");
        }
    };


    // TAEF catches C++ exceptions thrown by a test and reports the test as Failed ("Error: Caught ...")
    class Exceptions
    {
        TEST_CLASS(Exceptions)

        static void ThrowOutOfRange()
        {
            throw std::out_of_range("index out of range");
        }

        TEST_METHOD(ThrowsStdException)
        {
            throw std::runtime_error("std::runtime_error thrown by the test");
        }

        TEST_METHOD(ThrowsInt)
        {
            throw 42;
        }

        TEST_METHOD(ThrowsWexException)
        {
            WEX::Common::Throw::Exception(E_INVALIDARG, L"WEX::Common::Exception thrown by the test");
        }

        TEST_METHOD(VerifyThrowsPasses)
        {
            VERIFY_THROWS(ThrowOutOfRange(), std::out_of_range);
        }

        TEST_METHOD(VerifyNoThrowFails)
        {
            VERIFY_NO_THROW(ThrowOutOfRange());
        }
    };


    // Test results set explicitly by the test (Log::Result); Log::Error and failing VERIFYs result in Failed
    class ExplicitResults
    {
        TEST_CLASS(ExplicitResults)

        TEST_METHOD(SkippedByTest)
        {
            Log::Comment(L"This test decides at runtime that it cannot run on this machine");
            Log::Result(TestResults::Skipped, L"Skipped by the test");
        }

        TEST_METHOD(BlockedByTest)
        {
            Log::Result(TestResults::Blocked, L"Blocked by the test: a prerequisite is missing");
        }

        TEST_METHOD(NotRunByTest)
        {
            Log::Result(TestResults::NotRun, L"NotRun set by the test");
        }

        TEST_METHOD(FailedByLogResult)
        {
            Log::Result(TestResults::Failed, L"Failed set by the test");
        }

        TEST_METHOD(FailedByLogError)
        {
            Log::Error(L"Log::Error marks the test as failed");
        }

        TEST_METHOD(PassedWithWarning)
        {
            Log::Warning(L"Only a warning - the test passes");
        }
    };


    // Long running tests (e.g. for cancellation, test durations and parallel execution)
    class LongRunning
    {
        TEST_CLASS(LongRunning)

        TEST_METHOD(Sleeps1Second)
        {
            Sleep(1000);
            VERIFY_IS_TRUE(true, L"Sleeps1Second");
        }

        TEST_METHOD(Sleeps2SecondsAndFails)
        {
            Sleep(2000);
            VERIFY_ARE_EQUAL(1, 2, L"Sleeps2SecondsAndFails");
        }
    };
}
