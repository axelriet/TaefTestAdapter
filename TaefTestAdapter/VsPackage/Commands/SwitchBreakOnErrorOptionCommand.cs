// This file has been modified for TAEF support.

using System.ComponentModel.Design;

namespace TaefTestAdapter.VsPackage.Commands
{

    /// <summary>
    /// Toolbar button switching option 'Break on error'.
    /// </summary>
    internal sealed class SwitchBreakOnErrorOptionCommand : AbstractSwitchBooleanOptionCommand
    {
        /// <summary>
        /// Command id; must be identical to the value of symbol SwitchBreakOnErrorOptionCommandId in TaefTestAdapterPackage.vsct.
        /// </summary>
        internal const int CommandId = 0x0101;

        private SwitchBreakOnErrorOptionCommand(ITaefTestAdapterPackage package, IMenuCommandService commandService)
            : base(package, commandService, CommandId) {}

        private static SwitchBreakOnErrorOptionCommand Instance
        {
            get; set;
        }

        internal static void Initialize(ITaefTestAdapterPackage package, IMenuCommandService commandService)
        {
            Instance = new SwitchBreakOnErrorOptionCommand(package, commandService);
        }

        protected override bool Value
        {
            get { return Package.BreakOnError; }
            set { Package.BreakOnError = value; }
        }

    }

}
