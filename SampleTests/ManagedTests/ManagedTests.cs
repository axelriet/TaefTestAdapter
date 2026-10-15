// This file has been added for TAEF support.

using System;
using System.Threading;
using System.Threading.Tasks;
using WEX.TestExecution;
using WEX.TestExecution.Markup;

namespace TaefSampleTests.Managed
{
    [TestClass]
    public class BasicTests
    {
        private static bool _assemblyReady;
        private static bool _classReady;
        private bool _testReady;

        [AssemblyInitialize, TestProperty("ModuleProperty", "ManagedModule")]
        public static void AssemblySetup(object context) { _assemblyReady = true; }

        [AssemblyCleanup]
        public static void AssemblyCleanup() { _assemblyReady = false; }

        [ClassInitialize, TestProperty("ClassProperty", "ManagedClass")]
        public static void ClassSetup(object context) { _classReady = true; }

        [ClassCleanup]
        public static void ClassCleanup() { _classReady = false; }

        [TestInitialize]
        public void Setup() { _testReady = true; }

        [TestCleanup]
        public void Cleanup() { _testReady = false; }

        [TestMethod, Owner("ManagedTests"), Priority(1), TestProperty("Category", "Managed")]
        public void PassingTest() { Verify.IsTrue(_assemblyReady && _classReady && _testReady); }

        [TestMethod]
        public void FailingTest() { throw new InvalidOperationException("Managed failure for adapter validation"); }

        [TestMethod, Ignore]
        public void IgnoredTest() { throw new InvalidOperationException("An ignored managed test was executed"); }

        [TestMethod]
        public async Task AsyncPassingTest() { await Task.Delay(1); Verify.IsTrue(_testReady); }

        [TestMethod]
        public async Task AsyncFailingTest() { await Task.Delay(1); throw new InvalidOperationException("Managed async failure"); }

        [TestMethod, TestProperty("ThreadingModel", "MTA")]
        public void MtaTest() { Verify.AreEqual(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState()); }
    }

    [TestClass]
    public class DataTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod, TestProperty("Data:Value", "{first, second}")]
        public void LightweightData() { Verify.IsNotNull(TestContext.DataRow["Value"]); }

        [TestMethod, TestProperty("DataSource", "Table:ManagedData.xml#Rows")]
        public void TableData() { Verify.AreEqual("value", (string)TestContext.DataRow["Value"]); }
    }
}

[TestClass]
public class GlobalManagedTests
{
    [TestMethod]
    public void GlobalPassingTest() { Verify.IsTrue(true); }
}
