// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;

namespace TaefTestAdapter.Helpers
{
    /// <summary>Chooses the CLR used by TAEF without loading the test assembly into the adapter.</summary>
    public static class ManagedRuntimeResolver
    {
        public const string CoreClrVariable = "TAEF_CoreCLR";

        /// <summary>
        /// Adds an automatically selected CoreCLR to the child environment when required. Explicit environment
        /// settings and TAEF CoreClrProfile metadata take precedence. Returns false after reporting a runtime problem.
        /// </summary>
        public static bool TryConfigureEnvironment(string testDll, string teExecutable,
            IDictionary<string, string> environmentVariables, ILogger logger, out bool usesCoreClr,
            bool hasCoreClrProfile = false)
        {
            usesCoreClr = false;
            ManagedAssemblyInfo assembly = ManagedAssemblyInfo.Read(testDll, logger);
            if (assembly == null)
                return true;

            string architecture = TaefLocator.GetArchitecture(PeParser.GetMachineType(teExecutable, logger))
                ?? TaefLocator.GetArchitecture(testDll, logger);

            if (TryGetEnvironmentValue(environmentVariables, CoreClrVariable, out string configuredRuntime))
            {
                // An explicitly empty child variable also overrides the inherited environment.
                usesCoreClr = !string.IsNullOrWhiteSpace(configuredRuntime);
                if (!usesCoreClr || ValidateRuntime(configuredRuntime, architecture, logger))
                    return true;
                return false;
            }

            if (hasCoreClrProfile || assembly.HasCoreClrProfile)
            {
                usesCoreClr = true;
                return true;
            }

            Version requiredVersion = GetCoreClrVersion(assembly.TargetFramework);
            if (requiredVersion == null)
                return true;

            string runtime = FindCompatibleRuntime(requiredVersion, architecture, GetRuntimeRoots(architecture), logger);
            if (runtime == null)
            {
                logger.LogError($"Test DLL '{testDll}' requires .NET {requiredVersion.Major}.{requiredVersion.Minor} ({architecture}), " +
                    $"but a compatible CoreCLR runtime was not found. Install that runtime or set {CoreClrVariable} " +
                    "in the test project's environment settings to the required runtime directory.");
                return false;
            }

            environmentVariables[CoreClrVariable] = runtime;
            usesCoreClr = true;
            logger.DebugInfo($"Using CoreCLR '{runtime}' for managed test DLL '{testDll}'");
            return true;
        }

        /// <summary>Returns the CoreCLR target version, or null for Framework, .NET Standard, or unknown targets.</summary>
        public static Version GetCoreClrVersion(string targetFramework)
        {
            if (string.IsNullOrWhiteSpace(targetFramework))
                return null;
            try
            {
                var framework = new FrameworkName(targetFramework);
                return string.Equals(framework.Identifier, ".NETCoreApp", StringComparison.OrdinalIgnoreCase)
                    ? framework.Version : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the newest compatible patch of the requested runtime family. A higher minor version is used only
        /// when the requested minor is unavailable; automatic selection never crosses a major version.
        /// </summary>
        public static string FindCompatibleRuntime(Version requiredVersion, string architecture,
            IEnumerable<string> roots, ILogger logger)
        {
            var candidates = new List<KeyValuePair<Version, string>>();
            foreach (string root in roots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string shared = Path.Combine(root, "shared", "Microsoft.NETCore.App");
                    if (!Directory.Exists(shared))
                        continue;
                    foreach (string directory in Directory.EnumerateDirectories(shared))
                    {
                        if (Version.TryParse(Path.GetFileName(directory), out Version version)
                            && version.Major == requiredVersion.Major
                            && version >= requiredVersion
                            && RuntimeHasArchitecture(directory, architecture, logger))
                            candidates.Add(new KeyValuePair<Version, string>(version, directory));
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
                {
                    logger.DebugInfo($"Could not inspect .NET runtime root '{root}': {e.Message}");
                }
            }

            return candidates
                .OrderBy(c => c.Key.Minor)
                .ThenByDescending(c => c.Key)
                .Select(c => c.Value)
                .FirstOrDefault();
        }

        private static bool ValidateRuntime(string runtime, string architecture, ILogger logger)
        {
            if (RuntimeHasArchitecture(runtime, architecture, logger))
                return true;
            logger.LogError($"{CoreClrVariable} points to '{runtime}', which does not contain a {architecture} coreclr.dll " +
                "compatible with the selected TE.exe. Correct the managed test runtime environment setting.");
            return false;
        }

        private static bool RuntimeHasArchitecture(string runtime, string architecture, ILogger logger)
        {
            string coreClr;
            try { coreClr = Path.Combine(runtime, "coreclr.dll"); }
            catch (ArgumentException) { return false; }
            return File.Exists(coreClr)
                && string.Equals(TaefLocator.GetArchitecture(PeParser.GetMachineType(coreClr, logger)),
                    architecture, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetEnvironmentValue(IDictionary<string, string> environmentVariables,
            string name, out string value)
        {
            foreach (KeyValuePair<string, string> pair in environmentVariables)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = Environment.GetEnvironmentVariable(name);
            return value != null;
        }

        private static IEnumerable<string> GetRuntimeRoots(string architecture)
        {
            yield return Environment.GetEnvironmentVariable("DOTNET_ROOT_" + architecture.ToUpperInvariant());
            if (architecture == TaefLocator.ArchitectureX86)
                yield return Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)");
            yield return Environment.GetEnvironmentVariable("DOTNET_ROOT");

            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                string root = null;
                try
                {
                    using (RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = hive.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\" + architecture))
                        root = key?.GetValue("InstallLocation") as string;
                }
                catch (Exception e) when (e is System.Security.SecurityException || e is UnauthorizedAccessException
                    || e is IOException || e is PlatformNotSupportedException) { }
                if (root != null)
                    yield return root;
            }

            string programFiles = Environment.GetEnvironmentVariable("ProgramW6432")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                if (architecture == TaefLocator.ArchitectureX64)
                    yield return Path.Combine(programFiles, "dotnet", "x64");
                yield return Path.Combine(programFiles, "dotnet");
            }
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86))
                yield return Path.Combine(programFilesX86, "dotnet");
        }
    }
}
