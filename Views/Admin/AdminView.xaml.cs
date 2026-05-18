using System.Windows;
using System.Windows.Controls;
using CMS5000.ViewModels;

namespace CMS5000.Views.Admin;

public partial class AdminView : UserControl
{
    public AdminView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is AdminViewModel vm)
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AdminViewModel.IsEditing) && !vm.IsEditing)
                    AdminPwdBox.Clear();
            };
    }

    private void AdminPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
            vm.NewPassword = ((PasswordBox)sender).Password;
    }
}
