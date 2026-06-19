using System.Collections.ObjectModel;
using System.ComponentModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>릴레이 채널 1개 (체크 + 이름).</summary>
public class RelayChannelRow : INotifyPropertyChanged
{
    private bool _checked;
    public int    No   { get; init; }
    public string Name { get; set; } = "";
    public bool Checked
    {
        get => _checked;
        set { _checked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>RELAY 모듈 삽입(원본 frmRelayModule) VM. 헤더 + 릴레이 16채널.</summary>
public class RelayModuleInsertViewModel : ViewModelBase
{
    private const int MaxChannels = 16;

    public int StationId { get; }
    public int RackId    { get; }
    public string DialogTitle => "RELAY MODULE INSERT.";

    private int    _moduleId;
    private string _name = "";
    private bool   _activity = true;
    private ModuleTypeItem? _selectedType;

    public int    ModuleId { get => _moduleId; set => SetProperty(ref _moduleId, value); }
    public string Name     { get => _name;     set => SetProperty(ref _name, value); }
    public bool   Activity { get => _activity; set => SetProperty(ref _activity, value); }

    public ObservableCollection<ModuleTypeItem> ModuleTypes { get; } = [];
    public ModuleTypeItem? SelectedModuleType { get => _selectedType; set => SetProperty(ref _selectedType, value); }

    public ObservableCollection<RelayChannelRow> Relays { get; } = [];

    public bool Modified { get; private set; }
    public event Action? CloseRequested;
    /// <summary>릴레이 채널 Config 버튼 (channelNo).</summary>
    public event Action<int>? ConfigRelayRequested;

    public RelayCommand                  InsertCommand { get; }
    public RelayCommand                  CancelCommand { get; }
    public RelayCommand<RelayChannelRow> ConfigCommand { get; }

    public RelayModuleInsertViewModel(DeviceTreeNode rack)
    {
        StationId = rack.StationId;
        RackId    = rack.RackId;
        for (int i = 1; i <= MaxChannels; i++)
            Relays.Add(new RelayChannelRow { No = i, Name = $"Relay {i:D2}" });

        InsertCommand = new RelayCommand(_ => _ = InsertAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        ConfigCommand = new RelayCommand<RelayChannelRow>(r => { if (r != null) ConfigRelayRequested?.Invoke(r.No); });
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            foreach (var mt in await DeviceService.GetModuleTypesAsync()) ModuleTypes.Add(mt);
            SelectedModuleType = ModuleTypes.FirstOrDefault(mt =>
                (mt.NicName + mt.Name).ToUpperInvariant().Contains("RELAY")) ?? ModuleTypes.FirstOrDefault();
            ModuleId = await DeviceService.NextModuleIdAsync(StationId, RackId);
        }
        catch (Exception ex) { Err(ex.Message); }
    }

    private async Task InsertAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) { Warn("모듈 이름을 입력하세요."); return; }
        if (ModuleId <= 0) { Warn("유효한 Module ID를 입력하세요."); return; }
        var chosen = Relays.Where(r => r.Checked).ToList();
        if (chosen.Count == 0) { Warn("최소 1개 이상의 릴레이 채널을 선택하세요."); return; }
        try
        {
            await DeviceService.CreateModuleAsync(StationId, RackId, ModuleId, Name.Trim());
            if (SelectedModuleType != null)
                await DeviceService.SetModuleTypeAsync(StationId, RackId, ModuleId, SelectedModuleType.TypeId);
            if (!Activity)
                await DeviceService.SetModuleActivityAsync(StationId, RackId, ModuleId, 0);

            int idx = await DeviceService.NextChannelIndexAsync();
            foreach (var r in chosen)
                await DeviceService.CreateChannelAsync(StationId, RackId, ModuleId, r.No, idx++, r.Name.Trim());

            Modified = true;
            CloseRequested?.Invoke();
        }
        catch (Exception ex) { Err(ex.Message); }
    }

    private static void Warn(string m) => System.Windows.MessageBox.Show(m, "Relay Insert",
        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    private static void Err(string m) => System.Windows.MessageBox.Show($"삽입 실패: {m}", "오류",
        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
}
