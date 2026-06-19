using CMS5000.Models;
using Npgsql;

namespace CMS5000.Services;

public static class LoginLogService
{
    public static async Task RecordAsync(CmsUser user, string action)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await RecordAsync(conn, user, action);
    }

    internal static async Task RecordAsync(NpgsqlConnection conn, CmsUser user, string action)
    {
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO cms_login_logs (user_id, username, display_name, role, action, logged_at)
            VALUES (@uid, @u, @d, @r, @a, now())", conn);
        cmd.Parameters.AddWithValue("uid", Guid.Parse(user.Id));
        cmd.Parameters.AddWithValue("u",   user.Username);
        cmd.Parameters.AddWithValue("d",   user.DisplayName);
        cmd.Parameters.AddWithValue("r",   user.Role);
        cmd.Parameters.AddWithValue("a",   action);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<List<LoginLog>> GetRecentAsync(int limit = 200)
    {
        var list = new List<LoginLog>();
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, user_id, username, display_name, role, action, logged_at
            FROM cms_login_logs
            ORDER BY logged_at DESC
            LIMIT @lim", conn);
        cmd.Parameters.AddWithValue("lim", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new LoginLog
            {
                Id          = r.GetGuid(0).ToString(),
                UserId      = r.IsDBNull(1) ? "" : r.GetGuid(1).ToString(),
                Username    = r.GetString(2),
                DisplayName = r.IsDBNull(3) ? "" : r.GetString(3),
                Role        = r.IsDBNull(4) ? "" : r.GetString(4),
                Action      = r.GetString(5),
                LoggedAt    = DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc),
            });
        }
        return list;
    }
}
