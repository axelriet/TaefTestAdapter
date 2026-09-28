// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// Checks that the extension can be loaded by every Visual Studio version it can be installed into (see
    /// <c>Packaging\source.extension.vsixmanifest</c>).
    /// </summary>
    [TestClass]
    public class VsixManifestTests
    {
        private const string VisualStudioAssemblyPrefix = "Microsoft.VisualStudio.";

        private static string VsixManifestFile => Path.Combine(TestResources.AdapterSolutionDir, "Packaging", "source.extension.vsixmanifest");

        /// <summary>
        /// Visual Studio provides its assemblies (e.g. Microsoft.VisualStudio.Threading) with a binding redirect from all
        /// lower versions to the version it ships (e.g. <c>oldVersion="12.0.0.0-18.7.0.0" newVersion="18.7.0.0"</c>). An
        /// extension compiled against a newer SDK than the Visual Studio it is running in can therefore not be loaded
        /// (FileLoadException), although the VSIX installer accepts it: the assemblies of the extension must not reference
        /// Visual Studio assemblies newer than the oldest Visual Studio version the extension can be installed into.
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void VsPackageAssembly_ReferencesNoVisualStudioAssemblyNewerThanMinimumInstallationTarget()
        {
            Version minimumVisualStudioVersion = GetMinimumVisualStudioVersion();

            var visualStudioReferences = GetExtensionAssemblies()
                .SelectMany(a => a.GetReferencedAssemblies().Select(r => new { Assembly = a.GetName().Name, Reference = r }))
                .Where(r => r.Reference.Name.StartsWith(VisualStudioAssemblyPrefix, StringComparison.Ordinal))
                .ToList();

            visualStudioReferences.Should().Contain(r => r.Reference.Name == "Microsoft.VisualStudio.Threading");
            visualStudioReferences.Should().Contain(r => r.Reference.Name == "Microsoft.VisualStudio.Shell.15.0");
            foreach (var reference in visualStudioReferences)
            {
                new Version(reference.Reference.Version.Major, reference.Reference.Version.Minor).Should().BeLessOrEqualTo(
                    minimumVisualStudioVersion,
                    $"{reference.Assembly} references {reference.Reference.FullName}, which Visual Studio {minimumVisualStudioVersion} " +
                    $"(the minimum version of the InstallationTargets of {VsixManifestFile}) does not provide");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VsixManifest_InstallationTargetsAndPrerequisites_SupportVisualStudio2026And2022()
        {
            GetMinimumVisualStudioVersion().Should().Be(new Version(17, 0));

            XDocument manifest = XDocument.Load(VsixManifestFile);
            XNamespace ns = manifest.Root.Name.Namespace;
            manifest.Descendants(ns + "InstallationTarget").Select(t => (string)t.Attribute("Version"))
                .Should().NotBeEmpty().And.OnlyContain(v => v == "[17.0,19.0)");
        }

        /// <returns>
        /// The lower bound of the version ranges of the manifest's InstallationTargets and Prerequisites (which must all
        /// have the same, inclusive lower bound).
        /// </returns>
        private static Version GetMinimumVisualStudioVersion()
        {
            XDocument manifest = XDocument.Load(VsixManifestFile);
            XNamespace ns = manifest.Root.Name.Namespace;
            List<string> versionRanges = manifest.Descendants(ns + "InstallationTarget")
                .Concat(manifest.Descendants(ns + "Prerequisite"))
                .Select(e => (string)e.Attribute("Version"))
                .ToList();
            versionRanges.Should().HaveCountGreaterThan(1);

            var lowerBounds = new HashSet<Version>();
            foreach (string versionRange in versionRanges)
            {
                Match match = Regex.Match(versionRange ?? "", @"^\[(?<min>\d+\.\d+(\.\d+)*),");
                match.Success.Should().BeTrue($"version range '{versionRange}' should have an inclusive lower bound");
                lowerBounds.Add(new Version(match.Groups["min"].Value));
            }

            lowerBounds.Should().ContainSingle("all InstallationTargets and Prerequisites should require the same minimum Visual Studio version");
            return lowerBounds.Single();
        }

        /// <returns>The VS package's assembly and the adapter assemblies it references (all of them are loaded by Visual Studio).</returns>
        private static IList<Assembly> GetExtensionAssemblies()
        {
            var assemblies = new List<Assembly> { typeof(TaefTestAdapterPackage).Assembly };
            for (int i = 0; i < assemblies.Count; i++)
            {
                foreach (AssemblyName reference in assemblies[i].GetReferencedAssemblies())
                {
                    if (reference.Name.StartsWith("TaefTestAdapter.", StringComparison.Ordinal)
                        && assemblies.All(a => a.GetName().Name != reference.Name))
                    {
                        assemblies.Add(Assembly.Load(reference));
                    }
                }
            }

            assemblies.Select(a => a.GetName().Name).Should().Contain(new[]
            {
                "TaefTestAdapter.VsPackage", "TaefTestAdapter.TestAdapter", "TaefTestAdapter.Core", "TaefTestAdapter.Common"
            });
            return assemblies;
        }
    }
}
