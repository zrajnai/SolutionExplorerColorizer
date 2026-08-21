using Microsoft.VisualStudio.Shell;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SolutionExplorerProjectStyler {
    public class Helpers {
        public static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject {
            if (depObj == null) yield break;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is T dependencyObject) {
                    yield return dependencyObject;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child)) {
                    yield return childOfChild;
                }
            }
        }
    }


    public class Debouncer {

        private CancellationTokenSource _refreshCts;

        public void Debounce(Action action, int milliseconds = 50) {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;

            _ = Task.Run(async () => {
                try {
                    await Task.Delay(milliseconds, token);

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

                    if (token.IsCancellationRequested) return;

                    action();
                }
                catch (OperationCanceledException) {
                    // Superseded by a newer trigger
                }
                catch (Exception ex) {
                    Logger.Log($"[Debounce] Exception: {ex}");
                }
            }, token);
        }
    }
}