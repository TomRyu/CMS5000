using System.Windows.Controls;

namespace CMS5000.Views.Admin;

/// <summary>
/// RACK Modify 폼(3탭). 모달 창(<see cref="RackModifyView"/>)과
/// 장비구성 탭 양쪽에서 재사용한다. DataContext = RackModifyViewModel.
/// </summary>
public partial class RackModifyControl : UserControl
{
    public RackModifyControl() => InitializeComponent();
}
