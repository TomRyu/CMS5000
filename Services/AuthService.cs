using CMS5000.Models;
using Npgsql;

namespace CMS5000.Services;

public static class AuthService
{
    // 브루트포스 잠금 설정
    private const int MaxAttempts = 5;
    private const int LockoutMin  = 5;
    private const int WindowMin   = 15;

    public static CmsUser? CurrentUser { get; private set; }

    public static async Task<(bool Success, string Error)> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "아이디와 비밀번호를 입력하세요.");

        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        var lockSec = await GetRemainingLockSecondsAsync(conn, username);
        if (lockSec.HasValue)
        {
            var mins = Math.Max(1, (int)Math.Ceiling(lockSec.Value / 60.0));
            return (false, $"로그인 시도가 많아 일시적으로 잠겼습니다. 약 {mins}분 후 다시 시도하세요.");
        }

        var user = await FetchUserAsync(conn, username);

        if (user == null)
        {
            await RecordFailureAsync(conn, username);
            return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");
        }
        if (!user.IsActive)
            return (false, "비활성화된 계정입니다. 관리자에게 문의하세요.");
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            await RecordFailureAsync(conn, username);
            return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");
        }

        await ResetAttemptsAsync(conn, username);
        await LoginLogService.RecordAsync(conn, user, "login");
        CurrentUser = user;
        return (true, "");
    }

    public static void Logout()
    {
        if (CurrentUser != null)
            _ = LoginLogService.RecordAsync(CurrentUser, "logout");
        CurrentUser = null;
    }

    public static async Task<(bool Success, string Error)> ChangePasswordAsync(
        string currentPassword, string newPassword, string confirmPassword)
    {
        if (CurrentUser == null) return (false, "로그인 상태가 아닙니다.");
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return (false, "모든 항목을 입력하세요.");
        if (newPassword.Length < 8)    return (false, "새 비밀번호는 8자 이상이어야 합니다.");
        if (newPassword != confirmPassword) return (false, "새 비밀번호가 일치하지 않습니다.");

        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        string? currentHash;
        await using (var cmd = new NpgsqlCommand(
            "SELECT password_hash FROM cms_users WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", Guid.Parse(CurrentUser.Id));
            currentHash = (string?)await cmd.ExecuteScalarAsync();
        }

        if (currentHash == null) return (false, "사용자를 찾을 수 없습니다.");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, currentHash))
            return (false, "현재 비밀번호가 올바르지 않습니다.");
        if (BCrypt.Net.BCrypt.Verify(newPassword, currentHash))
            return (false, "기존과 다른 비밀번호를 사용하세요.");

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
        await using (var cmd = new NpgsqlCommand(
            "UPDATE cms_users SET password_hash = @h WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("h",  newHash);
            cmd.Parameters.AddWithValue("id", Guid.Parse(CurrentUser.Id));
            await cmd.ExecuteNonQueryAsync();
        }
        return (true, "");
    }

    public static UserRole GetCurrentRole() => CurrentUser?.Role switch
    {
        "Admin"       => UserRole.Admin,
        "Expert"      => UserRole.Expert,
        "Maintenance" => UserRole.Maintenance,
        _             => UserRole.Operator
    };

    // ── DB 조회 헬퍼 ────────────────────────────────────────────────────
    private static async Task<CmsUser?> FetchUserAsync(NpgsqlConnection conn, string username)
    {
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, username, password_hash, role, display_name, is_active, created_at, font_size
            FROM cms_users WHERE username = @u", conn);
        cmd.Parameters.AddWithValue("u", username);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new CmsUser
        {
            Id           = r.GetGuid(0).ToString(),
            Username     = r.GetString(1),
            PasswordHash = r.GetString(2),
            Role         = r.GetString(3),
            DisplayName  = r.GetString(4),
            IsActive     = r.GetBoolean(5),
            CreatedAt    = DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc),
            FontSize     = r.IsDBNull(7) ? "Medium" : r.GetString(7),
        };
    }

    // ── 스로틀링 ────────────────────────────────────────────────────────
    private static async Task<double?> GetRemainingLockSecondsAsync(NpgsqlConnection conn, string username)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT locked_until FROM cms_login_attempts WHERE username = @u", conn);
        cmd.Parameters.AddWithValue("u", username);
        var val = await cmd.ExecuteScalarAsync();
        if (val == null || val == DBNull.Value) return null;
        var remaining = (DateTime.SpecifyKind((DateTime)val, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds;
        return remaining > 0 ? remaining : null;
    }

    private static async Task RecordFailureAsync(NpgsqlConnection conn, string username)
    {
        int failCount = 0;
        DateTime firstFail = DateTime.UtcNow;

        await using (var sel = new NpgsqlCommand(
            "SELECT fail_count, first_fail_at FROM cms_login_attempts WHERE username = @u", conn))
        {
            sel.Parameters.AddWithValue("u", username);
            await using var r = await sel.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                failCount = r.GetInt32(0);
                firstFail = DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc);
            }
        }

        failCount++;
        if (firstFail < DateTime.UtcNow.AddMinutes(-WindowMin))
        {
            failCount = 1;
            firstFail = DateTime.UtcNow;
        }

        var lockedUntil = failCount >= MaxAttempts
            ? (object)DateTime.UtcNow.AddMinutes(LockoutMin)
            : DBNull.Value;

        await using var ups = new NpgsqlCommand(@"
            INSERT INTO cms_login_attempts (username, fail_count, first_fail_at, locked_until)
            VALUES (@u, @f, @fa, @lu)
            ON CONFLICT (username) DO UPDATE
              SET fail_count = @f, first_fail_at = @fa, locked_until = @lu", conn);
        ups.Parameters.AddWithValue("u",  username);
        ups.Parameters.AddWithValue("f",  failCount);
        ups.Parameters.AddWithValue("fa", firstFail);
        ups.Parameters.AddWithValue("lu", lockedUntil);
        await ups.ExecuteNonQueryAsync();
    }

    private static async Task ResetAttemptsAsync(NpgsqlConnection conn, string username)
    {
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM cms_login_attempts WHERE username = @u", conn);
        cmd.Parameters.AddWithValue("u", username);
        await cmd.ExecuteNonQueryAsync();
    }
}
