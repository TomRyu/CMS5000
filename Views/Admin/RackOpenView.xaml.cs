using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RackOpenView : Window
{
    public bool WantsModify { get; private set; }

    public RackOpenView(DeviceTreeNode rackNode)
    {
        InitializeComponent();
        var vm = new RackOpenViewModel(rackNode);
        vm.ModifyRequested += () => { WantsModify = true; Close(); };
        DataContext = vm;
    }
}
