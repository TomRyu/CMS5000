namespace CMS5000.Models;

public class LoginLog
{
    public string   Id          { get; set; } = "";
    public string   UserId      { get; set; } = "";
    public string   Username    { get; set; } = "";
    public string   DisplayName { get; set; } = "";
    public string   Role        { get; set; } = "";
    public string   Action      { get; set; } = "";
    public DateTime LoggedAt    { get; set; }

    public string ActionDisplay   => Action == "login" ? "로그인" : "로그아웃";
    public bool   IsLogin         => Action == "login";
    public string LoggedAtDisplay => LoggedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
