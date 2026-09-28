// This file has been added for TAEF support.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using TaefTestAdapter.Common;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Settings;

namespace TaefTestAdapter
{
    /// <summary>
    /// Finds the TE.exe to be used for a test DLL:
    /// <list type="number">
    /// <item>option TeExecutable (placeholders replaced): an existing file is used as is; for an existing folder,
    /// <c>&lt;folder&gt;\&lt;arch&gt;\TE.exe</c> and then <c>&lt;folder&gt;\TE.exe</c> are tried;</item>
    /// <item><c>&lt;Windows Kits root&gt;\Testing\Runtimes\TAEF\&lt;arch&gt;\TE.exe</c>, where the Windows Kits root is taken from
    /// registry value <c>HKLM\SOFTWARE\Microsoft\Windows Kits\Installed Roots\KitsRoot10</c> (64-bit and 32-bit view);
    /// the roots <c>%ProgramFiles(x86)%\Windows Kits\10</c> and <c>%ProgramFiles%\Windows Kits\10</c> are tried as well;</item>
    /// <item>TE.exe on the PATH.</item>
    /// </list>
    /// <c>&lt;arch&gt;</c> (x86, x64, arm64) is the architecture of the test DLL (PE machine type). Successful lookups are
    /// cached per architecture and configured value.
    /// </summary>
    public static class TaefLocator
    {
        public const string ArchitectureX86 = "x86";
        public const string ArchitectureX64 = "x64";
        public const string ArchitectureArm64 = "arm64";
        public const string ArchitectureArm = "arm";

        public const string KitsRootRegistryKey = @"SOFTWARE\Microsoft\Windows Kits\Installed Roots";
        public const string KitsRootRegistryValue = "KitsRoot10";

        /// <summary>Location of the TAEF runtimes (containing one folder per architecture) below a Windows Kits root.</summary>
        public const string TaefRuntimesSubDirectory = @"Testing\Runtimes\TAEF";

        private static readonly ConcurrentDictionary<string, string> Cache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // problems which have already been reported to a logger (i.e. within the current discovery/execution)
        private static readonly ConditionalWeakTable<ILogger, HashSet<string>> ReportedProblems =
            new ConditionalWeakTable<ILogger, HashSet<string>>();

        /// <summary>
        /// Finds the TE.exe to be used for <paramref name="testDll"/> (see class comment). If no TE.exe can be found,
        /// an error (including a hint on option TeExecutable) is logged once per architecture and logger, and null is
        /// returned - callers just need to skip the test DLL. A configured TeExecutable which does not point to TE.exe
        /// results in a warning (once per logger) and automatic detection.
        /// </summary>
        /// <returns>Full path of TE.exe, or null if none could be found</returns>
        public static string FindTeExecutable(string testDll, SettingsWrapper settings, ILogger logger)
        {
            if (testDll == null)
                throw new ArgumentNullException(nameof(testDll));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            string architecture = GetArchitecture(testDll, logger);
            string configuredValue = settings.GetTeExecutable(testDll)?.Trim() ?? "";

            string cacheKey = $"{architecture}|{configuredValue}";
            if (Cache.TryGetValue(cacheKey, out string cachedTeExecutable))
            {
                if (File.Exists(cachedTeExecutable))
                {
                    logger.VerboseInfo($"Using TE.exe '{cachedTeExecutable}' for {architecture} test DLL '{testDll}'");
                    return cachedTeExecutable;
                }
                Cache.TryRemove(cacheKey, out _);
            }

            string teExecutable = DoFindTeExecutable(architecture, configuredValue, logger, out bool isConfiguredTeExecutable);
            if (teExecutable != null)
            {
                // do not cache the fallback for a wrongly configured value (so that the warning is repeated in later runs)
                if (configuredValue.Length == 0 || isConfiguredTeExecutable)
                    Cache[cacheKey] = teExecutable;
                logger.DebugInfo($"Using TE.exe '{teExecutable}' for {architecture} test DLL '{testDll}'");
                return teExecutable;
            }

            ReportOnce(logger, $"notfound|{cacheKey}",
                () => logger.LogError(GetTeExecutableNotFoundMessage(architecture, configuredValue)));
            logger.DebugWarning($"No TE.exe found for {architecture} test DLL '{testDll}'");
            return null;
        }

        /// <returns>
        /// The architecture of <paramref name="testDll"/> (<see cref="ArchitectureX86"/>, <see cref="ArchitectureX64"/>,
        /// <see cref="ArchitectureArm64"/> or <see cref="ArchitectureArm"/>). If it can not be determined, the architecture
        /// of the operating system is returned (and a debug warning is logged).
        /// </returns>
        public static string GetArchitecture(string testDll, ILogger logger)
        {
            ushort machineType = PeParser.GetMachineType(testDll, logger);
            string architecture = GetArchitecture(machineType);
            if (architecture != null)
                return architecture;

            architecture = GetOperatingSystemArchitecture();
            logger.DebugWarning($"Could not determine architecture of '{testDll}' (machine type 0x{machineType:X4}), assuming {architecture}");
            return architecture;
        }

