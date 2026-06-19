using System.Windows;
using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class HwConfigView : Window
{
    private readonly HwConfigViewModel _vm;

    public HwConfigView(DeviceTreeNode rackNode)
    {
        InitializeComponent();
        _vm = new HwConfigViewModel(rackNode);
        DataContext = _vm;
        Closed += (_, _) => _vm.OnClosed();
    }
}
