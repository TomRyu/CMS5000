using CMS5000.Models;
using Npgsql;

namespace CMS5000.Services;

public static class UserService
{
    public static async Task<List<CmsUser>> GetAllAsync()
    {
        var list = new List<CmsUser>();
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            SELECT id, username, role, display_name, is_active, created_at, font_size
            FROM cms_users ORDER BY created_at", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(ReadUser(r));
        }
        return list;
    }

    public static async Task<CmsUser> CreateAsync(
        string username, string displayName, string role, string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, 11);
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(@"
            INSERT INTO cms_users (username, password_hash, role, display_name, is_active)
            VALUES (@u, @h, @r, @d, true)
            RETURNING id, username, role, display_name, is_active, created_at, font_size", conn);
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("h", hash);
        cmd.Parameters.AddWithValue("r", role);
        cmd.Parameters.AddWithValue("d", displayName);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return ReadUser(reader);
    }

    public static async Task UpdateAsync(
        string id, string displayName, string role, string? newPassword)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
            await using var cmd = new NpgsqlCommand(@"
                UPDATE cms_users SET display_name = @d, role = @r, password_hash = @h
                WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("d",  displayName);
            cmd.Parameters.AddWithValue("r",  role);
            cmd.Parameters.AddWithValue("h",  hash);
            cmd.Parameters.AddWithValue("id", Guid.Parse(id));
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE cms_users SET display_name = @d, role = @r
                WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("d",  displayName);
            cmd.Parameters.AddWithValue("r",  role);
            cmd.Parameters.AddWithValue("id", Guid.Parse(id));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public static async Task SetActiveAsync(string id, bool isActive)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(
            "UPDATE cms_users SET is_active = @a WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("a",  isActive);
        cmd.Parameters.AddWithValue("id", Guid.Parse(id));
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task DeleteAsync(string id)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(
            "DELETE FROM cms_users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", Guid.Parse(id));
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task SetFontSizeAsync(string userId, string fontSize)
    {
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();
        await using var cmd  = new NpgsqlCommand(
            "UPDATE cms_users SET font_size = @f WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("f",  fontSize);
        cmd.Parameters.AddWithValue("id", Guid.Parse(userId));
        await cmd.ExecuteNonQueryAsync();
    }

    private static CmsUser ReadUser(NpgsqlDataReader r) => new()
    {
        Id          = r.GetGuid(0).ToString(),
        Username    = r.GetString(1),
        Role        = r.GetString(2),
        DisplayName = r.GetString(3),
        IsActive    = r.GetBoolean(4),
        CreatedAt   = DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc),
        FontSize    = r.IsDBNull(6) ? "Medium" : r.GetString(6),
    };
}
