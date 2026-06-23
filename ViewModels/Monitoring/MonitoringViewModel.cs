using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CMS5000.Models;
using CMS5000.Models.Monitoring;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Monitoring;

/// <summary>
/// 비-admin 역할(Operator/Maintenance/Expert) 공용 진동 모니터링 화면 VM.
/// Phase 1: 셸(헤더/트리/탭) + Status(List/Bar Graph/Overview) + Events(Alarms/Bearing/Rotor).
/// 데이터는 현재 샘플/합성(추후 실 DB 연동).
/// </summary>
public class MonitoringViewModel : ViewModelBase
{
    public MonitoringViewModel()
    {
        SelectMainTabCommand   = new RelayCommand<string>(t => SelectedMainTab   = t ?? "Status");
        SelectStatusSubCommand = new RelayCommand<string>(t => SelectedStatusSub = t ?? "List");
        SelectEventSubCommand  = new RelayCommand<string>(t => SelectedEventSub  = t ?? "Alarms");
        SelectSideTabCommand   = new RelayCommand<string>(t => SelectedSideTab   = t ?? "Machines");
        ToggleSidebarCommand   = new RelayCommand(_ => ToggleSidebar());
        BuildSampleData();
        _ = LoadDeviceTreesAsync();
    }

    // ── 실 DB 트리: Machines=Train 트리, Devices=Rack 트리 (admin DeviceConfig 연동) ──
    public ObservableCollection<DeviceTreeNode> RackNodes  { get; } = [];
    public ObservableCollection<DeviceTreeNode> TrainNodes { get; } = [];
    /// <summary>현재 사이드 탭에 표시할 트리. Machines→Train, Devices→Rack.</summary>
    public System.Collections.Generic.IEnumerable<DeviceTreeNode> CurrentTree => IsMachinesSide ? TrainNodes : RackNodes;

    /// <summary>로그인·DB 변경 시 트리를 다시 로드(자동 갱신).</summary>
    public Task RefreshTreesAsync() => LoadDeviceTreesAsync();

