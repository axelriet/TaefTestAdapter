// Class templates: a test class template with explicit instantiations. Every
// instantiation is a test class of its own; its name is the compiler's __FUNCTION__ form of the class, e.g.
// TaefSamples::TemplateTests<class std::vector<int,class std::allocator<int> > >::CanIterate or
// TaefSamples::TemplateTests<struct TaefSamples::ReversedArray> (template arguments are fully qualified).
#include <array>
#include <initializer_list>
#include <list>
#include <vector>
#include "WexTestClass.h"

using namespace WEX::Logging;
using WEX::Common::String;

namespace TaefSamples
{
    struct ReversedArray : public std::array<int, 3>
    {
        ReversedArray(std::initializer_list<int>) : std::array<int, 3>{ { 3, 2, 1 } } {}
    };

    template <typename TIntContainer>
    class TemplateTests
    {
        TEST_CLASS(TemplateTests)

        TIntContainer m_container = TIntContainer{ 1, 2, 3 };

        // fails for containers holding 1, 2, 3 (1 + 2 == 3)
        void CheckCanDefeatMath(const wchar_t* testName)
        {
            VERIFY_ARE_NOT_EQUAL(m_container[0] + m_container[1], m_container[2], testName);
        }

        BEGIN_TEST_METHOD(CanIterate)
            TEST_METHOD_PROPERTY(L"Author", L"Bob")
        END_TEST_METHOD()

        TEST_METHOD(CanDefeatMath)
        {
            CheckCanDefeatMath(L"CanDefeatMath");
        }

        BEGIN_TEST_METHOD(TwoTraits)
            TEST_METHOD_PROPERTY(L"Author", L"Dave")
            TEST_METHOD_PROPERTY(L"TestCategory", L"Integration")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(ThreeTraits)
            TEST_METHOD_PROPERTY(L"Author", L"Dave")
            TEST_METHOD_PROPERTY(L"TestCategory", L"Integration")
            TEST_METHOD_PROPERTY(L"Class", L"Simple")
        END_TEST_METHOD()
    };

    template <typename TIntContainer>
    void TemplateTests<TIntContainer>::CanIterate()
    {
        int sum = 0;
        for (int value : m_container)
        {
            sum += value;
        }
        VERIFY_ARE_EQUAL(1 + 2 + 3, sum);
    }

    template <typename TIntContainer>
    void TemplateTests<TIntContainer>::TwoTraits()
    {
        CheckCanDefeatMath(L"TwoTraits");
    }

    template <typename TIntContainer>
    void TemplateTests<TIntContainer>::ThreeTraits()
    {
        CheckCanDefeatMath(L"ThreeTraits");
    }

    // TAEF only finds the tests of explicitly instantiated class templates
    template class TemplateTests<std::vector<int>>;
    template class TemplateTests<std::array<int, 3>>;
    template class TemplateTests<ReversedArray>;


    namespace ClassTemplates
    {
        template <typename TNumber>
        class NumberTemplateTests
        {
            TEST_CLASS(NumberTemplateTests)

            // 1 + 2 + 127 overflows a signed char
            TEST_METHOD(CanHoldLargeSum)
            {
                std::list<TNumber> numbers{ 1, 2, 127 };
                TNumber sum = 0;
                for (TNumber number : numbers)
                {
                    sum = static_cast<TNumber>(sum + number);
                }
                VERIFY_ARE_EQUAL(130, static_cast<int>(sum));
            }
        };

        template class NumberTemplateTests<signed char>;
        template class NumberTemplateTests<int>;
        template class NumberTemplateTests<long>;
    }
}
