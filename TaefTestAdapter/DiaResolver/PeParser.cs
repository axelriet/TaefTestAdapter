// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.DiaResolver
{

    /// <summary>
    /// Reads information from the headers of PE files (DLLs and executables) without loading them and without starting any
    /// process: the imported DLLs, the path of the PDB embedded by the linker, the machine type, and whether a DLL is a TAEF
    /// test DLL. The files are read with managed file I/O, so their paths may contain any characters (e.g. characters which
    /// are not part of the ANSI code page).
    /// </summary>
    public static class PeParser
    {
        #region Imports and PDB path

        /// <returns>
        /// The names of the DLLs imported by <paramref name="peFile"/> in the order of its import directory (without
        /// delay-loaded DLLs); empty if the file can not be read or is not a PE file.
        /// </returns>
        public static List<string> ParseImports(string peFile, ILogger logger)
        {
            var imports = new List<string>();
            ReadPeFile(peFile, logger, "imports", (stream, headers) => imports.AddRange(EnumerateImports(stream, headers)));
            return imports;
        }

        /// <returns>
        /// True if <paramref name="peFile"/> imports <paramref name="import"/> (compared with
        /// <paramref name="comparisonType"/>; delay-loaded DLLs are not considered).
        /// </returns>
        public static bool FindImport(string peFile, string import, StringComparison comparisonType, ILogger logger)
        {
            bool found = false;
            ReadPeFile(peFile, logger, "imports", (stream, headers) =>
                found = EnumerateImports(stream, headers).Any(currentImport => string.Compare(import, currentImport, comparisonType) == 0));
            return found;
        }

        // Most Windows DLLs and executables contain the path to their PDB
        // in the header. This should be the most stable way to
        // determine the location and the name of the PDB.
        //
        // This is inspired by
        // https://deplinenoise.wordpress.com/2013/06/14/getting-your-pdb-name-from-a-running-executable-windows/
        //
        // The CodeView entry (type IMAGE_DEBUG_TYPE_CODEVIEW, signature "RSDS") of the debug directory holds the PDB
        // path; linkers usually emit further entries (e.g. VC_FEATURE, POGO), so all entries are checked. The MSVC linker
        // stores the path UTF-8 encoded.
        /// <returns>The PDB path stored in <paramref name="peFile"/>, or null if there is none or the file can not be read.</returns>
        public static string ExtractPdbPath(string peFile, ILogger logger)
        {
            string pdbPath = null;
            ReadPeFile(peFile, logger, "PDB path", (stream, headers) => pdbPath = ReadPdbPath(stream, headers));
            return pdbPath;
        }

        private static void ReadPeFile(string file, ILogger logger, string subject, Action<Stream, PeHeaders> action)
        {
            try
            {
                using (var stream = OpenForReading(file))
                {
                    var headers = PeHeaders.Read(stream);
                    if (headers != null)
                        action(stream, headers);
                }
            }
            catch (Exception e)
            {
                logger?.DebugWarning($"Could not read the {subject} of '{file}': {e.Message}");
            }
        }

        /// <returns>The names of the DLLs of the import directory (IMAGE_IMPORT_DESCRIPTORs).</returns>
        private static IEnumerable<string> EnumerateImports(Stream stream, PeHeaders headers)
        {
            foreach (byte[] descriptor in ReadDescriptors(stream, headers, headers.ImportDirectoryRva, ImportDescriptorSize))
            {
                uint nameRva = BitConverter.ToUInt32(descriptor, ImportDescriptorNameOffset);
                if (nameRva == 0)
                    yield break; // terminating (all zero) descriptor

                string name = ReadDllName(stream, headers, nameRva);
                if (name != null)
                    yield return name;
            }
        }

        /// <returns>
        /// The names of the DLLs of the delay-load import directory (IMAGE_DELAYLOAD_DESCRIPTORs, created by linker option
        /// <c>/DELAYLOAD</c>).
        /// </returns>
        private static IEnumerable<string> EnumerateDelayLoadImports(Stream stream, PeHeaders headers)
        {
            foreach (byte[] descriptor in ReadDescriptors(stream, headers, headers.DelayImportDirectoryRva, DelayImportDescriptorSize))
            {
                uint attributes = BitConverter.ToUInt32(descriptor, 0);
                uint name = BitConverter.ToUInt32(descriptor, DelayImportDescriptorNameOffset);
                if (name == 0)
                    yield break; // terminating (all zero) descriptor

                // the addresses of a descriptor are RVAs if attribute dlattrRva is set (all linkers since VC 7), VAs otherwise
                ulong nameRva = (attributes & DelayLoadAttributeRva) != 0 ? name : unchecked((ulong)name - headers.ImageBase);
                if (nameRva == 0 || nameRva > uint.MaxValue)
                    continue;

                string dllName = ReadDllName(stream, headers, (uint)nameRva);
                if (dllName != null)
                    yield return dllName;
            }
        }

        /// <returns>
        /// The descriptors of the data directory starting at <paramref name="directoryRva"/>, up to the end of the file (the
        /// caller has to detect the terminating descriptor).
        /// </returns>
        private static IEnumerable<byte[]> ReadDescriptors(Stream stream, PeHeaders headers, uint directoryRva, int descriptorSize)
        {
            if (directoryRva == 0)
                yield break;

            long descriptorOffset = headers.RvaToFileOffset(directoryRva);
            if (descriptorOffset < 0)
                yield break;

            for (int i = 0; i < MaxNrOfImportDescriptors; i++, descriptorOffset += descriptorSize)
            {
                byte[] descriptor = ReadBytes(stream, descriptorOffset, descriptorSize);
                if (descriptor == null)
                    yield break;
                yield return descriptor;
            }
        }

        private static string ReadDllName(Stream stream, PeHeaders headers, uint nameRva)
        {
            long nameOffset = headers.RvaToFileOffset(nameRva);
            return nameOffset < 0 ? null : ReadAsciiString(stream, nameOffset, MaxImportNameLength);
        }

        private static string ReadPdbPath(Stream stream, PeHeaders headers)
        {
            if (headers.DebugDirectoryRva == 0)
                return null;

            long directoryOffset = headers.RvaToFileOffset(headers.DebugDirectoryRva);
            if (directoryOffset < 0)
                return null;

            uint nrOfEntries = Math.Min(MaxNrOfDebugDirectoryEntries, Math.Max(1u, headers.DebugDirectorySize / DebugDirectoryEntrySize));
            for (uint i = 0; i < nrOfEntries; i++)
            {
                byte[] entry = ReadBytes(stream, directoryOffset + i * DebugDirectoryEntrySize, (int)DebugDirectoryEntrySize);
                if (entry == null)
                    return null;

                uint type = BitConverter.ToUInt32(entry, 12);
                uint sizeOfData = BitConverter.ToUInt32(entry, 16);
                if (type != DebugTypeCodeView || sizeOfData <= CodeViewPdbPathOffset)
                    continue;

                uint addressOfRawData = BitConverter.ToUInt32(entry, 20);
                uint pointerToRawData = BitConverter.ToUInt32(entry, 24);
                long dataOffset = addressOfRawData != 0 ? headers.RvaToFileOffset(addressOfRawData) : -1;
                if (dataOffset < 0 && pointerToRawData != 0)
                    dataOffset = pointerToRawData;
                if (dataOffset < 0 || dataOffset >= stream.Length)
                    continue;

                int size = (int)Math.Min(Math.Min(sizeOfData, MaxCodeViewDataSize), stream.Length - dataOffset);
                byte[] data = ReadBytes(stream, dataOffset, size);
                if (data == null || data.Length <= CodeViewPdbPathOffset || BitConverter.ToUInt32(data, 0) != CodeViewRsdsSignature)
                    continue;

                int end = Array.IndexOf(data, (byte)0, CodeViewPdbPathOffset);
                return DecodePdbPath(data, CodeViewPdbPathOffset, (end < 0 ? data.Length : end) - CodeViewPdbPathOffset);
            }

            return null;
        }

        private static string DecodePdbPath(byte[] bytes, int index, int count)
        {
            try
            {
                return StrictUtf8.GetString(bytes, index, count);
            }
            catch (DecoderFallbackException)
            {
                // not written by the MSVC linker
                return Encoding.Default.GetString(bytes, index, count);
            }
        }

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // IMAGE_DEBUG_DIRECTORY and CodeView data ("RSDS" signature, GUID, age, PDB path)
        private const uint DebugDirectoryEntrySize = 28;
        private const uint MaxNrOfDebugDirectoryEntries = 64;
        private const uint DebugTypeCodeView = 2;
        private const uint CodeViewRsdsSignature = 0x53445352; // "RSDS"
        private const int CodeViewPdbPathOffset = 24;
        private const uint MaxCodeViewDataSize = 0x10000;

        #endregion

        #region Static TAEF test DLL detection (TAEF support)

        // Values of the PE file header's Machine field
        public const ushort MachineUnknown = 0x0000;
        public const ushort MachineX86 = 0x014C;
        public const ushort MachineX64 = 0x8664;
        public const ushort MachineArm64 = 0xAA64;
        /// <summary>
        /// IMAGE_FILE_MACHINE_ARM64EC: only used in object files. ARM64EC images (DLLs) carry machine type
        /// <see cref="MachineX64"/>, ARM64X images <see cref="MachineArm64"/>.
        /// </summary>
        public const ushort MachineArm64EC = 0xA641;
        public const ushort MachineArm = 0x01C0;
        public const ushort MachineArmThumb = 0x01C2;
        public const ushort MachineArmNT = 0x01C4;

        /// <summary>
        /// Name of the PE section which holds TAEF's test metadata (the WexTestClass.h pragma sections
        /// testdata$a_TDH ... testdata$h_TMM are merged into it by the linker).
        /// </summary>
        public const string TaefMetadataSectionName = "testdata";

        /// <summary>Minimum and maximum TAEF metadata ABI version accepted (12 on x86/x64 and 13 on ARM64 at the time of writing).</summary>
        public const ulong MinTaefAbiVersion = 10;
        public const ulong MaxTaefAbiVersion = 13;

        /// <summary>Real TAEF test DLLs import (or delay-load) at least one of these DLLs.</summary>
        public static readonly IReadOnlyList<string> TaefImports = Array.AsReadOnly(new[] { "Wex.Logger.dll", "Wex.Common.dll", "TE.Common.dll" });

        // "TAEF" (TAEF_HEADER_SIGNATURE 0x46454154, little endian)
        private static readonly byte[] TaefSignature = { 0x54, 0x41, 0x45, 0x46 };

        private const ushort ImageFileDll = 0x2000;
        private const ushort OptionalHeaderMagicPe32 = 0x10B;
        private const ushort OptionalHeaderMagicPe32Plus = 0x20B;
        private const int SectionHeaderSize = 40;
        // IMAGE_IMPORT_DESCRIPTOR: name RVA at offset 12
        private const int ImportDescriptorSize = 20;
        private const int ImportDescriptorNameOffset = 12;
        // IMAGE_DELAYLOAD_DESCRIPTOR: attributes at offset 0, DLL name at offset 4
        private const int DelayImportDescriptorSize = 32;
        private const int DelayImportDescriptorNameOffset = 4;
        private const uint DelayLoadAttributeRva = 0x1;
        // indexes of the data directories
        private const int ImportDataDirectory = 1;
        private const int DebugDataDirectory = 6;
        private const int DelayImportDataDirectory = 13;
        private const int MaxNrOfSections = 1024;
        private const int MaxNrOfImportDescriptors = 8192;
        private const int MaxImportNameLength = 512;

        /// <summary>
        /// Reads the Machine field of the PE file header of <paramref name="pe"/> (e.g. <see cref="MachineX86"/>,
        /// <see cref="MachineX64"/>, <see cref="MachineArm64"/>). Only the file headers are read.
        /// </summary>
        /// <returns>The machine type, or <see cref="MachineUnknown"/> if the file can not be read or is not a PE file. Never throws.</returns>
        public static ushort GetMachineType(string pe, ILogger logger)
        {
            try
            {
                using (var stream = OpenForReading(pe))
                {
                    var headers = PeHeaders.Read(stream);
                    if (headers == null)
                    {
                        logger?.DebugWarning($"Could not determine machine type of '{pe}': not a PE file");
                        return MachineUnknown;
                    }
                    return headers.Machine;
                }
            }
            catch (Exception e)
            {
                logger?.DebugWarning($"Could not determine machine type of '{pe}': {e.Message}");
                return MachineUnknown;
            }
        }

        /// <summary>
        /// Checks statically (without loading the DLL or starting any process) whether <paramref name="dll"/> is a TAEF
        /// test DLL: it must be a PE DLL with a section named exactly "testdata" whose raw data starts with the TAEF
        /// signature ("TAEF"), followed (at offset 4 for PE32, 8 for PE32+) by the ABI version (10..13), and it must
        /// import or delay-load (linker option <c>/DELAYLOAD</c>) at least one of Wex.Logger.dll, Wex.Common.dll and
        /// TE.Common.dll (DLLs only including WexTestClass.h have the metadata section, but no tests and no such import).
        /// Only the file headers, the section table, the start of the "testdata" section and the (delay-load) import
        /// directories are read.
        /// </summary>
        /// <returns>True if the DLL is a TAEF test DLL. Never throws; false for unreadable or malformed files.</returns>
        public static bool IsTaefTestDll(string dll, ILogger logger)
        {
            try
            {
                using (var stream = OpenForReading(dll))
                {
                    string reason = CheckTaefTestDll(stream);
                    if (reason == null)
                    {
                        logger?.VerboseInfo($"'{dll}' is a TAEF test DLL");
                        return true;
                    }

                    logger?.VerboseInfo($"'{dll}' is not a TAEF test DLL: {reason}");
                    return false;
                }
            }
            catch (Exception e)
            {
                logger?.DebugWarning($"Could not check whether '{dll}' is a TAEF test DLL: {e.Message}");
                return false;
            }
        }

        private static FileStream OpenForReading(string file)
        {
            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.RandomAccess);
        }

        /// <returns>null if the stream holds a TAEF test DLL, the reason why not otherwise</returns>
        private static string CheckTaefTestDll(Stream stream)
        {
            var headers = PeHeaders.Read(stream);
            if (headers == null)
                return "not a PE file";
            if ((headers.Characteristics & ImageFileDll) == 0)
                return "not a DLL";
            if (headers.PointerSize == 0)
                return "unknown optional header format";

            SectionHeader testDataSection = headers.Sections.FirstOrDefault(s => s.Name == TaefMetadataSectionName);
            if (testDataSection == null)
                return $"no '{TaefMetadataSectionName}' section";

            int pointerSize = headers.PointerSize;
            byte[] versionInfo = testDataSection.SizeOfRawData >= 2 * pointerSize
                ? ReadBytes(stream, testDataSection.PointerToRawData, 2 * pointerSize)
                : null;
            if (versionInfo == null || !versionInfo.Take(TaefSignature.Length).SequenceEqual(TaefSignature))
                return $"'{TaefMetadataSectionName}' section does not start with the TAEF signature";

            ulong abiVersion = pointerSize == 8
                ? BitConverter.ToUInt64(versionInfo, pointerSize)
                : BitConverter.ToUInt32(versionInfo, pointerSize);
            if (abiVersion < MinTaefAbiVersion || abiVersion > MaxTaefAbiVersion)
                return $"unsupported TAEF metadata ABI version {abiVersion}";

            string taefImport = FindImport(stream, headers, TaefImports);
            if (taefImport == null)
                return $"TAEF metadata found, but none of {string.Join(", ", TaefImports)} is imported or delay-loaded (DLL does not seem to contain tests)";

            return null;
        }

        /// <returns>
        /// The first import or delay-load import of the PE file which is contained in <paramref name="candidates"/>
        /// (ignoring case), or null
        /// </returns>
        private static string FindImport(Stream stream, PeHeaders headers, IReadOnlyList<string> candidates)
        {
            return EnumerateImports(stream, headers)
                .Concat(EnumerateDelayLoadImports(stream, headers))
                .Select(name => candidates.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(match => match != null);
        }

        private static byte[] ReadBytes(Stream stream, long offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > stream.Length)
                return null;

            stream.Position = offset;
            var buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                    return null;
                read += n;
            }
            return buffer;
        }

        private static string ReadAsciiString(Stream stream, long offset, int maxLength)
        {
            int count = (int)Math.Min(maxLength, stream.Length - offset);
            byte[] bytes = ReadBytes(stream, offset, count);
            if (bytes == null)
                return null;

            int length = Array.IndexOf(bytes, (byte)0);
            return length < 0 ? null : Encoding.ASCII.GetString(bytes, 0, length);
        }

        private class SectionHeader
        {
            public string Name;
            public uint VirtualSize;
            public uint VirtualAddress;
            public uint SizeOfRawData;
            public uint PointerToRawData;
        }

        /// <summary>
        /// The parts of the PE headers needed by the parser (see the PE/COFF specification). The data directories are 0 if
        /// the optional header is unknown.
        /// </summary>
        private class PeHeaders
        {
            public ushort Machine;
            public ushort Characteristics;
            /// <summary>4 for PE32, 8 for PE32+, 0 if the optional header is unknown</summary>
            public int PointerSize;
            public ulong ImageBase;
            public uint SizeOfHeaders;
            public uint ImportDirectoryRva;
            public uint DebugDirectoryRva;
            public uint DebugDirectorySize;
            public uint DelayImportDirectoryRva;
            public readonly List<SectionHeader> Sections = new List<SectionHeader>();

            /// <returns>The headers, or null if the stream does not hold a PE file</returns>
            public static PeHeaders Read(Stream stream)
            {
                // DOS header: "MZ", offset of the PE signature at 0x3C
                byte[] dosHeader = ReadBytes(stream, 0, 0x40);
                if (dosHeader == null || dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
                    return null;

                long peOffset = BitConverter.ToUInt32(dosHeader, 0x3C);

                // "PE\0\0" + COFF file header (20 bytes)
                byte[] fileHeader = ReadBytes(stream, peOffset, 24);
                if (fileHeader == null || fileHeader[0] != (byte)'P' || fileHeader[1] != (byte)'E' || fileHeader[2] != 0 || fileHeader[3] != 0)
                    return null;

                var headers = new PeHeaders
                {
                    Machine = BitConverter.ToUInt16(fileHeader, 4),
                    Characteristics = BitConverter.ToUInt16(fileHeader, 22)
                };
                int nrOfSections = BitConverter.ToUInt16(fileHeader, 6);
                int sizeOfOptionalHeader = BitConverter.ToUInt16(fileHeader, 20);

                // optional header
                long optionalHeaderOffset = peOffset + 24;
                byte[] optionalHeader = sizeOfOptionalHeader >= 2 ? ReadBytes(stream, optionalHeaderOffset, sizeOfOptionalHeader) : null;
                if (optionalHeader != null)
                {
                    ushort magic = BitConverter.ToUInt16(optionalHeader, 0);
                    int numberOfRvaAndSizesOffset = 0;
                    if (magic == OptionalHeaderMagicPe32)
                    {
                        headers.PointerSize = 4;
                        numberOfRvaAndSizesOffset = 92;
                    }
                    else if (magic == OptionalHeaderMagicPe32Plus)
                    {
                        headers.PointerSize = 8;
                        numberOfRvaAndSizesOffset = 108;
                    }

                    if (headers.PointerSize != 0 && sizeOfOptionalHeader >= 64)
                    {
                        headers.ImageBase = headers.PointerSize == 8 ? BitConverter.ToUInt64(optionalHeader, 24) : BitConverter.ToUInt32(optionalHeader, 28);
                        headers.SizeOfHeaders = BitConverter.ToUInt32(optionalHeader, 60);
                    }

                    if (headers.PointerSize != 0)
                    {
                        headers.ImportDirectoryRva = ReadDataDirectory(optionalHeader, numberOfRvaAndSizesOffset, ImportDataDirectory, out _);
                        headers.DebugDirectoryRva = ReadDataDirectory(optionalHeader, numberOfRvaAndSizesOffset, DebugDataDirectory, out headers.DebugDirectorySize);
                        headers.DelayImportDirectoryRva = ReadDataDirectory(optionalHeader, numberOfRvaAndSizesOffset, DelayImportDataDirectory, out _);
                    }
                }

                // section table
                long sectionTableOffset = optionalHeaderOffset + sizeOfOptionalHeader;
                byte[] sectionTable = ReadBytes(stream, sectionTableOffset, Math.Min(nrOfSections, MaxNrOfSections) * SectionHeaderSize);
                if (sectionTable != null)
                {
                    for (int i = 0; i * SectionHeaderSize < sectionTable.Length; i++)
                    {
                        int offset = i * SectionHeaderSize;
                        int nameLength = Array.IndexOf(sectionTable, (byte)0, offset, 8) - offset;
                        if (nameLength < 0)
                            nameLength = 8;

                        headers.Sections.Add(new SectionHeader
                        {
                            Name = Encoding.ASCII.GetString(sectionTable, offset, nameLength),
                            VirtualSize = BitConverter.ToUInt32(sectionTable, offset + 8),
                            VirtualAddress = BitConverter.ToUInt32(sectionTable, offset + 12),
                            SizeOfRawData = BitConverter.ToUInt32(sectionTable, offset + 16),
                            PointerToRawData = BitConverter.ToUInt32(sectionTable, offset + 20)
                        });
                    }
                }

                return headers;
            }

            /// <returns>
            /// The RVA of data directory <paramref name="index"/> (and its size), or 0 if the optional header does not
            /// contain it (the data directories follow the NumberOfRvaAndSizes field, 8 bytes each).
            /// </returns>
            private static uint ReadDataDirectory(byte[] optionalHeader, int numberOfRvaAndSizesOffset, int index, out uint size)
            {
                size = 0;
                int offset = numberOfRvaAndSizesOffset + 4 + index * 8;
                if (optionalHeader.Length < offset + 8 || BitConverter.ToUInt32(optionalHeader, numberOfRvaAndSizesOffset) <= index)
                    return 0;

                size = BitConverter.ToUInt32(optionalHeader, offset + 4);
                return BitConverter.ToUInt32(optionalHeader, offset);
            }

            /// <returns>The file offset of <paramref name="rva"/>, or -1 if it is not backed by the file</returns>
            public long RvaToFileOffset(uint rva)
            {
                foreach (SectionHeader section in Sections)
                {
                    uint size = Math.Max(section.VirtualSize, section.SizeOfRawData);
                    if (rva >= section.VirtualAddress && (ulong)rva < (ulong)section.VirtualAddress + size)
                    {
                        uint offsetInSection = rva - section.VirtualAddress;
                        return section.PointerToRawData == 0 || offsetInSection >= section.SizeOfRawData
                            ? -1
                            : section.PointerToRawData + (long)offsetInSection;
                    }
                }

                // RVAs within the headers map 1:1 to file offsets
                return rva < SizeOfHeaders ? (long)rva : -1;
            }
        }

        #endregion
    }
}
