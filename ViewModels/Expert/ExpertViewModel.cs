using System.Collections.ObjectModel;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Expert;

public class PlotTab(string title, string plotType)
{
    public string Title { get; set; } = title;
    public string PlotType { get; set; } = plotType;
    public bool IsActive { get; set; }
}

public class ExpertViewModel : ViewModelBase
{
    private string _selectedEquipment = "팬 D2";
    private string _selectedPlotType = "Spectrum";
    private PlotTab? _activeTab;

    public string SelectedEquipment { get => _selectedEquipment; set => SetProperty(ref _selectedEquipment, value); }
    public string SelectedPlotType  { get => _selectedPlotType;  set => SetProperty(ref _selectedPlotType, value); }
    public PlotTab? ActiveTab       { get => _activeTab;         set => SetProperty(ref _activeTab, value); }

    public ObservableCollection<string> EquipmentList { get; } = ["압축기 A", "펌프 B1", "모터 C", "팬 D2", "터빈 E", "발전기 F"];
    public ObservableCollection<string> PlotTypeList  { get; } = ["Orbit", "Orbit & Time Base", "Time Base", "Bode", "Polar", "Cascade", "Waterfall", "Spectrum", "Surface"];

    public ObservableCollection<PlotTab> OpenPlots { get; } = [];

    public RelayCommand AddPlotCommand   { get; }
    public RelayCommand<PlotTab> CloseTabCommand { get; }

    public ExpertViewModel()
    {
        AddPlotCommand = new RelayCommand(_ => AddPlot());
        CloseTabCommand = new RelayCommand<PlotTab>(tab => { if (tab != null) OpenPlots.Remove(tab); });

        OpenPlots.Add(new PlotTab("Spectrum - 팬 D2", "Spectrum") { IsActive = true });
        OpenPlots.Add(new PlotTab("Waterfall - 팬 D2", "Waterfall"));
        ActiveTab = OpenPlots[0];
    }

    private void AddPlot()
    {
        var tab = new PlotTab($"{SelectedPlotType} - {SelectedEquipment}", SelectedPlotType);
        OpenPlots.Add(tab);
        ActiveTab = tab;
    }
}
