namespace CMS5000.Models;

public class CmsUser
{
    public string   Id           { get; set; } = "";
    public string   Username     { get; set; } = "";
    public string   PasswordHash { get; set; } = "";
    public string   Role         { get; set; } = "";
    public string   DisplayName  { get; set; } = "";
    public bool     IsActive     { get; set; } = true;
    public DateTime CreatedAt    { get; set; }
    public string   FontSize     { get; set; } = "Medium";
}
