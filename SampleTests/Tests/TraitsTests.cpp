// TAEF metadata (properties), which the adapter reports as traits:
//   - module properties (TestModule.cpp) < class properties (TEST_CLASS_PROPERTY) < method properties (TEST_METHOD_PROPERTY)
//   - a property declared several times is one property with several values (TE.exe prints them space separated)
//   - Ignore=true (method or class level): the test is only listed and run with TE.exe /runIgnoredTests
//   - Description, DataSource, Data:* and Metadata:Index are TAEF properties, but not traits
#include "WexTestClass.h"

using namespace WEX::Logging;

namespace TaefSamples
{
    class Traits
    {
        TEST_CLASS(Traits)

        BEGIN_TEST_METHOD(With8Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
            TEST_METHOD_PROPERTY(L"Trait3", L"Equals3")
            TEST_METHOD_PROPERTY(L"Trait4", L"Equals4")
            TEST_METHOD_PROPERTY(L"Trait5", L"Equals5")
            TEST_METHOD_PROPERTY(L"Trait6", L"Equals6")
            TEST_METHOD_PROPERTY(L"Trait7", L"Equals7")
            TEST_METHOD_PROPERTY(L"Trait8", L"Equals8")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With7Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
            TEST_METHOD_PROPERTY(L"Trait3", L"Equals3")
            TEST_METHOD_PROPERTY(L"Trait4", L"Equals4")
            TEST_METHOD_PROPERTY(L"Trait5", L"Equals5")
            TEST_METHOD_PROPERTY(L"Trait6", L"Equals6")
            TEST_METHOD_PROPERTY(L"Trait7", L"Equals7")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With6Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
            TEST_METHOD_PROPERTY(L"Trait3", L"Equals3")
            TEST_METHOD_PROPERTY(L"Trait4", L"Equals4")
            TEST_METHOD_PROPERTY(L"Trait5", L"Equals5")
            TEST_METHOD_PROPERTY(L"Trait6", L"Equals6")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With5Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
            TEST_METHOD_PROPERTY(L"Trait3", L"Equals3")
            TEST_METHOD_PROPERTY(L"Trait4", L"Equals4")
            TEST_METHOD_PROPERTY(L"Trait5", L"Equals5")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With4Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
            TEST_METHOD_PROPERTY(L"Trait3", L"Equals3")
            TEST_METHOD_PROPERTY(L"Trait4", L"Equals4")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With3Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
            TEST_METHOD_PROPERTY(L"Trait3", L"Equals3")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With2Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
            TEST_METHOD_PROPERTY(L"Trait2", L"Equals2")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(With1Traits)
            TEST_METHOD_PROPERTY(L"Trait1", L"Equals1")
        END_TEST_METHOD()

        // the same key twice: TE.exe reports one property "Author" with the value "Alice Bob"
        BEGIN_TEST_METHOD(WithEqualTraits)
            TEST_METHOD_PROPERTY(L"Author", L"Alice")
            TEST_METHOD_PROPERTY(L"Author", L"Bob")
        END_TEST_METHOD()
    };

    void Traits::With8Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With8Traits");
    }

    void Traits::With7Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With7Traits");
    }

    void Traits::With6Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With6Traits");
    }

    void Traits::With5Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With5Traits");
    }

    void Traits::With4Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With4Traits");
    }

    void Traits::With3Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With3Traits");
    }

    void Traits::With2Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With2Traits");
    }

    void Traits::With1Traits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"With1Traits");
    }

    void Traits::WithEqualTraits()
    {
        VERIFY_ARE_EQUAL(1, 1, L"WithEqualTraits");
    }


    class ClassAndMethodProperties
    {
        BEGIN_TEST_CLASS(ClassAndMethodProperties)
            TEST_CLASS_PROPERTY(L"Owner", L"ClassOwner")
            TEST_CLASS_PROPERTY(L"Category", L"ClassCategory")
            TEST_CLASS_PROPERTY(L"ClassTrait", L"ClassValue")
        END_TEST_CLASS()

        // Owner=ClassOwner, Category=ClassCategory, ClassTrait=ClassValue (+ module properties)
        TEST_METHOD(InheritsClassProperties)
        {
            VERIFY_IS_TRUE(true, L"InheritsClassProperties");
        }

        BEGIN_TEST_METHOD(OverridesClassProperties)
            TEST_METHOD_PROPERTY(L"Owner", L"MethodOwner")
            TEST_METHOD_PROPERTY(L"Category", L"MethodCategory")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(WithPriority1)
            TEST_METHOD_PROPERTY(L"Priority", L"1")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(WithPriority2AndFails)
            TEST_METHOD_PROPERTY(L"Priority", L"2")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(WithMultipleValues)
            TEST_METHOD_PROPERTY(L"Category", L"Smoke")
            TEST_METHOD_PROPERTY(L"Category", L"Nightly")
        END_TEST_METHOD()

        // custom properties; a value with spaces; Description is metadata but not a trait
        BEGIN_TEST_METHOD(WithCustomPropertiesAndFails)
            TEST_METHOD_PROPERTY(L"BugId", L"12345")
            TEST_METHOD_PROPERTY(L"Area", L"Area with spaces")
            TEST_METHOD_PROPERTY(L"Description", L"A test with custom properties")
        END_TEST_METHOD()

        // listed and executed only with TE.exe /runIgnoredTests (passes if run)
        BEGIN_TEST_METHOD(IgnoredTest)
            TEST_METHOD_PROPERTY(L"Ignore", L"true")
            TEST_METHOD_PROPERTY(L"Priority", L"1")
        END_TEST_METHOD()
    };

    void ClassAndMethodProperties::OverridesClassProperties()
    {
        VERIFY_IS_TRUE(true, L"OverridesClassProperties");
    }

    void ClassAndMethodProperties::WithPriority1()
    {
        VERIFY_IS_TRUE(true, L"WithPriority1");
    }

    void ClassAndMethodProperties::WithPriority2AndFails()
    {
        VERIFY_IS_TRUE(false, L"WithPriority2AndFails");
    }

    void ClassAndMethodProperties::WithMultipleValues()
    {
        VERIFY_IS_TRUE(true, L"WithMultipleValues");
    }

    void ClassAndMethodProperties::WithCustomPropertiesAndFails()
    {
        VERIFY_IS_TRUE(false, L"WithCustomPropertiesAndFails");
    }

    void ClassAndMethodProperties::IgnoredTest()
    {
        Log::Comment(L"IgnoredTest is only executed with /runIgnoredTests");
    }


    // Ignore=true at class level: all tests of the class are ignored
    class IgnoredClass
    {
        BEGIN_TEST_CLASS(IgnoredClass)
            TEST_CLASS_PROPERTY(L"Ignore", L"true")
        END_TEST_CLASS()

        TEST_METHOD(IgnoredPassing)
        {
            VERIFY_IS_TRUE(true, L"IgnoredPassing");
        }

        TEST_METHOD(IgnoredFailing)
        {
            VERIFY_IS_TRUE(false, L"IgnoredFailing");
        }
    };
}
