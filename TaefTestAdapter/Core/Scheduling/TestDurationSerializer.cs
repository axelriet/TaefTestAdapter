// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

#pragma warning disable IDE0017 // Simplify object initialization

using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace TaefTestAdapter.Scheduling
{
    /// <summary>
    /// Content of a test durations file <c>&lt;testdll&gt;.taef.testdurations</c>.
    /// </summary>
    [Serializable]
    [XmlRoot]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public class TaefTestDurations
    {
        /// <summary>Full path of the test DLL.</summary>
        public string TestDll { get; set; }
        /// <summary>
        /// The durations of the tests of the test DLL.
        /// </summary>
        public List<TestDuration> TestDurations { get; set; } = new List<TestDuration>();
    }

    /// <summary>
    /// The duration of a test in ms.
    /// </summary>
    [Serializable]
    public struct TestDuration
    {
        /// <summary>
        /// Creates the duration of <paramref name="test"/>.
        /// </summary>
        public TestDuration(string test, int duration)
        {
            Test = test;
            Duration = duration;
        }

        /// <summary>
        /// The TAEF name of the test.
        /// </summary>
        [XmlAttribute]
        public string Test { get; set; }

        /// <summary>
        /// The duration in ms.
        /// </summary>
        [XmlAttribute]
        public int Duration { get; set; }
    }


    /// <summary>
    /// Thrown if a test durations file can not be read.
    /// </summary>
    [Serializable]
    public class InvalidTestDurationsException : Exception
    {
        public InvalidTestDurationsException() { }
        public InvalidTestDurationsException(string message) : base(message) { }
        public InvalidTestDurationsException(string message, Exception inner) : base(message, inner) { }
        protected InvalidTestDurationsException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Reads and writes the durations of the tests of a test DLL from/to the file
    /// <c>&lt;testdll&gt;</c><see cref="TaefConstants.DurationsExtension"/> (used for splitting the tests among threads
    /// if tests are executed in parallel).
    /// </summary>
    public class TestDurationSerializer
    {
        private static object Lock { get; } = new object();

        private readonly XmlSerializer _serializer = new XmlSerializer(typeof(TaefTestDurations));


        public IDictionary<TestCase, int> ReadTestDurations(IEnumerable<TestCase> testcases)
        {
            IDictionary<string, List<TestCase>> groupedTestcases = testcases.GroupByTestDll();
            var durations = new Dictionary<TestCase, int>();
            foreach (string testDll in groupedTestcases.Keys)
            {
                durations = durations.Union(ReadTestDurations(testDll, groupedTestcases[testDll])).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            return durations;
        }

        public void UpdateTestDurations(IEnumerable<TestResult> testResults)
        {
            IDictionary<string, List<TestResult>> groupedTestcases = GroupTestResultsByTestDll(testResults);
            foreach (string testDll in groupedTestcases.Keys)
            {
                // one lock for all durations files (writes are rare: once per test DLL and test run)
                lock (Lock)
                {
                    UpdateTestDurations(testDll, groupedTestcases[testDll]);
                }
            }
        }


        private IDictionary<TestCase, int> ReadTestDurations(string testDll, List<TestCase> testcases)
        {
            var durations = new Dictionary<TestCase, int>();
            string durationsFile = GetDurationsFile(testDll);
            if (!File.Exists(durationsFile))
            {
                return durations;
            }

            TaefTestDurations container;
            lock (Lock)
            {
                container = LoadTestDurations(durationsFile);
            }

            IDictionary<string, TestDuration> durationsMap = container.TestDurations.ToDictionary(x => x.Test, x => x);
            foreach (TestCase testcase in testcases)
            {
                if (durationsMap.TryGetValue(testcase.FullyQualifiedName, out var pair))
                    durations.Add(testcase, pair.Duration);
            }

            return durations;
        }

        private void UpdateTestDurations(string testDll, List<TestResult> testresults)
        {
            string durationsFile = GetDurationsFile(testDll);
            TaefTestDurations container = null;
            if (File.Exists(durationsFile))
            {
                try
                {
                    container = LoadTestDurations(durationsFile);
                }
                catch
                { }
            }
            if (container == null)
                container = new TaefTestDurations();
            container.TestDll = Path.GetFullPath(testDll);

            IDictionary<string, TestDuration> durations = container.TestDurations.ToDictionary(x => x.Test, x => x);
            foreach (TestResult testResult in 
                testresults.Where(tr => tr.Outcome == TestOutcome.Passed || tr.Outcome == TestOutcome.Failed))
            {
                durations[testResult.TestCase.FullyQualifiedName] =
                    new TestDuration(testResult.TestCase.FullyQualifiedName, GetDuration(testResult));
            }

            container.TestDurations.Clear();
            container.TestDurations.AddRange(durations.Values);

            SaveTestDurations(container, durationsFile);
        }

        private TaefTestDurations LoadTestDurations(string durationsFile)
        {
            var schemaSet = new XmlSchemaSet();
            var schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TaefTestDurations.xsd");
            var schemaSettings = new XmlReaderSettings();
            schemaSettings.XmlResolver = null;
            schemaSet.Add(null, XmlReader.Create(schemaStream, schemaSettings));

            var settings = new XmlReaderSettings();
            settings.Schemas = schemaSet;
            settings.ValidationType = ValidationType.Schema;
            settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
            settings.XmlResolver = null;
            settings.ValidationEventHandler += (object o, ValidationEventArgs e) => throw e.Exception;
            try
            {
                using (var reader = XmlReader.Create(durationsFile, settings))
                {
                    return _serializer.Deserialize(reader) as TaefTestDurations;
                }
            }
            catch (InvalidOperationException e) when (e.InnerException is XmlSchemaValidationException || e.InnerException is XmlException)
            {
                throw new InvalidTestDurationsException($"Invalid file {durationsFile}. {e.InnerException.Message}", e.InnerException);
            }
            catch (Exception e) when (e is XmlException || e is IOException || e is UnauthorizedAccessException)
            {
                throw new InvalidTestDurationsException($"Could not read file {durationsFile}. {e.Message}", e);
            }
        }

        private void SaveTestDurations(TaefTestDurations durations, string durationsFile)
        {
            using (var fileStream = new StreamWriter(durationsFile))
            {
                _serializer.Serialize(fileStream, durations);
            }
        }

        private IDictionary<string, List<TestResult>> GroupTestResultsByTestDll(IEnumerable<TestResult> testresults)
        {
            Dictionary<string, List<TestResult>> groupedTestResults = new Dictionary<string, List<TestResult>>();
            foreach (TestResult testResult in testresults)
            {
                List<TestResult> group;
                if (groupedTestResults.ContainsKey(testResult.TestCase.Source))
                {
                    group = groupedTestResults[testResult.TestCase.Source];
                }
                else
                {
                    group = new List<TestResult>();
                    groupedTestResults.Add(testResult.TestCase.Source, group);
                }
                group.Add(testResult);
            }
            return groupedTestResults;
        }

        private int GetDuration(TestResult testResult)
        {
            return (int)Math.Ceiling(testResult.Duration.TotalMilliseconds);
        }

        private string GetDurationsFile(string testDll)
        {
            return testDll + TaefConstants.DurationsExtension;
        }

    }

}