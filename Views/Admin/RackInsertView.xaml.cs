using System.Windows;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class RackInsertView : Window
{
    public RackInsertView(int stationId, int nextRackId)
    {
        InitializeComponent();
        var vm = new RackInsertViewModel(stationId, nextRackId);
        vm.CloseRequested += Close;
        DataContext = vm;
    }

    public bool Modified  => (DataContext as RackInsertViewModel)?.Modified  ?? false;
    public int  SavedRackId => (DataContext as RackInsertViewModel)?.SavedRackId ?? 0;
}
