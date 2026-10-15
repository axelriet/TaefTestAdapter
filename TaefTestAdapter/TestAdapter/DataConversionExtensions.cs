// This file has been modified for TAEF support.

using System;
using System.IO;
using System.Linq;
using TaefTestAdapter.Common;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using TestCase = TaefTestAdapter.Model.TestCase;
using TestOutcome = TaefTestAdapter.Model.TestOutcome;
using TestResult = TaefTestAdapter.Model.TestResult;
using Trait = TaefTestAdapter.Model.Trait;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestProperty = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestProperty;
using VsTestPropertyAttributes = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestPropertyAttributes;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using VsTestResultMessage = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage;
using VsTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;
using VsTrait = Microsoft.VisualStudio.TestPlatform.ObjectModel.Trait;

namespace TaefTestAdapter.TestAdapter
{

    /// <summary>
    /// Conversions between the adapter's model and the model of the VsTest framework. Tests are identified as follows:
    /// <list type="bullet">
    /// <item>The adapter's <see cref="TestCase.FullyQualifiedName"/> and <see cref="TestCase.DisplayName"/> are the TAEF
    /// name of the test (e.g. <c>Ns::Class::Method#metadataSet0</c>).</item>
    /// <item>The fully qualified name of a <see cref="VsTestCase"/> uses <c>.</c> as scope separator (e.g.
    /// <c>Ns.Class.Method#metadataSet0</c>, see <see cref="TaefNames.ToVsFullyQualifiedName"/>), which is what Visual
    /// Studio expects to group tests by namespace and class. Its display name is the TAEF name, which is also stored
    /// in property <see cref="TaefNameProperty"/>.</item>
    /// </list>
    /// </summary>
    public static class DataConversionExtensions
    {
        /// <summary>Id of the VS test property holding the TAEF name of a test.</summary>
        public const string TaefNamePropertyId = "TaefTestAdapter.TaefName";
        public const string TaefNamePropertyLabel = "TAEF name";

        /// <summary>
        /// VS test property (of type string) holding the TAEF name of a test, i.e. the name to be passed to TE.exe.
        /// </summary>
        public static readonly VsTestProperty TaefNameProperty;

        private static readonly VsTestProperty TestMetaDataProperty;
        private static readonly VsTestProperty DataRowIndexProperty;
        private static readonly VsTestProperty TaefClassProperty;
        private static readonly VsTestProperty ExecutionGroupProperty;
        private static readonly VsTestProperty ModuleFixturesProperty;
        private static readonly VsTestProperty HierarchyProperty;
        private static readonly VsTestProperty ManagedTypeProperty;
        private static readonly VsTestProperty ManagedMethodProperty;

        static DataConversionExtensions()
        {
            TaefNameProperty = VsTestProperty.Register(TaefNamePropertyId, TaefNamePropertyLabel, typeof(string), typeof(VsTestCase));
            TestMetaDataProperty = VsTestProperty.Register(TestCaseMetaDataProperty.Id, TestCaseMetaDataProperty.Label, typeof(string), typeof(VsTestCase));
            DataRowIndexProperty = VsTestProperty.Register(TestCaseDataRowIndexProperty.Id, TestCaseDataRowIndexProperty.Label, typeof(string), typeof(VsTestCase));
            TaefClassProperty = VsTestProperty.Register(TestCaseTaefClassProperty.Id, TestCaseTaefClassProperty.Label, typeof(string), typeof(VsTestCase));
            ExecutionGroupProperty = VsTestProperty.Register(TestCaseExecutionGroupProperty.Id, TestCaseExecutionGroupProperty.Label, typeof(string), typeof(VsTestCase));
            ModuleFixturesProperty = VsTestProperty.Register(TestCaseModuleFixturesProperty.Id, TestCaseModuleFixturesProperty.Label, typeof(string), typeof(VsTestCase));
            HierarchyProperty = VsTestProperty.Find("TestCase.Hierarchy")
                ?? VsTestProperty.Register("TestCase.Hierarchy", "Hierarchy", "", "", typeof(string[]), null,
                    VsTestPropertyAttributes.Hidden, typeof(VsTestCase));
            ManagedTypeProperty = VsTestProperty.Find("TestCase.ManagedType")
                ?? VsTestProperty.Register("TestCase.ManagedType", "ManagedType", "", "", typeof(string), null,
                    VsTestPropertyAttributes.Hidden, typeof(VsTestCase));
            ManagedMethodProperty = VsTestProperty.Find("TestCase.ManagedMethod")
                ?? VsTestProperty.Register("TestCase.ManagedMethod", "ManagedMethod", "", "", typeof(string), null,
                    VsTestPropertyAttributes.Hidden, typeof(VsTestCase));
        }


