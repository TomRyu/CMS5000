using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// 릴레이 채널 로직 설정(원본 frmRelay) VM.
/// 상단 정보(읽기전용) + Mode / And-Voting + RELAY LOGIC(Add/Remove 그리드).
/// </summary>
public class RelayConfigViewModel : ViewModelBase
{
    private const int MaxRelayLogs = 20;   // 원본 cConst.MAX_RELAY_LOGS

    private readonly int _channelIndex;

    public int    StationId   { get; }
    public int    RackId      { get; }
    public int    ModuleId    { get; }
    public int    ChannelId   { get; }
    public string ChannelTypeText => "Relay";
    public string DialogTitle  => $"RELAY[{ChannelId:D2}] CONFIG";

    private string _name;
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private int _activity = 1;   // 0=Inactivity, 1=Activity
    public int Activity { get => _activity; set => SetProperty(ref _activity, value); }

    // Mode: 0=Latching, 1=Non Latching
    private bool _modeLatch = true;
    private bool _modeNonLatch;
    public bool ModeLatch    { get => _modeLatch;    set => SetProperty(ref _modeLatch, value); }
    public bool ModeNonLatch { get => _modeNonLatch; set => SetProperty(ref _modeNonLatch, value); }

    // And-Voting: 0=Normal, 1=True
    private bool _avNormal = true;
    private bool _avTrue;
    public bool AvNormal { get => _avNormal; set => SetProperty(ref _avNormal, value); }
    public bool AvTrue   { get => _avTrue;   set => SetProperty(ref _avTrue, value); }

    // ── RELAY LOGIC 그리드 ──────────────────────────────────
    public ObservableCollection<RelayLogicRow> Logic { get; } = [];

    // ── Adding.... ──────────────────────────────────────────
    public ObservableCollection<int>    LogicModules  { get; } = [];
    public ObservableCollection<int>    LogicChannels { get; } = [];
    public ObservableCollection<string> AlertItems    { get; } = ["Alert", "Danger"];
    public ObservableCollection<string> AoeItems      { get; } = ["And", "Or", "End"];

    private int _seq = 1;
    public int Seq { get => _seq; set => SetProperty(ref _seq, value); }

    private int? _addModuleId;
    public int? AddModuleId
    {
        get => _addModuleId;
        set { SetProperty(ref _addModuleId, value); _ = LoadLogicChannelsAsync(); }
    }

    private int?    _addChannelId;
    private string? _addAlert;
    private string? _addAoe;
    public int?    AddChannelId { get => _addChannelId; set => SetProperty(ref _addChannelId, value); }
    public string? AddAlert     { get => _addAlert;     set => SetProperty(ref _addAlert, value); }
    public string? AddAoe       { get => _addAoe;       set => SetProperty(ref _addAoe, value); }

    private RelayLogicRow? _selectedLogic;
    public RelayLogicRow? SelectedLogic { get => _selectedLogic; set => SetProperty(ref _selectedLogic, value); }

    public bool Modified { get; private set; }
    public event Action? CloseRequested;

    public RelayCommand AddLogicCommand    { get; }
    public RelayCommand RemoveLogicCommand { get; }
    public RelayCommand OkCommand          { get; }
    public RelayCommand CancelCommand      { get; }

    public RelayConfigViewModel(int stationId, int rackId, int moduleId, int channelId, string name, int channelIndex)
    {
        StationId     = stationId;
        RackId        = rackId;
        ModuleId      = moduleId;
        ChannelId     = channelId;
        _name         = name;
        _channelIndex = channelIndex;

        AddLogicCommand    = new RelayCommand(_ => AddLogic());
        RemoveLogicCommand = new RelayCommand(_ => RemoveLogic(), _ => SelectedLogic != null);
        OkCommand          = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand      = new RelayCommand(_ => CloseRequested?.Invoke());

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            foreach (var m in await DeviceService.GetDistinctChannelModuleIdsAsync(StationId, RackId))
                LogicModules.Add(m);

            var cfg = await DeviceService.GetRelayConfigAsync(_channelIndex);
            ModeLatch    = cfg.Mode == 0;
            ModeNonLatch = cfg.Mode != 0;
            AvNormal     = cfg.AndVoting == 0;
            AvTrue       = cfg.AndVoting != 0;
            foreach (var row in cfg.Logic) Logic.Add(row);
            Seq = Logic.Count + 1;
        }
        catch (Exception ex) { Err(ex.Message); }
    }

    private async Task LoadLogicChannelsAsync()
    {
        LogicChannels.Clear();
        if (AddModuleId is not int mid || mid <= 0) return;
        try { foreach (var c in await DeviceService.GetChannelIdsAsync(StationId, RackId, mid)) LogicChannels.Add(c); }
        catch { }
    }

    private void AddLogic()
    {
        if (Seq >= MaxRelayLogs) { Warn("Logic can not register anymore."); return; }
        if (AddModuleId is not int mid || AddChannelId is not int cid ||
            string.IsNullOrEmpty(AddAlert) || string.IsNullOrEmpty(AddAoe))
        {
            Warn("Please check the items to be registered."); return;
        }

        Logic.Add(new RelayLogicRow
        {
            Sequence    = Seq,
            ModuleId    = mid,
            ChannelId   = cid,
            AlertDanger = AddAlert!,
            AndOrEnd    = AddAoe!,
        });

        // NextLogic (원본): 시퀀스 증가 + 입력값 초기화
        Seq          = Logic.Count + 1;
        AddModuleId  = null;
        AddChannelId = null;
        AddAlert     = null;
        AddAoe       = null;
    }

    private void RemoveLogic()
    {
        if (SelectedLogic == null) return;
        // 원본: 순차적으로(마지막부터) 삭제해야 함
        var last = Logic[^1];
        if (!ReferenceEquals(SelectedLogic, last))
        {
            Warn("순차적으로 삭제 해야 합니다."); return;
        }
        Logic.Remove(last);
        Seq = Logic.Count + 1;
    }

    private async Task SaveAsync()
    {
        if (ChannelId <= 0)         { Warn("오류: 채널 ID 가 0 입니다.");   return; }
        if (string.IsNullOrWhiteSpace(Name)) { Warn("오류: 채널 이름이 없습니다."); return; }
        try
        {
            int mode      = ModeLatch ? 0 : 1;
            int andVoting = AvNormal  ? 0 : 1;

            await DeviceService.UpdateChannelAsync(StationId, RackId, ModuleId, ChannelId, Name.Trim());
            await DeviceService.SetChannelActivityAsync(StationId, RackId, ModuleId, ChannelId, (byte)Activity);
            await DeviceService.SaveRelayConfigAsync(_channelIndex, mode, andVoting, Logic);

            Modified = true;
            System.Windows.MessageBox.Show("RELAY 설정을 저장했습니다.", "RELAY CONFIG",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            CloseRequested?.Invoke();
        }
        catch (Exception ex) { Err(ex.Message); }
    }

    private static void Warn(string m) => System.Windows.MessageBox.Show(m, "RELAY CONFIG",
        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    private static void Err(string m) => System.Windows.MessageBox.Show($"오류: {m}", "RELAY CONFIG",
        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
}
