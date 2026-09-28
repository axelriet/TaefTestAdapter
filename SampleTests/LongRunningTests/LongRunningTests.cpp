// Two tests running for 2 seconds each (parallel execution, cancellation and test duration scenarios)
#include <windows.h>
#include "WexTestClass.h"

namespace TaefSamples
{
    class LongRunningTests
    {
        TEST_CLASS(LongRunningTests)

        TEST_METHOD(Test1)
        {
            Sleep(2000);
            VERIFY_ARE_EQUAL(1, 1);
        }

        TEST_METHOD(Test2)
        {
            Sleep(2000);
            VERIFY_ARE_EQUAL(1, 2);
        }
    };
}
