// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;

namespace TaefTestAdapter.VsPackage.Templates
{
    /// <summary>
    /// Wizard of the TAEF Test Project and TAEF Test templates (<c>WizardExtension</c> of <c>ProjectTemplates\Test\TAEF</c>
    /// and <c>ItemTemplates\Test\TAEF</c>): adds the template parameters <c>$taefnamespace$</c> (the namespace of the test
    /// classes and the RootNamespace of a new project) and, for items, <c>$taefclassname$</c> (the name of the test class),
    /// see <see cref="TemplateNames"/>. It has no UI. It never throws, since an exception would abort the creation of the
    /// project or item: if the names cannot be computed, a project gets the namespace <see cref="TemplateNames.DefaultNamespace"/>
    /// and an item Visual Studio's own values (<c>$rootnamespace$</c> and <c>$safeitemname$</c>, in which characters that are
    /// not valid in identifiers are replaced by <c>_</c>).
    /// </summary>
    public sealed class TaefTemplateWizard : IWizard
    {
        public const string NamespaceParameter = "$taefnamespace$";
        public const string ClassNameParameter = "$taefclassname$";

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind,
            object[] customParams)
        {
            if (replacementsDictionary == null)
                return;

            bool isItem = runKind == WizardRunKind.AsNewItem;
            try
            {
                if (isItem)
                {
                    // $rootnamespace$ is the RootNamespace of the project the item is added to, as written in its project
                    // file; $rootname$ is the file name as typed, with extension (Visual Studio adds $fileinputname$ only
                    // after RunStarted, and $safeitemname$ has lost characters such as non-spacing marks)
                    string namespaceName = TemplateNames.GetNamespace(
                        GetFirstValue(replacementsDictionary, "$rootnamespace$", "$safeprojectname$", "$projectname$"));
                    string fileName = GetFirstValue(replacementsDictionary, "$fileinputname$");
                    if (fileName == null)
                    {
                        string rootName = GetFirstValue(replacementsDictionary, "$rootname$");
                        if (rootName != null)
                            fileName = Path.GetFileNameWithoutExtension(rootName);
                    }
                    string className = TemplateNames.GetClassName(
                        string.IsNullOrWhiteSpace(fileName) ? GetFirstValue(replacementsDictionary, "$safeitemname$") : fileName);
                    replacementsDictionary[NamespaceParameter] = namespaceName;
                    replacementsDictionary[ClassNameParameter] = className;
                }
                else
                {
                    replacementsDictionary[NamespaceParameter] = TemplateNames.GetNamespace(
                        GetFirstValue(replacementsDictionary, "$projectname$", "$safeprojectname$"));
                }
            }
            catch (Exception)
            {
                if (isItem)
                {
                    SetFallbackValue(replacementsDictionary, NamespaceParameter, "$rootnamespace$", TemplateNames.DefaultNamespace);
                    SetFallbackValue(replacementsDictionary, ClassNameParameter, "$safeitemname$", TemplateNames.DefaultClassName);
                }
                else
                {
                    // $safeprojectname$ is still the project name as typed when RunStarted is called: not an identifier
                    SetFallbackValue(replacementsDictionary, NamespaceParameter, null, TemplateNames.DefaultNamespace);
                }
            }
        }

        public void ProjectFinishedGenerating(Project project)
        {
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
        }

        public bool ShouldAddProjectItem(string filePath)
        {
            return true;
        }

        public void BeforeOpeningFile(ProjectItem projectItem)
        {
        }

        public void RunFinished()
        {
        }

        /// <returns>The value of the first of <paramref name="parameters"/> with a non-empty value, or null.</returns>
        private static string GetFirstValue(IDictionary<string, string> replacementsDictionary, params string[] parameters)
        {
            foreach (string parameter in parameters)
            {
                if (replacementsDictionary.TryGetValue(parameter, out string value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        private static void SetFallbackValue(IDictionary<string, string> replacementsDictionary, string parameter,
            string visualStudioParameter, string defaultValue)
        {
            try
            {
                replacementsDictionary[parameter] =
                    visualStudioParameter != null && replacementsDictionary.TryGetValue(visualStudioParameter, out string value) &&
                    !string.IsNullOrWhiteSpace(value)
                        ? value
                        : defaultValue;
            }
            catch (Exception)
            {
                // the template shows the parameter's name instead of a value, but it is created
            }
        }
    }
}
