// This file has been added for TAEF support.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.TestAdapter.Framework
{
    [TestClass]
    public class VsVersionTests
    {

        [TestMethod]
        [TestCategory(Unit)]
        public void GetVsVersionFromVersionString_ProductVersionsOfVsAndTestPlatform_AreParsedCorrectly()
        {
            // product versions of vstest.console.exe/testhost.exe (VS 2026 and VS 2022) and devenv.exe
            VsVersionUtils.GetVsVersionFromVersionString("18.10.0-release-26404-02").Should().Be(VsVersion.VS2026);
            VsVersionUtils.GetVsVersionFromVersionString("18.0.11018.127").Should().Be(VsVersion.VS2026);
            VsVersionUtils.GetVsVersionFromVersionString("17.14.36310.24").Should().Be(VsVersion.VS2022);
            VsVersionUtils.GetVsVersionFromVersionString("17.14.1+5a7fce5c5c").Should().Be(VsVersion.VS2022);
            VsVersionUtils.GetVsVersionFromVersionString("16.11.5").Should().Be(VsVersion.VS2019);
            VsVersionUtils.GetVsVersionFromVersionString("15.0").Should().Be(VsVersion.VS2017);
            VsVersionUtils.GetVsVersionFromVersionString("11.0.61030.0").Should().Be(VsVersion.VS2012);
            VsVersionUtils.GetVsVersionFromVersionString(" 17 ").Should().Be(VsVersion.VS2022);
            VsVersionUtils.GetVsVersionFromVersionString("18.").Should().Be(VsVersion.VS2026);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetVsVersionFromVersionString_NewerVersion_IsReturnedAsNumber()
        {
            VsVersion version = VsVersionUtils.GetVsVersionFromVersionString("19.0.1");

            ((int)version).Should().Be(19);
            version.IsSupported().Should().BeTrue();
            version.Year().Should().Be(0);
            version.VersionString().Should().Be("19.0");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetVsVersionFromVersionString_InvalidVersions_Unknown()
        {
            VsVersionUtils.GetVsVersionFromVersionString(null).Should().Be(VsVersion.Unknown);
            VsVersionUtils.GetVsVersionFromVersionString("").Should().Be(VsVersion.Unknown);
            VsVersionUtils.GetVsVersionFromVersionString("   ").Should().Be(VsVersion.Unknown);
            VsVersionUtils.GetVsVersionFromVersionString("abc").Should().Be(VsVersion.Unknown);
            VsVersionUtils.GetVsVersionFromVersionString("v18.0").Should().Be(VsVersion.Unknown);
            VsVersionUtils.GetVsVersionFromVersionString("10.0.40219.1").Should().Be(VsVersion.Unknown);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Year_KnownVersions_CorrectYear()
        {
            VsVersion.VS2012.Year().Should().Be(2012);
            VsVersion.VS2013.Year().Should().Be(2013);
            VsVersion.VS2015.Year().Should().Be(2015);
            VsVersion.VS2017.Year().Should().Be(2017);
            VsVersion.VS2019.Year().Should().Be(2019);
            VsVersion.VS2022.Year().Should().Be(2022);
            VsVersion.VS2026.Year().Should().Be(2026);
            VsVersion.Unknown.Year().Should().Be(0);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VersionString_Versions_MajorVersionDotZero()
        {
            VsVersion.VS2022.VersionString().Should().Be("17.0");
            VsVersion.VS2026.VersionString().Should().Be("18.0");
            VsVersion.Unknown.VersionString().Should().Be("0.0");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void IsSupported_Versions_OnlyVs2022AndLater()
        {
            VsVersionUtils.FirstSupportedVersion.Should().Be(VsVersion.VS2022);
            VsVersionUtils.LastKnownVersion.Should().Be(VsVersion.VS2026);

            VsVersion.Unknown.IsSupported().Should().BeFalse();
            VsVersion.VS2012.IsSupported().Should().BeFalse();
            VsVersion.VS2019.IsSupported().Should().BeFalse();
            VsVersion.VS2022.IsSupported().Should().BeTrue();
            VsVersion.VS2026.IsSupported().Should().BeTrue();
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void NeedsToBeThrottledAndPrintsTimeStampAndSeverity_Versions_OnlyOldVersionsAreSpecial()
        {
            foreach (VsVersion version in Enum.GetValues(typeof(VsVersion)).Cast<VsVersion>())
            {
                bool isOld = version != VsVersion.Unknown && version < VsVersion.VS2017;
                version.NeedsToBeThrottled().Should().Be(isOld, version.ToString());
                version.PrintsTimeStampAndSeverity().Should().Be(!isOld, version.ToString());
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VsVersion_RunningInVsTestFramework_IsSupportedVersion()
        {
            if (VsVersionUtils.VsVersion == VsVersion.Unknown)
                Assert.Inconclusive("Tests are not run by the VsTest framework of Visual Studio (e.g. by another test runner)");

            VsVersionUtils.VsVersion.IsSupported().Should().BeTrue(VsVersionUtils.VersionSource);
            VsVersionUtils.VersionSource.Should().MatchRegex(@"(?i)(testhost|vstest|devenv)[^\\]*\.exe, product version \d+\.");
        }

    }
}
