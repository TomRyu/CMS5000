using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Maintenance;

public class MaintenanceViewModel : ViewModelBase
{
    private string _selectedEquipment = "전체";
    private string _selectedChannel = "CH1";
    private DateTime _startDate = DateTime.Now.AddDays(-7);
    private DateTime _endDate = DateTime.Now;
    private string _selectedEventType = "전체";

    public string SelectedEquipment { get => _selectedEquipment; set => SetProperty(ref _selectedEquipment, value); }
    public string SelectedChannel   { get => _selectedChannel;   set => SetProperty(ref _selectedChannel, value); }
    public DateTime StartDate       { get => _startDate;         set => SetProperty(ref _startDate, value); }
    public DateTime EndDate         { get => _endDate;           set => SetProperty(ref _endDate, value); }
    public string SelectedEventType { get => _selectedEventType; set => SetProperty(ref _selectedEventType, value); }

    public ObservableCollection<string> EquipmentList { get; } = ["전체", "압축기 A", "펌프 B1", "모터 C", "팬 D2", "터빈 E", "발전기 F"];
    public ObservableCollection<string> ChannelList   { get; } = ["CH1", "CH2", "CH3", "CH4"];
    public ObservableCollection<string> EventTypeList { get; } = ["전체", "진동 이상", "온도 경고", "불균형", "점검 예정"];

    public ObservableCollection<HistoryRecord> HistoryRecords { get; } = [];

    public RelayCommand SearchCommand { get; }

    public MaintenanceViewModel()
    {
        SearchCommand = new RelayCommand(_ => LoadHistory());
        LoadHistory();
    }

    private void LoadHistory()
    {
        HistoryRecords.Clear();
        HistoryRecords.Add(new HistoryRecord { EquipmentName = "팬 D2",   EventType = "진동 이상", Description = "CH1 진동 임계값 초과 (95.8 RPM×1000)", OccurredAt = DateTime.Now.AddHours(-5),  Action = "점검 중", HandledBy = "김정비" });
        HistoryRecords.Add(new HistoryRecord { EquipmentName = "펌프 B1", EventType = "온도 경고", Description = "베어링 온도 72°C 초과",               OccurredAt = DateTime.Now.AddHours(-18), Action = "냉각 조치", HandledBy = "이담당" });
        HistoryRecords.Add(new HistoryRecord { EquipmentName = "터빈 E",  EventType = "불균형",   Description = "CH2 불균형 감지 (임계치 1.2배)",       OccurredAt = DateTime.Now.AddHours(-32), Action = "모니터링", HandledBy = "박주임" });
        HistoryRecords.Add(new HistoryRecord { EquipmentName = "압축기 A",EventType = "점검 예정", Description = "6개월 정기 점검 예정",                  OccurredAt = DateTime.Now.AddDays(-1),   Action = "계획됨", HandledBy = "" });
    }
}
