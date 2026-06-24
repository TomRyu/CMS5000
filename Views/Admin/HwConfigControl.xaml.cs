using System.Windows.Controls;

namespace CMS5000.Views.Admin;

/// <summary>H/W Config 폼. 장비구성 탭에서 호스팅. DataContext = HwConfigViewModel.</summary>
public partial class HwConfigControl : UserControl
{
    public HwConfigControl() => InitializeComponent();

    // 통신 로그(TextBox)에 새 줄이 추가되면 맨 아래로 자동 스크롤.
    private void Log_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) tb.ScrollToEnd();
    }
}
