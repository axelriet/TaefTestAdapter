// Test classes in named, nested and anonymous namespaces below the root namespace TaefSamples of all sample tests.
// TAEF test names contain the namespaces (TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_Named_Named::Test);
// anonymous namespaces are named `anonymous-namespace' (__FUNCTION__ style, including the backtick and the single
// quote), e.g. TaefSamples::`anonymous-namespace'::Namespace_Anon::Test.
#include "WexTestClass.h"

using namespace WEX::Logging;

namespace TaefSamples
{
    namespace Namespace_1
    {
        class Namespace_Named
        {
            TEST_CLASS(Namespace_Named)

            TEST_METHOD(Test)
            {
                VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::Namespace_1::Namespace_Named::Test");
            }
        };

        namespace Namespace_2_Nested
        {
            class Namespace_Named_Named
            {
                TEST_CLASS(Namespace_Named_Named)

                TEST_METHOD(Test)
                {
                    VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_Named_Named::Test");
                }
            };

            namespace Namespace_3_Deeply::Nested
            {
                class Namespace_Deep
                {
                    TEST_CLASS(Namespace_Deep)

                    TEST_METHOD(Fails)
                    {
                        VERIFY_ARE_EQUAL(1, 2, L"TaefSamples::Namespace_1::Namespace_2_Nested::Namespace_3_Deeply::Nested::Namespace_Deep::Fails");
                    }
                };
            }
        }

        namespace
        {
            class Namespace_Named_Anon
            {
                TEST_CLASS(Namespace_Named_Anon)

                TEST_METHOD(Test)
                {
                    VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::Namespace_1::(anonymous)::Namespace_Named_Anon::Test");
                }
            };
        }
    }

    namespace
    {
        class Namespace_Anon
        {
            TEST_CLASS(Namespace_Anon)

            TEST_METHOD(Test)
            {
                VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::(anonymous)::Namespace_Anon::Test");
            }
        };

        namespace
        {
            class Namespace_Anon_Anon
            {
                TEST_CLASS(Namespace_Anon_Anon)

                TEST_METHOD(Test)
                {
                    VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::(anonymous)::(anonymous)::Namespace_Anon_Anon::Test");
                }
            };
        }

        namespace Anon_Nested
        {
            class Namespace_Anon_Named
            {
                TEST_CLASS(Namespace_Anon_Named)

                TEST_METHOD(Test)
                {
                    VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::(anonymous)::Anon_Nested::Namespace_Anon_Named::Test");
                }
            };
        }
    }

    // directly in the root namespace TaefSamples (no nested namespace)
    class Namespace_Root
    {
        TEST_CLASS(Namespace_Root)

        TEST_METHOD(Test)
        {
            VERIFY_ARE_EQUAL(1, 1, L"TaefSamples::Namespace_Root::Test");
        }
    };
}
