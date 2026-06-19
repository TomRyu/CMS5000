using System.Windows;
using System.Windows.Controls;
using CMS5000.Services;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RelayModuleControl : UserControl
{
    private RelayModuleInsertViewModel? _vm;

    public RelayModuleControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.ConfigRelayRequested -= OnConfigRelay;
            _vm = DataContext as RelayModuleInsertViewModel;
            if (_vm != null) _vm.ConfigRelayRequested += OnConfigRelay;
        };
    }

    private async void OnConfigRelay(int channelNo)
    {
        if (_vm == null) return;
        try
        {
            // 원본 frmRelayModule: 채널이 있으면 수정, 없으면 생성 여부를 먼저 확인
            int? idx = await DeviceService.GetChannelIndexAsync(_vm.StationId, _vm.RackId, _vm.ModuleId, channelNo);
            if (idx is null)
            {
                if (MessageBox.Show("존재하지 않은 채널입니다. 새로 만드시겠습니까?.", "채널 생성",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                idx = await DeviceService.EnsureRelayChannelAsync(
                    _vm.StationId, _vm.RackId, _vm.ModuleId, channelNo,
                    $"Relay {channelNo:D2}", _vm.Name, _vm.SelectedModuleType?.TypeId);
            }

            var dlg = new RelayConfigView(_vm.StationId, _vm.RackId, _vm.ModuleId, channelNo,
                                          $"Relay {channelNo:D2}", idx.Value)
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(ex.Message, "Relay Config", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
