// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using TaefTestAdapter.TestAdapter.Helpers;

namespace TaefTestAdapter.TestAdapter.Framework
{

    /// <summary>
    /// Visual Studio versions (the value is the major version number). The Test Adapter for TAEF supports Visual Studio
    /// 2022 and later (see <see cref="VsVersionUtils.FirstSupportedVersion"/>); older versions are only listed to be
    /// able to tell users that their version is not supported.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum VsVersion
    {
        Unknown = -1, VS2012 = 11, VS2013 = 12, VS2015 = 14, VS2017 = 15, VS2019 = 16, VS2022 = 17, VS2026 = 18
    }

    /// <summary>
    /// Extension methods of <see cref="VsVersion"/>.
    /// </summary>
    public static class VsVersionExtensions
    {
        /// <returns>The year in the product name of <paramref name="version"/>, or 0 if it is not known.</returns>
        public static int Year(this VsVersion version)
        {
            switch (version)
            {
                case VsVersion.VS2012:
                    return 2012;
                case VsVersion.VS2013:
                    return 2013;
                case VsVersion.VS2015:
                    return 2015;
                case VsVersion.VS2017:
                    return 2017;
                case VsVersion.VS2019:
                    return 2019;
                case VsVersion.VS2022:
                    return 2022;
                case VsVersion.VS2026:
                    return 2026;
                default:
                    return 0;
            }
        }

        /// <returns>The version number of <paramref name="version"/> (<c>&lt;major&gt;.0</c>), or "0.0" if it is unknown.</returns>
        public static string VersionString(this VsVersion version)
        {
            return version == VsVersion.Unknown ? "0.0" : $"{(int)version}.0";
        }

        /// <returns>True if the Test Adapter for TAEF supports <paramref name="version"/> (Visual Studio 2022 and later).</returns>
        public static bool IsSupported(this VsVersion version)
        {
            return version >= VsVersionUtils.FirstSupportedVersion;
        }

        /// <summary>
        /// Old versions of Visual Studio lose test results if results are reported too quickly. This does not apply
        /// to the supported versions; unknown versions are assumed to be recent.
        /// </summary>
        public static bool NeedsToBeThrottled(this VsVersion version)
        {
            return version != VsVersion.Unknown && version < VsVersion.VS2017;
        }

        /// <summary>
        /// Recent versions of the VsTest framework print timestamp and severity of messages themselves. Unknown versions
        /// are assumed to be recent.
        /// </summary>
        public static bool PrintsTimeStampAndSeverity(this VsVersion version)
        {
            return version == VsVersion.Unknown || version >= VsVersion.VS2017;
        }
    }

    /// <summary>
    /// Determines the version of Visual Studio the adapter is running in. The version is taken from the product version
    /// of the first of the current process and its ancestors which is part of Visual Studio's test platform (testhost,
    /// vstest.console, ...) or Visual Studio itself (the major versions of VS and of the test platform shipped with it
    /// are the same, e.g. 18 for Visual Studio 2026). This works within Visual Studio's Test Explorer as well as for
    /// vstest.console.exe, and for 32-bit test hosts running within a 64-bit process tree.
    /// </summary>
    public static class VsVersionUtils
    {
        /// <summary>
        /// Processes whose product version is the version of Visual Studio: the test platform's test hosts
        /// (e.g. testhost.exe, testhost.x86.exe, testhost.net48.exe), vstest.console(.arm64).exe, the old
        /// vstest.discoveryengine/executionengine processes, and devenv.exe.
        /// </summary>
        private const string HostProcessPattern =
            @"^(?:testhost(?:\..+)?|vstest\.console(?:\..+)?|vstest\.(?:discoveryengine|executionengine)(?:\..+)?|devenv)\.exe$";
        private static readonly Regex HostProcessRegex = new Regex(HostProcessPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private const int MaxNrOfAncestorsToCheck = 10;

        public const VsVersion FirstSupportedVersion = VsVersion.VS2022;
        public const VsVersion LastKnownVersion = VsVersion.VS2026;

        /// <summary>The version of Visual Studio the adapter is running in, or <see cref="VsVersion.Unknown"/>.</summary>
        public static readonly VsVersion VsVersion;

        /// <summary>
        /// Description of the process the version has been determined from (file and product version), or null if the
        /// version could not be determined.
        /// </summary>
        public static readonly string VersionSource;

        static VsVersionUtils()
        {
            try
            {
                VsVersion = GetVsVersionFromProcess(out VersionSource);
            }
            catch (Exception)
            {
                VsVersion = VsVersion.Unknown;
                VersionSource = null;
            }
        }

        private static VsVersion GetVsVersionFromProcess(out string versionSource)
        {
            foreach (string image in GetImagesOfCurrentProcessAndAncestors())
            {
                if (!HostProcessRegex.IsMatch(Path.GetFileName(image)))
                    continue;

                string productVersion = FileVersionInfo.GetVersionInfo(image).ProductVersion;
                versionSource = $"{image}, product version {productVersion}";
                return GetVsVersionFromVersionString(productVersion);
            }

            versionSource = null;
            return VsVersion.Unknown;
        }

        private static IEnumerable<string> GetImagesOfCurrentProcessAndAncestors()
        {
            var visitedProcessIds = new HashSet<int>();
            int? processId = Process.GetCurrentProcess().Id;
            for (int i = 0; processId.HasValue && i <= MaxNrOfAncestorsToCheck && visitedProcessIds.Add(processId.Value); i++)
            {
                string image = ParentProcessUtils.GetProcessImageFileName(processId.Value);
                if (image != null)
                    yield return image;

                processId = ParentProcessUtils.GetParentProcessId(processId.Value);
            }
        }

        /// <param name="versionString">A product version such as <c>18.10.0-release-26404-02</c> or <c>17.14.36310.24</c>.</param>
        /// <returns>The Visual Studio version with the major version of <paramref name="versionString"/> (which might be
        /// a version not listed in <see cref="VsVersion"/>, e.g. of a Visual Studio release newer than
        /// <see cref="LastKnownVersion"/>), or <see cref="VsVersion.Unknown"/> if the string can not be parsed.</returns>
        public static VsVersion GetVsVersionFromVersionString(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
                return VsVersion.Unknown;

            versionString = versionString.Trim();
            for (int i = 0; i < versionString.Length; i++)
            {
                if (!char.IsDigit(versionString, i) && versionString[i] != '.')
                {
                    versionString = versionString.Substring(0, i);
                    break;
                }
            }
            versionString = versionString.TrimEnd('.');

            if (!versionString.Contains("."))
                versionString += ".0";

            if (!Version.TryParse(versionString, out Version version) || version.Major < (int)VsVersion.VS2012)
                return VsVersion.Unknown;

            return (VsVersion)version.Major;
        }
    }

}
