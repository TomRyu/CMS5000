using System.Collections.Generic;
using CMS5000.Models;
using CMS5000.Services.Hw;
using Npgsql;

namespace CMS5000.Services;

public static class DeviceService
{
    // ────────────────────────────────────────────────────────
    //  스테이션
    // ────────────────────────────────────────────────────────

    public static async Task<List<DeviceStation>> GetStationsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT stationid::int, name, company, companyaddr, COALESCE(racklistenport, 0)
            FROM public.station ORDER BY stationid
            """;
        var list = new List<DeviceStation>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new DeviceStation
            {
                StationId      = r.GetInt32(0),
                Name           = r.GetString(1),
                Company        = r.IsDBNull(2) ? "" : r.GetString(2),
                CompanyAddr    = r.IsDBNull(3) ? "" : r.GetString(3),
                RackListenPort = r.GetInt32(4)
            });
        }
        return list;
    }

    public static async Task CreateStationAsync(int stationId, string name, string company, string addr, int listenPort)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.station(stationid,name,company,companyaddr,racklistenport) VALUES(@id,@n,@c,@a,@p)";
        cmd.Parameters.AddWithValue("id", stationId);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("c",  company);
        cmd.Parameters.AddWithValue("a",  addr);
        cmd.Parameters.AddWithValue("p",  listenPort);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateStationAsync(int stationId, string name, string company, string addr, int listenPort)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.station SET name=@n,company=@c,companyaddr=@a,racklistenport=@p WHERE stationid=@id";
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("c",  company);
        cmd.Parameters.AddWithValue("a",  addr);
        cmd.Parameters.AddWithValue("p",  listenPort);
        cmd.Parameters.AddWithValue("id", stationId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteStationAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.station WHERE stationid=@id";
        cmd.Parameters.AddWithValue("id", stationId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  RACK 트리 로드 (Rack → Module → Channel)
    // ────────────────────────────────────────────────────────

    public static async Task<List<DeviceTreeNode>> GetRackNodesAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        // 1) Rack
        var rackMap = new Dictionary<int, DeviceTreeNode>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT r.rackid, r.activity::int, r.name, COALESCE(r.location,''),
                       COALESCE(lt.ipaddr,''), COALESCE(lt.port, 0)
                FROM   public.rack r
                LEFT   JOIN public.tcpip lt ON r.localtcp = lt.tcpid
                WHERE  r.stationid = @sid
                ORDER  BY r.rackid
                """;
            cmd.Parameters.AddWithValue("sid", stationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var node = new DeviceTreeNode
                {
                    Kind      = NodeKind.Rack,
                    StationId = stationId,
                    RackId    = r.GetInt32(0),
                    Activity  = (byte)r.GetInt32(1),
                    Name      = r.GetString(2),
                    Location  = r.GetString(3),
                    LocalIp   = r.GetString(4),
                    LocalPort = r.GetInt32(5)
                };
                node.Info = $"{node.LocalIp}:{node.LocalPort}";
                rackMap[node.RackId] = node;
            }
        }

        // 2) Module
        var moduleMap = new Dictionary<(int, int), DeviceTreeNode>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT m.rackid::int, m.moduleid, m.activity::int, m.name,
                       COALESCE(m.moduletype::int, 0)::text
                FROM   public.module m
                WHERE  m.stationid = @sid
                ORDER  BY m.rackid, m.moduleid
                """;
            cmd.Parameters.AddWithValue("sid", stationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int rackId = r.GetInt32(0);
                var node   = new DeviceTreeNode
                {
                    Kind       = NodeKind.Module,
                    StationId  = stationId,
                    RackId     = rackId,
                    ModuleId   = r.GetInt32(1),
                    Activity   = (byte)r.GetInt32(2),
                    Name       = r.IsDBNull(3) ? "" : r.GetString(3),
                    ModuleType = r.GetString(4)
                };
                node.Info = node.ModuleType;
                if (rackMap.TryGetValue(rackId, out var rack))
                    rack.Children.Add(node);
                moduleMap[(rackId, node.ModuleId)] = node;
            }
        }

        // 3) Channel
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT gc.rackid::int, gc.moduleid, gc.channelid, gc.channel_index,
                       gc.activity::int, gc.name
                FROM   public.general_channel gc
                WHERE  gc.stationid = @sid
                ORDER  BY gc.rackid, gc.moduleid, gc.channelid
                """;
            cmd.Parameters.AddWithValue("sid", stationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int rackId   = r.GetInt32(0);
                int moduleId = r.GetInt32(1);
                var node     = new DeviceTreeNode
                {
                    Kind         = NodeKind.Channel,
                    StationId    = stationId,
                    RackId       = rackId,
                    ModuleId     = moduleId,
                    ChannelId    = r.GetInt32(2),
                    ChannelIndex = r.GetInt32(3),
                    Activity     = (byte)r.GetInt32(4),
                    Name         = r.IsDBNull(5) ? "" : r.GetString(5)
                };
                if (moduleMap.TryGetValue((rackId, moduleId), out var mod))
                    mod.Children.Add(node);
            }
        }

        return [.. rackMap.Values.OrderBy(n => n.RackId)];
    }

    /// <summary>
    /// UpLoad(RACK FULL CONFIG)로 읽은 인벤토리/활성 상태를 DB에 반영(UPSERT).
    /// rack.activity/waveforminterval 갱신 + module/general_channel 의 활성을 갱신하되,
    /// DB에 없는 모듈/채널은 새로 INSERT 한다(앱의 모듈추가와 동일한 컬럼만 사용 — 활성·이름·채널인덱스).
    /// moduletype/센서/스케일/알람 등 상세값은 건드리지 않는다(FK·상세 매핑 위험 회피).
    /// 활성: 기기 Active 가 0이 아니면 1(활성), 0이면 0(비활성)으로 매핑. 채널 INSERT 시 channel_index 는 전역 MAX+1.
    /// </summary>
    public static async Task<(bool rackUpdated, int modulesUpdated, int modulesInserted, int channelsUpdated, int channelsInserted)>
        SaveUploadedInventoryAsync(int stationId, int rackId, HwRackConfigParser.RackResult res)
    {
        bool rackUpdated = false;
        int modUpd = 0, modIns = 0, chUpd = 0, chIns = 0;

        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            // 채널 INSERT 용 전역 channel_index 시작값
            int nextIdx;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT COALESCE(MAX(channel_index)::int, 0) + 1 FROM public.general_channel";
                nextIdx = (int)(await cmd.ExecuteScalarAsync())!;
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE public.rack SET activity=@a, waveforminterval=@wf WHERE stationid=@sid AND rackid=@rid";
                cmd.Parameters.AddWithValue("a",   res.Rack.Active != 0 ? 1 : 0);
                cmd.Parameters.AddWithValue("wf",  (int)res.Rack.WaveFormInterval);
                cmd.Parameters.AddWithValue("sid", stationId);
                cmd.Parameters.AddWithValue("rid", rackId);
                rackUpdated = await cmd.ExecuteNonQueryAsync() > 0;
            }

            foreach (var m in res.Modules)
            {
                int act = m.Info.Active != 0 ? 1 : 0;
                int n;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE public.module SET activity=@a WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
                    cmd.Parameters.AddWithValue("a",   act);
                    cmd.Parameters.AddWithValue("sid", stationId);
                    cmd.Parameters.AddWithValue("rid", rackId);
                    cmd.Parameters.AddWithValue("mid", (int)m.Info.Id);
                    n = await cmd.ExecuteNonQueryAsync();
                }
                if (n > 0) modUpd++;
                else
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    // moduletype 은 NOT NULL 이므로 기기 보고 타입값을 그대로 사용.
                    cmd.CommandText = "INSERT INTO public.module(stationid, rackid, moduleid, name, activity, moduletype) VALUES(@sid, @rid, @mid, @name, @a, @t)";
                    cmd.Parameters.AddWithValue("sid",  stationId);
                    cmd.Parameters.AddWithValue("rid",  rackId);
                    cmd.Parameters.AddWithValue("mid",  (int)m.Info.Id);
                    cmd.Parameters.AddWithValue("name", $"M{m.Info.Id:D2}");
                    cmd.Parameters.AddWithValue("a",    act);
                    cmd.Parameters.AddWithValue("t",    (int)m.Info.Type);
                    await cmd.ExecuteNonQueryAsync();
                    modIns++;
                }

                foreach (var ch in m.Channels)
                {
                    int cact = ch.Info.Active != 0 ? 1 : 0;
                    int n2;
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "UPDATE public.general_channel SET activity=@a WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid=@cid";
                        cmd.Parameters.AddWithValue("a",   cact);
                        cmd.Parameters.AddWithValue("sid", stationId);
                        cmd.Parameters.AddWithValue("rid", rackId);
                        cmd.Parameters.AddWithValue("mid", (int)m.Info.Id);
                        cmd.Parameters.AddWithValue("cid", (int)ch.Info.Id);
                        n2 = await cmd.ExecuteNonQueryAsync();
                    }
                    if (n2 > 0) chUpd++;
                    else
                    {
                        await using var cmd = conn.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity) VALUES(@sid, @rid, @mid, @cid, @cidx, @name, @a)";
                        cmd.Parameters.AddWithValue("sid",  stationId);
                        cmd.Parameters.AddWithValue("rid",  rackId);
                        cmd.Parameters.AddWithValue("mid",  (int)m.Info.Id);
                        cmd.Parameters.AddWithValue("cid",  (int)ch.Info.Id);
                        cmd.Parameters.AddWithValue("cidx", nextIdx++);
                        cmd.Parameters.AddWithValue("name", $"CH{ch.Info.Id:D2}");
                        cmd.Parameters.AddWithValue("a",    cact);
                        await cmd.ExecuteNonQueryAsync();
                        chIns++;
                    }
                }
            }

            await tx.CommitAsync();
            return (rackUpdated, modUpd, modIns, chUpd, chIns);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ────────────────────────────────────────────────────────
    //  TRAIN 트리 로드 (Train → Component → Point)
    // ────────────────────────────────────────────────────────

    public static async Task<List<DeviceTreeNode>> GetTrainNodesAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        // 1) Train
        var trainMap = new Dictionary<int, DeviceTreeNode>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT trainid::int, activity::int, name FROM public.train WHERE stationid=@sid ORDER BY trainid";
            cmd.Parameters.AddWithValue("sid", stationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var node = new DeviceTreeNode
                {
                    Kind      = NodeKind.Train,
                    StationId = stationId,
                    TrainId   = r.GetInt32(0),
                    Activity  = (byte)r.GetInt32(1),
                    Name      = r.GetString(2)
                };
                trainMap[node.TrainId] = node;
            }
        }

        // 2) Component
        var compMap = new Dictionary<(int, int), DeviceTreeNode>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT trainid::int, componentid::int, activity::int, name FROM public.component WHERE stationid=@sid ORDER BY trainid, componentid";
            cmd.Parameters.AddWithValue("sid", stationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int trainId = r.GetInt32(0);
                var node    = new DeviceTreeNode
                {
                    Kind        = NodeKind.Component,
                    StationId   = stationId,
                    TrainId     = trainId,
                    ComponentId = r.GetInt32(1),
                    Activity    = (byte)r.GetInt32(2),
                    Name        = r.GetString(3)
                };
                if (trainMap.TryGetValue(trainId, out var train))
                    train.Children.Add(node);
                compMap[(trainId, node.ComponentId)] = node;
            }
        }

        // 3) Point
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT trainid::int, componentid::int, pointid::int, activity::int, name, COALESCE(assign::int, 0) FROM public.point WHERE stationid=@sid ORDER BY trainid, componentid, pointid";
            cmd.Parameters.AddWithValue("sid", stationId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int trainId = r.GetInt32(0);
                int compId  = r.GetInt32(1);
                var node    = new DeviceTreeNode
                {
                    Kind        = NodeKind.Point,
                    StationId   = stationId,
                    TrainId     = trainId,
                    ComponentId = compId,
                    PointId     = r.GetInt32(2),
                    Activity    = (byte)r.GetInt32(3),
                    Name        = r.GetString(4),
                    Assign      = r.GetInt32(5)
                };
                if (compMap.TryGetValue((trainId, compId), out var comp))
                    comp.Children.Add(node);
            }
        }

        return [.. trainMap.Values.OrderBy(n => n.TrainId)];
    }

    // ────────────────────────────────────────────────────────
    //  채널 목록 (Point 할당용)
    // ────────────────────────────────────────────────────────

    public static async Task<List<ChannelOption>> GetChannelOptionsAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT gc.channelid::int,
                   'R' || LPAD(gc.rackid::text,2,'0') ||
                   '-M' || LPAD(gc.moduleid::text,2,'0') ||
                   '-CH' || LPAD(gc.channelid::text,2,'0') ||
                   '  ' || COALESCE(gc.name,'') AS display
            FROM   public.general_channel gc
            WHERE  gc.stationid = @sid
            ORDER  BY gc.rackid, gc.moduleid, gc.channelid
            """;
        cmd.Parameters.AddWithValue("sid", stationId);
        var list = new List<ChannelOption>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ChannelOption { ChannelId = r.GetInt32(0), DisplayName = r.GetString(1).TrimEnd() });
        return list;
    }

    // ────────────────────────────────────────────────────────
    //  Module Config (frmModule)
    // ────────────────────────────────────────────────────────

    /// <summary>모듈의 설정시각 + 채널(Activity/Reference) 목록을 로드한다.</summary>
    public static async Task<ModuleConfigInfo> GetModuleConfigAsync(int stationId, int rackId, int moduleId)
    {
        var info = new ModuleConfigInfo();
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(configdate::text,'') FROM public.module WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
            cmd.Parameters.AddWithValue("sid", stationId);
            cmd.Parameters.AddWithValue("rid", rackId);
            cmd.Parameters.AddWithValue("mid", moduleId);
            var res = await cmd.ExecuteScalarAsync();
            info.ConfigDate = res as string ?? "";
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT channelid::int, COALESCE(name,''), activity::int,
                       COALESCE(referenceactivity,0), COALESCE(referenceid,0)
                FROM   public.general_channel
                WHERE  stationid=@sid AND rackid=@rid AND moduleid=@mid
                ORDER  BY channelid
                """;
            cmd.Parameters.AddWithValue("sid", stationId);
            cmd.Parameters.AddWithValue("rid", rackId);
            cmd.Parameters.AddWithValue("mid", moduleId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                info.Channels.Add(new ModuleChannelRow
                {
                    ChannelId   = r.GetInt32(0),
                    Name        = r.GetString(1),
                    Activity    = r.GetInt32(2) == 1,
                    ReferenceOn = r.GetInt32(3) == 1,
                    ReferenceId = r.GetInt32(4),
                });
        }
        return info;
    }

    /// <summary>채널 타입 콤보용: (channeltype, nicname) 목록.</summary>
    public static async Task<List<ChannelOption>> GetChannelTypeOptionsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT channeltype::int, COALESCE(name, nicname) FROM public.channel_type ORDER BY channeltype";
        var list = new List<ChannelOption>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ChannelOption { ChannelId = r.GetInt32(0), DisplayName = r.IsDBNull(1) ? "" : r.GetString(1) });
        return list;
    }

    /// <summary>
    /// Sensor Info 콤보용: 해당 채널타입에 속한 센서 이름 목록(원본과 동일하게
    /// channel_type_sensor 로 필터 → 같은 종류만 표시).
    /// </summary>
    public static async Task<List<string>> GetSensorNamesAsync(int channelType)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.name
            FROM   public.channel_type_sensor cts
            INNER  JOIN public.sensor s ON cts.sensorid = s.sensorid
            WHERE  cts.channeltype = @ct AND s.name IS NOT NULL
            ORDER  BY s.name
            """;
        cmd.Parameters.AddWithValue("ct", channelType);
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Reference 대상 콤보용: 해당 랙의 Interface 모듈(ModuleID=1) 채널 ID 목록.</summary>
    public static async Task<List<ChannelOption>> GetReferIdOptionsAsync(int stationId, int rackId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT channelid::int, 'CH' || LPAD(channelid::text,2,'0') || '  ' || COALESCE(name,'')
            FROM   public.general_channel
            WHERE  stationid=@sid AND rackid=@rid AND moduleid=1
            ORDER  BY channelid
            """;
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        var list = new List<ChannelOption>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ChannelOption { ChannelId = r.GetInt32(0), DisplayName = r.GetString(1).TrimEnd() });
        return list;
    }

    /// <summary>Module Config 저장: 모듈 기본 + 각 채널 Activity/Reference.</summary>
    public static async Task UpdateModuleConfigAsync(
        int stationId, int rackId, int moduleId,
        string name, int moduleType, bool activity, string configDate,
        IEnumerable<ModuleChannelRow> channels)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE public.module SET name=@n, moduletype=@t, activity=@a, configdate=@cd::timestamp WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
                cmd.Parameters.AddWithValue("n",  name);
                cmd.Parameters.AddWithValue("t",  moduleType);
                cmd.Parameters.AddWithValue("a",  activity ? 1 : 0);
                cmd.Parameters.AddWithValue("cd", string.IsNullOrWhiteSpace(configDate) ? (object)DBNull.Value : configDate);
                cmd.Parameters.AddWithValue("sid", stationId);
                cmd.Parameters.AddWithValue("rid", rackId);
                cmd.Parameters.AddWithValue("mid", moduleId);
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (var ch in channels)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE public.general_channel
                    SET    activity=@a, referenceactivity=@ra, referenceid=@ri
                    WHERE  stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid=@cid
                    """;
                cmd.Parameters.AddWithValue("a",   ch.Activity ? 1 : 0);
                cmd.Parameters.AddWithValue("ra",  ch.ReferenceOn ? 1 : 0);
                cmd.Parameters.AddWithValue("ri",  ch.ReferenceId);
                cmd.Parameters.AddWithValue("sid", stationId);
                cmd.Parameters.AddWithValue("rid", rackId);
                cmd.Parameters.AddWithValue("mid", moduleId);
                cmd.Parameters.AddWithValue("cid", ch.ChannelId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ────────────────────────────────────────────────────────
    //  Reference Config (frmReference) — 채널 신호설정
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 채널 Reference 신호설정을 로드한다. 원본과 동일하게
    /// general_channel + reference + autoupload + sensor 를 조인해서 읽는다.
    /// </summary>
    public static async Task<ReferenceConfigInfo> GetReferenceConfigAsync(int stationId, int rackId, int moduleId, int channelId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(gc.name::text,''), COALESCE(gc.channeltype::int,0), COALESCE(gc.activity::int,0),
                   COALESCE(r.waveforminterval,0), COALESCE(r.rotationdirection,0), COALESCE(r.signalpolarity,0),
                   COALESCE(r.thresholdtype,0), COALESCE(r.simulated,0), COALESCE(r.alternatedid,0),
                   COALESCE(r.orientation,0), COALESCE(r.orientationangle,0), COALESCE(r.fluctuationrange,0),
                   COALESCE(r.eventperrevolution,0), COALESCE(r.unalteredtime,0), COALESCE(r.upper,0),
                   COALESCE(r.clamp,0), COALESCE(r.simulatedspeed,0),
                   COALESCE(r.thresholdlevel,0), COALESCE(r.hysteresislevel,0),
                   COALESCE(au.mode,0), COALESCE(au.startuprpm,0), COALESCE(au.shutdownrpm,0),
                   COALESCE(au.startupmin,0), COALESCE(au.startupmax,0), COALESCE(au.startupdelta,0),
                   COALESCE(au.shutdownmin,0), COALESCE(au.shutdownmax,0), COALESCE(au.shutdowndelta,0),
                   COALESCE(au.slowrollbegin,0), COALESCE(au.slowrollend,0), COALESCE(au.slowrolldelta,0),
                   COALESCE(s.type,0), COALESCE(s.power,0), COALESCE(s.sensitivity,0),
                   COALESCE(s.unit::text,''), COALESCE(s.power_check_low,0), COALESCE(s.power_check_high,0),
                   COALESCE(s.name::text,'')
            FROM   public.general_channel gc
            LEFT   JOIN public.reference  r  ON gc.channel_index = r.channel_index
            LEFT   JOIN public.autoupload au ON r.referidx       = au.referidx
            LEFT   JOIN public.sensor     s  ON gc.sensorid      = s.sensorid
            WHERE  gc.stationid=@sid AND gc.rackid=@rid AND gc.moduleid=@mid AND gc.channelid=@cid
            """;
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        cmd.Parameters.AddWithValue("cid", channelId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return new ReferenceConfigInfo();

        int I(int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
        double D(int i) => r.IsDBNull(i) ? 0 : Convert.ToDouble(r.GetValue(i));
        string S(int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)) ?? "";
        int alt = I(8);

        return new ReferenceConfigInfo
        {
            Name             = S(0),
            ChannelType      = I(1),
            ActivityMode     = I(2),
            WaveFormInterval = I(3),
            RotationDir      = I(4),
            SignalPolarity   = I(5),
            ThresholdType    = I(6),
            ReassignMode     = I(7),                 // reference.simulated
            AlternateId      = alt > 0 ? alt - 1 : -1, // 1~4 → 콤보 인덱스 0~3 (0/없음 → 빈칸)
            Orientation      = I(9),
            OrientationAngle = I(10),
            FluctuationRange = I(11),
            EpRevolution     = I(12),
            UnalteredTime    = I(13),
            UpperLimit       = I(14),
            ClampValue       = I(15),
            Speed            = I(16),                // simulatedspeed
            ThresholdLevel   = D(17),
            HysteresisLevel  = D(18),
            UploadCondition  = I(19),                // autoupload.mode
            StartUpRpm       = I(20),
            ShutDownRpm      = I(21),
            SuMin            = I(22),
            SuMax            = I(23),
            SuDelta          = I(24),
            SdMin            = I(25),
            SdMax            = I(26),
            SdDelta          = I(27),
            SrBegin          = I(28),
            SrEnd            = I(29),
            SrDelta          = I(30),
            SignalType       = I(31),                // sensor.type
            ProximitorPower  = I(32),                // sensor.power
            Sensitivity      = I(33),
            SensorUnit       = S(34),
            PowerLow         = D(35),
            PowerHigh        = D(36),
            SensorName       = S(37),
        };
    }

    /// <summary>채널 Reference 신호설정을 저장(upsert)한다.</summary>
    /// <summary>
    /// 채널 Reference 신호설정을 원본 테이블(general_channel/reference/autoupload/sensor)에
    /// 키 기준으로 저장한다. 해당 행이 있는 경우에만 UPDATE 한다(없으면 건너뜀).
    /// </summary>
    public static async Task UpsertReferenceConfigAsync(int stationId, int rackId, int moduleId, int channelId, ReferenceConfigInfo c)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            // 키 조회: channel_index, sensorid
            int channelIndex = 0, sensorId = 0;
            await using (var q = conn.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = "SELECT channel_index::int, COALESCE(sensorid,0)::int FROM public.general_channel WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid=@cid";
                q.Parameters.AddWithValue("sid", stationId); q.Parameters.AddWithValue("rid", rackId);
                q.Parameters.AddWithValue("mid", moduleId);  q.Parameters.AddWithValue("cid", channelId);
                await using var rr = await q.ExecuteReaderAsync();
                if (await rr.ReadAsync()) { channelIndex = rr.GetInt32(0); sensorId = rr.GetInt32(1); }
            }
            if (channelIndex == 0) { await tx.RollbackAsync(); return; }

            // general_channel : name / activity / channeltype
            await using (var u = conn.CreateCommand())
            {
                u.Transaction = tx;
                u.CommandText = "UPDATE public.general_channel SET name=@n, activity=@a, channeltype=@ct WHERE channel_index=@ci";
                u.Parameters.AddWithValue("n", c.Name); u.Parameters.AddWithValue("a", c.ActivityMode);
                u.Parameters.AddWithValue("ct", c.ChannelType); u.Parameters.AddWithValue("ci", channelIndex);
                await u.ExecuteNonQueryAsync();
            }

            // reference (+ referidx 조회 → autoupload)
            int referIdx = 0;
            await using (var q = conn.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = "SELECT COALESCE(referidx,0)::int FROM public.reference WHERE channel_index=@ci";
                q.Parameters.AddWithValue("ci", channelIndex);
                var res = await q.ExecuteScalarAsync();
                referIdx = res is int v ? v : 0;
            }
            if (referIdx > 0 || await RowExistsAsync(conn, tx, "reference", "channel_index", channelIndex))
            {
                await using var u = conn.CreateCommand();
                u.Transaction = tx;
                u.CommandText = """
                    UPDATE public.reference SET
                        waveforminterval=@wfi, rotationdirection=@rdir, signalpolarity=@spol, thresholdtype=@ttype,
                        simulated=@rmode, alternatedid=@altid, orientation=@orient, orientationangle=@oang,
                        fluctuationrange=@fluct, eventperrevolution=@epr, unalteredtime=@unalt, upper=@upper,
                        clamp=@clamp, simulatedspeed=@speed, thresholdlevel=@tlevel, hysteresislevel=@hyst
                    WHERE channel_index=@ci
                    """;
                void P(string n, object v) => u.Parameters.AddWithValue(n, v);
                P("wfi", c.WaveFormInterval); P("rdir", c.RotationDir); P("spol", c.SignalPolarity); P("ttype", c.ThresholdType);
                P("rmode", c.ReassignMode); P("altid", c.AlternateId + 1); P("orient", c.Orientation); P("oang", c.OrientationAngle);
                P("fluct", c.FluctuationRange); P("epr", c.EpRevolution); P("unalt", c.UnalteredTime); P("upper", c.UpperLimit);
                P("clamp", c.ClampValue); P("speed", c.Speed); P("tlevel", c.ThresholdLevel); P("hyst", c.HysteresisLevel);
                P("ci", channelIndex);
                await u.ExecuteNonQueryAsync();
            }

            if (referIdx > 0)
            {
                await using var u = conn.CreateCommand();
                u.Transaction = tx;
                u.CommandText = """
                    UPDATE public.autoupload SET
                        mode=@mode, startuprpm=@surpm, shutdownrpm=@sdrpm,
                        startupmin=@sumin, startupmax=@sumax, startupdelta=@sudelta,
                        shutdownmin=@sdmin, shutdownmax=@sdmax, shutdowndelta=@sddelta,
                        slowrollbegin=@srb, slowrollend=@sre, slowrolldelta=@srd
                    WHERE referidx=@rx
                    """;
                void P(string n, object v) => u.Parameters.AddWithValue(n, v);
                P("mode", c.UploadCondition); P("surpm", c.StartUpRpm); P("sdrpm", c.ShutDownRpm);
                P("sumin", c.SuMin); P("sumax", c.SuMax); P("sudelta", c.SuDelta);
                P("sdmin", c.SdMin); P("sdmax", c.SdMax); P("sddelta", c.SdDelta);
                P("srb", c.SrBegin); P("sre", c.SrEnd); P("srd", c.SrDelta);
                P("rx", referIdx);
                await u.ExecuteNonQueryAsync();
            }

            // sensor
            if (sensorId > 0)
            {
                await using var u = conn.CreateCommand();
                u.Transaction = tx;
                u.CommandText = """
                    UPDATE public.sensor SET
                        type=@stype, power=@ppow, sensitivity=@sens, unit=@sunit,
                        power_check_low=@plow, power_check_high=@phigh, name=@sname
                    WHERE sensorid=@sx
                    """;
                void P(string n, object v) => u.Parameters.AddWithValue(n, v);
                P("stype", c.SignalType); P("ppow", c.ProximitorPower); P("sens", c.Sensitivity);
                P("sunit", string.IsNullOrEmpty(c.SensorUnit) ? (object)DBNull.Value : c.SensorUnit);
                P("plow", c.PowerLow); P("phigh", c.PowerHigh);
                P("sname", string.IsNullOrEmpty(c.SensorName) ? (object)DBNull.Value : c.SensorName);
                P("sx", sensorId);
                await u.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task<bool> RowExistsAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string table, string keyCol, int keyVal)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"SELECT 1 FROM public.{table} WHERE {keyCol}=@k LIMIT 1";
        cmd.Parameters.AddWithValue("k", keyVal);
        return await cmd.ExecuteScalarAsync() != null;
    }

    // ────────────────────────────────────────────────────────
    //  Assign Insert 전용 조회
    // ────────────────────────────────────────────────────────

    public static async Task<List<AssignableChannel>> GetAssignableChannelsAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT gc.rackid::int, gc.moduleid::int, gc.channelid::int,
                   COALESCE(gc.name,'') AS channel_name, gc.activity::int,
                   COALESCE(p.trainid::int,0), COALESCE(p.componentid::int,0),
                   COALESCE(p.pointid::int,0), COALESCE(p.name,'') AS point_name
            FROM   public.general_channel gc
            LEFT JOIN public.point p
                   ON p.stationid = gc.stationid AND p.assign = gc.channelid
            WHERE  gc.stationid = @sid
            ORDER  BY gc.rackid, gc.moduleid, gc.channelid
            """;
        cmd.Parameters.AddWithValue("sid", stationId);
        var list = new List<AssignableChannel>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignableChannel
            {
                RackId            = r.GetInt32(0),
                ModuleId          = r.GetInt32(1),
                ChannelId         = r.GetInt32(2),
                ChannelName       = r.GetString(3),
                ChannelActive     = r.GetInt32(4) == 1,
                AssignedTrainId   = r.GetInt32(5),
                AssignedCompId    = r.GetInt32(6),
                AssignedPointId   = r.GetInt32(7),
                AssignedPointName = r.GetString(8),
            });
        return list;
    }

    public static async Task<List<AssignablePoint>> GetAssignablePointsAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.trainid::int, p.componentid::int, p.pointid::int,
                   COALESCE(p.name,'') AS point_name, p.activity::int,
                   COALESCE(p.assign::int, 0) AS assign
            FROM   public.point p
            WHERE  p.stationid = @sid
            ORDER  BY p.trainid, p.componentid, p.pointid
            """;
        cmd.Parameters.AddWithValue("sid", stationId);
        var list = new List<AssignablePoint>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignablePoint
            {
                TrainId           = r.GetInt32(0),
                ComponentId       = r.GetInt32(1),
                PointId           = r.GetInt32(2),
                PointName         = r.GetString(3),
                PointActive       = r.GetInt32(4) == 1,
                AssignedChannelId = r.GetInt32(5),
            });
        return list;
    }

    /// <summary>
    /// 매칭 등록. 원본 frmAssign(P_INSERT_ASSIGN)과 동일하게 ASSIGN 테이블에 행을 넣고,
    /// general_channel/point 의 assign 플래그(0/1)를 1로 동기화한다. 키는 전역 유일 channel_index.
    /// </summary>
    public static async Task InsertAssignAsync(int stationId, int channelIndex, int trainId, int compId, int pointId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO public.assign(channel_index, stationid, trainid, componentid, pointid, createdate)
                VALUES(@ci, @sid, @tid, @cid, @pid, now());

                UPDATE public.general_channel SET assign = 1 WHERE stationid = @sid AND channel_index = @ci;
                UPDATE public.point           SET assign = 1
                 WHERE stationid = @sid AND trainid = @tid AND componentid = @cid AND pointid = @pid;
                """;
            cmd.Parameters.AddWithValue("ci",  channelIndex);
            cmd.Parameters.AddWithValue("sid", stationId);
            cmd.Parameters.AddWithValue("tid", trainId);
            cmd.Parameters.AddWithValue("cid", compId);
            cmd.Parameters.AddWithValue("pid", pointId);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    /// <summary>
    /// 매칭 해제. 포인트로 ASSIGN 행을 찾아 삭제(원본 P_DEL_ASSIGN)하고, 관련 채널/포인트의
    /// assign 플래그를 해제한다. point↔channel 은 1:1 이므로 삭제 후 플래그를 0으로 되돌린다.
    /// </summary>
    public static async Task DeleteAssignByPointAsync(int stationId, int trainId, int compId, int pointId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();

        int? channelIndex = null;
        await using (var del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = """
                DELETE FROM public.assign
                 WHERE stationid = @sid AND trainid = @tid AND componentid = @cid AND pointid = @pid
                RETURNING channel_index
                """;
            del.Parameters.AddWithValue("sid", stationId);
            del.Parameters.AddWithValue("tid", trainId);
            del.Parameters.AddWithValue("cid", compId);
            del.Parameters.AddWithValue("pid", pointId);
            var ci = await del.ExecuteScalarAsync();
            if (ci != null && ci != DBNull.Value) channelIndex = Convert.ToInt32(ci);
        }

        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = """
                UPDATE public.point SET assign = 0
                 WHERE stationid = @sid AND trainid = @tid AND componentid = @cid AND pointid = @pid;
                UPDATE public.general_channel SET assign = 0
                 WHERE stationid = @sid AND channel_index = @ci AND @ci > 0;
                """;
            upd.Parameters.AddWithValue("sid", stationId);
            upd.Parameters.AddWithValue("tid", trainId);
            upd.Parameters.AddWithValue("cid", compId);
            upd.Parameters.AddWithValue("pid", pointId);
            upd.Parameters.AddWithValue("ci",  channelIndex ?? 0);
            await upd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    // ────────────────────────────────────────────────────────
    //  ASSIGN(매칭) 화면 — 원본 frmAssign 캐스케이드 조회
    //  source of truth = ASSIGN 테이블(키=channel_index). point/general_channel.assign 은 0/1 플래그.
    // ────────────────────────────────────────────────────────

    public static async Task<List<AssignRackRow>> GetAssignRacksAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT rackid::int, COALESCE(name,''), COALESCE(activity::int,0) FROM public.rack WHERE stationid=@s ORDER BY rackid";
        cmd.Parameters.AddWithValue("s", stationId);
        var list = new List<AssignRackRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignRackRow { RackId = r.GetInt32(0), Name = r.GetString(1), Activity = r.GetInt32(2) });
        return list;
    }

    public static async Task<List<AssignModuleRow>> GetAssignModulesAsync(int stationId, int rackId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.moduleid::int, COALESCE(m.name,''), COALESCE(m.activity::int,0), COALESCE(mt.name,'')
            FROM   public.module m
            LEFT   JOIN public.module_type mt ON m.moduletype = mt.moduletype
            WHERE  m.stationid=@s AND m.rackid=@r
            ORDER  BY m.moduleid
            """;
        cmd.Parameters.AddWithValue("s", stationId);
        cmd.Parameters.AddWithValue("r", rackId);
        var list = new List<AssignModuleRow>();
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            list.Add(new AssignModuleRow { ModuleId = rd.GetInt32(0), Name = rd.GetString(1), Activity = rd.GetInt32(2), ModuleType = rd.GetString(3) });
        return list;
    }

    public static async Task<List<AssignChannelRow>> GetAssignChannelsAsync(int stationId, int rackId, int moduleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT gc.channelid::int, COALESCE(gc.name,''), COALESCE(gc.activity::int,0),
                   COALESCE(ct.name,''), gc.channel_index::int,
                   EXISTS(SELECT 1 FROM public.assign a WHERE a.stationid=gc.stationid AND a.channel_index=gc.channel_index) AS assigned
            FROM   public.general_channel gc
            LEFT   JOIN public.channel_type ct ON gc.channeltype = ct.channeltype
            WHERE  gc.stationid=@s AND gc.rackid=@r AND gc.moduleid=@m
            ORDER  BY gc.channelid
            """;
        cmd.Parameters.AddWithValue("s", stationId);
        cmd.Parameters.AddWithValue("r", rackId);
        cmd.Parameters.AddWithValue("m", moduleId);
        var list = new List<AssignChannelRow>();
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            list.Add(new AssignChannelRow
            {
                ChannelId    = rd.GetInt32(0),
                Name         = rd.GetString(1),
                Activity     = rd.GetInt32(2),
                ChannelType  = rd.GetString(3),
                ChannelIndex = rd.GetInt32(4),
                IsAssigned   = rd.GetBoolean(5),
            });
        return list;
    }

    public static async Task<List<AssignTrainRow>> GetAssignTrainsAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT trainid::int, COALESCE(name,''), COALESCE(activity::int,0) FROM public.train WHERE stationid=@s ORDER BY trainid";
        cmd.Parameters.AddWithValue("s", stationId);
        var list = new List<AssignTrainRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignTrainRow { TrainId = r.GetInt32(0), Name = r.GetString(1), Activity = r.GetInt32(2) });
        return list;
    }

    public static async Task<List<AssignComponentRow>> GetAssignComponentsAsync(int stationId, int trainId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT componentid::int, COALESCE(name,''), COALESCE(activity::int,0) FROM public.component WHERE stationid=@s AND trainid=@t ORDER BY componentid";
        cmd.Parameters.AddWithValue("s", stationId);
        cmd.Parameters.AddWithValue("t", trainId);
        var list = new List<AssignComponentRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignComponentRow { ComponentId = r.GetInt32(0), Name = r.GetString(1), Activity = r.GetInt32(2) });
        return list;
    }

    public static async Task<List<AssignPointRow>> GetAssignPointsAsync(int stationId, int trainId, int componentId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.pointid::int, COALESCE(p.name,''), COALESCE(p.activity::int,0),
                   EXISTS(SELECT 1 FROM public.assign a
                          WHERE a.stationid=p.stationid AND a.trainid=p.trainid
                            AND a.componentid=p.componentid AND a.pointid=p.pointid) AS assigned
            FROM   public.point p
            WHERE  p.stationid=@s AND p.trainid=@t AND p.componentid=@c
            ORDER  BY p.pointid
            """;
        cmd.Parameters.AddWithValue("s", stationId);
        cmd.Parameters.AddWithValue("t", trainId);
        cmd.Parameters.AddWithValue("c", componentId);
        var list = new List<AssignPointRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignPointRow { PointId = r.GetInt32(0), Name = r.GetString(1), Activity = r.GetInt32(2), IsAssigned = r.GetBoolean(3) });
        return list;
    }

    /// <summary>ASSIGN 목록(원본 FpSprAssign): ASSIGN 테이블을 채널(channel_index)·포인트와 조인.</summary>
    public static async Task<List<AssignListRow>> GetAssignListAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT gc.stationid::int, gc.rackid::int, gc.moduleid::int, gc.channelid::int, COALESCE(gc.name,''),
                   p.trainid::int, p.componentid::int, p.pointid::int, COALESCE(p.name,'')
            FROM   public.assign a
            JOIN   public.general_channel gc ON gc.stationid = a.stationid AND gc.channel_index = a.channel_index
            JOIN   public.point p            ON p.stationid  = a.stationid AND p.trainid = a.trainid
                                            AND p.componentid = a.componentid AND p.pointid = a.pointid
            WHERE  a.stationid=@s
            ORDER  BY gc.rackid, gc.moduleid, gc.channelid
            """;
        cmd.Parameters.AddWithValue("s", stationId);
        var list = new List<AssignListRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AssignListRow
            {
                StationId   = r.GetInt32(0),
                RackId      = r.GetInt32(1),
                ModuleId    = r.GetInt32(2),
                ChannelId   = r.GetInt32(3),
                ChannelName = r.GetString(4),
                TrainId     = r.GetInt32(5),
                ComponentId = r.GetInt32(6),
                PointId     = r.GetInt32(7),
                PointName   = r.GetString(8),
            });
        return list;
    }

    // ────────────────────────────────────────────────────────
    //  RACK CRUD
    // ────────────────────────────────────────────────────────

    public static async Task<int> NextRackIdAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(rackid)::int, 0) + 1 FROM public.rack WHERE stationid=@sid";
        cmd.Parameters.AddWithValue("sid", stationId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task CreateRackAsync(int stationId, int rackId, string name, string location)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.rack(stationid, rackid, name, location, activity)
            VALUES(@sid, @rid, @name, @loc, 1)
            """;
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("rid",  rackId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("loc",  string.IsNullOrEmpty(location) ? DBNull.Value : (object)location);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task CreateRackFullAsync(
        int stationId, int rackId, bool activity,
        string name, string location,
        string localIp, int localPort,
        int serialPort, int baudRate, int dataBit, int parityBit, int stopBit,
        int waveformInterval, bool trend, int staticTrend, int dynamicTrend,
        int modbusModeIndex, string modbusIp, int modbusPort,
        int modSerialPort, int modBaudRate, int modDataBit, int modParityBit, int modStopBit,
        string serverIp, int serverPort)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            int? localTcpId = null;
            if (!string.IsNullOrWhiteSpace(localIp))
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction  = tx;
                cmd.CommandText  = """
                    INSERT INTO public.tcpip(tcpid, ipaddr, port)
                    VALUES((SELECT COALESCE(MAX(tcpid),0)+1 FROM public.tcpip), @ip, @port)
                    RETURNING tcpid
                    """;
                cmd.Parameters.AddWithValue("ip",   localIp);
                cmd.Parameters.AddWithValue("port", localPort);
                localTcpId = (int)(await cmd.ExecuteScalarAsync())!;
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.rack(stationid, rackid, name, location, activity, localtcp)
                    VALUES(@sid, @rid, @name, @loc, @act, @ltcp)
                    """;
                cmd.Parameters.AddWithValue("sid",  stationId);
                cmd.Parameters.AddWithValue("rid",  rackId);
                cmd.Parameters.AddWithValue("name", name);
                cmd.Parameters.AddWithValue("loc",  string.IsNullOrEmpty(location) ? DBNull.Value : (object)location);
                cmd.Parameters.AddWithValue("act",  activity ? 1 : 0);
                cmd.Parameters.AddWithValue("ltcp", localTcpId.HasValue ? (object)localTcpId.Value : DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public static async Task CopyRackAsync(int stationId, int srcRackId, int destRackId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            // 모듈 복사
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction  = tx;
                cmd.CommandText  = """
                    INSERT INTO public.module(stationid, rackid, moduleid, name, activity, moduletype)
                    SELECT stationid, @dest, moduleid, name, activity, moduletype
                    FROM   public.module
                    WHERE  stationid = @sid AND rackid = @src
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("sid",  stationId);
                cmd.Parameters.AddWithValue("src",  srcRackId);
                cmd.Parameters.AddWithValue("dest", destRackId);
                await cmd.ExecuteNonQueryAsync();
            }
            // 채널 복사
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
                    SELECT stationid, @dest, moduleid, channelid, channel_index, name, activity
                    FROM   public.general_channel
                    WHERE  stationid = @sid AND rackid = @src
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("sid",  stationId);
                cmd.Parameters.AddWithValue("src",  srcRackId);
                cmd.Parameters.AddWithValue("dest", destRackId);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>MODULE 복사: 모듈 + 채널을 같은 랙의 대상 모듈 ID로 복사.</summary>
    public static async Task CopyModuleAsync(int stationId, int rackId, int srcModuleId, int destModuleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.module(stationid, rackid, moduleid, name, activity, moduletype)
                    SELECT stationid, rackid, @dm, name, activity, moduletype
                    FROM   public.module WHERE stationid=@s AND rackid=@r AND moduleid=@sm
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("s", stationId); cmd.Parameters.AddWithValue("r", rackId);
                cmd.Parameters.AddWithValue("sm", srcModuleId); cmd.Parameters.AddWithValue("dm", destModuleId);
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
                    SELECT stationid, rackid, @dm, channelid, channel_index, name, activity
                    FROM   public.general_channel WHERE stationid=@s AND rackid=@r AND moduleid=@sm
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("s", stationId); cmd.Parameters.AddWithValue("r", rackId);
                cmd.Parameters.AddWithValue("sm", srcModuleId); cmd.Parameters.AddWithValue("dm", destModuleId);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>CHANNEL 복사: 같은 모듈의 대상 채널 ID로 복사(새 channel_index 부여).</summary>
    public static async Task CopyChannelAsync(int stationId, int rackId, int moduleId, int srcChannelId, int destChannelId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
            SELECT stationid, rackid, moduleid, @dc,
                   (SELECT COALESCE(MAX(channel_index),0)+1 FROM public.general_channel),
                   name, activity
            FROM   public.general_channel
            WHERE  stationid=@s AND rackid=@r AND moduleid=@m AND channelid=@sc
            ON CONFLICT DO NOTHING
            """;
        cmd.Parameters.AddWithValue("s", stationId); cmd.Parameters.AddWithValue("r", rackId);
        cmd.Parameters.AddWithValue("m", moduleId); cmd.Parameters.AddWithValue("sc", srcChannelId);
        cmd.Parameters.AddWithValue("dc", destChannelId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>해당 스테이션의 RACK ID 목록.</summary>
    public static async Task<List<int>> GetRackIdsAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT rackid::int FROM public.rack WHERE stationid=@sid ORDER BY rackid";
        cmd.Parameters.AddWithValue("sid", stationId);
        var list = new List<int>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetInt32(0));
        return list;
    }

    /// <summary>해당 스테이션/랙의 MODULE ID 목록. excludeModuleId(>0)는 제외(frmCopy 캐스케이드).</summary>
    public static async Task<List<int>> GetModuleIdsAsync(int stationId, int rackId, int excludeModuleId = -1)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = excludeModuleId > 0
            ? "SELECT moduleid::int FROM public.module WHERE stationid=@sid AND rackid=@rid AND moduleid<>@ex ORDER BY moduleid"
            : "SELECT moduleid::int FROM public.module WHERE stationid=@sid AND rackid=@rid ORDER BY moduleid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        if (excludeModuleId > 0) cmd.Parameters.AddWithValue("ex", excludeModuleId);
        var list = new List<int>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetInt32(0));
        return list;
    }

    /// <summary>해당 모듈의 CHANNEL ID 목록. excludeChannelId(>0)는 제외(frmCopy 캐스케이드).</summary>
    public static async Task<List<int>> GetChannelIdsAsync(int stationId, int rackId, int moduleId, int excludeChannelId = -1)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = excludeChannelId > 0
            ? "SELECT channelid::int FROM public.general_channel WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid<>@ex ORDER BY channelid"
            : "SELECT channelid::int FROM public.general_channel WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid ORDER BY channelid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        if (excludeChannelId > 0) cmd.Parameters.AddWithValue("ex", excludeChannelId);
        var list = new List<int>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetInt32(0));
        return list;
    }

    /// <summary>
    /// MODULE 교차 복사(frmCopy ModuleProcess): 원본 스테이션/랙/모듈 → 대상 스테이션/랙/모듈.
    /// 대상 모듈이 없으면 생성하고, 원본 채널을 새 channel_index로 복사한다(이미 있는 채널은 유지).
    /// </summary>
    public static async Task CopyModuleAsync(int srcStation, int srcRack, int srcModule,
                                             int destStation, int destRack, int destModule)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.module(stationid, rackid, moduleid, name, activity, moduletype)
                    SELECT @ds, @dr, @dm, name, activity, moduletype
                    FROM   public.module WHERE stationid=@ss AND rackid=@sr AND moduleid=@sm
                    ON CONFLICT DO NOTHING
                    """;
                AddCopyParams(cmd, srcStation, srcRack, srcModule, destStation, destRack, destModule);
                await cmd.ExecuteNonQueryAsync();
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
                    SELECT @ds, @dr, @dm, channelid,
                           (SELECT COALESCE(MAX(channel_index),0) FROM public.general_channel)
                             + ROW_NUMBER() OVER (ORDER BY channelid),
                           name, activity
                    FROM   public.general_channel WHERE stationid=@ss AND rackid=@sr AND moduleid=@sm
                    ON CONFLICT DO NOTHING
                    """;
                AddCopyParams(cmd, srcStation, srcRack, srcModule, destStation, destRack, destModule);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>
    /// CHANNEL 교차 복사(frmCopy ChannelProcess): 원본 → 대상 위치의 채널로 복사(새 channel_index).
    /// 대상 채널이 이미 있으면 교체한다.
    /// </summary>
    public static async Task CopyChannelAsync(int srcStation, int srcRack, int srcModule, int srcChannel,
                                              int destStation, int destRack, int destModule, int destChannel)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM public.general_channel WHERE stationid=@ds AND rackid=@dr AND moduleid=@dm AND channelid=@dc";
                del.Parameters.AddWithValue("ds", destStation); del.Parameters.AddWithValue("dr", destRack);
                del.Parameters.AddWithValue("dm", destModule);  del.Parameters.AddWithValue("dc", destChannel);
                await del.ExecuteNonQueryAsync();
            }
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
                    SELECT @ds, @dr, @dm, @dc,
                           (SELECT COALESCE(MAX(channel_index),0)+1 FROM public.general_channel),
                           name, activity
                    FROM   public.general_channel
                    WHERE  stationid=@ss AND rackid=@sr AND moduleid=@sm AND channelid=@sc
                    """;
                cmd.Parameters.AddWithValue("ss", srcStation); cmd.Parameters.AddWithValue("sr", srcRack);
                cmd.Parameters.AddWithValue("sm", srcModule);  cmd.Parameters.AddWithValue("sc", srcChannel);
                cmd.Parameters.AddWithValue("ds", destStation); cmd.Parameters.AddWithValue("dr", destRack);
                cmd.Parameters.AddWithValue("dm", destModule);  cmd.Parameters.AddWithValue("dc", destChannel);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    private static void AddCopyParams(NpgsqlCommand cmd, int ss, int sr, int sm, int ds, int dr, int dm)
    {
        cmd.Parameters.AddWithValue("ss", ss); cmd.Parameters.AddWithValue("sr", sr); cmd.Parameters.AddWithValue("sm", sm);
        cmd.Parameters.AddWithValue("ds", ds); cmd.Parameters.AddWithValue("dr", dr); cmd.Parameters.AddWithValue("dm", dm);
    }

    // ────────────────────────────────────────────────────────
    //  RELAY 채널 로직 설정 (원본 frmRelay / RELAY · RELAY_LOGIC)
    // ────────────────────────────────────────────────────────

    private static async Task<bool> ModuleExistsAsync(int sid, int rid, int mid)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM public.module WHERE stationid=@s AND rackid=@r AND moduleid=@m";
        cmd.Parameters.AddWithValue("s", sid); cmd.Parameters.AddWithValue("r", rid); cmd.Parameters.AddWithValue("m", mid);
        return await cmd.ExecuteScalarAsync() is not null;
    }

    /// <summary>채널의 channel_index. 없으면 null.</summary>
    public static async Task<int?> GetChannelIndexAsync(int sid, int rid, int mid, int cid)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT channel_index::int FROM public.general_channel WHERE stationid=@s AND rackid=@r AND moduleid=@m AND channelid=@c";
        cmd.Parameters.AddWithValue("s", sid); cmd.Parameters.AddWithValue("r", rid);
        cmd.Parameters.AddWithValue("m", mid); cmd.Parameters.AddWithValue("c", cid);
        var o = await cmd.ExecuteScalarAsync();
        return o is null or DBNull ? null : Convert.ToInt32(o);
    }

    /// <summary>해당 스테이션/랙의 GENERAL_CHANNEL 에 존재하는 모듈 ID 목록(원본 릴레이 로직 모듈 콤보).</summary>
    public static async Task<List<int>> GetDistinctChannelModuleIdsAsync(int sid, int rid)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT moduleid::int FROM public.general_channel WHERE stationid=@s AND rackid=@r ORDER BY 1";
        cmd.Parameters.AddWithValue("s", sid); cmd.Parameters.AddWithValue("r", rid);
        var list = new List<int>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetInt32(0));
        return list;
    }

    /// <summary>
    /// 릴레이 로직을 붙일 채널이 존재하도록 보장. 모듈·채널이 없으면 생성하고 channel_index 를 반환.
    /// </summary>
    public static async Task<int> EnsureRelayChannelAsync(int sid, int rid, int mid, int cid,
                                                          string channelName, string moduleName, int? moduleType)
    {
        if (!await ModuleExistsAsync(sid, rid, mid))
        {
            // module.moduletype 은 NOT NULL → INSERT 시 함께 지정
            await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO public.module(stationid, rackid, moduleid, name, activity, moduletype) VALUES(@s, @r, @m, @n, 1, @t)";
            cmd.Parameters.AddWithValue("s", sid);
            cmd.Parameters.AddWithValue("r", rid);
            cmd.Parameters.AddWithValue("m", mid);
            cmd.Parameters.AddWithValue("n", string.IsNullOrWhiteSpace(moduleName) ? $"RELAY{mid:D2}" : moduleName.Trim());
            cmd.Parameters.AddWithValue("t", moduleType ?? 0);
            await cmd.ExecuteNonQueryAsync();
        }

        int? idx = await GetChannelIndexAsync(sid, rid, mid, cid);
        if (idx is null)
        {
            int newIdx = await NextChannelIndexAsync();
            await CreateChannelAsync(sid, rid, mid, cid, newIdx,
                string.IsNullOrWhiteSpace(channelName) ? $"Relay{cid:D2}" : channelName.Trim());
            idx = newIdx;
        }
        return idx.Value;
    }

    private static string AlertDangerText(int v) => v == 1 ? "Danger" : "Alert";
    private static int    AlertDangerVal(string t) => t == "Danger" ? 1 : 0;
    private static string AndOrEndText(int v) => v switch { 1 => "Or", 2 => "End", _ => "And" };
    private static int    AndOrEndVal(string t) => t switch { "Or" => 1, "End" => 2, _ => 0 };

    /// <summary>릴레이 설정 로드. relay 행이 없으면 빈 설정(Mode=0/AndVoting=0).</summary>
    public static async Task<RelayConfigInfo> GetRelayConfigAsync(int channelIndex)
    {
        var info = new RelayConfigInfo();
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        int relayIdx;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT relayidx, COALESCE(mode,0), COALESCE(andvoting,0) FROM public.relay WHERE channel_index=@i ORDER BY relayidx LIMIT 1";
            cmd.Parameters.AddWithValue("i", channelIndex);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return info;   // relay 미설정
            relayIdx       = r.GetInt32(0);
            info.Mode      = Convert.ToInt32(r.GetValue(1));
            info.AndVoting = Convert.ToInt32(r.GetValue(2));
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT sequence, moduleid, channelid, COALESCE(alertdanger,0), COALESCE(andorend,0) FROM public.relay_logic WHERE relayidx=@r ORDER BY sequence";
            cmd.Parameters.AddWithValue("r", relayIdx);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                info.Logic.Add(new RelayLogicRow
                {
                    Sequence    = Convert.ToInt32(r.GetValue(0)),
                    ModuleId    = Convert.ToInt32(r.GetValue(1)),
                    ChannelId   = Convert.ToInt32(r.GetValue(2)),
                    AlertDanger = AlertDangerText(Convert.ToInt32(r.GetValue(3))),
                    AndOrEnd    = AndOrEndText(Convert.ToInt32(r.GetValue(4))),
                });
        }
        return info;
    }

    /// <summary>릴레이 설정 저장(원본 RelayModify): relay upsert + relay_logic 전체 재작성.</summary>
    public static async Task SaveRelayConfigAsync(int channelIndex, int mode, int andVoting, IEnumerable<RelayLogicRow> rows)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            int relayIdx;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT relayidx FROM public.relay WHERE channel_index=@i ORDER BY relayidx LIMIT 1";
                cmd.Parameters.AddWithValue("i", channelIndex);
                var o = await cmd.ExecuteScalarAsync();
                if (o is null or DBNull)
                {
                    await using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = "INSERT INTO public.relay(relayidx, channel_index, mode, andvoting) VALUES((SELECT COALESCE(MAX(relayidx),0)+1 FROM public.relay), @i, @m, @a) RETURNING relayidx";
                    ins.Parameters.AddWithValue("i", channelIndex);
                    ins.Parameters.AddWithValue("m", mode);
                    ins.Parameters.AddWithValue("a", andVoting);
                    relayIdx = Convert.ToInt32(await ins.ExecuteScalarAsync());
                }
                else
                {
                    relayIdx = Convert.ToInt32(o);
                    await using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = "UPDATE public.relay SET mode=@m, andvoting=@a WHERE relayidx=@r";
                    upd.Parameters.AddWithValue("m", mode);
                    upd.Parameters.AddWithValue("a", andVoting);
                    upd.Parameters.AddWithValue("r", relayIdx);
                    await upd.ExecuteNonQueryAsync();
                }
            }

            await using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM public.relay_logic WHERE relayidx=@r";
                del.Parameters.AddWithValue("r", relayIdx);
                await del.ExecuteNonQueryAsync();
            }

            foreach (var row in rows)
            {
                await using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO public.relay_logic(relayidx, sequence, moduleid, channelid, alertdanger, andorend) VALUES(@r, @s, @m, @c, @ad, @ae)";
                ins.Parameters.AddWithValue("r",  relayIdx);
                ins.Parameters.AddWithValue("s",  row.Sequence);
                ins.Parameters.AddWithValue("m",  row.ModuleId);
                ins.Parameters.AddWithValue("c",  row.ChannelId);
                ins.Parameters.AddWithValue("ad", AlertDangerVal(row.AlertDanger));
                ins.Parameters.AddWithValue("ae", AndOrEndVal(row.AndOrEnd));
                await ins.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    /// <summary>RACK 복사(원본 frmCopy RACK COPY): 대상 스테이션/랙으로 rack + 모듈 + 채널을 복사한다.</summary>
    public static async Task CopyRackAsync(int srcStation, int srcRack, int destStation, int destRack)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            // rack 기본 행
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.rack(stationid, rackid, name, location, activity,
                                            waveforminterval, trend, statictrend, dynamictrend, modbusmode)
                    SELECT @ds, @dr, name, location, activity,
                           COALESCE(waveforminterval,0), COALESCE(trend,0), COALESCE(statictrend,10),
                           COALESCE(dynamictrend,10), COALESCE(modbusmode,0)
                    FROM   public.rack WHERE stationid=@ss AND rackid=@sr
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("ss", srcStation); cmd.Parameters.AddWithValue("sr", srcRack);
                cmd.Parameters.AddWithValue("ds", destStation); cmd.Parameters.AddWithValue("dr", destRack);
                await cmd.ExecuteNonQueryAsync();
            }
            // 모듈
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.module(stationid, rackid, moduleid, name, activity, moduletype)
                    SELECT @ds, @dr, moduleid, name, activity, moduletype
                    FROM   public.module WHERE stationid=@ss AND rackid=@sr
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("ss", srcStation); cmd.Parameters.AddWithValue("sr", srcRack);
                cmd.Parameters.AddWithValue("ds", destStation); cmd.Parameters.AddWithValue("dr", destRack);
                await cmd.ExecuteNonQueryAsync();
            }
            // 채널
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
                    SELECT @ds, @dr, moduleid, channelid, channel_index, name, activity
                    FROM   public.general_channel WHERE stationid=@ss AND rackid=@sr
                    ON CONFLICT DO NOTHING
                    """;
                cmd.Parameters.AddWithValue("ss", srcStation); cmd.Parameters.AddWithValue("sr", srcRack);
                cmd.Parameters.AddWithValue("ds", destStation); cmd.Parameters.AddWithValue("dr", destRack);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public static async Task DeleteRackAsync(int stationId, int rackId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            foreach (var sql in new[]
            {
                "DELETE FROM public.general_channel WHERE stationid=@sid AND rackid=@rid",
                "DELETE FROM public.module           WHERE stationid=@sid AND rackid=@rid",
                "DELETE FROM public.rack             WHERE stationid=@sid AND rackid=@rid"
            })
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("sid", stationId);
                cmd.Parameters.AddWithValue("rid", rackId);
                await cmd.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ────────────────────────────────────────────────────────
    //  MODULE CRUD
    // ────────────────────────────────────────────────────────

    public static async Task<int> NextModuleIdAsync(int stationId, int rackId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(moduleid)::int, 0) + 1 FROM public.module WHERE stationid=@sid AND rackid=@rid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task CreateModuleAsync(int stationId, int rackId, int moduleId, string name)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.module(stationid, rackid, moduleid, name, activity)
            VALUES(@sid, @rid, @mid, @name, 1)
            """;
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("rid",  rackId);
        cmd.Parameters.AddWithValue("mid",  moduleId);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task SetModuleTypeAsync(int stationId, int rackId, int moduleId, int moduleType)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.module SET moduletype=@t WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
        cmd.Parameters.AddWithValue("t",   moduleType);
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateModuleNameAsync(int stationId, int rackId, int moduleId, string name)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.module SET name=@n WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
        cmd.Parameters.AddWithValue("n",   name);
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>해당 모듈에 속한 GENERAL_CHANNEL 개수(원본 모듈 삭제 가드용).</summary>
    public static async Task<int> ModuleChannelCountAsync(int stationId, int rackId, int moduleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(channelid) FROM public.general_channel WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// 원본 frmMain CMenu_Module_Del 와 동일: 채널이 없을 때만 MODULE 행만 삭제(하위 캐스케이드 없음).
    /// 호출 전 <see cref="ModuleChannelCountAsync"/> 로 가드한다.
    /// </summary>
    public static async Task DeleteModuleAsync(int stationId, int rackId, int moduleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.module WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  CHANNEL CRUD
    // ────────────────────────────────────────────────────────

    public static async Task<int> NextChannelIdAsync(int stationId, int rackId, int moduleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(channelid)::int, 0) + 1 FROM public.general_channel WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>전역 다음 channel_index (MAX+1).</summary>
    public static async Task<int> NextChannelIndexAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(channel_index)::int, 0) + 1 FROM public.general_channel";
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task CreateChannelAsync(int stationId, int rackId, int moduleId, int channelId, int channelIndex, string name)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.general_channel(stationid, rackid, moduleid, channelid, channel_index, name, activity)
            VALUES(@sid, @rid, @mid, @cid, @cidx, @name, 1)
            """;
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("rid",  rackId);
        cmd.Parameters.AddWithValue("mid",  moduleId);
        cmd.Parameters.AddWithValue("cid",  channelId);
        cmd.Parameters.AddWithValue("cidx", channelIndex);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateChannelAsync(int stationId, int rackId, int moduleId, int channelId, string name)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.general_channel SET name=@name WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid=@cid";
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("rid",  rackId);
        cmd.Parameters.AddWithValue("mid",  moduleId);
        cmd.Parameters.AddWithValue("cid",  channelId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task SetChannelActivityAsync(int stationId, int rackId, int moduleId, int channelId, byte activity)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.general_channel SET activity=@a WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid=@cid";
        cmd.Parameters.AddWithValue("a",   (int)activity);
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        cmd.Parameters.AddWithValue("cid", channelId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>테이블이 존재할 때만 channel_index 기준 행 개수. 없으면 0(원본 가드 쿼리 호환).</summary>
    private static async Task<int> CountByChannelIndexIfExistsAsync(NpgsqlConnection conn, string table, int channelIndex)
    {
        await using (var chk = conn.CreateCommand())
        {
            chk.CommandText = "SELECT to_regclass(@t)";
            chk.Parameters.AddWithValue("t", "public." + table);
            var reg = await chk.ExecuteScalarAsync();
            if (reg is null or DBNull) return 0;
        }
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM public.{table} WHERE channel_index=@idx";
        cmd.Parameters.AddWithValue("idx", channelIndex);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// 원본 CMenu_Channel_Del 가드: TREND_STATIC / EVENT_LIST / STATUS_LIST 잔여 데이터 개수.
    /// 해당 테이블이 없으면 0으로 간주.
    /// </summary>
    public static async Task<(int trend, int evt, int status)> ChannelDataCountsAsync(int channelIndex)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        int trend  = await CountByChannelIndexIfExistsAsync(conn, "trend_static", channelIndex);
        int evt    = await CountByChannelIndexIfExistsAsync(conn, "event_list",   channelIndex);
        int status = await CountByChannelIndexIfExistsAsync(conn, "status_list",  channelIndex);
        return (trend, evt, status);
    }

    public static async Task DeleteChannelAsync(int stationId, int rackId, int moduleId, int channelId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.general_channel WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid AND channelid=@cid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        cmd.Parameters.AddWithValue("cid", channelId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  RACK 활성/비활성 토글
    // ────────────────────────────────────────────────────────

    public static async Task UpdateRackInfoAsync(int stationId, int rackId, string name, string location)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.rack SET name=@n, location=@l WHERE stationid=@sid AND rackid=@rid";
        cmd.Parameters.AddWithValue("n",   name);
        cmd.Parameters.AddWithValue("l",   location);
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task SetRackActivityAsync(int stationId, int rackId, byte activity)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.rack SET activity=@a WHERE stationid=@sid AND rackid=@rid";
        cmd.Parameters.AddWithValue("a",   (int)activity);
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// RACK Modify 다이얼로그용 전체 설정값 로드. rack + tcpip/serial(local·server·modbus) 조인.
    /// </summary>
    public static async Task<RackFullInfo?> GetRackFullAsync(int stationId, int rackId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT r.name, COALESCE(r.location,''), r.activity::int,
                   COALESCE(r.waveforminterval,0), COALESCE(r.trend,0),
                   COALESCE(r.statictrend,0), COALESCE(r.dynamictrend,0),
                   COALESCE(r.modbusmode,0),
                   COALESCE(lt.ipaddr,''), COALESCE(lt.port,0),
                   COALESCE(ls.port,0), COALESCE(ls.baudrate,0), COALESCE(ls.databits,0), COALESCE(ls.paritybit,0), COALESCE(ls.stopbit,0),
                   COALESCE(st.ipaddr,''), COALESCE(st.port,0),
                   COALESCE(mt.ipaddr,''), COALESCE(mt.port,0),
                   COALESCE(ms.port,0), COALESCE(ms.baudrate,0), COALESCE(ms.databits,0), COALESCE(ms.paritybit,0), COALESCE(ms.stopbit,0)
            FROM   public.rack r
            LEFT   JOIN public.tcpip  lt ON r.localtcp     = lt.tcpid
            LEFT   JOIN public.serial ls ON r.localserial  = ls.serialid
            LEFT   JOIN public.tcpip  st ON r.srvtcp       = st.tcpid
            LEFT   JOIN public.tcpip  mt ON r.modbustcp    = mt.tcpid
            LEFT   JOIN public.serial ms ON r.modbusserial = ms.serialid
            WHERE  r.stationid = @sid AND r.rackid = @rid
            """;
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new RackFullInfo
        {
            Name             = r.GetString(0),
            Location         = r.GetString(1),
            Activity         = r.GetInt32(2) == 1,
            WaveformInterval = r.GetInt32(3),
            Trend            = r.GetInt32(4) == 1,
            StaticTrend      = r.GetInt32(5),
            DynamicTrend     = r.GetInt32(6),
            ModbusMode       = r.GetInt32(7),
            LocalIp          = r.GetString(8),
            LocalPort        = r.GetInt32(9),
            LocalSerialPort  = r.GetInt32(10),
            LocalBaudRate    = r.GetInt32(11),
            LocalDataBit     = r.GetInt32(12),
            LocalParityBit   = r.GetInt32(13),
            LocalStopBit     = r.GetInt32(14),
            ServerIp         = r.GetString(15),
            ServerPort       = r.GetInt32(16),
            ModbusIp         = r.GetString(17),
            ModbusPort       = r.GetInt32(18),
            ModSerialPort    = r.GetInt32(19),
            ModBaudRate      = r.GetInt32(20),
            ModDataBit       = r.GetInt32(21),
            ModParityBit     = r.GetInt32(22),
            ModStopBit       = r.GetInt32(23),
        };
    }

    /// <summary>
    /// RACK Modify 저장: 원본 frmRack1과 동일하게 rack 기본/Waveform/Trend/Modbus 모드 +
    /// 로컬 TCP·시리얼, 서버 TCP, Modbus TCP·시리얼을 한 트랜잭션으로 upsert 한다.
    /// </summary>
    public static async Task UpdateRackFullAsync(int stationId, int rackId, RackFullInfo info)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            // 현재 연결된 FK id 조회
            int? localTcp = null, localSerial = null, srvTcp = null, modTcp = null, modSerial = null;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT localtcp, localserial, srvtcp, modbustcp, modbusserial FROM public.rack WHERE stationid=@sid AND rackid=@rid";
                cmd.Parameters.AddWithValue("sid", stationId);
                cmd.Parameters.AddWithValue("rid", rackId);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    localTcp    = r.IsDBNull(0) ? null : r.GetInt32(0);
                    localSerial = r.IsDBNull(1) ? null : r.GetInt32(1);
                    srvTcp      = r.IsDBNull(2) ? null : r.GetInt32(2);
                    modTcp      = r.IsDBNull(3) ? null : r.GetInt32(3);
                    modSerial   = r.IsDBNull(4) ? null : r.GetInt32(4);
                }
            }

            // 연결 정보 upsert
            int localTcpId    = await UpsertTcpAsync(conn, tx, localTcp, info.LocalIp, info.LocalPort);
            int localSerialId = await UpsertSerialAsync(conn, tx, localSerial, info.LocalSerialPort, info.LocalBaudRate, info.LocalDataBit, info.LocalParityBit, info.LocalStopBit);
            int srvTcpId      = await UpsertTcpAsync(conn, tx, srvTcp, info.ServerIp, info.ServerPort);
            int modTcpId      = await UpsertTcpAsync(conn, tx, modTcp, info.ModbusIp, info.ModbusPort);
            int modSerialId   = await UpsertSerialAsync(conn, tx, modSerial, info.ModSerialPort, info.ModBaudRate, info.ModDataBit, info.ModParityBit, info.ModStopBit);

            // rack 본체
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    UPDATE public.rack SET
                        name=@n, location=@l, activity=@a,
                        waveforminterval=@wi, trend=@tr, statictrend=@st, dynamictrend=@dt,
                        modbusmode=@mm,
                        localtcp=@ltcp, localserial=@lser, srvtcp=@stcp, modbustcp=@mtcp, modbusserial=@mser
                    WHERE stationid=@sid AND rackid=@rid
                    """;
                cmd.Parameters.AddWithValue("n",   info.Name);
                cmd.Parameters.AddWithValue("l",   string.IsNullOrEmpty(info.Location) ? DBNull.Value : info.Location);
                cmd.Parameters.AddWithValue("a",   info.Activity ? 1 : 0);
                cmd.Parameters.AddWithValue("wi",  info.WaveformInterval);
                cmd.Parameters.AddWithValue("tr",  info.Trend ? 1 : 0);
                cmd.Parameters.AddWithValue("st",  info.StaticTrend);
                cmd.Parameters.AddWithValue("dt",  info.DynamicTrend);
                cmd.Parameters.AddWithValue("mm",  info.ModbusMode);
                cmd.Parameters.AddWithValue("ltcp", localTcpId);
                cmd.Parameters.AddWithValue("lser", localSerialId);
                cmd.Parameters.AddWithValue("stcp", srvTcpId);
                cmd.Parameters.AddWithValue("mtcp", modTcpId);
                cmd.Parameters.AddWithValue("mser", modSerialId);
                cmd.Parameters.AddWithValue("sid", stationId);
                cmd.Parameters.AddWithValue("rid", rackId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>tcpip 행을 갱신(id 있음)하거나 새로 생성하고 tcpid 를 반환한다.</summary>
    private static async Task<int> UpsertTcpAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int? id, string ip, int port)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        if (id.HasValue)
        {
            cmd.CommandText = "UPDATE public.tcpip SET ipaddr=@ip, port=@port WHERE tcpid=@id";
            cmd.Parameters.AddWithValue("ip",   ip ?? "");
            cmd.Parameters.AddWithValue("port", port);
            cmd.Parameters.AddWithValue("id",   id.Value);
            await cmd.ExecuteNonQueryAsync();
            return id.Value;
        }
        cmd.CommandText = "INSERT INTO public.tcpip(tcpid, ipaddr, port) VALUES((SELECT COALESCE(MAX(tcpid),0)+1 FROM public.tcpip), @ip, @port) RETURNING tcpid";
        cmd.Parameters.AddWithValue("ip",   ip ?? "");
        cmd.Parameters.AddWithValue("port", port);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>serial 행을 갱신(id 있음)하거나 새로 생성하고 serialid 를 반환한다.</summary>
    private static async Task<int> UpsertSerialAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int? id,
        int port, int baud, int data, int parity, int stop)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        if (id.HasValue)
        {
            cmd.CommandText = "UPDATE public.serial SET port=@p, baudrate=@b, databits=@d, paritybit=@pa, stopbit=@s WHERE serialid=@id";
            cmd.Parameters.AddWithValue("p",  port);
            cmd.Parameters.AddWithValue("b",  baud);
            cmd.Parameters.AddWithValue("d",  data);
            cmd.Parameters.AddWithValue("pa", parity);
            cmd.Parameters.AddWithValue("s",  stop);
            cmd.Parameters.AddWithValue("id", id.Value);
            await cmd.ExecuteNonQueryAsync();
            return id.Value;
        }
        cmd.CommandText = "INSERT INTO public.serial(serialid, port, baudrate, databits, paritybit, stopbit) VALUES((SELECT COALESCE(MAX(serialid),0)+1 FROM public.serial), @p,@b,@d,@pa,@s) RETURNING serialid";
        cmd.Parameters.AddWithValue("p",  port);
        cmd.Parameters.AddWithValue("b",  baud);
        cmd.Parameters.AddWithValue("d",  data);
        cmd.Parameters.AddWithValue("pa", parity);
        cmd.Parameters.AddWithValue("s",  stop);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task SetModuleActivityAsync(int stationId, int rackId, int moduleId, byte activity)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.module SET activity=@a WHERE stationid=@sid AND rackid=@rid AND moduleid=@mid";
        cmd.Parameters.AddWithValue("a",   (int)activity);
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("rid", rackId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  TRAIN CRUD (저장 프로시저 사용)
    // ────────────────────────────────────────────────────────

    public static async Task<int> NextTrainIdAsync(int stationId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(trainid),0)+1 FROM public.train WHERE stationid=@sid";
        cmd.Parameters.AddWithValue("sid", stationId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task CreateTrainAsync(int stationId, int trainId, string name, byte activity = 1)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "CALL P_INSERT_TRAIN(@sid,@tid,@act,@name)";
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("tid",  trainId);
        cmd.Parameters.AddWithValue("act",  (int)activity);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateTrainAsync(int stationId, int trainId, string name, byte activity)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "CALL P_UPDATE_TRAIN(@sid,@tid,@act,@name)";
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("tid",  trainId);
        cmd.Parameters.AddWithValue("act",  (int)activity);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteTrainAsync(int stationId, int trainId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await ExecAsync(conn, tx, "DELETE FROM public.point     WHERE stationid=@sid AND trainid=@tid", stationId, trainId);
            await ExecAsync(conn, tx, "DELETE FROM public.component WHERE stationid=@sid AND trainid=@tid", stationId, trainId);
            await ExecAsync(conn, tx, "DELETE FROM public.train     WHERE stationid=@sid AND trainid=@tid", stationId, trainId);
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ────────────────────────────────────────────────────────
    //  COMPONENT CRUD
    // ────────────────────────────────────────────────────────

    public static async Task<int> NextComponentIdAsync(int stationId, int trainId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(componentid),0)+1 FROM public.component WHERE stationid=@sid AND trainid=@tid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("tid", trainId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task CreateComponentAsync(int stationId, int trainId, int componentId, string name, byte activity = 1)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "CALL P_INSERT_COMPONENT(@sid,@tid,@cid,@act,@name)";
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("tid",  trainId);
        cmd.Parameters.AddWithValue("cid",  componentId);
        cmd.Parameters.AddWithValue("act",  (int)activity);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateComponentAsync(int stationId, int trainId, int componentId, string name, byte activity)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "CALL P_UPDATE_COMPONENT(@sid,@tid,@cid,@act,@name)";
        cmd.Parameters.AddWithValue("sid",  stationId);
        cmd.Parameters.AddWithValue("tid",  trainId);
        cmd.Parameters.AddWithValue("cid",  componentId);
        cmd.Parameters.AddWithValue("act",  (int)activity);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteComponentAsync(int stationId, int trainId, int componentId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();
        try
        {
            await ExecAsync(conn, tx, "DELETE FROM public.point     WHERE stationid=@sid AND trainid=@tid AND componentid=@cid", stationId, trainId, componentId);
            await ExecAsync(conn, tx, "DELETE FROM public.component WHERE stationid=@sid AND trainid=@tid AND componentid=@cid", stationId, trainId, componentId);
            await tx.CommitAsync();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    // ────────────────────────────────────────────────────────
    //  POINT CRUD
    // ────────────────────────────────────────────────────────

    public static async Task<int> NextPointIdAsync(int stationId, int trainId, int componentId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(pointid),0)+1 FROM public.point WHERE stationid=@sid AND trainid=@tid AND componentid=@cid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("tid", trainId);
        cmd.Parameters.AddWithValue("cid", componentId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task CreatePointAsync(int stationId, int trainId, int componentId, int pointId, string name, byte activity = 1, int assign = 0)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "CALL P_INSERT_POINT(@sid,@tid,@cid,@pid,@act,@assign,@name)";
        cmd.Parameters.AddWithValue("sid",    stationId);
        cmd.Parameters.AddWithValue("tid",    trainId);
        cmd.Parameters.AddWithValue("cid",    componentId);
        cmd.Parameters.AddWithValue("pid",    pointId);
        cmd.Parameters.AddWithValue("act",    (int)activity);
        cmd.Parameters.AddWithValue("assign", assign);
        cmd.Parameters.AddWithValue("name",   name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdatePointAsync(int stationId, int trainId, int componentId, int pointId, string name, byte activity, int assign)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "CALL P_UPDATE_POINT(@sid,@tid,@cid,@pid,@act,@assign,@name)";
        cmd.Parameters.AddWithValue("sid",    stationId);
        cmd.Parameters.AddWithValue("tid",    trainId);
        cmd.Parameters.AddWithValue("cid",    componentId);
        cmd.Parameters.AddWithValue("pid",    pointId);
        cmd.Parameters.AddWithValue("act",    (int)activity);
        cmd.Parameters.AddWithValue("assign", assign);
        cmd.Parameters.AddWithValue("name",   name);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeletePointAsync(int stationId, int trainId, int componentId, int pointId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.point WHERE stationid=@sid AND trainid=@tid AND componentid=@cid AND pointid=@pid";
        cmd.Parameters.AddWithValue("sid", stationId);
        cmd.Parameters.AddWithValue("tid", trainId);
        cmd.Parameters.AddWithValue("cid", componentId);
        cmd.Parameters.AddWithValue("pid", pointId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  내부 헬퍼
    // ────────────────────────────────────────────────────────

    private static async Task ExecAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql,
        int stationId, int id1 = 0, int id2 = 0)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction  = tx;
        cmd.CommandText  = sql;
        cmd.Parameters.AddWithValue("sid", stationId);
        if (sql.Contains("@tid"))  cmd.Parameters.AddWithValue("tid", id1);
        if (sql.Contains("@cid"))  cmd.Parameters.AddWithValue("cid", id2);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── 모듈 타입 ───────────────────────────────────────────
    public static async Task<List<ModuleTypeItem>> GetModuleTypesAsync()
    {
        var list = new List<ModuleTypeItem>();
        try
        {
            await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText = "SELECT moduletype::int, nicname, name, COALESCE(description,'') FROM public.module_type ORDER BY moduletype";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new ModuleTypeItem { TypeId = r.GetInt32(0), NicName = r.IsDBNull(1)?"":r.GetString(1), Name = r.IsDBNull(2)?"":r.GetString(2), Description = r.GetString(3) });
        }
        catch { }
        return list;
    }

    public static async Task CreateModuleTypeAsync(int typeId, string nicName, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.module_type(moduletype,nicname,name,description) VALUES(@t,@n,@nn,@d)";
        cmd.Parameters.AddWithValue("t",  typeId);
        cmd.Parameters.AddWithValue("n",  nicName);
        cmd.Parameters.AddWithValue("nn", name);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateModuleTypeAsync(int typeId, string nicName, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.module_type SET nicname=@n, name=@nn, description=@d WHERE moduletype=@t";
        cmd.Parameters.AddWithValue("t",  typeId);
        cmd.Parameters.AddWithValue("n",  nicName);
        cmd.Parameters.AddWithValue("nn", name);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteModuleTypeAsync(int typeId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.module_type WHERE moduletype=@t";
        cmd.Parameters.AddWithValue("t", typeId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextModuleTypeIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(moduletype),0)+1 FROM public.module_type";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    // ── 채널 타입 ───────────────────────────────────────────
    public static async Task<List<ChannelTypeItem>> GetChannelTypesAsync()
    {
        var list = new List<ChannelTypeItem>();
        try
        {
            await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
            await using var cmd  = conn.CreateCommand();
            cmd.CommandText = "SELECT channeltype::int, nicname, name, COALESCE(description,'') FROM public.channel_type ORDER BY channeltype";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new ChannelTypeItem { TypeId = r.GetInt32(0), NicName = r.IsDBNull(1)?"":r.GetString(1), Name = r.IsDBNull(2)?"":r.GetString(2), Description = r.GetString(3) });
        }
        catch { }
        return list;
    }

    public static async Task CreateChannelTypeAsync(int typeId, string nicName, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.channel_type(channeltype,nicname,name,description) VALUES(@t,@n,@nn,@d)";
        cmd.Parameters.AddWithValue("t",  typeId);
        cmd.Parameters.AddWithValue("n",  nicName);
        cmd.Parameters.AddWithValue("nn", name);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateChannelTypeAsync(int typeId, string nicName, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.channel_type SET nicname=@n, name=@nn, description=@d WHERE channeltype=@t";
        cmd.Parameters.AddWithValue("t",  typeId);
        cmd.Parameters.AddWithValue("n",  nicName);
        cmd.Parameters.AddWithValue("nn", name);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteChannelTypeAsync(int typeId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.channel_type WHERE channeltype=@t";
        cmd.Parameters.AddWithValue("t", typeId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextChannelTypeIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(channeltype),0)+1 FROM public.channel_type";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    // ────────────────────────────────────────────────────────
    //  센서 (Sensor) — 조회 전용
    // ────────────────────────────────────────────────────────

    public static async Task<List<SensorItem>> GetSensorsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT s.sensorid::int, s.name,
                   COALESCE(s.type,0)::int,
                   COALESCE(s.sensitivity,0)::float8,
                   COALESCE(u.name,''),
                   COALESCE(s.icp,0)::int,
                   COALESCE(s.power,0)::int,
                   COALESCE(s.power_check_low,0)::float8,
                   COALESCE(s.power_check_high,0)::float8,
                   COALESCE(s.brandname,''),
                   COALESCE(s.spec,'')
            FROM public.sensor s
            LEFT JOIN public.sensor_unit u ON s.unit = u.unitid
            ORDER BY s.sensorid
            """;
        var list = new List<SensorItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SensorItem
            {
                SensorId       = r.GetInt32(0),
                Name           = r.GetString(1),
                Type           = r.GetInt32(2),
                Sensitivity    = r.GetDouble(3),
                UnitName       = r.GetString(4),
                Icp            = r.GetInt32(5),
                Power          = r.GetInt32(6),
                PowerCheckLow  = r.GetDouble(7),
                PowerCheckHigh = r.GetDouble(8),
                BrandName      = r.GetString(9),
                Spec           = r.GetString(10)
            });
        return list;
    }

    // ────────────────────────────────────────────────────────
    //  Module & Channel Type 연결 (module_channel_type)
    // ────────────────────────────────────────────────────────

    public static async Task<List<McChannelItem>> GetModuleChannelTypesAsync(int moduleTypeId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.channeltype::int, b.name
            FROM public.module_channel_type a
            INNER JOIN public.channel_type b ON a.channeltype = b.channeltype
            WHERE a.moduletype = @mt
            ORDER BY b.channeltype
            """;
        cmd.Parameters.AddWithValue("mt", moduleTypeId);
        var list = new List<McChannelItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new McChannelItem { ChannelTypeId = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }

    public static async Task AddModuleChannelTypeAsync(int moduleTypeId, int channelTypeId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.module_channel_type(moduletype, channeltype, description)
            VALUES(@mt, @ct, '')
            ON CONFLICT (moduletype, channeltype) DO NOTHING
            """;
        cmd.Parameters.AddWithValue("mt", moduleTypeId);
        cmd.Parameters.AddWithValue("ct", channelTypeId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task RemoveModuleChannelTypeAsync(int moduleTypeId, int channelTypeId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.module_channel_type WHERE moduletype=@mt AND channeltype=@ct";
        cmd.Parameters.AddWithValue("mt", moduleTypeId);
        cmd.Parameters.AddWithValue("ct", channelTypeId);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextSensorIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(sensorid),0)+1 FROM public.sensor";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task CreateSensorAsync(int id, string name, int type, double sensitivity,
        int unitId, int icp, int power, double powerLow, double powerHigh, string brand, string spec)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.sensor
                (sensorid, name, type, sensitivity, unit, icp, power, power_check_low, power_check_high, brandname, spec)
            VALUES (@id, @n, @t, @s, @u, @i, @pw, @pl, @ph, @b, @sp)
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("t",  (short)type);
        cmd.Parameters.AddWithValue("s",  sensitivity);
        cmd.Parameters.AddWithValue("u",  (short)unitId);
        cmd.Parameters.AddWithValue("i",  (short)icp);
        cmd.Parameters.AddWithValue("pw", (short)power);
        cmd.Parameters.AddWithValue("pl", (float)powerLow);
        cmd.Parameters.AddWithValue("ph", (float)powerHigh);
        cmd.Parameters.AddWithValue("b",  brand);
        cmd.Parameters.AddWithValue("sp", spec);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateSensorAsync(int id, string name, int type, double sensitivity,
        int unitId, int icp, int power, double powerLow, double powerHigh, string brand, string spec)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE public.sensor SET
                name=@n, type=@t, sensitivity=@s, unit=@u, icp=@i,
                power=@pw, power_check_low=@pl, power_check_high=@ph, brandname=@b, spec=@sp
            WHERE sensorid=@id
            """;
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("t",  (short)type);
        cmd.Parameters.AddWithValue("s",  sensitivity);
        cmd.Parameters.AddWithValue("u",  (short)unitId);
        cmd.Parameters.AddWithValue("i",  (short)icp);
        cmd.Parameters.AddWithValue("pw", (short)power);
        cmd.Parameters.AddWithValue("pl", (float)powerLow);
        cmd.Parameters.AddWithValue("ph", (float)powerHigh);
        cmd.Parameters.AddWithValue("b",  brand);
        cmd.Parameters.AddWithValue("sp", spec);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteSensorAsync(int id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.sensor WHERE sensorid=@i";
        cmd.Parameters.AddWithValue("i", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  센서 단위 (Sensor Unit)
    // ────────────────────────────────────────────────────────

    public static async Task<List<SensorUnitItem>> GetSensorUnitsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT unitid::int, name, COALESCE(description,'') FROM public.sensor_unit ORDER BY unitid";
        var list = new List<SensorUnitItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SensorUnitItem { UnitId = r.GetInt32(0), Name = r.GetString(1), Description = r.GetString(2) });
        return list;
    }

    public static async Task CreateSensorUnitAsync(int id, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.sensor_unit(unitid,name,description) VALUES(@i,@n,@d)";
        cmd.Parameters.AddWithValue("i", (short)id);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("d", desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateSensorUnitAsync(int id, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.sensor_unit SET name=@n, description=@d WHERE unitid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("d", desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteSensorUnitAsync(int id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.sensor_unit WHERE unitid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextSensorUnitIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(unitid),0)+1 FROM public.sensor_unit";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ────────────────────────────────────────────────────────
    //  Display Plot
    // ────────────────────────────────────────────────────────

    public static async Task<List<DisplayPlotItem>> GetDisplayPlotsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT plotid::int, name, COALESCE(description,''), COALESCE(dynamic,0)::int FROM public.display_plot ORDER BY plotid";
        var list = new List<DisplayPlotItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new DisplayPlotItem { PlotId = r.GetInt32(0), Name = r.GetString(1), Description = r.GetString(2), Dynamic = r.GetInt32(3) });
        return list;
    }

    public static async Task CreateDisplayPlotAsync(int id, string name, string desc, int dynamic)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.display_plot(plotid,name,description,dynamic) VALUES(@i,@n,@d,@dy)";
        cmd.Parameters.AddWithValue("i",  (short)id);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("d",  desc);
        cmd.Parameters.AddWithValue("dy", (short)dynamic);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateDisplayPlotAsync(int id, string name, string desc, int dynamic)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.display_plot SET name=@n, description=@d, dynamic=@dy WHERE plotid=@i";
        cmd.Parameters.AddWithValue("i",  (short)id);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("d",  desc);
        cmd.Parameters.AddWithValue("dy", (short)dynamic);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteDisplayPlotAsync(int id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.display_plot WHERE plotid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextDisplayPlotIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(plotid),0)+1 FROM public.display_plot";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ────────────────────────────────────────────────────────
    //  비례값 (Proportional)
    // ────────────────────────────────────────────────────────

    public static async Task<List<ProportionalItem>> GetProportionalsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT varid::int, nicname, name, COALESCE(description,'') FROM public.proportional ORDER BY varid";
        var list = new List<ProportionalItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ProportionalItem { VarId = r.GetInt32(0), NicName = r.GetString(1), Name = r.GetString(2), Description = r.GetString(3) });
        return list;
    }

    public static async Task CreateProportionalAsync(int id, string nicName, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.proportional(varid,nicname,name,description) VALUES(@i,@nn,@n,@d)";
        cmd.Parameters.AddWithValue("i",  (short)id);
        cmd.Parameters.AddWithValue("nn", nicName);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateProportionalAsync(int id, string nicName, string name, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.proportional SET nicname=@nn, name=@n, description=@d WHERE varid=@i";
        cmd.Parameters.AddWithValue("i",  (short)id);
        cmd.Parameters.AddWithValue("nn", nicName);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteProportionalAsync(int id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.proportional WHERE varid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextProportionalIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(varid),0)+1 FROM public.proportional";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ────────────────────────────────────────────────────────
    //  스케일 범위 (Scale Range)
    // ────────────────────────────────────────────────────────

    public static async Task<List<ScaleRangeItem>> GetScaleRangesAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT scaleid::int, name, COALESCE(min,0)::float8, COALESCE(max,0)::float8, COALESCE(description,'') FROM public.display_scale_range ORDER BY scaleid";
        var list = new List<ScaleRangeItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ScaleRangeItem { ScaleId = r.GetInt32(0), Name = r.GetString(1), Min = r.GetDouble(2), Max = r.GetDouble(3), Description = r.GetString(4) });
        return list;
    }

    public static async Task CreateScaleRangeAsync(int id, string name, double min, double max, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.display_scale_range(scaleid,name,min,max,description) VALUES(@i,@n,@mn,@mx,@d)";
        cmd.Parameters.AddWithValue("i",  (short)id);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("mn", (float)min);
        cmd.Parameters.AddWithValue("mx", (float)max);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateScaleRangeAsync(int id, string name, double min, double max, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.display_scale_range SET name=@n, min=@mn, max=@mx, description=@d WHERE scaleid=@i";
        cmd.Parameters.AddWithValue("i",  (short)id);
        cmd.Parameters.AddWithValue("n",  name);
        cmd.Parameters.AddWithValue("mn", (float)min);
        cmd.Parameters.AddWithValue("mx", (float)max);
        cmd.Parameters.AddWithValue("d",  desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteScaleRangeAsync(int id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.display_scale_range WHERE scaleid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextScaleRangeIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(scaleid),0)+1 FROM public.display_scale_range";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ────────────────────────────────────────────────────────
    //  이벤트/상태 (Event)
    // ────────────────────────────────────────────────────────

    public static async Task<List<EventItem>> GetEventsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT eventid::int, name, COALESCE(\"class\",0)::int, COALESCE(description,'') FROM public.event ORDER BY eventid";
        var list = new List<EventItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new EventItem { EventId = r.GetInt32(0), Name = r.GetString(1), EventClass = r.GetInt32(2), Description = r.GetString(3) });
        return list;
    }

    public static async Task CreateEventAsync(int id, string name, int eventClass, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.event(eventid,name,eventclass,description) VALUES(@i,@n,@c,@d)";
        cmd.Parameters.AddWithValue("i", (short)id);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("c", (short)eventClass);
        cmd.Parameters.AddWithValue("d", desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task UpdateEventAsync(int id, string name, int eventClass, string desc)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "UPDATE public.event SET name=@n, eventclass=@c, description=@d WHERE eventid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        cmd.Parameters.AddWithValue("n", name);
        cmd.Parameters.AddWithValue("c", (short)eventClass);
        cmd.Parameters.AddWithValue("d", desc);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteEventAsync(int id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.event WHERE eventid=@i";
        cmd.Parameters.AddWithValue("i", (short)id);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> NextEventIdAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(eventid),0)+1 FROM public.event";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // ────────────────────────────────────────────────────────
    //  채널 타입 & 센서 (Channel Type & Sensor)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetCtSensorRowsAsync(int ct)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.sensorid::int, b.name
            FROM public.channel_type_sensor a
            INNER JOIN public.sensor b ON a.sensorid = b.sensorid
            WHERE a.channeltype = @ct ORDER BY b.sensorid
            """;
        cmd.Parameters.AddWithValue("ct", (short)ct);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddCtSensorAsync(int ct, int sensorId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.channel_type_sensor(channeltype,sensorid) VALUES(@ct,@s) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("s",  sensorId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveCtSensorAsync(int ct, int sensorId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.channel_type_sensor WHERE channeltype=@ct AND sensorid=@s";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("s",  sensorId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  채널 타입 & 센서단위 (Channel Type & SensorUnit)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetCtSensorUnitRowsAsync(int ct)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.unitid::int, b.name
            FROM public.channel_type_sensor_unit a
            INNER JOIN public.sensor_unit b ON a.unitid = b.unitid
            WHERE a.channeltype = @ct ORDER BY b.unitid
            """;
        cmd.Parameters.AddWithValue("ct", (short)ct);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddCtSensorUnitAsync(int ct, int unitId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.channel_type_sensor_unit(channeltype,unitid) VALUES(@ct,@u) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("u",  (short)unitId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveCtSensorUnitAsync(int ct, int unitId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.channel_type_sensor_unit WHERE channeltype=@ct AND unitid=@u";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("u",  (short)unitId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  채널 타입 & Display Plot
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetCtPlotRowsAsync(int ct)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.plotid::int, b.name
            FROM public.channel_type_display_plot a
            INNER JOIN public.display_plot b ON a.plotid = b.plotid
            WHERE a.channeltype = @ct ORDER BY b.plotid
            """;
        cmd.Parameters.AddWithValue("ct", (short)ct);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddCtPlotAsync(int ct, int plotId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.channel_type_display_plot(channeltype,plotid) VALUES(@ct,@p) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("p",  (short)plotId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveCtPlotAsync(int ct, int plotId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.channel_type_display_plot WHERE channeltype=@ct AND plotid=@p";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("p",  (short)plotId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  채널 타입 & 비례값 (Channel Type & Proportional)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetCtPropRowsAsync(int ct)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.varid::int, b.nicname
            FROM public.channel_type_proportional a
            INNER JOIN public.proportional b ON a.varid = b.varid
            WHERE a.channeltype = @ct ORDER BY b.varid
            """;
        cmd.Parameters.AddWithValue("ct", (short)ct);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddCtPropAsync(int ct, int varId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.channel_type_proportional(channeltype,varid) VALUES(@ct,@v) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("v",  varId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveCtPropAsync(int ct, int varId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.channel_type_proportional WHERE channeltype=@ct AND varid=@v";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("v",  varId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  채널 타입 & 스케일범위 (Channel Type & ScaleRange)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetCtScaleRowsAsync(int ct)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.scaleid::int, b.name
            FROM public.channel_type_display_scale_range a
            INNER JOIN public.display_scale_range b ON a.scaleid = b.scaleid
            WHERE a.channeltype = @ct ORDER BY b.scaleid
            """;
        cmd.Parameters.AddWithValue("ct", (short)ct);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddCtScaleAsync(int ct, int scaleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.channel_type_display_scale_range(channeltype,scaleid) VALUES(@ct,@s) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("s",  (short)scaleId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveCtScaleAsync(int ct, int scaleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.channel_type_display_scale_range WHERE channeltype=@ct AND scaleid=@s";
        cmd.Parameters.AddWithValue("ct", (short)ct);
        cmd.Parameters.AddWithValue("s",  (short)scaleId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  센서 단위 변환기 (Sensor Unit Converter)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SuConvRow>> GetSuConverterAsync(int unitId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT x.name, y.formula, y.conv_factor::text, y.sens_formula, y.sens_conv_factor::text
            FROM public.sensor_unit x,
                 (SELECT unit, formula, conv_factor, sens_formula, sens_conv_factor, seq
                  FROM public.sensor_unit a
                  INNER JOIN public.sensor_unit_converter b ON a.unitid = b.defunit
                  WHERE a.unitid = @uid) y
            WHERE x.unitid = y.unit
            ORDER BY y.seq
            """;
        cmd.Parameters.AddWithValue("uid", (short)unitId);
        var list = new List<SuConvRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SuConvRow
            {
                ConvUnit       = r.IsDBNull(0) ? "" : r.GetString(0),
                Formula        = r.IsDBNull(1) ? "" : r.GetString(1),
                ConvFactor     = r.IsDBNull(2) ? "" : r.GetString(2),
                SensFormula    = r.IsDBNull(3) ? "" : r.GetString(3),
                SensConvFactor = r.IsDBNull(4) ? "" : r.GetString(4)
            });
        return list;
    }

    // ────────────────────────────────────────────────────────
    //  Display Plot & 비례값 (Display Plot & Proportional)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetDpPropRowsAsync(int plotId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.varid::int, b.nicname
            FROM public.display_plot_proportional a
            INNER JOIN public.proportional b ON a.varid = b.varid
            WHERE a.plotid = @pid ORDER BY b.varid
            """;
        cmd.Parameters.AddWithValue("pid", (short)plotId);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddDpPropAsync(int plotId, int varId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.display_plot_proportional(plotid,varid,description) VALUES(@p,@v,'') ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("v", varId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveDpPropAsync(int plotId, int varId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.display_plot_proportional WHERE plotid=@p AND varid=@v";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("v", varId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  Display Plot & DataSource
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetDpDataSourceRowsAsync(int plotId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.dataid::int, b.name
            FROM public.plot_data_source a
            INNER JOIN public.data_source b ON a.dataid = b.dataid
            WHERE a.plotid = @pid ORDER BY b.dataid
            """;
        cmd.Parameters.AddWithValue("pid", (short)plotId);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task<List<SimpleItem>> GetAllDataSourcesAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT dataid::int, name FROM public.data_source ORDER BY dataid";
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddDpDataSourceAsync(int plotId, int dataId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.plot_data_source(plotid,dataid,description) VALUES(@p,@d,'') ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("d", dataId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveDpDataSourceAsync(int plotId, int dataId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.plot_data_source WHERE plotid=@p AND dataid=@d";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("d", dataId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  Display Plot & Compensation
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetDpCompRowsAsync(int plotId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.compid::int, b.name
            FROM public.plot_compensation a
            INNER JOIN public.compensation b ON a.compid = b.compid
            WHERE a.plotid = @pid ORDER BY b.compid
            """;
        cmd.Parameters.AddWithValue("pid", (short)plotId);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task<List<SimpleItem>> GetAllCompensationsAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT compid::int, name FROM public.compensation ORDER BY compid";
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddDpCompAsync(int plotId, int compId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.plot_compensation(plotid,compid,description) VALUES(@p,@c,'') ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("c", compId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveDpCompAsync(int plotId, int compId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.plot_compensation WHERE plotid=@p AND compid=@c";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("c", compId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  Display Plot & Freq Analysis
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetDpFreqRowsAsync(int plotId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.analysisid::int, b.name
            FROM public.plot_freq_analysis a
            INNER JOIN public.freq_analysis b ON a.analysisid = b.analysisid
            WHERE a.plotid = @pid ORDER BY b.analysisid
            """;
        cmd.Parameters.AddWithValue("pid", (short)plotId);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task<List<SimpleItem>> GetAllFreqAnalysisAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT analysisid::int, name FROM public.freq_analysis ORDER BY analysisid";
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddDpFreqAsync(int plotId, int analysisId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.plot_freq_analysis(plotid,analysisid,description) VALUES(@p,@a,'') ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("a", analysisId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveDpFreqAsync(int plotId, int analysisId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.plot_freq_analysis WHERE plotid=@p AND analysisid=@a";
        cmd.Parameters.AddWithValue("p", (short)plotId);
        cmd.Parameters.AddWithValue("a", analysisId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ────────────────────────────────────────────────────────
    //  비례값 & 스케일범위 (Proportional & Scale Range)
    // ────────────────────────────────────────────────────────
    public static async Task<List<SimpleItem>> GetPropScaleRowsAsync(int varId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            SELECT b.scaleid::int, b.name
            FROM public.proportional_display_scale_range a
            INNER JOIN public.display_scale_range b ON a.scaleid = b.scaleid
            WHERE a.varid = @vid ORDER BY b.scaleid
            """;
        cmd.Parameters.AddWithValue("vid", varId);
        var list = new List<SimpleItem>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(new SimpleItem { Id = r.GetInt32(0), Name = r.GetString(1) });
        return list;
    }
    public static async Task AddPropScaleAsync(int varId, int scaleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO public.proportional_display_scale_range(varid,scaleid) VALUES(@v,@s) ON CONFLICT DO NOTHING";
        cmd.Parameters.AddWithValue("v", varId);
        cmd.Parameters.AddWithValue("s", (short)scaleId);
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemovePropScaleAsync(int varId, int scaleId)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM public.proportional_display_scale_range WHERE varid=@v AND scaleid=@s";
        cmd.Parameters.AddWithValue("v", varId);
        cmd.Parameters.AddWithValue("s", (short)scaleId);
        await cmd.ExecuteNonQueryAsync();
    }
}
