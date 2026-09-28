// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TaefTestAdapter.DiaResolver.Helpers
{

    /// <summary>
    /// Compiles C++ code without any headers and runtime library into a DLL with a full PDB next to it, using the MSVC
    /// compiler and linker of a Visual Studio installation (found with vswhere). Used to test the PDB based lookup of
    /// source locations with C++ constructs the sample test DLLs do not contain (e.g. overloaded member functions).
    /// </summary>
    internal static class CppTestDll
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

        // host\target folders below VC\Tools\MSVC\<version>\bin, in order of preference
        private static readonly string[] ToolFolders = { @"Hostx64\x64", @"Hostx86\x86", @"Hostarm64\arm64", @"Hostx64\x86" };

        /// <summary>
        /// Compiles <paramref name="source"/> into <c>&lt;directory&gt;\&lt;name&gt;.dll</c> (with <c>&lt;name&gt;.pdb</c>).
        /// Functions defined in the code are only contained in the DLL if they are used, e.g. by an exported function.
        /// </summary>
        /// <returns>The full path of the DLL, or null if the code could not be compiled (see <paramref name="error"/>).</returns>
        public static string Compile(string source, string directory, string name, out string error)
        {
            (string compiler, string linker) = FindTools();
            if (compiler == null)
            {
                error = "No MSVC compiler found (install the C++ workload of Visual Studio)";
                return null;
            }

            Directory.CreateDirectory(directory);
            string sourceFile = Path.Combine(directory, name + ".cpp");
            string dll = Path.Combine(directory, name + ".dll");
            File.WriteAllText(sourceFile, source, new UTF8Encoding(true));

            // /Od: no inlining, /Z7: debug information in the object file, /GS-: no security cookie (no runtime library)
            if (!Run(compiler, $"/nologo /c /Od /Z7 /GS- /EHs-c- /utf-8 \"{sourceFile}\" /Fo\"{Path.Combine(directory, name + ".obj")}\"", directory, out error))
                return null;
            if (!Run(linker, $"/nologo /DLL /NOENTRY /NODEFAULTLIB /DEBUG:FULL /INCREMENTAL:NO \"{Path.Combine(directory, name + ".obj")}\" /OUT:\"{dll}\" /PDB:\"{Path.ChangeExtension(dll, ".pdb")}\"", directory, out error))
                return null;

            return dll;
        }

        private static bool Run(string tool, string arguments, string workingDirectory, out string error)
        {
            var startInfo = new ProcessStartInfo(tool, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            };
            // options of the calling environment must not interfere
            startInfo.EnvironmentVariables.Remove("CL");
            startInfo.EnvironmentVariables.Remove("_CL_");
            startInfo.EnvironmentVariables.Remove("LINK");
            startInfo.EnvironmentVariables.Remove("_LINK_");

            using (Process process = Process.Start(startInfo))
            {
                // ReSharper disable PossibleNullReferenceException
                var standardError = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd() + standardError.Result;
                // ReSharper restore PossibleNullReferenceException
                if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
                {
                    process.Kill();
                    error = $"'{tool} {arguments}' timed out";
                    return false;
                }

                error = process.ExitCode == 0 ? null : $"'{tool} {arguments}' returned {process.ExitCode}:{Environment.NewLine}{output}";
                return process.ExitCode == 0;
            }
        }

        private static (string Compiler, string Linker) FindTools()
        {
            foreach (string installation in FindVsInstallations())
            {
                string toolsDir = Path.Combine(installation, @"VC\Tools\MSVC");
                if (!Directory.Exists(toolsDir))
                    continue;

                IEnumerable<string> toolsets = Directory.GetDirectories(toolsDir)
                    .OrderByDescending(d => Version.TryParse(Path.GetFileName(d), out Version version) ? version : new Version(0, 0));
                foreach (string toolset in toolsets)
                {
                    foreach (string toolFolder in ToolFolders)
                    {
                        string compiler = Path.Combine(toolset, "bin", toolFolder, "cl.exe");
                        string linker = Path.Combine(toolset, "bin", toolFolder, "link.exe");
                        if (File.Exists(compiler) && File.Exists(linker))
                            return (compiler, linker);
                    }
                }
            }

            return (null, null);
        }

        private static IEnumerable<string> FindVsInstallations()
        {
            var installations = new List<string>();

            string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Visual Studio\Installer\vswhere.exe");
            if (File.Exists(vswhere))
            {
                try
                {
                    var startInfo = new ProcessStartInfo(vswhere, "-all -prerelease -products * -sort -property installationPath")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (Process process = Process.Start(startInfo))
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(30000);
                        installations.AddRange(output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()));
                    }
                }
                catch (Exception)
                {
                    // fall back to the default locations
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (string version in new[] { "18", "2022" })
            {
                foreach (string edition in new[] { "Enterprise", "Professional", "Community", "Preview", "BuildTools" })
                {
                    installations.Add(Path.Combine(programFiles, "Microsoft Visual Studio", version, edition));
                }
            }

            return installations.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }

}