        /// <summary>
        /// Converts a VS test case into the adapter's model. The TAEF name is taken from property
        /// <see cref="TaefNameProperty"/> or, if that is not available, from the test case's display name. The adapter's
        /// test properties (<see cref="TestCaseMetaDataProperty"/>, <see cref="TestCaseDataRowIndexProperty"/>) are restored
        /// from the VS test properties set by <see cref="ToVsTestCase"/> (invalid values are ignored).
        /// </summary>
        public static TestCase ToTestCase(this VsTestCase vsTestCase)
        {
            string taefName = GetTaefName(vsTestCase);
            var testCase = new TestCase(taefName, vsTestCase.Source,
                taefName, vsTestCase.CodeFilePath, vsTestCase.LineNumber);
            testCase.Traits.AddRange(vsTestCase.Traits.Select(ToTrait));

            var metaDataSerialization = vsTestCase.GetPropertyValue(TestMetaDataProperty);
            if (metaDataSerialization != null)
                testCase.Properties.Add(new TestCaseMetaDataProperty((string)metaDataSerialization));

            // needed to select the test's data row if its name contains wildcards (see CommandLineGenerator)
            TestCaseDataRowIndexProperty dataRowIndex = TestCaseDataRowIndexProperty.TryParse(vsTestCase.GetPropertyValue(DataRowIndexProperty) as string);
            if (dataRowIndex != null)
                testCase.Properties.Add(dataRowIndex);

            string managedClass = vsTestCase.GetPropertyValue(TaefClassProperty) as string;
            if (!string.IsNullOrEmpty(managedClass)
                && (taefName.StartsWith(managedClass + ".", StringComparison.Ordinal) || taefName == managedClass))
            {
                testCase.Properties.Add(new TestCaseTaefClassProperty(managedClass));
                string executionGroup = vsTestCase.GetPropertyValue(ExecutionGroupProperty) as string;
                if (!string.IsNullOrEmpty(executionGroup))
                    testCase.Properties.Add(new TestCaseExecutionGroupProperty(executionGroup));
                TestCaseModuleFixturesProperty moduleFixtures = TestCaseModuleFixturesProperty.TryParse(
                    vsTestCase.GetPropertyValue(ModuleFixturesProperty) as string);
                if (moduleFixtures != null)
                    testCase.Properties.Add(moduleFixtures);
            }

            return testCase;
        }

        /// <summary>
        /// Converts a test case of the adapter's model into a VS test case (see <see cref="DataConversionExtensions"/>
        /// for the names).
        /// </summary>
        public static VsTestCase ToVsTestCase(this TestCase testCase)
        {
            string fullyQualifiedName = testCase.IsManaged ? testCase.FullyQualifiedName : TaefNames.ToVsFullyQualifiedName(testCase.FullyQualifiedName);
            var vsTestCase = new VsTestCase(fullyQualifiedName, TestExecutor.ExecutorUri, testCase.Source)
            {
                DisplayName = testCase.DisplayName,
                CodeFilePath = testCase.CodeFilePath,
                LineNumber = testCase.LineNumber
            };
            vsTestCase.SetPropertyValue(TaefNameProperty, testCase.FullyQualifiedName);

            vsTestCase.Traits.AddRange(testCase.Traits.Select(ToVsTrait));

            var property = testCase.Properties.OfType<TestCaseMetaDataProperty>().SingleOrDefault();
            if (property != null)
                vsTestCase.SetPropertyValue(TestMetaDataProperty, property.Serialization);

            var dataRowIndex = testCase.Properties.OfType<TestCaseDataRowIndexProperty>().FirstOrDefault();
            if (dataRowIndex != null)
                vsTestCase.SetPropertyValue(DataRowIndexProperty, dataRowIndex.Serialization);

            if (testCase.IsManaged)
            {
                // VSTest otherwise derives the ID from ManagedType/ManagedMethod, which are shared by all data rows.
                Guid idFromExactName = vsTestCase.Id;
                string listedClass = testCase.TaefClassName;
                vsTestCase.SetPropertyValue(TaefClassProperty, listedClass);
                TestCaseExecutionGroupProperty executionGroup = testCase.Properties.OfType<TestCaseExecutionGroupProperty>().FirstOrDefault();
                if (executionGroup != null)
                    vsTestCase.SetPropertyValue(ExecutionGroupProperty, executionGroup.Serialization);
                TestCaseModuleFixturesProperty moduleFixtures = testCase.Properties.OfType<TestCaseModuleFixturesProperty>().FirstOrDefault();
                if (moduleFixtures != null)
                    vsTestCase.SetPropertyValue(ModuleFixturesProperty, moduleFixtures.Serialization);
                string type = TaefNames.RemoveManagedRow(listedClass);
                string method = TaefNames.GetManagedMethodName(testCase.FullyQualifiedName, listedClass);
                int namespaceEnd = type.LastIndexOf('.');
                string testNamespace = namespaceEnd < 0 ? "" : type.Substring(0, namespaceEnd);
                string className = type.Substring(namespaceEnd + 1);
                string displayedMethod = testCase.FullyQualifiedName.StartsWith(listedClass + ".", StringComparison.Ordinal)
                    ? testCase.FullyQualifiedName.Substring(listedClass.Length + 1)
                    : testCase.FullyQualifiedName;
                string classRow = listedClass.Substring(type.Length);
                vsTestCase.SetPropertyValue(HierarchyProperty, new[] { Path.GetFileName(testCase.Source), testNamespace, className + classRow, displayedMethod });
                if (method.Length > 0)
                {
                    vsTestCase.SetPropertyValue(ManagedTypeProperty, type);
                    vsTestCase.SetPropertyValue(ManagedMethodProperty, method);
                }
                vsTestCase.Id = idFromExactName;
            }

            return vsTestCase;
        }

