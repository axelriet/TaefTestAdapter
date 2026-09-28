// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaefTestAdapter.Tests.Common.Helpers
{
    /// <summary>
    /// A copy of a sample test DLL in a new temporary directory, for tests which modify files (rename or delete the PDB,
    /// write indicator, helper or durations files, mark the DLL as downloaded, ...) without affecting the shared sample
    /// folders. The directory is deleted on <see cref="Dispose"/>.
    /// </summary>
    /// <remarks>
    /// Copied are the DLL, its PDB (always with its original name, since that name is embedded into the DLL), all files
    /// named <c>&lt;DLL file name&gt;.*</c> (e.g. <c>HelperFileTests_taef.dll.taef_settings_helper</c>; renamed if the DLL
    /// is renamed; test durations files are not copied) and the runtime dependencies of the DLL
    /// (<see cref="TestResources.GetRuntimeDependencies"/>, e.g. DllProject.dll for DllTests_taef.dll).
    /// </remarks>
    public sealed class SampleCopy : IDisposable
    {
        private readonly TemporaryDirectory _directory;

        /// <summary>Full path of the temporary directory.</summary>
        public string Directory => _directory.Path;

        /// <summary>Full path of the copied test DLL.</summary>
        public string TestDll { get; }

        /// <summary>Full path of the copied PDB (does not exist if it has not been copied).</summary>
        public string Pdb { get; }

        /// <summary>Full path of the original sample DLL.</summary>
        public string OriginalTestDll { get; }

        private SampleCopy(TemporaryDirectory directory, string originalTestDll, string testDll, string pdb)
        {
            _directory = directory;
            OriginalTestDll = originalTestDll;
            TestDll = testDll;
            Pdb = pdb;
        }

        /// <param name="sampleDll">Full path of the sample DLL, e.g. <see cref="TestResources.Tests_DebugX64"/>.</param>
        /// <param name="targetFileName">File name of the copy (default: the original file name).</param>
        /// <param name="copyPdb">If false, the PDB is not copied.</param>
        /// <param name="copyDependencies">If false, neither runtime dependencies (e.g. DllProject.dll) nor <c>&lt;DLL&gt;.*</c> files are copied.</param>
        public static SampleCopy Create(string sampleDll, string targetFileName = null, bool copyPdb = true, bool copyDependencies = true)
        {
            sampleDll = Path.GetFullPath(sampleDll);
            if (!File.Exists(sampleDll))
                throw new FileNotFoundException($"Sample test DLL does not exist - build SampleTests.sln or set environment variable {TestResources.SamplesDirEnvVariable}", sampleDll);

            var directory = new TemporaryDirectory();
            try
            {
                string sourceDir = Path.GetDirectoryName(sampleDll) ?? "";
                string sourceFileName = Path.GetFileName(sampleDll);
                targetFileName = targetFileName ?? sourceFileName;

                string testDll = directory.GetPath(targetFileName);
                File.Copy(sampleDll, testDll);

                string pdbFileName = Path.GetFileNameWithoutExtension(sourceFileName) + ".pdb";
                string pdb = directory.GetPath(pdbFileName);
                string sourcePdb = Path.Combine(sourceDir, pdbFileName);
                if (copyPdb && File.Exists(sourcePdb))
                    File.Copy(sourcePdb, pdb);

                if (copyDependencies)
                {
                    foreach (string file in System.IO.Directory.GetFiles(sourceDir, sourceFileName + ".*"))
                    {
                        // note that the pattern also matches the DLL itself
                        string fileName = Path.GetFileName(file);
                        if (!fileName.StartsWith(sourceFileName + ".", StringComparison.OrdinalIgnoreCase)
                            || fileName.EndsWith(TaefConstants.DurationsExtension, StringComparison.OrdinalIgnoreCase))
                            continue;
                        string suffix = fileName.Substring(sourceFileName.Length);
                        File.Copy(file, directory.GetPath(targetFileName + suffix));
                    }

                    foreach (string dependency in TestResources.GetRuntimeDependencies(sourceFileName))
                    {
                        foreach (string file in GetFileAndPdb(Path.Combine(sourceDir, dependency)))
                            File.Copy(file, directory.GetPath(Path.GetFileName(file)));
                    }
                }

                return new SampleCopy(directory, sampleDll, testDll, pdb);
            }
            catch
            {
                directory.Dispose();
                throw;
            }
        }

        private static IEnumerable<string> GetFileAndPdb(string file)
        {
            return new[] { file, Path.ChangeExtension(file, ".pdb") }.Where(File.Exists);
        }

        /// <returns>Full path of <paramref name="relativePath"/> within the temporary directory.</returns>
        public string GetPath(string relativePath) => _directory.GetPath(relativePath);

        public override string ToString() => TestDll;

        public void Dispose()
        {
            _directory.Dispose();
        }
    }

}
