using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// MODULE / CHANNEL COPY 다이얼로그(원본 frmCopy) VM.
/// 원본 소스(읽기전용) + 대상 소스(캐스케이드 콤보) → 복사.
/// 모듈 레벨: Station/Rack/Module 콤보 활성, Channel 비활성.
/// 채널 레벨: Station/Rack/Module 잠금(원본 고정), Channel 콤보만 활성.
/// </summary>
public class CopyViewModel : ViewModelBase
{
    private readonly DeviceTreeNode _src;

    /// <summary>true = MODULE COPY...., false = CHANNEL COPY....</summary>
    public bool IsModuleLevel { get; }

    public string DialogTitle => IsModuleLevel ? "MODULE COPY...." : "CHANNEL COPY....";

    // ── 원본(읽기전용) ──────────────────────────────────────
    public int SrcStationId { get; }
    public int SrcRackId    { get; }
    public int SrcModuleId  { get; }
    public int SrcChannelId { get; }

    // ── 대상 콤보 데이터 ────────────────────────────────────
    public ObservableCollection<DeviceStation> Stations     { get; } = [];
    public ObservableCollection<int>           DestRacks    { get; } = [];
    public ObservableCollection<int>           DestModules  { get; } = [];
    public ObservableCollection<int>           DestChannels { get; } = [];

    // ── 레벨별 활성화(원본 frmCopy) ─────────────────────────
    public bool StationEnabled => IsModuleLevel;
    public bool RackEnabled    => IsModuleLevel;
    public bool ModuleEnabled  => IsModuleLevel;
    public bool ChannelEnabled => !IsModuleLevel;

    private DeviceStation? _destStation;
    public DeviceStation? DestStation
    {
        get => _destStation;
        set { SetProperty(ref _destStation, value); _ = LoadDestRacksAsync(); }
    }

    private string _destRackText = "";
    public string DestRackText
    {
        get => _destRackText;
        set { SetProperty(ref _destRackText, value); _ = LoadDestModulesAsync(); }
    }

    private string _destModuleText = "";
    public string DestModuleText
    {
        get => _destModuleText;
        set { SetProperty(ref _destModuleText, value); if (IsModuleLevel) _ = LoadDestChannelsAsync(); }
    }

    private string _destChannelText = "";
    public string DestChannelText { get => _destChannelText; set => SetProperty(ref _destChannelText, value); }

    public bool Modified { get; private set; }
    public event Action? CloseRequested;

    public RelayCommand CopyCommand   { get; }
    public RelayCommand CancelCommand { get; }

    public CopyViewModel(DeviceTreeNode node)
    {
        _src          = node;
        IsModuleLevel = node.Kind == NodeKind.Module;

        SrcStationId  = node.StationId;
        SrcRackId     = node.RackId;
        SrcModuleId   = node.ModuleId;
        SrcChannelId  = IsModuleLevel ? 0 : node.ChannelId;

        // 채널 레벨은 대상 Station/Rack/Module 이 원본으로 고정
        _destRackText   = SrcRackId.ToString();
        _destModuleText = SrcModuleId.ToString();

        CopyCommand   = new RelayCommand(_ => _ = CopyAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            foreach (var s in await DeviceService.GetStationsAsync()) Stations.Add(s);
            DestStation = Stations.FirstOrDefault(s => s.StationId == SrcStationId) ?? Stations.FirstOrDefault();

            if (!IsModuleLevel)
            {
                // 채널 레벨: 같은 모듈 내 다른 채널 목록(원본 ChangedModuleID)
                await LoadDestChannelsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"로드 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task LoadDestRacksAsync()
    {
        DestRacks.Clear();
        if (DestStation == null) return;
        try { foreach (var r in await DeviceService.GetRackIdsAsync(DestStation.StationId)) DestRacks.Add(r); }
        catch { }
    }

    private async Task LoadDestModulesAsync()
    {
        if (!IsModuleLevel) return;
        DestModules.Clear();
        if (DestStation == null) return;
        if (!int.TryParse(_destRackText?.Trim(), out int rack) || rack <= 0) return;
        try
        {
            // 원본 cbRackID_SelectedIndexChanged: 같은 랙이면 원본 모듈 제외
            int exclude = (DestStation.StationId == SrcStationId && rack == SrcRackId) ? SrcModuleId : -1;
            foreach (var m in await DeviceService.GetModuleIdsAsync(DestStation.StationId, rack, exclude))
                DestModules.Add(m);
        }
        catch { }
    }

    private async Task LoadDestChannelsAsync()
    {
        DestChannels.Clear();
        int sid, rid, mid, exclude;
        if (IsModuleLevel)
        {
            if (DestStation == null) return;
            if (!int.TryParse(_destRackText?.Trim(),   out rid) || rid <= 0) return;
            if (!int.TryParse(_destModuleText?.Trim(), out mid) || mid <= 0) return;
            sid     = DestStation.StationId;
            exclude = -1;
        }
        else
        {
            // 채널 레벨: 원본 모듈 고정, 원본 채널 제외
            sid = SrcStationId; rid = SrcRackId; mid = SrcModuleId; exclude = SrcChannelId;
        }
        try { foreach (var c in await DeviceService.GetChannelIdsAsync(sid, rid, mid, exclude)) DestChannels.Add(c); }
        catch { }
    }

    private async Task CopyAsync()
    {
        if (IsModuleLevel)
        {
            if (DestStation == null) { Warn("대상 스테이션을 선택하세요."); return; }
            if (!int.TryParse(DestRackText?.Trim(),   out int destRack)   || destRack   <= 0) { Warn("대상 RACK ID를 선택하세요."); return; }
            if (!int.TryParse(DestModuleText?.Trim(), out int destModule) || destModule <= 0) { Warn("대상 MODULE ID를 선택하세요."); return; }

            if (DestStation.StationId == SrcStationId && destRack == SrcRackId && destModule == SrcModuleId)
            { Warn("원본과 동일한 위치입니다."); return; }

            try
            {
                await DeviceService.CopyModuleAsync(SrcStationId, SrcRackId, SrcModuleId,
                                                    DestStation.StationId, destRack, destModule);
                Modified = true;
                System.Windows.MessageBox.Show(
                    $"MODULE {SrcModuleId:D2} → [{DestStation.StationId}] RACK {destRack:D2} MODULE {destModule:D2} 복사 완료.",
                    "MODULE COPY", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                CloseRequested?.Invoke();
            }
            catch (Exception ex) { Err(ex); }
        }
        else
        {
            if (!int.TryParse(DestChannelText?.Trim(), out int destChannel) || destChannel <= 0)
            { Warn("대상 CHANNEL ID를 선택하세요."); return; }
            if (destChannel == SrcChannelId) { Warn("원본과 동일한 CHANNEL ID입니다."); return; }

            try
            {
                // 채널 레벨: 같은 모듈 내 다른 채널로 복사
                await DeviceService.CopyChannelAsync(SrcStationId, SrcRackId, SrcModuleId, SrcChannelId,
                                                     SrcStationId, SrcRackId, SrcModuleId, destChannel);
                Modified = true;
                System.Windows.MessageBox.Show(
                    $"CHANNEL {SrcChannelId:D2} → CHANNEL {destChannel:D2} 복사 완료.",
                    "CHANNEL COPY", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                CloseRequested?.Invoke();
            }
            catch (Exception ex) { Err(ex); }
        }
    }

    private static void Warn(string msg) =>
        System.Windows.MessageBox.Show(msg, "COPY",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

    private static void Err(Exception ex) =>
        System.Windows.MessageBox.Show($"복사 실패: {ex.Message}", "오류",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
}
