using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;
using CMS5000.ViewModels.Expert;
using CMS5000.ViewModels.Maintenance;
using CMS5000.ViewModels.Operator;
using Velopack;
using Velopack.Sources;

namespace CMS5000.ViewModels;

public class MainViewModel : ViewModelBase
{
    private UserRole _currentRole = UserRole.Operator;
    private object? _currentView;
    private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    private bool _isLoginVisible = true;
    private string _selectedRoleName = "";
    private string _activeNavIcon = "Machinery";
    private bool _isTreePanelVisible = true;
    private NavNode? _selectedNode;
    private string _breadcrumb = "Machinery Health";
    private string _loginUsername = "";
    private string _loginPassword = "";
    private string _loginError = "";
    private bool _isLoginBusy;
    private string _updateStatus = "";

    public UserRole CurrentRole        { get => _currentRole;       set { SetProperty(ref _currentRole, value); OnPropertyChanged(nameof(IsAdminRole)); } }
    public object? CurrentView         { get => _currentView;        set => SetProperty(ref _currentView, value); }
    public string CurrentTime          { get => _currentTime;        set => SetProperty(ref _currentTime, value); }
    public bool IsLoginVisible         { get => _isLoginVisible;     set => SetProperty(ref _isLoginVisible, value); }
    public string SelectedRoleName     { get => _selectedRoleName;   set => SetProperty(ref _selectedRoleName, value); }
    public string ActiveNavIcon        { get => _activeNavIcon;      set => SetProperty(ref _activeNavIcon, value); }
    public bool IsTreePanelVisible     { get => _isTreePanelVisible; set => SetProperty(ref _isTreePanelVisible, value); }
    public NavNode? SelectedNode       { get => _selectedNode;       set { SetProperty(ref _selectedNode, value); UpdateBreadcrumb(); } }
    public string Breadcrumb           { get => _breadcrumb;         set => SetProperty(ref _breadcrumb, value); }
    public string LoginUsername        { get => _loginUsername;      set => SetProperty(ref _loginUsername, value); }
    public string LoginPassword        { get => _loginPassword;      set => SetProperty(ref _loginPassword, value); }
    public string LoginError           { get => _loginError;         set => SetProperty(ref _loginError, value); }
    public bool IsLoginBusy            { get => _isLoginBusy;        set => SetProperty(ref _isLoginBusy, value); }
    public string UpdateStatus         { get => _updateStatus;       set => SetProperty(ref _updateStatus, value); }
    public bool IsAdminRole            => _currentRole == UserRole.Admin;
    public int AlertCount              => 3;
    public int NotificationCount       => 2;

    public OperatorViewModel    OperatorVM    { get; } = new();
    public MaintenanceViewModel MaintenanceVM { get; } = new();
    public ExpertViewModel      ExpertVM      { get; } = new();
    public AdminViewModel       AdminVM       { get; } = new();

    public ObservableCollection<NavNode> NavTree { get; } = [];

    public RelayCommand LoginCommand                   { get; }
    public RelayCommand LogoutCommand                  { get; }
    public RelayCommand<string> SwitchNavCommand       { get; }
    public RelayCommand ToggleTreeCommand              { get; }
    public RelayCommand<string> ShowAlarmDetailCommand { get; }

    private readonly System.Timers.Timer _clockTimer;

    public MainViewModel()
    {
        LoginCommand = new RelayCommand(_ => _ = LoginAsync());
        LogoutCommand = new RelayCommand(_ => Logout());
        SwitchNavCommand = new RelayCommand<string>(icon =>
        {
            if (icon == null) return;
            ActiveNavIcon = icon;
            if (icon == "Admin")
                CurrentView = AdminVM;
        });
        ToggleTreeCommand = new RelayCommand(_ => IsTreePanelVisible = !IsTreePanelVisible);
        ShowAlarmDetailCommand = new RelayCommand<string>(status =>
        {
            if (IsLoginVisible || status == null) return;
            if (CurrentView is not OperatorViewModel)
            {
                CurrentView = OperatorVM;
                ActiveNavIcon = "Dashboard";
            }
            OperatorVM.ShowFilterCommand.Execute(status);
        });

        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clockTimer.Start();

        BuildNavTree();
        _ = CheckForUpdatesAsync();
    }

