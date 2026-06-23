using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Admin;
using CMS5000.ViewModels.Base;
using CMS5000.ViewModels.Expert;
using CMS5000.ViewModels.Maintenance;
using CMS5000.ViewModels.Operator;
using CMS5000.ViewModels.Settings;

namespace CMS5000.ViewModels;

public class MainViewModel : ViewModelBase
{
    private static readonly string[] ChartSampleTypes =
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
    private bool _isDbConnected;
    private string _connectionStatusText = $"[DB]{PostgresService.CurrentDatabase} Connecting...";
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
    public bool IsDbConnected          { get => _isDbConnected;      set => SetProperty(ref _isDbConnected, value); }
    public string ConnectionStatusText { get => _connectionStatusText; set => SetProperty(ref _connectionStatusText, value); }
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

    public OperatorViewModel      OperatorVM      { get; } = new();
    public MaintenanceViewModel   MaintenanceVM   { get; } = new();
    public ExpertViewModel        ExpertVM        { get; } = new();
    // 비-admin 역할 공용 진동 모니터링 화면(Operator/Maintenance/Expert 동일 적용)
    public Monitoring.MonitoringViewModel MonitoringVM { get; } = new();
    public AdminViewModel         AdminVM         { get; } = new();
    public DeviceConfigViewModel  DeviceConfigVM  { get; } = new();
    public SettingsViewModel      SettingsVM      { get; } = new();
    public LogViewModel           LogVM           { get; } = new();

    public ObservableCollection<NavNode>  NavTree        { get; } = [];
    public ObservableCollection<string>  LoginUsernames { get; } = [];

    public RelayCommand LoginCommand                        { get; }
    public RelayCommand LogoutCommand                       { get; }
    public RelayCommand<string> SwitchNavCommand            { get; }
    public RelayCommand ToggleTreeCommand                   { get; }
    public RelayCommand<string> ShowAlarmDetailCommand      { get; }
    public RelayCommand TogglePasswordVisibilityCommand     { get; }
    public RelayCommand DbConnectCommand                    { get; }
    public RelayCommand DbDisconnectCommand                 { get; }

    private readonly System.Timers.Timer _clockTimer;
    private readonly System.Timers.Timer _dbCheckTimer;

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
            else if (icon == "DeviceConfig")
                CurrentView = DeviceConfigVM;
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

        // Connection 메뉴: DB 연결/해제 (원본 Database Connect/Disconnect — 실제 동작 + LED 반영)
        DbConnectCommand = new RelayCommand(_ =>
        {
            // 원본 ConnectDatabaseChecker: 접속정보 다이얼로그 → 성공 시 저장·접속
            var dlg = new Views.Admin.DbConnectView { Owner = System.Windows.Application.Current.MainWindow };
            dlg.ShowDialog();
            if (dlg.Connected)
            {
                _ = CheckDbConnectionAsync();   // 즉시 LED 갱신
                _ = DeviceConfigVM.ReloadForDbChangeAsync();   // DB 변경 시 STATION/RACK/TRAIN 재로드
                _ = MonitoringVM.RefreshTreesAsync();          // 모니터링 화면 트리도 갱신
                AppLogService.Info("연결", $"DB 연결(수동): {PostgresService.CurrentDatabase}");
            }
        });
        DbDisconnectCommand = new RelayCommand(_ =>
        {
            if (System.Windows.MessageBox.Show(
                    "DB 연결을 해제하시겠습니까?\n해제 시 데이터 조회·저장이 중단되고 하단 LED 가 회색으로 표시됩니다.",
                    "Database Disconnect",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question,
                    System.Windows.MessageBoxResult.No) != System.Windows.MessageBoxResult.Yes)
                return;

            PostgresService.Disconnect();
            _ = CheckDbConnectionAsync();   // 즉시 LED 회색
            AppLogService.Info("연결", "DB 연결 해제(수동)");
        });
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

        // DB 연결 상태 주기 점검 (하단 상태바 LED)
        _dbCheckTimer = new System.Timers.Timer(5000);
        _dbCheckTimer.Elapsed += (_, _) => _ = CheckDbConnectionAsync();
        _dbCheckTimer.Start();
        _ = CheckDbConnectionAsync();   // 시작 즉시 1회

