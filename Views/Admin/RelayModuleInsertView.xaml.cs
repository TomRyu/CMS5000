using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RelayModuleInsertView : Window
{
    public bool Inserted => (DataContext as RelayModuleInsertViewModel)?.Modified ?? false;

    public RelayModuleInsertView(DeviceTreeNode rackNode)
    {
        InitializeComponent();
        var vm = new RelayModuleInsertViewModel(rackNode);
        vm.CloseRequested += Close;
        DataContext = vm;
    }
}
