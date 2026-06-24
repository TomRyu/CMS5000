using System;
using System.Collections.Generic;

namespace CMS5000.Services.Hw;

/// <summary>
/// UpLoad(Config Request) 응답으로 받은 RACK FULL CONFIG payload를 구조체로 역직렬화한다.
/// 원본 cConfigPK.CreateRackConfig 의 바디 레이아웃과 동일:
///   RACK_INFO(4) + ModuleCount(1)
///   + 모듈마다: MODULE_INFO(4) + ChannelCount(1) + 채널들(CHANNEL_IO 388B 반복)
/// (실측 21803B 샘플로 정확히 소비됨이 검증됨. 채널은 IO 타입 = CHANNEL_INFO+CHANNEL.)
/// </summary>
public static class HwRackConfigParser
{
    public sealed class ChannelResult
    {
        public PkChannelInfo Info;
        public PkChannel     Io;
    }

    public sealed class ModuleResult
    {
        public PkModuleInfo Info;
        public int ChannelCount;
        public List<ChannelResult> Channels = new();
    }

    public sealed class RackResult
    {
        public PkRackInfo Rack;
        public List<ModuleResult> Modules = new();
        public int  Consumed;            // 실제 소비한 바이트 수
        public bool Exact;               // Consumed == payload.Length (레이아웃 일치)
    }

    /// <summary>RACK FULL CONFIG 바디(payload) 파싱.</summary>
    public static RackResult Parse(byte[] body)
    {
        var res = new RackResult();
        int o = 0;
        int rackSize = HwMarshal.SizeOf<PkRackInfo>();   // 4
        int modSize  = HwMarshal.SizeOf<PkModuleInfo>(); // 4
        int ioSize   = HwMarshal.SizeOf<PkChannelIo>();  // 388

        res.Rack = HwMarshal.FromBytes<PkRackInfo>(body, o); o += rackSize;
        int moduleCount = body[o]; o += 1;

        for (int i = 0; i < moduleCount; i++)
        {
            if (o + modSize + 1 > body.Length) break;
            var mod = new ModuleResult { Info = HwMarshal.FromBytes<PkModuleInfo>(body, o) };
            o += modSize;
            mod.ChannelCount = body[o]; o += 1;

            for (int c = 0; c < mod.ChannelCount; c++)
            {
                if (o + ioSize > body.Length) break;   // 안전: 모자라면 중단(예: Relay 등 다른 채널 크기)
                var io = HwMarshal.FromBytes<PkChannelIo>(body, o); o += ioSize;
                mod.Channels.Add(new ChannelResult { Info = io.Info, Io = io.Io });
            }
            res.Modules.Add(mod);
        }

        res.Consumed = o;
        res.Exact    = (o == body.Length);
        return res;
    }
}
