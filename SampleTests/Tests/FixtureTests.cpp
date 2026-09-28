// Fixtures: TEST_CLASS_SETUP/TEST_CLASS_CLEANUP run once per class, TEST_METHOD_SETUP/TEST_METHOD_CLEANUP around
// every test. TAEF creates ONE instance per test class, so per-test state must be reset in the method setup.
//
// Failing fixtures:
//   - setup returns false          -> the affected tests are Blocked
//   - VERIFY fails (or throws) in a setup -> the affected tests are Failed
//   - cleanup fails                -> the result of the test does not change (TE.exe reports the failure after the test)
#include "WexTestClass.h"
#include "../LibProject/Lib.h"

using namespace WEX::Logging;
using WEX::Common::String;

namespace TaefSamples
{
    class ClassWithFixtures
    {
        TEST_CLASS(ClassWithFixtures)

        int m_summand = 0;
        int m_classSetupCalls = 0;

        TEST_CLASS_SETUP(ClassSetup)
        {
            ++m_classSetupCalls;
            Log::Comment(L"[ClassWithFixtures] class setup");
            return true;
        }

        TEST_CLASS_CLEANUP(ClassCleanup)
        {
            Log::Comment(L"[ClassWithFixtures] class cleanup");
            return true;
        }

        TEST_METHOD_SETUP(MethodSetup)
        {
            m_summand = 10;
            Log::Comment(L"[ClassWithFixtures] method setup");
            return true;
        }

        TEST_METHOD_CLEANUP(MethodCleanup)
        {
            m_summand = -1;
            Log::Comment(L"[ClassWithFixtures] method cleanup");
            return true;
        }

        TEST_METHOD(AddFails)
        {
            VERIFY_ARE_EQUAL(1000, Add(m_summand, 10));
        }

        TEST_METHOD(AddPasses)
        {
            VERIFY_ARE_EQUAL(20, Add(m_summand, 10));
        }

        BEGIN_TEST_METHOD(AddPassesWithTraits)
            TEST_METHOD_PROPERTY(L"Type", L"Small")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(AddPassesWithTraits2)
            TEST_METHOD_PROPERTY(L"Type", L"Small")
            TEST_METHOD_PROPERTY(L"Author", L"Alice")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(AddPassesWithTraits3)
            TEST_METHOD_PROPERTY(L"Type", L"Small")
            TEST_METHOD_PROPERTY(L"Author", L"Alice")
            TEST_METHOD_PROPERTY(L"TestCategory", L"Integration")
        END_TEST_METHOD()

        TEST_METHOD(SetupRunsBeforeEachTest)
        {
            VERIFY_ARE_EQUAL(10, m_summand, L"method setup resets the state");
            VERIFY_ARE_EQUAL(1, m_classSetupCalls, L"class setup runs once");
        }
    };

    void ClassWithFixtures::AddPassesWithTraits()
    {
        VERIFY_ARE_EQUAL(20, Add(m_summand, 10), L"AddPassesWithTraits");
    }

    void ClassWithFixtures::AddPassesWithTraits2()
    {
        VERIFY_ARE_EQUAL(20, Add(m_summand, 10), L"AddPassesWithTraits2");
    }

    void ClassWithFixtures::AddPassesWithTraits3()
    {
        VERIFY_ARE_EQUAL(20, Add(m_summand, 10), L"AddPassesWithTraits3");
    }


    class FailingClassSetup
    {
        TEST_CLASS(FailingClassSetup)

        TEST_CLASS_SETUP(ClassSetup)
        {
            Log::Comment(L"[FailingClassSetup] class setup returns false");
            return false;
        }

        TEST_METHOD(FirstTest)
        {
            Log::Comment(L"FailingClassSetup::FirstTest is never executed");
        }

        TEST_METHOD(SecondTest)
        {
            Log::Comment(L"FailingClassSetup::SecondTest is never executed");
        }
    };


    class FailingMethodSetup
    {
        TEST_CLASS(FailingMethodSetup)

        TEST_METHOD_SETUP(MethodSetup)
        {
            Log::Comment(L"[FailingMethodSetup] method setup returns false");
            return false;
        }

        TEST_METHOD(Test)
        {
            Log::Comment(L"FailingMethodSetup::Test is never executed");
        }
    };


    class VerifyInClassSetup
    {
        TEST_CLASS(VerifyInClassSetup)

        TEST_CLASS_SETUP(ClassSetup)
        {
            VERIFY_ARE_EQUAL(5, 6, L"VERIFY failing in TEST_CLASS_SETUP");
            return true;
        }

        TEST_METHOD(Test)
        {
            Log::Comment(L"VerifyInClassSetup::Test is never executed");
        }
    };


    class VerifyInMethodSetup
    {
        TEST_CLASS(VerifyInMethodSetup)

        TEST_METHOD_SETUP(MethodSetup)
        {
            VERIFY_ARE_EQUAL(7, 8, L"VERIFY failing in TEST_METHOD_SETUP");
            return true;
        }

        TEST_METHOD(Test)
        {
            Log::Comment(L"VerifyInMethodSetup::Test is never executed");
        }
    };


    class FailingMethodCleanup
    {
        TEST_CLASS(FailingMethodCleanup)

        TEST_METHOD_CLEANUP(MethodCleanup)
        {
            Log::Comment(L"[FailingMethodCleanup] method cleanup returns false");
            return false;
        }

        TEST_METHOD(TestPassesAlthoughCleanupFails)
        {
            VERIFY_IS_TRUE(true, L"TestPassesAlthoughCleanupFails");
        }
    };
}
