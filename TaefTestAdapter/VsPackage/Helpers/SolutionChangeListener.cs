// This file has been added for TAEF support.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;

namespace TaefTestAdapter.VsPackage.Helpers
{
    /// <summary>
    /// Invokes a callback (on the UI thread) whenever a solution or folder has been opened or closed, the loading of
    /// a solution's projects has completed, or the active configuration has changed, i.e. whenever the values of the
    /// placeholders $(SolutionDir), $(PlatformName) and $(ConfigurationName) might have changed.
    /// </summary>
    internal sealed class SolutionChangeListener : IVsUpdateSolutionEvents, IDisposable
    {
        private readonly Action _onChange;

        private IVsSolutionBuildManager _solutionBuildManager;
        private uint _updateSolutionEventsCookie;

        /// <param name="solutionBuildManager">Used to listen to changes of the active configuration; may be null</param>
        /// <param name="onChange">Callback invoked on changes (on the UI thread)</param>
        internal SolutionChangeListener(IVsSolutionBuildManager solutionBuildManager, Action onChange)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _onChange = onChange ?? throw new ArgumentNullException(nameof(onChange));

            SolutionEvents.OnAfterOpenSolution += OnSolutionChanged;
            SolutionEvents.OnAfterBackgroundSolutionLoadComplete += OnSolutionChanged;
            SolutionEvents.OnAfterCloseSolution += OnSolutionChanged;
            SolutionEvents.OnAfterOpenFolder += OnSolutionChanged;
            SolutionEvents.OnAfterCloseFolder += OnSolutionChanged;

            if (solutionBuildManager != null
                && ErrorHandler.Succeeded(solutionBuildManager.AdviseUpdateSolutionEvents(this, out _updateSolutionEventsCookie)))
            {
                _solutionBuildManager = solutionBuildManager;
            }
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SolutionEvents.OnAfterOpenSolution -= OnSolutionChanged;
            SolutionEvents.OnAfterBackgroundSolutionLoadComplete -= OnSolutionChanged;
            SolutionEvents.OnAfterCloseSolution -= OnSolutionChanged;
            SolutionEvents.OnAfterOpenFolder -= OnSolutionChanged;
            SolutionEvents.OnAfterCloseFolder -= OnSolutionChanged;

            if (_solutionBuildManager != null)
            {
                _solutionBuildManager.UnadviseUpdateSolutionEvents(_updateSolutionEventsCookie);
                _solutionBuildManager = null;
            }
        }

        private void OnSolutionChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            NotifyChange();
        }

        private void NotifyChange()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _onChange();
        }

        #region IVsUpdateSolutionEvents

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            NotifyChange();
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Begin(ref int pfCancelUpdate) => VSConstants.S_OK;

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand) => VSConstants.S_OK;

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate) => VSConstants.S_OK;

        public int UpdateSolution_Cancel() => VSConstants.S_OK;

        #endregion
    }
}
