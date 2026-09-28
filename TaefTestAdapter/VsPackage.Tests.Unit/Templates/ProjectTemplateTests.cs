// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.Tests.Common;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.VsPackage.Templates
{
    /// <summary>
    /// Creates a project from the TAEF Test Project template (<c>ProjectTemplates\Test\TAEF</c>) and evaluates its F5
    /// settings with MSBuild: by default, F5 runs <c>TE.exe "&lt;test dll&gt;" /inproc</c>, and the values of
    /// <em>Project Properties &gt; Debugging</em> (which Visual Studio stores in the <c>.vcxproj.user</c> file, imported by
    /// Microsoft.Cpp.targets) take precedence. Also adds a file created from the TAEF Test item template
    /// (<c>ItemTemplates\Test\TAEF</c>) to the project, builds it and checks the tests listed by TE.exe.
    /// </summary>
    [TestClass]
    public class ProjectTemplateTests
    {
        private const string ProjectName = "MyTests";

        // a unique name for the file created from the item template (a C++ identifier: must not start with a digit)
        private readonly string _itemName = "Tests_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        private const string LocalDebuggerCommand = "LocalDebuggerCommand";
        private const string LocalDebuggerCommandArguments = "LocalDebuggerCommandArguments";
        private const string DebuggerFlavor = "DebuggerFlavor";
        private const string TargetPath = "TargetPath";

        private static string TemplateDir => Path.Combine(TestResources.AdapterSolutionDir, @"ProjectTemplates\Test\TAEF");
        private static string ItemTemplateDir => Path.Combine(TestResources.AdapterSolutionDir, @"ItemTemplates\Test\TAEF");

        private string _rootDir;
        private string _projectFile;

        [TestInitialize]
        public void SetUp()
        {
            // the project folder contains a space (as e.g. "C:\Users\John Doe\source\repos") to check the quoting
            _rootDir = Path.Combine(Path.GetTempPath(), "TaefTestAdapter_" + Guid.NewGuid().ToString("N"));
            string projectDir = Path.Combine(_rootDir, "My Tests");
            Directory.CreateDirectory(projectDir);
            _projectFile = CreateProjectFromTemplate(projectDir);
        }

        [TestCleanup]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_rootDir, true);
            }
            catch (Exception)
            {
                // best effort
            }
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void F5Settings_NoUserFile_TeExeOfPlatformRunsTestDllInProcess()
        {
            IDictionary<string, string> x64 = Evaluate("Debug", "x64");
            x64[LocalDebuggerCommand].Should().EndWith(@"\Testing\Runtimes\TAEF\x64\TE.exe");
            x64[LocalDebuggerCommandArguments].Should().Be($"\"{x64[TargetPath]}\" /inproc");
            x64[DebuggerFlavor].Should().Be("WindowsLocalDebugger");

            IDictionary<string, string> x86 = Evaluate("Release", "Win32");
            x86[LocalDebuggerCommand].Should().EndWith(@"\Testing\Runtimes\TAEF\x86\TE.exe");
            x86[LocalDebuggerCommandArguments].Should().Be($"\"{x86[TargetPath]}\" /inproc");
            x86[DebuggerFlavor].Should().Be("WindowsLocalDebugger");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void F5Settings_CommandArgumentsInUserFile_UserValueIsUsed()
        {
            // as written by Visual Studio for Command Arguments '"$(TargetPath)" /inproc /name:*Addition' of Debug|x64
            WriteUserFile(
                "  <PropertyGroup Condition=\"'$(Configuration)|$(Platform)'=='Debug|x64'\">",
                "    <LocalDebuggerCommandArguments>\"$(TargetPath)\" /inproc /name:*Addition</LocalDebuggerCommandArguments>",
                "  </PropertyGroup>");

            IDictionary<string, string> x64 = Evaluate("Debug", "x64");
            x64[LocalDebuggerCommandArguments].Should().Be($"\"{x64[TargetPath]}\" /inproc /name:*Addition");
            x64[LocalDebuggerCommand].Should().EndWith(@"\Testing\Runtimes\TAEF\x64\TE.exe");

            // other configurations keep the defaults
            IDictionary<string, string> x86 = Evaluate("Debug", "Win32");
            x86[LocalDebuggerCommandArguments].Should().Be($"\"{x86[TargetPath]}\" /inproc");
            x86[LocalDebuggerCommand].Should().EndWith(@"\Testing\Runtimes\TAEF\x86\TE.exe");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void F5Settings_CommandAndDebuggerInUserFile_UserValuesAreUsed()
        {
            WriteUserFile(
                "  <PropertyGroup Condition=\"'$(Configuration)|$(Platform)'=='Release|x64'\">",
                @"    <LocalDebuggerCommand>C:\MyTaef\x64\TE.exe</LocalDebuggerCommand>",
                "    <DebuggerFlavor>WindowsRemoteDebugger</DebuggerFlavor>",
                "  </PropertyGroup>");

            IDictionary<string, string> properties = Evaluate("Release", "x64");
            properties[LocalDebuggerCommand].Should().Be(@"C:\MyTaef\x64\TE.exe");
            properties[DebuggerFlavor].Should().Be("WindowsRemoteDebugger");
            properties[LocalDebuggerCommandArguments].Should().Be($"\"{properties[TargetPath]}\" /inproc");
        }

        [TestMethod]
        [TestCategory(Integration)]
        public void ItemTemplate_FileAddedToProject_AllTestsAreInTheRootNamespace()
        {
            AddItemFromTemplate(_itemName);

            string testDll = Build("Debug", "x64");

            IList<string> names = ListTestNames(testDll);
            names.Should().Contain(new[]
            {
                $"{ProjectName}::SampleTests::Addition",
                $"{ProjectName}::{_itemName}",
                $"{ProjectName}::{_itemName}::Addition",
                $"{ProjectName}::{_itemName}::Strings",
                $"{ProjectName}::{_itemName}::WithProperties",
            }, string.Join(Environment.NewLine, names));
            names.Should().Contain(n => n.StartsWith($"{ProjectName}::{_itemName}::DataDriven"), string.Join(Environment.NewLine, names));
            // no test class outside of the project's namespace (e.g. in the global namespace)
            names.Should().OnlyContain(n => n.StartsWith(ProjectName + "::"), string.Join(Environment.NewLine, names));
        }

        /// <returns>The project file, with the template parameters replaced as Visual Studio does.</returns>
        private static string CreateProjectFromTemplate(string projectDir)
        {
            var parameters = new Dictionary<string, string>
            {
                ["guid1"] = Guid.NewGuid().ToString("D").ToUpperInvariant(),
                ["projectname"] = ProjectName,
                ["safeprojectname"] = ProjectName
            };

            string projectFile = Path.Combine(projectDir, ProjectName + ".vcxproj");
            File.WriteAllText(projectFile, ReplaceParameters(File.ReadAllText(Path.Combine(TemplateDir, "TaefTest.vcxproj")), parameters), Encoding.UTF8);
            File.WriteAllText(Path.Combine(projectDir, "Tests.cpp"), ReplaceParameters(File.ReadAllText(Path.Combine(TemplateDir, "test.cpp")), parameters), Encoding.UTF8);
            return projectFile;
        }

        /// <summary>
        /// Adds <c>&lt;itemName&gt;.cpp</c> created from the item template to the project, as Visual Studio does
        /// (<c>$rootnamespace$</c> is the RootNamespace of the project).
        /// </summary>
        private void AddItemFromTemplate(string itemName)
        {
            var parameters = new Dictionary<string, string>
            {
                ["rootnamespace"] = ProjectName,
                ["safeitemname"] = itemName,
                ["fileinputname"] = itemName
            };

            string content = ReplaceParameters(File.ReadAllText(Path.Combine(ItemTemplateDir, "test.cpp")), parameters);
            Regex.Matches(content, @"\$[a-z0-9]+\$").Cast<Match>().Select(m => m.Value).Should().BeEmpty("all template parameters are replaced");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(_projectFile) ?? "", itemName + ".cpp"), content, Encoding.UTF8);

            string project = File.ReadAllText(_projectFile);
            project.Should().Contain("<ClCompile Include=\"Tests.cpp\" />");
            File.WriteAllText(_projectFile, project.Replace(
                "<ClCompile Include=\"Tests.cpp\" />",
                $"<ClCompile Include=\"Tests.cpp\" />{Environment.NewLine}    <ClCompile Include=\"{itemName}.cpp\" />"), Encoding.UTF8);
        }

        private static string ReplaceParameters(string content, IDictionary<string, string> parameters)
        {
            return Regex.Replace(content, @"\$(?<name>[a-z0-9]+)\$", m => parameters.TryGetValue(m.Groups["name"].Value, out string value) ? value : m.Value);
        }

        /// <returns>The test DLL built from the project.</returns>
        private string Build(string configuration, string platform)
        {
            string arguments = $"\"{_projectFile}\" -nologo -nr:false -v:minimal -p:Configuration={configuration} -p:Platform={platform}";
            int exitCode = new TestProcessLauncher().GetOutputStreams(Path.GetDirectoryName(_projectFile), GetMsBuildPath(), arguments,
                out List<string> standardOut, out List<string> standardErr);
            string output = string.Join(Environment.NewLine, standardOut.Concat(standardErr));
            if (exitCode != 0 && (output.Contains("MSB4019") || output.Contains("WexTestClass.h") || output.Contains("TE.Common.lib")))
                Assert.Inconclusive($"The C++ build tools or the TAEF development files are not available:{Environment.NewLine}{output}");
            exitCode.Should().Be(0, output);

            string testDll = Path.Combine(Path.GetDirectoryName(_projectFile) ?? "", platform == "x64" ? "x64" : "", configuration, ProjectName + ".dll");
            File.Exists(testDll).Should().BeTrue(output);
            return testDll;
        }

        /// <returns>The names of the test classes and tests of <paramref name="testDll"/> as listed by <c>TE.exe /list</c>.</returns>
        private static IList<string> ListTestNames(string testDll)
        {
            string teExecutable = TestResources.TeExecutableX64;
            if (!File.Exists(teExecutable))
                Assert.Inconclusive($"TE.exe not found: {teExecutable}");

            int exitCode = new TestProcessLauncher().GetOutputStreams(Path.GetDirectoryName(testDll), teExecutable, $"\"{testDll}\" /list",
                out List<string> standardOut, out List<string> standardErr);
            string output = string.Join(Environment.NewLine, standardOut.Concat(standardErr));
            exitCode.Should().Be(0, output);

            // the names of test classes and tests are the lines containing '::', indented below the DLL
            return standardOut
                .Where(line => line != null && line.Contains("::"))
                .Select(line => line.Trim())
                .ToList();
        }

        private void WriteUserFile(params string[] propertyGroupLines)
        {
            var lines = new List<string>
            {
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
                "<Project ToolsVersion=\"Current\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
            };
            lines.AddRange(propertyGroupLines);
            lines.Add("</Project>");
            File.WriteAllLines(_projectFile + ".user", lines, Encoding.UTF8);
        }

        /// <returns>The evaluated debugger properties and TargetPath of the project for the given configuration.</returns>
        private IDictionary<string, string> Evaluate(string configuration, string platform)
        {
            string msBuild = GetMsBuildPath();
            string[] properties = { LocalDebuggerCommand, LocalDebuggerCommandArguments, DebuggerFlavor, TargetPath };
            string arguments = $"\"{_projectFile}\" -nologo -nr:false " +
                               string.Join(" ", properties.Select(p => "-getProperty:" + p)) +
                               $" -p:Configuration={configuration} -p:Platform={platform}";

            int exitCode = new TestProcessLauncher().GetOutputStreams(Path.GetDirectoryName(_projectFile), msBuild, arguments,
                out List<string> standardOut, out List<string> standardErr);
            string output = string.Join(Environment.NewLine, standardOut.Concat(standardErr));
            if (exitCode != 0 && (output.Contains("MSB4019") || output.Contains("MSB1001")))
                Assert.Inconclusive($"The C++ build tools or MSBuild's -getProperty switch are not available:{Environment.NewLine}{output}");
            exitCode.Should().Be(0, output);

            // -getProperty with several properties prints {"Properties": {"<name>": "<JSON string>", ...}}
            var result = new Dictionary<string, string>();
            foreach (Match match in Regex.Matches(output, "\"(?<name>\\w+)\":\\s*\"(?<value>(?:[^\"\\\\]|\\\\.)*)\""))
            {
                result[match.Groups["name"].Value] = Regex.Unescape(match.Groups["value"].Value);
            }
            result.Keys.Should().Contain(properties, output);
            result[TargetPath].Should().EndWith($@"\My Tests\{(platform == "x64" ? @"x64\" : "")}{configuration}\{ProjectName}.dll");
            return result;
        }

        private static string GetMsBuildPath()
        {
            // <VS>\Common7\IDE\Extensions\TestPlatform\vstest.console.exe -> <VS>\MSBuild\Current\Bin\MSBuild.exe
            string vsTestConsole = TestResources.GetVsTestConsolePath();
            if (vsTestConsole != null)
            {
                string visualStudioDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(vsTestConsole) ?? "", @"..\..\..\.."));
                string msBuild = Path.Combine(visualStudioDir, @"MSBuild\Current\Bin\MSBuild.exe");
                if (File.Exists(msBuild))
                    return msBuild;
            }

            Assert.Inconclusive($"MSBuild.exe of Visual Studio not found (vstest.console.exe: '{vsTestConsole}')");
            return null;
        }

    }

}
