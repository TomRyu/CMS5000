using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// RACK COPY 다이얼로그(원본 frmCopy "RACK COPY....") VM.
/// Original Source(읽기전용) + Destination(스테이션/랙 콤보) 선택 → 복사.
/// </summary>
public class RackCopyViewModel : ViewModelBase
{
    private readonly DeviceTreeNode _srcNode;

    public int    SrcStationId { get; }
    public int    SrcRackId    { get; }
    public string DialogTitle => "RACK COPY....";

    public ObservableCollection<DeviceStation> Stations  { get; } = [];
    public ObservableCollection<int>           DestRacks { get; } = [];

    private DeviceStation? _destStation;
    public DeviceStation? DestStation
    {
        get => _destStation;
        set { SetProperty(ref _destStation, value); _ = LoadDestRacksAsync(); }
    }

    private string _destRackText = "";
    public string DestRackText { get => _destRackText; set => SetProperty(ref _destRackText, value); }

    public bool Modified { get; private set; }
    public event Action? CloseRequested;

    public RelayCommand CopyCommand   { get; }
    public RelayCommand CancelCommand { get; }

    public RackCopyViewModel(DeviceTreeNode rackNode)
    {
        _srcNode     = rackNode;
        SrcStationId = rackNode.StationId;
        SrcRackId    = rackNode.RackId;

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

    private async Task CopyAsync()
    {
        if (DestStation == null)
        {
            Warn("대상 스테이션을 선택하세요."); return;
        }
        if (!int.TryParse(DestRackText?.Trim(), out int destRack) || destRack <= 0)
        {
            Warn("대상 RACK ID를 입력/선택하세요."); return;
        }
        if (DestStation.StationId == SrcStationId && destRack == SrcRackId)
        {
            Warn("원본과 동일한 위치입니다."); return;
        }
        try
        {
            await DeviceService.CopyRackAsync(SrcStationId, SrcRackId, DestStation.StationId, destRack);
            Modified = true;
            System.Windows.MessageBox.Show($"RACK {SrcRackId:D2} → [{DestStation.StationId}] RACK {destRack:D2} 복사 완료.",
                "RACK COPY", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"복사 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static void Warn(string msg) =>
        System.Windows.MessageBox.Show(msg, "RACK COPY",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
}
