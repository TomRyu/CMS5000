namespace CMS5000.Models;

/// <summary>
/// RACK Modify 다이얼로그용 전체 설정값 (원본 frmRack1과 동일 항목).
/// rack + tcpip(local/server/modbus) + serial(local/modbus) 조인 결과.
/// </summary>
public class RackFullInfo
{
    // 기본
    public string Name     { get; set; } = "";
    public string Location { get; set; } = "";
    public bool   Activity { get; set; }

    // Waveform / Trend
    public int  WaveformInterval { get; set; }
    public bool Trend            { get; set; }
    public int  StaticTrend      { get; set; }
    public int  DynamicTrend     { get; set; }

    // Local TCP/IP
    public string LocalIp   { get; set; } = "";
    public int    LocalPort { get; set; }

    // Local Serial (databit/paritybit/stopbit 은 콤보 인덱스 저장 방식)
    public int LocalSerialPort { get; set; }
    public int LocalBaudRate   { get; set; }
    public int LocalDataBit    { get; set; }
    public int LocalParityBit  { get; set; }
    public int LocalStopBit    { get; set; }

    // Server TCP/IP
    public string ServerIp   { get; set; } = "";
    public int    ServerPort { get; set; }

    // Modbus
    public int    ModbusMode { get; set; }  // 0=사용안함, 1=TCP/IP, 2=SERIAL
    public string ModbusIp   { get; set; } = "";
    public int    ModbusPort { get; set; }
    public int    ModSerialPort { get; set; }
    public int    ModBaudRate   { get; set; }
    public int    ModDataBit    { get; set; }
    public int    ModParityBit  { get; set; }
    public int    ModStopBit    { get; set; }
}
