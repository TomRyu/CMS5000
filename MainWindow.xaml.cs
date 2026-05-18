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
}