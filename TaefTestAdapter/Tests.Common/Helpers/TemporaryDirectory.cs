// This file has been added for TAEF support.

using System;
using System.IO;
using System.Text;
using System.Threading;
using TaefTestAdapter.Helpers;

namespace TaefTestAdapter.Tests.Common.Helpers
{
    /// <summary>
    /// A new, empty directory below %TEMP% which is deleted (best effort) on <see cref="Dispose"/>.
    /// </summary>
    public class TemporaryDirectory : IDisposable
    {
        /// <summary>Full path of the directory (without trailing backslash).</summary>
        public string Path { get; }

        public TemporaryDirectory()
        {
            Path = Utils.GetTempDirectory();
        }

        /// <returns>Full path of <paramref name="relativePath"/> within the directory.</returns>
        public string GetPath(string relativePath) => System.IO.Path.Combine(Path, relativePath);

        /// <summary>Writes a UTF-8 (without BOM) file into the directory (creating sub directories as needed).</summary>
        /// <returns>Full path of the file.</returns>
        public string CreateFile(string relativePath, string content = "")
        {
            string file = GetPath(relativePath);
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            File.WriteAllText(file, content, new UTF8Encoding(false));
            return file;
        }

        public override string ToString() => Path;

        public void Dispose()
        {
            // processes (e.g. TE.ProcessHost.exe) or virus scanners might still hold files for a short time
            for (int i = 0; i < 10 && Directory.Exists(Path); i++)
            {
                if (Utils.DeleteDirectory(Path))
                    break;
                Thread.Sleep(100);
            }
        }
    }

}
