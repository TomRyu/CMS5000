using System.ComponentModel;
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
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel old)
            old.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is MainViewModel vm)
        {
            vm.PropertyChanged += OnVmPropertyChanged;
            // 저장된 비밀번호가 있으면 PasswordBox에 반영
            PwdBox.Password = vm.LoginPassword;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm) return;
        // 비밀번호 숨김으로 전환 시 PasswordBox 동기화
        if (e.PropertyName == nameof(MainViewModel.IsPasswordVisible) && !vm.IsPasswordVisible)
            PwdBox.Password = vm.LoginPassword;
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