        /// <returns>
        /// The architecture for the machine type of a PE image, or null for unknown machine types. ARM64EC images carry
        /// machine type AMD64 (their tests are run with the x64 TE.exe) and ARM64X images machine type ARM64;
        /// <see cref="PeParser.MachineArm64EC"/> only occurs in object files and is therefore unknown.
        /// </returns>
        public static string GetArchitecture(ushort machineType)
        {
            switch (machineType)
            {
                case PeParser.MachineX86:
                    return ArchitectureX86;
                case PeParser.MachineX64:
                    return ArchitectureX64;
                case PeParser.MachineArm64:
                    return ArchitectureArm64;
                case PeParser.MachineArm:
                case PeParser.MachineArmThumb:
                case PeParser.MachineArmNT:
                    return ArchitectureArm;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Clears the cache of TE.exe locations (e.g. for tests).
        /// </summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static string GetTeExecutableNotFoundMessage(string architecture, string configuredValue)
        {
            string configured = string.IsNullOrWhiteSpace(configuredValue)
                ? ""
                : $" (the configured value '{configuredValue}' does not point to TE.exe)";
            return $"Could not find {TaefConstants.TeExecutableName} for {architecture} test DLLs{configured} - tests of such DLLs cannot be discovered or run. " +
                   $"Install TAEF (with the Windows Driver Kit (WDK), or with a TAEF runtime installer of the WDK), or set option '{SettingsWrapper.OptionTeExecutable}' ({nameof(RunSettings.TeExecutable)}) " +
                   $"to TE.exe or to a folder containing {architecture}\\{TaefConstants.TeExecutableName}.";
        }

        private static string DoFindTeExecutable(string architecture, string configuredValue, ILogger logger, out bool isConfiguredTeExecutable)
        {
            isConfiguredTeExecutable = false;
            if (configuredValue.Length > 0)
            {
                string teExecutable = FindConfiguredTeExecutable(configuredValue, architecture, logger);
                if (teExecutable != null)
                {
                    isConfiguredTeExecutable = true;
                    return teExecutable;
                }

                ReportOnce(logger, $"configured|{architecture}|{configuredValue}",
                    () => logger.LogWarning($"Option '{SettingsWrapper.OptionTeExecutable}': '{configuredValue}' is neither an existing file nor a folder containing {architecture}\\{TaefConstants.TeExecutableName} or {TaefConstants.TeExecutableName} - trying to find {TaefConstants.TeExecutableName} automatically"));
            }

            foreach (string kitsRoot in GetWindowsKitsRoots(logger))
            {
                string candidate = SafeCombine(kitsRoot, TaefRuntimesSubDirectory, architecture, TaefConstants.TeExecutableName);
                if (candidate != null && File.Exists(candidate))
                    return Path.GetFullPath(candidate);

                logger.VerboseInfo($"{TaefConstants.TeExecutableName} not found at '{candidate}'");
            }

            return FindOnPath(TaefConstants.TeExecutableName, logger);
        }

        private static string FindConfiguredTeExecutable(string configuredValue, string architecture, ILogger logger)
        {
            try
            {
                if (File.Exists(configuredValue))
                    return Path.GetFullPath(configuredValue);

                if (Directory.Exists(configuredValue))
                {
                    foreach (string candidate in new[]
                    {
                        Path.Combine(configuredValue, architecture, TaefConstants.TeExecutableName),
                        Path.Combine(configuredValue, TaefConstants.TeExecutableName)
                    })
                    {
                        if (File.Exists(candidate))
                            return Path.GetFullPath(candidate);
                    }
                }
            }
            catch (Exception e)
            {
                logger.DebugWarning($"Exception while evaluating option '{SettingsWrapper.OptionTeExecutable}' ('{configuredValue}'): {e.Message}");
            }

            return null;
        }

        private static IEnumerable<string> GetWindowsKitsRoots(ILogger logger)
        {
            var roots = new List<string>();
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(KitsRootRegistryKey))
                    {
                        if (key?.GetValue(KitsRootRegistryValue) is string root && !string.IsNullOrWhiteSpace(root))
                            roots.Add(root.Trim());
                    }
                }
                catch (Exception e)
                {
                    logger.VerboseInfo($"Could not read registry value {KitsRootRegistryKey}\\{KitsRootRegistryValue} ({view}): {e.Message}");
                }
            }

            foreach (string programFiles in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            })
            {
                if (!string.IsNullOrWhiteSpace(programFiles))
                    roots.Add(SafeCombine(programFiles, "Windows Kits", "10"));
            }

            return roots
                .Where(r => r != null)
                .Select(r => r.TrimEnd('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string FindOnPath(string fileName, ILogger logger)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string directory in path.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = SafeCombine(directory.Trim().Trim('"'), fileName);
                try
                {
                    if (candidate != null && File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
                catch (Exception e)
                {
                    logger.VerboseInfo($"Could not check '{candidate}': {e.Message}");
                }
            }

            return null;
        }

        private static string SafeCombine(params string[] paths)
        {
            try
            {
                return Path.Combine(paths);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string GetOperatingSystemArchitecture()
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X86:
                    return ArchitectureX86;
                case Architecture.Arm64:
                    return ArchitectureArm64;
                case Architecture.Arm:
                    return ArchitectureArm;
                default:
                    return ArchitectureX64;
            }
        }

        private static void ReportOnce(ILogger logger, string problem, Action report)
        {
            HashSet<string> reportedProblems = ReportedProblems.GetValue(logger, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            bool isNew;
            lock (reportedProblems)
            {
                isNew = reportedProblems.Add(problem);
            }

            if (isNew)
                report();
        }

    }

}
