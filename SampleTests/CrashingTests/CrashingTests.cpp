// Tests which crash the test host process. By default TE.exe runs the tests out of process (TE.ProcessHost.exe):
// a crash only fails the crashing test ("The test host process was unexpectedly terminated with exit code ..."),
// TE.exe starts a new host process and continues with the remaining tests. With TE.exe /inproc a crash
// terminates TE.exe itself, so the tests after the crash are never run.
#include <windows.h>
#include <cstdlib>
#include "WexTestClass.h"
#include "../LibProject/Lib.h"

using namespace WEX::Logging;
using WEX::Common::String;

namespace TaefSamples
{
#pragma warning(push)
#pragma warning(disable: 4717) // recursive on all control paths, function will cause runtime stack overflow
    static int RecurseForever(volatile int* depth)
    {
        volatile char buffer[4096];
        buffer[0] = static_cast<char>(++*depth);
        return RecurseForever(depth) + buffer[0];
    }
#pragma warning(pop)

    // Crashes must not wait for user interaction (Windows Error Reporting dialogs) - the tests run unattended
    static void DisableCrashDialogs()
    {
        SetErrorMode(SetErrorMode(0) | SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
    }

    class Crashing
    {
        TEST_CLASS(Crashing)

        // Output outside of the tests; the cleanup is skipped after a crash ("TestSkipped: TAEF: The cleanup method ...")
        TEST_CLASS_SETUP(ClassSetup)
        {
            Log::Comment(L"[Crashing] class setup");
            return true;
        }

        TEST_CLASS_CLEANUP(ClassCleanup)
        {
            Log::Comment(L"[Crashing] class cleanup");
            return true;
        }

        TEST_METHOD(AddFailsBeforeCrash)
        {
            VERIFY_ARE_EQUAL(1000, Add(10, 10));
        }

        TEST_METHOD(AddPassesBeforeCrash)
        {
            VERIFY_ARE_EQUAL(20, Add(10, 10));
        }

        // access violation (exit code 0xC0000005)
        TEST_METHOD(TheCrash)
        {
            Log::Comment(L"About to dereference a null pointer");
            DisableCrashDialogs();
            volatile int* nullPointer = nullptr;
            *nullPointer = Add(10, 10);
        }

        TEST_METHOD(AddFailsAfterCrash)
        {
            VERIFY_ARE_EQUAL(1000, Add(10, 10));
        }

        TEST_METHOD(AddPassesAfterCrash)
        {
            VERIFY_ARE_EQUAL(20, Add(10, 10));
        }

        TEST_METHOD(LongRunning)
        {
            Sleep(2000);
            VERIFY_ARE_EQUAL(1, 1);
        }

        // abort() (exit code 3); the abort message box and Windows Error Reporting are switched off so that the
        // Debug CRT does not wait for user interaction
        TEST_METHOD(TheAbort)
        {
            Log::Comment(L"About to call abort()");
            DisableCrashDialogs();
            _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
            abort();
        }

        // stack overflow (exit code 0xC00000FD)
        TEST_METHOD(TheStackOverflow)
        {
            Log::Comment(L"About to overflow the stack");
            DisableCrashDialogs();
            volatile int depth = 0;
            Log::Comment(String().Format(L"never reached: %d", RecurseForever(&depth)));
        }

        TEST_METHOD(AddPassesAfterAllCrashes)
        {
            VERIFY_ARE_EQUAL(20, Add(10, 10), L"AddPassesAfterAllCrashes");
        }
    };
}
