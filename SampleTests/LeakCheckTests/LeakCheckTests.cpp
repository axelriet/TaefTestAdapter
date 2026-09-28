// Memory leak detection with TAEF:
// every test takes a CRT debug heap checkpoint in the method setup and verifies at its end that all memory has been
// freed. The method cleanup checks again and logs leaks of tests which failed before they reached their own check;
// TAEF reports such cleanup errors after the test ("Error: TAEF: Cleanup fixture ... failed.") without changing the
// test's result.
//
// Expected results: Debug: Passing Passed, Failing Failed, PassingAndLeaking Failed (leak), FailingAndLeaking Failed
//                   Release: no leak detection, so PassingAndLeaking passes
#include <cstring>
#include "WexTestClass.h"
#include "LeakDetector.h"

using namespace WEX::Logging;
using WEX::Common::String;

namespace TaefSamples
{
    class MemoryLeaks
    {
        TEST_CLASS(MemoryLeaks)

        LeakDetector m_leakDetector;

        TEST_METHOD_SETUP(StartLeakDetection)
        {
            m_leakDetector.Start();
            return true;
        }

        TEST_METHOD_CLEANUP(CheckForLeaks)
        {
            size_t leakedBlocks = 0;
            size_t leakedBytes = m_leakDetector.GetLeakedBytes(leakedBlocks);
            if (leakedBytes > 0)
            {
                Log::Error(String().Format(L"memory leak detected after the test: %Iu bytes in %Iu blocks", leakedBytes, leakedBlocks));
            }
            return true;
        }

        TEST_METHOD(Passing)
        {
            {
                char* buffer = new char[100];
                strcpy_s(buffer, 100, "memory which is freed again");
                delete[] buffer;
            }
            VERIFY_IS_TRUE(true);
            m_leakDetector.VerifyNoMemoryLeaks();
        }

        TEST_METHOD(Failing)
        {
            Log::Comment(L"This test does not leak, but fails");
            VERIFY_IS_TRUE(false);
            m_leakDetector.VerifyNoMemoryLeaks();
        }

        TEST_METHOD(PassingAndLeaking)
        {
            char* leaked = new char[100];
            strcpy_s(leaked, 100, "leaked memory");
            Log::Comment(L"Leaking 100 chars...");
            VERIFY_IS_TRUE(true);
            m_leakDetector.VerifyNoMemoryLeaks();
        }

        TEST_METHOD(FailingAndLeaking)
        {
            char* leaked = new char[100];
            strcpy_s(leaked, 100, "leaked memory");
            Log::Comment(L"Leaking 100 chars...");
            VERIFY_IS_TRUE(false);
            m_leakDetector.VerifyNoMemoryLeaks();
        }
    };
}
