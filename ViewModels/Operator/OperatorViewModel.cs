using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Operator;

public class OperatorViewModel : ViewModelBase
{
    private int _normalCount = 12;
    private int _warningCount = 3;
    private int _dangerCount = 1;
    private string? _activeFilter;

    public int NormalCount  { get => _normalCount;  set => SetProperty(ref _normalCount, value); }
    public int WarningCount { get => _warningCount; set => SetProperty(ref _warningCount, value); }
    public int DangerCount  { get => _dangerCount;  set => SetProperty(ref _dangerCount, value); }
    public int TotalCount   => NormalCount + WarningCount + DangerCount;

    public string? ActiveFilter
    {
        get => _activeFilter;
        set
        {
            if (SetProperty(ref _activeFilter, value))
            {
                OnPropertyChanged(nameof(IsFiltered));
                OnPropertyChanged(nameof(FilterLabel));
                OnPropertyChanged(nameof(FilterIsNormal));
                OnPropertyChanged(nameof(FilterIsWarning));
                OnPropertyChanged(nameof(FilterIsDanger));
                RefreshFilteredEquipments();
            }
        }
    }

    public bool IsFiltered      => ActiveFilter != null;
    public bool FilterIsNormal  => ActiveFilter == "Normal";
    public bool FilterIsWarning => ActiveFilter == "Warning";
    public bool FilterIsDanger  => ActiveFilter == "Danger";

    public string FilterLabel => ActiveFilter switch
    {
        "Normal"  => "정상 설비",
        "Warning" => "경고 설비",
        "Danger"  => "위험 설비",
        _         => "전체 설비"
    };

    public ObservableCollection<Equipment> Equipments       { get; } = [];
    public ObservableCollection<Equipment> FilteredEquipments { get; } = [];
    public ObservableCollection<AlarmItem> ActiveAlarms     { get; } = [];

    public RelayCommand<string> ShowFilterCommand { get; }
    public RelayCommand         ClearFilterCommand { get; }

    public OperatorViewModel()
    {
        ShowFilterCommand  = new RelayCommand<string>(status =>
            ActiveFilter = ActiveFilter == status ? null : status);
        ClearFilterCommand = new RelayCommand(_ => ActiveFilter = null);

        LoadSampleData();
    }

    private void LoadSampleData()
    {
        Equipments.Add(new Equipment { Id = "EQ-001", Name = "압축기 A",  Location = "1구역", Status = EquipmentStatus.Normal,  CurrentValue = 45.2, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-002", Name = "펌프 B1",   Location = "2구역", Status = EquipmentStatus.Warning, CurrentValue = 72.1, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-003", Name = "모터 C",    Location = "1구역", Status = EquipmentStatus.Normal,  CurrentValue = 30.5, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-004", Name = "팬 D2",     Location = "3구역", Status = EquipmentStatus.Danger,  CurrentValue = 95.8, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-005", Name = "터빈 E",    Location = "2구역", Status = EquipmentStatus.Warning, CurrentValue = 68.3, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-006", Name = "발전기 F",  Location = "3구역", Status = EquipmentStatus.Normal,  CurrentValue = 41.7, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-007", Name = "블로워 G",  Location = "1구역", Status = EquipmentStatus.Normal,  CurrentValue = 28.4, Unit = "RPM×1000" });
        Equipments.Add(new Equipment { Id = "EQ-008", Name = "컴프레서 H",Location = "2구역", Status = EquipmentStatus.Normal,  CurrentValue = 52.0, Unit = "RPM×1000" });

        ActiveAlarms.Add(new AlarmItem { Id = "AL-001", EquipmentName = "팬 D2",   Message = "진동 임계값 초과", Level = AlarmLevel.Critical, OccurredAt = DateTime.Now.AddMinutes(-5),  Channel = "CH1" });
        ActiveAlarms.Add(new AlarmItem { Id = "AL-002", EquipmentName = "펌프 B1", Message = "베어링 온도 경고", Level = AlarmLevel.Warning,  OccurredAt = DateTime.Now.AddMinutes(-18), Channel = "CH3" });
        ActiveAlarms.Add(new AlarmItem { Id = "AL-003", EquipmentName = "터빈 E",  Message = "불균형 감지",      Level = AlarmLevel.Warning,  OccurredAt = DateTime.Now.AddMinutes(-32), Channel = "CH2" });

        RefreshFilteredEquipments();
    }

    private void RefreshFilteredEquipments()
    {
        FilteredEquipments.Clear();
        var source = ActiveFilter == null
            ? (IEnumerable<Equipment>)Equipments
            : Equipments.Where(e => e.Status.ToString() == ActiveFilter);
        foreach (var eq in source)
            FilteredEquipments.Add(eq);
    }
}
