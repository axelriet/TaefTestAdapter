// Helpers used by the sample tests. Failing VERIFY_* macros report the location of the macro itself, so
// failures inside these helpers point to Helpers.h or Helpers.cpp and not to the calling test method.
#pragma once

#include <cwchar>
#include "WexTestClass.h"

namespace TaefSamples
{
    // VERIFY in another translation unit (Helpers.cpp)
    void CheckIfZeroInOtherFile(int i);

    // Log::Error with explicit source information (WEX_LOGGER_CURRENT_SOURCE_INFO) in another translation unit
    void LogErrorWithSourceInfoInOtherFile();

    // VERIFY in a header-inline function
    inline void CheckIfZeroInHeader(int i)
    {
        VERIFY_ARE_EQUAL(0, i, L"CheckIfZeroInHeader");
    }

    // RAII helper which logs a message with its source location when a scope is entered and left (Log::Comment),
    // so the context of a failure shows up in the test output around it.
    class LogScope
    {
    public:
        LogScope(const wchar_t* message, const wchar_t* file, int line)
            : m_message(message)
        {
            const wchar_t* fileName = wcsrchr(file, L'\\');
            WEX::Logging::Log::Comment(WEX::Common::String().Format(
                L"Scope: %s (%s:%d)", message, fileName != nullptr ? fileName + 1 : file, line));
        }

        ~LogScope()
        {
            WEX::Logging::Log::Comment(WEX::Common::String().Format(L"End of scope: %s", m_message));
        }

        LogScope(const LogScope&) = delete;
        LogScope& operator=(const LogScope&) = delete;

    private:
        const wchar_t* m_message;
    };
}

#define SAMPLE_LOG_SCOPE_CONCAT_(a, b) a##b
#define SAMPLE_LOG_SCOPE_CONCAT(a, b) SAMPLE_LOG_SCOPE_CONCAT_(a, b)
#define LOG_SCOPE(message) ::TaefSamples::LogScope SAMPLE_LOG_SCOPE_CONCAT(logScope_, __LINE__)(message, __FILEW__, __LINE__)
