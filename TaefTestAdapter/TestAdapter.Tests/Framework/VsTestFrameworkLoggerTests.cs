// This file has been modified for TAEF support.

using System.Text.RegularExpressions;
using FluentAssertions;
using TaefTestAdapter.Common;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Framework
{
    [TestClass]
    public class VsTestFrameworkLoggerTests : TestAdapterTestsBase
    {

        private VsTestFrameworkLogger _logger;

        [TestInitialize]
        public override void SetUp()
        {
            base.SetUp();

            _logger = new VsTestFrameworkLogger(MockVsLogger.Object, () => MockOptions.Object.OutputMode,
                () => MockOptions.Object.TimestampMode, () => MockOptions.Object.SeverityMode, () => MockOptions.Object.PrefixOutputWithTaef);
        }


        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_Null_NonEmptyString()
        {
            _logger.LogInfo(null);

            MockVsLogger.Verify(l => l.SendMessage(
                It.Is<TestMessageLevel>(tml => tml == TestMessageLevel.Informational),
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s))),
                Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_EmptyString_NonEmptyString()
        {
            _logger.LogInfo("");

            MockVsLogger.Verify(l => l.SendMessage(
                It.Is<TestMessageLevel>(tml => tml == TestMessageLevel.Informational),
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s))),
                Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_Whitespace_NonEmptyString()
        {
            _logger.LogInfo("\n");

            MockVsLogger.Verify(l => l.SendMessage(
                It.Is<TestMessageLevel>(tml => tml == TestMessageLevel.Informational),
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s))),
                Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogWarning_Foo_WarningAndFoo()
        {
            _logger.LogWarning("foo");

            MockVsLogger.Verify(l => l.SendMessage(
                It.Is<TestMessageLevel>(tml => tml == TestMessageLevel.Warning),
                It.Is<string>(s => s.Contains("Warning") && s.Contains("foo"))),
                Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogError_Foo_ErrorAndFoo()
        {
            _logger.LogError("foo");

            MockVsLogger.Verify(l => l.SendMessage(
                It.Is<TestMessageLevel>(tml => tml == TestMessageLevel.Error),
                It.Is<string>(s => s.Contains("ERROR") && s.Contains("foo"))),
                Times.Exactly(1));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_PrefixOutputWithTaef_MessageIsPrefixed()
        {
            MockOptions.Setup(o => o.PrefixOutputWithTaef).Returns(true);

            _logger.LogInfo("foo");
            _logger.LogWarning("bar");
            _logger.LogError("baz");

            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Informational, "[TAEF] foo"), Times.Once);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Warning, "[TAEF Warning] bar"), Times.Once);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Error, "[TAEF ERROR] baz"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_NoPrefixNoTimestampNoSeverity_MessageIsUnchanged()
        {
            MockOptions.Setup(o => o.SeverityMode).Returns(SeverityMode.DoNotPrintSeverity);

            _logger.LogInfo("foo");
            _logger.LogError("bar");

            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Informational, "foo"), Times.Once);
            MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Error, "bar"), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_PrintTimestamp_MessageHasTimestamp()
        {
            MockOptions.Setup(o => o.TimestampMode).Returns(TimestampMode.PrintTimestamp);
            MockOptions.Setup(o => o.PrefixOutputWithTaef).Returns(true);
            string message = null;
            MockVsLogger.Setup(l => l.SendMessage(It.IsAny<TestMessageLevel>(), It.IsAny<string>()))
                .Callback((TestMessageLevel level, string m) => message = m);

            _logger.LogWarning("foo");

            message.Should().MatchRegex(@"^\[TAEF \S.* Warning\] foo$");
            Regex.Match(message, @"^\[TAEF (?<timestamp>.*) Warning\] foo$").Groups["timestamp"].Value.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogInfo_AutomaticModes_DependOnVisualStudioVersion()
        {
            MockOptions.Setup(o => o.TimestampMode).Returns(TimestampMode.Automatic);
            MockOptions.Setup(o => o.SeverityMode).Returns(SeverityMode.Automatic);

            _logger.LogWarning("foo");

            // VS 2017 and later (and unknown versions) print timestamps and severity themselves
            if (VsVersionUtils.VsVersion.PrintsTimeStampAndSeverity())
                MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Warning, "foo"), Times.Once);
            else
                MockVsLogger.Verify(l => l.SendMessage(TestMessageLevel.Warning, It.Is<string>(s => s.StartsWith("[") && s.EndsWith(" Warning] foo"))), Times.Once);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DebugInfo_OutputModeInfo_NothingIsLogged()
        {
            MockOptions.Setup(o => o.OutputMode).Returns(OutputMode.Info);

            _logger.DebugInfo("foo");
            _logger.VerboseInfo("bar");

            MockVsLogger.Verify(l => l.SendMessage(It.IsAny<TestMessageLevel>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void LogError_Message_IsReturnedByGetMessages()
        {
            _logger.LogError("foo");
            _logger.LogWarning("bar");
            _logger.LogInfo("baz");

            _logger.GetMessages(Severity.Error).Should().ContainSingle().Which.Should().EndWith("foo");
            _logger.GetMessages(Severity.Error, Severity.Warning).Should().HaveCount(2);
        }

    }

}
