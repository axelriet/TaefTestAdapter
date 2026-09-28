// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Settings
{
    [TestClass]
    public class PlaceholderReplacerTests
    {
        private const string SolutionDir = @"C:\TheSolution\";
        private const string TestDllDir = @"C:\TheSolution\out\Debug";
        private const string TestDll = TestDllDir + @"\My_taef.dll";

        private class PlaceholderAndValue
        {
            public string Placeholder { get; }
            public object Value { get; }

            public PlaceholderAndValue(string placeholder, object value)
            {
                Placeholder = placeholder;
                Value = value;
            }
        }

        private class MethodnameAndPlaceholder
        {
            public string MethodName { get; }
            public string Placeholder { get; }

            public MethodnameAndPlaceholder(string methodName, string placeholder)
            {
                MethodName = methodName;
                Placeholder = placeholder;
            }
        }

        private static readonly string[] MethodNames = {
            nameof(PlaceholderReplacer.ReplaceTeExecutablePlaceholders),
            nameof(PlaceholderReplacer.ReplaceAdditionalPdbsPlaceholders),
            nameof(PlaceholderReplacer.ReplaceAdditionalTestExecutionParamPlaceholdersForDiscovery),
            nameof(PlaceholderReplacer.ReplaceAdditionalTestExecutionParamPlaceholdersForExecution),
            nameof(PlaceholderReplacer.ReplaceSetupBatchPlaceholders),
            nameof(PlaceholderReplacer.ReplaceTeardownBatchPlaceholders),
            nameof(PlaceholderReplacer.ReplacePathExtensionPlaceholders),
            nameof(PlaceholderReplacer.ReplaceWorkingDirPlaceholdersForDiscovery),
            nameof(PlaceholderReplacer.ReplaceWorkingDirPlaceholdersForExecution),
            nameof(PlaceholderReplacer.ReplaceEnvironmentVariablesPlaceholdersForDiscovery),
            nameof(PlaceholderReplacer.ReplaceEnvironmentVariablesPlaceholdersForExecution)
        };

        private static readonly List<PlaceholderAndValue> PlaceholdersAndExpectedValues = new List<PlaceholderAndValue>
        {
            new PlaceholderAndValue(PlaceholderReplacer.SolutionDirPlaceholder, SolutionDir),
            new PlaceholderAndValue(PlaceholderReplacer.PlatformNamePlaceholder, "Win33"),
            new PlaceholderAndValue(PlaceholderReplacer.ConfigurationNamePlaceholder, "MyDebug"),
            new PlaceholderAndValue(PlaceholderReplacer.TestDllDirPlaceholder, TestDllDir),
            new PlaceholderAndValue(PlaceholderReplacer.TestDllPlaceholder, TestDll),
            new PlaceholderAndValue(PlaceholderReplacer.TestDirPlaceholder, "testDirectory"),
            new PlaceholderAndValue(PlaceholderReplacer.ThreadIdPlaceholder, 42),
            new PlaceholderAndValue("$(HelperFileKey)", "HelperFileValue"),
            new PlaceholderAndValue("%" + EnvironmentVariableName + "%", EnvironmentVariableValue)
        };

        private const string EnvironmentVariableName = "PLACEHOLDER_REPLACER_TESTS_VARIABLE";
        private const string EnvironmentVariableValue = "EnvironmentVariableValue";

        private static readonly List<MethodnameAndPlaceholder> UnsupportedCombinations = new List<MethodnameAndPlaceholder>
        {
            // $(TestDir) and $(ThreadId) are only available for test execution
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeExecutablePlaceholders), PlaceholderReplacer.TestDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceAdditionalPdbsPlaceholders), PlaceholderReplacer.TestDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceAdditionalTestExecutionParamPlaceholdersForDiscovery), PlaceholderReplacer.TestDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplacePathExtensionPlaceholders), PlaceholderReplacer.TestDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceWorkingDirPlaceholdersForDiscovery), PlaceholderReplacer.TestDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceEnvironmentVariablesPlaceholdersForDiscovery), PlaceholderReplacer.TestDirPlaceholder),

            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeExecutablePlaceholders), PlaceholderReplacer.ThreadIdPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceAdditionalPdbsPlaceholders), PlaceholderReplacer.ThreadIdPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceAdditionalTestExecutionParamPlaceholdersForDiscovery), PlaceholderReplacer.ThreadIdPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplacePathExtensionPlaceholders), PlaceholderReplacer.ThreadIdPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceWorkingDirPlaceholdersForDiscovery), PlaceholderReplacer.ThreadIdPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceEnvironmentVariablesPlaceholdersForDiscovery), PlaceholderReplacer.ThreadIdPlaceholder),

            // batch files are not specific to a test DLL
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceSetupBatchPlaceholders), PlaceholderReplacer.TestDllPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceSetupBatchPlaceholders), PlaceholderReplacer.TestDllDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceSetupBatchPlaceholders), PlaceholderReplacer.ConfigurationNamePlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceSetupBatchPlaceholders), PlaceholderReplacer.PlatformNamePlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceSetupBatchPlaceholders), "$(HelperFileKey)"),

            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeardownBatchPlaceholders), PlaceholderReplacer.TestDllPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeardownBatchPlaceholders), PlaceholderReplacer.TestDllDirPlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeardownBatchPlaceholders), PlaceholderReplacer.ConfigurationNamePlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeardownBatchPlaceholders), PlaceholderReplacer.PlatformNamePlaceholder),
            new MethodnameAndPlaceholder(nameof(PlaceholderReplacer.ReplaceTeardownBatchPlaceholders), "$(HelperFileKey)"),
        };

        [TestInitialize]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, EnvironmentVariableValue);
        }

        [TestCleanup]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllReplacementsTest()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer placeholderReplacer = CreateReplacer(() => SolutionDir, new Dictionary<string, string>
            {
                {nameof(ITaefTestAdapterSettings.ConfigurationName), "MyDebug"},
                {nameof(ITaefTestAdapterSettings.PlatformName), "Win33"},
                {"HelperFileKey", "HelperFileValue"}
            }, mockLogger);

            MethodNames.Should().HaveCount(typeof(PlaceholderReplacer).GetMethods().Count(m => m.Name.StartsWith("Replace") && m.IsPublic),
                "all replacement methods should be tested");

            foreach (string methodName in MethodNames)
            {
                foreach (PlaceholderAndValue placeholder in PlaceholdersAndExpectedValues)
                {
                    if (!UnsupportedCombinations.Any(combination =>
                        combination.MethodName == methodName && combination.Placeholder == placeholder.Placeholder))
                    {
                        var result = InvokeMethodWithStandardParameters(placeholderReplacer, methodName, placeholder.Placeholder);
                        result.Should().Be(placeholder.Value.ToString(), $"{methodName} should replace {placeholder.Placeholder} with {(object) placeholder.Value.ToString()}");
                        mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllReplacementMethods_UnknownPlaceholderResultsInWarning()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => "solutiondir", new Dictionary<string, string>(), mockLogger);

            string placeholder = "$(UnknownPlaceholder)";
            foreach (string methodName in MethodNames)
            {
                mockLogger.Reset();
                string result = InvokeMethodWithStandardParameters(replacer, methodName, placeholder);
                result.Should().Be(placeholder);
                mockLogger.Verify(l => l.LogWarning(It.Is<string>(msg => msg.Contains(placeholder))), Times.Once);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllReplacementMethods_UnknownPlaceholders_AreNotReplaced()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => "solutiondir", new Dictionary<string, string>(), mockLogger);

            foreach (string placeholder in new[] { "$(UnknownPlaceholder)", "$(TestDllFolder)" })
            {
                foreach (string methodName in MethodNames)
                {
                    mockLogger.Reset();
                    string result = InvokeMethodWithStandardParameters(replacer, methodName, placeholder);
                    result.Should().Be(placeholder);
                    mockLogger.Verify(l => l.LogWarning(It.Is<string>(msg => msg.Contains(placeholder) && msg.Contains("could not be replaced"))), Times.Once);
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReplaceTeExecutablePlaceholders_TypicalValues_AreReplaced()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => SolutionDir, new Dictionary<string, string>(), mockLogger);

            replacer.ReplaceTeExecutablePlaceholders(" $(SolutionDir)tools\\TAEF ", TestDll).Should().Be(@"C:\TheSolution\tools\TAEF");
            replacer.ReplaceTeExecutablePlaceholders("$(TestDllDir)\\..\\TE.exe", TestDll).Should().Be(@"C:\TheSolution\out\Debug\..\TE.exe");
            replacer.ReplaceTeExecutablePlaceholders("", TestDll).Should().Be("");
            replacer.ReplaceTeExecutablePlaceholders(null, TestDll).Should().Be("");
            mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);

            replacer.ReplaceTeExecutablePlaceholders("$(TestDir)\\TE.exe", TestDll).Should().Be("$(TestDir)\\TE.exe");
            mockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains(SettingsWrapper.OptionTeExecutable) && s.Contains("$(TestDir)"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReplaceSolutionDir_SolutionDirNotKnown_ValueFromHelperFileIsUsed()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => null, new Dictionary<string, string>
            {
                { nameof(ITaefTestAdapterSettings.SolutionDir), @"C:\FromHelperFile\" }
            }, mockLogger);

            replacer.ReplaceWorkingDirPlaceholdersForDiscovery("$(SolutionDir)Tests", TestDll).Should().Be(@"C:\FromHelperFile\Tests");
            replacer.ReplaceAdditionalTestExecutionParamPlaceholdersForExecution("/p:\"Dir=$(SolutionDir)\"", TestDll, "testDir", 1)
                .Should().Be("/p:\"Dir=C:\\FromHelperFile\\\"");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReplaceSolutionDir_SolutionDirNotKnownAndNoHelperFile_PlaceholderIsRemoved()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => "", new Dictionary<string, string>(), mockLogger);

            replacer.ReplaceWorkingDirPlaceholdersForDiscovery("$(SolutionDir)Tests", TestDll).Should().Be("Tests");
            mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ReplaceEnvironmentVariablesPlaceholders_TestDirAndThreadId_OnlyReplacedForExecution()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => SolutionDir, new Dictionary<string, string>(), mockLogger);
            const string variables = "Dir=$(TestDir)//||//Thread=$(ThreadId)//||//Dll=$(TestDll)";

            replacer.ReplaceEnvironmentVariablesPlaceholdersForExecution(variables, TestDll, @"C:\testdir", 7)
                .Should().Be($"Dir=C:\\testdir//||//Thread=7//||//Dll={TestDll}");
            replacer.ReplaceEnvironmentVariablesPlaceholdersForDiscovery(variables, TestDll)
                .Should().Be($"Dir=//||//Thread=//||//Dll={TestDll}");
            mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void AllReplacementMethods_SeveralPlaceholders_AreAllReplaced()
        {
            var mockLogger = new Mock<ILogger>();
            PlaceholderReplacer replacer = CreateReplacer(() => SolutionDir, new Dictionary<string, string>(), mockLogger);

            replacer.ReplaceAdditionalTestExecutionParamPlaceholdersForExecution(
                    "/p:\"A=$(TestDir)\" /p:\"B=$(TestDir)\" /p:\"C=$(TestDllDir)\" /p:\"D=$(ThreadId)\"", TestDll, @"C:\t", 3)
                .Should().Be($"/p:\"A=C:\\t\" /p:\"B=C:\\t\" /p:\"C={TestDllDir}\" /p:\"D=3\"");
        }

        private static PlaceholderReplacer CreateReplacer(Func<string> getSolutionDir, IDictionary<string, string> helperFileContent, Mock<ILogger> mockLogger)
        {
            var mockHelperFilesCache = new Mock<HelperFilesCache>();
            mockHelperFilesCache.Setup(c => c.GetReplacementsMap(It.IsAny<string>())).Returns(helperFileContent);
            var mockOptions = new Mock<ITaefTestAdapterSettings>();
            return new PlaceholderReplacer(getSolutionDir, () => mockOptions.Object, mockHelperFilesCache.Object, mockLogger.Object);
        }

        private string InvokeMethodWithStandardParameters(PlaceholderReplacer placeholderReplacer, string methodName,
            string input)
        {
            var method = typeof(PlaceholderReplacer).GetMethod(methodName);
            // ReSharper disable once PossibleNullReferenceException
            var parameters = method.GetParameters();

            var parameterValues = new List<object> {input};
            for (int i = 1; i < parameters.Length; i++)
            {
                parameterValues.Add(GetValue(parameters[i]));
            }

            return (string) method.Invoke(placeholderReplacer, parameterValues.ToArray());
        }

        private object GetValue(ParameterInfo parameter)
        {
            switch (parameter.Name)
            {
                case "testDll": return TestDll;
                case "threadId": return 42;
                default: return parameter.Name;
            }
        }

    }
}
