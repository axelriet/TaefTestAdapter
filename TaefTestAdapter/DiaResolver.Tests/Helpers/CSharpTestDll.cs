// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TaefTestAdapter.DiaResolver.Helpers
{
    /// <summary>Compiles managed PE/PDB fixtures without loading them; each compilation has a fresh directory.</summary>
    internal static class CSharpTestDll
    {
        internal static string Compile(string source, string debugType, string platform, out string error, IEnumerable<string> additionalReferences = null, string assemblyName = null)
        {
            string compiler = FindCompiler();
            string markup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Windows Kits\10\Testing\Development\lib\x64\ref4.5\te.managed.dll");
            if (compiler == null || !File.Exists(markup))
            {
                error = "The Visual Studio C# compiler and WDK TAEF reference assemblies are required.";
                return null;
            }

            string id = Guid.NewGuid().ToString("N");
            string directory = Path.Combine(Path.GetTempPath(), "TaefManagedSymbols_" + id);
            Directory.CreateDirectory(directory);
            string sourceFile = Path.Combine(directory, "Source_" + id + ".cs");
            string binary = Path.Combine(directory, assemblyName == null ? "Managed_" + id + ".dll" : assemblyName + ".dll");
            File.WriteAllText(sourceFile, source, new UTF8Encoding(false));
            string references = string.Join(" ", (additionalReferences ?? Enumerable.Empty<string>()).Select(path => $"/reference:\"{path}\""));
            var startInfo = new ProcessStartInfo(compiler,
                $"/nologo /target:library /optimize- /warn:0 /debug:{debugType} /platform:{platform} /reference:\"{markup}\" {references} /out:\"{binary}\" \"{sourceFile}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directory
            };
            using (Process process = Process.Start(startInfo))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    error = "The C# fixture compiler timed out.";
                    return null;
                }
                error = stdout.Result + stderr.Result;
                return process.ExitCode == 0 ? binary : null;
            }
        }

        private static string FindCompiler()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (string version in new[] { "18", "2022" })
            foreach (string edition in new[] { "Enterprise", "Professional", "Community", "Preview", "BuildTools" })
            {
                string path = Path.Combine(programFiles, "Microsoft Visual Studio", version, edition, @"MSBuild\Current\Bin\Roslyn\csc.exe");
                if (File.Exists(path))
                    return path;
            }
            string vsRoot = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            return string.IsNullOrEmpty(vsRoot) ? null : new[]
            {
                Path.Combine(vsRoot, @"MSBuild\Current\Bin\Roslyn\csc.exe")
            }.FirstOrDefault(File.Exists);
        }
    }
}
