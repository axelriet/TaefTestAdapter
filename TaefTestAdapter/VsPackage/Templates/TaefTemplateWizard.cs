// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using EnvDTE;
using Microsoft.VisualStudio.TemplateWizard;

namespace TaefTestAdapter.VsPackage.Templates
{
    /// <summary>
    /// Wizard of the TAEF Test Project and TAEF Test templates (<c>WizardExtension</c> of <c>ProjectTemplates\Test\TAEF</c>
    /// and <c>ItemTemplates\Test\TAEF</c>): adds the template parameters <c>$taefnamespace$</c> (the namespace of the test
    /// classes and the RootNamespace of a new project) and, for items, <c>$taefclassname$</c> (the name of the test class),
    /// see <see cref="TemplateNames"/>. It has no UI. It never throws, since an exception would abort the creation of the
    /// project or item: if the names cannot be computed, Visual Studio's own values are used (<c>$safeprojectname$</c> and
    /// <c>$safeitemname$</c>, in which characters that are not valid in identifiers are replaced by <c>_</c>, and
    /// <c>$rootnamespace$</c>).
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
                    // file; $fileinputname$ is the file name without extension, as typed
                    string namespaceName = TemplateNames.GetNamespace(
                        GetFirstValue(replacementsDictionary, "$rootnamespace$", "$safeprojectname$", "$projectname$"));
                    string className = TemplateNames.GetClassName(
                        GetFirstValue(replacementsDictionary, "$fileinputname$", "$safeitemname$"));
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
                SetFallbackValue(replacementsDictionary, NamespaceParameter, isItem ? "$rootnamespace$" : "$safeprojectname$",
                    TemplateNames.DefaultNamespace);
                if (isItem)
                    SetFallbackValue(replacementsDictionary, ClassNameParameter, "$safeitemname$", TemplateNames.DefaultClassName);
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
                    replacementsDictionary.TryGetValue(visualStudioParameter, out string value) && !string.IsNullOrWhiteSpace(value)
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
