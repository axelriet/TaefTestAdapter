// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage
{
    /// <summary>
    /// Checks the metadata of the VSIX (<c>Packaging\source.extension.vsixmanifest</c>) and of the NuGet package
    /// (<c>Packaging\VsPackage.nuspec</c>) shown by the Visual Studio Marketplace, nuget.org and Visual Studio: publisher,
    /// links to the project, its source repository and the readme, and tags.
    /// </summary>
    [TestClass]
    public class PackageMetadataTests
    {
        /// <summary>
        /// The publisher of the extension on the Visual Studio Marketplace, which rejects packages with any other publisher
        /// (the VSIX Publisher and the authors of the NuGet package).
        /// </summary>
        private const string Publisher = "Axel Rietschin Software Development LLC";

        private const string ProjectUrl = "https://github.com/axelriet/TaefTestAdapter";
        private const string RepositoryUrl = "https://github.com/axelriet/TaefTestAdapter.git";
        private const string RepositoryBranch = "main";
        private const string ReleaseNotesUrl = "https://github.com/axelriet/TaefTestAdapter/blob/main/CHANGELOG.md";
        private const string Readme = "README.md";

        /// <summary>The adapter runs native TAEF test DLLs, which are written in C++ or C.</summary>
        private static readonly string[] SupportedTags = { "TAEF", "C++", "C" };
        private static readonly string[] UnsupportedLanguageTags = { "C#", "JScript", "VBScript" };

        /// <summary>The tags of the VSIX and of the NuGet package: the supported languages and the test and Visual Studio tags.</summary>
        private const string VsixTags = "Unit Test, C++, C, TAEF, Test Authoring and Execution Framework, TE.exe, Test Explorer, Testing";
        private const string NuGetTags = "TAEF C++ C native TE.exe WEX test adapter unit integration automated testing VisualStudio TestExplorer vstest";

        /// <summary>A git commit hash (SHA-1, or SHA-256 in repositories using it).</summary>
        private const string CommitHashPattern = "^[0-9a-f]{40}([0-9a-f]{24})?$";

        private static string VsixManifestFile => Path.Combine(TestResources.AdapterSolutionDir, "Packaging", "source.extension.vsixmanifest");
        private static string NuspecFile => Path.Combine(TestResources.AdapterSolutionDir, "Packaging", "VsPackage.nuspec");
        private static string Vsix => Path.Combine(TestResources.PackagingDir, "TaefTestAdapter.vsix");
        private static string NuGetPackage => Path.Combine(TestResources.PackagingDir, $"TaefTestAdapter.{VsixManifestTests.GetNuGetPackageVersion()}.nupkg");

        [TestMethod]
        [TestCategory(Unit)]
        public void VsixPublisherAndNuGetAuthors_AreExactlyTheMarketplacePublisher()
        {
            string publisher = (string)GetVsixIdentity(XDocument.Load(VsixManifestFile)).Attribute("Publisher");
            string authors = (string)GetNuspecMetadata(XDocument.Load(NuspecFile), "authors");

            publisher.Should().Be(Publisher);
            authors.Should().Be(Publisher);
            authors.Should().Be(publisher);
            GetNuspecMetadata(XDocument.Load(NuspecFile), "owners").Should().BeNull("nuget.org takes the owners from the account");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VsixManifest_MoreInfo_IsTheProjectUrl()
        {
            ((string)GetVsixMetadata(XDocument.Load(VsixManifestFile), "MoreInfo")).Should().Be(ProjectUrl);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Nuspec_LinksToProjectRepositoryReadmeAndReleaseNotes()
        {
            XDocument nuspec = XDocument.Load(NuspecFile);

            ((string)GetNuspecMetadata(nuspec, "projectUrl")).Should().Be(ProjectUrl);
            ((string)GetNuspecMetadata(nuspec, "releaseNotes")).Should().Contain(ReleaseNotesUrl);
            ((string)GetNuspecMetadata(nuspec, "readme")).Should().Be(Readme);

            XElement repository = GetNuspecMetadata(nuspec, "repository");
            AssertRepository(repository);
            repository.Attribute("commit").Should().BeNull("the commit of the sources is added when the package is built (see Packaging.csproj)");

            // the readme is the README.md of the repository, which Packaging.csproj copies into its output folder
            XElement readmeFile = nuspec.Root.Elements().Single(e => e.Name.LocalName == "files").Elements()
                .Where(f => (string)f.Attribute("src") == Readme)
                .Should().ContainSingle().Which;
            ((string)readmeFile.Attribute("target")).Should().BeEmpty("the readme must be at the root of the package");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void VsixAndNuGetTags_NameTheSupportedLanguagesOnly()
        {
            string vsixTags = (string)GetVsixMetadata(XDocument.Load(VsixManifestFile), "Tags") ?? "";
            string nuGetTags = (string)GetNuspecMetadata(XDocument.Load(NuspecFile), "tags") ?? "";

            AssertTags(vsixTags.Split(',').Select(t => t.Trim()).ToList(), "VSIX");
            AssertTags(nuGetTags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(), "NuGet package");
            vsixTags.Should().Be(VsixTags);
            nuGetTags.Should().Be(NuGetTags);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void Vsix_ContainsPublisherMoreInfoAndTags()
        {
            if (!File.Exists(Vsix))
                Assert.Inconclusive($"The VSIX has not been built: {Vsix}");

            XDocument manifest;
            using (var archive = new ZipArchive(File.OpenRead(Vsix), ZipArchiveMode.Read))
            {
                manifest = LoadXml(archive, "extension.vsixmanifest");
            }

            ((string)GetVsixIdentity(manifest).Attribute("Publisher")).Should().Be(Publisher);
            ((string)GetVsixMetadata(manifest, "MoreInfo")).Should().Be(ProjectUrl);
            ((string)GetVsixMetadata(manifest, "Tags")).Should().Be(VsixTags);
        }

        /// <summary>
        /// The NuGet package contains the readme and records the commit of its sources if it was built from a git checkout
        /// (and without the commit if not: never with an empty commit or an unreplaced token).
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void NuGetPackage_ContainsReadmeAndRepositoryCommit()
        {
            if (!File.Exists(NuGetPackage))
                Assert.Inconclusive($"The NuGet package has not been built: {NuGetPackage}");

            XDocument nuspec;
            string nuspecText;
            using (var archive = new ZipArchive(File.OpenRead(NuGetPackage), ZipArchiveMode.Read))
            {
                string nuspecEntry = archive.Entries.Select(e => e.FullName)
                    .Where(n => !n.Contains("/") && n.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
                    .Should().ContainSingle().Which;
                nuspecText = ReadText(archive, nuspecEntry);
                nuspec = XDocument.Parse(nuspecText);

                ReadBytes(archive, Readme).SequenceEqual(File.ReadAllBytes(Path.Combine(TestResources.EnlistmentRoot, Readme)))
                    .Should().BeTrue("the readme of the package is the README.md of the repository");
            }

            nuspecText.Should().NotMatchRegex(@"\$\w+\$", "all replacement tokens are replaced");
            ((string)GetNuspecMetadata(nuspec, "authors")).Should().Be(Publisher);
            ((string)GetNuspecMetadata(nuspec, "projectUrl")).Should().Be(ProjectUrl);
            ((string)GetNuspecMetadata(nuspec, "readme")).Should().Be(Readme);
            ((string)GetNuspecMetadata(nuspec, "tags")).Should().Be(NuGetTags);

            XElement repository = GetNuspecMetadata(nuspec, "repository");
            AssertRepository(repository);
            XAttribute commit = repository.Attribute("commit");
            if (commit != null)
            {
                commit.Value.Should().MatchRegex(CommitHashPattern, "the commit is the hash of a git commit");
            }
            else if (IsGitCheckout(TestResources.EnlistmentRoot))
            {
                // the build creates the package without the commit if git is not available or fails (for example, because
                // of its safe.directory check if the checkout belongs to another user)
                if (GetGitHead(TestResources.EnlistmentRoot) == null)
                    Assert.Inconclusive($"'git rev-parse HEAD' fails in {TestResources.EnlistmentRoot}, so the NuGet package has probably been built without the commit: {NuGetPackage}");
                Assert.Fail($"The NuGet package has been built from a git checkout, but its repository has no commit: {NuGetPackage}");
            }
        }

        private static void AssertRepository(XElement repository)
        {
            repository.Should().NotBeNull();
            // ReSharper disable PossibleNullReferenceException
            ((string)repository.Attribute("type")).Should().Be("git");
            ((string)repository.Attribute("url")).Should().Be(RepositoryUrl);
            ((string)repository.Attribute("branch")).Should().Be(RepositoryBranch);
            // ReSharper restore PossibleNullReferenceException
        }

        private static void AssertTags(IList<string> tags, string package)
        {
            tags.Should().Contain(SupportedTags, $"the {package} is for TAEF tests in C++ and C");
            tags.Should().NotContain(t => UnsupportedLanguageTags.Contains(t, StringComparer.OrdinalIgnoreCase),
                $"the {package} runs native TAEF test DLLs only");
            tags.Should().NotContain("").And.OnlyHaveUniqueItems();
        }

        private static bool IsGitCheckout(string directory)
        {
            // .git is a folder, or a file in worktrees and submodules
            string git = Path.Combine(directory, ".git");
            return Directory.Exists(git) || File.Exists(git);
        }

        /// <summary>
        /// The commit of HEAD of the git checkout <paramref name="directory"/> ('git rev-parse HEAD'), or null if git is not
        /// available or fails there.
        /// </summary>
        private static string GetGitHead(string directory)
        {
            try
            {
                var startInfo = new ProcessStartInfo("git.exe", "rev-parse HEAD")
                {
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var output = new StringBuilder();
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (output)
                            {
                                output.AppendLine(e.Data);
                            }
                        }
                    };
                    process.ErrorDataReceived += (sender, e) => { };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(10000))
                    {
                        process.Kill();
                        return null;
                    }
                    // waits until the output has been read
                    process.WaitForExit();

                    string head;
                    lock (output)
                    {
                        head = output.ToString().Trim();
                    }
                    return process.ExitCode == 0 && Regex.IsMatch(head, CommitHashPattern) ? head : null;
                }
            }
            catch (Exception)
            {
                // git is not on the PATH (or has exited while being killed)
                return null;
            }
        }

        private static XElement GetVsixMetadata(XDocument manifest, string element)
        {
            XNamespace ns = manifest.Root.Name.Namespace;
            return manifest.Root.Element(ns + "Metadata")?.Element(ns + element);
        }

        private static XElement GetVsixIdentity(XDocument manifest)
        {
            XElement identity = GetVsixMetadata(manifest, "Identity");
            identity.Should().NotBeNull();
            return identity;
        }

        private static XElement GetNuspecMetadata(XDocument nuspec, string element)
        {
            return nuspec.Root.Elements().Single(e => e.Name.LocalName == "metadata")
                .Elements().SingleOrDefault(e => e.Name.LocalName == element);
        }

        private static XDocument LoadXml(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            entry.Should().NotBeNull(entryName);
            // ReSharper disable once PossibleNullReferenceException
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static byte[] ReadBytes(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            entry.Should().NotBeNull(entryName);
            // ReSharper disable once PossibleNullReferenceException
            using (Stream stream = entry.Open())
            using (var bytes = new MemoryStream())
            {
                stream.CopyTo(bytes);
                return bytes.ToArray();
            }
        }

        private static string ReadText(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            entry.Should().NotBeNull(entryName);
            // ReSharper disable once PossibleNullReferenceException
            using (var reader = new StreamReader(entry.Open()))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
