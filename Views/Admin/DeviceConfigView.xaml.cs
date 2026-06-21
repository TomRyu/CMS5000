using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class DeviceConfigView : UserControl
{
    public DeviceConfigView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private bool   _leftCollapsed;
    private bool   _leftRestored;
    private double _expandWidth = 280;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 마지막으로 조정한 좌측 패널 폭/접힘 상태 복원 (최초 1회)
        if (!_leftRestored)
        {
            _leftRestored = true;
            var saved = LocalSettingsService.Current.DevicePanelWidth;
            if (saved is double w && w >= LeftPanelColumn.MinWidth && w <= LeftPanelColumn.MaxWidth)
            {
                _expandWidth = w;
                LeftPanelColumn.Width = new GridLength(w);
            }
            if (LocalSettingsService.Current.DevicePanelCollapsed)
                ApplyLeftCollapsed(true, save: false);
        }

        if (DataContext is not DeviceConfigViewModel vm) return;
        vm.InsertStationRequested  += OnInsertStation;
        vm.ModifyStationRequested  += OnModifyStation;
        vm.RackCopyRequested       += OnRackCopy;
        vm.CopyRequested           += OnCopy;
        _ = vm.InitAsync();
    }

    private void OnInsertStation(int nextId)
    {
        if (DataContext is not DeviceConfigViewModel vm) return;
        var dlg = new StationEditView(nextId) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Modified)
            _ = vm.RefreshAsyncSelectStation(dlg.SavedId);
    }

    private void OnModifyStation(DeviceStation station)
    {
        if (DataContext is not DeviceConfigViewModel vm) return;
        var dlg = new StationEditView(station) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Modified)
            _ = vm.RefreshAsyncSelectStation(dlg.SavedId);
    }

    private void OnRackCopy(DeviceTreeNode rackNode)
    {
        if (DataContext is not DeviceConfigViewModel vm) return;
        var dlg = new RackCopyView(rackNode) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Copied)
            _ = vm.RefreshAsyncSelectStation(rackNode.StationId);
    }

    private void OnCopy(DeviceTreeNode node)
    {
        if (DataContext is not DeviceConfigViewModel vm) return;
        var dlg = new CopyView(node) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Copied)
            _ = vm.RefreshAsyncSelectStation(node.StationId);
    }

    // ── Rack Modify 툴바 버튼 ────────────────────────────────

    private void RackModify_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceConfigViewModel vm) return;
        if (vm.RackViewNode == null) return;

        var dlg = new RackModifyView(vm.RackViewNode)
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();

        if (dlg.Modified)
            _ = vm.RefreshRackViewAsync();
    }

    // ── Rack 슬롯 더블클릭 ───────────────────────────────────

    private void RackSlot_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DeviceConfigViewModel vm) return;
        if (vm.SelectedRackViewSlot is not { IsOccupied: true } slot) return;
        if (vm.RackViewNode == null || slot.ModuleNode == null) return;

        var dlg = new ModuleModifyView(vm.RackViewNode, slot.ModuleNode)
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();

        if (dlg.Modified)
            _ = vm.RefreshRackViewAsync();
    }

    // ── 좌측 패널 폭 조절 저장 ────────────────────────────────

    private void LeftPanelSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        double w = LeftPanelColumn.ActualWidth;
        if (w <= 0) return;
        _expandWidth = w;
        LocalSettingsService.Current.DevicePanelWidth = w;
        LocalSettingsService.Save();
    }

    // ── 좌측 패널 접기 / 펼치기 ───────────────────────────────

    private void ToggleLeftPanel_Click(object sender, RoutedEventArgs e)
        => ApplyLeftCollapsed(!_leftCollapsed, save: true);

    private void ApplyLeftCollapsed(bool collapsed, bool save)
    {
        if (collapsed)
        {
            // 현재 펼쳐진 폭 기억(접기 직전)
            if (!_leftCollapsed && LeftPanelColumn.ActualWidth > 1)
                _expandWidth = LeftPanelColumn.ActualWidth;
            LeftPanelColumn.MinWidth = 0;
            LeftPanelColumn.Width    = new GridLength(0);
            LeftPanelGrid.Visibility     = Visibility.Collapsed;
            LeftPanelSplitter.Visibility = Visibility.Collapsed;
            LeftToggleIcon.Text = ((char)0xE76C).ToString(); // ChevronRight (expand)
        }
        else
        {
            LeftPanelColumn.MinWidth = 220;
            LeftPanelColumn.Width    = new GridLength(_expandWidth >= 220 ? _expandWidth : 280);
            LeftPanelGrid.Visibility     = Visibility.Visible;
            LeftPanelSplitter.Visibility = Visibility.Visible;
            LeftToggleIcon.Text = ((char)0xE76B).ToString(); // ChevronLeft (collapse)
        }
        _leftCollapsed = collapsed;
        if (save)
        {
            LocalSettingsService.Current.DevicePanelCollapsed = collapsed;
            LocalSettingsService.Save();
        }
    }

    // ── 트리 선택 이벤트 ──────────────────────────────────────

    private void RackTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DeviceConfigViewModel vm && e.NewValue is DeviceTreeNode node)
            vm.SelectRackNode(node);
    }

    private void TrainTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DeviceConfigViewModel vm && e.NewValue is DeviceTreeNode node)
            vm.SelectTrainNode(node);
    }

    // ── 우클릭 시 노드 선택 ────────────────────────────────────

    private void RackNode_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DeviceTreeNode node)
            if (DataContext is DeviceConfigViewModel vm)
                vm.SelectRackNode(node);
    }

    private void TrainNode_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DeviceTreeNode node)
            if (DataContext is DeviceConfigViewModel vm)
                vm.SelectTrainNode(node);
    }

    // ── RACK 확장 / 축소 ──────────────────────────────────────

    private void RackExpand_Click(object sender, RoutedEventArgs e)
    {
        var tvi = GetTreeViewItem(sender);
        if (tvi != null) { tvi.IsExpanded = true; ExpandAll(tvi); }
    }

    private void RackCollapse_Click(object sender, RoutedEventArgs e)
    {
        var tvi = GetTreeViewItem(sender);
        if (tvi != null) CollapseAll(tvi);
    }

    // ── TRAIN 확장 / 축소 ─────────────────────────────────────

    private void TrainExpand_Click(object sender, RoutedEventArgs e)
    {
        var tvi = GetTreeViewItem(sender);
        if (tvi != null) { tvi.IsExpanded = true; ExpandAll(tvi); }
    }

    private void TrainCollapse_Click(object sender, RoutedEventArgs e)
    {
        var tvi = GetTreeViewItem(sender);
        if (tvi != null) CollapseAll(tvi);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────

    private static TreeViewItem? GetTreeViewItem(object sender)
    {
        if (sender is not MenuItem mi) return null;
        if (mi.Parent is not ContextMenu cm) return null;
        DependencyObject? d = cm.PlacementTarget;
        while (d != null)
        {
            if (d is TreeViewItem tvi) return tvi;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private static void ExpandAll(TreeViewItem item)
    {
        item.IsExpanded = true;
        foreach (var child in item.Items)
        {
            if (item.ItemContainerGenerator.ContainerFromItem(child) is TreeViewItem childTvi)
                ExpandAll(childTvi);
        }
    }

    private static void CollapseAll(TreeViewItem item)
    {
        item.IsExpanded = false;
        foreach (var child in item.Items)
        {
            if (item.ItemContainerGenerator.ContainerFromItem(child) is TreeViewItem childTvi)
                CollapseAll(childTvi);
        }
    }
}
