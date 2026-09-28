// Tests which only pass if the adapter is configured by SampleTests.taef.runsettings:
//   - AdditionalTestExecutionParam  /p:"TestDirectory=$(TestDir)"  (TE.exe runtime parameter)
//   - WorkingDir                    $(SolutionDir)                  (TE.exe runs in the SampleTests folder)
//   - EnvironmentVariables          MYENVVAR=MyValue
#include <windows.h>
#include <cstdlib>
#include <cwchar>
#include <memory>
#include "WexTestClass.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;
using WEX::Common::String;

namespace TaefSamples
{
    // Runtime parameters are passed to TE.exe with /p:"Name=Value"
    class RuntimeParameterTests
    {
        TEST_CLASS(RuntimeParameterTests)

        TEST_METHOD(TestDirectoryIsSet)
        {
            String testDirectory;
            if (FAILED(RuntimeParameters::TryGetValue(L"TestDirectory", testDirectory)))
            {
                VERIFY_FAIL(L"Runtime parameter 'TestDirectory' is not set: pass /p:\"TestDirectory=$(TestDir)\" to TE.exe "
                            L"(adapter setting AdditionalTestExecutionParam, see SampleTests.taef.runsettings)");
            }

            Log::Comment(String().Format(L"TestDirectory=%s", static_cast<const wchar_t*>(testDirectory)));
            DWORD attributes = GetFileAttributesW(testDirectory);
            VERIFY_IS_TRUE(attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0,
                           L"runtime parameter TestDirectory must be an existing directory");
        }

        // Runtime parameters provided by TAEF itself
        TEST_METHOD(BuiltInParametersAreAvailable)
        {
            String fullTestName;
            VERIFY_SUCCEEDED(RuntimeParameters::TryGetValue(L"FullTestName", fullTestName));
            VERIFY_ARE_EQUAL(String(L"TaefSamples::RuntimeParameterTests::BuiltInParametersAreAvailable"), fullTestName);

            String deploymentDirectory;
            VERIFY_SUCCEEDED(RuntimeParameters::TryGetValue(L"TestDeploymentDir", deploymentDirectory));
            Log::Comment(String().Format(L"TestDeploymentDir=%s", static_cast<const wchar_t*>(deploymentDirectory)));
        }
    };

    class WorkingDir
    {
        TEST_CLASS(WorkingDir)

        TEST_METHOD(IsSolutionDirectory)
        {
            wchar_t workingDirectory[MAX_PATH + 1] = {};
            GetCurrentDirectoryW(MAX_PATH, workingDirectory);

            const wchar_t expectedEnd[] = L"SampleTests";
            size_t length = wcslen(workingDirectory);
            size_t endLength = wcslen(expectedEnd);
            bool endsWithSampleTests = length >= endLength && _wcsicmp(workingDirectory + length - endLength, expectedEnd) == 0;

            VERIFY_IS_TRUE(endsWithSampleTests, String().Format(L"working directory is %s", workingDirectory));
        }
    };

    class EnvironmentVariable
    {
        TEST_CLASS(EnvironmentVariable)

        TEST_METHOD(IsSet)
        {
            wchar_t* value = nullptr;
            size_t size = 0;
            VERIFY_ARE_EQUAL(0, _wdupenv_s(&value, &size, L"MYENVVAR"));
            std::unique_ptr<wchar_t, decltype(&free)> valueGuard(value, &free);

            VERIFY_IS_NOT_NULL(value, L"environment variable MYENVVAR is not set (adapter setting EnvironmentVariables: MYENVVAR=MyValue)");
            VERIFY_ARE_EQUAL(String(L"MyValue"), String(value));
        }
    };
}
