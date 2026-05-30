using System.Windows.Controls;
using CMS5000.ViewModels.Settings;

namespace CMS5000.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void ChangePwButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var success = await vm.ChangePasswordAsync(
            CurrentPwBox.Password, NewPwBox.Password, ConfirmPwBox.Password);

        if (success)
        {
            CurrentPwBox.Clear();
            NewPwBox.Clear();
            ConfirmPwBox.Clear();
        }
    }
}
