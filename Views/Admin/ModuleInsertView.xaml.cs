using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class ModuleInsertView : Window
{
    public bool Inserted => (DataContext as ModuleInsertViewModel)?.Modified ?? false;

    public ModuleInsertView(DeviceTreeNode rackNode)
    {
        InitializeComponent();
        var vm = new ModuleInsertViewModel(rackNode);
        vm.CloseRequested += Close;
        DataContext = vm;
    }
}
