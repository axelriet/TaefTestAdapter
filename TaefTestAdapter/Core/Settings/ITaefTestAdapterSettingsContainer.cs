// This file has been modified for TAEF support.

using System.Collections.Generic;

namespace TaefTestAdapter.Settings
{
    /// <summary>
    /// The solution settings and the project settings of a settings file.
    /// </summary>
    public interface ITaefTestAdapterSettingsContainer
    {
        /// <summary>The solution settings.</summary>
        RunSettings SolutionSettings { get; }
        /// <summary>The project settings (each with a ProjectRegex).</summary>
        List<RunSettings> ProjectSettings { get; }

        /// <returns>The first project settings whose ProjectRegex matches the full path of <paramref name="testDll"/>, or null.</returns>
        RunSettings GetSettingsForTestDll(string testDll);
    }

}
