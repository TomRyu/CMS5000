using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// Reference Config(원본 frmReference) VM. 4탭(INFO. / Sensor &amp; Auto Upload / Auto Upload / Min·Max·Delta)의
/// 모든 신호설정 값을 보관·로드·저장한다. 라디오 그룹은 정수값 + bool 헬퍼로 노출한다.
/// </summary>
public class ReferenceConfigViewModel : ViewModelBase
{
    public int StationId { get; }
    public int RackId    { get; }
    public int ModuleId  { get; }
    public int ChannelId { get; }

    public string DialogTitle => $"Reference [CH{ChannelId:D2}] Config";

    private readonly ReferenceConfigInfo _c = new();

    // ── 콤보 소스 ────────────────────────────────────────────
    public static IReadOnlyList<string> ActivityModes => ["Inactivity", "Activity", "Simulated"];
    public static IReadOnlyList<string> AlternateIds  => ["1", "2", "3", "4"];
    public static IReadOnlyList<string> Conditions    => ["None", "Time", "RPM", "Both"];
    public ObservableCollection<ChannelOption> ChannelTypes { get; } = [];
    public ObservableCollection<string>        SensorNames  { get; } = [];
    public ObservableCollection<string>        SensorUnits  { get; } = [];

    // ── 헤더 ─────────────────────────────────────────────────
    public string Name         { get => _c.Name;         set { _c.Name = value; OnPropertyChanged(); } }
    public int    ChannelTypeIndex { get => _channelTypeIndex; set => SetProperty(ref _channelTypeIndex, value); }
    private int   _channelTypeIndex = -1;
    public int    ActivityMode { get => _c.ActivityMode; set { _c.ActivityMode = value; OnPropertyChanged(); RaiseReassignEnable(); } }
    public bool   Assign       { get => _c.Assign;       set { _c.Assign = value; OnPropertyChanged(); } }

    // Reference Reassignment 활성 조건 (원본: Activity=Simulated 일 때만)
    public bool ReassignEnabled => _c.ActivityMode == 2;
    public bool SpeedEnabled    => ReassignEnabled && _c.ReassignMode == 1;
    public bool AltEnabled      => ReassignEnabled && _c.ReassignMode == 0;
    private void RaiseReassignEnable()
    {
        OnPropertyChanged(nameof(ReassignEnabled));
        OnPropertyChanged(nameof(SpeedEnabled));
        OnPropertyChanged(nameof(AltEnabled));
        OnPropertyChanged(nameof(ReassignAlternate));
        OnPropertyChanged(nameof(ReassignSimulated));
    }

    // ── INFO 탭 ──────────────────────────────────────────────
    public int Speed            { get => _c.Speed;            set { _c.Speed = value; OnPropertyChanged(); } }
    public int AlternateId      { get => _c.AlternateId;      set { _c.AlternateId = value; OnPropertyChanged(); } }
    public double ThresholdLevel { get => _c.ThresholdLevel;   set { _c.ThresholdLevel = value; OnPropertyChanged(); } }
    public int    ClampValue      { get => _c.ClampValue;       set { _c.ClampValue = value; OnPropertyChanged(); } }
    public int    UpperLimit      { get => _c.UpperLimit;       set { _c.UpperLimit = value; OnPropertyChanged(); } }
    public double HysteresisLevel { get => _c.HysteresisLevel;  set { _c.HysteresisLevel = value; OnPropertyChanged(); } }
    public int FluctuationRange { get => _c.FluctuationRange; set { _c.FluctuationRange = value; OnPropertyChanged(); } }
    public int UnalteredTime    { get => _c.UnalteredTime;    set { _c.UnalteredTime = value; OnPropertyChanged(); } }
    public int OrientationAngle { get => _c.OrientationAngle; set { _c.OrientationAngle = value; OnPropertyChanged(); } }
    public int WaveFormInterval { get => _c.WaveFormInterval; set { _c.WaveFormInterval = value; OnPropertyChanged(); } }
    public int EpRevolution     { get => _c.EpRevolution;     set { _c.EpRevolution = value; OnPropertyChanged(); } }

