using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RackCopyView : Window
{
    public bool Copied => (DataContext as RackCopyViewModel)?.Modified ?? false;

    public RackCopyView(DeviceTreeNode rackNode)
    {
        InitializeComponent();
        var vm = new RackCopyViewModel(rackNode);
        vm.CloseRequested += Close;
        DataContext = vm;
    }
}
