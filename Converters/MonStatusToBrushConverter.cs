using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CMS5000.Models.Monitoring;

namespace CMS5000.Converters;

/// <summary>MonStatus → 상태 색 브러시(LED·뱃지·막대 색).</summary>
public class MonStatusToBrushConverter : IValueConverter
{
    public static SolidColorBrush BrushFor(MonStatus s) => s switch
    {
        MonStatus.Good    => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // green
        MonStatus.Warning => new SolidColorBrush(Color.FromRgb(0xEA, 0xB3, 0x08)), // yellow
        MonStatus.Alert   => new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)), // orange
        MonStatus.Alarm   => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // red
        _                 => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), // gray
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BrushFor(value is MonStatus s ? s : MonStatus.None);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
