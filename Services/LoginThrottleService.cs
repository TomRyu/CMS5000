using System.IO;
using System.Text.Json;

namespace CMS5000.Services;

/// <summary>
/// 로그인 실패 잠금(무차별 대입 완화). 머신 단위로 아이디별 실패 횟수를 세고,
/// 한도 초과 시 일정 시간 잠근다. 재시작으로 우회되지 않도록 로컬 파일에 보존.
///
/// ⚠️ 한계: 클라이언트(이 PC)에서만 동작하는 방어다. DB에 직접 붙는 현재 구조상
/// 완전한 서버측 차단이 아니므로, 추후 백엔드/Edge Function 도입 시 서버측으로 옮겨야 한다.
/// </summary>
public static class LoginThrottleService
{
    private const int MaxAttempts        = 5;   // 잠금까지 허용 실패 횟수
    private const int LockoutMinutes     = 5;   // 잠금 지속 시간
    private const int AttemptWindowMin   = 15;  // 실패 횟수를 누적하는 시간 창

    private class Record
    {
        public int      FailCount   { get; set; }
        public DateTime FirstFailUtc { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
    }

    private static readonly object Gate = new();
    private static Dictionary<string, Record>? _records;

    private static string StatePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CMS5000", "login_throttle.json");

    private static Dictionary<string, Record> Records
    {
        get
        {
            if (_records != null) return _records;
            try
            {
                if (File.Exists(StatePath))
                    _records = JsonSerializer.Deserialize<Dictionary<string, Record>>(File.ReadAllText(StatePath));
            }
            catch { }
            return _records ??= new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(Records));
        }
        catch { }
    }

    /// <summary>현재 잠겨 있으면 남은 시간을 반환. 잠금 아님이면 null.</summary>
    public static TimeSpan? GetRemainingLock(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        lock (Gate)
        {
            if (!Records.TryGetValue(username, out var r) || r.LockedUntilUtc == null)
                return null;
            var remaining = r.LockedUntilUtc.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                Records.Remove(username);
                Persist();
                return null;
            }
            return remaining;
        }
    }

    public static void RecordFailure(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        lock (Gate)
        {
            if (!Records.TryGetValue(username, out var r))
                r = Records[username] = new Record { FirstFailUtc = DateTime.UtcNow };

            // 누적 창을 벗어났으면 카운트 초기화
            if (DateTime.UtcNow - r.FirstFailUtc > TimeSpan.FromMinutes(AttemptWindowMin))
            {
                r.FailCount = 0;
                r.FirstFailUtc = DateTime.UtcNow;
                r.LockedUntilUtc = null;
            }

            r.FailCount++;
            if (r.FailCount >= MaxAttempts)
                r.LockedUntilUtc = DateTime.UtcNow.AddMinutes(LockoutMinutes);

            Persist();
        }
    }

    /// <summary>로그인 성공 시 해당 아이디 기록 초기화.</summary>
    public static void Reset(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        lock (Gate)
        {
            if (Records.Remove(username))
                Persist();
        }
    }
}
