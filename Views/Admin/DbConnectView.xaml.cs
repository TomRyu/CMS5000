using System.Windows;
using System.Windows.Controls;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class DbConnectView : Window
{
    private readonly DbConnectViewModel _vm;

    public bool Connected => _vm.Connected;

    public DbConnectView()
    {
        InitializeComponent();
        _vm = new DbConnectViewModel();
        _vm.CloseRequested += Close;
        DataContext = _vm;

        // PasswordBox 는 바인딩이 안 되므로 초기값을 코드로 채운다.
        PwBox.Password = _vm.Password;
    }

    private void PwBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is DbConnectViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }
}
