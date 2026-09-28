// This file has been modified for TAEF support.

using System.ComponentModel.Design;

namespace TaefTestAdapter.VsPackage.Commands
{

    /// <summary>
    /// Toolbar button switching option 'Print test output'.
    /// </summary>
    internal sealed class SwitchPrintTestOutputOptionCommand : AbstractSwitchBooleanOptionCommand
    {
        /// <summary>
        /// Command id; must be identical to the value of symbol SwitchPrintTestOutputOptionCommandId in TaefTestAdapterPackage.vsct.
        /// </summary>
        internal const int CommandId = 0x0103;

        private SwitchPrintTestOutputOptionCommand(ITaefTestAdapterPackage package, IMenuCommandService commandService)
            : base(package, commandService, CommandId) {}

        private static SwitchPrintTestOutputOptionCommand Instance
        {
            get; set;
        }

        internal static void Initialize(ITaefTestAdapterPackage package, IMenuCommandService commandService)
        {
            Instance = new SwitchPrintTestOutputOptionCommand(package, commandService);
        }

        protected override bool Value
        {
            get { return Package.PrintTestOutput; }
            set { Package.PrintTestOutput = value; }
        }

    }

}
