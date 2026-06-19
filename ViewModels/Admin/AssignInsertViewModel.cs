using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// 원본 frmAssign(매칭) 화면 VM.
/// 좌측 RACK→MODULE→CHANNEL, 우측 TRAIN→COMPONENT→POINT 캐스케이드 선택 후
/// 채널↔포인트를 Assign/UnAssign 한다. 하단 ASSIGN 목록 표시.
/// </summary>
public class AssignInsertViewModel : ViewModelBase
{
    private readonly int _stationId;

    private bool   _isBusy;
    private string _status = "";
    private string _title  = "ASSIGN";

    public bool   IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string Title  { get => _title;  set => SetProperty(ref _title, value); }

    public int StationId => _stationId;

    // ── 좌측: RACK / MODULE / CHANNEL ───────────────────────
    public ObservableCollection<AssignRackRow>    Racks    { get; } = [];
    public ObservableCollection<AssignModuleRow>  Modules  { get; } = [];
    public ObservableCollection<AssignChannelRow> Channels { get; } = [];

    // ── 우측: TRAIN / COMPONENT / POINT ─────────────────────
    public ObservableCollection<AssignTrainRow>     Trains     { get; } = [];
    public ObservableCollection<AssignComponentRow> Components { get; } = [];
    public ObservableCollection<AssignPointRow>     Points     { get; } = [];

    // ── 하단: ASSIGN 목록 ───────────────────────────────────
    public ObservableCollection<AssignListRow> AssignList { get; } = [];

    private AssignRackRow?     _selRack;
    private AssignModuleRow?   _selModule;
    private AssignChannelRow?  _selChannel;
    private AssignTrainRow?    _selTrain;
    private AssignComponentRow? _selComponent;
    private AssignPointRow?    _selPoint;
    private AssignListRow?     _selAssign;

    public AssignRackRow? SelectedRack
    {
        get => _selRack;
        set { SetProperty(ref _selRack, value); _ = OnRackChangedAsync(); }
    }
    public AssignModuleRow? SelectedModule
    {
        get => _selModule;
        set { SetProperty(ref _selModule, value); _ = OnModuleChangedAsync(); }
    }
    public AssignChannelRow? SelectedChannel
    {
        get => _selChannel;
        set { SetProperty(ref _selChannel, value); RaiseSelections(); }
    }
    public AssignTrainRow? SelectedTrain
    {
        get => _selTrain;
        set { SetProperty(ref _selTrain, value); _ = OnTrainChangedAsync(); }
    }
    public AssignComponentRow? SelectedComponent
    {
        get => _selComponent;
        set { SetProperty(ref _selComponent, value); _ = OnComponentChangedAsync(); }
    }
    public AssignPointRow? SelectedPoint
    {
        get => _selPoint;
        set { SetProperty(ref _selPoint, value); RaiseSelections(); }
    }
    public AssignListRow? SelectedAssign
    {
        get => _selAssign;
        set { SetProperty(ref _selAssign, value); OnPropertyChanged(nameof(CanUnAssign)); }
    }

    public bool CanAssign   => SelectedChannel is { IsAssigned: false } && SelectedPoint is { IsAssigned: false };
    public bool CanUnAssign => SelectedAssign != null;

    public string SelectedChannelText => SelectedChannel != null && SelectedRack != null && SelectedModule != null
        ? $"R{SelectedRack.RackId:D2}-M{SelectedModule.ModuleId:D2}-CH{SelectedChannel.ChannelId:D2}  {SelectedChannel.Name}"
        : "(미선택)";

    public string SelectedPointText => SelectedPoint != null && SelectedTrain != null && SelectedComponent != null
        ? $"T{SelectedTrain.TrainId:D2}-C{SelectedComponent.ComponentId:D2}-P{SelectedPoint.PointId:D2}  {SelectedPoint.Name}"
        : "(미선택)";

    public RelayCommand AssignCommand   { get; }
    public RelayCommand UnAssignCommand { get; }
    public RelayCommand RefreshCommand  { get; }

    public AssignInsertViewModel(int stationId)
    {
        _stationId      = stationId;
        AssignCommand   = new RelayCommand(_ => _ = AssignAsync(),   _ => CanAssign);
        UnAssignCommand = new RelayCommand(_ => _ = UnAssignAsync(), _ => CanUnAssign);
        RefreshCommand  = new RelayCommand(_ => _ = LoadAsync());
    }

