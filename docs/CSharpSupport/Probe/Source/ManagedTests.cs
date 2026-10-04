using System;
using System.Threading.Tasks;
using WEX.Logging.Interop;
using WEX.TestExecution;
using WEX.TestExecution.Markup;

namespace Contoso.Managed.Tests
{
    [TestClass]
    public class BasicTests
    {
        public TestContext TestContext { get; set; }

        [AssemblyInitialize]
        public static void AssemblySetup(TestContext context) { Log.Comment("AssemblySetup"); }

        [ClassInitialize]
        public static void ClassSetup(TestContext context) { Log.Comment("ClassSetup"); }

        [TestMethod]
        [TestProperty("Priority", "1")]
        [TestProperty("Owner", "contoso")]
        public void Passing()
        {
            Log.Comment("Passing test");
            Verify.AreEqual(2, 1 + 1);
        }

        [TestMethod]
        public void Failing()
        {
            Verify.AreEqual(3, 1 + 1, "deliberate failure");
        }

        [TestMethod]
        public void Throwing()
        {
            throw new InvalidOperationException("boom");
        }

        [TestMethod]
        [Ignore]
        public void Ignored() { }

        [TestMethod]
        [TestProperty("Data:Value", "{1,2,3}")]
        public void LightweightData()
        {
            int value = (int)TestContext.DataRow["Value"];
            Log.Comment("Value=" + value);
            Verify.IsTrue(value != 2, "row 2 fails");
        }

        [TestMethod]
        [DataSource("Table:ManagedTestsData.xml#Rows")]
        public void TableData()
        {
            Log.Comment("Name=" + TestContext.DataRow["Name"]);
        }

        [TestMethod]
        public async Task AsyncMethod()
        {
            await Task.Delay(1);
            Verify.IsTrue(true);
        }

        [TestMethod]
        public void Overloaded() { }
    }

    public class Outer
    {
        [TestClass]
        public class Nested
        {
            [TestMethod]
            public void InNested() { Verify.IsTrue(true); }
        }
    }

    [TestClass]
    [TestProperty("Data:Mode", "{fast,slow}")]
    public class DataDrivenClass
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void InDataClass() { Log.Comment("Mode=" + TestContext.DataRow["Mode"]); }
    }
}

[TestClass]
public class GlobalNamespaceTests
{
    [TestMethod]
    public void InGlobal() { Verify.IsTrue(true); }
}
