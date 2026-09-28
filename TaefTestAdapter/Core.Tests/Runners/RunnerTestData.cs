// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using TaefTestAdapter.Helpers;
using TaefTestAdapter.Model;
using TaefTestAdapter.TestAdapter.Helpers;

namespace TaefTestAdapter.Runners
{
    /// <summary>
    /// Test data and helpers for the tests of the runners.
    /// </summary>
    public static class RunnerTestData
    {
        /// <returns>
        /// Test cases of <paramref name="testDll"/> for those of <paramref name="allNames"/> which are contained in
        /// <paramref name="namesToRun"/> (in the order of <paramref name="allNames"/>), with meta data computed as the
        /// discoverer does it (number of tests of the TAEF class among <paramref name="allNames"/>, number of all tests).
        /// Tests contained in <paramref name="ignoredNames"/> get the trait Ignore=true.
        /// </returns>
        public static List<TestCase> CreateTestCases(string testDll, IEnumerable<string> namesToRun, IEnumerable<string> allNames,
            IEnumerable<string> ignoredNames = null)
        {
            var toRun = new HashSet<string>(namesToRun);
            var ignored = new HashSet<string>(ignoredNames ?? Enumerable.Empty<string>());
            List<string> all = allNames.ToList();
            Dictionary<string, int> nrOfTestsPerClass = all
                .GroupBy(TaefNames.GetClassName)
                .ToDictionary(g => g.Key, g => g.Count());

            var testCases = new List<TestCase>();
            foreach (string name in all.Where(toRun.Contains))
            {
                var testCase = new TestCase(name, testDll, name, "", 0);
                if (ignored.Contains(name))
                    testCase.Traits.Add(new Trait(TaefConstants.IgnoreProperty, "true"));
                testCase.Properties.Add(new TestCaseMetaDataProperty(nrOfTestsPerClass[TaefNames.GetClassName(name)], all.Count));
                testCases.Add(testCase);
            }
            return testCases;
        }

        /// <returns>Test cases for all of <paramref name="names"/> (i.e., all tests of the test DLL are run).</returns>
        public static List<TestCase> CreateTestCases(string testDll, params string[] names)
            => CreateTestCases(testDll, names, names);

        /// <returns>
        /// The console output TE.exe would print if the tests <paramref name="results"/> (name, TAEF result) were run:
        /// banner, one group per test, and the summary line.
        /// </returns>
        public static List<string> CreateTeOutput(params (string Name, string Result)[] results)
        {
            var lines = new List<string> { "Test Authoring and Execution Framework v10.104k for x64" };
            foreach ((string name, string result) in results)
            {
                lines.Add("");
                lines.Add("StartGroup: " + name);
                if (result == "Failed")
                    lines.Add($@"Error: Verify: AreEqual(1, 2) - Values (1, 2) [File: C:\src\Tests.cpp, Function: {TaefNames.GetBaseName(name)}, Line: 42]");
                lines.Add($"EndGroup: {name} [{result}]");
            }
            lines.Add("");
            lines.Add(GetSummaryLine(results.Select(r => r.Result).ToList()));
            return lines;
        }

        public static string GetSummaryLine(IList<string> taefResults)
        {
            int Count(string result) => taefResults.Count(r => r == result);
            return $"Summary: Total={taefResults.Count}, Passed={Count("Passed")}, Failed={Count("Failed")}, Blocked={Count("Blocked")}, Not Run={Count("NotRun")}, Skipped={Count("Skipped")}";
        }
    }

    /// <summary>
    /// Finds processes started by the test process (e.g. TE.exe) and their child processes (e.g. TE.ProcessHost.exe).
    /// </summary>
    public static class ProcessTree
    {
        /// <returns>Ids of the running processes named <paramref name="processName"/> (without .exe) whose parent is <paramref name="parentId"/>.</returns>
        public static IList<int> GetChildProcessIds(int parentId, string processName)
        {
            var result = new List<int>();
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        if (ParentProcessUtils.GetParentProcessId(process.Id) == parentId)
                            result.Add(process.Id);
                    }
                    catch (Exception)
                    {
                        // process has exited meanwhile
                    }
                }
            }
            return result;
        }

        /// <returns>Ids of the TE.exe processes started by the current process.</returns>
        public static IList<int> GetTeProcessIds()
            => GetChildProcessIds(Process.GetCurrentProcess().Id, "TE");

        /// <returns>Ids of the TE.ProcessHost.exe processes started by the TE.exe processes <paramref name="teProcessIds"/>.</returns>
        public static IList<int> GetTestHostProcessIds(IEnumerable<int> teProcessIds)
            => teProcessIds.SelectMany(id => GetChildProcessIds(id, "TE.ProcessHost")).ToList();

        /// <returns>True if the process has exited (or does not exist anymore) within <paramref name="timeoutInMs"/>.</returns>
        public static bool HasExited(int processId, int timeoutInMs = 5000)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return process.WaitForExit(timeoutInMs);
                }
            }
            catch (ArgumentException)
            {
                // not running
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        /// <summary>Waits until <paramref name="condition"/> is true (polling), at most <paramref name="timeoutInMs"/>.</summary>
        /// <returns>The final value of the condition.</returns>
        public static bool WaitFor(Func<bool> condition, int timeoutInMs)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!condition())
            {
                if (stopwatch.ElapsedMilliseconds > timeoutInMs)
                    return condition();
                Thread.Sleep(20);
            }
            return true;
        }
    }

}
