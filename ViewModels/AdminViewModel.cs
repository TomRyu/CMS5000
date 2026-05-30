using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels;

public class UserGroup
{
    public string Role       { get; set; } = "";
    public string RoleKorean { get; set; } = "";
    public ObservableCollection<CmsUser> Users { get; set; } = [];
}

public class AdminViewModel : ViewModelBase
{
    private ObservableCollection<CmsUser>   _users        = [];
    private ObservableCollection<UserGroup> _groupedUsers = [];
    private ObservableCollection<LoginLog>  _loginLogs    = [];
    private CmsUser? _selectedUser;
    private string _editUsername    = "";
    private string _editDisplayName = "";
    private string _editRole        = "Operator";
    private string _newPassword     = "";
    private bool   _isEditing;
    private bool   _isAddingNew;
    private string _statusMessage = "";
    private bool   _isBusy;
    private bool   _isLogsBusy;

    public ObservableCollection<CmsUser>   Users        { get => _users;        set { SetProperty(ref _users, value);        RebuildGroups(); OnPropertyChanged(nameof(ActiveCount)); OnPropertyChanged(nameof(InactiveCount)); } }
    public ObservableCollection<UserGroup> GroupedUsers { get => _groupedUsers; set => SetProperty(ref _groupedUsers, value); }
    public ObservableCollection<LoginLog>  LoginLogs    { get => _loginLogs;    set => SetProperty(ref _loginLogs, value); }

