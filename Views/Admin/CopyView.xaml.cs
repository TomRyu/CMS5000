using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class CopyView : Window
{
    public bool Copied => (DataContext as CopyViewModel)?.Modified ?? false;

    public CopyView(DeviceTreeNode node)
    {
        InitializeComponent();
        var vm = new CopyViewModel(node);
        vm.CloseRequested += Close;
        DataContext = vm;
    }
}
