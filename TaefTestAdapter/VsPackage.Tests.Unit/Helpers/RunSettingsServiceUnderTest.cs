using TaefTestAdapter.TestAdapter.Settings;

namespace TaefTestAdapter.VsPackage.Helpers
{
    public class RunSettingsServiceUnderTest : RunSettingsService
    {
        private readonly string _solutionRunSettingsFile;

        internal RunSettingsServiceUnderTest(IGlobalRunSettings globalRunSettings, string solutionRunSettingsFile) 
            : base(globalRunSettings)
        {
            _solutionRunSettingsFile = solutionRunSettingsFile;
        }

        protected override string GetSolutionSettingsXmlFile()
        {
            return _solutionRunSettingsFile;
        }
    }
}