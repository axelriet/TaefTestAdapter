// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Assertions;
using TaefTestAdapter.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Settings
{
    [TestClass]
    public class HelperFilesCacheTests : TestsBase
    {
        [TestMethod]
        [TestCategory(Unit)]
        public void GetHelperFile_TestDll_ReturnsFileWithTaefExtension()
        {
            HelperFilesCache.GetHelperFile(@"C:\tests\My_taef.dll").Should().Be(@"C:\tests\My_taef.dll.taef_settings_helper");
            HelperFilesCache.HelperFileEnding.Should().Be(".taef_settings_helper");
            HelperFilesCache.SettingsSeparator.Should().Be("::TAEF::");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_NoFile_EmptyDictionary()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.GetPath("My_taef.dll");
                HelperFilesCache.GetHelperFile(testDll).AsFileInfo().Should().NotExist();

                var cache = new HelperFilesCache(MockLogger.Object);
                var map = cache.GetReplacementsMap(testDll);

                map.Should().BeEmpty();
                MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_EmptyString_EmptyDictionary()
        {
            DoTest("", map =>
            {
                map.Should().BeEmpty();
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_InvalidString_EmptyDictionary()
        {
            DoTest("Foo", map =>
            {
                map.Should().BeEmpty();
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_SingleValue_ProperDictionary()
        {
            DoTest("Foo=Bar", map =>
            {
                map.Should().HaveCount(1);
                map.Should().Contain(new KeyValuePair<string, string>("Foo", "Bar"));
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_TwoValues_ProperDictionary()
        {
            DoTest($"Placeholder1=value1{HelperFilesCache.SettingsSeparator}Placeholder2=value2", map =>
            {
                map.Should().HaveCount(2);
                map.Should().Contain(new KeyValuePair<string, string>("Placeholder1", "value1"));
                map.Should().Contain(new KeyValuePair<string, string>("Placeholder2", "value2"));
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_SingleWithEmptyValue_ProperDictionary()
        {
            DoTest("Placeholder1=", map =>
            {
                map.Should().HaveCount(1);
                map.Should().Contain(new KeyValuePair<string, string>("Placeholder1", ""));
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_ValueWithEqualSignsAndSpaces_ProperDictionary()
        {
            DoTest($"Path=C:\\My Dir\\x=y{HelperFilesCache.SettingsSeparator}Other= a b ", map =>
            {
                map.Should().HaveCount(2);
                map["Path"].Should().Be("C:\\My Dir\\x=y");
                map["Other"].Should().Be(" a b");
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_SingleWithTrailingSeparator_ProperDictionary()
        {
            DoTest($"Ph=V{HelperFilesCache.SettingsSeparator}", map =>
            {
                map.Should().HaveCount(1);
                map.Should().Contain(new KeyValuePair<string, string>("Ph", "V"));
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_OnlySeparator_ProperDictionary()
        {
            DoTest($"{HelperFilesCache.SettingsSeparator}", map =>
            {
                map.Should().BeEmpty();
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_OnlyTwoSeparators_ProperDictionary()
        {
            DoTest($"{HelperFilesCache.SettingsSeparator}{HelperFilesCache.SettingsSeparator}", map =>
            {
                map.Should().BeEmpty();
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_OtherSeparator_IsNotASeparator()
        {
            DoTest("A=1::OTHER::B=2", map =>
            {
                map.Should().HaveCount(1);
                map["A"].Should().Be("1::OTHER::B=2");
            });
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_FileChangesAfterFirstAccess_CachedValuesAreReturned()
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.GetPath("My_taef.dll");
                File.WriteAllText(HelperFilesCache.GetHelperFile(testDll), "Foo=Bar");

                var cache = new HelperFilesCache(MockLogger.Object);
                cache.GetReplacementsMap(testDll)["Foo"].Should().Be("Bar");

                File.WriteAllText(HelperFilesCache.GetHelperFile(testDll), "Foo=Baz");
                cache.GetReplacementsMap(testDll)["Foo"].Should().Be("Bar");
                new HelperFilesCache(MockLogger.Object).GetReplacementsMap(testDll)["Foo"].Should().Be("Baz");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetReplacementsMap_DuplicateKeys_WarningIsLoggedAndEmptyDictionaryIsReturned()
        {
            DoTest($"A=1{HelperFilesCache.SettingsSeparator}A=2", map =>
            {
                map.Should().BeEmpty();
                MockLogger.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains(HelperFilesCache.HelperFileEnding))), Times.Once);
            });
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void GetReplacementsMap_HelperFileOfSample_ContainsSolutionSettingsAndTheTarget()
        {
            // written by the post-build step of HelperFileTests (see SampleTests\README.md)
            string testDll = TestResources.HelperFileTests_ReleaseX86;
            HelperFilesCache.GetHelperFile(testDll).AsFileInfo().Should().Exist();

            var map = new HelperFilesCache(MockLogger.Object).GetReplacementsMap(testDll);

            map["TheTarget"].Should().Be(TestResources.HelperFileTestsDll);
            map.Should().ContainKeys("SolutionPath", "SolutionDir", "PlatformName", "ConfigurationName", "TheWorkingDirectory");
            map["ConfigurationName"].Should().Be("Release");
            map["PlatformName"].Should().Be("Win32");
            MockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Never);
        }

        private void DoTest(string content, Action<IDictionary<string, string>> assertions)
        {
            using (var directory = new TemporaryDirectory())
            {
                string testDll = directory.GetPath("My_taef.dll");
                string helperFile = HelperFilesCache.GetHelperFile(testDll);
                File.WriteAllText(helperFile, content);

                var cache = new HelperFilesCache(MockLogger.Object);
                var map = cache.GetReplacementsMap(testDll);
                assertions(map);
            }
        }
    }
}
