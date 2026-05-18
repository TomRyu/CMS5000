using CMS5000.Models;
using Postgrest;

namespace CMS5000.Services;

public static class AuthService
{
    public static CmsUser? CurrentUser { get; private set; }

    public static async Task<(bool Success, string Error)> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "아이디와 비밀번호를 입력하세요.");

        try
        {
            var response = await SupabaseService.Client
                .From<CmsUser>()
                .Filter("username", Constants.Operator.Equals, username)
                .Get();

            if (response.Models.Count == 0)
                return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");

            var user = response.Models[0];

            if (!user.IsActive)
                return (false, "비활성화된 계정입니다. 관리자에게 문의하세요.");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");

            CurrentUser = user;
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"서버 연결에 실패했습니다: {ex.Message}");
        }
    }

    public static void Logout() => CurrentUser = null;

    public static UserRole GetCurrentRole() => CurrentUser?.Role switch
    {
        "Admin"       => UserRole.Admin,
        "Expert"      => UserRole.Expert,
        "Maintenance" => UserRole.Maintenance,
        _             => UserRole.Operator
    };
}
