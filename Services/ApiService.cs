using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace CMS5000.Services;

/// <summary>
/// Edge Functions(`api`) 호출 계층. 클라이언트는 service_role 대신 공개 anon 키만 보유하고,
/// 민감 작업은 모두 서버(함수)에서 수행한다. 로그인 시 받은 JWT를 이후 요청에 Bearer로 첨부.
/// </summary>
public static class ApiService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static string _baseUrl = "";
    private static string _anonKey = "";

    /// <summary>로그인 성공 시 받은 세션 토큰. 로그아웃 시 null.</summary>
    public static string? AccessToken { get; set; }

    public static void Initialize()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("appsettings.json 파일을 찾을 수 없습니다.", configPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        var sb = doc.RootElement.GetProperty("Supabase");
        var url = sb.GetProperty("Url").GetString()
            ?? throw new InvalidOperationException("Supabase Url이 설정되지 않았습니다.");
        _anonKey = sb.GetProperty("AnonKey").GetString()
            ?? throw new InvalidOperationException("Supabase AnonKey가 설정되지 않았습니다.");
        _baseUrl = $"{url.TrimEnd('/')}/functions/v1/api";
    }

    /// <summary>함수 헬스체크. 시작 시 연결 확인용.</summary>
    public static async Task EnsureReachableAsync()
    {
        var (_, error) = await GetAsync<HealthDto>("/health", auth: false);
        if (error != null) throw new HttpRequestException(error);
    }

    public static Task<(T? Data, string? Error)> GetAsync<T>(string path, bool auth = true)
        => SendAsync<T>(HttpMethod.Get, path, null, auth);

    public static Task<(T? Data, string? Error)> PostAsync<T>(string path, object? body, bool auth = true)
        => SendAsync<T>(HttpMethod.Post, path, body, auth);

    /// <summary>ok 응답만 필요한 호출.</summary>
    public static async Task<(bool Ok, string Error)> PostOkAsync(string path, object? body, bool auth = true)
    {
        var (data, error) = await SendAsync<OkDto>(HttpMethod.Post, path, body, auth);
        return error == null ? (true, "") : (false, error);
    }

    private static async Task<(T? Data, string? Error)> SendAsync<T>(
        HttpMethod method, string path, object? body, bool auth)
    {
        if (string.IsNullOrEmpty(_baseUrl))
            return (default, "API가 초기화되지 않았습니다.");

        try
        {
            using var req = new HttpRequestMessage(method, _baseUrl + path);
            req.Headers.TryAddWithoutValidation("apikey", _anonKey);
            if (auth)
            {
                if (string.IsNullOrEmpty(AccessToken))
                    return (default, "로그인이 필요합니다.");
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {AccessToken}");
            }
            if (body != null)
                req.Content = JsonContent.Create(body);

            using var resp = await Http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return (default, ExtractError(text) ?? $"요청이 실패했습니다 ({(int)resp.StatusCode}).");

            if (string.IsNullOrWhiteSpace(text))
                return (default, null);

            return (JsonSerializer.Deserialize<T>(text, JsonOpts), null);
        }
        catch (Exception ex)
        {
            return (default, $"서버 연결에 실패했습니다: {ex.Message}");
        }
    }

    private static string? ExtractError(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var e))
                return e.GetString();
        }
        catch { }
        return null;
    }

    private record HealthDto(bool Ok);
    private record OkDto(bool Ok);
}
