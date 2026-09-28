// Data-driven tests:
//   - table data sources (DataSource property, XML in DataDrivenTests.xml): Method#<row index> or Method#<row name>
//   - lightweight data (Data:<Name> properties): Method#metadataSet<N>; several Data: properties form a cross product
//   - a missing data source: TAEF reports the pseudo test Method#error, which is Blocked
#include "WexTestClass.h"

using namespace WEX::Logging;
using namespace WEX::TestExecution;
using WEX::Common::String;

#define SIMPLE_TABLE_DATASOURCE L"Table:DataDrivenTests.xml#SimpleTable"

namespace TaefSamples
{
    class TableDataTests
    {
        TEST_CLASS(TableDataTests)

        // passes for (1, "") only
        static void CheckParameters(const wchar_t* testName)
        {
            int i = 0;
            String s;
            VERIFY_SUCCEEDED(TestData::TryGetValue(L"i", i));
            VERIFY_SUCCEEDED(TestData::TryGetValue(L"s", s));
            Log::Comment(String().Format(L"%s: TestData = (%d,%s)", testName, i, static_cast<const wchar_t*>(s)));

            DisableVerifyExceptions continueOnFailure;
            VERIFY_ARE_EQUAL(1, i);
            VERIFY_ARE_EQUAL(String(L""), s);
        }

        BEGIN_TEST_METHOD(Simple)
            TEST_METHOD_PROPERTY(L"DataSource", SIMPLE_TABLE_DATASOURCE)
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(SimpleTraits)
            TEST_METHOD_PROPERTY(L"DataSource", SIMPLE_TABLE_DATASOURCE)
            TEST_METHOD_PROPERTY(L"Type", L"Small")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(SimpleTraits2)
            TEST_METHOD_PROPERTY(L"DataSource", SIMPLE_TABLE_DATASOURCE)
            TEST_METHOD_PROPERTY(L"Type", L"Small")
            TEST_METHOD_PROPERTY(L"Author", L"Alice")
        END_TEST_METHOD()

        BEGIN_TEST_METHOD(SimpleTraits3)
            TEST_METHOD_PROPERTY(L"DataSource", SIMPLE_TABLE_DATASOURCE)
            TEST_METHOD_PROPERTY(L"Type", L"Medium")
            TEST_METHOD_PROPERTY(L"Author", L"Carol")
            TEST_METHOD_PROPERTY(L"TestCategory", L"Integration")
        END_TEST_METHOD()
    };

    void TableDataTests::Simple()
    {
        CheckParameters(L"Simple");
    }

    void TableDataTests::SimpleTraits()
    {
        CheckParameters(L"SimpleTraits");
    }

    void TableDataTests::SimpleTraits2()
    {
        CheckParameters(L"SimpleTraits2");
    }

    void TableDataTests::SimpleTraits3()
    {
        CheckParameters(L"SimpleTraits3");
    }


    class LightweightDataTests
    {
        TEST_CLASS(LightweightDataTests)

        BEGIN_TEST_METHOD(SingleValue)
            TEST_METHOD_PROPERTY(L"Data:Value", L"{1, 2, 3}")
        END_TEST_METHOD()

        // cross product of the values: #metadataSet0..3; the first declared property varies fastest
        BEGIN_TEST_METHOD(TwoValues)
            TEST_METHOD_PROPERTY(L"Data:Size", L"{10, 20}")
            TEST_METHOD_PROPERTY(L"Data:Color", L"{Red, Blue}")
        END_TEST_METHOD()
    };

    void LightweightDataTests::SingleValue()
    {
        int value = 0;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Value", value));
        VERIFY_ARE_NOT_EQUAL(2, value, L"the data set with Value 2 is designed to fail");
    }

    void LightweightDataTests::TwoValues()
    {
        int size = 0;
        String color;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Size", size));
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Color", color));
        Log::Comment(String().Format(L"Size=%d, Color=%s", size, static_cast<const wchar_t*>(color)));
        VERIFY_IS_FALSE(size == 20 && color == L"Blue", L"the data set (20, Blue) is designed to fail");
    }


    class StringTableDataTests
    {
        TEST_CLASS(StringTableDataTests)

        BEGIN_TEST_METHOD(CheckStringLength)
            TEST_METHOD_PROPERTY(L"DataSource", L"Table:DataDrivenTests.xml#StringLengths")
        END_TEST_METHOD()
    };

    void StringTableDataTests::CheckStringLength()
    {
        String text;
        int length = 0;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Text", text));
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Length", length));
        VERIFY_ARE_EQUAL(length, text.GetLength());
    }


    // Named rows: the row's Name attribute becomes the test name suffix
    class NamedRows
    {
        TEST_CLASS(NamedRows)

        BEGIN_TEST_METHOD(AllowedCharacters)
            TEST_METHOD_PROPERTY(L"DataSource", L"Table:DataDrivenTests.xml#AllowedCharacters")
        END_TEST_METHOD()

        // passes for the rows with an odd Number; two rows carry row level metadata (Priority, Owner)
        BEGIN_TEST_METHOD(SpecialCharacters)
            TEST_METHOD_PROPERTY(L"DataSource", L"Table:DataDrivenTests.xml#SpecialCharacters")
        END_TEST_METHOD()
    };

    void NamedRows::AllowedCharacters()
    {
        String characters;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Characters", characters));
        VERIFY_IS_FALSE(characters.IsEmpty());
    }

    void NamedRows::SpecialCharacters()
    {
        int number = 0;
        VERIFY_SUCCEEDED(TestData::TryGetValue(L"Number", number));
        VERIFY_ARE_EQUAL(1, number % 2, L"rows with an even Number are designed to fail");
    }


    // The data source does not exist: TE.exe lists and runs the pseudo test
    // TaefSamples::MissingDataSource::Test#error (Blocked)
    class MissingDataSource
    {
        TEST_CLASS(MissingDataSource)

        BEGIN_TEST_METHOD(Test)
            TEST_METHOD_PROPERTY(L"DataSource", L"Table:MissingDataSource.xml#Table")
        END_TEST_METHOD()
    };

    void MissingDataSource::Test()
    {
        Log::Comment(L"MissingDataSource::Test is never executed");
    }
}
