using Postgrest.Attributes;
using Postgrest.Models;

namespace CMS5000.Models;

[Table("cms_users")]
public class CmsUser : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = "";

    [Column("username")]
    public string Username { get; set; } = "";

    [Column("password_hash")]
    public string PasswordHash { get; set; } = "";

    [Column("role")]
    public string Role { get; set; } = "";

    [Column("display_name")]
    public string DisplayName { get; set; } = "";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("font_size")]
    public string FontSize { get; set; } = "Medium";
}
