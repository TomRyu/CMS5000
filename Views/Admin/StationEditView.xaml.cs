using CMS5000.Models;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class StationEditView : System.Windows.Window
{
    public StationEditView(int nextId)
    {
        InitializeComponent();
        var vm = new StationEditViewModel(nextId);
        vm.CloseRequested += () => Close();
        DataContext = vm;
    }

    public StationEditView(DeviceStation station)
    {
        InitializeComponent();
        var vm = new StationEditViewModel(station);
        vm.CloseRequested += () => Close();
        DataContext = vm;
    }

    public bool Modified => (DataContext as StationEditViewModel)?.Modified ?? false;
    public int  SavedId  => (DataContext as StationEditViewModel)?.SavedId  ?? 0;
}
