using System.Windows;
using CMS5000.Models;

namespace CMS5000.Services;

public static class FontSizeManager
{
    public static void Apply(FontSizePreset preset)
    {
        var (base_, input, small, xsmall) = preset switch
        {
            FontSizePreset.Small => (13.0, 12.0, 11.0, 8.0),
            FontSizePreset.Large => (17.0, 16.0, 13.0, 11.0),
            _                   => (15.0, 14.0, 12.0, 10.0),
        };

        var res = Application.Current.Resources;
        res["FontSizeBase"]   = base_;
        res["FontSizeInput"]  = input;
        res["FontSizeSmall"]  = small;
        res["FontSizeXSmall"] = xsmall;
    }

    public static FontSizePreset Current =>
        Enum.TryParse<FontSizePreset>(AuthService.CurrentUser?.FontSize, out var p) ? p : FontSizePreset.Medium;
}
