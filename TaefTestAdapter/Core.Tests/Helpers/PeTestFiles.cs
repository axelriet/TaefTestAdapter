// This file has been added for TAEF support.
// It is also compiled into DiaResolver.Tests (linked file, see DiaResolver.Tests.csproj).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TaefTestAdapter.DiaResolver;
using TaefTestAdapter.Helpers;

namespace TaefTestAdapter.TestHelpers
{

    /// <summary>
    /// Builds minimal PE files in memory (no code; only the headers, an import section, and optionally a delay-load import
    /// section, a debug directory with a PDB path and a TAEF metadata section), e.g. to test the static detection of TAEF
    /// test DLLs (PeParser.IsTaefTestDll), the architecture detection and the PDB path extraction without any built
    /// binaries. The files can not be loaded by Windows.
    /// </summary>
    internal class SyntheticPeFile
    {
        public const ushort MachineX86 = 0x014C;
        public const ushort MachineX64 = 0x8664;
        public const ushort MachineArm64 = 0xAA64;
        public const ushort MachineArm64EC = 0xA641;
        public const ushort MachineArmNT = 0x01C4;

        /// <summary>Image base written into the optional header.</summary>
        public const uint ImageBase = 0x10000000;

        private const int PeHeaderOffset = 0x80;
        private const int SizeOfHeaders = 0x400;
        private const int SectionSize = 0x200;
        private const uint FirstSectionRva = 0x1000;
        private const uint DebugDirectoryEntrySize = 28;

        /// <summary>Machine field of the COFF header (default: x64).</summary>
        public ushort Machine { get; set; } = MachineX64;

        /// <summary>PE32+ (64-bit) or PE32 (32-bit) optional header (default: derived from <see cref="Machine"/>).</summary>
        public bool? IsPe32Plus { get; set; }

        /// <summary>Sets the IMAGE_FILE_DLL flag (default: true).</summary>
        public bool IsDll { get; set; } = true;

        /// <summary>Names of the imported DLLs (default: KERNEL32.dll, Wex.Common.dll, Wex.Logger.dll).</summary>
        public IList<string> Imports { get; set; } = new List<string> { "KERNEL32.dll", "Wex.Common.dll", "Wex.Logger.dll" };

        /// <summary>Names of the delay-loaded DLLs (linker option /DELAYLOAD; default: none).</summary>
        public IList<string> DelayLoadImports { get; set; }

        /// <summary>
        /// If true (default), the delay-load descriptors contain RVAs (attribute dlattrRva, all linkers since VC 7),
        /// otherwise VAs (VC 6).
        /// </summary>
        public bool DelayLoadDescriptorsUseRvas { get; set; } = true;

        /// <summary>
        /// PDB path of the CodeView entry of the debug directory (UTF-8 encoded, like the MSVC linker does; default: none,
        /// i.e. no debug directory).
        /// </summary>
        public string PdbPath { get; set; }

        /// <summary>Name of the TAEF metadata section (default: testdata); null: no such section.</summary>
        public string TestDataSectionName { get; set; } = "testdata";

        /// <summary>Signature at the start of the TAEF metadata section (default: "TAEF").</summary>
        public byte[] TestDataSignature { get; set; } = Encoding.ASCII.GetBytes("TAEF");

        /// <summary>TAEF ABI version following the signature (default: 12).</summary>
        public ulong TaefAbiVersion { get; set; } = 12;

        /// <summary>Size of the raw data of the TAEF metadata section (default: 0x200).</summary>
        public int TestDataRawSize { get; set; } = SectionSize;

        private bool Pe32Plus => IsPe32Plus ?? (Machine != MachineX86 && Machine != MachineArmNT);

        /// <summary>A PE file which PeParser.IsTaefTestDll() recognizes as TAEF test DLL.</summary>
        public static SyntheticPeFile TaefTestDll(ushort machine = MachineX64)
        {
            return new SyntheticPeFile { Machine = machine };
        }