    public CmsUser? SelectedUser   { get => _selectedUser;    set => SetProperty(ref _selectedUser, value); }
    public string EditUsername     { get => _editUsername;    set => SetProperty(ref _editUsername, value); }
    public string EditDisplayName  { get => _editDisplayName; set => SetProperty(ref _editDisplayName, value); }
    public string EditRole         { get => _editRole;        set => SetProperty(ref _editRole, value); }
    public string NewPassword      { get => _newPassword;     set => SetProperty(ref _newPassword, value); }
    public bool   IsEditing        { get => _isEditing;       set => SetProperty(ref _isEditing, value); }
    public bool   IsAddingNew      { get => _isAddingNew;     set => SetProperty(ref _isAddingNew, value); }
    public string StatusMessage    { get => _statusMessage;   set { SetProperty(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatusMessage)); } }
    public bool   IsBusy           { get => _isBusy;          set => SetProperty(ref _isBusy, value); }
    public bool   IsLogsBusy       { get => _isLogsBusy;      set => SetProperty(ref _isLogsBusy, value); }

    public int  ActiveCount      => _users.Count(u => u.IsActive);
    public int  InactiveCount    => _users.Count(u => !u.IsActive);
    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public List<string> RoleOptions { get; } = ["Operator", "Maintenance", "Expert", "Admin"];

    public RelayCommand              RefreshCommand      { get; }
    public RelayCommand              RefreshLogsCommand  { get; }
    public RelayCommand              NewUserCommand      { get; }
    public RelayCommand              SaveCommand         { get; }
    public RelayCommand              CancelEditCommand   { get; }
    public RelayCommand<CmsUser>     EditUserCommand     { get; }
    public RelayCommand<CmsUser>     DeleteUserCommand   { get; }
    public RelayCommand<CmsUser>     ToggleActiveCommand { get; }

    public AdminViewModel()
    {
        RefreshCommand      = new RelayCommand(_ => _ = LoadUsersAsync());
        RefreshLogsCommand  = new RelayCommand(_ => _ = LoadLogsAsync());
        NewUserCommand      = new RelayCommand(_ => StartNewUser());
        SaveCommand         = new RelayCommand(_ => _ = SaveAsync());
        CancelEditCommand   = new RelayCommand(_ => CancelEdit());
        EditUserCommand     = new RelayCommand<CmsUser>(u => { if (u != null) StartEditUser(u); });
        DeleteUserCommand   = new RelayCommand<CmsUser>(u => { if (u != null) _ = DeleteUserAsync(u); });
        ToggleActiveCommand = new RelayCommand<CmsUser>(u => { if (u != null) _ = ToggleActiveAsync(u); });
    }

    /// <summary>로그인(관리자) 후 호출해 목록·이력을 적재.</summary>
    public void LoadAll()
    {
        _ = LoadUsersAsync();
        _ = LoadLogsAsync();
    }

    public async Task LoadUsersAsync()
    {
        IsBusy = true;
        StatusMessage = "";
        try
        {
            var (data, error) = await ApiService.GetAsync<List<CmsUser>>("/users");
            if (error != null) { StatusMessage = $"목록 로드 실패: {error}"; return; }
            Users = new ObservableCollection<CmsUser>(data ?? []);
        }
        finally { IsBusy = false; }
    }

    private async Task LoadLogsAsync()
    {
        IsLogsBusy = true;
        try
        {
            var (data, error) = await ApiService.GetAsync<List<LoginLog>>("/login-logs?limit=200");
            if (error != null) { StatusMessage = $"이력 로드 실패: {error}"; return; }
            LoginLogs = new ObservableCollection<LoginLog>(data ?? []);
        }
        finally { IsLogsBusy = false; }
    }

    private static readonly string[] RoleOrder = ["Admin", "Expert", "Maintenance", "Operator"];

    private void RebuildGroups()
    {
        var groups = RoleOrder
            .Select(role => new UserGroup
            {
                Role       = role,
                RoleKorean = role switch
                {
                    "Admin"       => "관리자",
                    "Expert"      => "진단전문가",
                    "Maintenance" => "정비담당자",
                    _             => "운전자"
                },
                Users = new ObservableCollection<CmsUser>(_users.Where(u => u.Role == role))
            })
            .Where(g => g.Users.Count > 0)
            .ToList();

        GroupedUsers = new ObservableCollection<UserGroup>(groups);
    }

    private void StartNewUser()
    {
        _selectedUser   = null;
        EditUsername    = "";
        EditDisplayName = "";
        EditRole        = "Operator";
        NewPassword     = "";
        IsAddingNew     = true;
        IsEditing       = true;
    }

    private void StartEditUser(CmsUser user)
    {
        _selectedUser   = user;
        EditUsername    = user.Username;
        EditDisplayName = user.DisplayName;
        EditRole        = user.Role;
        NewPassword     = "";
        IsAddingNew     = false;
        IsEditing       = true;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditUsername) || string.IsNullOrWhiteSpace(EditDisplayName))
        {
            StatusMessage = "아이디와 이름을 입력하세요.";
            return;
        }

        IsBusy = true;
        try
        {
            string? error;
            if (IsAddingNew)
            {
                if (string.IsNullOrWhiteSpace(NewPassword)) { StatusMessage = "비밀번호를 입력하세요."; return; }
                (_, error) = await ApiService.PostAsync<CmsUser>("/users", new
                {
                    username    = EditUsername.Trim(),
                    displayName = EditDisplayName.Trim(),
                    role        = EditRole,
                    password    = NewPassword
                });
                if (error != null) { StatusMessage = $"저장 실패: {error}"; return; }
                StatusMessage = $"'{EditUsername}' 사용자가 추가되었습니다.";
            }
            else if (_selectedUser != null)
            {
                (_, error) = await ApiService.PostAsync<CmsUser>("/users", new
                {
                    id          = _selectedUser.Id,
                    displayName = EditDisplayName.Trim(),
                    role        = EditRole,
                    password    = string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword
                });
                if (error != null) { StatusMessage = $"저장 실패: {error}"; return; }
                StatusMessage = $"'{EditUsername}' 사용자가 수정되었습니다.";
            }
            IsEditing = false;
            await LoadUsersAsync();
        }
        finally { IsBusy = false; }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        _selectedUser = null;
        StatusMessage = "";
    }

    private async Task DeleteUserAsync(CmsUser user)
    {
        if (user.Id == AuthService.CurrentUser?.Id)
        {
            StatusMessage = "현재 로그인된 계정은 삭제할 수 없습니다.";
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"'{user.DisplayName}({user.Username})' 사용자를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
            "사용자 삭제 확인",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        IsBusy = true;
        try
        {
            var (ok, error) = await ApiService.PostOkAsync("/users/delete", new { id = user.Id });
            if (!ok) { StatusMessage = $"삭제 실패: {error}"; return; }
            StatusMessage = $"'{user.Username}' 사용자가 삭제되었습니다.";
            AppLogService.Warning("관리", $"사용자 삭제: {user.DisplayName}({user.Username})");
            if (IsEditing && _selectedUser?.Id == user.Id) CancelEdit();
            await LoadUsersAsync();
        }
        finally { IsBusy = false; }
    }

    private async Task ToggleActiveAsync(CmsUser user)
    {
        IsBusy = true;
        try
        {
            var (_, error) = await ApiService.PostAsync<CmsUser>("/users",
                new { id = user.Id, isActive = !user.IsActive });
            if (error != null) { StatusMessage = $"상태 변경 실패: {error}"; return; }
            StatusMessage = $"'{user.Username}' 계정 {(!user.IsActive ? "활성화" : "비활성화")} 완료";
            await LoadUsersAsync();
        }
        finally { IsBusy = false; }
    }
}