    // 라디오 그룹 (정수 + bool 헬퍼)
    // 비활성(Activity≠Simulated)일 땐 원본처럼 아무것도 선택되지 않도록 false 반환
    public bool ReassignAlternate { get => ReassignEnabled && _c.ReassignMode == 0; set { if (value) { _c.ReassignMode = 0; RaiseRadio(); } } }
    public bool ReassignSimulated { get => ReassignEnabled && _c.ReassignMode == 1; set { if (value) { _c.ReassignMode = 1; RaiseRadio(); } } }
    public bool RotationCw  { get => _c.RotationDir == 0; set { if (value) { _c.RotationDir = 0; RaiseRadio(); } } }
    public bool RotationCcw { get => _c.RotationDir == 1; set { if (value) { _c.RotationDir = 1; RaiseRadio(); } } }
    public bool PolarityNotch      { get => _c.SignalPolarity == 0; set { if (value) { _c.SignalPolarity = 0; RaiseRadio(); } } }
    public bool PolarityProjection { get => _c.SignalPolarity == 1; set { if (value) { _c.SignalPolarity = 1; RaiseRadio(); } } }
    public bool ThresholdAuto   { get => _c.ThresholdType == 0; set { if (value) { _c.ThresholdType = 0; RaiseRadio(); } } }
    public bool ThresholdManual { get => _c.ThresholdType == 1; set { if (value) { _c.ThresholdType = 1; RaiseRadio(); } } }
    public bool OrientationLeft  { get => _c.Orientation == 0; set { if (value) { _c.Orientation = 0; RaiseRadio(); } } }
    public bool OrientationRight { get => _c.Orientation == 1; set { if (value) { _c.Orientation = 1; RaiseRadio(); } } }

    // ── Sensor 탭 ────────────────────────────────────────────
    public string SensorName  { get => _c.SensorName;  set { _c.SensorName = value; OnPropertyChanged(); } }
    public int    Sensitivity { get => _c.Sensitivity; set { _c.Sensitivity = value; OnPropertyChanged(); } }
    public string SensorUnit  { get => _c.SensorUnit;  set { _c.SensorUnit = value; OnPropertyChanged(); } }
    public double PowerLow    { get => _c.PowerLow;    set { _c.PowerLow = value; OnPropertyChanged(); } }
    public double PowerHigh   { get => _c.PowerHigh;   set { _c.PowerHigh = value; OnPropertyChanged(); } }
    public bool IcpOn  { get => _c.Icp == 1; set { if (value) { _c.Icp = 1; RaiseRadio(); } } }
    public bool IcpOff { get => _c.Icp == 0; set { if (value) { _c.Icp = 0; RaiseRadio(); } } }
    public bool Power24 { get => _c.ProximitorPower == 0; set { if (value) { _c.ProximitorPower = 0; RaiseRadio(); } } }
    public bool Power18 { get => _c.ProximitorPower == 1; set { if (value) { _c.ProximitorPower = 1; RaiseRadio(); } } }
    public bool SignalProximitor { get => _c.SignalType == 0; set { if (value) { _c.SignalType = 0; RaiseRadio(); } } }
    public bool SignalMagnetic   { get => _c.SignalType == 1; set { if (value) { _c.SignalType = 1; RaiseRadio(); } } }

    // ── Auto Upload 탭 ───────────────────────────────────────
    public int UploadTime      { get => _c.UploadTime;      set { _c.UploadTime = value; OnPropertyChanged(); } }
    public int UploadCondition { get => _c.UploadCondition; set { _c.UploadCondition = value; OnPropertyChanged(); } }
    public int StartUpRpm      { get => _c.StartUpRpm;      set { _c.StartUpRpm = value; OnPropertyChanged(); } }
    public int ShutDownRpm     { get => _c.ShutDownRpm;     set { _c.ShutDownRpm = value; OnPropertyChanged(); } }

    // ── Min/Max/Delta 탭 ─────────────────────────────────────
    public int SrBegin { get => _c.SrBegin; set { _c.SrBegin = value; OnPropertyChanged(); } }
    public int SrEnd   { get => _c.SrEnd;   set { _c.SrEnd = value; OnPropertyChanged(); } }
    public int SrDelta { get => _c.SrDelta; set { _c.SrDelta = value; OnPropertyChanged(); } }
    public int SdMax   { get => _c.SdMax;   set { _c.SdMax = value; OnPropertyChanged(); } }
    public int SdMin   { get => _c.SdMin;   set { _c.SdMin = value; OnPropertyChanged(); } }
    public int SdDelta { get => _c.SdDelta; set { _c.SdDelta = value; OnPropertyChanged(); } }
    public int SuMax   { get => _c.SuMax;   set { _c.SuMax = value; OnPropertyChanged(); } }
    public int SuMin   { get => _c.SuMin;   set { _c.SuMin = value; OnPropertyChanged(); } }
    public int SuDelta { get => _c.SuDelta; set { _c.SuDelta = value; OnPropertyChanged(); } }

