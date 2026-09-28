// $fileinputname$.cpp - native tests for the Test Authoring and Execution Framework (TAEF).
//
// The project must be a DLL that can find the TAEF headers and import libraries, e.g.
//   include path: $(WindowsSdkDir)Testing\Development\inc
//   library path: $(WindowsSdkDir)Testing\Development\lib\<x86|x64|arm64>
//   libraries:    TE.Common.lib;Wex.Common.lib;Wex.Logger.lib
// (the 'TAEF Test Project' template is set up like this). Run the tests with the Test Explorer
// (Test Adapter for TAEF) or on the command line with 'TE.exe <test dll>'.
//
// The test class is declared in a namespace derived from the project's RootNamespace: the Test
// Explorer groups the tests by namespace and class, e.g.
// $taefnamespace$ > $taefclassname$ > Addition.
//
// The test methods and fixtures below are declared in the class and defined outside of it, so this
// file compiles with and without INLINE_TEST_METHOD_MARKUP.

#include "WexTestClass.h"

using namespace WEX::Common;
using namespace WEX::Logging;
using namespace WEX::TestExecution;

namespace $taefnamespace$
{
    class $taefclassname$
    {
        // Makes this class a TAEF test class (a test class needs no base class).
        TEST_CLASS($taefclassname$);

        // Fixtures have the signature 'bool Fixture()'. A setup fixture returning false blocks the
        // affected tests; a failing VERIFY_* or an exception in a setup fixture fails them.
        TEST_CLASS_SETUP(ClassSetup);       // once, before the first test of this class
        TEST_CLASS_CLEANUP(ClassCleanup);   // once, after the last test of this class
        TEST_METHOD_SETUP(MethodSetup);     // before each test of this class
        TEST_METHOD_CLEANUP(MethodCleanup); // after each test of this class

        // Test methods have the signature 'void Method()'.
        TEST_METHOD(Addition);
        TEST_METHOD(Strings);

        // Metadata (properties) of a test are shown as traits in the Test Explorer.
        BEGIN_TEST_METHOD(WithProperties)
            TEST_METHOD_PROPERTY(L"Priority", L"1")
            TEST_METHOD_PROPERTY(L"Category", L"Smoke")
        END_TEST_METHOD()

        // A lightweight data-driven test: it is run once per value, as
        // $taefnamespace$::$taefclassname$::DataDriven#metadataSet0, #metadataSet1 and #metadataSet2.
        BEGIN_TEST_METHOD(DataDriven)
            TEST_METHOD_PROPERTY(L"Data:Value", L"{1, 2, 3}")
        END_TEST_METHOD()
    };

    bool $taefclassname$::ClassSetup()
    {
        Log::Comment(L"$taefclassname$: class setup");
        return true;
    }

    bool $taefclassname$::ClassCleanup()
    {
        Log::Comment(L"$taefclassname$: class cleanup");
        return true;
    }

    bool $taefclassname$::MethodSetup()
    {
        return true;
    }

    bool $taefclassname$::MethodCleanup()
    {
        return true;
    }

    void $taefclassname$::Addition()
    {
        // A failing VERIFY_* macro logs an error (with file and line) and ends the test.
        VERIFY_ARE_EQUAL(4, 2 + 2);
        VERIFY_IS_TRUE(2 + 2 > 3, L"2 + 2 should be greater than 3");
        VERIFY_IS_LESS_THAN(2 + 2, 5);
    }

    void $taefclassname$::Strings()
    {
        // Note: VERIFY_ARE_EQUAL compares the addresses of plain C strings;
        // use WEX::Common::String (or std::wstring) to compare their contents.
        String expected(L"TAEF");
        String actual(L"TA");
        actual.Append(L"EF");
        VERIFY_ARE_EQUAL(expected, actual);

        // Log::Comment output is shown as the test's output in the Test Explorer.
        Log::Comment(String().Format(L"Compared '%s' with '%s'", static_cast<const wchar_t*>(expected), static_cast<const wchar_t*>(actual)));
    }

    void $taefclassname$::WithProperties()
    {
        Log::Comment(L"Properties of a test can be used to select tests (Test Explorer traits, TE.exe /select).");
        VERIFY_SUCCEEDED(S_OK);
    }

    void $taefclassname$::DataDriven()
    {
        int value = 0;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Value", value));
        Log::Comment(String().Format(L"Value = %d", value));
        VERIFY_IS_GREATER_THAN(value, 0);
    }
}