        /// <returns>
        /// The TAEF name of <paramref name="vsTestCase"/> (property <see cref="TaefNameProperty"/>, or the display name
        /// if the property is not available).
        /// </returns>
        public static string GetTaefName(this VsTestCase vsTestCase)
        {
            string taefName = vsTestCase.GetPropertyValue(TaefNameProperty) as string;
            return string.IsNullOrEmpty(taefName) ? vsTestCase.DisplayName : taefName;
        }


        private static Trait ToTrait(this VsTrait trait)
        {
            return new Trait(trait.Name, trait.Value);
        }

        private static VsTrait ToVsTrait(this Trait trait)
        {
            return new VsTrait(trait.Name, trait.Value);
        }


        /// <summary>
        /// Converts a test result into a VS test result. The output of the test (see <see cref="TestResult.Output"/>)
        /// becomes a message of category <see cref="VsTestResultMessage.StandardOutCategory"/>.
        /// </summary>
        public static VsTestResult ToVsTestResult(this TestResult testResult)
        {
            var vsTestResult = new VsTestResult(ToVsTestCase(testResult.TestCase))
            {
                Outcome = testResult.Outcome.ToVsTestOutcome(),
                ComputerName = testResult.ComputerName,
                DisplayName = testResult.DisplayName,
                Duration = testResult.Duration,
                ErrorMessage = testResult.ErrorMessage,
                ErrorStackTrace = testResult.ErrorStackTrace
            };

            if (!string.IsNullOrEmpty(testResult.Output))
                vsTestResult.Messages.Add(new VsTestResultMessage(VsTestResultMessage.StandardOutCategory, testResult.Output));

            return vsTestResult;
        }


        public static VsTestOutcome ToVsTestOutcome(this TestOutcome testOutcome)
        {
            switch (testOutcome)
            {
                case TestOutcome.Passed:
                    return VsTestOutcome.Passed;
                case TestOutcome.Failed:
                    return VsTestOutcome.Failed;
                case TestOutcome.Skipped:
                    return VsTestOutcome.Skipped;
                case TestOutcome.None:
                    return VsTestOutcome.None;
                case TestOutcome.NotFound:
                    return VsTestOutcome.NotFound;
                default:
                    throw new InvalidOperationException($"Unknown enum literal: {testOutcome}");
            }
        }

        public static Severity GetSeverity(this TestMessageLevel level)
        {
            switch (level)
            {
                case TestMessageLevel.Informational: return Severity.Info;
                case TestMessageLevel.Warning: return Severity.Warning;
                case TestMessageLevel.Error: return Severity.Error;
                default:
                    throw new InvalidOperationException($"Unknown enum literal: {level}");
            }
        }

        public static TestMessageLevel GetTestMessageLevel(this Severity severity)
        {
            switch (severity)
            {
                case Severity.Info: return TestMessageLevel.Informational;
                case Severity.Warning: return TestMessageLevel.Warning;
                case Severity.Error: return TestMessageLevel.Error;
                default:
                    throw new InvalidOperationException($"Unknown enum literal: {severity}");
            }
        }

    }

}
