// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using TaefTestAdapter.Model;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter.Scheduling
{
    /// <summary>
    /// Distributes tests to threads such that all threads run about the same number of tests (tests of a class stay
    /// together where possible).
    /// </summary>
    public class NumberBasedTestsSplitter : ITestsSplitter
    {
        private readonly IEnumerable<TestCase> _testcasesToRun;
        private readonly SettingsWrapper _settings;


        public NumberBasedTestsSplitter(IEnumerable<TestCase> testcasesToRun, SettingsWrapper settings)
        {
            _settings = settings;
            _testcasesToRun = testcasesToRun;
        }


        public List<List<TestCase>> SplitTestcases()
        {
            int nrOfThreadsToUse = Math.Min(_settings.MaxNrOfThreads, _testcasesToRun.Count());
            var splitTestCases = new List<TestCase>[nrOfThreadsToUse];
            for (int i = 0; i < nrOfThreadsToUse; i++)
            {
                splitTestCases[i] = new List<TestCase>();
            }

            int testcaseCounter = 0;
            foreach (TestCase testCase in _testcasesToRun)
            {
                splitTestCases[testcaseCounter++ % nrOfThreadsToUse].Add(testCase);
            }

            return splitTestCases.ToList();
        }

    }

}