        /// <summary>A plain DLL without TAEF metadata.</summary>
        public static SyntheticPeFile PlainDll(ushort machine = MachineX64)
        {
            return new SyntheticPeFile { Machine = machine, TestDataSectionName = null, Imports = new List<string> { "KERNEL32.dll" } };
        }

        public string WriteTo(string file)
        {
            File.WriteAllBytes(file, Build());
            return file;
        }

        public byte[] Build()
        {
            // sections are placed at consecutive RVAs (0x1000 apart) and file offsets (aligned to 0x200)
            var sections = new List<(string Name, byte[] Data)>();
            uint nextRva = FirstSectionRva;
            int nextRawOffset = SizeOfHeaders;
            uint AddSection(string name, Func<uint, int, byte[]> buildData)
            {
                uint sectionRva = nextRva;
                byte[] data = buildData(sectionRva, nextRawOffset);
                sections.Add((name, data));
                nextRva += 0x1000;
                nextRawOffset += Align(data.Length);
                return sectionRva;
            }

            uint importRva = Imports != null && Imports.Count > 0 ? AddSection(".idata", (r, o) => BuildImportSection(r)) : 0;
            uint delayImportRva = DelayLoadImports != null && DelayLoadImports.Count > 0 ? AddSection(".didat", (r, o) => BuildDelayImportSection(r)) : 0;
            uint debugRva = PdbPath != null ? AddSection(".rdata", BuildDebugSection) : 0;
            if (TestDataSectionName != null)
                AddSection(TestDataSectionName, (r, o) => BuildTestDataSection());

            int sizeOfOptionalHeader = Pe32Plus ? 240 : 224;
            var file = new byte[SizeOfHeaders + sections.Sum(s => Align(s.Data.Length))];

            // DOS header
            file[0] = (byte)'M';
            file[1] = (byte)'Z';
            WriteUInt32(file, 0x3C, PeHeaderOffset);

            // PE signature and COFF file header
            int offset = PeHeaderOffset;
            file[offset] = (byte)'P';
            file[offset + 1] = (byte)'E';
            WriteUInt16(file, offset + 4, Machine);
            WriteUInt16(file, offset + 6, (ushort)sections.Count);
            WriteUInt16(file, offset + 20, (ushort)sizeOfOptionalHeader);
            ushort characteristics = 0x0002 /* executable image */;
            characteristics |= Pe32Plus ? (ushort)0x0020 /* large address aware */ : (ushort)0x0100 /* 32 bit machine */;
            if (IsDll)
                characteristics |= 0x2000;
            WriteUInt16(file, offset + 22, characteristics);

            // optional header
            int optionalHeader = offset + 24;
            WriteUInt16(file, optionalHeader, Pe32Plus ? (ushort)0x20B : (ushort)0x10B);
            if (Pe32Plus)
                Array.Copy(BitConverter.GetBytes((ulong)ImageBase), 0, file, optionalHeader + 24, 8);
            else
                WriteUInt32(file, optionalHeader + 28, ImageBase);
            WriteUInt32(file, optionalHeader + 32, 0x1000); // section alignment
            WriteUInt32(file, optionalHeader + 36, 0x200); // file alignment
            WriteUInt32(file, optionalHeader + 56, (uint)(FirstSectionRva + sections.Count * 0x1000)); // size of image
            WriteUInt32(file, optionalHeader + 60, SizeOfHeaders);
            WriteUInt16(file, optionalHeader + 68, 2); // subsystem: GUI
            int dataDirectories = optionalHeader + (Pe32Plus ? 112 : 96);
            WriteUInt32(file, dataDirectories - 4, 16); // number of RVAs and sizes
            if (importRva != 0)
            {
                WriteUInt32(file, dataDirectories + 1 * 8, importRva);
                WriteUInt32(file, dataDirectories + 1 * 8 + 4, (uint)((Imports.Count + 1) * 20));
            }
            if (debugRva != 0)
            {
                WriteUInt32(file, dataDirectories + 6 * 8, debugRva);
                WriteUInt32(file, dataDirectories + 6 * 8 + 4, DebugDirectoryEntrySize);
            }
            if (delayImportRva != 0)
            {
                WriteUInt32(file, dataDirectories + 13 * 8, delayImportRva);
                WriteUInt32(file, dataDirectories + 13 * 8 + 4, (uint)((DelayLoadImports.Count + 1) * 32));
            }

            // section table and sections
            int sectionHeader = optionalHeader + sizeOfOptionalHeader;
            int rawOffset = SizeOfHeaders;
            uint rva = FirstSectionRva;
            foreach ((string name, byte[] data) in sections)
            {
                byte[] nameBytes = Encoding.ASCII.GetBytes(name);
                Array.Copy(nameBytes, 0, file, sectionHeader, Math.Min(8, nameBytes.Length));
                WriteUInt32(file, sectionHeader + 8, (uint)data.Length); // virtual size
                WriteUInt32(file, sectionHeader + 12, rva);
                WriteUInt32(file, sectionHeader + 16, (uint)data.Length); // size of raw data (not aligned, allows testing too small sections)
                WriteUInt32(file, sectionHeader + 20, (uint)rawOffset);
                WriteUInt32(file, sectionHeader + 36, 0x40000040); // initialized data, readable
                Array.Copy(data, 0, file, rawOffset, data.Length);

                sectionHeader += 40;
                rawOffset += Align(data.Length);
                rva += 0x1000;
            }

            return file;
        }

