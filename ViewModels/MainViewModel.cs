using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;
using CMS5000.ViewModels.Expert;
using CMS5000.ViewModels.Maintenance;
using CMS5000.ViewModels.Operator;
using CMS5000.ViewModels.Settings;
using Postgrest;

namespace CMS5000.ViewModels;

public class MainViewModel : ViewModelBase
{
    private static readonly string[] LightningChartSampleTypes =
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
    private string _windowTitle = "CMS-5000 | ㈜오토시스";
    private bool _saveUsername;
    private bool _savePassword;
    private bool _isPasswordVisible;

    public UserRole CurrentRole        { get => _currentRole;       set { SetProperty(ref _currentRole, value); OnPropertyChanged(nameof(IsAdminRole)); OnPropertyChanged(nameof(IsExpertRole)); OnPropertyChanged(nameof(IsMaintenanceOrExpert)); } }
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
    public string WindowTitle          { get => _windowTitle;        set => SetProperty(ref _windowTitle, value); }
    public bool SaveUsername           { get => _saveUsername;       set => SetProperty(ref _saveUsername, value); }
    public bool SavePassword           { get => _savePassword;       set => SetProperty(ref _savePassword, value); }
    public bool IsPasswordVisible      { get => _isPasswordVisible;  set { SetProperty(ref _isPasswordVisible, value); OnPropertyChanged(nameof(IsPasswordHidden)); } }
    public bool IsPasswordHidden       => !_isPasswordVisible;
    public bool IsAdminRole            => _currentRole == UserRole.Admin;
    public bool IsExpertRole           => _currentRole == UserRole.Expert;
    public bool IsMaintenanceOrExpert  => _currentRole == UserRole.Maintenance || _currentRole == UserRole.Expert;
    public int AlertCount              => 3;
    public int NotificationCount       => 2;

    public OperatorViewModel    OperatorVM    { get; } = new();
    public MaintenanceViewModel MaintenanceVM { get; } = new();
    public ExpertViewModel      ExpertVM      { get; } = new();
    public AdminViewModel       AdminVM       { get; } = new();
    public SettingsViewModel    SettingsVM    { get; } = new();
    public LogViewModel         LogVM         { get; } = new();

    public ObservableCollection<NavNode>  NavTree        { get; } = [];
    public ObservableCollection<string>  LoginUsernames { get; } = [];

