using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace SolutionExplorerProjectStyler {
    public class ColorUpdater {
        private enum ProjectStatus {
            Loaded,
            Unloaded,
            FailedToLoad,
            Missing
        }

        private static readonly System.Drawing.Color DefaultColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
        private static readonly Color DefaultMediaColor = Color.FromArgb(DefaultColor.A, DefaultColor.R, DefaultColor.G, DefaultColor.B);

        private static readonly SolidColorBrush UnloadedBrush = new SolidColorBrush(Colors.Gray);
        private static readonly SolidColorBrush FailedBrush = new SolidColorBrush(Colors.Yellow);
        private static readonly SolidColorBrush MissingBrush = new SolidColorBrush(Colors.Red);
        private static readonly SolidColorBrush LoadedBrush = new SolidColorBrush(DefaultMediaColor);

        private readonly FrameworkElement _root;
        private readonly Debouncer _debouncer = new Debouncer();

        public ColorUpdater() {
            _root = SolutionExplorerFinder.GetSolutionExplorerRoot();
            if (_root == null) return;

            var itemsControl = _root as ItemsControl;
            itemsControl.ItemContainerGenerator.ItemsChanged += OnItemContainerGeneratorItemsChanged;
            Logger.Log("[ColorUpdater] Could not find Solution Explorer root.");
        }

        private void OnItemContainerGeneratorItemsChanged(object sender, ItemsChangedEventArgs e) {
            UpdateProjectColors();
        }

        public void UpdateProjectColors() {
            ThreadHelper.ThrowIfNotOnUIThread();

            _debouncer.Debounce(() => {
                foreach (var tb in Helpers.FindVisualChildren<TextBlock>(_root)) {
                    tb.Unloaded -= OnTextBlockUnloaded;
                    tb.Unloaded += OnTextBlockUnloaded;
                    tb.DataContextChanged -= OnTextBlockDataContextChanged;
                    tb.DataContextChanged += OnTextBlockDataContextChanged;
                    ApplyColor(tb);
                }
            });
        }

        private void OnTextBlockUnloaded(object sender, RoutedEventArgs e) {
            if (!(sender is TextBlock tb)) return;
            tb.Unloaded -= OnTextBlockUnloaded;
            tb.DataContextChanged -= OnTextBlockDataContextChanged;
        }

        private void OnTextBlockDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is TextBlock tb)) return;
            ApplyColor(tb);
        }

        private void ApplyColor(TextBlock tb) {
            var hierarchyItem = GetHVsHierarchyItem(tb);
            var hierarchy = hierarchyItem?.HierarchyIdentity.NestedHierarchy;
            var brush = !string.IsNullOrEmpty(hierarchyItem?.CanonicalName)
                ? GetBrush(GetProjectStatus(hierarchy))
                : LoadedBrush;
            Logger.Log($"[UI] Applying {brush} to '{hierarchyItem?.CanonicalName}'.");
            tb.Foreground = brush;
        }

        private Brush GetBrush(ProjectStatus status) {
            switch (status) {
                default:
                case ProjectStatus.Loaded: return LoadedBrush;
                case ProjectStatus.FailedToLoad: return FailedBrush;
                case ProjectStatus.Missing: return MissingBrush;
                case ProjectStatus.Unloaded: return UnloadedBrush;
            }
        }

        private ProjectStatus GetProjectStatus(IVsHierarchy hierarchy) {
            ThreadHelper.ThrowIfNotOnUIThread();
            object statusObj = null;
            hierarchy?.GetProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID5.VSHPROPID_ProjectUnloadStatus,
                out statusObj
            );
            if (statusObj is uint statusInt) {
                switch ((_VSProjectUnloadStatus)statusInt) {
                    case _VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser: return ProjectStatus.Unloaded;
                    case _VSProjectUnloadStatus.UNLOADSTATUS_StorageNotLoadable: return ProjectStatus.FailedToLoad;
                    case _VSProjectUnloadStatus.UNLOADSTATUS_StorageNotAvailable: return ProjectStatus.Missing;
                    case _VSProjectUnloadStatus.UNLOADSTATUS_UpgradeFailed:
                        break;
                }
            }
            return ProjectStatus.Loaded;
        }

        private IVsHierarchyItem GetHVsHierarchyItem(TextBlock tb) {
            var dataContextType = tb.DataContext.GetType();
            var itemProperty = dataContextType.GetProperty("Item");
            var item = itemProperty?.GetValue(tb.DataContext) as IAttachedCollectionSource;
            return item?.SourceItem as IVsHierarchyItem;
        }
    }
}
