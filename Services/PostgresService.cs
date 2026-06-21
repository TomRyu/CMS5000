using System.IO;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// 사용자별 영구 접속정보 경로(%APPDATA%\CMS5000\connection.json).
    /// 앱 폴더(appsettings.json)와 분리되어 재빌드·Velopack 업데이트와 무관하게 마지막 DB가 유지된다.
    /// </summary>
    private static string UserConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "CMS5000", "connection.json");

    /// <summary>
    /// 사용자 영구 접속정보(%APPDATA%\CMS5000\connection.json)가 존재하는지.
    /// false 면 최초 실행으로 보고, 시작 시 접속 다이얼로그로 접속정보를 입력받는다.
    /// </summary>
    public static bool HasUserConfig => File.Exists(UserConfigPath);

    // connection.json 비밀번호 암호화 마커(Windows DPAPI, CurrentUser 범위).
    private const string EncPrefix = "enc:v1:";
    // 마지막으로 읽은 connection.json 의 비밀번호가 평문(레거시)이었으면 true → 시작 시 암호문으로 재저장.
    private static bool _userPwWasPlaintext;

    /// <summary>비밀번호를 DPAPI(CurrentUser)로 암호화해 "enc:v1:&lt;base64&gt;" 형태로 만든다.</summary>
    private static string ProtectPassword(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return plain;
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return EncPrefix + Convert.ToBase64String(enc);
        }
        catch { return plain; }   // 암호화 불가 시 평문 저장(최악의 경우에도 동작 보장)
    }

    /// <summary>"enc:v1:" 접두사가 있으면 복호화, 없으면 레거시 평문으로 간주. 복호화 실패 시 빈 문자열.</summary>
    private static string UnprotectPassword(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(EncPrefix, StringComparison.Ordinal))
            return stored;   // 레거시 평문
        try
        {
            var data = Convert.FromBase64String(stored[EncPrefix.Length..]);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
        }
        catch { return ""; } // 다른 사용자/PC 등으로 복호화 실패 → 빈 값(재입력 유도)
    }

    public static void Initialize()
    {
        // 1) 기본값: 번들 appsettings.json (폴백/템플릿). 배포본은 자격증명이 비어 있을 수 있다.
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("appsettings.json 파일을 찾을 수 없습니다.", configPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var pg = doc.RootElement.GetProperty("PostgreSQL");

        string host = pg.GetProperty("Host").GetString()     ?? "";
        string db   = pg.GetProperty("Database").GetString() ?? "";
        string user = pg.GetProperty("Username").GetString() ?? "";
        string pw   = pg.GetProperty("Password").GetString() ?? "";

        // 2) 사용자 영구 설정이 있으면 그 값으로 덮어씀(= 마지막 접속 DB 복원)
        TryLoadUserConfig(ref host, ref db, ref user, ref pw);

        CurrentHost = host; CurrentDatabase = db; CurrentUsername = user; CurrentPassword = pw;

        // 3) 접속정보가 모두 채워졌을 때만 데이터소스를 만든다.
        //    비어 있으면(최초 배포본) _ds 를 null 로 두고, 시작 시 다이얼로그 입력 후 ReconfigureAsync 가 생성한다.
        if (!string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(db) && !string.IsNullOrWhiteSpace(user))
            _ds = BuildDataSource(host, db, user, pw);

        // 4) 레거시 평문 connection.json 이면 암호문으로 1회 마이그레이션.
        if (_userPwWasPlaintext && !string.IsNullOrEmpty(pw))
        {
            SaveConnection(host, db, user, pw);
            _userPwWasPlaintext = false;
        }
    }

    /// <summary>%APPDATA% 의 connection.json 이 있으면 접속정보를 읽어 덮어쓴다.</summary>
    private static void TryLoadUserConfig(ref string host, ref string db, ref string user, ref string pw)
    {
        try
        {
            if (!File.Exists(UserConfigPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(UserConfigPath));
            var r = doc.RootElement;
            if (r.TryGetProperty("Host", out var h)     && h.GetString() is { Length: > 0 } hv) host = hv;
            if (r.TryGetProperty("Database", out var d) && d.GetString() is { Length: > 0 } dv) db   = dv;
            if (r.TryGetProperty("Username", out var u) && u.GetString() is { Length: > 0 } uv) user = uv;
            if (r.TryGetProperty("Password", out var p) && p.GetString() is { } pv)
            {
                _userPwWasPlaintext = !pv.StartsWith(EncPrefix, StringComparison.Ordinal) && pv.Length > 0;
                pw = UnprotectPassword(pv);
            }
        }
        catch { /* 손상 시 무시하고 기본값(appsettings) 사용 */ }
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
    /// %APPDATA%\CMS5000\connection.json 에 저장한다. 실패하면 기존 연결을 유지하고 false 를 반환.
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

        SaveConnection(host, db, user, pw);
        return true;
    }

    /// <summary>
    /// 마지막 접속정보를 %APPDATA%\CMS5000\connection.json 에 저장한다.
    /// 앱 설치 폴더가 아닌 사용자 영구 경로이므로 재빌드·업데이트 후에도 유지된다.
    /// </summary>
    private static void SaveConnection(string host, string db, string user, string pw)
    {
        try
        {
            var path = UserConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteString("Host", host);
                w.WriteString("Database", db);
                w.WriteString("Username", user);
                w.WriteString("Password", ProtectPassword(pw));   // DPAPI 암호화 저장
                w.WriteEndObject();
            }
            File.WriteAllBytes(path, ms.ToArray());
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

    /// <summary>
    /// 현재 접속된 PostgreSQL 서버에서 "CMS5000 프로그램과 관련된" DB 이름만 조회한다.
    /// 판별 기준: public 스키마에 CMS 시그니처 테이블(general_channel)이 존재하는 DB.
    /// (codegen·postgres 등 무관한 DB는 자동 제외. 이름이 아닌 스키마로 판별하므로 정확.)
    /// Connection > Database Connect 다이얼로그의 Database 콤보박스 채우기에 사용.
    /// </summary>
    public static async Task<List<string>> GetDatabaseNamesAsync()
    {
        // 1) 접속 가능한 후보 DB 목록(템플릿/접속불가 제외)
        var candidates = new List<string>();
        await using (var conn = await DataSource.OpenConnectionAsync())
        await using (var cmd  = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datistemplate = false AND datallowconn ORDER BY datname", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync())
                candidates.Add(r.GetString(0));

        // 2) 각 후보에 접속해 CMS 시그니처 테이블 존재여부 검사
        var result = new List<string>();
        foreach (var db in candidates)
        {
            try
            {
                var csb = new NpgsqlConnectionStringBuilder
                {
                    Host = CurrentHost, Database = db, Username = CurrentUsername, Password = CurrentPassword,
                    Timeout = 5, CommandTimeout = 5, Pooling = false,
                };
                await using var c = new NpgsqlConnection(csb.ConnectionString);
                await c.OpenAsync();
                await using var cmd = new NpgsqlCommand(
                    "SELECT to_regclass('public.general_channel') IS NOT NULL", c);
                if (await cmd.ExecuteScalarAsync() is bool ok && ok)
                    result.Add(db);
            }
            catch { /* 접근 불가/검사 실패 DB는 목록에서 제외 */ }
        }
        return result;
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
