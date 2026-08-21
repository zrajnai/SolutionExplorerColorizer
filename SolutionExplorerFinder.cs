using Microsoft.VisualStudio.Shell;
using System.Windows;

namespace SolutionExplorerProjectStyler {
    public static class SolutionExplorerFinder {
        public static FrameworkElement GetSolutionExplorerRoot() {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Look directly at the main Visual Studio WPF window
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null) return null;

            // Search down the main application window's visual tree for the Solution Explorer pane container
            return FindSolutionExplorerPane(mainWindow);
        }

        private static FrameworkElement FindSolutionExplorerPane(DependencyObject parent) {
            if (parent == null) return null;

            var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childrenCount; i++) {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe) {
                    // Visual Studio's Solution Explorer tool window control name or type identifier
                    // We check if the type name or element name matches the solution explorer view
                    if (fe.GetType().Name.Contains("SolutionExplorer") ||
                        (fe.Name != null && fe.Name.Contains("SolutionExplorer"))) {
                        return fe;
                    }

                    var result = FindSolutionExplorerPane(fe);
                    if (result != null) return result;
                }
            }
            return null;
        }
    }
}
