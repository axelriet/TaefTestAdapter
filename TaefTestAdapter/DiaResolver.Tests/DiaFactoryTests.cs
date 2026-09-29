// This file has been added for TAEF support.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using TaefTestAdapter.Common;
using TaefTestAdapter.Tests.Common.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>
    /// Tests of the choice of msdia140.dll by <c>DiaFactory</c> (internal, so its method is called by reflection): each
    /// process architecture needs the DLL of its own architecture, which must be next to the adapter's assemblies.
    /// </summary>
    [TestClass]
    public class DiaFactoryTests
    {
        private const string DiaDll = "msdia140.dll";

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitectureDir_ProcessArchitectures_DirOfTheArchitecture()
        {
            GetArchitectureDir(Architecture.X86, true).Should().Be("x86");
            GetArchitectureDir(Architecture.X64, false).Should().Be("x64");
            GetArchitectureDir(Architecture.Arm64, false).Should().Be("arm64");
        }

        /// <summary>
        /// An x64 or ARM64EC process on ARM64 Windows reports <see cref="Architecture.X64"/>: it cannot load the arm64 DLL.
        /// The architecture decides, not the bitness.
        /// </summary>
        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitectureDir_ArchitectureAndBitnessDisagree_ArchitectureDecides()
        {
            GetArchitectureDir(Architecture.X64, true).Should().Be("x64");
            GetArchitectureDir(Architecture.Arm64, true).Should().Be("arm64");
            GetArchitectureDir(Architecture.X86, false).Should().Be("x86");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void GetArchitectureDir_ArchitectureWithoutDll_DirOfTheBitness()
        {
            GetArchitectureDir(Architecture.Arm, true).Should().Be("x86");
            GetArchitectureDir(Architecture.Arm, false).Should().Be("x64");
            GetArchitectureDir((Architecture)42, true).Should().Be("x86");
            GetArchitectureDir((Architecture)42, false).Should().Be("x64");
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DiaDll_CurrentProcess_ExistsWithTheMachineTypeOfTheProcess()
        {
            string architectureDir = GetArchitectureDir(RuntimeInformation.ProcessArchitecture, IntPtr.Size == 4);

            AssertDiaDll(architectureDir, GetMachineType(architectureDir));
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DiaDlls_X86AndX64_ExistWithTheirMachineType()
        {
            AssertDiaDll("x86", PeParser.MachineX86);
            AssertDiaDll("x64", PeParser.MachineX64);
        }

        [TestMethod]
        [TestCategory(Unit)]
        public void DiaDll_Arm64_ExistsWithItsMachineType()
        {
            string dll = Path.Combine(GetAdapterDir(), "arm64", DiaDll);
            if (!File.Exists(dll))
                Assert.Inconclusive($"The DIA SDK of the Visual Studio used for the build has no arm64 {DiaDll} (see build.ps1): {dll}");

            AssertDiaDll("arm64", PeParser.MachineArm64);
        }

        private static void AssertDiaDll(string architectureDir, ushort expectedMachineType)
        {
            string dll = Path.Combine(GetAdapterDir(), architectureDir, DiaDll);
            File.Exists(dll).Should().BeTrue(dll);
            PeParser.GetMachineType(dll, new FakeLogger(() => OutputMode.Info)).Should().Be(expectedMachineType, dll);
        }

        private static ushort GetMachineType(string architectureDir)
        {
            switch (architectureDir)
            {
                case "x86":
                    return PeParser.MachineX86;
                case "x64":
                    return PeParser.MachineX64;
                case "arm64":
                    return PeParser.MachineArm64;
                default:
                    throw new ArgumentException(architectureDir, nameof(architectureDir));
            }
        }

        /// <returns>The folder of the adapter's assemblies (as determined by <c>DiaFactory</c>).</returns>
        private static string GetAdapterDir()
        {
            string codeBase = typeof(DefaultDiaResolverFactory).Assembly.CodeBase;
            return Path.GetDirectoryName(Uri.UnescapeDataString(new UriBuilder(codeBase).Path));
        }

        private static string GetArchitectureDir(Architecture processArchitecture, bool is32BitProcess)
        {
            MethodInfo method = typeof(DefaultDiaResolverFactory).Assembly
                .GetType("TaefTestAdapter.DiaResolver.DiaFactory", true)
                .GetMethod("GetArchitectureDir", BindingFlags.Static | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            // ReSharper disable once PossibleNullReferenceException
            return (string)method.Invoke(null, new object[] { processArchitecture, is32BitProcess });
        }
    }
}
