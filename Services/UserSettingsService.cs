using CMS5000.Models;

namespace CMS5000.Services;

public static class UserSettingsService
{
    public static async Task SaveFontSizeAsync(FontSizePreset preset)
    {
        var user = AuthService.CurrentUser;
        if (user == null) return;
        try
        {
            user.FontSize = preset.ToString();
            await SupabaseService.Client.From<CmsUser>().Update(user);
        }
        catch { }
    }
}
