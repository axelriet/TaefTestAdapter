// Tests in a TAEF DLL which depends on another DLL (DllProject.dll) at runtime. TE.exe finds DllProject.dll in
// the folder of the test DLL or on the PATH (adapter setting PathExtension), but not in the working directory.
#include "WexTestClass.h"
#include "../DllProject/DllProject.h"

namespace TaefSamples
{
    class Passing
    {
        TEST_CLASS(Passing)

        TEST_METHOD(InvokeFunction)
        {
            VERIFY_ARE_EQUAL(0, ReturnZero());
        }
    };

    class Failing
    {
        TEST_CLASS(Failing)

        TEST_METHOD(InvokeFunction)
        {
            VERIFY_ARE_EQUAL(1, ReturnZero());
        }
    };
}
