using CMS5000.Models;

namespace CMS5000.Services;

public static class AuthService
{
    public static CmsUser? CurrentUser { get; private set; }

    private record LoginResponse(string Token, CmsUser User);

    public static async Task<(bool Success, string Error)> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "아이디와 비밀번호를 입력하세요.");

        // 서버(함수)가 bcrypt 검증·실패 잠금·이력 기록을 모두 처리한다.
        var (data, error) = await ApiService.PostAsync<LoginResponse>(
            "/login", new { username, password }, auth: false);

        if (error != null)
            return (false, error);
        if (data?.User == null || string.IsNullOrEmpty(data.Token))
            return (false, "로그인 응답이 올바르지 않습니다.");

        ApiService.AccessToken = data.Token;
        CurrentUser = data.User;
        return (true, "");
    }

    public static void Logout()
    {
        // 로그아웃 이력은 서버에 기록(실패해도 무시)
        if (CurrentUser != null && !string.IsNullOrEmpty(ApiService.AccessToken))
            _ = ApiService.PostOkAsync("/logout-log", null);
        CurrentUser = null;
        ApiService.AccessToken = null;
    }

    /// <summary>로그인한 본인의 비밀번호 변경. 검증은 서버에서 수행.</summary>
    public static async Task<(bool Success, string Error)> ChangePasswordAsync(
        string currentPassword, string newPassword, string confirmPassword)
    {
        if (CurrentUser == null)
            return (false, "로그인 상태가 아닙니다.");

        var (ok, error) = await ApiService.PostOkAsync("/change-password",
            new { currentPassword, newPassword, confirmPassword });
        return ok ? (true, "") : (false, error);
    }

    public static UserRole GetCurrentRole() => CurrentUser?.Role switch
    {
        "Admin"       => UserRole.Admin,
        "Expert"      => UserRole.Expert,
        "Maintenance" => UserRole.Maintenance,
        _             => UserRole.Operator
    };
}
