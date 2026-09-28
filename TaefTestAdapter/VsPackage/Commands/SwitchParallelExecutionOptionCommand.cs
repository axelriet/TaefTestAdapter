// This file has been modified for TAEF support.

using System.ComponentModel.Design;

namespace TaefTestAdapter.VsPackage.Commands
{

    /// <summary>
    /// Toolbar button switching option 'Parallel test execution'.
    /// </summary>
    internal sealed class SwitchParallelExecutionOptionCommand : AbstractSwitchBooleanOptionCommand
    {
        /// <summary>
        /// Command id; must be identical to the value of symbol SwitchParallelExecutionOptionCommandId in TaefTestAdapterPackage.vsct.
        /// </summary>
        internal const int CommandId = 0x0102;

        private SwitchParallelExecutionOptionCommand(ITaefTestAdapterPackage package, IMenuCommandService commandService)
            : base(package, commandService, CommandId) {}

        private static SwitchParallelExecutionOptionCommand Instance
        {
            get; set;
        }

        internal static void Initialize(ITaefTestAdapterPackage package, IMenuCommandService commandService)
        {
            Instance = new SwitchParallelExecutionOptionCommand(package, commandService);
        }

        protected override bool Value
        {
            get { return Package.ParallelTestExecution; }
            set { Package.ParallelTestExecution = value; }
        }

    }

}
