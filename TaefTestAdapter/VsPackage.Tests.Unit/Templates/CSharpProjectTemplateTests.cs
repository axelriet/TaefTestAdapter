// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Templates
{
    [TestClass]
    public class CSharpProjectTemplateTests
    {
        [TestMethod]
        [TestCategory(Integration)]
        public void CSharpProjectAndItemTemplates_BuildAndRunAllDataRowsOnFrameworkAndModernDotNet()
        {
            string root = Path.Combine(Path.GetTempPath(), "TaefCSharpTemplates_" + Guid.NewGuid().ToString("N"));
            string sourceDir = Path.Combine(root, "src");
            string resultsDir = Path.Combine(root, "results");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(resultsDir);
            string templateDir = Path.Combine(TestResources.AdapterSolutionDir, @"ProjectTemplates\CSharp\Test\TAEF");
            string itemDir = Path.Combine(TestResources.AdapterSolutionDir, @"ItemTemplates\CSharp\Test\TAEF");
            string project = File.ReadAllText(Path.Combine(templateDir, "TaefTest.csproj"))
                .Replace("$taefnamespace$", "Contoso.System.WEX")
                .Replace("<TargetFramework>net48</TargetFramework>", "<TargetFrameworks>net48;net8.0;net10.0</TargetFrameworks>");
            string projectFile = Path.Combine(sourceDir, "Templates.csproj");
            File.WriteAllText(projectFile, project);
            File.WriteAllText(Path.Combine(sourceDir, "Tests.cs"), File.ReadAllText(Path.Combine(templateDir, "test.cs"))
                .Replace("$taefnamespace$", "Contoso.System.WEX"));
            File.WriteAllText(Path.Combine(sourceDir, "ItemTests.cs"), File.ReadAllText(Path.Combine(itemDir, "test.cs"))
                .Replace("$taefnamespace$", "Contoso.System.WEX").Replace("$taefclassname$", "VerifyTests"));
            new XDocument(new XElement("configuration",
                new XElement("packageSources", new XElement("clear"),
                    new XElement("add", new XAttribute("key", "adapter"), new XAttribute("value", TestResources.PackagingDir)),
                    new XElement("add", new XAttribute("key", "nuget"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
                new XElement("config", new XElement("add", new XAttribute("key", "globalPackagesFolder"),
                    new XAttribute("value", Path.Combine(root, "packages")))))).Save(Path.Combine(root, "NuGet.Config"));
            File.WriteAllText(Path.Combine(root, "global.json"), "{\"test\":{\"runner\":\"VSTest\"}}");

            string arguments = $"test \"{projectFile}\" --disable-build-servers --nologo --verbosity normal " +
                $"--results-directory \"{resultsDir}\" --logger \"trx;LogFilePrefix=templates\" -m:1";
            int exitCode = new TestProcessLauncher().GetOutputStreams(root, "dotnet", arguments,
                out List<string> standardOut, out List<string> standardError);
            string output = string.Join(Environment.NewLine, standardOut.Concat(standardError));
            File.WriteAllText(Path.Combine(root, "test.log"), output);
            exitCode.Should().Be(0, output);

            string[] results = Directory.GetFiles(resultsDir, "*.trx");
            results.Should().HaveCount(3, output);
            foreach (string result in results)
            {
                XElement counters = XDocument.Load(result).Descendants().Single(e => e.Name.LocalName == "Counters");
                ((int)counters.Attribute("total")).Should().Be(10, result);
                ((int)counters.Attribute("passed")).Should().Be(10, result);
                ((int)counters.Attribute("failed")).Should().Be(0, result);
            }
        }
    }
}