        BuildNavTree();
        LoadSavedCredentials();
        _ = CheckForUpdatesAsync();
        LoadLoginUsernames();
    }

    /// <summary>DB 도달 여부를 점검해 하단 상태바 LED·텍스트를 갱신한다.</summary>
    private async Task CheckDbConnectionAsync()
    {
        // 하단 상태바는 Connection > Database Connect 다이얼로그의 Database 필드(PostgresService.CurrentDatabase)와
        // 정확히 동일한 이름을 "[DB]{이름}" 형식으로 표시한다.
        string db = $"[DB]{PostgresService.CurrentDatabase}";
        try
        {
            await PostgresService.EnsureReachableAsync();
            IsDbConnected = true;
            ConnectionStatusText = $"{db} Connection OK.";
        }
        catch
        {
            IsDbConnected = false;
            ConnectionStatusText = PostgresService.IsManuallyDisconnected
                ? $"{db} Disconnected."
                : $"{db} Connection FAIL.";
        }
    }

    public void SelectNavNode(NavNode node)
    {
        SelectedNode = node;

        if (!node.IsChartNode) return;

        ExpertVM.OpenChartSample(node.ChartType);
        CurrentView = ExpertVM;
        ActiveNavIcon = "Diagnosis";
        Breadcrumb = $"Machinery Health / ScottPlot Samples / {node.Name}";
        AppLogService.Info("차트", $"ScottPlot 샘플 열기: {node.Name}");
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
        "Log"          => "로그",
        "Settings"     => "설정",
        "Admin"        => "사용자 관리",
        "DeviceConfig" => "장비 구성",
        _            => icon
    };

    private async Task LoginAsync()
    {
        IsLoginBusy = true;
        LoginError = "";
        bool success;
        string error;
        try
        {
            (success, error) = await AuthService.LoginAsync(LoginUsername, LoginPassword);
        }
        catch (Exception ex)
        {
            IsLoginBusy = false;
            LoginError = $"DB 오류: {ex.Message}";
            AppLogService.Error("인증", $"로그인 예외: {ex.Message}");
            return;
        }
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
            UserRole.Operator    => MonitoringVM,
            UserRole.Maintenance => MonitoringVM,
            UserRole.Expert      => MonitoringVM,
            _                    => null
        };

        ActiveNavIcon = role == UserRole.Admin ? "Admin" : "Dashboard";
        // 비-admin은 MonitoringView 자체 트리가 있으므로 기존 "Database Name" 트리 패널은 숨긴다.
        IsTreePanelVisible = false;
        // 비-admin 모니터링 화면: 로그인 시점(연결 보장)에 Train/Rack 트리를 다시 로드한다.
        if (role != UserRole.Admin)
            _ = MonitoringVM.RefreshTreesAsync();
        IsPasswordVisible = false;
        SettingsVM.LoadFromCurrentUser();
        SessionTimeoutService.ResetActivity();
        LoadLoginUsernames();

        if (role == UserRole.Admin)
            AdminVM.LoadAll();

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
        LoadLoginUsernames();
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
            Name = "ScottPlot Samples",
            Status = EquipmentStatus.Normal,
            IsExpanded = true,
            Children = [.. ChartSampleTypes.Select(type => new NavNode
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

    // 로그인 전 아이디 자동완성은 이 PC에 저장된 계정만 사용한다.
    // (예전엔 로그인 이력 테이블을 조회했으나, 인증 전 DB 접근/아이디 노출을 없애기 위해 로컬 한정으로 변경)
    private void LoadLoginUsernames()
    {
        LoginUsernames.Clear();
        try
        {
            if (!File.Exists(CredentialsPath)) return;
            var data = JsonSerializer.Deserialize<SavedCredentials>(File.ReadAllText(CredentialsPath));
            if (data == null) return;

            var names = new List<string>(data.Passwords.Keys);
            if (!string.IsNullOrEmpty(data.LastUsername) && !names.Contains(data.LastUsername))
                names.Insert(0, data.LastUsername);

            foreach (var name in names.Distinct())
                LoginUsernames.Add(name);
        }
        catch { }
    }

    private void UpdateBreadcrumb()
    {
        if (_selectedNode == null) return;
        if (_selectedNode.IsChartNode)
        {
            Breadcrumb = $"Machinery Health / ScottPlot Samples / {_selectedNode.Name}";
            return;
        }

        Breadcrumb = $"Machinery Health / Unit 1 / {_selectedNode.Name}";
    }
}
