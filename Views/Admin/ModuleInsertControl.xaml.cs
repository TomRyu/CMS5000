using System.Windows;
using System.Windows.Controls;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class ModuleInsertControl : UserControl
{
    private ModuleInsertViewModel? _vm;

    public ModuleInsertControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null) _vm.ConfigChannelRequested -= OnConfigChannel;
        _vm = DataContext as ModuleInsertViewModel;
        if (_vm != null) _vm.ConfigChannelRequested += OnConfigChannel;
    }

    private void OnConfigChannel(int channelId)
    {
        if (_vm == null) return;
        var dlg = new ReferenceConfigView(_vm.StationId, _vm.RackId, _vm.ModuleId, channelId)
        {
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();
    }
}