        private byte[] BuildImportSection(uint sectionRva)
        {
            var data = new byte[SectionSize];
            int namesOffset = (Imports.Count + 1) * 20;
            for (int i = 0; i < Imports.Count; i++)
            {
                byte[] name = Encoding.ASCII.GetBytes(Imports[i]);
                if (namesOffset + name.Length + 1 > data.Length)
                    throw new InvalidOperationException("Too many imports");

                int descriptor = i * 20;
                WriteUInt32(data, descriptor, sectionRva + (uint)(data.Length - 8)); // original first thunk (dummy, points to zeros)
                WriteUInt32(data, descriptor + 12, sectionRva + (uint)namesOffset); // name
                WriteUInt32(data, descriptor + 16, sectionRva + (uint)(data.Length - 8)); // first thunk (dummy)
                Array.Copy(name, 0, data, namesOffset, name.Length);
                namesOffset += name.Length + 1;
            }
            return data;
        }

        private byte[] BuildDelayImportSection(uint sectionRva)
        {
            // IMAGE_DELAYLOAD_DESCRIPTORs (32 bytes: attributes, DLL name, module handle, IAT, INT, bound IAT, unload IAT,
            // time stamp) terminated by an all-zero descriptor, followed by the DLL names
            var data = new byte[SectionSize];
            int namesOffset = (DelayLoadImports.Count + 1) * 32;
            uint baseAddress = DelayLoadDescriptorsUseRvas ? 0 : ImageBase;
            for (int i = 0; i < DelayLoadImports.Count; i++)
            {
                byte[] name = Encoding.ASCII.GetBytes(DelayLoadImports[i]);
                if (namesOffset + name.Length + 1 > data.Length)
                    throw new InvalidOperationException("Too many delay-load imports");

                int descriptor = i * 32;
                WriteUInt32(data, descriptor, DelayLoadDescriptorsUseRvas ? 1u : 0u); // attributes
                WriteUInt32(data, descriptor + 4, baseAddress + sectionRva + (uint)namesOffset); // DLL name
                WriteUInt32(data, descriptor + 12, baseAddress + sectionRva + (uint)(data.Length - 8)); // IAT (dummy)
                WriteUInt32(data, descriptor + 16, baseAddress + sectionRva + (uint)(data.Length - 8)); // INT (dummy)
                Array.Copy(name, 0, data, namesOffset, name.Length);
                namesOffset += name.Length + 1;
            }
            return data;
        }

