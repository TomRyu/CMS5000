using System.Windows;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RelayConfigView : Window
{
    public bool Saved => (DataContext as RelayConfigViewModel)?.Modified ?? false;

    public RelayConfigView(int stationId, int rackId, int moduleId, int channelId, string name, int channelIndex)
    {
        InitializeComponent();
        var vm = new RelayConfigViewModel(stationId, rackId, moduleId, channelId, name, channelIndex);
        vm.CloseRequested += Close;
        DataContext = vm;
    }
}
