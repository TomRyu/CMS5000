using System;
using System.Threading.Tasks;

namespace CMS5000.Services;

/// <summary>
/// 중요사항 실행 비밀번호(DownLoad/UpLoad 등) 관리.
/// DB(public.cms_app_settings)에 BCrypt 해시로 저장 — 같은 DB에 연결된 모든 PC가 공유한다.
/// 최초 미설정 상태면 IsSetAsync()=false 이고, 첫 사용 시 설정받는다.
/// </summary>
public static class SecurityService
{
    private const string Key = "critical_action_password_hash";

    /// <summary>비밀번호가 설정돼 있는지.</summary>
    public static async Task<bool> IsSetAsync()
        => await GetHashAsync() is { Length: > 0 };

    public static async Task<string?> GetHashAsync()
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM public.cms_app_settings WHERE key=@k";
        cmd.Parameters.AddWithValue("k", Key);
        return (await cmd.ExecuteScalarAsync()) as string;
    }

    /// <summary>입력 비밀번호가 저장된 해시와 일치하는지.</summary>
    public static async Task<bool> VerifyAsync(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        var hash = await GetHashAsync();
        if (string.IsNullOrEmpty(hash)) return false;
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }

    /// <summary>비밀번호 설정/변경(BCrypt 해시로 upsert).</summary>
    public static async Task SetAsync(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) throw new ArgumentException("비밀번호가 비어있습니다.");
        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO public.cms_app_settings(key, value, updated_at)
            VALUES(@k, @v, now())
            ON CONFLICT (key) DO UPDATE SET value=EXCLUDED.value, updated_at=now()
            """;
        cmd.Parameters.AddWithValue("k", Key);
        cmd.Parameters.AddWithValue("v", hash);
        await cmd.ExecuteNonQueryAsync();
    }
}
