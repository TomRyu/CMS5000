using System.IO;
using System.Text.Json;
using Npgsql;

namespace CMS5000.Services;

public static class PostgresService
{
    private static NpgsqlDataSource? _ds;

    // 현재 접속 정보(원본 My.Settings.SERVER/DATABASE/USER/PASSWORD 대응)
    public static string CurrentHost     { get; private set; } = "";
    public static string CurrentDatabase { get; private set; } = "";
    public static string CurrentUsername { get; private set; } = "";
    public static string CurrentPassword { get; private set; } = "";

    public static void Initialize()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("appsettings.json 파일을 찾을 수 없습니다.", configPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var pg = doc.RootElement.GetProperty("PostgreSQL");

        string host = pg.GetProperty("Host").GetString()     ?? throw new InvalidOperationException("PostgreSQL.Host 설정 누락");
        string db   = pg.GetProperty("Database").GetString() ?? throw new InvalidOperationException("PostgreSQL.Database 설정 누락");
        string user = pg.GetProperty("Username").GetString() ?? throw new InvalidOperationException("PostgreSQL.Username 설정 누락");
        string pw   = pg.GetProperty("Password").GetString() ?? throw new InvalidOperationException("PostgreSQL.Password 설정 누락");

        _ds = BuildDataSource(host, db, user, pw);
        CurrentHost = host; CurrentDatabase = db; CurrentUsername = user; CurrentPassword = pw;
    }

