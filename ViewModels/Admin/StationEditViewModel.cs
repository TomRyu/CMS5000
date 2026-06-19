using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Admin;

public class StationEditViewModel : ViewModelBase
{
    private readonly bool _isNew;
    private int    _stationId;
    private string _stationName = "";
    private string _company     = "";
    private string _companyAddr = "";
    private int    _rackPort;

    public string DialogTitle  => _isNew ? "STATION INSERT." : "STATION MODIFY.";
    public bool   IsIdReadOnly => !_isNew;
    public bool   Modified     { get; private set; }
    public int    SavedId      => _stationId;

    public int StationId
    {
        get => _stationId;
        set => SetProperty(ref _stationId, value);
    }
    public string StationName
    {
        get => _stationName;
        set => SetProperty(ref _stationName, value);
    }
    public string Company
    {
        get => _company;
        set => SetProperty(ref _company, value);
    }
    public string CompanyAddr
    {
        get => _companyAddr;
        set => SetProperty(ref _companyAddr, value);
    }
    public int RackPort
    {
        get => _rackPort;
        set => SetProperty(ref _rackPort, value);
    }

    public RelayCommand SaveCommand   { get; }
    public RelayCommand CancelCommand { get; }
    public event Action? CloseRequested;

    public StationEditViewModel(int nextId)
    {
        _isNew     = true;
        _stationId = nextId;
        SaveCommand   = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    public StationEditViewModel(DeviceStation station)
    {
        _isNew        = false;
        _stationId    = station.StationId;
        _stationName  = station.Name;
        _company      = station.Company;
        _companyAddr  = station.CompanyAddr;
        _rackPort     = station.RackListenPort;
        SaveCommand   = new RelayCommand(_ => _ = SaveAsync());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(StationName))
        {
            System.Windows.MessageBox.Show("스테이션 이름을 입력하세요.", "입력 오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        try
        {
            if (_isNew)
                await DeviceService.CreateStationAsync(StationId, StationName.Trim(),
                    Company.Trim(), CompanyAddr.Trim(), RackPort);
            else
                await DeviceService.UpdateStationAsync(StationId, StationName.Trim(),
                    Company.Trim(), CompanyAddr.Trim(), RackPort);
            Modified = true;
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"저장 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
