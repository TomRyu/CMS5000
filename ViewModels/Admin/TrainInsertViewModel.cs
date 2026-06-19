using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

public enum TrainInsertKind { Train, Component, Point }

public class TrainInsertViewModel : ViewModelBase
{
    private readonly TrainInsertKind _kind;
    private readonly int _stationId;
    private readonly int _parentTrainId;
    private readonly int _parentComponentId;

    private int    _nodeId;
    private string _name     = "";
    private bool   _activity = true;
    private int    _assign;
    private ChannelOption? _selectedChannel;

    public bool Modified { get; private set; }
    public event Action? CloseRequested;

    // ── 표시 속성 ─────────────────────────────────────────────
    public string DialogTitle => _kind switch
    {
        TrainInsertKind.Train     => "TRAIN INSERT",
        TrainInsertKind.Component => "COMPONENT INSERT",
        TrainInsertKind.Point     => "POINT INSERT",
        _                         => "INSERT"
    };
    public int    StationId         => _stationId;
    public bool   ShowTrainIdReadonly     => _kind != TrainInsertKind.Train;
    public bool   ShowTrainIdEditable    => _kind == TrainInsertKind.Train;
    public bool   ShowComponentIdRow     => _kind is TrainInsertKind.Component or TrainInsertKind.Point;
    public bool   IsComponentIdEditable  => _kind == TrainInsertKind.Component;
    public bool   IsComponentIdReadonly  => _kind == TrainInsertKind.Point;
    public bool   ShowPointIdRow         => _kind == TrainInsertKind.Point;
    public bool   ShowChannelRow         => _kind == TrainInsertKind.Point;
    public int    ReadonlyTrainId        => _parentTrainId;
    public int    ReadonlyComponentId    => _parentComponentId;
    public string IdLabel => _kind switch
    {
        TrainInsertKind.Train     => "TRAIN ID",
        TrainInsertKind.Component => "COMPONENT ID",
        TrainInsertKind.Point     => "POINT ID",
        _                         => "ID"
    };

    public int    NodeId  { get => _nodeId;  set => SetProperty(ref _nodeId, value); }
    public string Name    { get => _name;    set => SetProperty(ref _name, value); }
    public bool   Activity{ get => _activity;set => SetProperty(ref _activity, value); }
    public int    Assign  { get => _assign;  set => SetProperty(ref _assign, value); }

    public ObservableCollection<ChannelOption> Channels { get; } = [];
    public ChannelOption? SelectedChannel
    {
        get => _selectedChannel;
        set { SetProperty(ref _selectedChannel, value); if (value != null) Assign = value.ChannelId; }
    }

    public RelayCommand SaveCommand   { get; }
    public RelayCommand CancelCommand { get; }

    // Train 추가
    public TrainInsertViewModel(int stationId, int nextTrainId)
    {
        _kind = TrainInsertKind.Train; _stationId = stationId; _nodeId = nextTrainId;
        SaveCommand   = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    // Component 추가
    public TrainInsertViewModel(int stationId, int trainId, int nextComponentId)
    {
        _kind = TrainInsertKind.Component; _stationId = stationId;
        _parentTrainId = trainId; _nodeId = nextComponentId;
        SaveCommand   = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    // Point 추가
    public TrainInsertViewModel(int stationId, int trainId, int componentId, int nextPointId, IEnumerable<ChannelOption> channels)
    {
        _kind = TrainInsertKind.Point; _stationId = stationId;
        _parentTrainId = trainId; _parentComponentId = componentId; _nodeId = nextPointId;
        Channels.Add(new ChannelOption { ChannelId = 0, DisplayName = "(미연결)" });
        foreach (var c in channels) Channels.Add(c);
        _selectedChannel = Channels[0];
        SaveCommand   = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            System.Windows.MessageBox.Show("이름을 입력하세요.", "입력 오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        byte act = (byte)(_activity ? 1 : 0);
        try
        {
            switch (_kind)
            {
                case TrainInsertKind.Train:
                    await DeviceService.CreateTrainAsync(_stationId, _nodeId, Name.Trim(), act);
                    break;
                case TrainInsertKind.Component:
                    await DeviceService.CreateComponentAsync(_stationId, _parentTrainId, _nodeId, Name.Trim(), act);
                    break;
                case TrainInsertKind.Point:
                    await DeviceService.CreatePointAsync(_stationId, _parentTrainId, _parentComponentId, _nodeId, Name.Trim(), act, _assign);
                    break;
            }
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
