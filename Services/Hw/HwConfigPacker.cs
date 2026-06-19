using System;
using CMS5000.Models;

namespace CMS5000.Services.Hw;

/// <summary>
/// CMS-5000 데이터(DB) → 포팅된 설정 구조체 → byte 패킷.
/// 원본 cConfigPK 의 패킷 조립(GetRackPacket 등)에 대응.
/// </summary>
public static class HwConfigPacker
{
    /// <summary>RACK COMMUNICATION (Server/Modbus TCP + Serial).</summary>
    public static byte[] BuildRackComm(int station, int rack, RackFullInfo? f)
    {
        var comm = new PkCommunication
        {
            Server = PkTcpip.Create(f?.ServerIp ?? "", f?.ServerPort ?? 0),
            Modbus = PkTcpip.Create(f?.ModbusIp ?? "", f?.ModbusPort ?? 0),
            Serial = PkSerial.Create(f?.LocalSerialPort ?? 0, f?.LocalDataBit ?? 0, f?.LocalParityBit ?? 0, f?.LocalStopBit ?? 0, f?.LocalBaudRate ?? 0),
        };
        var pk = new PkRackCommConfig
        {
            Header = PkHeader.Create(new PkOpcode(HwPacket.CMD_CFG, HwPacket.TYP_COMM_CFG),
                                     new PkInfomation(station, rack, 0, 0), HwMarshal.SizeOf<PkCommunication>()),
            Communication = comm,
        };
        return HwMarshal.GetBytes(pk);
    }

    /// <summary>MODULE_CONFIG (헤더 + MODULE_INFO + channelCount).</summary>
    public static byte[] BuildModule(int station, int rack, int module)
    {
        var pk = new PkModuleConfig
        {
            Header = PkHeader.Create(new PkOpcode(HwPacket.CMD_CFG, HwPacket.TYP_MODULE_CFG),
                                     new PkInfomation(station, rack, module, 0), HwMarshal.SizeOf<PkModuleInfo>() + 1),
            Module = new PkModuleInfo { Id = (byte)module, Type = 0, Active = 1 },
            ChannelCount = 0,
        };
        return HwMarshal.GetBytes(pk);
    }

    /// <summary>CHANNEL REFERENCE (헤더 + CHANNEL_INFO + REFERENCE).</summary>
    public static byte[] BuildChannelReference(int s, int r, int m, int c, ReferenceConfigInfo info)
    {
        var body = new PkChannelReference
        {
            Info  = new PkChannelInfo { Id = (byte)c, Type = 0, Active = 1 },
            Refer = ToReference(info),
        };
        byte[] bodyBytes   = HwMarshal.GetBytes(body);
        var    header      = PkHeader.Create(new PkOpcode(HwPacket.CMD_CFG, HwPacket.TYP_CHANNEL_CFG),
                                             new PkInfomation(s, r, m, c), bodyBytes.Length);
        byte[] headerBytes = HwMarshal.GetBytes(header);

        var buf = new byte[headerBytes.Length + bodyBytes.Length];
        Array.Copy(headerBytes, 0, buf, 0, headerBytes.Length);
        Array.Copy(bodyBytes, 0, buf, headerBytes.Length, bodyBytes.Length);
        return buf;
    }

    private static PkReference ToReference(ReferenceConfigInfo i) => new()
    {
        RotationDirection  = (byte)i.RotationDir,
        SignalPolarity     = (byte)i.SignalPolarity,
        ThresholdType      = (byte)i.ThresholdType,
        Simulated          = (byte)i.ReassignMode,
        AlternatedId       = (byte)Math.Max(0, i.AlternateId),
        Orientation        = (byte)i.Orientation,
        WaveformInterval   = (byte)i.WaveFormInterval,
        OrientationAngle   = (short)i.OrientationAngle,
        FluctuationRange   = (short)i.FluctuationRange,
        EventPerRevolution = i.EpRevolution,
        UnalteredTime      = i.UnalteredTime,
        SimulateSpeed      = i.Speed,
        Upper              = i.UpperLimit,
        Clamp              = i.ClampValue,
        ThresholdLevel     = (float)i.ThresholdLevel,
        HysteresisLevel    = (float)i.HysteresisLevel,
        Sensor = new PkSensor
        {
            Type = (byte)i.SignalType, Unit = 0, Icp = (byte)i.Icp, Power = (byte)i.ProximitorPower,
            Sensitivity = i.Sensitivity, Lower = (float)i.PowerLow, Upper = (float)i.PowerHigh,
        },
        AutoUpload = new PkAutoUpload
        {
            Mode = (short)i.UploadCondition, Time = (short)i.UploadTime,
            SuRpm = i.StartUpRpm, SdRpm = i.ShutDownRpm,
            SuMin = i.SuMin, SuMax = i.SuMax, SuDelta = i.SuDelta,
            SdMin = i.SdMin, SdMax = i.SdMax, SdDelta = i.SdDelta,
            SrBegin = i.SrBegin, SrEnd = i.SrEnd, SrDelta = i.SrDelta,
        },
    };
}
