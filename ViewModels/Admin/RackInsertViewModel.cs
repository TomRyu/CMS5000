using System.Collections.ObjectModel;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

public class RackInsertViewModel : ViewModelBase
{
    // ── 기본 ─────────────────────────────────────────────────
    private readonly int _stationId;
    private int    _rackId;
    private bool   _activity = true;

    // ── INFO ─────────────────────────────────────────────────
    private string _name             = "";
    private string _location         = "";
    private string _localIp          = "";
    private string _localPort        = "3000";
    private int    _serialPort;
    private string _baudRate         = "9600";
    private int    _dataBit          = 3;   // index (0=5,1=6,2=7,3=8)
    private int    _parityBit;              // index (0=None,1=Odd,2=Even)
    private int    _stopBit;               // index (0=1,1=1.5,2=2)
    private string _waveformInterval = "0";
    private bool   _trend;
    private string _staticTrend      = "10";
    private string _dynamicTrend     = "10";

    // ── Modbus ───────────────────────────────────────────────
    private int    _modbusModeIndex;
    private string _modbusIp   = "";
    private string _modbusPort = "502";
    private int    _modSerialPort;
    private string _modBaudRate = "9600";
    private int    _modDataBit  = 3;
    private int    _modParityBit;
    private int    _modStopBit;

    // ── Server Connect ───────────────────────────────────────
    private string _serverIp   = "";
    private string _serverPort = "3000";

    // ── 결과 ─────────────────────────────────────────────────
    public bool Modified { get; private set; }
    public int  SavedRackId => _rackId;

    public event Action? CloseRequested;

    // ── 콤보박스 소스 (원본 frmRack1 항목·순서와 동일) ──────────
    public static IReadOnlyList<string> BaudRates    { get; } = ["4800","9200","19200","38400","57600","115200"];
    public static IReadOnlyList<string> DataBits     { get; } = ["5","6","7","8"];
    public static IReadOnlyList<string> ParityBits   { get; } = ["NONE","EVEN","ODD","MARK","SPACE"];
    public static IReadOnlyList<string> StopBits     { get; } = ["1","2","1.5"];
    public static IReadOnlyList<string> ModbusModes  { get; } = ["사용안함","TCP/IP","SERIAL"];
    public static IReadOnlyList<string> ComPorts     { get; } = ["COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","COM10","COM11","COM12","COM13","COM14","COM15","COM16"];

    // ── 바인딩 프로퍼티 ──────────────────────────────────────
    public string DialogTitle => "RACK INSERT";
    public int    StationId   => _stationId;

    public int    RackId   { get => _rackId;   set => SetProperty(ref _rackId, value); }
    public bool   Activity { get => _activity; set => SetProperty(ref _activity, value); }

    public string Name             { get => _name;             set => SetProperty(ref _name, value); }
    public string Location         { get => _location;         set => SetProperty(ref _location, value); }
    public string LocalIp          { get => _localIp;          set => SetProperty(ref _localIp, value); }
    public string LocalPort        { get => _localPort;        set => SetProperty(ref _localPort, value); }
    public int    SerialPort       { get => _serialPort;       set => SetProperty(ref _serialPort, value); }
    public string BaudRate         { get => _baudRate;         set => SetProperty(ref _baudRate, value); }
    public int    DataBit          { get => _dataBit;          set => SetProperty(ref _dataBit, value); }
    public int    ParityBit        { get => _parityBit;        set => SetProperty(ref _parityBit, value); }
    public int    StopBit          { get => _stopBit;          set => SetProperty(ref _stopBit, value); }
    public string WaveformInterval { get => _waveformInterval; set => SetProperty(ref _waveformInterval, value); }
    public bool   Trend            { get => _trend;            set { SetProperty(ref _trend, value); OnPropertyChanged(nameof(TrendEnabled)); } }
    public bool   TrendEnabled     => _trend;
    public string StaticTrend      { get => _staticTrend;      set => SetProperty(ref _staticTrend, value); }
    public string DynamicTrend     { get => _dynamicTrend;     set => SetProperty(ref _dynamicTrend, value); }

    public int    ModbusModeIndex  { get => _modbusModeIndex;  set { SetProperty(ref _modbusModeIndex, value); OnPropertyChanged(nameof(IsModbusTcp)); OnPropertyChanged(nameof(IsModbusSerial)); } }
    public bool   IsModbusTcp      => _modbusModeIndex == 1;
    public bool   IsModbusSerial   => _modbusModeIndex == 2;
    public string ModbusIp         { get => _modbusIp;         set => SetProperty(ref _modbusIp, value); }
    public string ModbusPort       { get => _modbusPort;       set => SetProperty(ref _modbusPort, value); }
    public int    ModSerialPort    { get => _modSerialPort;    set => SetProperty(ref _modSerialPort, value); }
    public string ModBaudRate      { get => _modBaudRate;      set => SetProperty(ref _modBaudRate, value); }
    public int    ModDataBit       { get => _modDataBit;       set => SetProperty(ref _modDataBit, value); }
    public int    ModParityBit     { get => _modParityBit;     set => SetProperty(ref _modParityBit, value); }
    public int    ModStopBit       { get => _modStopBit;       set => SetProperty(ref _modStopBit, value); }

    public string ServerIp         { get => _serverIp;         set => SetProperty(ref _serverIp, value); }
    public string ServerPort       { get => _serverPort;       set => SetProperty(ref _serverPort, value); }

    public RelayCommand SaveCommand   { get; }
    public RelayCommand CancelCommand { get; }

    public RackInsertViewModel(int stationId, int nextRackId)
    {
        _stationId = stationId;
        _rackId    = nextRackId;
        SaveCommand   = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
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
            int localPort       = int.TryParse(_localPort, out int lp) ? lp : 3000;
            int waveformInt     = int.TryParse(_waveformInterval, out int wi) ? wi : 0;
            int staticTrendInt  = int.TryParse(_staticTrend,  out int st) ? st : 10;
            int dynamicTrendInt = int.TryParse(_dynamicTrend, out int dt) ? dt : 10;
            int serverPortInt   = int.TryParse(_serverPort, out int sp) ? sp : 3000;
            int modbusPortInt   = int.TryParse(_modbusPort, out int mp) ? mp : 502;
            int baudInt         = int.TryParse(_baudRate,   out int b)  ? b  : 9600;
            int modBaudInt      = int.TryParse(_modBaudRate,out int mb) ? mb : 9600;

            await DeviceService.CreateRackFullAsync(
                _stationId, _rackId, _activity,
                Name.Trim(), Location.Trim(),
                LocalIp.Trim(), localPort,
                _serialPort, baudInt, _dataBit, _parityBit, _stopBit,
                waveformInt, _trend, staticTrendInt, dynamicTrendInt,
                _modbusModeIndex, ModbusIp.Trim(), modbusPortInt,
                _modSerialPort, modBaudInt, _modDataBit, _modParityBit, _modStopBit,
                ServerIp.Trim(), serverPortInt);

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
