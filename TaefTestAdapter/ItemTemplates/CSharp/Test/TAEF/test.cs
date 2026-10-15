using global::WEX.Logging.Interop;
using global::WEX.TestExecution;
using global::WEX.TestExecution.Markup;

namespace $taefnamespace$
{
    [TestClass]
    public class $taefclassname$
    {
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            Log.Comment("Class setup");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Log.Comment("Class cleanup");
        }

        [TestInitialize]
        public void MethodSetup()
        {
        }

        [TestCleanup]
        public void MethodCleanup()
        {
        }

        [TestMethod]
        public void Addition()
        {
            Verify.AreEqual(4, 2 + 2);
        }

        [TestMethod]
        [TestProperty("Owner", "Your name")]
        public void WithProperties()
        {
            Log.Comment("Properties are shown as traits in Test Explorer.");
            Verify.IsTrue(true);
        }

        [TestMethod]
        [TestProperty("Data:Values", "{1,2,3}")]
        public void DataDriven()
        {
            int value = global::System.Convert.ToInt32(TestContext.DataRow["Values"]);
            Verify.IsTrue(value > 0);
        }
    }
}