        private byte[] BuildDebugSection(uint sectionRva, int rawOffset)
        {
            // one IMAGE_DEBUG_DIRECTORY entry (type CodeView), followed by the CodeView data: "RSDS", GUID, age, PDB path
            byte[] path = Encoding.UTF8.GetBytes(PdbPath);
            int codeViewSize = 24 + path.Length + 1;
            var data = new byte[DebugDirectoryEntrySize + codeViewSize];
            WriteUInt32(data, 12, 2); // IMAGE_DEBUG_TYPE_CODEVIEW
            WriteUInt32(data, 16, (uint)codeViewSize);
            WriteUInt32(data, 20, sectionRva + DebugDirectoryEntrySize); // address of raw data
            WriteUInt32(data, 24, (uint)rawOffset + DebugDirectoryEntrySize); // pointer to raw data

            int codeView = (int)DebugDirectoryEntrySize;
            Array.Copy(Encoding.ASCII.GetBytes("RSDS"), 0, data, codeView, 4);
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, data, codeView + 4, 16);
            WriteUInt32(data, codeView + 20, 1); // age
            Array.Copy(path, 0, data, codeView + 24, path.Length);
            return data;
        }

        private byte[] BuildTestDataSection()
        {
            var data = new byte[TestDataRawSize];
            int pointerSize = Pe32Plus ? 8 : 4;
            Array.Copy(TestDataSignature, 0, data, 0, Math.Min(TestDataSignature.Length, data.Length));
            if (data.Length >= 2 * pointerSize)
            {
                byte[] abi = pointerSize == 8 ? BitConverter.GetBytes(TaefAbiVersion) : BitConverter.GetBytes((uint)TaefAbiVersion);
                Array.Copy(abi, 0, data, pointerSize, pointerSize);
            }
            return data;
        }

        private static int Align(int size) => Math.Max(0x200, (size + 0x1FF) & ~0x1FF);

        private static void WriteUInt16(byte[] data, int offset, ushort value) => Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 2);

        private static void WriteUInt32(byte[] data, int offset, uint value) => Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 4);
    }


    internal static class EmbeddedPdbPath
    {
        /// <summary>
        /// Changes the PDB path embedded into (a copy of) a DLL such that it points to a non-existing file (the last
        /// character of the path is replaced; the length of the path does not change). Afterwards, the PDB of the DLL is
        /// only found if it is located next to the DLL (or on the PATH). Never use this on the shared sample DLLs.
        /// </summary>
        /// <returns>The new (non-existing) embedded PDB path.</returns>
        public static string Break(string dllCopy)
        {
            string pdbPath = PeParser.ExtractPdbPath(dllCopy, new Tests.Common.Fakes.FakeLogger());
            if (string.IsNullOrEmpty(pdbPath))
                throw new InvalidOperationException($"DLL '{dllCopy}' does not contain a PDB path");

            string newPdbPath = pdbPath.Substring(0, pdbPath.Length - 1) + (pdbPath.EndsWith("x", StringComparison.Ordinal) ? "y" : "x");
            byte[] pattern = Encoding.UTF8.GetBytes(pdbPath + "\0");
            byte[] replacement = Encoding.UTF8.GetBytes(newPdbPath + "\0");

            byte[] bytes = File.ReadAllBytes(dllCopy);
            int index = bytes.IndexOf(pattern);
            if (index < 0)
                throw new InvalidOperationException($"PDB path '{pdbPath}' not found in DLL '{dllCopy}'");
            Array.Copy(replacement, 0, bytes, index, replacement.Length);
            File.WriteAllBytes(dllCopy, bytes);

            string actualPdbPath = PeParser.ExtractPdbPath(dllCopy, new Tests.Common.Fakes.FakeLogger());
            if (actualPdbPath != newPdbPath || File.Exists(newPdbPath))
                throw new InvalidOperationException($"Failed to change PDB path of '{dllCopy}': '{actualPdbPath}'");

            return newPdbPath;
        }
    }

}
