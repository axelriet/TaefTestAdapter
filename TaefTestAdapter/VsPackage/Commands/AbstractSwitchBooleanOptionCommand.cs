// This file has been modified for TAEF support.

using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;

namespace TaefTestAdapter.VsPackage.Commands
{
    /// <summary>
    /// Base class of the toolbar buttons which switch a boolean option of the adapter.
    /// </summary>
    internal abstract class AbstractSwitchBooleanOptionCommand
    {
        /// <summary>
        /// GUID of the command set; must be identical to the GUID of symbol guidTaefTestAdapterPackageCmdSet in TaefTestAdapterPackage.vsct.
        /// </summary>
        internal static Guid CommandSet { get; } = new Guid("e36b8b2e-66df-4a55-b356-3b08793b9b02");

        protected readonly ITaefTestAdapterPackage Package;

        protected AbstractSwitchBooleanOptionCommand(ITaefTestAdapterPackage package, IMenuCommandService commandService, int commandId)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            if (commandService == null)
                throw new ArgumentNullException(nameof(commandService));

            var finalCommandId = new CommandID(CommandSet, commandId);
            var command = new OleMenuCommand(OnCommandInvoked, finalCommandId);
            command.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(command);
        }

        protected abstract bool Value { get; set; }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sender is OleMenuCommand command)
                command.Checked = Value;
        }

        private void OnCommandInvoked(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Value = !Value;
        }

    }

}
