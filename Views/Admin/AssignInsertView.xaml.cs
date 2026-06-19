using System.Windows;
using CMS5000.ViewModels.Admin;

namespace CMS5000.Views.Admin;

public partial class AssignInsertView : Window
{
    public AssignInsertView(int stationId, string stationName)
    {
        InitializeComponent();
        var vm = new AssignInsertViewModel(stationId);
        vm.Title = $"ASSIGN INSERT  —  Station {stationId}  {stationName}";
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
