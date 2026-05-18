using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CMS5000.ViewModels;

namespace CMS5000.Views.Shell;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.LoginPassword = ((PasswordBox)sender).Password;
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            vm.LoginCommand.Execute(null);
    }
}
