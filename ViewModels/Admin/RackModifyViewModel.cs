using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

/// <summary>
/// RACK Modify 다이얼로그 VM. 원본(frmRack1)과 동일한 3탭(INFO / Modbus / Server Connect.)을
/// 갖추며, DB에서 전체 설정값을 로드하고 저장한다. 콤보 소스는 <see cref="RackInsertViewModel"/> 재사용.
/// </summary>
public class RackModifyViewModel : ViewModelBase
{
    public DeviceTreeNode RackNode { get; }

    // ── 기본 ─────────────────────────────────────────────────
    private int    _stationId;
    private int    _rackId;
    private bool   _activity = true;

    // ── INFO ─────────────────────────────────────────────────
    private string _name         = "";
    private string _location     = "";
    private string _localIp      = "";
    private int    _localPort     = 3000;
    private string _localComPort = "COM1";
    private string _baudRate     = "38400";
    private int    _dataBit      = 3;   // index: 0=5,1=6,2=7,3=8
    private int    _parityBit;          // index: 0=NONE,1=EVEN,2=ODD,3=MARK,4=SPACE
    private int    _stopBit;            // index: 0=1,1=2,2=1.5
    private int    _waveformInterval;
    private bool   _trend;
    private int    _staticTrend  = 10;
    private int    _dynamicTrend = 10;

    // ── Modbus ───────────────────────────────────────────────
    private int    _modbusModeIndex;
    private string _modbusIp   = "";
    private int    _modbusPort = 502;
    // (원본과 동일하게 Modbus Serial 그룹은 화면에 표시하지 않으나 값은 보존한다)
    private string _modComPort = "COM1";
    private string _modBaudRate = "38400";
    private int    _modDataBit  = 3;
    private int    _modParityBit;
    private int    _modStopBit;

    // ── Server Connect ───────────────────────────────────────
    private string _serverIp   = "";
    private int    _serverPort = 3000;

    // ── 결과 ─────────────────────────────────────────────────
    public bool Modified { get; private set; }
    public event Action? CloseRequested;

    // ── 콤보 소스 (Insert VM 재사용) ──────────────────────────
    public static IReadOnlyList<string> BaudRates   => RackInsertViewModel.BaudRates;
    public static IReadOnlyList<string> DataBits    => RackInsertViewModel.DataBits;
    public static IReadOnlyList<string> ParityBits  => RackInsertViewModel.ParityBits;
    public static IReadOnlyList<string> StopBits    => RackInsertViewModel.StopBits;
    public static IReadOnlyList<string> ModbusModes => RackInsertViewModel.ModbusModes;
    public static IReadOnlyList<string> ComPorts    => RackInsertViewModel.ComPorts;

    // ── 바인딩 프로퍼티 ──────────────────────────────────────
    public string DialogTitle => $"RACK [{RackNode.RackId:D2}] MODIFY.";
    public int    StationId { get => _stationId; set => SetProperty(ref _stationId, value); }
    public int    RackId    { get => _rackId;    set => SetProperty(ref _rackId, value); }
    public bool   Activity  { get => _activity;  set => SetProperty(ref _activity, value); }

    public string Name             { get => _name;             set => SetProperty(ref _name, value); }
    public string Location         { get => _location;         set => SetProperty(ref _location, value); }
    public string LocalIp          { get => _localIp;          set => SetProperty(ref _localIp, value); }
    public int    LocalPort        { get => _localPort;        set => SetProperty(ref _localPort, value); }
    public string LocalComPort     { get => _localComPort;     set => SetProperty(ref _localComPort, value); }
    public string BaudRate         { get => _baudRate;         set => SetProperty(ref _baudRate, value); }
    public int    DataBit          { get => _dataBit;          set => SetProperty(ref _dataBit, value); }
    public int    ParityBit        { get => _parityBit;        set => SetProperty(ref _parityBit, value); }
    public int    StopBit          { get => _stopBit;          set => SetProperty(ref _stopBit, value); }
    public int    WaveformInterval { get => _waveformInterval; set => SetProperty(ref _waveformInterval, value); }
    public bool   Trend            { get => _trend;            set { SetProperty(ref _trend, value); OnPropertyChanged(nameof(TrendEnabled)); } }
    public bool   TrendEnabled     => _trend;
    public int    StaticTrend      { get => _staticTrend;      set => SetProperty(ref _staticTrend, value); }
    public int    DynamicTrend     { get => _dynamicTrend;     set => SetProperty(ref _dynamicTrend, value); }

