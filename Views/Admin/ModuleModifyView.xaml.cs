using System.Windows;
using System.Windows.Controls;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class ModuleModifyView : Window
{
    private readonly DeviceTreeNode _rackNode;
    private readonly DeviceTreeNode _moduleNode;

    public bool Modified => (DataContext as ModuleModifyViewModel)?.Modified ?? false;

    public ModuleModifyView(DeviceTreeNode rackNode, DeviceTreeNode moduleNode)
    {
        InitializeComponent();
        _rackNode = rackNode;
        _moduleNode = moduleNode;

        var vm = new ModuleModifyViewModel(rackNode, moduleNode);
        vm.CloseRequested += Close;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }

    // 채널 Config 버튼 → Reference Config 열기 (코드비하인드 직접 처리)
    private void ConfigChannel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not ChannelModifyItem ch)
            return;
        try
        {
            var dlg = new ReferenceConfigView(_rackNode.StationId, _rackNode.RackId, _moduleNode.ModuleId, ch.ChannelId)
            {
                Owner = this
            };
            dlg.ShowDialog();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Reference Config 열기 실패:\n\n{ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
