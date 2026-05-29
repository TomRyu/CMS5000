using System.Collections.ObjectModel;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Expert;

public class PlotTab(string title, string plotType) : ViewModelBase
{
    private bool _isActive;

    public string Title { get; set; } = title;
    public string PlotType { get; set; } = plotType;
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
}

public class ExpertViewModel : ViewModelBase
{
    private static readonly string[] PlotTypes =
    [
        "Trend",
        "Spectrum",
        "Spectrogram",
        "Waterfall",
        "Cascade",
        "Orbit",
        "Orbit & Time Base",
        "Time Base",
        "Bode",
        "Polar",
        "Campbell Diagram",
        "Surface",
    ];

    private string _selectedEquipment = "Fan D2";
    private string _selectedPlotType = "Spectrum";
    private PlotTab? _activeTab;

    public string SelectedEquipment { get => _selectedEquipment; set => SetProperty(ref _selectedEquipment, value); }
    public string SelectedPlotType  { get => _selectedPlotType;  set => SetProperty(ref _selectedPlotType, value); }
    public PlotTab? ActiveTab       { get => _activeTab;         set => SetActiveTab(value); }

    public ObservableCollection<string> EquipmentList { get; } =
    [
        "Compressor A",
        "Pump B1",
        "Motor C",
        "Fan D2",
        "Turbine E",
        "Generator F",
    ];

    public ObservableCollection<string> PlotTypeList { get; } = [.. PlotTypes];

    public ObservableCollection<PlotTab> OpenPlots { get; } = [];

    public RelayCommand AddPlotCommand { get; }
    public RelayCommand<PlotTab> SelectTabCommand { get; }
    public RelayCommand<PlotTab> CloseTabCommand { get; }

    public ExpertViewModel()
    {
        AddPlotCommand = new RelayCommand(_ => AddPlot());
        SelectTabCommand = new RelayCommand<PlotTab>(tab => ActiveTab = tab);
        CloseTabCommand = new RelayCommand<PlotTab>(CloseTab);

        OpenPlots.Add(new PlotTab("Spectrum - Fan D2", "Spectrum"));
        OpenPlots.Add(new PlotTab("Waterfall - Fan D2", "Waterfall"));
        ActiveTab = OpenPlots[0];
    }

    public void OpenChartSample(string plotType)
    {
        if (string.IsNullOrWhiteSpace(plotType)) return;

        SelectedPlotType = plotType;
        var existing = OpenPlots.FirstOrDefault(tab => tab.PlotType == plotType && tab.Title.EndsWith("Sample"));
        if (existing != null)
        {
            ActiveTab = existing;
            return;
        }

        var tab = new PlotTab($"{plotType} - Sample", plotType);
        OpenPlots.Add(tab);
        ActiveTab = tab;
    }

    private void AddPlot()
    {
        var tab = new PlotTab($"{SelectedPlotType} - {SelectedEquipment}", SelectedPlotType);
        OpenPlots.Add(tab);
        ActiveTab = tab;
    }

    private void CloseTab(PlotTab? tab)
    {
        if (tab == null) return;

        var wasActive = ReferenceEquals(tab, ActiveTab);
        var index = OpenPlots.IndexOf(tab);
        OpenPlots.Remove(tab);

        if (!wasActive) return;
        ActiveTab = OpenPlots.Count == 0 ? null : OpenPlots[Math.Min(index, OpenPlots.Count - 1)];
    }

    private bool SetActiveTab(PlotTab? tab)
    {
        if (ReferenceEquals(_activeTab, tab)) return false;

        if (_activeTab != null)
            _activeTab.IsActive = false;

        _activeTab = tab;

        if (_activeTab != null)
        {
            _activeTab.IsActive = true;
            SelectedPlotType = _activeTab.PlotType;
        }

        OnPropertyChanged(nameof(ActiveTab));
        return true;
    }
}
