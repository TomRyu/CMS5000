using System;

namespace CMS5000.Services.Hw;

/// <summary>
/// 원본 cPacket 포팅: 랙 통신 프로토콜 헤더(24바이트, Pack=1 Sequential, little-endian).
/// 레이아웃: STX | OPCODE(4) | INFOMATION(4) | TIME(8) | Length(4) | CRC(2) | ETX.
/// </summary>
public sealed class HwPacket
{
    public const byte STX = 0x02;
    public const byte ETX = 0x03;

    // Command
    public const byte CMD_HS      = 0x01;
    public const byte CMD_CFG     = 0x02;
    public const byte CMD_DATA    = 0x03;
    public const byte CMD_EVENT   = 0x04;
    public const byte CMD_STATUS  = 0x05;
    public const byte CMD_ACK     = 0x06;
    public const byte CMD_AJUST   = 0x07;
    public const byte CMD_NAK     = 0x15;
    public const byte CMD_CFG_REQ = 0x12;   // 요청 설정

    // Type
    public const byte TYP_CFG_COMM     = 0x01;
    public const byte TYP_CFG_FULLRACK = 0x02;
    public const byte TYP_COMM_CFG     = 0x01;
    public const byte TYP_RACK_CFG     = 0x02;
    public const byte TYP_MODULES_CFG  = 0x21;
    public const byte TYP_MODULE_CFG   = 0x22;
    public const byte TYP_CHANNELS_CFG = 0x31;
    public const byte TYP_CHANNEL_CFG  = 0x32;

    public const int HeaderSize = 24;

    public byte Command, Type, ReturnCommand, ReturnType;
    public byte StationId, RackId, ModuleId, ChannelId;
    public int  Length;

    public HwPacket() { }

    public HwPacket(byte command, byte type, byte returnCommand = 0, byte returnType = 0)
    {
        Command = command; Type = type; ReturnCommand = returnCommand; ReturnType = returnType;
    }

    public void SetInfo(int station, int rack, int module, int channel)
    {
        StationId = (byte)station; RackId = (byte)rack; ModuleId = (byte)module; ChannelId = (byte)channel;
    }

    /// <summary>헤더(24바이트) 직렬화. payloadLength 는 Length 필드에 기록.</summary>
    public byte[] BuildHeader(int payloadLength = 0)
    {
        var b = new byte[HeaderSize];
        var now = DateTime.Now;
        b[0]  = STX;
        b[1]  = Command; b[2] = Type; b[3] = ReturnCommand; b[4] = ReturnType;
        b[5]  = StationId; b[6] = RackId; b[7] = ModuleId; b[8] = ChannelId;
        // TIME (8): Year(int16) Month Day Hour Minute Second(int16)
        b[9]  = (byte)(now.Year & 0xFF); b[10] = (byte)((now.Year >> 8) & 0xFF);
        b[11] = (byte)now.Month; b[12] = (byte)now.Day; b[13] = (byte)now.Hour; b[14] = (byte)now.Minute;
        b[15] = (byte)(now.Second & 0xFF); b[16] = (byte)((now.Second >> 8) & 0xFF);
        // Length (int32 LE)
        b[17] = (byte)(payloadLength & 0xFF);
        b[18] = (byte)((payloadLength >> 8) & 0xFF);
        b[19] = (byte)((payloadLength >> 16) & 0xFF);
        b[20] = (byte)((payloadLength >> 24) & 0xFF);
        b[21] = 0; b[22] = 0;   // CRC (원본도 0으로 전송)
        b[23] = ETX;
        Length = payloadLength;
        return b;
    }

    /// <summary>수신 버퍼(24바이트 이상)에서 헤더 파싱.</summary>
    public static HwPacket? Parse(byte[] buf)
    {
        if (buf == null || buf.Length < HeaderSize || buf[0] != STX) return null;
        var p = new HwPacket
        {
            Command = buf[1], Type = buf[2], ReturnCommand = buf[3], ReturnType = buf[4],
            StationId = buf[5], RackId = buf[6], ModuleId = buf[7], ChannelId = buf[8],
            Length = buf[17] | (buf[18] << 8) | (buf[19] << 16) | (buf[20] << 24),
        };
        return p;
    }

    public string CommandText => Command switch
    {
        CMD_CFG     => "Config",
        CMD_CFG_REQ => "Config Request",
        CMD_ACK     => "ACK",
        CMD_NAK     => "NAK",
        CMD_AJUST   => "Adjust",
        _           => $"0x{Command:X2}",
    };
}
