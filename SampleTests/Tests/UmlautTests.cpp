// Non-ASCII test, class and namespace names, properties and data (the source is UTF-8, compiled with /utf-8).
#include "WexTestClass.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;
using WEX::Common::String;

namespace TaefSamples
{
    class Ümlautß
    {
        TEST_CLASS(Ümlautß)

        TEST_METHOD(Täst)
        {
            VERIFY_ARE_EQUAL(1, 2, L"Ümlautß::Täst");
        }

        BEGIN_TEST_METHOD(Träits)
            TEST_METHOD_PROPERTY(L"Träit1", L"Völue1a")
            TEST_METHOD_PROPERTY(L"Träit1", L"Völue1b")
            TEST_METHOD_PROPERTY(L"Träit2", L"Völue2")
        END_TEST_METHOD()
    };

    void Ümlautß::Träits()
    {
        Log::Comment(L"Ümlaut output: äöü ÄÖÜ ß 名前 ✓");
    }


    namespace Nämespace
    {
        class KlässWithSetüp
        {
            TEST_CLASS(KlässWithSetüp)

            int m_välue = 0;

            TEST_METHOD_SETUP(Setüp)
            {
                m_välue = 1;
                return true;
            }

            TEST_METHOD(Täst)
            {
                VERIFY_ARE_EQUAL(2, m_välue, L"TaefSamples::Nämespace::KlässWithSetüp::Täst");
            }

            BEGIN_TEST_METHOD(Träits)
                TEST_METHOD_PROPERTY(L"Träit1", L"Völue1a")
                TEST_METHOD_PROPERTY(L"Träit1", L"Völue1b")
                TEST_METHOD_PROPERTY(L"Träit2", L"Völue2")
            END_TEST_METHOD()
        };

        void KlässWithSetüp::Träits()
        {
            VERIFY_ARE_EQUAL(1, m_välue, L"TaefSamples::Nämespace::KlässWithSetüp::Träits");
        }
    }


    class DataDrivenTästs
    {
        TEST_CLASS(DataDrivenTästs)

        BEGIN_TEST_METHOD(Täst)
            TEST_METHOD_PROPERTY(L"Data:Päräm", L"{ÄÖÜäöüß}")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(Träits)
            TEST_METHOD_PROPERTY(L"Data:Päräm", L"{ÄÖÜäöüß}")
            TEST_METHOD_PROPERTY(L"Träit1", L"Völue1a")
            TEST_METHOD_PROPERTY(L"Träit1", L"Völue1b")
            TEST_METHOD_PROPERTY(L"Träit2", L"Völue2")
        END_TEST_METHOD()
    };

    void DataDrivenTästs::Täst()
    {
        String päräm;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Päräm", päräm));
        VERIFY_ARE_EQUAL(String(L"ÄÖÜäöüß"), päräm);
    }

    void DataDrivenTästs::Träits()
    {
        String päräm;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Päräm", päräm));
        VERIFY_ARE_EQUAL(String(L"äöüßÄÖÜ"), päräm);
    }


    class TheInterface
    {
    public:
        virtual ~TheInterface() = default;
        virtual int GetValue(int i) = 0;
    };

    class ImplementationA : public TheInterface
    {
    public:
        int GetValue(int i) override { return i + 1; }
    };

    class ImplementationB : public TheInterface
    {
    public:
        int GetValue(int i) override { return i + 2; }
    };

    template <typename TImplementation>
    class ÜmlautTemplateTests
    {
        TEST_CLASS(ÜmlautTemplateTests)

        TEST_METHOD(Täst)
        {
            TImplementation theInstance;
            VERIFY_ARE_EQUAL(2, theInstance.GetValue(1));
        }
    };

    template class ÜmlautTemplateTests<ImplementationA>;
    template class ÜmlautTemplateTests<ImplementationB>;
}
