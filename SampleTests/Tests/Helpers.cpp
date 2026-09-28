#include "Helpers.h"

using namespace WEX::Logging;

namespace TaefSamples
{
    void CheckIfZeroInOtherFile(int i)
    {
        VERIFY_ARE_EQUAL(0, i, L"CheckIfZeroInOtherFile");
    }

    void LogErrorWithSourceInfoInOtherFile()
    {
        Log::Error(L"Log::Error with source information", WEX_LOGGER_CURRENT_SOURCE_INFO);
    }
}