    public int    ModbusModeIndex  { get => _modbusModeIndex;  set => SetProperty(ref _modbusModeIndex, value); }
    public string ModbusIp         { get => _modbusIp;         set => SetProperty(ref _modbusIp, value); }
    public int    ModbusPort       { get => _modbusPort;       set => SetProperty(ref _modbusPort, value); }

    public string ServerIp         { get => _serverIp;         set => SetProperty(ref _serverIp, value); }
    public int    ServerPort       { get => _serverPort;       set => SetProperty(ref _serverPort, value); }

    public RelayCommand ModifyCommand { get; }
    public RelayCommand CancelCommand { get; }

    public RackModifyViewModel(DeviceTreeNode rackNode)
    {
        RackNode  = rackNode;
        StationId = rackNode.StationId;
        RackId    = rackNode.RackId;

        // 노드 기본값 우선 표시 → DB 로드로 덮어씀
        Name     = rackNode.Name;
        Location = rackNode.Location;
        Activity = rackNode.IsActive;

        ModifyCommand = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var info = await DeviceService.GetRackFullAsync(RackNode.StationId, RackNode.RackId);
            if (info == null) return;

            Name             = info.Name;
            Location         = info.Location;
            Activity         = info.Activity;
            WaveformInterval = info.WaveformInterval;
            Trend            = info.Trend;
            StaticTrend      = info.StaticTrend;
            DynamicTrend     = info.DynamicTrend;

            LocalIp      = info.LocalIp;
            LocalPort    = info.LocalPort;
            LocalComPort = info.LocalSerialPort > 0 ? $"COM{info.LocalSerialPort}" : "";
            BaudRate     = info.LocalBaudRate > 0 ? info.LocalBaudRate.ToString() : "";
            DataBit      = info.LocalDataBit;
            ParityBit    = info.LocalParityBit;
            StopBit      = info.LocalStopBit;

            ServerIp   = info.ServerIp;
            ServerPort = info.ServerPort;

            ModbusModeIndex = info.ModbusMode;
            ModbusIp        = info.ModbusIp;
            ModbusPort      = info.ModbusPort;

            _modComPort   = info.ModSerialPort > 0 ? $"COM{info.ModSerialPort}" : "";
            _modBaudRate  = info.ModBaudRate > 0 ? info.ModBaudRate.ToString() : "";
            _modDataBit   = info.ModDataBit;
            _modParityBit = info.ModParityBit;
            _modStopBit   = info.ModStopBit;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"랙 정보 로드 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            System.Windows.MessageBox.Show("RACK 이름을 입력하세요.", "입력 오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        try
        {
            var info = new RackFullInfo
            {
                Name             = Name.Trim(),
                Location         = Location.Trim(),
                Activity         = Activity,
                WaveformInterval = WaveformInterval,
                Trend            = Trend,
                StaticTrend      = StaticTrend,
                DynamicTrend     = DynamicTrend,
                LocalIp          = LocalIp.Trim(),
                LocalPort        = LocalPort,
                LocalSerialPort  = ParseCom(LocalComPort),
                LocalBaudRate    = int.TryParse(BaudRate, out int b) ? b : 0,
                LocalDataBit     = DataBit,
                LocalParityBit   = ParityBit,
                LocalStopBit     = StopBit,
                ServerIp         = ServerIp.Trim(),
                ServerPort       = ServerPort,
                ModbusMode       = ModbusModeIndex,
                ModbusIp         = ModbusIp.Trim(),
                ModbusPort       = ModbusPort,
                ModSerialPort    = ParseCom(_modComPort),
                ModBaudRate      = int.TryParse(_modBaudRate, out int mb) ? mb : 0,
                ModDataBit       = _modDataBit,
                ModParityBit     = _modParityBit,
                ModStopBit       = _modStopBit,
            };

            await DeviceService.UpdateRackFullAsync(RackNode.StationId, RackNode.RackId, info);

            Modified = true;
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"저장 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>"COM3" → 3. 형식이 아니면 0.</summary>
    private static int ParseCom(string com)
    {
        if (string.IsNullOrWhiteSpace(com)) return 0;
        return int.TryParse(new string([.. com.Where(char.IsDigit)]), out int n) ? n : 0;
    }
}
