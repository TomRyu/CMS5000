using System.Windows;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class ReferenceConfigView : Window
{
    public bool Modified => (DataContext as ReferenceConfigViewModel)?.Modified ?? false;

    public ReferenceConfigView(int stationId, int rackId, int moduleId, int channelId)
    {
        InitializeComponent();
        var vm = new ReferenceConfigViewModel(stationId, rackId, moduleId, channelId);
        vm.CloseRequested += Close;
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