    private async Task LoginAsync()
    {
        IsLoginBusy = true;
        LoginError = "";
        var (success, error) = await AuthService.LoginAsync(LoginUsername, LoginPassword);
        IsLoginBusy = false;

        if (!success)
        {
            LoginError = error;
            return;
        }

        var role = AuthService.GetCurrentRole();
        CurrentRole = role;
        IsLoginVisible = false;

        SelectedRoleName = role switch
        {
            UserRole.Admin       => $"관리자 ({AuthService.CurrentUser?.DisplayName})",
            UserRole.Operator    => $"운전자 ({AuthService.CurrentUser?.DisplayName})",
            UserRole.Maintenance => $"정비담당자 ({AuthService.CurrentUser?.DisplayName})",
            UserRole.Expert      => $"진단전문가 ({AuthService.CurrentUser?.DisplayName})",
            _                    => AuthService.CurrentUser?.DisplayName ?? ""
        };

        CurrentView = role switch
        {
            UserRole.Admin       => AdminVM,
            UserRole.Operator    => OperatorVM,
            UserRole.Maintenance => MaintenanceVM,
            UserRole.Expert      => ExpertVM,
            _                    => null
        };

        ActiveNavIcon = role == UserRole.Admin ? "Admin" : "Dashboard";
        LoginPassword = "";
    }

    private void Logout()
    {
        AuthService.Logout();
        IsLoginVisible = true;
        CurrentView = null;
        LoginUsername = "";
        LoginPassword = "";
        LoginError = "";
    }

    private void BuildNavTree()
    {
        NavTree.Add(new NavNode
        {
            Name = "Section A", Status = EquipmentStatus.Danger, IsExpanded = true,
            Children =
            [
                new NavNode
                {
                    Name = "Train Cracked Gas C...", Status = EquipmentStatus.Danger, IsExpanded = true,
                    Children =
                    [
                        new NavNode { Name = "Motor-106-A9",      Status = EquipmentStatus.Normal },
                        new NavNode { Name = "Gearbox-Double-7A", Status = EquipmentStatus.Danger },
                        new NavNode { Name = "Compressor-70A",    Status = EquipmentStatus.Normal },
                        new NavNode { Name = "Compressor-70A-2",  Status = EquipmentStatus.Normal },
                    ]
                },
                new NavNode
                {
                    Name = "Circulation Pump 1", Status = EquipmentStatus.Normal,
                    Children =
                    [
                        new NavNode { Name = "Motor", Status = EquipmentStatus.Normal },
                        new NavNode { Name = "Pump",  Status = EquipmentStatus.Normal },
                    ]
                }
            ]
        });
        NavTree.Add(new NavNode
        {
            Name = "Section B", Status = EquipmentStatus.Warning,
            Children =
            [
                new NavNode { Name = "Feed Water Pump 1", Status = EquipmentStatus.Warning,
                    Children = [ new NavNode { Name = "Motor", Status = EquipmentStatus.Warning } ] },
                new NavNode { Name = "Feed Water Pump 2", Status = EquipmentStatus.Normal },
                new NavNode { Name = "Refrigeration",     Status = EquipmentStatus.Normal },
            ]
        });
        NavTree.Add(new NavNode
        {
            Name = "Section C", Status = EquipmentStatus.Normal,
            Children =
            [
                new NavNode { Name = "Coker Wet Gas...", Status = EquipmentStatus.Normal },
                new NavNode { Name = "Bypass Fan",       Status = EquipmentStatus.Normal },
            ]
        });
        NavTree.Add(new NavNode
        {
            Name = "Section D", Status = EquipmentStatus.Normal,
            Children =
            [
                new NavNode
                {
                    Name = "Machine Train 1", Status = EquipmentStatus.Normal,
                    Children =
                    [
                        new NavNode { Name = "Motor X",   Status = EquipmentStatus.Normal },
                        new NavNode { Name = "Gearbox G", Status = EquipmentStatus.Normal },
                        new NavNode { Name = "Blower",    Status = EquipmentStatus.Normal },
                    ]
                },
                new NavNode { Name = "Machine Train 2", Status = EquipmentStatus.Normal }
            ]
        });
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource("https://github.com/TomRyu/CMS5000", null, false));
            var update = await mgr.CheckForUpdatesAsync();
            if (update == null) return;

            UpdateStatus = $"업데이트 다운로드 중... v{update.TargetFullRelease.Version}";
            await mgr.DownloadUpdatesAsync(update);
            UpdateStatus = $"v{update.TargetFullRelease.Version} 준비 완료 — 재시작 시 적용";
        }
        catch { }
    }

    private void UpdateBreadcrumb()
    {
        if (_selectedNode == null) return;
        Breadcrumb = $"Machinery Health / Unit 1 / {_selectedNode.Name}";
    }
}
