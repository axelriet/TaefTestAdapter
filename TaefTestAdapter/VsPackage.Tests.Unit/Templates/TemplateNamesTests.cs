// This file has been added for TAEF support.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Templates
{
    [TestClass]
    public class TemplateNamesTests
    {
        private const string Fallback = "Fallback";

        [TestMethod]
        [TestCategory(Unit)]
        public void ToIdentifier_Names_TitleCaseIdentifiersWithoutUnderscores()
        {
            var expectedIdentifiers = new Dictionary<string, string>
            {
                ["Contoso.Tests"] = "ContosoTests",
                ["Contoso.Unit Tests"] = "ContosoUnitTests",
                ["Contoso.Unit-Tests"] = "ContosoUnitTests",
                ["my tests-2"] = "MyTests2",
                ["My_Tests"] = "MyTests",
                ["My_New_Tests"] = "MyNewTests",
                ["My New-Tests"] = "MyNewTests",
                ["myTests"] = "MyTests",
                ["TAEF"] = "TAEF",
                ["SampleTests"] = "SampleTests",
                ["x"] = "X",
                ["a1b2"] = "A1b2",
                ["tests 2b"] = "Tests2b",
                ["  leading and trailing  "] = "LeadingAndTrailing",
                ["C++ & C# tests (1)"] = "CCTests1",
                ["Contoso::Tests"] = "ContosoTests",
                ["2024 tests"] = "Taef2024Tests",
                ["_2024"] = "Taef2024",
                ["42"] = "Taef42",
                ["Ümlaut tests"] = "ÜmlautTests",
                ["über.tests"] = "ÜberTests",
                ["straße"] = "Straße",
                ["测试 tests"] = "测试Tests",
                ["cafe\u0301 tests"] = "Cafe\u0301Tests", // combining acute accent: part of the word
                ["\u0301tests"] = "Tests", // a combining mark without a letter is not
                ["\U00020000 tests"] = "\U00020000Tests", // a letter outside of the BMP (a surrogate pair)
                ["tests\U0001F600smile"] = "TestsSmile", // emoji (a surrogate pair) separate words
            };

            foreach (KeyValuePair<string, string> expected in expectedIdentifiers)
            {
                TemplateNames.ToIdentifier(expected.Key, Fallback).Should().Be(expected.Value, $"'{expected.Key}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToIdentifier_NoLetterOrDigit_Fallback()
        {
            foreach (string name in new[] { "", " ", "...", "_", "-_-", "#", "\U0001F600", null })
            {
                TemplateNames.ToIdentifier(name, Fallback).Should().Be(Fallback, $"'{name}'");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNamespaceAndGetClassName_NoLetterOrDigit_Defaults()
        {
            TemplateNames.GetNamespace("...").Should().Be("TaefTests").And.Be(TemplateNames.DefaultNamespace);
            TemplateNames.GetNamespace(null).Should().Be(TemplateNames.DefaultNamespace);
            TemplateNames.GetClassName("").Should().Be("TaefTest").And.Be(TemplateNames.DefaultClassName);
            TemplateNames.GetClassName(null).Should().Be(TemplateNames.DefaultClassName);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNamespaceAndGetClassName_NamesWithoutClash_TitleCaseIdentifiers()
        {
            TemplateNames.GetNamespace("Contoso.Unit Tests").Should().Be("ContosoUnitTests");
            TemplateNames.GetNamespace("Contoso.Unit-Tests").Should().Be("ContosoUnitTests");
            TemplateNames.GetNamespace("2024 tests").Should().Be("Taef2024Tests");
            TemplateNames.GetClassName("My New-Tests").Should().Be("MyNewTests");
            TemplateNames.GetClassName("Ümlaut tests").Should().Be("ÜmlautTests");

            // the namespace may have the name of the test class, of its members and of the project template's class
            foreach (string name in new[] { "MyTests", "SampleTests", "Addition", "Strings", "WithProperties", "DataDriven" })
            {
                TemplateNames.GetNamespace(name).Should().Be(name);
            }
            // TAEF's other namespaces and names which are not macros in the lower case spelling
            foreach (string name in new[] { "Common", "Logging", "TestExecution", "Wex", "Verify", "Null", "Guid", "Tests" })
            {
                TemplateNames.GetNamespace(name).Should().Be(name);
                TemplateNames.GetClassName(name).Should().Be(name);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetClassName_MembersOfTheItemTemplatesClass_TestsIsAppended()
        {
            // a member named like its class would be a constructor
            string[] members = { "ClassSetup", "ClassCleanup", "MethodSetup", "MethodCleanup", "Addition", "Strings", "WithProperties", "DataDriven" };
            GetMembersOfItemTemplateClass().Should().BeEquivalentTo(members);

            foreach (string member in members)
            {
                TemplateNames.GetClassName(member).Should().Be(member + "Tests");
            }
            TemplateNames.GetClassName("addition").Should().Be("AdditionTests");
            TemplateNames.GetClassName("with properties").Should().Be("WithPropertiesTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNamespace_FixturesOfTheItemTemplatesClass_TestsIsAppended()
        {
            // without INLINE_TEST_METHOD_MARKUP, TEST_CLASS_SETUP(ClassSetup) declares ClassSetup only __if_not_exists(ClassSetup)
            foreach (string fixture in new[] { "ClassSetup", "ClassCleanup", "MethodSetup", "MethodCleanup" })
            {
                TemplateNames.GetNamespace(fixture).Should().Be(fixture + "Tests");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNamespaceAndGetClassName_NamesUsedByTheTemplatesCodeOrTaefMacros_TestsIsAppended()
        {
            foreach (string name in new[] { "String", "Log", "TestData", "WEX", "HRESULT" })
            {
                TemplateNames.GetNamespace(name).Should().Be(name + "Tests");
                TemplateNames.GetClassName(name).Should().Be(name + "Tests");
            }
            TemplateNames.GetNamespace("log").Should().Be("LogTests");
            TemplateNames.GetClassName("test data").Should().Be("TestDataTests");

            // compiles as test class, but TE.exe creates an instance of the local struct of TEST_CLASS instead
            TemplateNames.GetClassName("TaefClassNameTester").Should().Be("TaefClassNameTesterTests");
            // the class of the project template, in the same namespace
            TemplateNames.GetClassName("SampleTests").Should().Be("SampleTestsTests");
            GetProjectTemplateClasses().Should().Equal("SampleTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetNamespaceAndGetClassName_MacrosAndDeclarationsOfTheHeaders_TestsIsAppended()
        {
            // object-like macros (of the project template and of the headers WexTestClass.h includes)
            foreach (string macro in new[] { "WIN32", "UNICODE", "NDEBUG", "NULL", "EOF", "E2BIG", "NOERROR", "FAR", "StringCchCopy", "GetExceptionCode" })
            {
                TemplateNames.GetNamespace(macro).Should().Be(macro + "Tests");
                TemplateNames.GetClassName(macro).Should().Be(macro + "Tests");
            }

            // declarations of the global namespace: the name of a namespace only
            foreach (string declaration in new[] { "GUID", "DWORD", "HANDLE", "IStream", "IsEqualGUID", "StringCchCopyW" })
            {
                TemplateNames.GetNamespace(declaration).Should().Be(declaration + "Tests");
                TemplateNames.GetClassName(declaration).Should().Be(declaration);
            }

            // #pragma deprecated
            TemplateNames.GetNamespace("StringCopyWorkerW").Should().Be("StringCopyWorkerWTests");
            TemplateNames.GetClassName("StringCopyWorkerW").Should().Be("StringCopyWorkerWTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReservedNames_AreTitleCaseIdentifiersWhichTheWizardAvoids()
        {
            TemplateNames.ReservedNamespaces.Should().HaveCountGreaterThan(300).And.OnlyHaveUniqueItems();
            TemplateNames.ReservedClassNames.Should().HaveCountGreaterThan(200).And.OnlyHaveUniqueItems();

            foreach (string name in TemplateNames.ReservedNamespaces)
            {
                TemplateNames.ToIdentifier(name, Fallback).Should().Be(name, "only names the wizard can create are reserved");
                TemplateNames.GetNamespace(name).Should().Be(name + "Tests");
            }
            foreach (string name in TemplateNames.ReservedClassNames)
            {
                TemplateNames.ToIdentifier(name, Fallback).Should().Be(name, "only names the wizard can create are reserved");
                TemplateNames.GetClassName(name).Should().Be(name + "Tests");
            }
        }

        /// <returns>The test methods and fixtures of the item template's class.</returns>
        private static IEnumerable<string> GetMembersOfItemTemplateClass()
        {
            string code = File.ReadAllText(Path.Combine(TestResources.AdapterSolutionDir, @"ItemTemplates\Test\TAEF\test.cpp"));
            return Regex.Matches(code, @"^\s*(?:TEST_METHOD|BEGIN_TEST_METHOD|TEST_(?:CLASS|METHOD)_(?:SETUP|CLEANUP))\((?<name>\w+)\)", RegexOptions.Multiline)
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value);
        }

        /// <returns>The classes of the project template.</returns>
        private static IEnumerable<string> GetProjectTemplateClasses()
        {
            string code = File.ReadAllText(Path.Combine(TestResources.AdapterSolutionDir, @"ProjectTemplates\Test\TAEF\test.cpp"));
            return Regex.Matches(code, @"^\s*class\s+(?<name>\w+)", RegexOptions.Multiline).Cast<Match>().Select(m => m.Groups["name"].Value);
        }
    }
}