    private async Task LoadDeviceTreesAsync()
    {
        try
        {
            var stations = await DeviceService.GetStationsAsync();
            var st = stations.FirstOrDefault();
            if (st == null) return;

            var racks  = await DeviceService.GetRackNodesAsync(st.StationId);
            var trains = await DeviceService.GetTrainNodesAsync(st.StationId);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                SelectedMachineName = string.IsNullOrWhiteSpace(st.Name) ? $"Station {st.StationId}" : st.Name;
                RackNodes.Clear();  foreach (var n in racks)  RackNodes.Add(n);
                TrainNodes.Clear(); foreach (var n in trains) TrainNodes.Add(n);
                OnPropertyChanged(nameof(CurrentTree));
            });
        }
        catch { /* DB 미연결 등은 무시(빈 트리) */ }
    }

    // ── 사이드바 폭 / 접기·펼치기 ─────────────────────────
    private GridLength _sidebarWidth = new(320);
    public GridLength SidebarWidth { get => _sidebarWidth; set => SetProperty(ref _sidebarWidth, value); }
    private double _lastSidebarWidth = 320;
    private bool _isSidebarCollapsed;
    public bool IsSidebarCollapsed { get => _isSidebarCollapsed; set => SetProperty(ref _isSidebarCollapsed, value); }
    public RelayCommand ToggleSidebarCommand { get; }

    private void ToggleSidebar()
    {
        if (IsSidebarCollapsed)
        {
            SidebarWidth = new GridLength(_lastSidebarWidth < 120 ? 320 : _lastSidebarWidth);
            IsSidebarCollapsed = false;
        }
        else
        {
            if (SidebarWidth.IsAbsolute && SidebarWidth.Value > 60) _lastSidebarWidth = SidebarWidth.Value;
            SidebarWidth = new GridLength(0);
            IsSidebarCollapsed = true;
        }
    }

    // ── 헤더 ─────────────────────────────────────────────
    public string MachineTitle => $"{SelectedMachineName}  -  CMS-5000";
    private string _selectedMachineName = "BFP-201";
    public string SelectedMachineName
    {
        get => _selectedMachineName;
        set { SetProperty(ref _selectedMachineName, value); OnPropertyChanged(nameof(MachineTitle)); }
    }

    // ── 좌측 사이드바 ────────────────────────────────────
    private string _selectedSideTab = "Machines";
    public string SelectedSideTab { get => _selectedSideTab; set { SetProperty(ref _selectedSideTab, value); RaiseSide(); } }
    public bool IsMachinesSide => SelectedSideTab == "Machines";
    public bool IsDevicesSide  => SelectedSideTab == "Devices";
    private void RaiseSide() { OnPropertyChanged(nameof(IsMachinesSide)); OnPropertyChanged(nameof(IsDevicesSide)); OnPropertyChanged(nameof(CurrentTree)); }

    public ObservableCollection<MachineNode> MachineTree { get; } = [];
    public ObservableCollection<SidebarItem> SpectrumWaveforms { get; } = [];
    public ObservableCollection<SidebarItem> TrendedVariables { get; } = [];

    private MachineNode? _selectedNode;
    public MachineNode? SelectedNode
    {
        get => _selectedNode;
        set { SetProperty(ref _selectedNode, value); if (value is { Kind: "machine" }) SelectedMachineName = value.Name; }
    }

    // ── 메인 탭 (Status / Events / Plots / Case History) ──
    private string _selectedMainTab = "Status";
    public string SelectedMainTab { get => _selectedMainTab; set { SetProperty(ref _selectedMainTab, value); RaiseMain(); } }
    public bool IsStatusTab      => SelectedMainTab == "Status";
    public bool IsEventsTab      => SelectedMainTab == "Events";
    public bool IsPlotsTab       => SelectedMainTab == "Plots";
    public bool IsCaseHistoryTab => SelectedMainTab == "CaseHistory";
    private void RaiseMain()
    {
        OnPropertyChanged(nameof(IsStatusTab)); OnPropertyChanged(nameof(IsEventsTab));
        OnPropertyChanged(nameof(IsPlotsTab));  OnPropertyChanged(nameof(IsCaseHistoryTab));
    }

    // Status 하위탭
    private string _selectedStatusSub = "List";
    public string SelectedStatusSub { get => _selectedStatusSub; set { SetProperty(ref _selectedStatusSub, value); RaiseStatusSub(); } }
    public bool IsStatusList     => SelectedStatusSub == "List";
    public bool IsStatusBar      => SelectedStatusSub == "BarGraph";
    public bool IsStatusOverview => SelectedStatusSub == "Overview";
    private void RaiseStatusSub()
    {
        OnPropertyChanged(nameof(IsStatusList)); OnPropertyChanged(nameof(IsStatusBar)); OnPropertyChanged(nameof(IsStatusOverview));
    }

    // Events 하위탭
    private string _selectedEventSub = "Alarms";
    public string SelectedEventSub { get => _selectedEventSub; set { SetProperty(ref _selectedEventSub, value); RaiseEventSub(); } }
    public bool IsEvAlarms  => SelectedEventSub == "Alarms";
    public bool IsEvBearing => SelectedEventSub == "Bearing";
    public bool IsEvRotor   => SelectedEventSub == "Rotor";
    /// <summary>현재 Events 하위탭에 해당하는 표 데이터.</summary>
    public ObservableCollection<AlarmRow> CurrentEvents => SelectedEventSub switch
    {
        "Bearing" => BearingHealth,
        "Rotor"   => RotorHealth,
        _         => Alarms,
    };
    public string EventsTitle => SelectedEventSub switch
    {
        "Bearing" => "Bearing Health",
        "Rotor"   => "Rotor Health",
        _         => "Alarms / Events",
    };
    private void RaiseEventSub()
    {
        OnPropertyChanged(nameof(IsEvAlarms)); OnPropertyChanged(nameof(IsEvBearing)); OnPropertyChanged(nameof(IsEvRotor));
        OnPropertyChanged(nameof(CurrentEvents)); OnPropertyChanged(nameof(EventsTitle));
    }

    public RelayCommand<string> SelectMainTabCommand   { get; }
    public RelayCommand<string> SelectStatusSubCommand { get; }
    public RelayCommand<string> SelectEventSubCommand  { get; }
    public RelayCommand<string> SelectSideTabCommand   { get; }

    // ── Status 데이터 ────────────────────────────────────
    public ObservableCollection<StatusPointRow> StatusList { get; } = [];
    public ObservableCollection<BarGroup>       BarGroups  { get; } = [];
    public ObservableCollection<OverviewBlock>  Overview   { get; } = [];

    // ── Events 데이터 ────────────────────────────────────
    public ObservableCollection<AlarmRow> Alarms        { get; } = [];
    public ObservableCollection<AlarmRow> BearingHealth { get; } = [];
    public ObservableCollection<AlarmRow> RotorHealth   { get; } = [];

    // ── Plots 데이터 ─────────────────────────────────────
    public ObservableCollection<string> PlotTypeNames { get; } =
    [
        "Trend Plot", "Stacked Trend", "Bode Plot", "Polar Plot", "Shaft Centerline",
        "XvsY Plot", "Spectrum (FFT)", "Spectrum + Trend", "Waterfall Plot",
        "Cascade Plot", "Timebase", "Multi-point Orbit",
    ];
    private string _selectedPlotType = "Trend Plot";
    public string SelectedPlotType
    {
        get => _selectedPlotType;
        set { SetProperty(ref _selectedPlotType, value); OnPropertyChanged(nameof(PlotHeader)); }
    }
    public string PlotHeader => $"{SelectedMachineName}   |   {SelectedPlotType}";

    // ─────────────────────────────────────────────────────
    private void BuildSampleData()
    {
        // 트리
        var root = new MachineNode { Name = "BFP-201", Kind = "root", Status = MonStatus.Alarm };
        var bfp  = new MachineNode { Name = "BFP", Kind = "group", Status = MonStatus.Good };
        bfp.Children.Add(new MachineNode { Name = "BFP-101", Status = MonStatus.Good });
        bfp.Children.Add(new MachineNode { Name = "BFP-201", Status = MonStatus.Good });
        bfp.Children.Add(new MachineNode { Name = "BFP-301", Status = MonStatus.Warning });
        bfp.Children.Add(new MachineNode { Name = "BFP-401", Status = MonStatus.Alert });
        root.Children.Add(bfp);
        MachineTree.Add(root);

        foreach (var t in new[] { "Val Spec O/Al(200 Hz/1600 trend…", "Avg WF(50000 Hz) [13]" })
            SpectrumWaveforms.Add(new SidebarItem { Text = t });
        foreach (var (t, hi) in new[]
        {
            ("Val Spec O/Al(200 Hz/1600 trend…", false),
            ("Demand Spec O/Vel(200 Hz/160…", false),
            ("Avg WF(50000 Hz) [13]", true),
            ("Demand WF(50000 Hz) [13]", false),
        })
            TrendedVariables.Add(new SidebarItem { Text = t, Highlighted = hi });

        // Status > List
        StatusList.Add(new StatusPointRow { Status = MonStatus.Good,    Point = "BFP-1G1 | BFP", Machine = "BFP-101", TagName = "BFP1", LastRecorded = "12/1 mm/s" });
        StatusList.Add(new StatusPointRow { Status = MonStatus.Good,    Point = "BFP-2G1 | BFP", Machine = "BFP-201", TagName = "BFP2", LastRecorded = "10/1 mm/s" });
        StatusList.Add(new StatusPointRow { Status = MonStatus.Warning, Point = "BFP-3G1 | BFP", Machine = "BFP-301", TagName = "Tree", LastRecorded = "0/1 mm/s" });
        StatusList.Add(new StatusPointRow { Status = MonStatus.Alert,   Point = "BFP-4G1 | BFP", Machine = "BFP-401", TagName = "Tree", LastRecorded = "0/1 mm/s" });

        // Status > Bar Graph
        var g1 = new BarGroup { MachineTitle = "BFP-101 | Eighty Critical" };
        g1.Cards.Add(new BarCard { Axis = "Horizontal", Status = MonStatus.Good, Value = 0.015, AxisMax = 0.018, ValueText = "0.015 mm/s rms", Timestamp = "12/24/2024 6:45:30" });
        g1.Cards.Add(new BarCard { Axis = "Vertical",   Status = MonStatus.Good, Value = 0.668, AxisMax = 0.9,   ValueText = "0.668 mm/s rms", Timestamp = "12/24/2024 6:45:28" });
        g1.Cards.Add(new BarCard { Axis = "Axial",      Status = MonStatus.Good, Value = 0.240, AxisMax = 0.27,  ValueText = "0.240 mm/s rms", Timestamp = "12/24/2024 6:45:28" });
        BarGroups.Add(g1);

        var g2 = new BarGroup { MachineTitle = "BFP-301 | Warning" };
        g2.Cards.Add(new BarCard { Axis = "Horizontal", Status = MonStatus.Warning, Value = 0.85, AxisMax = 1.0, ValueText = "0.850 mm/s rms", Timestamp = "12/24/2024 6:45:30" });
        g2.Cards.Add(new BarCard { Axis = "Vertical",   Status = MonStatus.Alert,   Value = 1.05, AxisMax = 1.2, ValueText = "1.050 mm/s rms", Timestamp = "12/24/2024 6:45:28" });
        g2.Cards.Add(new BarCard { Axis = "Horizontal", Status = MonStatus.Alarm,   Value = 2.30, AxisMax = 2.4, ValueText = "2.300 mm/s rms", Timestamp = "12/24/2024 6:45:28" });
        BarGroups.Add(g2);

        // Status > Overview
        Overview.Add(new OverviewBlock { Name = "Motor", Status = MonStatus.Good, Overall = "0.024", Horizontal = "0.020", Axial = "0.010" });
        Overview.Add(new OverviewBlock { Name = "Pump",  Status = MonStatus.Good, Overall = "1.642", Horizontal = "1.173", Axial = "1.098" });
        Overview.Add(new OverviewBlock { Name = "Motor", Status = MonStatus.Alert, Overall = "2.582", Horizontal = "2.582", Vertical = "1.668", Axial = "0.998" });

        // Events > Alarms
        Alarms.Add(new AlarmRow { Level = "Alert", IsAlarm = false, AuditPath = "BFP-201 / BFP / BFP-1G1", DevicePoint = "Val Spec O/Al(200 Hz)",  Machine = "BFP-101", Description = "Overall Vibration over Alert limit", Type = "SW Alert", Value = "0.9950 mm/s rms",  Time = "2024-02-17 08:12:34" });
        Alarms.Add(new AlarmRow { Level = "Alarm", IsAlarm = true,  AuditPath = "BFP-201 / BFP / BFP-4G1", DevicePoint = "Val Spec O/Al(200 Hz)",  Machine = "BFP-401", Description = "Overall Vibration over Alarm limit", Type = "SW Alarm", Value = "11.2634 mm/s rms", Time = "2024-02-17 10:45:22" });
        Alarms.Add(new AlarmRow { Level = "Alert", IsAlarm = false, AuditPath = "BFP-201 / BFP / BFP-2G1", DevicePoint = "Demand Spec O/Vel(200 Hz)", Machine = "BFP-201", Description = "Velocity spectrum alert",          Type = "SW Alert", Value = "3.4210 mm/s rms",  Time = "2024-02-18 14:30:05" });
        Alarms.Add(new AlarmRow { Level = "Alarm", IsAlarm = true,  AuditPath = "BFP-201 / BFP / BFP-3G1", DevicePoint = "Avg WF(50000 Hz)",      Machine = "BFP-301", Description = "Waveform amplitude exceeded",     Type = "SW Alarm", Value = "8.7710 mm/s rms",  Time = "2024-02-19 06:05:50" });

        BearingHealth.Add(new AlarmRow { Level = "Alert", IsAlarm = false, AuditPath = "BFP-201 / BFP / BFP-1G1", DevicePoint = "Bearing Envelope", Machine = "BFP-101", Description = "BPFO energy rising", Type = "Bearing", Value = "2.1 gE", Time = "2024-02-18 09:20:00" });
        RotorHealth.Add(new AlarmRow   { Level = "Alarm", IsAlarm = true,  AuditPath = "BFP-201 / BFP / BFP-4G1", DevicePoint = "1X Amplitude",     Machine = "BFP-401", Description = "Unbalance suspected", Type = "Rotor",   Value = "6.8 mm/s", Time = "2024-02-19 11:10:00" });

        SelectedNode = root;
    }
}
