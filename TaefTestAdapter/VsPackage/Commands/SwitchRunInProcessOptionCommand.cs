// This file has been modified for TAEF support.

using System.ComponentModel.Design;

namespace TaefTestAdapter.VsPackage.Commands
{

    /// <summary>
    /// Toolbar button switching option 'Run tests in process'.
    /// </summary>
    internal sealed class SwitchRunInProcessOptionCommand : AbstractSwitchBooleanOptionCommand
    {
        /// <summary>
        /// Command id; must be identical to the value of symbol SwitchRunInProcessOptionCommandId in TaefTestAdapterPackage.vsct.
        /// </summary>
        internal const int CommandId = 0x0100;

        private SwitchRunInProcessOptionCommand(ITaefTestAdapterPackage package, IMenuCommandService commandService)
            : base(package, commandService, CommandId) {}

        private static SwitchRunInProcessOptionCommand Instance
        {
            get; set;
        }

        internal static void Initialize(ITaefTestAdapterPackage package, IMenuCommandService commandService)
        {
            Instance = new SwitchRunInProcessOptionCommand(package, commandService);
        }

        protected override bool Value
        {
            get { return Package.RunInProcess; }
            set { Package.RunInProcess = value; }
        }

    }

}
