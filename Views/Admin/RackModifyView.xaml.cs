using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RackModifyView : Window
{
    public bool Modified => (DataContext as RackModifyViewModel)?.Modified ?? false;

    public RackModifyView(DeviceTreeNode rackNode)
    {
        InitializeComponent();
        var vm = new RackModifyViewModel(rackNode);
        vm.CloseRequested += Close;
        DataContext = vm;
    }
}
