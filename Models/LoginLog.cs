using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;

namespace CMS5000.Models;

[Table("cms_login_logs")]
public class LoginLog : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = "";

    [Column("user_id")]
    public string UserId { get; set; } = "";

    [Column("username")]
    public string Username { get; set; } = "";

    [Column("display_name")]
    public string DisplayName { get; set; } = "";

    [Column("role")]
    public string Role { get; set; } = "";

    [Column("action")]
    public string Action { get; set; } = "";

    [Column("logged_at")]
    public DateTime LoggedAt { get; set; }

    [JsonIgnore] public string ActionDisplay  => Action == "login" ? "로그인" : "로그아웃";
    [JsonIgnore] public bool   IsLogin        => Action == "login";
    [JsonIgnore] public string LoggedAtDisplay => LoggedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
