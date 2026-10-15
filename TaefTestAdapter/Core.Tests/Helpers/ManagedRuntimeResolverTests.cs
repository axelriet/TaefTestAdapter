// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaefTestAdapter.TestHelpers;
using TaefTestAdapter.Tests.Common.Fakes;
using TaefTestAdapter.Tests.Common.Helpers;
using static TaefTestAdapter.Tests.Common.TestMetadata.TestCategories;

namespace TaefTestAdapter.Helpers
{
    [TestClass]
    public class ManagedRuntimeResolverTests
    {
        [TestMethod, TestCategory(Unit)]
        public void TargetFramework_OnlyCoreAppRequiresCoreClr()
        {
            ManagedRuntimeResolver.GetCoreClrVersion(".NETCoreApp,Version=v8.0").Should().Be(new Version(8, 0));
            ManagedRuntimeResolver.GetCoreClrVersion(".NETCoreApp,Version=v10.0").Should().Be(new Version(10, 0));
            ManagedRuntimeResolver.GetCoreClrVersion(".NETFramework,Version=v4.8").Should().BeNull();
            ManagedRuntimeResolver.GetCoreClrVersion(".NETStandard,Version=v2.0").Should().BeNull();
            ManagedRuntimeResolver.GetCoreClrVersion("invalid").Should().BeNull();
            ManagedRuntimeResolver.GetCoreClrVersion(null).Should().BeNull();
        }

        [TestMethod, TestCategory(Unit)]
        public void CompatibleRuntime_UsesNewestPatchOfLowestCompatibleMinorAndArchitecture()
        {
            using (var directory = new TemporaryDirectory())
            {
                WriteRuntime(directory, "8.0.1", SyntheticPeFile.MachineX64);
                string expected = WriteRuntime(directory, "8.0.9", SyntheticPeFile.MachineX64);
                WriteRuntime(directory, "8.0.99", SyntheticPeFile.MachineX86);
                WriteRuntime(directory, "8.1.2", SyntheticPeFile.MachineX64);
                WriteRuntime(directory, "10.0.1", SyntheticPeFile.MachineX64);
                ManagedRuntimeResolver.FindCompatibleRuntime(new Version(8, 0), "x64",
                    new[] { directory.Path }, new FakeLogger()).Should().Be(expected);
                ManagedRuntimeResolver.FindCompatibleRuntime(new Version(9, 0), "x64",
                    new[] { directory.Path }, new FakeLogger()).Should().BeNull();
                ManagedRuntimeResolver.FindCompatibleRuntime(new Version(8, 0), "arm64",
                    new[] { directory.Path }, new FakeLogger()).Should().BeNull();
            }
        }

        [TestMethod, TestCategory(Unit)]
        public void ExplicitRuntime_UsesSelectedTeArchitectureAndReportsAnInvalidOverride()
        {
            using (var directory = new TemporaryDirectory())
            {
                string te = directory.CreateFile("TE.exe");
                SyntheticPeFile.PlainDll(SyntheticPeFile.MachineX86).WriteTo(te);
                string runtime = WriteRuntime(directory, "8.0.1", SyntheticPeFile.MachineX64);
                var environment = new Dictionary<string, string> { [ManagedRuntimeResolver.CoreClrVariable] = runtime };
                var logger = new FakeLogger();
                ManagedRuntimeResolver.TryConfigureEnvironment(typeof(ManagedRuntimeResolver).Assembly.Location,
                    te, environment, logger, out bool usesCoreClr).Should().BeFalse();
                usesCoreClr.Should().BeTrue();
                logger.Errors.Should().ContainSingle().Which.Should().Contain("x86 coreclr.dll");
                environment[ManagedRuntimeResolver.CoreClrVariable] = "";
                ManagedRuntimeResolver.TryConfigureEnvironment(typeof(ManagedRuntimeResolver).Assembly.Location,
                    te, environment, logger, out usesCoreClr).Should().BeTrue();
                usesCoreClr.Should().BeFalse();
            }
        }

        [TestMethod, TestCategory(Unit)]
        public void AnyCpuImage_UsesOperatingSystemArchitectureInsteadOfPe32Machine()
        {
            TaefLocator.GetArchitecture(typeof(ManagedRuntimeResolver).Assembly.Location, new FakeLogger())
                .Should().Be(RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());
        }

        private static string WriteRuntime(TemporaryDirectory directory, string version, ushort machine)
        {
            string file = directory.CreateFile(Path.Combine("shared", "Microsoft.NETCore.App", version, "coreclr.dll"));
            SyntheticPeFile.PlainDll(machine).WriteTo(file);
            return Path.GetDirectoryName(file);
        }
    }
}
