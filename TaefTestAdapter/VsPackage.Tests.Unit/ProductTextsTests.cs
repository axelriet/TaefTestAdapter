// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Common;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.VsPackage.OptionsPages;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// Checks the texts users see (VSIX manifest, NuGet package, Help/About and options category of the package, toolbar,
    /// output messages, options pages): one product name everywhere, and TAEF terminology.
    /// </summary>
    [TestClass]
    public class ProductTextsTests
    {
        private const string ProductName = "Test Adapter for TAEF";
        private const string ProductDescription =
            "Enables Visual Studio's testing tools with unit tests written for the Test Authoring and Execution Framework (TAEF).";
        private const string VsixFileName = "TaefTestAdapter.vsix";

        private static string VsixManifestFile => Path.Combine(TestResources.AdapterSolutionDir, "Packaging", "source.extension.vsixmanifest");
        private static string NuspecFile => Path.Combine(TestResources.AdapterSolutionDir, "Packaging", "VsPackage.nuspec");
        private static string VsctFile => Path.Combine(TestResources.AdapterSolutionDir, "VsPackage", "TaefTestAdapterPackage.vsct");
        private static string ResxFile => Path.Combine(TestResources.AdapterSolutionDir, "VsPackage", "Resources", "VSPackage.resx");

        [TestMethod]
        [TestCategory(Unit)]
        public void ProductName_IsIdenticalEverywhere()
        {
            GetVsixMetadata("DisplayName").Should().Be(ProductName);
            Strings.Instance.ExtensionName.Should().Be(ProductName);
            TaefTestAdapterPackage.OptionsCategoryName.Should().Be(ProductName);
            GetResxValue("110").Should().Be(ProductName, "resource 110 is the product name in Help/About");
            GetResxValue("120").Should().Be(ProductName, "resource 120 is the options category");
            GetNuspecMetadata("title").Should().Be(ProductName);

            XElement toolbar = GetVsctElements("Menu").Single(m => (string)m.Attribute("type") == "Toolbar");
            toolbar.Descendants().Single(e => e.Name.LocalName == "ButtonText").Value.Should().Be(ProductName);
            toolbar.Descendants().Single(e => e.Name.LocalName == "CommandName").Value.Should().Be(ProductName);

            Strings.Instance.TestDiscoveryStarting.Should().StartWith(ProductName + ": ");
            Strings.Instance.TestExecutionStarting.Should().StartWith(ProductName + ": ");
            Strings.Instance.TroubleShootingLink.Should().Contain(ProductName);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ProductDescription_IsIdenticalInVsixManifestAndHelpAbout()
        {
            GetVsixMetadata("Description").Should().Be(ProductDescription);
            GetResxValue("112").Should().Be(ProductDescription, "resource 112 is the product description in Help/About");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ToolbarButtons_AllHaveButtonTextAndToolTip()
        {
            IList<XElement> buttons = GetVsctElements("Button").ToList();
            buttons.Should().HaveCount(4);

            foreach (XElement button in buttons)
            {
                string id = (string)button.Attribute("id");
                XElement strings = button.Elements().Single(e => e.Name.LocalName == "Strings");
                string buttonText = strings.Elements().SingleOrDefault(e => e.Name.LocalName == "ButtonText")?.Value;
                string toolTipText = strings.Elements().SingleOrDefault(e => e.Name.LocalName == "ToolTipText")?.Value;

                buttonText.Should().NotBeNullOrWhiteSpace($"button {id} needs a text");
                toolTipText.Should().NotBeNullOrWhiteSpace($"button {id} needs a tooltip");
                toolTipText.Should().StartWith(buttonText.Split(' ')[0], $"the tooltip of button {id} starts with the name of its option");
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VsixManifest_ReleaseNotes_AreIncludedInVsix()
        {
            string releaseNotes = GetVsixMetadata("ReleaseNotes");
            releaseNotes.Should().Be("ReleaseNotes.txt");

            string vsix = Path.Combine(TestResources.PackagingDir, VsixFileName);
            if (!File.Exists(vsix))
                Assert.Inconclusive($"The VSIX has not been built: {vsix}");

            using (var archive = new ZipArchive(File.OpenRead(vsix), ZipArchiveMode.Read))
            {
                string[] entries = archive.Entries.Select(e => e.FullName).ToArray();
                entries.Should().Contain(new[] { releaseNotes, GetVsixMetadata("License"), "NOTICE.txt" });

                ZipArchiveEntry releaseNotesEntry = archive.GetEntry(releaseNotes);
                // ReSharper disable once PossibleNullReferenceException
                using (var reader = new StreamReader(releaseNotesEntry.Open()))
                {
                    reader.ReadToEnd().Should().StartWith("# Changelog").And.Contain(ProductName);
                }
            }
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void ProductTexts_UseTaefTerminology()
        {
            // the adapter runs the tests of TAEF test DLLs, which consist of classes and data-driven tests
            string[] forbiddenTerms = { "executable", "suite", "parameterized" };

            foreach (KeyValuePair<string, string> text in GetProductTexts())
            {
                foreach (string term in forbiddenTerms)
                {
                    (text.Value ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase).Should().Be(-1,
                        $"{text.Key} should use TAEF terminology instead of '{term}': \"{text.Value}\"");
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> GetProductTexts()
        {
            foreach (string element in new[] { "DisplayName", "Description", "Tags" })
                yield return Text($"VSIX manifest {element}", GetVsixMetadata(element));

            foreach (string element in new[] { "title", "description", "summary", "tags" })
                yield return Text($"NuGet package {element}", GetNuspecMetadata(element));

            XDocument resx = XDocument.Load(ResxFile);
            foreach (XElement data in resx.Root.Elements("data").Where(d => d.Attribute("type") == null))
                yield return Text($"VSPackage.resx {(string)data.Attribute("name")}", (string)data.Element("value"));

            foreach (XElement text in GetVsctElements("Strings").SelectMany(s => s.Elements()))
                yield return Text($"vsct {text.Name.LocalName}", text.Value);

            foreach (PropertyInfo property in typeof(IStrings).GetProperties())
                yield return Text($"Strings.{property.Name}", (string)property.GetValue(Strings.Instance));

            var pageTypes = typeof(TaefTestAdapterPackage).Assembly.GetTypes()
                .Where(t => typeof(NotifyingDialogPage).IsAssignableFrom(t) && !t.IsAbstract);
            foreach (Type pageType in pageTypes)
            {
                foreach (PropertyInfo option in OptionsPagesTests.GetOptions(pageType))
                {
                    yield return Text($"option {pageType.Name}.{option.Name} (category)", option.GetCustomAttribute<CategoryAttribute>()?.Category);
                    yield return Text($"option {pageType.Name}.{option.Name} (display name)", option.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName);
                    yield return Text($"option {pageType.Name}.{option.Name} (description)", option.GetCustomAttribute<DescriptionAttribute>()?.Description);
                }
            }
        }

        private static KeyValuePair<string, string> Text(string source, string text) => new KeyValuePair<string, string>(source, text);

        private static string GetVsixMetadata(string element)
        {
            XDocument manifest = XDocument.Load(VsixManifestFile);
            XNamespace ns = manifest.Root.Name.Namespace;
            return manifest.Root.Element(ns + "Metadata")?.Element(ns + element)?.Value;
        }

        private static string GetNuspecMetadata(string element)
        {
            XDocument nuspec = XDocument.Load(NuspecFile);
            return nuspec.Root.Elements().Single(e => e.Name.LocalName == "metadata")
                .Elements().SingleOrDefault(e => e.Name.LocalName == element)?.Value;
        }

        private static string GetResxValue(string name)
        {
            XDocument resx = XDocument.Load(ResxFile);
            return (string)resx.Root.Elements("data").Single(d => (string)d.Attribute("name") == name).Element("value");
        }

        private static IEnumerable<XElement> GetVsctElements(string localName)
        {
            return XDocument.Load(VsctFile).Descendants().Where(e => e.Name.LocalName == localName);
        }
    }
}
