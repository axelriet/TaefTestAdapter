// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaefTestAdapter.TestAdapter.Fakes;
using TaefTestAdapter.TestAdapter.Settings;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VsTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace TaefTestAdapter.TestAdapter
{

    /// <summary>
    /// Base class of the tests of the VsTest framework integration: adds mocks of the VsTest framework's run context,
    /// framework handle and message logger, and helpers for test case filters, run settings and copies of sample DLLs.
    /// </summary>
    public abstract class TestAdapterTestsBase : TestsBase
    {
        protected readonly Mock<IRunContext> MockRunContext = new Mock<IRunContext>();
        protected readonly Mock<IFrameworkHandle> MockFrameworkHandle = new Mock<IFrameworkHandle>();
        protected readonly Mock<IMessageLogger> MockVsLogger = new Mock<IMessageLogger>();

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            MockRunContext.Setup(rc => rc.SolutionDirectory).Returns(Path.GetFullPath(TestResources.SampleTestsSolutionDir));
        }

        [TestCleanup]
        public override void TearDown()
        {
            base.TearDown();

            MockRunContext.Reset();
            MockFrameworkHandle.Reset();
            MockVsLogger.Reset();

            foreach (IDisposable disposable in _disposables)
            {
                disposable.Dispose();
            }
            _disposables.Clear();
        }

        /// <summary>
        /// Creates a copy of a sample test DLL (with PDB, dependencies and helper files) in a new temporary directory,
        /// which is deleted after the test. Tests which run tests must use copies: test execution writes a test
        /// durations file next to the test DLL.
        /// </summary>
        protected SampleCopy CopySample(string sampleDll, string targetFileName = null, bool copyPdb = true, bool copyDependencies = true)
        {
            SampleCopy copy = SampleCopy.Create(sampleDll, targetFileName, copyPdb, copyDependencies);
            _disposables.Add(copy);
            return copy;
        }

        /// <summary>Creates a temporary directory which is deleted after the test.</summary>
        protected TemporaryDirectory CreateTemporaryDirectory()
        {
            var directory = new TemporaryDirectory();
            _disposables.Add(directory);
            return directory;
        }

        /// <summary>
        /// Makes <see cref="MockRunContext"/> provide a test case filter (see <see cref="FakeTestCaseFilterExpression"/>
        /// for the syntax), e.g. <c>FullyQualifiedName=TaefSamples.TestMath.AddPasses</c> or <c>Owner=ClassOwner</c>.
        /// </summary>
        protected void SetupTestCaseFilter(string filter)
        {
            MockRunContext
                .Setup(rc => rc.GetTestCaseFilter(It.IsAny<IEnumerable<string>>(), It.IsAny<Func<string, TestProperty>>()))
                .Returns((IEnumerable<string> supportedProperties, Func<string, TestProperty> propertyProvider) =>
                    FakeTestCaseFilterExpression.Create(filter, supportedProperties, propertyProvider));
        }

        /// <returns>The results recorded at <see cref="MockFrameworkHandle"/> (in the order of recording).</returns>
        protected IList<VsTestResult> GetRecordedResults()
        {
            return MockFrameworkHandle.Invocations
                .Where(i => i.Method.Name == nameof(ITestExecutionRecorder.RecordResult))
                .Select(i => (VsTestResult)i.Arguments[0])
                .ToList();
        }

        /// <returns>The single result recorded at <see cref="MockFrameworkHandle"/> for the test with TAEF name <paramref name="taefName"/>.</returns>
        protected VsTestResult GetRecordedResult(string taefName)
        {
            List<VsTestResult> results = GetRecordedResults().Where(r => r.TestCase.DisplayName == taefName).ToList();
            if (results.Count != 1)
                Assert.Fail($"Expected exactly one result for test '{taefName}', but found {results.Count}");
            return results[0];
        }

        /// <returns>
        /// Run settings (as provided by the VsTest framework) containing the adapter settings given by
        /// <paramref name="taefTestAdapterSettingsXml"/>, e.g. <c>&lt;TaefTestAdapterSettings&gt;&lt;SolutionSettings&gt;&lt;Settings&gt;...</c>.
        /// </returns>
        protected static Mock<IRunSettings> CreateRunSettings(string taefTestAdapterSettingsXml)
        {
            var provider = new RunSettingsProvider();
            using (var reader = XmlReader.Create(new StringReader(taefTestAdapterSettingsXml)))
            {
                provider.Load(reader);
            }

            var mockRunSettings = new Mock<IRunSettings>();
            mockRunSettings.Setup(rs => rs.GetSettings(TaefConstants.SettingsName)).Returns(provider);
            mockRunSettings.Setup(rs => rs.SettingsXml).Returns($"<RunSettings>{taefTestAdapterSettingsXml}</RunSettings>");
            return mockRunSettings;
        }

        /// <returns><c>&lt;TaefTestAdapterSettings&gt;</c> element with the given solution settings (e.g. <c>&lt;RunInProcess&gt;true&lt;/RunInProcess&gt;</c>).</returns>
        protected static string CreateSettingsXml(string solutionSettings, string projectSettings = "")
        {
            return $"<{TaefConstants.SettingsName}><SolutionSettings><Settings>{solutionSettings}</Settings></SolutionSettings><ProjectSettings>{projectSettings}</ProjectSettings></{TaefConstants.SettingsName}>";
        }

        /// <returns>The VS test cases of <paramref name="testCases"/> whose TAEF names are <paramref name="taefNames"/>.</returns>
        protected static IList<VsTestCase> ToVsTestCases(IEnumerable<Model.TestCase> testCases, params string[] taefNames)
        {
            List<Model.TestCase> selected = testCases.Where(tc => taefNames.Contains(tc.FullyQualifiedName)).ToList();
            if (selected.Count != taefNames.Length)
                Assert.Fail($"Expected tests {string.Join(", ", taefNames)}, found {string.Join(", ", selected.Select(tc => tc.FullyQualifiedName))}");
            return selected.Select(tc => tc.ToVsTestCase()).ToList();
        }

    }

}