    private void RaiseSelections()
    {
        OnPropertyChanged(nameof(CanAssign));
        OnPropertyChanged(nameof(SelectedChannelText));
        OnPropertyChanged(nameof(SelectedPointText));
    }

    public async Task LoadAsync()
    {
        IsBusy = true; Status = "";
        try
        {
            Racks.Clear(); Modules.Clear(); Channels.Clear();
            Trains.Clear(); Components.Clear(); Points.Clear();
            AssignList.Clear();

            foreach (var r in await DeviceService.GetAssignRacksAsync(_stationId))  Racks.Add(r);
            foreach (var t in await DeviceService.GetAssignTrainsAsync(_stationId)) Trains.Add(t);
            await ReloadAssignListAsync();

            _selRack = null; _selModule = null; _selChannel = null;
            _selTrain = null; _selComponent = null; _selPoint = null; _selAssign = null;
            OnPropertyChanged(nameof(SelectedRack)); OnPropertyChanged(nameof(SelectedTrain));
            RaiseSelections(); OnPropertyChanged(nameof(CanUnAssign));
        }
        catch (Exception ex) { Status = $"로드 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ReloadAssignListAsync()
    {
        AssignList.Clear();
        foreach (var a in await DeviceService.GetAssignListAsync(_stationId)) AssignList.Add(a);
    }

    private async Task OnRackChangedAsync()
    {
        Modules.Clear(); Channels.Clear();
        RaiseSelections();
        if (_selRack == null) return;
        try { foreach (var m in await DeviceService.GetAssignModulesAsync(_stationId, _selRack.RackId)) Modules.Add(m); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private async Task OnModuleChangedAsync()
    {
        Channels.Clear();
        RaiseSelections();
        if (_selRack == null || _selModule == null) return;
        try { foreach (var c in await DeviceService.GetAssignChannelsAsync(_stationId, _selRack.RackId, _selModule.ModuleId)) Channels.Add(c); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private async Task OnTrainChangedAsync()
    {
        Components.Clear(); Points.Clear();
        RaiseSelections();
        if (_selTrain == null) return;
        try { foreach (var c in await DeviceService.GetAssignComponentsAsync(_stationId, _selTrain.TrainId)) Components.Add(c); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private async Task OnComponentChangedAsync()
    {
        Points.Clear();
        RaiseSelections();
        if (_selTrain == null || _selComponent == null) return;
        try { foreach (var p in await DeviceService.GetAssignPointsAsync(_stationId, _selTrain.TrainId, _selComponent.ComponentId)) Points.Add(p); }
        catch (Exception ex) { Status = ex.Message; }
    }

    private async Task AssignAsync()
    {
        if (!CanAssign || SelectedChannel == null || SelectedPoint == null ||
            SelectedTrain == null || SelectedComponent == null) return;
        IsBusy = true;
        try
        {
            await DeviceService.SetPointAssignAsync(_stationId, SelectedTrain.TrainId, SelectedComponent.ComponentId,
                                                    SelectedPoint.PointId, SelectedChannel.ChannelId);
            Status = $"매칭 완료: {SelectedChannelText}  ←→  {SelectedPointText}";
            await RefreshAfterChangeAsync();
        }
        catch (Exception ex) { Status = $"매칭 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task UnAssignAsync()
    {
        var a = SelectedAssign;
        if (a == null) return;
        IsBusy = true;
        try
        {
            await DeviceService.SetPointAssignAsync(_stationId, a.TrainId, a.ComponentId, a.PointId, 0);
            Status = $"매칭 해제: R{a.RackId:D2}-M{a.ModuleId:D2}-CH{a.ChannelId:D2} ←→ T{a.TrainId:D2}-C{a.ComponentId:D2}-P{a.PointId:D2}";
            await RefreshAfterChangeAsync();
        }
        catch (Exception ex) { Status = $"매칭 해제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task RefreshAfterChangeAsync()
    {
        await ReloadAssignListAsync();
        // 현재 선택된 모듈/컴포넌트의 채널·포인트 Assign 표시 갱신
        if (_selRack != null && _selModule != null) await OnModuleChangedAsync();
        if (_selTrain != null && _selComponent != null) await OnComponentChangedAsync();
        SelectedAssign = null;
    }
}
