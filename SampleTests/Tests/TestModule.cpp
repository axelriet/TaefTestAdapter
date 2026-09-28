// Module level metadata and fixtures of Tests_taef.dll.
//
// MODULE_PROPERTY values are inherited by every test of the DLL; class properties (TEST_CLASS_PROPERTY) and
// method properties (TEST_METHOD_PROPERTY) override them. MODULE_SETUP/MODULE_CLEANUP run once per test host
// process (TE.exe starts a new host after a crash and runs the module setup again).
#include "WexTestClass.h"

using namespace WEX::Logging;

BEGIN_MODULE()
    MODULE_PROPERTY(L"Component", L"SampleTests")
    MODULE_PROPERTY(L"Owner", L"ModuleOwner")
END_MODULE()

MODULE_SETUP(TestsModuleSetup)
{
    Log::Comment(L"[Tests] module setup");
    return true;
}

MODULE_CLEANUP(TestsModuleCleanup)
{
    Log::Comment(L"[Tests] module cleanup");
    return true;
}
