// The post-build step of this project writes HelperFileTests_taef.dll.taef_settings_helper next to the test DLL:
//   SolutionPath=...::TAEF::SolutionDir=...::TAEF::PlatformName=...::TAEF::ConfigurationName=...::TAEF::TheTarget=HelperFileTests_taef.dll::TAEF::TheWorkingDirectory=...
// The adapter makes every Key=Value pair available as placeholder $(Key). SampleTests.taef.runsettings passes
// /p:"TheTarget=$(TheTarget)" to TE.exe (AdditionalTestExecutionParam), so the test passes only if the helper file
// has been read.
#include "WexTestClass.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;
using WEX::Common::String;

namespace TaefSamples
{
    class HelperFileTests
    {
        TEST_CLASS(HelperFileTests)

        TEST_METHOD(TheTargetIsSet)
        {
            String theTarget;
            if (FAILED(RuntimeParameters::TryGetValue(L"TheTarget", theTarget)))
            {
                VERIFY_FAIL(L"Runtime parameter 'TheTarget' is not set: pass /p:\"TheTarget=$(TheTarget)\" to TE.exe "
                            L"(adapter setting AdditionalTestExecutionParam; the value comes from HelperFileTests_taef.dll.taef_settings_helper)");
            }
            VERIFY_ARE_EQUAL(String(L"HelperFileTests_taef.dll"), theTarget);
        }
    };
}
