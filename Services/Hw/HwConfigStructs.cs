using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CMS5000.Services.Hw;

// =====================================================================
//  원본 cConfigPK.vb 의 설정 패킷 구조체 byte 단위 포팅.
//  모두 StructLayout(Sequential, Pack=1) — VB 원본과 동일한 메모리 레이아웃.
//  직렬화는 HwMarshal.GetBytes 로 Marshal.StructureToPtr 사용(원본 GetPacket 과 동일).
// =====================================================================

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkOpcode
{
    public byte Command, Type, ReturnCommand, ReturnType;
    public PkOpcode(byte c, byte t, byte rc = 0, byte rt = 0) { Command = c; Type = t; ReturnCommand = rc; ReturnType = rt; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkInfomation
{
    public byte StationId, RackId, ModuleId, ChannelId;
    public PkInfomation(int s, int r, int m, int c) { StationId = (byte)s; RackId = (byte)r; ModuleId = (byte)m; ChannelId = (byte)c; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkTime
{
    public short Year; public byte Month, Day, Hour, Minute; public short Second;
    public static PkTime Now()
    {
        var n = DateTime.Now;
        return new PkTime { Year = (short)n.Year, Month = (byte)n.Month, Day = (byte)n.Day, Hour = (byte)n.Hour, Minute = (byte)n.Minute, Second = (short)n.Second };
    }
}

/// <summary>원본 HEADER (24 byte).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkHeader
{
    public byte Stx;
    public PkOpcode Opcode;
    public PkInfomation Info;
    public PkTime Time;
    public int Length;
    public byte Crc1, Crc2;
    public byte Etx;

    public static PkHeader Create(PkOpcode op, PkInfomation info, int len) => new()
    {
        Stx = HwPacket.STX, Opcode = op, Info = info, Time = PkTime.Now(), Length = len, Crc1 = 0, Crc2 = 0, Etx = HwPacket.ETX
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkTcpip
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] public byte[] IpAddr;
    public int Port;
    public static PkTcpip Create(string ip, int port)
    {
        var buf = new byte[20];
        if (!string.IsNullOrEmpty(ip)) { var b = Encoding.ASCII.GetBytes(ip); Array.Copy(b, buf, Math.Min(b.Length, 20)); }
        return new PkTcpip { IpAddr = buf, Port = port };
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkSerial
{
    public byte Port, DataBit, ParityBit, StopBit;
    public int  BaudRate;
    public static PkSerial Create(int port, int data, int parity, int stop, int baud) =>
        new() { Port = (byte)port, DataBit = (byte)data, ParityBit = (byte)parity, StopBit = (byte)stop, BaudRate = baud };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkCommunication
{
    public PkTcpip Server, Modbus;
    public PkSerial Serial;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkRackInfo
{
    public byte Id, WaveFormInterval; public short Active;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkModuleInfo
{
    public byte Id, Type; public short Active;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkChannelInfo
{
    public byte Id, Type; public short Active;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkAutoUpload
{
    public short Mode, Time;
    public int SuRpm, SdRpm, SuMin, SuMax, SuDelta, SdMin, SdMax, SdDelta, SrBegin, SrEnd, SrDelta;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkSensor
{
    public byte Type, Unit, Icp, Power;
    public float Sensitivity, Lower, Upper;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkReference
{
    public byte RotationDirection, SignalPolarity, ThresholdType, Simulated, AlternatedId, Orientation, WaveformInterval, Dummy;
    public short OrientationAngle, FluctuationRange;
    public int EventPerRevolution, UnalteredTime, SimulateSpeed, Upper, Clamp;
    public float ThresholdLevel, HysteresisLevel;
    public PkSensor Sensor;
    public PkAutoUpload AutoUpload;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkRelayLogic
{
    public int Sequence; public byte ModuleId, ChannelId, Alarm, AndOrEnd;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkRelay
{
    public byte Mode, AndVoting, Dummy1, Dummy2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)] public PkRelayLogic[] Logic;
    public static PkRelay Empty() => new() { Logic = new PkRelayLogic[20] };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkEnableUnit
{
    public byte Rpm, Dir, Dir2, Gap, P2p, Nx1, X1, X1p, X2, X2p, Dummy1, Dummy2;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkScaleRangeClamp
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Rpm;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Dir;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Dir2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Gap;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] P2p;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] Nx1;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] X1;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] X1p;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] X2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public float[] X2p;
    public PkEnableUnit Unit;
    public static PkScaleRangeClamp Empty() => new()
    {
        Rpm = new float[3], Dir = new float[3], Dir2 = new float[3], Gap = new float[3], P2p = new float[3],
        Nx1 = new float[3], X1 = new float[3], X1p = new float[3], X2 = new float[3], X2p = new float[3],
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkAlarm
{
    public PkEnableUnit Enable;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] Rpm;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] Dir;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] Dir2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] Gap;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] P2p;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] Nx1;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] X1;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] X1p;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] X2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public float[] X2p;
    public static PkAlarm Empty() => new()
    {
        Rpm = new float[2], Dir = new float[2], Dir2 = new float[2], Gap = new float[2], P2p = new float[2],
        Nx1 = new float[2], X1 = new float[2], X1p = new float[2], X2 = new float[2], X2p = new float[2],
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkAlarmSet
{
    public byte Bypass, SpecialInhibit, Dummy1, Dummy2;
    public PkAlarm Alert, Danger;
    public static PkAlarmSet Empty() => new() { Alert = PkAlarm.Empty(), Danger = PkAlarm.Empty() };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkChannel
{
    public byte DataType, RecordOut, RecordOutProportional, AlertMode, DangerMode, DangerDelay100Ms,
                Orientation, DirectFreqRange, OkMode, TimedOkDefact, DirectChannelAbove, SyncSampleRevolution,
                RotationDirection, ReferenceActivity, ReferenceId, Dummy1;
    public short OrientationAngle, RampAngle;
    public int   InstCrossRpm, FreqSpan;
    public float AlertDelay, DangerDelay, ZeroPosition, TripMultiply, CrossoverVoltage;
    public PkSensor Sensor;
    public PkScaleRangeClamp ScaleRangeClamp;
    public PkAlarmSet AlarmSet;
    public static PkChannel Empty() => new() { ScaleRangeClamp = PkScaleRangeClamp.Empty(), AlarmSet = PkAlarmSet.Empty() };
}

// ── 합성 패킷 (헤더 포함) ──
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkRackCommConfig { public PkHeader Header; public PkCommunication Communication; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkChannelReference { public PkChannelInfo Info; public PkReference Refer; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkChannelRelay { public PkChannelInfo Info; public PkRelay Relay; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkChannelIo { public PkChannelInfo Info; public PkChannel Io; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkChannelsConfig { public PkHeader Header; public byte Count; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkModuleConfig { public PkHeader Header; public PkModuleInfo Module; public byte ChannelCount; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkModulesConfig { public PkHeader Header; public byte ModuleCount; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PkRackConfig { public PkHeader Header; public PkRackInfo Rack; public byte ModuleCount; }

/// <summary>구조체 → byte[] (원본 Marshal.StructureToPtr 와 동일).</summary>
public static class HwMarshal
{
    public static byte[] GetBytes<T>(T value) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        var buf = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value!, ptr, false);
            Marshal.Copy(ptr, buf, 0, size);
        }
        finally { Marshal.FreeHGlobal(ptr); }
        return buf;
    }

    public static int SizeOf<T>() where T : struct => Marshal.SizeOf<T>();
}
