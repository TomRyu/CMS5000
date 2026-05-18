using System.IO;
using System.Text.Json;
using Supabase;

namespace CMS5000.Services;

public static class SupabaseService
{
    private static Client? _client;
    public static Client Client => _client ?? throw new InvalidOperationException("Supabase가 초기화되지 않았습니다.");

    public static async Task InitializeAsync()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("appsettings.json 파일을 찾을 수 없습니다.", configPath);

        var json = await File.ReadAllTextAsync(configPath);
        var doc = JsonDocument.Parse(json);
        var sb = doc.RootElement.GetProperty("Supabase");
        var url = sb.GetProperty("Url").GetString()
            ?? throw new InvalidOperationException("Supabase URL이 설정되지 않았습니다.");
        var key = sb.GetProperty("ServiceRoleKey").GetString()
            ?? throw new InvalidOperationException("Supabase ServiceRoleKey가 설정되지 않았습니다.");

        var options = new SupabaseOptions { AutoRefreshToken = false, AutoConnectRealtime = false };
        _client = new Client(url, key, options);
        await _client.InitializeAsync();
    }
}
