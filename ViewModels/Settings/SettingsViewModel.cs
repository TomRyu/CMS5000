using CMS5000.Models;
using CMS5000.Services;
using CMS5000.ViewModels.Base;

namespace CMS5000.ViewModels.Settings;

public class SettingsViewModel : ViewModelBase
{
    private FontSizePreset _fontSizePreset = FontSizePreset.Medium;

    public FontSizePreset FontSizePreset
    {
        get => _fontSizePreset;
        set
        {
            if (!SetProperty(ref _fontSizePreset, value)) return;
            OnPropertyChanged(nameof(IsSmall));
            OnPropertyChanged(nameof(IsMedium));
            OnPropertyChanged(nameof(IsLarge));
            FontSizeManager.Apply(value);
            _ = UserSettingsService.SaveFontSizeAsync(value);
        }
    }

    public bool IsSmall  => _fontSizePreset == FontSizePreset.Small;
    public bool IsMedium => _fontSizePreset == FontSizePreset.Medium;
    public bool IsLarge  => _fontSizePreset == FontSizePreset.Large;

    public RelayCommand<string> SetFontSizeCommand { get; }
    public RelayCommand CheckUpdateCommand { get; }

    // ── 비밀번호 변경 ──────────────────────────────
    private string _changePwStatus = "";
    private bool   _changePwSuccess;
    private bool   _isChangePwBusy;
    public string ChangePwStatus  { get => _changePwStatus;  set { SetProperty(ref _changePwStatus, value); OnPropertyChanged(nameof(HasChangePwStatus)); } }
    public bool   ChangePwSuccess { get => _changePwSuccess; set => SetProperty(ref _changePwSuccess, value); }
    public bool   IsChangePwBusy  { get => _isChangePwBusy;  set { SetProperty(ref _isChangePwBusy, value); OnPropertyChanged(nameof(CanChangePw)); } }
    public bool   HasChangePwStatus => !string.IsNullOrEmpty(_changePwStatus);
    public bool   CanChangePw       => !_isChangePwBusy;

    /// <summary>코드비하인드(PasswordBox)에서 호출. 성공 시 true 반환(입력칸 비우기 용).</summary>
    public async Task<bool> ChangePasswordAsync(string current, string newPwd, string confirm)
    {
        IsChangePwBusy = true;
        ChangePwStatus = "";
        var (success, error) = await AuthService.ChangePasswordAsync(current, newPwd, confirm);
        IsChangePwBusy = false;
        ChangePwSuccess = success;
        ChangePwStatus  = success ? "비밀번호가 변경되었습니다." : error;
        if (success)
            AppLogService.Success("계정", "비밀번호 변경", AuthService.CurrentUser?.DisplayName);
        else
            AppLogService.Warning("계정", $"비밀번호 변경 실패: {error}", AuthService.CurrentUser?.DisplayName);
        return success;
    }

    // ── 세션 타임아웃 ──────────────────────────────
    public record TimeoutOption(int Value, string Label);

    public List<TimeoutOption> TimeoutOptions { get; } =
    [
        new(0,  "사용 안 함"),
        new(5,  "5분"),
        new(10, "10분"),
        new(15, "15분"),
        new(30, "30분"),
        new(60, "60분"),
    ];

    public int SessionTimeoutMinutes
    {
        get => LocalSettingsService.Current.SessionTimeoutMinutes;
        set
        {
            if (LocalSettingsService.Current.SessionTimeoutMinutes == value) return;
            LocalSettingsService.Current.SessionTimeoutMinutes = value;
            LocalSettingsService.Save();
            OnPropertyChanged();
            AppLogService.Info("설정", value <= 0 ? "자동 로그아웃 끔" : $"자동 로그아웃 {value}분");
        }
    }

    // 프로그램 정보 / 버전 이력
    public string CurrentVersion { get; } = UpdateService.GetCurrentVersionText();
    public List<ChangelogEntry> Changelog { get; } = ChangelogService.Load();

    private string _updateStatus = "";
    public string UpdateStatus { get => _updateStatus; set => SetProperty(ref _updateStatus, value); }

    public SettingsViewModel()
    {
        SetFontSizeCommand = new RelayCommand<string>(preset =>
        {
            if (Enum.TryParse<FontSizePreset>(preset, out var p))
                FontSizePreset = p;
        });

        CheckUpdateCommand = new RelayCommand(_ => _ = CheckUpdateAsync());
    }

    private async Task CheckUpdateAsync()
    {
        await UpdateService.CheckDownloadAndApplyAsync(
            status => UpdateStatus = status,
            versionToApply =>
            {
                var result = System.Windows.MessageBox.Show(
                    $"새 버전 v{versionToApply} 업데이트가 준비되었습니다.\n지금 재시작해서 적용하시겠습니까?",
                    "업데이트 준비 완료",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);
                return result == System.Windows.MessageBoxResult.Yes;
            });
    }

    public string DisplayName { get; private set; } = "";
    public string Username    { get; private set; } = "";
    public string RoleName    { get; private set; } = "";

    public void LoadFromCurrentUser()
    {
        var user = AuthService.CurrentUser;
        DisplayName = user?.DisplayName ?? "";
        Username    = user?.Username ?? "";
        RoleName    = user?.Role switch
        {
            "Admin"       => "관리자",
            "Expert"      => "진단전문가",
            "Maintenance" => "정비담당자",
            _             => "운전자"
        };
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(RoleName));

        var preset = FontSizeManager.Current;
        _fontSizePreset = preset;
        OnPropertyChanged(nameof(FontSizePreset));
        OnPropertyChanged(nameof(IsSmall));
        OnPropertyChanged(nameof(IsMedium));
        OnPropertyChanged(nameof(IsLarge));
        FontSizeManager.Apply(preset);
    }
}
