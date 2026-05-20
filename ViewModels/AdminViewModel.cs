using System.Collections.ObjectModel;
using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;
using Postgrest;

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

        _ = LoadUsersAsync();
        _ = LoadLogsAsync();
    }

    public async Task LoadUsersAsync()
    {
        IsBusy = true;
        StatusMessage = "";
        try
        {
            var response = await SupabaseService.Client.From<CmsUser>()
                .Order("created_at", Constants.Ordering.Ascending)
                .Get();
            Users = new ObservableCollection<CmsUser>(response.Models);
        }
        catch (Exception ex) { StatusMessage = $"목록 로드 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task LoadLogsAsync()
    {
        IsLogsBusy = true;
        try
        {
            var response = await SupabaseService.Client.From<LoginLog>()
                .Order("logged_at", Constants.Ordering.Descending)
                .Limit(200)
                .Get();
            LoginLogs = new ObservableCollection<LoginLog>(response.Models);
        }
        catch (Exception ex) { StatusMessage = $"이력 로드 실패: {ex.Message}"; }
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
            if (IsAddingNew)
            {
                if (string.IsNullOrWhiteSpace(NewPassword)) { StatusMessage = "비밀번호를 입력하세요."; return; }
                var newUser = new CmsUser
                {
                    Username     = EditUsername.Trim(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword, 11),
                    Role         = EditRole,
                    DisplayName  = EditDisplayName.Trim(),
                    IsActive     = true
                };
                await SupabaseService.Client.From<CmsUser>().Insert(newUser);
                StatusMessage = $"'{EditUsername}' 사용자가 추가되었습니다.";
            }
            else if (_selectedUser != null)
            {
                _selectedUser.DisplayName = EditDisplayName.Trim();
                _selectedUser.Role        = EditRole;
                if (!string.IsNullOrWhiteSpace(NewPassword))
                    _selectedUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword, 11);
                await SupabaseService.Client.From<CmsUser>().Update(_selectedUser);
                StatusMessage = $"'{EditUsername}' 사용자가 수정되었습니다.";
            }
            IsEditing = false;
            await LoadUsersAsync();
        }
        catch (Exception ex) { StatusMessage = $"저장 실패: {ex.Message}"; }
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
        IsBusy = true;
        try
        {
            await SupabaseService.Client.From<CmsUser>()
                .Filter("id", Constants.Operator.Equals, user.Id)
                .Delete();
            StatusMessage = $"'{user.Username}' 사용자가 삭제되었습니다.";
            if (IsEditing && _selectedUser?.Id == user.Id) CancelEdit();
            await LoadUsersAsync();
        }
        catch (Exception ex) { StatusMessage = $"삭제 실패: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ToggleActiveAsync(CmsUser user)
    {
        IsBusy = true;
        try
        {
            user.IsActive = !user.IsActive;
            await SupabaseService.Client.From<CmsUser>().Update(user);
            StatusMessage = $"'{user.Username}' 계정 {(user.IsActive ? "활성화" : "비활성화")} 완료";
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            user.IsActive = !user.IsActive;
            StatusMessage = $"상태 변경 실패: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}