    private static NpgsqlDataSource BuildDataSource(string host, string db, string user, string pw)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host        = host,
            Database    = db,
            Username    = user,
            Password    = pw,
            Pooling     = true,
            MinPoolSize = 1,
            MaxPoolSize = 10,
        };
        return NpgsqlDataSource.Create(builder.ConnectionString);
    }

    /// <summary>
    /// 원본 ConnectDatabaseChecker: 새 접속정보로 연결을 시도해 성공하면 데이터소스를 교체하고
    /// appsettings.json 에 저장한다. 실패하면 기존 연결을 유지하고 false 를 반환.
    /// </summary>
    public static async Task<bool> ReconfigureAsync(string host, string db, string user, string pw)
    {
        NpgsqlDataSource? candidate = null;
        try
        {
            candidate = BuildDataSource(host, db, user, pw);
            await using (var conn = await candidate.OpenConnectionAsync())
            await using (var cmd = new NpgsqlCommand("SELECT 1", conn))
                await cmd.ExecuteScalarAsync();
        }
        catch
        {
            candidate?.Dispose();
            return false;
        }

        var old = _ds;
        _ds = candidate;
        CurrentHost = host; CurrentDatabase = db; CurrentUsername = user; CurrentPassword = pw;
        IsManuallyDisconnected = false;
        old?.Dispose();

        SaveToAppSettings(host, db, user, pw);
        return true;
    }

    private static void SaveToAppSettings(string host, string db, string user, string pw)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = doc.RootElement.Clone();

            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("PostgreSQL"))
                    {
                        w.WritePropertyName("PostgreSQL");
                        w.WriteStartObject();
                        w.WriteString("Host", host);
                        w.WriteString("Database", db);
                        w.WriteString("Username", user);
                        w.WriteString("Password", pw);
                        // PostgreSQL 하위의 그 외 키는 보존
                        foreach (var sub in prop.Value.EnumerateObject())
                            if (sub.Name is not ("Host" or "Database" or "Username" or "Password"))
                                sub.WriteTo(w);
                        w.WriteEndObject();
                    }
                    else
                    {
                        prop.WriteTo(w);
                    }
                }
                w.WriteEndObject();
            }
            File.WriteAllBytes(configPath, ms.ToArray());
        }
        catch { /* 저장 실패는 무시(런타임 연결은 이미 교체됨) */ }
    }

    public static NpgsqlDataSource DataSource =>
        _ds ?? throw new InvalidOperationException("PostgresService가 초기화되지 않았습니다.");

    /// <summary>
    /// 사용자가 Connection 메뉴에서 수동으로 DB 연결을 끊은 상태.
    /// true 이면 연결 점검이 실패로 처리되어 하단 LED 가 회색이 된다(원본 Database Disconnect 의 실제 동작 버전).
    /// </summary>
    public static bool IsManuallyDisconnected { get; private set; }

    /// <summary>수동 연결 해제: 끊김 상태로 표시(연결 점검·데이터 접근 차단).</summary>
    public static void Disconnect()
    {
        IsManuallyDisconnected = true;
    }

    /// <summary>수동 재연결: 끊김 표시 해제.</summary>
    public static void Reconnect()
    {
        IsManuallyDisconnected = false;
    }

    public static async Task EnsureReachableAsync()
    {
        if (IsManuallyDisconnected)
            throw new InvalidOperationException("DB 연결이 수동으로 해제되었습니다.");

        await using var conn = await DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
    }

    /// <summary>
    /// Rack/Module/Channel 확장 스키마를 멱등(IF NOT EXISTS)으로 보장한다.
    /// supabase/migrations 의 DDL과 동일하며, 미적용 환경에서도 화면이 동작하도록 시작 시 1회 실행.
    /// </summary>
    public static async Task EnsureSchemaAsync()
    {
        const string ddl = """
            CREATE TABLE IF NOT EXISTS public.tcpip (
                tcpid  serial PRIMARY KEY, ipaddr varchar(20), port int);

            CREATE TABLE IF NOT EXISTS public.serial (
                serialid serial PRIMARY KEY, port smallint DEFAULT 0, baudrate int DEFAULT 0,
                databits smallint DEFAULT 0, paritybit smallint DEFAULT 0, stopbit smallint DEFAULT 0);

            -- 기존 tcpip/serial 테이블에 누락 컬럼 보강 (레거시 컬럼명: databits 복수형)
            ALTER TABLE public.tcpip
                ADD COLUMN IF NOT EXISTS ipaddr varchar(20),
                ADD COLUMN IF NOT EXISTS port   int;

            ALTER TABLE public.serial
                ADD COLUMN IF NOT EXISTS port      smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS baudrate  int      DEFAULT 0,
                ADD COLUMN IF NOT EXISTS databits  smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS paritybit smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS stopbit   smallint DEFAULT 0;

            ALTER TABLE public.rack
                ADD COLUMN IF NOT EXISTS waveforminterval smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS trend            smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS statictrend      smallint DEFAULT 10,
                ADD COLUMN IF NOT EXISTS dynamictrend     smallint DEFAULT 10,
                ADD COLUMN IF NOT EXISTS localserial      int,
                ADD COLUMN IF NOT EXISTS srvtcp           int,
                ADD COLUMN IF NOT EXISTS modbusmode       smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS modbustcp        int,
                ADD COLUMN IF NOT EXISTS modbusserial     int;

            ALTER TABLE public.module
                ADD COLUMN IF NOT EXISTS configdate timestamp;

            ALTER TABLE public.general_channel
                ADD COLUMN IF NOT EXISTS referenceactivity smallint DEFAULT 0,
                ADD COLUMN IF NOT EXISTS referenceid       smallint DEFAULT 0;

            CREATE TABLE IF NOT EXISTS public.channel_reference (
                stationid int NOT NULL, rackid int NOT NULL, moduleid int NOT NULL, channelid int NOT NULL,
                name varchar(64) DEFAULT '', channeltype int DEFAULT 0, activitymode smallint DEFAULT 0, assign smallint DEFAULT 0,
                reassignmode smallint DEFAULT 0, speed int DEFAULT 0, alternateid smallint DEFAULT 0, rotationdir smallint DEFAULT 0,
                signalpolarity smallint DEFAULT 0, thresholdtype smallint DEFAULT 0, thresholdlevel int DEFAULT 0,
                clampvalue int DEFAULT 0, upperlimit int DEFAULT 0, hysteresislevel int DEFAULT 0, fluctuationrange int DEFAULT 0,
                unalteredtime int DEFAULT 0, orientationangle int DEFAULT 0, orientation smallint DEFAULT 0,
                waveforminterval int DEFAULT 0, eprevolution int DEFAULT 0,
                sensorname varchar(64) DEFAULT '', sensitivity int DEFAULT 0, sensorunit varchar(32) DEFAULT '', icp smallint DEFAULT 0,
                powerlow int DEFAULT 0, powerhigh int DEFAULT 0, proximitorpower smallint DEFAULT 0, signaltype smallint DEFAULT 0,
                uploadtime int DEFAULT 0, uploadcondition smallint DEFAULT 0, startuprpm int DEFAULT 0, shutdownrpm int DEFAULT 0,
                sr_begin int DEFAULT 0, sr_end int DEFAULT 0, sr_delta int DEFAULT 0,
                sd_max int DEFAULT 0, sd_min int DEFAULT 0, sd_delta int DEFAULT 0,
                su_max int DEFAULT 0, su_min int DEFAULT 0, su_delta int DEFAULT 0,
                PRIMARY KEY (stationid, rackid, moduleid, channelid));

            -- 릴레이 채널 설정(원본 RELAY / RELAY_LOGIC)
            CREATE TABLE IF NOT EXISTS public.relay (
                relayidx      serial PRIMARY KEY,
                channel_index int NOT NULL,
                mode          smallint DEFAULT 0,
                andvoting     smallint DEFAULT 0);

            CREATE TABLE IF NOT EXISTS public.relay_logic (
                relayidx   int      NOT NULL,
                sequence   smallint NOT NULL,
                moduleid   smallint DEFAULT 0,
                channelid  smallint DEFAULT 0,
                alertdanger smallint DEFAULT 0,
                andorend   smallint DEFAULT 0,
                PRIMARY KEY (relayidx, sequence));
            """;

        await using var conn = await DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(ddl, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
