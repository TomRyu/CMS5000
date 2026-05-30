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

        // 무차별 대입 완화: 잠금 상태면 즉시 차단
        var remainingLock = LoginThrottleService.GetRemainingLock(username);
        if (remainingLock != null)
        {
            var mins = Math.Max(1, (int)Math.Ceiling(remainingLock.Value.TotalMinutes));
            return (false, $"로그인 시도가 많아 일시적으로 잠겼습니다. 약 {mins}분 후 다시 시도하세요.");
        }

        try
        {
            var response = await SupabaseService.Client
                .From<CmsUser>()
                .Filter("username", Constants.Operator.Equals, username)
                .Get();

            if (response.Models.Count == 0)
            {
                LoginThrottleService.RecordFailure(username);
                return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");
            }

            var user = response.Models[0];

            if (!user.IsActive)
                return (false, "비활성화된 계정입니다. 관리자에게 문의하세요.");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                LoginThrottleService.RecordFailure(username);
                return (false, "아이디 또는 비밀번호가 올바르지 않습니다.");
            }

            LoginThrottleService.Reset(username);
            CurrentUser = user;
            await RecordLogAsync(user, "login");
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"서버 연결에 실패했습니다: {ex.Message}");
        }
    }

    /// <summary>로그인한 본인의 비밀번호 변경.</summary>
    public static async Task<(bool Success, string Error)> ChangePasswordAsync(
        string currentPassword, string newPassword, string confirmPassword)
    {
        if (CurrentUser == null)
            return (false, "로그인 상태가 아닙니다.");
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return (false, "모든 항목을 입력하세요.");
        if (newPassword.Length < 8)
            return (false, "새 비밀번호는 8자 이상이어야 합니다.");
        if (newPassword != confirmPassword)
            return (false, "새 비밀번호가 일치하지 않습니다.");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, CurrentUser.PasswordHash))
            return (false, "현재 비밀번호가 올바르지 않습니다.");
        if (BCrypt.Net.BCrypt.Verify(newPassword, CurrentUser.PasswordHash))
            return (false, "기존과 다른 비밀번호를 사용하세요.");

        try
        {
            CurrentUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 11);
            await SupabaseService.Client.From<CmsUser>().Update(CurrentUser);
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"비밀번호 변경 실패: {ex.Message}");
        }
    }

    public static void Logout()
    {
        if (CurrentUser != null)
            _ = RecordLogAsync(CurrentUser, "logout");
        CurrentUser = null;
    }

    private static async Task RecordLogAsync(CmsUser user, string action)
    {
        try
        {
            var log = new LoginLog
            {
                UserId      = user.Id,
                Username    = user.Username,
                DisplayName = user.DisplayName,
                Role        = user.Role,
                Action      = action,
                LoggedAt    = DateTime.UtcNow
            };
            await SupabaseService.Client.From<LoginLog>().Insert(log);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginLog] {action} 기록 실패: {ex.Message}");
        }
    }

    public static UserRole GetCurrentRole() => CurrentUser?.Role switch
    {
        "Admin"       => UserRole.Admin,
        "Expert"      => UserRole.Expert,
        "Maintenance" => UserRole.Maintenance,
        _             => UserRole.Operator
    };
}
