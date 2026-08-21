using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Constants = Microsoft.VisualStudio.OLE.Interop.Constants;
using Task = System.Threading.Tasks.Task;

namespace SolutionExplorerProjectStyler {
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")] // Match your project GUID / package guid
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class SolutionExplorerColorExtensionPackage : AsyncPackage {
        private SolutionEventsListener _solutionListener;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
            // Switch to main thread early to access services safely
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            Logger.Initialize(this);
            Logger.Log("==========================================");
            Logger.Log("SolutionExplorerColorExtensionPackage: InitializeAsync started.");
            Logger.Log("==========================================");

            try {
                if (await GetServiceAsync(typeof(SVsSolution)) is IVsSolution solutionService) {
                    Logger.Log("IVsSolution service successfully acquired. Creating SolutionEventsListener...");
                    _solutionListener = new SolutionEventsListener(solutionService);

                    _solutionListener.RefreshAllProjects();
                }
                else {
                    Logger.Log("ERROR: IVsSolution service could not be acquired!");
                }

                if (await GetServiceAsync(typeof(SVsRegisterPriorityCommandTarget)) is IVsRegisterPriorityCommandTarget priorityCmdTarget) {
                    var hr = priorityCmdTarget.RegisterPriorityCommandTarget(0, _solutionListener, out _);

                    if (hr == VSConstants.S_OK) {
                        Logger.Log("Successfully registered global IOleCommandTarget.");
                    }
                }
            }
            catch (Exception ex) {
                Logger.Log($"FATAL EXCEPTION in InitializeAsync: {ex}");
            }

            Logger.Log("SolutionExplorerColorExtensionPackage: InitializeAsync completed.");
        }
    }

    public class SolutionEventsListener : IVsSolutionEvents, IVsHierarchyEvents, IOleCommandTarget {
        private readonly IVsSolution _solutionService;
        private readonly Dictionary<IVsHierarchy, uint> _advisedHierarchies = new Dictionary<IVsHierarchy, uint>();
        private readonly Debouncer _debouncer = new Debouncer();
        private readonly ColorUpdater _colorUpdater = new ColorUpdater();

        public SolutionEventsListener(IVsSolution solutionService) {
            ThreadHelper.ThrowIfNotOnUIThread();
            _solutionService = solutionService;

            var hr = _solutionService.AdviseSolutionEvents(this, out _);
            if (hr == VSConstants.S_OK) {
                Logger.Log("SolutionEventsListener successfully advised to solution events.");
            }
            else {
                Logger.Log($"ERROR: Failed to advise solution events. HR: {hr:X}");
            }
        }

        private void TrackProjectChanges(IVsHierarchy hierarchy) {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (hierarchy == null) return;

            try {
                if (!_advisedHierarchies.ContainsKey(hierarchy)) {
                    if (hierarchy.AdviseHierarchyEvents(this, out var cookie) == VSConstants.S_OK) {
                        _advisedHierarchies[hierarchy] = cookie;
                    }
                }
            }
            catch (Exception ex) {
                Logger.Log($"[Evaluate] FATAL EXCEPTION: {ex}");
            }
        }

        public void RefreshAllProjects() {
            //ThreadHelper.ThrowIfNotOnUIThread();
            if (_solutionService == null) return;

            _debouncer.Debounce(() => {
                ThreadHelper.ThrowIfNotOnUIThread();
                var guidAllProjects = Guid.Empty;
                var hr = _solutionService.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_ALLPROJECTS, ref guidAllProjects, out var enumHierarchies);

                if (hr != VSConstants.S_OK || enumHierarchies == null) return;

                var hierarchies = new IVsHierarchy[1];
                while (enumHierarchies.Next(1, hierarchies, out var fetched) == VSConstants.S_OK && fetched == 1) {
                    TrackProjectChanges(hierarchies[0]);
                }
                _colorUpdater.UpdateProjectColors();
            });
        }


        #region IVsSolutionEvents

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) {
            //ThreadHelper.ThrowIfNotOnUIThread();
            Logger.Log($"[Event] OnAfterOpenProject triggered (fAdded: {fAdded}).");
            RefreshAllProjects();
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

        public int OnAfterLoadProject(IVsHierarchy pHierarchy, IVsHierarchy pStubHierarchy) {
            //ThreadHelper.ThrowIfNotOnUIThread();
            Logger.Log("[Event] OnAfterLoadProject triggered.");
            //RefreshAllProjects();
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

        public int OnAfterOpenSolution(object pUnkStub, int fNewSolution) {
            //ThreadHelper.ThrowIfNotOnUIThread();
            Logger.Log($"[Event] OnAfterOpenSolution triggered (fNewSolution: {fNewSolution}).");
            RefreshAllProjects();
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkStub) => VSConstants.S_OK;

        public int OnAfterCloseSolution(object pUnkStub) {
            ThreadHelper.ThrowIfNotOnUIThread();
            //ThreadHelper.ThrowIfNotOnUIThread();
            Logger.Log("[Event] OnAfterCloseSolution triggered.");
            foreach (var kvp in _advisedHierarchies) {
                kvp.Key.UnadviseHierarchyEvents(kvp.Value);
            }
            _advisedHierarchies.Clear();
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;
        public int OnQueryCloseSolution(object pUnkStub, ref int pfCancel) => VSConstants.S_OK;
        public int OnQueryUnloadProject(IVsHierarchy pHierarchy, ref int pfCancel) => VSConstants.S_OK;

        #endregion

        #region IVsHierarchyEvents

        public int OnPropertyChanged(uint itemid, int propid, uint flags) {
            Logger.Log($"[Event] Hierarchy OnPropertyChanged triggered for node (itemid: {itemid}, propid: {propid}).");

            if (itemid == (uint)VSConstants.VSITEMID.Root &&
                (propid == (int)__VSHPROPID5.VSHPROPID_ProjectUnloadStatus
                 //|| propid == (int)__VSHPROPID.VSHPROPID_Caption
                 //|| propid == (int)__VSHPROPID.VSHPROPID_StateIconIndex
                 )) {
                RefreshAllProjects();
            }
            return VSConstants.S_OK;
        }

        public int OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded) {
            Logger.Log($"[Event] OnItemAdded triggered. (itemidParent: {itemidParent}, itemidSiblingPrev: {itemidSiblingPrev}, itemidAdded: {itemidAdded})");
            return VSConstants.S_OK;
        }

        public int OnInvalidateIcon(IntPtr hicon) {
            Logger.Log("[Event] OnInvalidateIcon triggered.");
            return VSConstants.S_OK;
        }

        public int OnItemsAppended(uint itemidParent) {
            Logger.Log("[Event] OnItemsAppended triggered.");
            return VSConstants.S_OK;
        }

        public int OnItemDeleted(uint itemid) {
            Logger.Log($"[Event] OnItemDeleted triggered. (itemid: {itemid})");
            return VSConstants.S_OK;
        }

        public int OnInvalidateItems(uint itemidParent) {
            Logger.Log("[Event] OnInvalidateItems triggered.");
            return VSConstants.S_OK;
        }

        #endregion

        #region IOleCommandTarget

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) => (int)Constants.OLECMDERR_E_NOTSUPPORTED;

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdExecOpt, IntPtr pvaIn, IntPtr pvaOut) {
            //ThreadHelper.ThrowIfNotOnUIThread();
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 &&
                nCmdID == (uint)VSConstants.VSStd97CmdID.ReloadProject) {
                Logger.Log("[Command] ReloadProject command intercepted.");

                // Trigger refresh after Visual Studio finishes attempting the reload
                Task.Delay(500).ContinueWith(_ => {
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        RefreshAllProjects();
                    });
                });
            }
            // Let VS execute the command first via the chain
            return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        #endregion
    }
}