    public RelayCommand LoginCommand                        { get; }
    public RelayCommand LogoutCommand                       { get; }
    public RelayCommand<string> SwitchNavCommand            { get; }
    public RelayCommand ToggleTreeCommand                   { get; }
    public RelayCommand<string> ShowAlarmDetailCommand      { get; }
    public RelayCommand TogglePasswordVisibilityCommand     { get; }

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
            else if (icon == "Settings")
                CurrentView = SettingsVM;
            else if (icon == "Log")
            {
                CurrentView = LogVM;
                Breadcrumb = "Machinery Health / 로그";
            }
            AppLogService.Info("탐색", $"메뉴 이동: {NavDisplayName(icon)}");
        });
        ToggleTreeCommand = new RelayCommand(_ => IsTreePanelVisible = !IsTreePanelVisible);
        TogglePasswordVisibilityCommand = new RelayCommand(_ => IsPasswordVisible = !IsPasswordVisible);
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
        LoadSavedCredentials();
        _ = CheckForUpdatesAsync();
        _ = LoadLoginUsernamesAsync();
    }

    public void SelectNavNode(NavNode node)
    {
        SelectedNode = node;

        if (!node.IsChartNode) return;

        ExpertVM.OpenChartSample(node.ChartType);
        CurrentView = ExpertVM;
        ActiveNavIcon = "Diagnosis";
        Breadcrumb = $"Machinery Health / LightningChart Samples / {node.Name}";
        AppLogService.Info("차트", $"LightningChart 샘플 열기: {node.Name}");
    }

    private static string NavDisplayName(string icon) => icon switch
    {
        "Dashboard"  => "대시보드",
        "Equipment"  => "설비",
        "Alarm"      => "알람",
        "Analysis"   => "분석",
        "Diagnosis"  => "진단",
        "Inspection" => "점검/이력",
        "Report"     => "보고서",
        "Log"        => "로그",
        "Settings"   => "설정",
        "Admin"      => "사용자 관리",
        _            => icon
    };

    private async Task LoginAsync()
    {
        IsLoginBusy = true;
        LoginError = "";
        var (success, error) = await AuthService.LoginAsync(LoginUsername, LoginPassword);
        IsLoginBusy = false;

        if (!success)
        {
            LoginError = error;
            AppLogService.Warning("인증", $"로그인 실패: {error}", LoginUsername);
            return;
        }

        var role = AuthService.GetCurrentRole();
        AppLogService.Success("인증", $"로그인 성공 ({role})", AuthService.CurrentUser?.DisplayName);
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
        IsTreePanelVisible = role != UserRole.Admin;
        IsPasswordVisible = false;
        SettingsVM.LoadFromCurrentUser();
        SessionTimeoutService.ResetActivity();

        PersistCredentials(LoginUsername, LoginPassword);
    }

    /// <summary>무활동 세션 타임아웃에 의한 자동 로그아웃.</summary>
    public void TriggerAutoLogout()
    {
        if (IsLoginVisible) return;
        AppLogService.Warning("인증", "무활동 세션 타임아웃으로 자동 로그아웃", AuthService.CurrentUser?.DisplayName);
        Logout();
        LoginError = "자동 로그아웃되었습니다. 다시 로그인하세요.";
    }

    private void Logout()
    {
        AppLogService.Info("인증", "로그아웃", AuthService.CurrentUser?.DisplayName);
        AuthService.Logout();
        IsLoginVisible = true;
        IsTreePanelVisible = true;
        CurrentView = null;
        LoginUsername = "";
        LoginPassword = "";
        LoginError = "";
        IsPasswordVisible = false;
        LoadSavedCredentials();
        _ = LoadLoginUsernamesAsync();
    }

    private static string CredentialsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CMS5000", "credentials.json");

    private void LoadSavedCredentials()
    {
        try
        {
            if (!File.Exists(CredentialsPath)) return;
            var json = File.ReadAllText(CredentialsPath);
            var data = JsonSerializer.Deserialize<SavedCredentials>(json);
            if (data == null) return;
            SaveUsername = data.SaveUsername;
            SavePassword = data.SavePassword;
            if (data.SaveUsername && !string.IsNullOrEmpty(data.LastUsername))
            {
                LoginUsername = data.LastUsername;
                if (data.SavePassword && data.Passwords.TryGetValue(data.LastUsername, out var enc))
                {
                    var pwdBytes = ProtectedData.Unprotect(Convert.FromBase64String(enc), null, DataProtectionScope.CurrentUser);
                    LoginPassword = Encoding.UTF8.GetString(pwdBytes);
                }
            }
        }
        catch { }
    }

    private void PersistCredentials(string username, string password)
    {
        try
        {
            SavedCredentials existing = new();
            if (File.Exists(CredentialsPath))
            {
                try
                {
                    var existingJson = File.ReadAllText(CredentialsPath);
                    existing = JsonSerializer.Deserialize<SavedCredentials>(existingJson) ?? new();
                }
                catch { }
            }

            var passwords = new Dictionary<string, string>(existing.Passwords);
            if (SavePassword && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var pwdBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
                passwords[username] = Convert.ToBase64String(pwdBytes);
            }

            var data = new SavedCredentials
            {
                LastUsername = SaveUsername ? username : "",
                SaveUsername = SaveUsername,
                SavePassword = SavePassword,
                Passwords    = passwords,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(CredentialsPath)!);
            File.WriteAllText(CredentialsPath, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    public void OnUsernameSelected(string username)
    {
        if (!SavePassword) { LoginPassword = ""; return; }
        try
        {
            if (!File.Exists(CredentialsPath)) return;
            var json = File.ReadAllText(CredentialsPath);
            var data = JsonSerializer.Deserialize<SavedCredentials>(json);
            if (data == null) return;
            if (data.Passwords.TryGetValue(username, out var enc))
            {
                var pwdBytes = ProtectedData.Unprotect(Convert.FromBase64String(enc), null, DataProtectionScope.CurrentUser);
                LoginPassword = Encoding.UTF8.GetString(pwdBytes);
            }
            else
            {
                LoginPassword = "";
            }
        }
        catch { }
    }

    private record SavedCredentials
    {
        public string                      LastUsername { get; init; } = "";
        public bool                        SaveUsername { get; init; } = false;
        public bool                        SavePassword { get; init; } = false;
        public Dictionary<string, string>  Passwords    { get; init; } = new();
    }

    private void BuildNavTree()
    {
        NavTree.Add(new NavNode
        {
            Name = "LightningChart Samples",
            Status = EquipmentStatus.Normal,
            IsExpanded = true,
            Children = [.. LightningChartSampleTypes.Select(type => new NavNode
            {
                Name = type,
                ChartType = type,
                Status = EquipmentStatus.Normal
            })]
        });

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
        var version = UpdateService.GetCurrentVersionText();
        if (!string.IsNullOrWhiteSpace(version))
            WindowTitle = $"CMS-5000 v{version} | ㈜오토시스";

        await UpdateService.CheckDownloadAndApplyAsync(
            status => UpdateStatus = status,
            versionToApply =>
            {
                AppLogService.Info("업데이트", $"새 버전 v{versionToApply} 다운로드 완료");
                if (!IsLoginVisible)
                {
                    // 로그인 중 또는 이후에는 강제 재시작하지 않음 — 다음 실행 시 자동 적용
                    System.Windows.Application.Current.Dispatcher.Invoke(
                        () => UpdateStatus = $"v{versionToApply} 업데이트 준비 완료 (다음 실행 시 적용)");
                    return false;
                }

                var result = System.Windows.MessageBox.Show(
                    $"새 버전 v{versionToApply} 업데이트가 준비되었습니다.\n지금 재시작해서 적용하시겠습니까?",
                    "업데이트 준비 완료",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                return result == System.Windows.MessageBoxResult.Yes;
            });
    }

    private async Task LoadLoginUsernamesAsync()
    {
        try
        {
            var response = await SupabaseService.Client.From<LoginLog>()
                .Order("logged_at", Constants.Ordering.Descending)
                .Limit(500)
                .Get();

            var distinct = response.Models
                .Select(l => l.Username)
                .Distinct()
                .ToList();

            LoginUsernames.Clear();
            foreach (var name in distinct)
                LoginUsernames.Add(name);
        }
        catch { }
    }

    private void UpdateBreadcrumb()
    {
        if (_selectedNode == null) return;
        if (_selectedNode.IsChartNode)
        {
            Breadcrumb = $"Machinery Health / LightningChart Samples / {_selectedNode.Name}";
            return;
        }

        Breadcrumb = $"Machinery Health / Unit 1 / {_selectedNode.Name}";
    }
}
