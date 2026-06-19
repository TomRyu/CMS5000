using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// REFERENCE/IO 모듈 삽입(원본 frmModule INSERT) VM — 전체 폼(헤더 + 채널 4개).
/// 탭에서 호스팅(ModuleInsertControl).
/// </summary>
public class ModuleInsertViewModel : ViewModelBase
{
    public int StationId { get; }
    public int RackId    { get; }
    public string DialogTitle => "REFERENCE/IO MODULE INSERT.";

    private int    _moduleId;
    private string _name = "";
    private bool   _activity = true;
    private ModuleTypeItem? _selectedType;

    public int    ModuleId { get => _moduleId; set => SetProperty(ref _moduleId, value); }
    public string Name     { get => _name;     set => SetProperty(ref _name, value); }
    public bool   Activity { get => _activity; set => SetProperty(ref _activity, value); }

    public ObservableCollection<ModuleTypeItem>    ModuleTypes       { get; } = [];
    public ObservableCollection<ChannelOption>     RefChannelOptions { get; } = [];
    public ObservableCollection<ChannelModifyItem> Channels          { get; } = [];

    public ModuleTypeItem? SelectedModuleType
    {
        get => _selectedType;
        set { SetProperty(ref _selectedType, value); ApplyReferenceVisibility(); }
    }

    public bool Modified { get; private set; }
    public event Action? CloseRequested;
    /// <summary>채널 Config 버튼 → Reference Config (channelId).</summary>
    public event Action<int>? ConfigChannelRequested;

    public RelayCommand                    InsertCommand        { get; }
    public RelayCommand                    CancelCommand        { get; }
    public RelayCommand<ChannelModifyItem> ConfigChannelCommand { get; }

    public ModuleInsertViewModel(DeviceTreeNode rack)
    {
        StationId = rack.StationId;
        RackId    = rack.RackId;

        for (int i = 1; i <= 4; i++)
            Channels.Add(new ChannelModifyItem { ChannelId = i, SlotLabel = $"CHANNEL {i:D2}", IsActive = false });

        InsertCommand = new RelayCommand(_ => _ = InsertAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        ConfigChannelCommand = new RelayCommand<ChannelModifyItem>(ch =>
        {
            if (ch != null) ConfigChannelRequested?.Invoke(ch.ChannelId);
        });
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            foreach (var mt in await DeviceService.GetModuleTypesAsync()) ModuleTypes.Add(mt);
            SelectedModuleType = ModuleTypes.FirstOrDefault(mt =>
                !(mt.NicName + mt.Name).ToUpperInvariant().Contains("RELAY")) ?? ModuleTypes.FirstOrDefault();

            RefChannelOptions.Add(new ChannelOption { ChannelId = 0, DisplayName = "(없음)" });
            foreach (var ch in await DeviceService.GetReferIdOptionsAsync(StationId, RackId))
                RefChannelOptions.Add(ch);

            ModuleId = await DeviceService.NextModuleIdAsync(StationId, RackId);
            foreach (var c in Channels) c.SelectedRefChannel = RefChannelOptions.FirstOrDefault();
            ApplyReferenceVisibility();
        }
        catch (Exception ex) { Err(ex.Message); }
    }

    private void ApplyReferenceVisibility()
    {
        bool show = !IsCmsNet(SelectedModuleType);
        foreach (var c in Channels) c.ShowReference = show;
    }

    private static bool IsCmsNet(ModuleTypeItem? t)
    {
        if (t == null) return false;
        string s = (t.NicName + " " + t.Name).ToUpperInvariant();
        return s.Contains("CMS") || s.Contains("INTERFACE");
    }

    private async Task InsertAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { Warn("모듈 이름을 입력하세요."); return; }
        if (ModuleId <= 0) { Warn("유효한 Module ID를 입력하세요."); return; }
        try
        {
            await DeviceService.CreateModuleAsync(StationId, RackId, ModuleId, Name.Trim());
            if (SelectedModuleType != null)
                await DeviceService.SetModuleTypeAsync(StationId, RackId, ModuleId, SelectedModuleType.TypeId);
            if (!Activity)
                await DeviceService.SetModuleActivityAsync(StationId, RackId, ModuleId, 0);

            int idx = await DeviceService.NextChannelIndexAsync();
            foreach (var c in Channels.Where(c => c.IsActive))
            {
                await DeviceService.CreateChannelAsync(StationId, RackId, ModuleId, c.ChannelId, idx++, $"CH{c.ChannelId:D2}");
                await DeviceService.UpdateModuleConfigAsync(StationId, RackId, ModuleId, Name.Trim(),
                    SelectedModuleType?.TypeId ?? 0, Activity, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    [ new ModuleChannelRow { ChannelId = c.ChannelId, Activity = c.IsActive,
                        ReferenceOn = c.RefOn, ReferenceId = c.SelectedRefChannel?.ChannelId ?? 0 } ]);
            }

            Modified = true;
            CloseRequested?.Invoke();
        }
        catch (Exception ex) { Err(ex.Message); }
    }

    private static void Warn(string m) => System.Windows.MessageBox.Show(m, "Module Insert",
        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    private static void Err(string m) => System.Windows.MessageBox.Show($"삽입 실패: {m}", "오류",
        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
}
