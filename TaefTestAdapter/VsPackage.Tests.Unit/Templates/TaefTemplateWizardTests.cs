// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TemplateWizard;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Templates
{
    /// <summary>
    /// Runs the wizard of the TAEF templates with the parameters Visual Studio passes to it, and checks that the templates
    /// use the wizard (<c>WizardExtension</c> of their .vstemplate files) and its parameters.
    /// </summary>
    [TestClass]
    public class TaefTemplateWizardTests
    {
        private const string NamespaceParameter = TaefTemplateWizard.NamespaceParameter;
        private const string ClassNameParameter = TaefTemplateWizard.ClassNameParameter;

        private static readonly string[] TemplateFiles =
        {
            @"ProjectTemplates\Test\TAEF\TaefTest.vstemplate",
            @"ItemTemplates\Test\TAEF\TaefTest.vstemplate"
        };

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewProject_NamespaceIsTitleCaseProjectName()
        {
            Dictionary<string, string> parameters = NewProjectParameters("Contoso.Unit Tests");

            RunStarted(parameters, WizardRunKind.AsNewProject);

            parameters[NamespaceParameter].Should().Be("ContosoUnitTests");
            parameters.Should().NotContainKey(ClassNameParameter);
            // Visual Studio's parameters are kept
            parameters["$projectname$"].Should().Be("Contoso.Unit Tests");
            parameters["$safeprojectname$"].Should().Be("Contoso_Unit_Tests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewProjectNamedLikeANameOfTheTemplatesCode_TestsIsAppended()
        {
            Dictionary<string, string> parameters = NewProjectParameters("log");

            RunStarted(parameters, WizardRunKind.AsNewProject);

            parameters[NamespaceParameter].Should().Be("LogTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewProjectWithoutProjectName_NamespaceFromSafeProjectNameOrDefault()
        {
            var parameters = new Dictionary<string, string> { ["$safeprojectname$"] = "My_Tests" };
            RunStarted(parameters, WizardRunKind.AsNewProject);
            parameters[NamespaceParameter].Should().Be("MyTests");

            parameters = new Dictionary<string, string>();
            RunStarted(parameters, WizardRunKind.AsNewProject);
            parameters[NamespaceParameter].Should().Be(TemplateNames.DefaultNamespace);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItem_NamespaceFromRootNamespaceAndClassNameFromFileName()
        {
            Dictionary<string, string> parameters = NewItemParameters("ContosoUnitTests", "My New-Tests");

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[NamespaceParameter].Should().Be("ContosoUnitTests");
            parameters[ClassNameParameter].Should().Be("MyNewTests");
            parameters["$safeitemname$"].Should().Be("My_New_Tests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemInProjectWithHandWrittenRootNamespace_NamespaceIsAnIdentifier()
        {
            // e.g. <RootNamespace>Contoso.Unit-Tests</RootNamespace>, which Visual Studio passes unchanged
            Dictionary<string, string> parameters = NewItemParameters("Contoso.Unit-Tests", "MathTests");

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[NamespaceParameter].Should().Be("ContosoUnitTests");
            parameters[ClassNameParameter].Should().Be("MathTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemInProjectWithRootNamespaceWithUnderscores_NamespaceIsTitleCase()
        {
            // e.g. a project created with Visual Studio's $safeprojectname$: the RootNamespace is converted, too (README)
            Dictionary<string, string> parameters = NewItemParameters("Contoso_Unit_Tests", "MathTests");

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[NamespaceParameter].Should().Be("ContosoUnitTests");
            parameters[ClassNameParameter].Should().Be("MathTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemNamedLikeAMemberOfTheTemplatesClass_TestsIsAppended()
        {
            Dictionary<string, string> parameters = NewItemParameters("ContosoUnitTests", "Addition");

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[ClassNameParameter].Should().Be("AdditionTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemWithoutRootNamespace_NamespaceFromProjectName()
        {
            Dictionary<string, string> parameters = NewItemParameters("", "MathTests");
            parameters["$projectname$"] = "Contoso.Tests";
            parameters["$safeprojectname$"] = "Contoso_Tests";
            RunStarted(parameters, WizardRunKind.AsNewItem);
            parameters[NamespaceParameter].Should().Be("ContosoTests");

            parameters = NewItemParameters(null, "MathTests");
            parameters.Remove("$rootnamespace$");
            parameters["$projectname$"] = "my tests-2";
            RunStarted(parameters, WizardRunKind.AsNewItem);
            parameters[NamespaceParameter].Should().Be("MyTests2");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemWithNonSpacingMarks_ClassNameKeepsThem()
        {
            // Visual Studio's $safeitemname$ has lost the non-spacing marks ("cafe_tests"; the Hindi name loses its virama)
            Dictionary<string, string> parameters = NewItemParameters("ContosoTests", "cafe\u0301 tests");
            RunStarted(parameters, WizardRunKind.AsNewItem);
            parameters[ClassNameParameter].Should().Be("Cafe\u0301Tests");

            parameters = NewItemParameters("ContosoTests", "\u092A\u0930\u0940\u0915\u094D\u0937\u0923");
            RunStarted(parameters, WizardRunKind.AsNewItem);
            parameters[ClassNameParameter].Should().Be("\u092A\u0930\u0940\u0915\u094D\u0937\u0923");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemWithFileInputName_ClassNameFromFileInputName()
        {
            // hosts which pass $fileinputname$ to RunStarted (Visual Studio adds it later)
            Dictionary<string, string> parameters = NewItemParameters("ContosoTests", "Other");
            parameters["$fileinputname$"] = "My New-Tests";

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[ClassNameParameter].Should().Be("MyNewTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemWithoutRootName_ClassNameFromSafeItemName()
        {
            Dictionary<string, string> parameters = NewItemParameters("ContosoTests", "My New-Tests");
            parameters.Remove("$rootname$");

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[ClassNameParameter].Should().Be("MyNewTests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_NewItemWithoutParameters_DefaultNames()
        {
            var parameters = new Dictionary<string, string>();

            RunStarted(parameters, WizardRunKind.AsNewItem);

            parameters[NamespaceParameter].Should().Be(TemplateNames.DefaultNamespace);
            parameters[ClassNameParameter].Should().Be(TemplateNames.DefaultClassName);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_ExceptionWhileComputingTheNamesOfAnItem_VisualStudioValuesAreUsed()
        {
            var comparer = new ThrowingComparer("$fileinputname$");
            var parameters = new Dictionary<string, string>(NewItemParameters("ContosoTests", "My New-Tests"), comparer);
            comparer.IsArmed = true;

            RunStarted(parameters, WizardRunKind.AsNewItem);

            comparer.HasThrown.Should().BeTrue();
            parameters[NamespaceParameter].Should().Be("ContosoTests");
            parameters[ClassNameParameter].Should().Be("My_New_Tests");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_ExceptionWhileComputingTheNamespaceOfAProject_DefaultNamespaceIsUsed()
        {
            // $safeprojectname$ is still the project name as typed when RunStarted is called, so it is not used
            var comparer = new ThrowingComparer("$projectname$");
            var parameters = new Dictionary<string, string>(NewProjectParameters("Contoso.Unit Tests"), comparer);
            comparer.IsArmed = true;

            RunStarted(parameters, WizardRunKind.AsNewProject);

            comparer.HasThrown.Should().BeTrue();
            parameters[NamespaceParameter].Should().Be(TemplateNames.DefaultNamespace);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void RunStarted_DictionaryThrowsAlways_DoesNotThrow()
        {
            foreach (WizardRunKind runKind in new[] { WizardRunKind.AsNewProject, WizardRunKind.AsNewItem })
            {
                var comparer = new ThrowingComparer(null);
                var parameters = new Dictionary<string, string>(NewItemParameters("ContosoTests", "MathTests"), comparer);
                comparer.IsArmed = true;

                Action runStarted = () => RunStarted(parameters, runKind);

                runStarted.Should().NotThrow();
                comparer.HasThrown.Should().BeTrue();
            }

            Action withoutDictionary = () => RunStarted(null, WizardRunKind.AsNewItem);
            withoutDictionary.Should().NotThrow();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void OtherMembers_DoNothing()
        {
            var wizard = new TaefTemplateWizard();

            wizard.ShouldAddProjectItem(@"C:\Contoso\Tests.cpp").Should().BeTrue();
            wizard.Invoking(w => w.ProjectFinishedGenerating(null)).Should().NotThrow();
            wizard.Invoking(w => w.ProjectItemFinishedGenerating(null)).Should().NotThrow();
            wizard.Invoking(w => w.BeforeOpeningFile(null)).Should().NotThrow();
            wizard.Invoking(w => w.RunFinished()).Should().NotThrow();
        }

        /// <summary>
        /// Visual Studio loads the wizard by the full name of its assembly: a new version of the assembly (SetVersion.ps1)
        /// must also be set in the templates.
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void Templates_WizardExtension_IsThisWizard()
        {
            typeof(TaefTemplateWizard).Assembly.GetName().GetPublicKeyToken().Should().NotBeNullOrEmpty("the assembly of a template wizard must have a strong name");

            foreach (string templateFile in TemplateFiles)
            {
                XDocument template = XDocument.Load(Path.Combine(TestResources.AdapterSolutionDir, templateFile));
                XNamespace ns = template.Root.Name.Namespace;
                XElement wizardExtension = template.Root.Elements(ns + "WizardExtension").Should().ContainSingle(templateFile).Which;

                ((string)wizardExtension.Element(ns + "Assembly")).Should().Be(typeof(TaefTemplateWizard).Assembly.FullName, templateFile);
                ((string)wizardExtension.Element(ns + "FullClassName")).Should().Be(typeof(TaefTemplateWizard).FullName, templateFile);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Templates_NamespaceAndClassName_AreTheWizardsParameters()
        {
            string projectFile = ReadTemplateFile(@"ProjectTemplates\Test\TAEF\TaefTest.vcxproj");
            projectFile.Should().Contain($"<RootNamespace>{NamespaceParameter}</RootNamespace>");

            string projectCode = ReadTemplateFile(@"ProjectTemplates\Test\TAEF\test.cpp");
            projectCode.Should().Contain($"namespace {NamespaceParameter}\r\n");

            string itemCode = ReadTemplateFile(@"ItemTemplates\Test\TAEF\test.cpp");
            itemCode.Should().Contain($"namespace {NamespaceParameter}\r\n");
            itemCode.Should().Contain($"class {ClassNameParameter}\r\n").And.Contain($"TEST_CLASS({ClassNameParameter});");

            // Visual Studio's names (with '_' for invalid characters, or not even an identifier) are not used
            foreach (string content in new[] { projectFile, projectCode, itemCode })
            {
                Regex.Matches(content, @"\$(safeprojectname|rootnamespace|safeitemname)\$").Cast<Match>().Select(m => m.Value).Should().BeEmpty();
            }
        }

        /// <summary>
        /// The template files are zipped into the VSIX as they are: the files created from them have the same line endings
        /// (see .gitattributes).
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void Templates_TextFiles_HaveWindowsLineEndings()
        {
            string[] textFiles = new[] { "ProjectTemplates", "ItemTemplates" }
                .SelectMany(d => Directory.GetFiles(Path.Combine(TestResources.AdapterSolutionDir, d), "*", SearchOption.AllDirectories))
                .Where(f => !f.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            textFiles.Should().HaveCount(5);

            foreach (string textFile in textFiles)
            {
                string content = File.ReadAllText(textFile);
                content.Should().Contain("\r\n", textFile);
                Regex.IsMatch(content, @"\r(?!\n)|(?<!\r)\n").Should().BeFalse($"{textFile} should only contain CRLF line breaks");
            }
        }

        internal static void RunStarted(Dictionary<string, string> parameters, WizardRunKind runKind)
        {
            new TaefTemplateWizard().RunStarted(null, parameters, runKind, new object[0]);
        }

        /// <returns>(Some of) the parameters Visual Studio passes when a project is created from the project template.</returns>
        internal static Dictionary<string, string> NewProjectParameters(string projectName)
        {
            return new Dictionary<string, string>
            {
                ["$guid1$"] = Guid.NewGuid().ToString("D").ToUpperInvariant(),
                ["$projectname$"] = projectName,
                ["$safeprojectname$"] = ToVisualStudioSafeName(projectName)
            };
        }

        /// <returns>
        /// (Some of) the parameters Visual Studio passes to <c>RunStarted</c> when an item is created from the item template:
        /// <c>$rootnamespace$</c> is the RootNamespace of the project, <c>$rootname$</c> the file name as typed, with
        /// extension. Visual Studio adds <c>$fileinputname$</c> (the file name without extension) only after
        /// <c>RunStarted</c>.
        /// </returns>
        internal static Dictionary<string, string> NewItemParameters(string rootNamespace, string fileName)
        {
            return new Dictionary<string, string>
            {
                ["$rootnamespace$"] = rootNamespace,
                ["$defaultnamespace$"] = rootNamespace,
                ["$rootname$"] = fileName + ".cpp",
                ["$safeitemname$"] = ToVisualStudioSafeName(fileName)
            };
        }

        /// <returns>
        /// The name made an identifier as Visual Studio does for $safeitemname$ (non-spacing marks are removed, other
        /// characters which are not valid in identifiers replaced by <c>_</c>).
        /// </returns>
        internal static string ToVisualStudioSafeName(string name)
        {
            string safeName = Regex.Replace(Regex.Replace(name ?? "", @"\p{Mn}", ""), @"[^\p{L}\p{Nd}_]", "_");
            return safeName.Length > 0 && char.IsDigit(safeName[0]) ? "_" + safeName : safeName;
        }

        private static string ReadTemplateFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(TestResources.AdapterSolutionDir, relativePath));
        }

        /// <summary>Throws for the given key (all keys if null) once armed, as a dictionary with unexpected behavior.</summary>
        private sealed class ThrowingComparer : IEqualityComparer<string>
        {
            private readonly string _key;

            public ThrowingComparer(string key)
            {
                _key = key;
            }

            public bool IsArmed { get; set; }

            public bool HasThrown { get; private set; }

            public bool Equals(string x, string y)
            {
                return string.Equals(x, y, StringComparison.Ordinal);
            }

            public int GetHashCode(string key)
            {
                if (IsArmed && (_key == null || _key == key))
                {
                    HasThrown = true;
                    throw new InvalidOperationException($"Test exception for key {key}");
                }
                return StringComparer.Ordinal.GetHashCode(key);
            }
        }
    }
}
