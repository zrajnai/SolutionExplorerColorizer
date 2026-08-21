using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SolutionExplorerProjectStyler
{
    public static class Logger
    {
        private static IVsOutputWindowPane _pane;

        public static void Initialize(AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!(package.GetServiceAsync(typeof(SVsOutputWindow)).Result is IVsOutputWindow outputWindow)) return;
            var paneGuid = new Guid("1a9b495d-6b1a-4076-b244-98d0a5663053");
            outputWindow.CreatePane(ref paneGuid, "Project Styler Logs", 1, 1);
            outputWindow.GetPane(ref paneGuid, out _pane);
        }

        public static void Log(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_pane != null)
            {
                var formatted = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
                _pane.OutputStringThreadSafe(formatted);
            }
        }
    }
}
