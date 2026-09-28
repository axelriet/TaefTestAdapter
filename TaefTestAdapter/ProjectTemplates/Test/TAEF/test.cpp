// $projectname$ - native tests for the Test Authoring and Execution Framework (TAEF).
//
// Build the project, then run the tests
//  - from the Test Explorer (Test Adapter for TAEF), or
//  - on the command line: TE.exe "$projectname$.dll"
// F5 debugs the tests: by default, it starts 'TE.exe <test dll> /inproc'. Command and arguments can be
// changed in Project Properties > Debugging; the Command Arguments must then contain the quoted test DLL
// (macro TargetPath) and /inproc, e.g. followed by /name:*Addition to debug only some tests.
//
// This project defines INLINE_TEST_METHOD_MARKUP, so TEST_METHOD and the fixture macros are
// followed by the body. Tests declared with BEGIN_TEST_METHOD are always defined outside of the class.

#include "WexTestClass.h"

using namespace WEX::Common;
using namespace WEX::Logging;
using namespace WEX::TestExecution;

namespace $taefnamespace$
{
    class SampleTests
    {
        // Makes this class a TAEF test class (a test class needs no base class).
        TEST_CLASS(SampleTests);

        // Runs once before the first test of this class. Returning false blocks the tests of the class.
        TEST_CLASS_SETUP(ClassSetup)
        {
            Log::Comment(L"SampleTests: class setup");
            return true;
        }

        // Runs once after the last test of this class.
        TEST_CLASS_CLEANUP(ClassCleanup)
        {
            Log::Comment(L"SampleTests: class cleanup");
            return true;
        }

        TEST_METHOD(Addition)
        {
            // A failing VERIFY_* macro logs an error (with file and line) and ends the test.
            VERIFY_ARE_EQUAL(4, 2 + 2);
            VERIFY_IS_TRUE(2 + 2 > 3, L"2 + 2 should be greater than 3");
        }

        TEST_METHOD(Strings)
        {
            // VERIFY_ARE_EQUAL compares the addresses of plain C strings;
            // use WEX::Common::String (or std::wstring) to compare their contents.
            String expected(L"TAEF");
            String actual(L"TA");
            actual.Append(L"EF");
            VERIFY_ARE_EQUAL(expected, actual);

            // Log::Comment output is shown as the test's output in the Test Explorer.
            Log::Comment(String().Format(L"Compared '%s' with '%s'", static_cast<const wchar_t*>(expected), static_cast<const wchar_t*>(actual)));
        }

        // Metadata (properties) of a test are shown as traits in the Test Explorer.
        BEGIN_TEST_METHOD(WithProperties)
            TEST_METHOD_PROPERTY(L"Priority", L"1")
            TEST_METHOD_PROPERTY(L"Category", L"Smoke")
        END_TEST_METHOD()

        // A lightweight data-driven test: it is run once per value, as
        // $taefnamespace$::SampleTests::DataDriven#metadataSet0, #metadataSet1 and #metadataSet2.
        BEGIN_TEST_METHOD(DataDriven)
            TEST_METHOD_PROPERTY(L"Data:Value", L"{1, 2, 3}")
        END_TEST_METHOD()
    };

    void SampleTests::WithProperties()
    {
        VERIFY_ARE_NOT_EQUAL(0, 1);
    }

    void SampleTests::DataDriven()
    {
        int value = 0;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Value", value));
        Log::Comment(String().Format(L"Value = %d", value));
        VERIFY_IS_GREATER_THAN(value, 0);
    }
}
