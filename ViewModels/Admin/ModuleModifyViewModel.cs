using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// Module Config(원본 frmModule) VM. 헤더(STATION/RACK/ID/NAME/TYPE/Activity/ConfigDate)와
/// 고정 4채널(Activity / Reference ON·OFF·ID / Config 버튼)을 갖춘다.
/// </summary>
public class ModuleModifyViewModel : ViewModelBase
{
    private string          _moduleName = "";
    private bool            _isActive   = true;
    private string          _configDate = "";
    private ModuleTypeItem? _selectedModuleType;

    public DeviceTreeNode RackNode   { get; }
    public DeviceTreeNode ModuleNode { get; }

    public int    StationId   => RackNode.StationId;
    public int    RackId      => RackNode.RackId;
    public int    ModuleId    => ModuleNode.ModuleId;
    public string DialogTitle => $"Module [{ModuleNode.ModuleId:D2}] Modify";

    public string ModuleName { get => _moduleName; set => SetProperty(ref _moduleName, value); }
    public bool   IsActive   { get => _isActive;   set => SetProperty(ref _isActive, value); }
    public string ConfigDate { get => _configDate; set => SetProperty(ref _configDate, value); }

    public ModuleTypeItem? SelectedModuleType
    {
        get => _selectedModuleType;
        set { SetProperty(ref _selectedModuleType, value); ApplyReferenceVisibility(); }
    }

    public ObservableCollection<ModuleTypeItem>    ModuleTypes       { get; } = [];
    public ObservableCollection<ChannelOption>     RefChannelOptions { get; } = [];
    public ObservableCollection<ChannelModifyItem> Channels          { get; } = [];

    public bool Modified { get; private set; }

    public RelayCommand                       ModifyCommand        { get; }
    public RelayCommand                       CancelCommand        { get; }
    public RelayCommand<ChannelModifyItem>    ConfigChannelCommand { get; }

    public event Action? CloseRequested;
    /// <summary>채널 Config 버튼 → Reference Config 창 열기 요청 (channelId).</summary>
    public event Action<int>? ConfigChannelRequested;

    public ModuleModifyViewModel(DeviceTreeNode rackNode, DeviceTreeNode moduleNode)
    {
        RackNode   = rackNode;
        ModuleNode = moduleNode;
        ModuleName = moduleNode.Name;
        IsActive   = moduleNode.IsActive;

        for (int i = 1; i <= 4; i++)
            Channels.Add(new ChannelModifyItem
            {
                ChannelId = i,
                SlotLabel = $"CHANNEL {i:D2}",
            });

        ModifyCommand        = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand        = new RelayCommand(_ => CloseRequested?.Invoke());
        ConfigChannelCommand = new RelayCommand<ChannelModifyItem>(ch =>
        {
            if (ch != null) ConfigChannelRequested?.Invoke(ch.ChannelId);
        });
    }

    public async Task LoadAsync()
    {
        try
        {
            var types = await DeviceService.GetModuleTypesAsync();
            foreach (var mt in types) ModuleTypes.Add(mt);
            SelectedModuleType = ModuleTypes.FirstOrDefault(mt =>
                mt.NicName == ModuleNode.ModuleType || mt.Name == ModuleNode.ModuleType
                || mt.TypeId.ToString() == ModuleNode.ModuleType)
                ?? ModuleTypes.FirstOrDefault();

            // Reference 대상 콤보 (Interface 모듈의 채널)
            RefChannelOptions.Add(new ChannelOption { ChannelId = 0, DisplayName = "(없음)" });
            foreach (var ch in await DeviceService.GetReferIdOptionsAsync(RackNode.StationId, RackNode.RackId))
                RefChannelOptions.Add(ch);

            // 채널 값 로드
            var info = await DeviceService.GetModuleConfigAsync(RackNode.StationId, RackNode.RackId, ModuleNode.ModuleId);
            ConfigDate = info.ConfigDate;
            foreach (var row in info.Channels)
            {
                var item = Channels.FirstOrDefault(c => c.ChannelId == row.ChannelId);
                if (item == null) continue;
                item.IsActive = row.Activity;
                item.RefOn    = row.ReferenceOn;
                item.SelectedRefChannel = RefChannelOptions.FirstOrDefault(o => o.ChannelId == row.ReferenceId)
                                          ?? RefChannelOptions.FirstOrDefault();
            }
            ApplyReferenceVisibility();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"모듈 정보 로드 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>원본과 동일: CMSNet(인터페이스) 타입이면 채널 Reference 그룹 숨김.</summary>
    private void ApplyReferenceVisibility()
    {
        bool show = !IsCmsNet(SelectedModuleType);
        foreach (var ch in Channels) ch.ShowReference = show;
    }

    private static bool IsCmsNet(ModuleTypeItem? t)
    {
        if (t == null) return false;
        string s = (t.NicName + " " + t.Name).ToUpperInvariant();
        return s.Contains("CMS") || s.Contains("INTERFACE");
    }

    private async Task SaveAsync()
    {
        try
        {
            var rows = Channels.Select(c => new ModuleChannelRow
            {
                ChannelId   = c.ChannelId,
                Activity    = c.IsActive,
                ReferenceOn = c.RefOn,
                ReferenceId = c.SelectedRefChannel?.ChannelId ?? 0,
            }).ToList();

            await DeviceService.UpdateModuleConfigAsync(
                RackNode.StationId, RackNode.RackId, ModuleNode.ModuleId,
                ModuleName.Trim(),
                SelectedModuleType?.TypeId ?? 0,
                IsActive,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                rows);

            Modified = true;
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"저장 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