    public bool Modified { get; private set; }
    public event Action? CloseRequested;

    public RelayCommand OkCommand     { get; }
    public RelayCommand CancelCommand { get; }

    public ReferenceConfigViewModel(int stationId, int rackId, int moduleId, int channelId)
    {
        StationId = stationId; RackId = rackId; ModuleId = moduleId; ChannelId = channelId;
        OkCommand     = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    private void RaiseRadio()
    {
        // 모든 라디오 헬퍼 갱신 (상호배타 표시)
        foreach (var n in new[]
        {
            nameof(ReassignAlternate), nameof(ReassignSimulated), nameof(RotationCw), nameof(RotationCcw),
            nameof(PolarityProjection), nameof(PolarityNotch), nameof(ThresholdManual), nameof(ThresholdAuto),
            nameof(OrientationLeft), nameof(OrientationRight), nameof(IcpOn), nameof(IcpOff),
            nameof(Power18), nameof(Power24), nameof(SignalMagnetic), nameof(SignalProximitor),
        })
            OnPropertyChanged(n);
        RaiseReassignEnable();
    }

    public async Task LoadAsync()
    {
        try
        {
            foreach (var ct in await DeviceService.GetChannelTypeOptionsAsync()) ChannelTypes.Add(ct);

            var info = await DeviceService.GetReferenceConfigAsync(StationId, RackId, ModuleId, ChannelId);
            CopyFrom(info);

            // Sensor Info 콤보: 해당 채널타입의 센서만 (원본과 동일)
            try { foreach (var s in await DeviceService.GetSensorNamesAsync(info.ChannelType)) SensorNames.Add(s); } catch { }

            int idx = ChannelTypes.ToList().FindIndex(c => c.ChannelId == info.ChannelType);
            ChannelTypeIndex = idx >= 0 ? idx : (ChannelTypes.Count > 0 ? 0 : -1);

            // 전 속성 갱신
            foreach (var p in GetType().GetProperties())
                if (p.CanRead && p.GetIndexParameters().Length == 0)
                    OnPropertyChanged(p.Name);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Reference 정보 로드 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void CopyFrom(ReferenceConfigInfo s)
    {
        _c.Name = s.Name; _c.ChannelType = s.ChannelType; _c.ActivityMode = s.ActivityMode; _c.Assign = s.Assign;
        _c.ReassignMode = s.ReassignMode; _c.Speed = s.Speed; _c.AlternateId = s.AlternateId; _c.RotationDir = s.RotationDir;
        _c.SignalPolarity = s.SignalPolarity; _c.ThresholdType = s.ThresholdType; _c.ThresholdLevel = s.ThresholdLevel;
        _c.ClampValue = s.ClampValue; _c.UpperLimit = s.UpperLimit; _c.HysteresisLevel = s.HysteresisLevel;
        _c.FluctuationRange = s.FluctuationRange; _c.UnalteredTime = s.UnalteredTime; _c.OrientationAngle = s.OrientationAngle;
        _c.Orientation = s.Orientation; _c.WaveFormInterval = s.WaveFormInterval; _c.EpRevolution = s.EpRevolution;
        _c.SensorName = s.SensorName; _c.Sensitivity = s.Sensitivity; _c.SensorUnit = s.SensorUnit; _c.Icp = s.Icp;
        _c.PowerLow = s.PowerLow; _c.PowerHigh = s.PowerHigh; _c.ProximitorPower = s.ProximitorPower; _c.SignalType = s.SignalType;
        _c.UploadTime = s.UploadTime; _c.UploadCondition = s.UploadCondition; _c.StartUpRpm = s.StartUpRpm; _c.ShutDownRpm = s.ShutDownRpm;
        _c.SrBegin = s.SrBegin; _c.SrEnd = s.SrEnd; _c.SrDelta = s.SrDelta;
        _c.SdMax = s.SdMax; _c.SdMin = s.SdMin; _c.SdDelta = s.SdDelta;
        _c.SuMax = s.SuMax; _c.SuMin = s.SuMin; _c.SuDelta = s.SuDelta;
    }

    private async Task SaveAsync()
    {
        try
        {
            _c.ChannelType = (ChannelTypeIndex >= 0 && ChannelTypeIndex < ChannelTypes.Count)
                ? ChannelTypes[ChannelTypeIndex].ChannelId : 0;

            await DeviceService.UpsertReferenceConfigAsync(StationId, RackId, ModuleId, ChannelId, _c);
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
