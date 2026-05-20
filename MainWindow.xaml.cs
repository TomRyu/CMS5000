using System.ComponentModel;
using System.Windows;
using CMS5000.ViewModels;

namespace CMS5000;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var result = MessageBox.Show(
            "프로그램을 종료하시겠습니까?",
            "CMS-5000 종료",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
            e.Cancel = true;

        base.OnClosing(e);
    }
}