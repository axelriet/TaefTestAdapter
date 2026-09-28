// This file has been modified for TAEF support.

using System;
using System.Linq;

namespace TaefTestAdapter.Tests.Common.Helpers
{

    public static class CiSupport
    {
        // AppVeyor, Azure Pipelines, GitHub Actions, and the generic variable set by most CI systems
        private static readonly string[] CiEnvironmentVariables = { "APPVEYOR", "TF_BUILD", "GITHUB_ACTIONS", "CI" };

        public static bool IsRunningOnBuildServer =>
            CiEnvironmentVariables.Any(v => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v)));

        private static double Weight => IsRunningOnBuildServer ? 2.5 : 1;


        /// <returns><paramref name="duration"/>, multiplied by 2.5 if running on a build server.</returns>
        public static int GetWeightedDuration(double duration)
        {
            return (int)Math.Round(duration * Weight);
        }

    }

}
