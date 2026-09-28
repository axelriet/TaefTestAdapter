// TAEF tests in a mixed-mode DLL: the test code is native, the static library ClrLibProject is compiled with /clr
// and calls the C# library ClrDotNetLibProject.dll. TE.exe treats the DLL as a native TAEF test DLL; the CLR is
// loaded into the test host process when the managed code is called for the first time.
//
// The CLR resolves assembly references relative to the host process (TE.ProcessHost.exe), not relative to the
// test DLL, so the module setup registers an AssemblyResolve handler for the test DLL's folder.
#include "WexTestClass.h"
#include "../ClrLibProject/ClrLibProject.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;
using WEX::Common::String;

MODULE_SETUP(ClrTestsModuleSetup)
{
    String testDeploymentDir;
    VERIFY_SUCCEEDED(RuntimeParameters::TryGetValue(L"TestDeploymentDir", testDeploymentDir));
    ClrLibProject::ResolveManagedAssembliesFrom(testDeploymentDir);
    return true;
}

namespace TaefSamples
{
    class ClrTests
    {
        TEST_CLASS(ClrTests)

        TEST_METHOD(Pass)
        {
            ClrLibProject::ClrClass instance;
            VERIFY_ARE_EQUAL(2, instance.Add(1, 1));
        }

        TEST_METHOD(Fail)
        {
            ClrLibProject::ClrClass instance;
            VERIFY_ARE_EQUAL(3, instance.Add(1, 1));
        }
    };
}
